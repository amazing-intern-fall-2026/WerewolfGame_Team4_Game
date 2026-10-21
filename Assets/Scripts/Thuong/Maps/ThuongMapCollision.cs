using UnityEngine;

namespace Thuong.Maps
{
    // The same axis-aligned footprints are serialized into the map's BoxCollider2Ds.
    // Queries are scoped to this definition, never the minigame's PhysicsScene2D.
    public static class ThuongMapCollision
    {
        public static readonly Vector2 HalfSize = new(.23f, .13f);
        public static readonly Vector2 Offset = new(0, .12f);
        private const float Skin = .003f;

        public static bool IsFree(ThuongMapDefinition map, Vector2 feet)
        {
            if (!float.IsFinite(feet.x) || !float.IsFinite(feet.y)) return false;
            Vector2 p = feet + Offset;
            if (p.x - HalfSize.x < map.boundsMin.x || p.y - HalfSize.y < map.boundsMin.y ||
                p.x + HalfSize.x > map.boundsMax.x || p.y + HalfSize.y > map.boundsMax.y) return false;
            foreach (Rect rect in map.obstacles)
                if (Expand(rect).Contains(p)) return false;
            return true;
        }

        public static Vector2 Move(ThuongMapDefinition map, Vector2 feet, Vector2 displacement)
        {
            if (!float.IsFinite(displacement.x) || !float.IsFinite(displacement.y) || !IsFree(map, feet)) return feet;
            Vector2 center = feet + Offset;
            Vector2 end = center + displacement;
            end.x = Mathf.Clamp(end.x, map.boundsMin.x + HalfSize.x + Skin, map.boundsMax.x - HalfSize.x - Skin);
            end.y = Mathf.Clamp(end.y, map.boundsMin.y + HalfSize.y + Skin, map.boundsMax.y - HalfSize.y - Skin);
            Vector2 remaining = end - center;
            for (int pass = 0; pass < 4 && remaining.sqrMagnitude > .00000001f; pass++)
            {
                float nearest = 1f;
                Vector2 normal = Vector2.zero;
                foreach (Rect obstacle in map.obstacles)
                    if (Sweep(center, remaining, Expand(obstacle), out float t, out Vector2 n) && t < nearest)
                    { nearest = t; normal = n; }
                if (normal == Vector2.zero) { center += remaining; break; }
                float safe = Mathf.Max(0, nearest - Skin / Mathf.Max(remaining.magnitude, Skin));
                center += remaining * safe;
                remaining *= 1 - safe;
                remaining -= normal * Mathf.Min(0, Vector2.Dot(remaining, normal));
            }
            Vector2 result = center - Offset;
            return IsFree(map, result) ? result : feet;
        }

        public static bool HasLine(ThuongMapDefinition map, Vector2 from, Vector2 to)
            => (Move(map, from, to - from) - to).sqrMagnitude < .0001f;

        private static Rect Expand(Rect r) => Rect.MinMaxRect(r.xMin - HalfSize.x, r.yMin - HalfSize.y,
            r.xMax + HalfSize.x, r.yMax + HalfSize.y);

        private static bool Sweep(Vector2 start, Vector2 delta, Rect box, out float time, out Vector2 normal)
        {
            float enter = 0, leave = 1;
            normal = Vector2.zero;
            for (int axis = 0; axis < 2; axis++)
            {
                float d = delta[axis], p = start[axis], low = box.min[axis], high = box.max[axis];
                if (Mathf.Abs(d) < .000001f)
                { if (p <= low || p >= high) { time = 0; return false; } continue; }
                float a = (low - p) / d, b = (high - p) / d;
                Vector2 n = axis == 0 ? Vector2.left : Vector2.down;
                if (a > b) { (a, b) = (b, a); n = -n; }
                if (a >= enter) { enter = a; normal = n; }
                leave = Mathf.Min(leave, b);
                if (enter > leave) { time = 0; return false; }
            }
            time = enter;
            return normal != Vector2.zero && enter >= 0 && enter < 1 && leave > 0;
        }
    }
}
