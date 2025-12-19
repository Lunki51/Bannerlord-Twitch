using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using BannerlordTwitch.Util;

namespace BLTAdoptAHero.UI
{
    public class MapHub : Hub
    {
        private static MapData currentMapData = null;
        private static DateTime lastUpdate = DateTime.MinValue;
        private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(60);

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
            public string Type { get; set; } // "land", "sea", "blocked"
            public List<float[]> Points { get; set; } = new();
        }

        public override Task OnConnected()
        {
            Refresh();
            return base.OnConnected();
        }

        public void Refresh()
        {
            if (currentMapData != null)
            {
                Clients.Caller.updateMap(currentMapData);
            }
        }

        public static void UpdateMapData()
        {
            if (DateTime.Now - lastUpdate < UpdateInterval || Mission.Current != null)
                return;

            try
            {
                if (Campaign.Current == null)
                    return;
                

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

                // Get map bounds for normalization
                var mapBounds = GetMapBounds();

                // Get all settlements
                mapData.Settlements = Campaign.Current.Settlements
                    .Where(s => s.Position.X != 0 || s.Position.Y != 0)
                    .Select(s => new SettlementData
                    {
                        Id = s.StringId ?? s.Name?.ToString() ?? "unknown",
                        Name = s.Name?.ToString() ?? "Unknown",
                        Type = s.IsTown ? "Town" : s.IsCastle ? "Castle" : "Village",
                        KingdomId = s.OwnerClan?.Kingdom?.StringId,
                        X = NormalizeX(s.Position.X, mapBounds),
                        Y = NormalizeY(s.Position.Y, mapBounds)
                    })
                    .ToList();

                // Add terrain zones (approximate for Calradia)
                mapData.TerrainZones = GetTerrainZones();

                currentMapData = mapData;
                lastUpdate = DateTime.Now;

                // Broadcast to all connected clients
                var context = GlobalHost.ConnectionManager.GetHubContext<MapHub>();
                if (Mission.Current != null)
                {
                    // Tell the overlay to hide

                    context.Clients.All.updateMap(null);
                    return;
                }
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
            // Get bounds from all settlements
            var settlements = Campaign.Current.Settlements.ToList();
            if (!settlements.Any())
                return (0, 1000, 0, 1000); // Default fallback

            var minX = settlements.Min(s => s.Position.X);
            var maxX = settlements.Max(s => s.Position.X);
            var minY = settlements.Min(s => s.Position.Y);
            var maxY = settlements.Max(s => s.Position.Y);

            // Add padding
            var paddingX = (maxX - minX) * 0.1f;
            var paddingY = (maxY - minY) * 0.1f;

            return (minX - paddingX, maxX + paddingX, minY - paddingY, maxY + paddingY);
        }

        private static float NormalizeX(float x, (float minX, float maxX, float minY, float maxY) bounds)
        {
            return ((x - bounds.minX) / (bounds.maxX - bounds.minX)) * 100f;
        }

        private static float NormalizeY(float y, (float minX, float maxX, float minY, float maxY) bounds)
        {
            float height = bounds.maxY - bounds.minY;
            if (height == 0) return 50f;

            // Convert game coordinates to 0-100% range
            // Bannerlord Y is vertical on the map
            float normalizedY = (y - bounds.minY) / height;

            // Invert for SVG (SVG 0 is top, Game 0 is bottom)
            return (1f - normalizedY) * 100f;
        }

        private static string ColorToHex(uint color)
        {
            // Convert TaleWorlds color format to hex
            // Bannerlord uses ARGB format
            var r = (color >> 16) & 0xFF;
            var g = (color >> 8) & 0xFF;
            var b = color & 0xFF;
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private static List<TerrainZone> GetTerrainZones()
        {
            // Define approximate terrain zones for Calradia based on typical map layout
            var zones = new List<TerrainZone>();

            // Western Sea (left side)
            zones.Add(new TerrainZone
            {
                Type = "sea",
                Points = new List<float[]>
                {
                    new float[] { 0, 0 },
                    new float[] { 8, 0 },
                    new float[] { 8, 100 },
                    new float[] { 0, 100 }
                }
            });

            // Southern Sea (bottom)
            zones.Add(new TerrainZone
            {
                Type = "sea",
                Points = new List<float[]>
                {
                    new float[] { 8, 88 },
                    new float[] { 100, 88 },
                    new float[] { 100, 100 },
                    new float[] { 8, 100 }
                }
            });

            // Eastern Edge (blocked - mountains)
            zones.Add(new TerrainZone
            {
                Type = "blocked",
                Points = new List<float[]>
                {
                    new float[] { 92, 0 },
                    new float[] { 100, 0 },
                    new float[] { 100, 100 },
                    new float[] { 92, 100 }
                }
            });

            // Northern Edge (blocked - ice/mountains)
            zones.Add(new TerrainZone
            {
                Type = "blocked",
                Points = new List<float[]>
                {
                    new float[] { 0, 0 },
                    new float[] { 100, 0 },
                    new float[] { 100, 5 },
                    new float[] { 0, 5 }
                }
            });

            // Central playable land
            zones.Add(new TerrainZone
            {
                Type = "land",
                Points = new List<float[]>
                {
                    new float[] { 8, 5 },
                    new float[] { 92, 5 },
                    new float[] { 92, 88 },
                    new float[] { 8, 88 }
                }
            });

            return zones;
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