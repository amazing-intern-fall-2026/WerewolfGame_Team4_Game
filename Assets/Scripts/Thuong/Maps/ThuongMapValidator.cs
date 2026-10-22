using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Thuong.Maps
{
    public static class ThuongMapValidator
    {
        public static bool Validate(ThuongMapDefinition map, out string error)
        {
            error = "Thiếu nội dung map hoặc phiên bản map không hợp lệ.";
            if (map == null || string.IsNullOrWhiteSpace(map.mapId) || map.mapId.Length > 48 || map.contentRevision < 1 ||
                map.worldPrefab == null || map.stationPrefab == null || map.thumbnail == null ||
                map.stationPrefab.GetComponent<TaskStation>() == null || map.boundsMax.x <= map.boundsMin.x ||
                map.boundsMax.y <= map.boundsMin.y || map.spawns == null || map.spawns.Length < 25 || map.stations?.Length != 6 ||
                map.obstacles == null || map.lanterns == null) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var slots = new HashSet<int>(); var tasks = new HashSet<TaskData>();
            foreach (var station in map.stations)
                if (station == null || string.IsNullOrEmpty(station.stationId) || !ids.Add(station.stationId) ||
                    station.maskSlot < 0 || station.maskSlot >= map.stations.Length || !slots.Add(station.maskSlot) || station.task == null ||
                    !tasks.Add(station.task) || !ThuongMapCollision.IsFree(map, station.position))
                { error = "Trạm không hợp lệ: " + station?.stationId + " slot=" + station?.maskSlot + " task=" + station?.task +
                    " free=" + (station != null && ThuongMapCollision.IsFree(map,station.position)); return false; }
            for (int i = 0; i < map.spawns.Length; i++)
            {
                if (!ThuongMapCollision.IsFree(map, map.spawns[i]) ||
                    map.stations.Any(s => Vector2.Distance(s.position, map.spawns[i]) < .7f) ||
                    map.spawns.Take(i).Any(p => Vector2.Distance(p, map.spawns[i]) < .65f))
                { error = "Điểm xuất phát bị chặn, chồng nhau hoặc chồng trạm."; return false; }
            }
            // Topology is fully contained in one prefab, with no competing session or scene systems.
            if (map.worldPrefab.GetComponentsInChildren<Camera>(true).Length > 0 ||
                map.worldPrefab.GetComponentsInChildren<AudioListener>(true).Length > 0 ||
                map.worldPrefab.GetComponentsInChildren<Unity.Netcode.NetworkManager>(true).Length > 0 ||
                map.worldPrefab.GetComponentsInChildren<GameRoleManager>(true).Length > 0 ||
                map.worldPrefab.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true).Length > 0)
            { error = "Map prefab chứa manager/camera không được phép."; return false; }
            error = null; return true;
        }

        // Dijkstra on footprint-safe segments. Distances are actual walkable routes, not straight lines.
        public static float[] WalkingDistances(ThuongMapDefinition map, Vector2 origin, float spacing = .5f)
        {
            int width = Mathf.RoundToInt((map.boundsMax.x - map.boundsMin.x) / spacing) + 1;
            int height = Mathf.RoundToInt((map.boundsMax.y - map.boundsMin.y) / spacing) + 1;
            var distances = Enumerable.Repeat(float.PositiveInfinity, width * height).ToArray();
            var visited = new bool[distances.Length];
            Vector2 Point(int index) => map.boundsMin + new Vector2(index % width, index / width) * spacing;
            for (int i = 0; i < distances.Length; i++)
                if (Vector2.Distance(Point(i), origin) <= spacing * 1.5f && ThuongMapCollision.IsFree(map, Point(i)) &&
                    ThuongMapCollision.HasLine(map, origin, Point(i))) distances[i] = Vector2.Distance(Point(i), origin);
            for (int iteration = 0; iteration < distances.Length; iteration++)
            {
                int best = -1; float cost = float.PositiveInfinity;
                for (int i = 0; i < distances.Length; i++)
                    if (!visited[i] && distances[i] < cost) { cost = distances[i]; best = i; }
                if (best < 0) break;
                visited[best] = true;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int x = best % width + dx, y = best / width + dy;
                    if (dx == 0 && dy == 0 || x < 0 || y < 0 || x >= width || y >= height) continue;
                    int next = y * width + x;
                    if (visited[next] || !ThuongMapCollision.IsFree(map, Point(next)) ||
                        !ThuongMapCollision.HasLine(map, Point(best), Point(next))) continue;
                    distances[next] = Mathf.Min(distances[next], cost + Vector2.Distance(Point(best), Point(next)));
                }
            }
            return map.stations.OrderBy(s => s.maskSlot).Select(station =>
            {
                float cost = float.PositiveInfinity;
                for (int i = 0; i < distances.Length; i++)
                    if (distances[i] < cost && Vector2.Distance(Point(i), station.position) <= 1.25f &&
                        ThuongMapCollision.HasLine(map, Point(i), station.position)) cost = distances[i];
                return cost;
            }).ToArray();
        }
    }
}
