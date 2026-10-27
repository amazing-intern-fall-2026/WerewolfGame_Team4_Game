using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Thuong.Maps
{
    // Reusable collision grid for offline content checks. Never runs in the movement loop.
    public sealed class ThuongMapNavigation
    {
        private readonly ThuongMapDefinition map;
        private readonly float spacing;
        private readonly int width, height;
        private readonly bool[] free, linksReady;
        private readonly byte[] links;
        private readonly int[][] stationNodes;
        private static readonly Vector2Int[] Directions =
        {
            new(-1,-1), new(0,-1), new(1,-1), new(-1,0),
            new(1,0), new(-1,1), new(0,1), new(1,1)
        };

        public ThuongMapNavigation(ThuongMapDefinition definition, float cellSpacing = .5f)
        {
            if (!float.IsFinite(cellSpacing) || cellSpacing <= 0) throw new ArgumentOutOfRangeException(nameof(cellSpacing));
            map = definition; spacing = cellSpacing;
            width = Mathf.FloorToInt((map.boundsMax.x - map.boundsMin.x) / spacing) + 1;
            height = Mathf.FloorToInt((map.boundsMax.y - map.boundsMin.y) / spacing) + 1;
            free = new bool[width * height]; linksReady = new bool[free.Length]; links = new byte[free.Length];
            for (int i = 0; i < free.Length; i++) free[i] = ThuongMapCollision.IsFree(map, Point(i));
            stationNodes = map.stations.OrderBy(s => s.maskSlot).Select(s => Near(s.position, 1.25f).ToArray()).ToArray();
        }

        private Vector2 Point(int index) => map.boundsMin + new Vector2(index % width, index / width) * spacing;

        private IEnumerable<int> Near(Vector2 point, float radius)
        {
            Vector2 local = (point - map.boundsMin) / spacing;
            int cells = Mathf.CeilToInt(radius / spacing);
            for (int y = Mathf.Max(0, Mathf.FloorToInt(local.y) - cells); y <= Mathf.Min(height - 1, Mathf.CeilToInt(local.y) + cells); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(local.x) - cells); x <= Mathf.Min(width - 1, Mathf.CeilToInt(local.x) + cells); x++)
                {
                    int index = y * width + x;
                    if (free[index] && Vector2.Distance(Point(index), point) <= radius && ThuongMapCollision.HasLine(map, Point(index), point))
                        yield return index;
                }
        }

        private byte Links(int index)
        {
            if (linksReady[index]) return links[index];
            linksReady[index] = true;
            for (int d = 0; d < Directions.Length; d++)
            {
                int x = index % width + Directions[d].x, y = index / width + Directions[d].y;
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                int next = y * width + x;
                if (free[next] && ThuongMapCollision.HasLine(map, Point(index), Point(next))) links[index] |= (byte)(1 << d);
            }
            return links[index];
        }

        private float[] Distances(Vector2 origin)
        {
            var distances = Enumerable.Repeat(float.PositiveInfinity, free.Length).ToArray();
            var queue = new SortedSet<(float cost, int node)>();
            foreach (int index in Near(origin, spacing * 1.5f))
            {
                distances[index] = Vector2.Distance(origin, Point(index));
                queue.Add((distances[index], index));
            }
            while (queue.Count > 0)
            {
                var current = queue.Min; queue.Remove(current);
                if (current.cost > distances[current.node]) continue;
                byte mask = Links(current.node);
                for (int d = 0; d < Directions.Length; d++)
                {
                    if ((mask & (1 << d)) == 0) continue;
                    int next = current.node + Directions[d].x + Directions[d].y * width;
                    float cost = current.cost + spacing * (Directions[d].x != 0 && Directions[d].y != 0 ? 1.41421356f : 1);
                    if (cost >= distances[next]) continue;
                    distances[next] = cost; queue.Add((cost, next));
                }
            }
            return distances;
        }

        public float[] WalkingDistances(Vector2 origin)
        {
            float[] distances = Distances(origin);
            return stationNodes.Select(nodes => nodes.Length == 0 ? float.PositiveInfinity : nodes.Min(i => distances[i])).ToArray();
        }

        public float Distance(Vector2 origin, Vector2 destination)
        {
            float[] distances = Distances(origin);
            return Near(destination, spacing * 1.5f).Select(i => distances[i] + Vector2.Distance(Point(i), destination))
                .DefaultIfEmpty(float.PositiveInfinity).Min();
        }
    }
}
