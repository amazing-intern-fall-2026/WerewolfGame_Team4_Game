using System;
using System.Linq;
using UnityEngine;

namespace Thuong.Maps
{
    public readonly struct ThuongMapVariant
    {
        public ThuongMapDefinition Map { get; }
        public GameEventType EventType { get; }
        public ThuongMapVariant(ThuongMapDefinition map, GameEventType eventType)
        { Map = map; EventType = eventType; }
    }

    [CreateAssetMenu(menuName = "Thuong/Map Catalog")]
    public sealed class ThuongMapCatalog : ScriptableObject
    {
        public const string ResourcePath = "ThuongMapCatalog";
        public const int ProtocolVersion = 5;
        public ThuongMapDefinition[] maps = Array.Empty<ThuongMapDefinition>();
        public ThuongMapDefinition Find(string id) => maps.FirstOrDefault(m => m != null && m.released && m.mapId == id);
        public ThuongMapDefinition[] Released => maps.Where(m => m != null && m.released).ToArray();

        public ThuongMapVariant[] GetVariants(ThuongEventPresentationCatalog events)
        {
            // None is an actual normal-map choice, alongside every supported event.
            var types = (events?.events ?? Array.Empty<ThuongEventVisual>())
                .Where(e => e != null && e.type != GameEventType.None && Enum.IsDefined(typeof(GameEventType), e.type))
                .Select(e => e.type).Distinct().Prepend(GameEventType.None).ToArray();
            return Released.SelectMany(map => types.Select(type => new ThuongMapVariant(map, type))).ToArray();
        }
    }
}
