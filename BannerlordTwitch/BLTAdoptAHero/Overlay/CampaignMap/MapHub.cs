using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using BannerlordTwitch.Util;
using TaleWorlds.Library;

namespace BLTAdoptAHero.UI
{
    public class MapHub : Hub
    {
        private static MapData currentMapData = null;
        private static DateTime lastUpdate = DateTime.MinValue;
        private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(5);
        private static Mission lastMission = null;

        // Overlay dimensions for aspect ratio calculation
        private const float OVERLAY_WIDTH = 100f;
        private const float OVERLAY_HEIGHT = 95f;
        private const float OVERLAY_ASPECT_RATIO = OVERLAY_WIDTH / OVERLAY_HEIGHT; // 3.33

        public class MapData
        {
            public List<KingdomData> Kingdoms { get; set; } = new();
            public List<SettlementData> Settlements { get; set; } = new();
            public List<TerrainZone> TerrainZones { get; set; } = new();
        }

        public class KingdomData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Color { get; set; }
        }

        public class SettlementData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public string KingdomId { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
        }

        public class TerrainZone
        {
            public string Type { get; set; }
            public List<float[]> Points { get; set; } = new();
        }

        public override Task OnConnected()
        {
            Refresh();
            return base.OnConnected();
        }

        public void Refresh()
        {
            // Check if map overlay is disabled in settings
            if (BLTAdoptAHeroModule.CommonConfig?.ShowCampaignMapOverlay != true)
            {
                Clients.Caller.updateMap(null);
                return;
            }

            // Check if in mission - always respond immediately
            if (Mission.Current != null || Campaign.Current?.MapSceneWrapper == null)
            {
                Clients.Caller.updateMap(null);
                return;
            }

            // Send current data if we have it, otherwise trigger update
            if (currentMapData != null)
            {
                Clients.Caller.updateMap(currentMapData);
            }
            else
            {
                // Force immediate update
                UpdateMapDataInternal(true);
                Clients.Caller.updateMap(currentMapData);
            }
        }

        public static void UpdateMapData()
        {
            UpdateMapDataInternal(false);
        }

        private static void UpdateMapDataInternal(bool forceUpdate)
        {
            var context = GlobalHost.ConnectionManager.GetHubContext<MapHub>();

            // Check if map overlay is disabled in settings
            if (BLTAdoptAHeroModule.CommonConfig?.ShowCampaignMapOverlay != true)
            {
                if (currentMapData != null)
                {
                    context.Clients.All.updateMap(null);
                    currentMapData = null;
                    lastMission = null;
                }
                return;
            }

            // Check mission status - if it changed, update immediately
            bool missionChanged = lastMission != Mission.Current;
            lastMission = Mission.Current;

            // Check if in mission or not on campaign map
            if (Mission.Current != null || Campaign.Current?.MapSceneWrapper == null)
            {
                // In mission or not on campaign map - hide the map
                if (currentMapData != null || missionChanged)
                {
                    context.Clients.All.updateMap(null);
                    currentMapData = null;
                    Log.Trace("[MapHub] Map hidden - in mission or not on campaign map");
                }
                return;
            }

            // If we just left a mission, update immediately
            if (missionChanged)
            {
                forceUpdate = true;
                Log.Trace("[MapHub] Mission ended, forcing map update");
            }

            // Check throttle (unless forced)
            if (!forceUpdate && DateTime.Now - lastUpdate < UpdateInterval && currentMapData != null)
                return;

            try
            {
                if (Campaign.Current == null)
                {
                    context.Clients.All.updateMap(null);
                    currentMapData = null;
                    return;
                }

                var mapData = new MapData();

                // Get all active kingdoms
                mapData.Kingdoms = Campaign.Current.Kingdoms
                    .Where(k => !k.IsEliminated && k.StringId != null)
                    .Select(k => new KingdomData
                    {
                        Id = k.StringId,
                        Name = k.Name?.ToString() ?? "Unknown",
                        Color = ColorToHex(k.Color)
                    })
                    .ToList();

                // Get map bounds
                var mapBounds = GetMapBounds();

                // Get all settlements (only towns and castles, no villages)
                var rawSettlements = Campaign.Current.Settlements
                    .Where(s => (s.IsTown || s.IsCastle) && (s.Position.X != 0 || s.Position.Y != 0))
                    .ToList();

                // Normalize settlements to fit the viewBox (0-100 width, 0-30 height)
                var settlements = new List<SettlementData>();
                foreach (var s in rawSettlements)
                {
                    var settlement = new SettlementData
                    {
                        Id = s.StringId ?? s.Name?.ToString() ?? "unknown",
                        Name = s.Name?.ToString() ?? "Unknown",
                        Type = s.IsTown ? "Town" : "Castle",
                        KingdomId = s.OwnerClan?.Kingdom?.StringId,
                        X = NormalizeX(s.Position.X, mapBounds),
                        Y = NormalizeY(s.Position.Y, mapBounds)
                    };

                    // Only separate overlapping settlements
                    var (spreadX, spreadY) = SpreadSettlement(settlement.X, settlement.Y, settlements);
                    settlement.X = spreadX;
                    settlement.Y = spreadY;

                    settlements.Add(settlement);
                }

                mapData.Settlements = settlements;

                currentMapData = mapData;
                lastUpdate = DateTime.Now;

                // Broadcast to all connected clients
                context.Clients.All.updateMap(mapData);

                Log.Trace($"[MapHub] Updated map data: {mapData.Kingdoms.Count} kingdoms, {mapData.Settlements.Count} settlements");
            }
            catch (Exception ex)
            {
                Log.Error($"[MapHub] Error updating map data: {ex.Message}");
            }
        }

        private static (float minX, float maxX, float minY, float maxY) GetMapBounds()
        {
            var settlements = Campaign.Current.Settlements
                .Where(s => (s.IsTown || s.IsCastle) && (s.Position.X != 0 || s.Position.Y != 0))
                .ToList();

            if (!settlements.Any())
                return (0, 1000, 0, 1000);

            // Get the actual min/max positions
            var minX = settlements.Min(s => s.Position.X);
            var maxX = settlements.Max(s => s.Position.X);
            var minY = settlements.Min(s => s.Position.Y);
            var maxY = settlements.Max(s => s.Position.Y);

            // Add just a tiny bit of padding so edge settlements aren't right on the border
            float dataWidth = maxX - minX;
            float dataHeight = maxY - minY;

            float paddingX = dataWidth * 0.02f; // 2% padding
            float paddingY = dataHeight * 0.02f;

            return (minX - paddingX, maxX + paddingX, minY - paddingY, maxY + paddingY);
        }

        private static float NormalizeX(float x, (float minX, float maxX, float minY, float maxY) bounds)
        {
            float width = bounds.maxX - bounds.minX;
            if (width == 0) return 50f;

            // Map to viewBox width (0-100) with margins (5-95)
            float normalized = (x - bounds.minX) / width;
            return 5f + (normalized * 90f);
        }

        private static float NormalizeY(float y, (float minX, float maxX, float minY, float maxY) bounds)
        {
            float height = bounds.maxY - bounds.minY;
            if (height == 0) return 15f;

            float normalized = (y - bounds.minY) / height;
            // Invert for SVG (SVG 0 is top, Game 0 is bottom)
            // Map to viewBox height (0-30) with margins (2-28)
            return 2f + ((1f - normalized) * 91f);
        }

        // Only separate overlapping settlements, don't distort the map
        private static (float x, float y) SpreadSettlement(float x, float y, List<SettlementData> existingSettlements)
        {
            const float minDistance = 2.5f; // Minimum distance between settlements in viewBox units
            const int maxIterations = 5;

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                bool moved = false;

                foreach (var other in existingSettlements)
                {
                    float dx = x - other.X;
                    float dy = y - other.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                    if (dist < minDistance && dist > 0.01f)
                    {
                        // Push away just enough to separate
                        float overlap = minDistance - dist;
                        float pushX = (dx / dist) * overlap * 0.5f;
                        float pushY = (dy / dist) * overlap * 0.5f;
                        x += pushX;
                        y += pushY;
                        moved = true;
                    }
                }

                // Keep within viewBox bounds with margins
                x = Math.Max(3f, Math.Min(97f, x));
                y = Math.Max(1f, Math.Min(94f, y));

                if (!moved) break;
            }

            return (x, y);
        }

        private static string ColorToHex(uint color)
        {
            // Convert TaleWorlds ARGB format to hex
            var r = (color >> 16) & 0xFF;
            var g = (color >> 8) & 0xFF;
            var b = color & 0xFF;
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        public static void Register()
        {
            BLTOverlay.BLTOverlay.Register("campaign-map", 0,
                GetContent("CampaignMap.css"),
                GetContent("CampaignMap.html"),
                GetContent("CampaignMap.js"));
        }

        private static string GetContent(string fileName)
        {
            var path = Path.Combine(
                Path.GetDirectoryName(typeof(MapHub).Assembly.Location) ?? ".",
                "Overlay", "CampaignMap", fileName);
            return File.ReadAllText(path);
        }
    }
}