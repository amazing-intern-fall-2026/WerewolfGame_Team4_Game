using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Thuong.Maps
{
    public sealed class ThuongMapRuntime : MonoBehaviour
    {
        public ThuongMapDefinition Definition { get; private set; }
        public GameObject World { get; private set; }
        public TaskStation[] Stations { get; private set; } = Array.Empty<TaskStation>();

        public bool Bind(ThuongMapDefinition definition, out string error)
        {
            if (Definition == definition && World != null) { error = null; return true; }
            if (!ThuongMapValidator.Validate(definition, out error)) return false;
            Clear();
            Definition = definition;
            World = Instantiate(definition.worldPrefab, transform);
            World.name = "MapRoot - " + definition.mapId;
            Stations = new TaskStation[definition.stations.Length];
            foreach (var binding in definition.stations.OrderBy(s => s.maskSlot))
            {
                var station = Instantiate(definition.stationPrefab, World.transform).GetComponent<TaskStation>();
                station.name = binding.stationId;
                station.transform.position = binding.position;
                station.task = binding.task;
                station.player = null;
                station.enabled = false;
                station.interactionRadius = 1.3f;
                foreach (var collider in station.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
                var art = station.transform.Find("Medieval station art");
                if (art != null)
                {
                    foreach (Transform child in art) child.gameObject.SetActive(false);
                    var visual = new GameObject("Map station art", typeof(SpriteRenderer), typeof(ThuongDepthSort));
                    visual.transform.SetParent(art, false);
                    visual.GetComponent<SpriteRenderer>().sprite = binding.sprite;
                    visual.GetComponent<SpriteRenderer>().sharedMaterial = World.GetComponentInChildren<SpriteRenderer>()?.sharedMaterial;
                }
                foreach (var text in station.GetComponentsInChildren<TMP_Text>(true))
                    if (text.text != "E") text.text = binding.task.taskName;
                if (station.marker != null) station.marker.SetActive(false);
                Stations[binding.maskSlot] = station;
            }
            return true;
        }

        public void Clear()
        {
            if (World != null) { World.SetActive(false); if (Application.isPlaying) Destroy(World); else DestroyImmediate(World); }
            World = null; Definition = null; Stations = Array.Empty<TaskStation>();
        }
        private void OnDestroy() => Clear();
    }
}
