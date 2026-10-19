using System;
using System.Collections.Generic;
using UnityEngine;

namespace Thuong.Minigames
{
    [Serializable]
    public sealed class ThuongMinigameEntry
    {
        public string taskId;
        public TaskData task;
        public ThuongMinigameKind kind;
        [TextArea] public string instructions;
        public ThuongMinigameRules rules = new();
        public GameObject viewPrefab;
        public Sprite background, subject, tool, obstacle;
    }

    [Serializable]
    public sealed class ThuongExtraTaskStation
    {
        public TaskData task;
        public Vector2 position;
    }

    [CreateAssetMenu(menuName = "Thuong/Minigame Catalog")]
    public sealed class ThuongMinigameCatalog : ScriptableObject
    {
        public const string ResourcePath = "ThuongMinigameCatalog";
        public int schemaVersion;
        public List<ThuongMinigameEntry> entries = new();
        public List<ThuongExtraTaskStation> additionalStations = new();
        public GameObject stationPrefab;
        public IngredientData[] ingredients;
        public Sprite trapCover, trapFlag, trapCleared;

        public ThuongMinigameEntry Find(TaskData task) => entries.Find(entry => entry != null && entry.task == task);
        public ThuongMinigameEntry Find(string id) => entries.Find(entry => entry != null && entry.taskId == id);

        public int[] IngredientScores()
        {
            if (ingredients == null) return Array.Empty<int>();
            var scores = new int[ingredients.Length];
            for (int i = 0; i < scores.Length; i++)
            {
                var ingredient = ingredients[i];
                scores[i] = ingredient.type == IngredientType.Healing ? ingredient.potency :
                    ingredient.type == IngredientType.Poison ? -ingredient.potency : 0;
            }
            return scores;
        }

        public bool Validate(out string error)
        {
            if (schemaVersion != 1) { error = "Catalog chưa cài đặt hoàn tất hoặc sai phiên bản."; return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var tasks = new HashSet<TaskData>();
            foreach (var entry in entries)
                if (entry == null || string.IsNullOrWhiteSpace(entry.taskId) || entry.taskId.Length > 48 ||
                    !ids.Add(entry.taskId) || entry.task == null || !tasks.Add(entry.task) ||
                    entry.rules == null || !entry.rules.IsValid || entry.viewPrefab == null ||
                    entry.viewPrefab.GetComponent<ThuongTaskMinigameView>() == null)
                { error = "Catalog có ID/task trùng, luật hoặc prefab không hợp lệ."; return false; }
            if (ingredients == null || ingredients.Length != 3 || Array.Exists(ingredients, item => item == null || item.potency < 1 || item.potency > 3))
            { error = "Cần ba nguyên liệu hợp lệ cho pha thuốc."; return false; }
            var extra = new HashSet<TaskData>();
            foreach (var station in additionalStations)
                if (station == null || !tasks.Contains(station.task) || !extra.Add(station.task))
                { error = "Nhiệm vụ bổ sung phải có mapping duy nhất."; return false; }
            if (additionalStations.Count > 0 && (stationPrefab == null || stationPrefab.GetComponent<TaskStation>() == null))
            { error = "Thiếu prefab trạm nhiệm vụ."; return false; }
            error = null;
            return true;
        }
    }
}
