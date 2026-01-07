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
                float worldWidth = mapBounds.maxX - mapBounds.minX;
                float worldHeight = mapBounds.maxY - mapBounds.minY;

                Log.Trace($"[MapHub] World bounds: X({mapBounds.minX:F1} to {mapBounds.maxX:F1}) Width:{worldWidth:F1}, Y({mapBounds.minY:F1} to {mapBounds.maxY:F1}) Height:{worldHeight:F1}");

                // Get all settlements (only towns and castles, no villages)
                var rawSettlements = Campaign.Current.Settlements
                    .Where(s => (s.IsTown || s.IsCastle) && (s.Position.X != 0 || s.Position.Y != 0))
                    .ToList();

                // Normalize settlements
                var settlements = new List<SettlementData>();
                float minNormX = 100f, maxNormX = 0f;
                float minNormY = 95f, maxNormY = 0f;

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

                    // Track actual normalized range BEFORE spreading
                    minNormX = Math.Min(minNormX, settlement.X);
                    maxNormX = Math.Max(maxNormX, settlement.X);
                    minNormY = Math.Min(minNormY, settlement.Y);
                    maxNormY = Math.Max(maxNormY, settlement.Y);

                    // Spread overlapping settlements
                    var (spreadX, spreadY) = SpreadSettlement(settlement.X, settlement.Y, settlements);
                    settlement.X = spreadX;
                    settlement.Y = spreadY;

                    settlements.Add(settlement);
                }

                float normWidth = maxNormX - minNormX;
                float normHeight = maxNormY - minNormY;

                Log.Trace($"[MapHub] Normalized BEFORE spread: X({minNormX:F1} to {maxNormX:F1}) Width:{normWidth:F1}, Y({minNormY:F1} to {maxNormY:F1}) Height:{normHeight:F1}");

                // Track AFTER spreading
                minNormX = settlements.Min(s => s.X);
                maxNormX = settlements.Max(s => s.X);
                minNormY = settlements.Min(s => s.Y);
                maxNormY = settlements.Max(s => s.Y);
                normWidth = maxNormX - minNormX;
                normHeight = maxNormY - minNormY;

                Log.Trace($"[MapHub] Normalized AFTER spread: X({minNormX:F1} to {maxNormX:F1}) Width:{normWidth:F1}, Y({minNormY:F1} to {maxNormY:F1}) Height:{normHeight:F1}");

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

            // Get EXACT bounds - no padding here
            var minX = settlements.Min(s => s.Position.X);
            var maxX = settlements.Max(s => s.Position.X);
            var minY = settlements.Min(s => s.Position.Y);
            var maxY = settlements.Max(s => s.Position.Y);

            return (minX, maxX, minY, maxY);
        }

        private static float NormalizeX(float x, (float minX, float maxX, float minY, float maxY) bounds)
        {
            float width = bounds.maxX - bounds.minX;
            if (width == 0) return 50f;

            // Normalize to 0-1 range
            float normalized = (x - bounds.minX) / width;

            // STRETCH TO FULL WIDTH: Map to 0-100 (edge to edge)
            return normalized * 100f;
        }

        private static float NormalizeY(float y, (float minX, float maxX, float minY, float maxY) bounds)
        {
            float height = bounds.maxY - bounds.minY;
            if (height == 0) return 47.5f;

            // Normalize to 0-1 range
            float normalized = (y - bounds.minY) / height;

            // Invert for SVG (SVG 0 is top, Game 0 is bottom)
            // Add padding: map to 5-90 instead of 0-95 (5 units padding top/bottom)
            return 5f + ((1f - normalized) * 85f);
        }

        // Minimal spreading - we want to maintain the stretched layout
        private static (float x, float y) SpreadSettlement(float x, float y, List<SettlementData> existingSettlements)
        {
            const float minDistance = 0.5f; // Minimum distance between settlements
            const int maxIterations = 100; // Minimal iterations

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
                        // Small push to separate overlapping settlements
                        float overlap = minDistance - dist;
                        float pushX = (dx / dist) * overlap * 0.2f;
                        float pushY = (dy / dist) * overlap * 0.3f;
                        x += pushX;
                        y += pushY;
                        moved = true;
                    }
                }

                // Keep within bounds - full X range, padded Y range
                x = Math.Max(4f, Math.Min(96f, x));
                y = Math.Max(5f, Math.Min(90f, y));

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