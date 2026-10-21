using System;
using System.Linq;
using UnityEngine;

namespace Thuong.Maps
{
    [CreateAssetMenu(menuName = "Thuong/Map Catalog")]
    public sealed class ThuongMapCatalog : ScriptableObject
    {
        public const string ResourcePath = "ThuongMapCatalog";
        public const int ProtocolVersion = 5;
        public ThuongMapDefinition[] maps = Array.Empty<ThuongMapDefinition>();
        public ThuongMapDefinition Find(string id) => maps.FirstOrDefault(m => m != null && m.released && m.mapId == id);
        public ThuongMapDefinition[] Released => maps.Where(m => m != null && m.released).ToArray();
    }
}
