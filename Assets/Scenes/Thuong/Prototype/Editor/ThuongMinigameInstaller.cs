using System;
using System.IO;
using System.Linq;
using Thuong.Minigames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ThuongMinigameInstaller
{
    public const string Root = "Assets/Scenes/Thuong/Prototype/Integration/Minigames";
    public const string CatalogPath = Root + "/Resources/ThuongMinigameCatalog.asset";

    [MenuItem("Tools/Thuong/Minigames/Install Assets")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        EnsureFolder(Root + "/Resources"); EnsureFolder(Root + "/Tasks"); EnsureFolder(Root + "/Ingredients"); EnsureFolder(Root + "/Prefabs");
        var catalog = AssetDatabase.LoadAssetAtPath<ThuongMinigameCatalog>(CatalogPath);
        // Re-running the installer never resets tuned rules or art. Version 2 moves
        // all station placement into map definitions.
        if (catalog != null && catalog.schemaVersion >= 1 && catalog.entries.Count == 6)
        {
            catalog.additionalStations.Clear();
            catalog.stationPrefab = null;
            catalog.schemaVersion = 2;
            if (!catalog.Validate(out string existingError)) throw new InvalidOperationException(existingError);
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            Debug.Log("THUONG_MINIGAME_ASSETS_ALREADY_INSTALLED"); return;
        }
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ThuongMinigameCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.entries.Clear(); catalog.additionalStations.Clear();
        catalog.stationPrefab = null;
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Thuong/Prototype/Art/World.mat");
        foreach (ThuongMinigameKind kind in Enum.GetValues(typeof(ThuongMinigameKind)))
        {
            var root = new GameObject(kind + " Task View", typeof(ThuongTaskMinigameView));
            root.GetComponent<ThuongTaskMinigameView>().spriteMaterial = material;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/" + kind + ".prefab");
            Object.DestroyImmediate(root);
            catalog.entries.Add(new ThuongMinigameEntry { kind = kind, viewPrefab = prefab });
        }
        var wood = Entry(catalog, ThuongMinigameKind.ChopWood, "thuong.task.wood", ExistingTask(4),
            "Nhấn vào khung khi khúc gỗ đến vạch vàng. Chặt đúng 5 khúc; tối đa 2 lần trượt.", 5, 2, 18, 12, 1.8f);
        wood.subject = PrefabSprite("Assets/Minigames/Chop Wood/PF Village Props - Wood Log 01.prefab");
        wood.background = SceneSprite("Assets/Minigames/Chop Wood/ChopWood.unity", "BG1");
        var wheat = Entry(catalog, ThuongMinigameKind.Harvest, "thuong.task.harvest", ExistingTask(2),
            "Nhấn vào khung khi lúa đến vạch vàng. Thu đủ 8 bó, bỏ qua chướng ngại màu đỏ; tối đa 1 lần trượt.", 8, 1, 23, 18, 1.5f);
        wheat.subject = PrefabSprite("Assets/Minigames/Wheat Harvest/Wheat in Minigame.prefab");
        wheat.background = SpriteAt("Assets/Minigames/Wheat Harvest/BGforgame.png");
        wheat.obstacle = wood.subject;
        var eggs = Entry(catalog, ThuongMinigameKind.Eggs, "thuong.task.eggs", NewTask("Eggs", "Thu gom trứng"),
            "Di chuyển chuột hoặc kéo trong khung để đưa giỏ dưới trứng. Hứng 10 quả; tối đa 2 lần rơi.", 10, 2, 25, 18, 1.6f);
        eggs.subject = AssetDatabase.LoadAllAssetsAtPath("Assets/Minigames/Fruit Collect/Farming Asset Pack/farming-tileset.png")
            .OfType<Sprite>().Single(sprite => sprite.name == "farming-tileset_114");
        eggs.tool = SceneSprite("Assets/Minigames/Fruit Collect/Egg Collect.unity", "Basket");
        var potion = Entry(catalog, ThuongMinigameKind.Potion, "thuong.task.potion", NewTask("Potion", "Pha chế thuốc"),
            "Chọn 3 nguyên liệu theo loại thuốc được giao.", 3, 0, 22, 8, 1.5f);
        potion.subject = SceneSprite("Assets/Minigames/Potion Making/PotionMaking.unity", "Cauldron") ??
            SpriteAt("Assets/Minigames/Potion Making/Pixel Magical Potions Pack/PNG/Outline/Green.png");
        var defender = Entry(catalog, ThuongMinigameKind.Defender, "thuong.task.defender", NewTask("Defender", "Bảo vệ khu vực"),
            "Di chuyển chuột hoặc kéo quanh tâm để xoay khiên. Chặn 10 đợt; được trúng tối đa 2 đòn.", 10, 2, 25, 18, 1.6f);
        defender.subject = PrefabSprite("Assets/Minigames/Defender Game/PF Village Props - Arrow.prefab");
        defender.tool = SceneSprite("Assets/Minigames/Defender Game/Defend.unity", "Shield2");
        defender.obstacle = SceneSprite("Assets/Minigames/Defender Game/Defend.unity", "PlayerCenter");
        Entry(catalog, ThuongMinigameKind.Trapsweeper, "thuong.task.traps", NewTask("Traps", "Gỡ bẫy"),
            "Mở 22 ô an toàn trên bàn 5×5 có 3 bẫy. Lần mở đầu an toàn. Chuột phải hoặc bật Cắm cờ cho cảm ứng.", 22, 0, 40, 15, 1.5f);
        catalog.ingredients = new[]
        {
            Ingredient("Healing", "Thảo dược", IngredientType.Healing, 2, SpriteAt("Assets/Minigames/Potion Making/alchhemy pack/aloe vera 32.png")),
            Ingredient("Poison", "Nấm độc", IngredientType.Poison, 2, SpriteAt("Assets/Minigames/Potion Making/alchhemy pack/white toadstool 32.png")),
            Ingredient("Neutral", "Chất nền", IngredientType.Neutral, 1, SpriteAt("Assets/Minigames/Potion Making/Pixel Magical Potions Pack/PNG/Outline/Yellow.png"))
        };
        catalog.trapCover = TileSprite("e4ed6653d9243c64d90bcc227c2b4767");
        catalog.trapFlag = TileSprite("37cea9c523ea2b44698954399f072226");
        catalog.trapCleared = TileSprite("e0a432aeb79ccff448983d3fe1d7bb63");
        catalog.schemaVersion = 2;
        if (!catalog.Validate(out string error)) throw new InvalidOperationException(error);
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        Debug.Log("THUONG_MINIGAME_ASSETS_INSTALLED: six task adapters; no saved scene, demo or project setting changed.");
    }

    public static void InstallAndCheck()
    {
        Install(); ThuongMinigameChecks.Run();
    }

    private static ThuongMinigameEntry Entry(ThuongMinigameCatalog catalog, ThuongMinigameKind kind, string id,
        TaskData task, string help, int target, int misses, float duration, float minimum, float interval)
    {
        var entry = catalog.entries.Single(item => item.kind == kind);
        entry.taskId = id; entry.task = task; entry.instructions = help;
        entry.rules = new ThuongMinigameRules { target = target, allowedMistakes = misses, duration = duration,
            minimumDayRemaining = minimum, interval = interval, timingWindow = .4f, boardWidth = 5, traps = 3 };
        return entry;
    }
    private static TaskData ExistingTask(int index) => AssetDatabase.LoadAssetAtPath<TaskData>("Assets/Scenes/Thuong/Prototype/Data/Task" + index + ".asset");
    private static TaskData NewTask(string id, string name)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TaskData>(Root + "/Tasks/" + id + ".asset");
        if (existing != null) return existing;
        var task = ScriptableObject.CreateInstance<TaskData>(); task.taskName = name;
        task.description = "Đến trạm, nhấn E và hoàn thành thử thách."; task.progressValue = 10;
        AssetDatabase.CreateAsset(task, Root + "/Tasks/" + id + ".asset"); return task;
    }
    private static IngredientData Ingredient(string id, string name, IngredientType type, int potency, Sprite icon)
    {
        var existing = AssetDatabase.LoadAssetAtPath<IngredientData>(Root + "/Ingredients/" + id + ".asset");
        if (existing != null) return existing;
        var ingredient = ScriptableObject.CreateInstance<IngredientData>();
        ingredient.ingredientName = name; ingredient.type = type; ingredient.potency = potency; ingredient.icon = icon;
        AssetDatabase.CreateAsset(ingredient, Root + "/Ingredients/" + id + ".asset"); return ingredient;
    }
    private static Sprite PrefabSprite(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponentInChildren<SpriteRenderer>(true)?.sprite;
    private static Sprite SpriteAt(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    private static Sprite TileSprite(string guid) => AssetDatabase.LoadAssetAtPath<Tile>(AssetDatabase.GUIDToAssetPath(guid))?.sprite;
    private static Sprite SceneSprite(string path, string name)
    {
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var target = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(item => item.name == name);
            if (target == null) return null;
            var renderer = target.GetComponent<SpriteRenderer>();
            if (renderer != null) return renderer.sprite;
            var image = target.GetComponent<Image>();
            return image != null ? image.sprite : null;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
