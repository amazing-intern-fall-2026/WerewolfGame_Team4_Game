using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Maps
{
    // A schematic: never draws remote avatars or private role information.
    public sealed class ThuongTaskMapGraphic : MaskableGraphic
    {
        public ThuongMapDefinition Map;
        public Vector2 Player;
        public readonly List<Vector2> Route = new();
        public Vector2 Project(Vector2 point) => Map == null ? Vector2.zero : new Vector2(
            (point.x - Map.boundsMin.x) / (Map.boundsMax.x - Map.boundsMin.x),
            (point.y - Map.boundsMin.y) / (Map.boundsMax.y - Map.boundsMin.y));

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (Map == null) return;
            Rect rect = rectTransform.rect;
            Vector2 Position(Vector2 point) => rect.min + Vector2.Scale(Project(point), rect.size);
            Quad(mesh, rect.min, rect.max, new Color32(221, 207, 163, 255));
            foreach (var obstacle in Map.obstacles)
                Quad(mesh, Position(obstacle.min), Position(obstacle.max), new Color32(117, 111, 83, 220));
            for (int i = 1; i < Route.Count; i++)
            {
                Vector2 a = Position(Route[i - 1]), b = Position(Route[i]);
                Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * 2.5f;
                int start = mesh.currentVertCount;
                mesh.AddVert(a + normal, new Color32(20, 93, 100, 255), Vector2.zero);
                mesh.AddVert(a - normal, new Color32(20, 93, 100, 255), Vector2.zero);
                mesh.AddVert(b - normal, new Color32(20, 93, 100, 255), Vector2.zero);
                mesh.AddVert(b + normal, new Color32(20, 93, 100, 255), Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
            }
            Vector2 p = Position(Player);
            Quad(mesh, p - Vector2.one * 7, p + Vector2.one * 7, new Color32(255, 255, 247, 255));
            Quad(mesh, p - Vector2.one * 4, p + Vector2.one * 4, new Color32(18, 82, 140, 255));
        }

        private static void Quad(VertexHelper mesh, Vector2 min, Vector2 max, Color32 color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector2(min.x, min.y), color, Vector2.zero);
            mesh.AddVert(new Vector2(min.x, max.y), color, Vector2.zero);
            mesh.AddVert(new Vector2(max.x, max.y), color, Vector2.zero);
            mesh.AddVert(new Vector2(max.x, min.y), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
