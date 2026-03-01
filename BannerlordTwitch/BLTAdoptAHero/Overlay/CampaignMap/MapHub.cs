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
        private static readonly TimeSpan UpdateInterval = TimeSpan.FromMinutes(3);
        private static Mission lastMission = null;

        private const float OVERLAY_WIDTH = 100f;
        private const float OVERLAY_HEIGHT = 95f;
        private const float OVERLAY_ASPECT_RATIO = OVERLAY_WIDTH / OVERLAY_HEIGHT;

        public static MapHub.MapData CurrentMapData => currentMapData;
        private static List<CoastlineSegment> _cachedCoastline = null;
        private static List<SettlementData> _cachedSettlements = null;

        public class MapData
        {
            public List<KingdomData> Kingdoms { get; set; } = new();
            public List<SettlementData> Settlements { get; set; } = new();
            public List<CoastlineSegment> Coastline { get; set; } = new();
        }

        public class KingdomData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Color1 { get; set; }
            public string Color2 { get; set; }
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

        public class CoastlineSegment
        {
            public float X1 { get; set; }
            public float Y1 { get; set; }
            public float X2 { get; set; }
            public float Y2 { get; set; }
        }

        public override Task OnConnected()
        {
            Refresh();
            return base.OnConnected();
        }

        public void Refresh()
        {
            if (BLTAdoptAHeroModule.CommonConfig?.ShowCampaignMapOverlay != true)
            {
                Clients.Caller.updateMap(null);
                return;
            }

            if (Mission.Current != null || Campaign.Current?.MapSceneWrapper == null)
            {
                Clients.Caller.updateMap(null);
                return;
            }

            if (currentMapData != null)
            {
                Clients.Caller.updateMap(currentMapData);
            }
            else
            {
                UpdateMapDataInternal(true);
                Clients.Caller.updateMap(currentMapData);
            }
        }

        private static string GetKingdomColor(Kingdom k, bool first)
        {
            if (first)
            {
                uint color = (k.Color != 0 && (k.Color & 0x00FFFFFF) != 0)
                    ? k.Color
                    : k.RulingClan.Color;
                return ColorToHex(color | 0xFF000000);
            }
            else
            {
                uint color = (k.Color2 != 0 && (k.Color2 & 0x00FFFFFF) != 0)
                    ? k.Color2
                    : k.RulingClan.Color2;
                return ColorToHex(color | 0xFF000000);
            }
        }

        public static void UpdateMapData()
        {
            UpdateMapDataInternal(false);
        }

        private static void UpdateMapDataInternal(bool forceUpdate)
        {
            var context = GlobalHost.ConnectionManager.GetHubContext<MapHub>();

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

            bool missionChanged = lastMission != Mission.Current;
            lastMission = Mission.Current;

            if (Mission.Current != null || Campaign.Current?.MapSceneWrapper == null)
            {
                if (currentMapData != null || missionChanged)
                {
                    context.Clients.All.updateMap(null);
                    currentMapData = null;
                    Log.Trace("[MapHub] Map hidden - in mission or not on campaign map");
                }
                return;
            }

            if (missionChanged)
            {
                forceUpdate = true;
                Log.Trace("[MapHub] Mission ended, forcing map update");
            }

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

                mapData.Kingdoms = Campaign.Current.Kingdoms
                    .Where(k => !k.IsEliminated && k.StringId != null)
                    .Select(k => new KingdomData
                    {
                        Id = k.StringId,
                        Name = k.Name?.ToString() ?? "Unknown",
                        Color1 = GetKingdomColor(k, true),
                        Color2 = GetKingdomColor(k, false)
                    })
                    .ToList();

                var mapBounds = GetMapBounds();

                var rawSettlements = Campaign.Current.Settlements
                    .Where(s => (s.IsTown || s.IsCastle) && (s.Position.X != 0 || s.Position.Y != 0))
                    .ToList();

                // Only rebuild settlements if they've changed
                if (_cachedSettlements == null)
                {
                    var settlements = new List<SettlementData>();
                    foreach (var s in rawSettlements)
                    {
                        settlements.Add(new SettlementData
                        {
                            Id = s.StringId ?? s.Name?.ToString() ?? "unknown",
                            Name = s.Name?.ToString() ?? "Unknown",
                            Type = s.IsTown ? "Town" : "Castle",
                            KingdomId = s.OwnerClan?.Kingdom?.StringId,
                            X = NormalizeX(s.Position.X, mapBounds),
                            Y = NormalizeY(s.Position.Y, mapBounds)
                        });
                    }
                    //SpreadSettlements(settlements);
                    _cachedSettlements = settlements;
                    Log.Trace($"[MapHub] Settlement positions cached: {_cachedSettlements.Count}");
                }

                // KingdomId can change without positions changing, update that cheaply
                var kingdomLookup = rawSettlements.ToDictionary(
                    s => s.StringId ?? s.Name?.ToString() ?? "unknown",
                    s => s.OwnerClan?.Kingdom?.StringId);
                foreach (var s in _cachedSettlements)
                {
                    if (kingdomLookup.TryGetValue(s.Id, out var kid))
                        s.KingdomId = kid;
                }

                mapData.Settlements = _cachedSettlements;

                if (_cachedCoastline == null || _cachedCoastline.Count == 0)
                {
                    Log.Trace("[MapHub] Generating coastline cache...");
                    _cachedCoastline = GenerateCoastline(mapBounds, _cachedSettlements);
                    Log.Trace($"[MapHub] Coastline cache built: {_cachedCoastline.Count} segments");
                }
                mapData.Coastline = _cachedCoastline;

                currentMapData = mapData;
                lastUpdate = DateTime.Now;

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
                .Where(s => s.IsTown || s.IsCastle)
                .ToList();

            if (!settlements.Any())
                return (0, 1000, 0, 1000);

            var minX = settlements.Min(s => s.Position.X);
            var maxX = settlements.Max(s => s.Position.X);
            var minY = settlements.Min(s => s.Position.Y);
            var maxY = settlements.Max(s => s.Position.Y);

            float width = maxX - minX;
            float height = maxY - minY;

            if (width == 0) width = 1;
            if (height == 0) height = 1;

            float marginPercent = 0.05f; // 5% margin on all sides

            float marginX = width * marginPercent;
            float marginY = height * marginPercent;

            minX -= marginX;
            maxX += marginX;
            minY -= marginY;
            maxY += marginY;

            return (minX, maxX, minY, maxY);
        }

        private static float NormalizeX(float x, (float minX, float maxX, float minY, float maxY) bounds)
        {
            float width = bounds.maxX - bounds.minX;
            if (width == 0) return 50f;
            float normalized = (x - bounds.minX) / width;
            return normalized * 100f;
        }

        private static float NormalizeY(float y, (float minX, float maxX, float minY, float maxY) bounds)
        {
            float height = bounds.maxY - bounds.minY;
            if (height == 0) return 47.5f;
            float normalized = (y - bounds.minY) / height;
            return (1f - normalized) * 100f;
        }

        private static void SpreadSettlements(List<SettlementData> settlements)
        {
            if (settlements.Count == 0) return;

            const float clumpRadius = 8.0f;
            float minSpacing = GlobalCommonConfig.Get().MapOverlayMinSpacing;
            const float spreadBias = 1.4f;
            const float clumpRepelRadius = 10.0f;
            const float clumpRepelStrength = 0.5f;
            const int intraIter = 150;
            const int interIter = 40;

            int n = settlements.Count;

            float[] origX = settlements.Select(s => s.X).ToArray();
            float[] origY = settlements.Select(s => s.Y).ToArray();

            int[] parent = Enumerable.Range(0, n).ToArray();

            int Find(int i)
            {
                while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
                return i;
            }
            void Union(int a, int b)
            {
                a = Find(a); b = Find(b);
                if (a != b) parent[a] = b;
            }

            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    float dx = origX[i] - origX[j];
                    float dy = origY[i] - origY[j];
                    if (dx * dx + dy * dy <= clumpRadius * clumpRadius)
                        Union(i, j);
                }

            var clumps = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!clumps.TryGetValue(root, out var list))
                    clumps[root] = list = new List<int>();
                list.Add(i);
            }

            float targetSpacing = minSpacing * spreadBias;

            foreach (var clump in clumps.Values)
            {
                if (clump.Count <= 1) continue;

                float anchorX = clump.Average(i => origX[i]);
                float anchorY = clump.Average(i => origY[i]);

                for (int iter = 0; iter < intraIter; iter++)
                {
                    bool moved = false;

                    for (int a = 0; a < clump.Count; a++)
                        for (int b = a + 1; b < clump.Count; b++)
                        {
                            int ia = clump[a], ib = clump[b];
                            float dx = settlements[ia].X - settlements[ib].X;
                            float dy = settlements[ia].Y - settlements[ib].Y;
                            float distSq = dx * dx + dy * dy;

                            if (distSq < targetSpacing * targetSpacing)
                            {
                                float dist = distSq > 1e-6f ? (float)Math.Sqrt(distSq) : 0.01f;
                                if (dist < 0.01f)
                                {
                                    dx = 0.3f + a * 0.07f;
                                    dy = 0.2f + b * 0.05f;
                                    dist = (float)Math.Sqrt(dx * dx + dy * dy);
                                }
                                float push = (targetSpacing - dist) * 0.5f;
                                float nx = dx / dist, ny = dy / dist;
                                settlements[ia].X += nx * push;
                                settlements[ia].Y += ny * push;
                                settlements[ib].X -= nx * push;
                                settlements[ib].Y -= ny * push;
                                moved = true;
                            }
                        }

                    float cx = clump.Average(i => settlements[i].X);
                    float cy = clump.Average(i => settlements[i].Y);
                    float shiftX = anchorX - cx;
                    float shiftY = anchorY - cy;
                    foreach (int i in clump)
                    {
                        settlements[i].X += shiftX;
                        settlements[i].Y += shiftY;
                    }

                    if (!moved) break;
                }
            }

            var clumpList = clumps.Values.ToList();
            for (int iter = 0; iter < interIter; iter++)
            {
                bool moved = false;
                for (int a = 0; a < clumpList.Count; a++)
                    for (int b = a + 1; b < clumpList.Count; b++)
                    {
                        float ax = clumpList[a].Average(i => settlements[i].X);
                        float ay = clumpList[a].Average(i => settlements[i].Y);
                        float bx = clumpList[b].Average(i => settlements[i].X);
                        float by = clumpList[b].Average(i => settlements[i].Y);

                        float dx = ax - bx, dy = ay - by;
                        float distSq = dx * dx + dy * dy;

                        if (distSq < clumpRepelRadius * clumpRepelRadius)
                        {
                            float dist = distSq > 1e-6f ? (float)Math.Sqrt(distSq) : 0.1f;
                            float push = (clumpRepelRadius - dist) * clumpRepelStrength;
                            float nx = dx / dist, ny = dy / dist;

                            foreach (int i in clumpList[a]) { settlements[i].X += nx * push; settlements[i].Y += ny * push; }
                            foreach (int i in clumpList[b]) { settlements[i].X -= nx * push; settlements[i].Y -= ny * push; }
                            moved = true;
                        }
                    }
                if (!moved) break;
            }

            foreach (var s in settlements)
            {
                s.X = Math.Max(3f, Math.Min(97f, s.X));
                s.Y = Math.Max(5f, Math.Min(90f, s.Y));
            }
        }

        private static string ColorToHex(uint color)
        {
            var r = (color >> 16) & 0xFF;
            var g = (color >> 8) & 0xFF;
            var b = color & 0xFF;
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private static List<CoastlineSegment> GenerateCoastline(
            (float minX, float maxX, float minY, float maxY) settlementBounds,
            List<SettlementData> normalizedSettlements)
        {
            var map = Campaign.Current?.MapSceneWrapper;
            if (map == null) return new List<CoastlineSegment>();

            map.GetMapBorders(out Vec2 minPos, out Vec2 maxPos, out float _);
            var sampleBounds = (minX: minPos.x, maxX: maxPos.x, minY: minPos.y, maxY: maxPos.y);

            const int GRID_W = 120;
            const int GRID_H = 114;

            float worldW = sampleBounds.maxX - sampleBounds.minX;
            float worldH = sampleBounds.maxY - sampleBounds.minY;
            float cellW = worldW / GRID_W;
            float cellH = worldH / GRID_H;

            var waterValue = new float[GRID_W * GRID_H];
            var isLandRestriction = new bool[GRID_W * GRID_H];
            for (int i = 0; i < waterValue.Length; i++) waterValue[i] = -1f;
            int validSamples = 0;

            for (int gy = 0; gy < GRID_H; gy++)
            {
                float worldY = sampleBounds.minY + (gy + 0.5f) * cellH;
                int rowBase = gy * GRID_W;
                for (int gx = 0; gx < GRID_W; gx++)
                {
                    float worldX = sampleBounds.minX + (gx + 0.5f) * cellW;
                    if (worldX + cellW * 0.5f < settlementBounds.minX || worldX - cellW * 0.5f > settlementBounds.maxX ||
                        worldY + cellH * 0.5f < settlementBounds.minY || worldY - cellH * 0.5f > settlementBounds.maxY)
                    {
                        // leave as -1f
                        continue;
                    }
                    try
                    {
                        var (isWaterCell, terrainType, valid) = SampleTerrain(map, worldX, worldY, cellW, cellH);
                        isLandRestriction[rowBase + gx] = (terrainType == TerrainType.LandRestriction || terrainType == TerrainType.SeaRestriction);
                        waterValue[rowBase + gx] = isWaterCell ? 1f : 0f;
                        if (valid) validSamples++;
                    }
                    catch { waterValue[rowBase + gx] = 1f; }
                }
            }

            Log.Trace($"[MapHub] Coastline sampled {validSamples}/{GRID_W * GRID_H}");

            bool changed = true;
            int passes = 0;
            while (changed && passes < 20)
            {
                changed = false; passes++;
                for (int i = 0; i < waterValue.Length; i++)
                {
                    if (waterValue[i] >= 0f) continue;
                    int gx = i % GRID_W, gy = i / GRID_W;
                    float sum = 0f; int count = 0;
                    if (gx > 0 && waterValue[i - 1] >= 0f) { sum += waterValue[i - 1]; count++; }
                    if (gx < GRID_W - 1 && waterValue[i + 1] >= 0f) { sum += waterValue[i + 1]; count++; }
                    if (gy > 0 && waterValue[i - GRID_W] >= 0f) { sum += waterValue[i - GRID_W]; count++; }
                    if (gy < GRID_H - 1 && waterValue[i + GRID_W] >= 0f) { sum += waterValue[i + GRID_W]; count++; }
                    if (count > 0) { waterValue[i] = sum / count; changed = true; }
                }
            }

            var blurred = new float[GRID_W * GRID_H];
            float[] kernel = { 1f, 2f, 1f, 2f, 4f, 2f, 1f, 2f, 1f };
            const float kernelSum = 16f;
            for (int gy = 1; gy < GRID_H - 1; gy++)
                for (int gx = 1; gx < GRID_W - 1; gx++)
                {
                    float sum = 0f; int k = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            sum += waterValue[(gy + dy) * GRID_W + (gx + dx)] * kernel[k++];
                    blurred[gy * GRID_W + gx] = sum / kernelSum;
                }
            for (int gx = 0; gx < GRID_W; gx++)
            {
                blurred[gx] = waterValue[gx];
                blurred[(GRID_H - 1) * GRID_W + gx] = waterValue[(GRID_H - 1) * GRID_W + gx];
            }
            for (int gy = 0; gy < GRID_H; gy++)
            {
                blurred[gy * GRID_W] = waterValue[gy * GRID_W];
                blurred[gy * GRID_W + GRID_W - 1] = waterValue[gy * GRID_W + GRID_W - 1];
            }

            var isWater = new bool[GRID_W * GRID_H];
            for (int i = 0; i < isWater.Length; i++)
                isWater[i] = blurred[i] >= 0.5f;

            var segments = new List<CoastlineSegment>(GRID_W * GRID_H / 5);

            for (int gy = 0; gy < GRID_H - 1; gy++)
            {
                int rowA = gy * GRID_W, rowB = (gy + 1) * GRID_W;
                float edgeWorldY = sampleBounds.minY + (gy + 1) * cellH;
                float edgeSvgY = NormalizeY(edgeWorldY, settlementBounds);

                for (int gx = 0; gx < GRID_W; gx++)
                {
                    if (waterValue[rowA + gx] < 0f || waterValue[rowB + gx] < 0f) continue;
                    if (isLandRestriction[rowA + gx] || isLandRestriction[rowB + gx]) continue;
                    if (isWater[rowA + gx] != isWater[rowB + gx])
                    {
                        float svgX1 = NormalizeX(sampleBounds.minX + gx * cellW, settlementBounds);
                        float svgX2 = NormalizeX(sampleBounds.minX + (gx + 1) * cellW, settlementBounds);
                        segments.Add(new CoastlineSegment { X1 = svgX1, Y1 = edgeSvgY, X2 = svgX2, Y2 = edgeSvgY });
                    }
                }
            }

            for (int gy = 0; gy < GRID_H; gy++)
            {
                int rowBase = gy * GRID_W;
                float svgY1 = NormalizeY(sampleBounds.minY + gy * cellH, settlementBounds);
                float svgY2 = NormalizeY(sampleBounds.minY + (gy + 1) * cellH, settlementBounds);

                for (int gx = 0; gx < GRID_W - 1; gx++)
                {
                    if (waterValue[rowBase + gx] < 0f || waterValue[rowBase + gx + 1] < 0f) continue;
                    if (isLandRestriction[rowBase + gx] || isLandRestriction[rowBase + gx + 1]) continue;
                    if (isWater[rowBase + gx] != isWater[rowBase + gx + 1])
                    {
                        float edgeSvgX = NormalizeX(sampleBounds.minX + (gx + 1) * cellW, settlementBounds);
                        segments.Add(new CoastlineSegment { X1 = edgeSvgX, Y1 = svgY1, X2 = edgeSvgX, Y2 = svgY2 });
                    }
                }
            }

            segments = FilterCoastlineSegments(segments, normalizedSettlements);
            Log.Trace($"[MapHub] Coastline after proximity filter: {segments.Count} segments");
            return segments;
        }

        private static List<CoastlineSegment> FilterCoastlineSegments(
        List<CoastlineSegment> segments,
        List<SettlementData> settlements,
        float maxDistFromSettlement = 12f,
        float maxChainDistance = 15f)  // SVG units of connected coastline away from a qualifying segment
        {
            if (settlements.Count == 0) return segments;

            int n = segments.Count;
            if (n == 0) return segments;

            var positions = settlements.Select(s => (s.X, s.Y)).ToList();
            const float ENDPOINT_EPSILON = 0.01f;

            // --- Step 1: Build adjacency with edge lengths ---
            var adjacency = new List<(int index, float length)>[n];
            for (int i = 0; i < n; i++) adjacency[i] = new List<(int, float)>();

            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    var a = segments[i]; var b = segments[j];
                    bool shared =
                        Near(a.X1, a.Y1, b.X1, b.Y1) || Near(a.X1, a.Y1, b.X2, b.Y2) ||
                        Near(a.X2, a.Y2, b.X1, b.Y1) || Near(a.X2, a.Y2, b.X2, b.Y2);
                    if (shared)
                    {
                        // Length of segment j = cost to traverse it
                        float dx = b.X2 - b.X1, dy = b.Y2 - b.Y1;
                        float len = (float)Math.Sqrt(dx * dx + dy * dy);
                        adjacency[i].Add((j, len));
                        adjacency[j].Add((i, len));
                    }
                }

            // --- Step 2: Mark segments close to a settlement ---
            var closeToSettlement = new bool[n];
            for (int i = 0; i < n; i++)
            {
                float midX = (segments[i].X1 + segments[i].X2) * 0.5f;
                float midY = (segments[i].Y1 + segments[i].Y2) * 0.5f;
                foreach (var (sx, sy) in positions)
                {
                    float dx = midX - sx, dy = midY - sy;
                    if (dx * dx + dy * dy <= maxDistFromSettlement * maxDistFromSettlement)
                    {
                        closeToSettlement[i] = true;
                        break;
                    }
                }
            }

            // --- Step 3: Dijkstra from close segments, propagate up to maxChainDistance ---
            var bestDist = new float[n];
            for (int i = 0; i < n; i++) bestDist[i] = float.MaxValue;

            // Priority queue: (distanceSoFar, segmentIndex)
            var pq = new SortedSet<(float dist, int idx)>(Comparer<(float, int)>.Create(
                (a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2.CompareTo(b.Item2)));

            for (int i = 0; i < n; i++)
                if (closeToSettlement[i]) { bestDist[i] = 0f; pq.Add((0f, i)); }

            while (pq.Count > 0)
            {
                var (dist, idx) = pq.Min;
                pq.Remove(pq.Min);

                if (dist > bestDist[idx]) continue;
                if (dist >= maxChainDistance) continue;

                foreach (var (neighbour, len) in adjacency[idx])
                {
                    float newDist = dist + len;
                    if (newDist < bestDist[neighbour] && newDist <= maxChainDistance)
                    {
                        bestDist[neighbour] = newDist;
                        pq.Add((newDist, neighbour));
                    }
                }
            }

            var result = new List<CoastlineSegment>(n);
            for (int i = 0; i < n; i++)
                if (bestDist[i] <= maxChainDistance) result.Add(segments[i]);

            Log.Trace($"[MapHub] Coastline filter: {n} -> {result.Count} segments (maxDist={maxDistFromSettlement}, maxChain={maxChainDistance})");
            return result;

            bool Near(float x1, float y1, float x2, float y2)
            {
                float dx = x1 - x2, dy = y1 - y2;
                return dx * dx + dy * dy <= ENDPOINT_EPSILON * ENDPOINT_EPSILON;
            }
        }

        private static bool IsWaterTerrain(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Water:
                case TerrainType.SeaRestriction:
                case TerrainType.OpenSea:
                case TerrainType.CoastalSea:
                    return true;
                default:
                    return false;
            }
        }

        private static (bool isWater, TerrainType terrain, bool valid) SampleTerrain(
            IMapScene map, float worldX, float worldY, float cellW, float cellH)
        {
            foreach (bool isOnLand in new[] { true, false })
            {
                var vec = new CampaignVec2(new Vec2(worldX, worldY), isOnLand);
                var face = map.GetFaceIndex(in vec);
                if (face.IsValid())
                {
                    var terrain = map.GetFaceTerrainType(face);
                    return (IsWaterTerrain(terrain), terrain, true);
                }
            }
            foreach (var (ox, oy) in new[] { (0.4f, 0f), (-0.4f, 0f), (0f, 0.4f), (0f, -0.4f) })
            {
                float nx = worldX + ox * cellW, ny = worldY + oy * cellH;
                foreach (bool isOnLand in new[] { true, false })
                {
                    var vec = new CampaignVec2(new Vec2(nx, ny), isOnLand);
                    var face = map.GetFaceIndex(in vec);
                    if (face.IsValid())
                    {
                        var terrain = map.GetFaceTerrainType(face);
                        return (IsWaterTerrain(terrain), terrain, true);
                    }
                }
            }
            return (true, TerrainType.Water, false);
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