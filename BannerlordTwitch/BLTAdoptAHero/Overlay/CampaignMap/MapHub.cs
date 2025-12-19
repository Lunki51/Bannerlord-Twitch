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
            // Check if map overlay is disabled in settings
            if (BLTAdoptAHeroModule.CommonConfig?.ShowCampaignMapOverlay != true)
            {
                Clients.Caller.updateMap(null);
                return;
            }

            // Check if in mission - check both Mission.Current and if we're in campaign map
            if (Mission.Current != null || Campaign.Current?.MapSceneWrapper == null)
            {
                Clients.Caller.updateMap(null);
                return;
            }

            if (currentMapData != null)
            {
                Clients.Caller.updateMap(currentMapData);
            }
        }

        public static void UpdateMapData()
        {
            var context = GlobalHost.ConnectionManager.GetHubContext<MapHub>();

            // Check if map overlay is disabled in settings
            if (BLTAdoptAHeroModule.CommonConfig?.ShowCampaignMapOverlay != true)
            {
                context.Clients.All.updateMap(null);
                currentMapData = null;
                return;
            }

            // Check mission status and campaign map availability
            if (Mission.Current != null || Campaign.Current?.MapSceneWrapper == null)
            {
                // In mission or not on campaign map - hide the map
                context.Clients.All.updateMap(null);
                currentMapData = null;
                return;
            }

            if (DateTime.Now - lastUpdate < UpdateInterval)
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

                // Get map bounds for normalization
                var mapBounds = GetMapBounds();

                // Get all settlements
                mapData.Settlements = Campaign.Current.Settlements
                    .Where(s => s.Position.X != 0 || s.Position.Y != 0 && !s.IsVillage)
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

                // Generate terrain zones based on actual navigation data
                mapData.TerrainZones = GenerateTerrainZones(mapBounds);

                currentMapData = mapData;
                lastUpdate = DateTime.Now;

                // Broadcast to all connected clients
                context.Clients.All.updateMap(mapData);

                Log.Trace($"[MapHub] Updated map data: {mapData.Kingdoms.Count} kingdoms, {mapData.Settlements.Count} settlements, {mapData.TerrainZones.Count} terrain zones");
            }
            catch (Exception ex)
            {
                Log.Error($"[MapHub] Error updating map data: {ex.Message}");
            }
        }

        private static (float minX, float maxX, float minY, float maxY) GetMapBounds()
        {
            var settlements = Campaign.Current.Settlements.ToList();
            if (!settlements.Any())
                return (0, 1000, 0, 1000);

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

            float normalizedY = (y - bounds.minY) / height;
            // Invert for SVG (SVG 0 is top, Game 0 is bottom)
            return (1f - normalizedY) * 100f;
        }

        private static string ColorToHex(uint color)
        {
            // Convert TaleWorlds ARGB format to hex
            var r = (color >> 16) & 0xFF;
            var g = (color >> 8) & 0xFF;
            var b = color & 0xFF;
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private static List<TerrainZone> GenerateTerrainZones((float minX, float maxX, float minY, float maxY) bounds)
        {
            var zones = new List<TerrainZone>();

            try
            {
                // Sample the map in a grid to detect terrain types
                const int gridSize = 20; // 20x20 grid
                var terrainGrid = new string[gridSize, gridSize];

                for (int x = 0; x < gridSize; x++)
                {
                    for (int y = 0; y < gridSize; y++)
                    {
                        // Convert grid position to world position
                        float worldX = bounds.minX + (x / (float)gridSize) * (bounds.maxX - bounds.minX);
                        float worldY = bounds.minY + (y / (float)gridSize) * (bounds.maxY - bounds.minY);

                        //var pos = new CampaignVec2(worldXY, worldY);
                        //var terrainType = GetTerrainTypeAtPosition(pos);
                        //terrainGrid[x, y] = terrainType;
                    }
                }

                // Group adjacent cells of same type into zones
                var visited = new bool[gridSize, gridSize];
                for (int x = 0; x < gridSize; x++)
                {
                    for (int y = 0; y < gridSize; y++)
                    {
                        if (!visited[x, y])
                        {
                            var zone = FloodFill(terrainGrid, visited, x, y, gridSize);
                            if (zone.Points.Count >= 3)
                            {
                                zones.Add(zone);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MapHub] Error generating terrain zones: {ex.Message}");
                // Return basic fallback zones
                return GetFallbackTerrainZones();
            }

            return zones.Any() ? zones : GetFallbackTerrainZones();
        }

        private static string GetTerrainTypeAtPosition(CampaignVec2 position)
        {
            try
            {
                // Try to get terrain type from the game's navigation system
                var scene = Campaign.Current?.MapSceneWrapper;
                if (scene != null)
                {
                    // Check if position is on water using Campaign's terrain data
                    // The game uses different navigation face types
                    var terrainType = Campaign.Current.MapSceneWrapper.GetTerrainTypeAtPosition(position);

                    // Map terrain type to our zone types
                    // TerrainType.Water, TerrainType.Mountain, TerrainType.Bridge, etc.
                    if (terrainType == TerrainType.Water || terrainType == TerrainType.River ||
                        terrainType == TerrainType.Lake || terrainType == TerrainType.Bridge)
                        return "sea";
                    if (terrainType == TerrainType.Mountain || terrainType == TerrainType.Canyon)
                        return "blocked";

                    return "land";
                }
            }
            catch
            {
                // Fallback: check if near water settlements or edge of map
            }

            return "land";
        }

        private static TerrainZone FloodFill(string[,] grid, bool[,] visited, int startX, int startY, int gridSize)
        {
            var terrainType = grid[startX, startY];
            var points = new List<(int x, int y)>();
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue((startX, startY));
            visited[startX, startY] = true;

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                points.Add((x, y));

                // Check 4-directional neighbors
                var neighbors = new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) };
                foreach (var (nx, ny) in neighbors)
                {
                    if (nx >= 0 && nx < gridSize && ny >= 0 && ny < gridSize &&
                        !visited[nx, ny] && grid[nx, ny] == terrainType)
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }

            // Convert grid points to normalized polygon
            var zone = new TerrainZone { Type = terrainType };
            var hull = GetConvexHull(points);
            foreach (var (x, y) in hull)
            {
                zone.Points.Add(new float[] { x * 5f, y * 5f }); // Scale to 0-100 range
            }

            return zone;
        }

        private static List<(int x, int y)> GetConvexHull(List<(int x, int y)> points)
        {
            // Simple convex hull for zone boundary
            if (points.Count < 3) return points;

            var sorted = points.OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            var hull = new List<(int x, int y)>();

            // Lower hull
            for (int i = 0; i < sorted.Count; i++)
            {
                while (hull.Count >= 2 && CrossProduct(hull[hull.Count - 2], hull[hull.Count - 1], sorted[i]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(sorted[i]);
            }

            // Upper hull
            int lowerSize = hull.Count;
            for (int i = sorted.Count - 2; i >= 0; i--)
            {
                while (hull.Count > lowerSize && CrossProduct(hull[hull.Count - 2], hull[hull.Count - 1], sorted[i]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(sorted[i]);
            }

            hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        private static long CrossProduct((int x, int y) o, (int x, int y) a, (int x, int y) b)
        {
            return (long)(a.x - o.x) * (b.y - o.y) - (long)(a.y - o.y) * (b.x - o.x);
        }

        private static List<TerrainZone> GetFallbackTerrainZones()
        {
            var zones = new List<TerrainZone>();

            // Western Sea
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

            // Southern Sea
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

            // Eastern Edge (mountains)
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

            // Northern Edge
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

            // Central land
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
