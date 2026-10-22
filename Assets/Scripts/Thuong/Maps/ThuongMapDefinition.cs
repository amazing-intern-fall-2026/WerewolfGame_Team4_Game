using System;
using UnityEngine;

namespace Thuong.Maps
{
    [Serializable]
    public sealed class ThuongMapStation
    {
        public string stationId;
        [Range(0, 15)] public int maskSlot;
        public TaskData task;
        public Vector2 position;
        public Sprite sprite;
    }

    [CreateAssetMenu(menuName = "Thuong/Map Definition")]
    public sealed class ThuongMapDefinition : ScriptableObject
    {
        public string mapId;
        [Min(1)] public int contentRevision = 1;
        public string displayName;
        public Sprite thumbnail;
        public GameObject worldPrefab;
        public GameObject stationPrefab;
        public Vector2 boundsMin = new(-13, -11), boundsMax = new(15, 13);
        public Vector2[] spawns = Array.Empty<Vector2>();
        public ThuongMapStation[] stations = Array.Empty<ThuongMapStation>();
        public Rect[] obstacles = Array.Empty<Rect>();
        public Vector2 plaza, stage, gate;
        public Vector2[] lanterns = Array.Empty<Vector2>();
        public bool released = true;
    }
}
