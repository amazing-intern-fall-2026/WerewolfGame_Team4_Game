using System;
using System.Linq;
using Thuong.Maps;
using Thuong.Minigames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class ThuongMapInstaller
{
    public const string LargeVillageId = "village_large";

    // Targeted entry point: only creates this map and appends its catalog reference.
    [MenuItem("Tools/Thuong/Maps/Install Large Village Only")]
    public static void InstallLargeVillage()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var catalog = AssetDatabase.LoadAssetAtPath<ThuongMapCatalog>(Root + "/Resources/ThuongMapCatalog.asset");
        var minigames = Resources.Load<ThuongMinigameCatalog>(ThuongMinigameCatalog.ResourcePath);
        if (catalog == null || minigames == null) throw new InvalidOperationException("Install the existing maps and minigames first.");
        material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Thuong/Prototype/Art/World.mat");
        blockers.Clear();
        var preview = EditorSceneManager.NewPreviewScene();
        GameObject world = null;
        try
        {
            world = new GameObject("World " + LargeVillageId);
            SceneManager.MoveGameObjectToScene(world, preview);
            BuildLargeVillage(world.transform);
            var definition = Asset<ThuongMapDefinition>(Root + "/Definitions/" + LargeVillageId + ".asset");
            definition.mapId = LargeVillageId;
            definition.displayName = "Làng Mở Rộng";
            definition.contentRevision = 1;
            definition.boundsMin = new Vector2(-20, -17);
            definition.boundsMax = new Vector2(22, 19);
            definition.plaza = new Vector2(1, 0);
            definition.gate = new Vector2(1, -15);
            definition.stage = new Vector2(6, -4);
            definition.spawns = Enumerable.Range(0, 25)
                .Select(i => new Vector2(1 + (i % 5 - 2) * .85f, (i / 5 - 2) * .85f)).ToArray();
            definition.lanterns = new[]
            {
                new Vector2(-4,-3), new Vector2(5,-3), new Vector2(-4,4), new Vector2(6,4),
                new Vector2(-10,10), new Vector2(8,12), new Vector2(18,0), new Vector2(10,-10),
                new Vector2(-8,-12), new Vector2(-17,2), new Vector2(-17,17)
            };
            definition.obstacles = blockers.ToArray();
            definition.stationPrefab = minigames.stationPrefab;
            definition.stations = BuildStations(0, minigames);
            Vector2[] positions =
            {
                new(-8,-5), new(-7,12), new(12,15), new(-7,-11), new(17,3),
                new(15,10), new(3,15), new(12,2), new(-17,14), new(10,-13)
            };
            for (int i = 0; i < positions.Length; i++) definition.stations[i].position = positions[i];
            definition.worldPrefab = PrefabUtility.SaveAsPrefabAsset(world, Root + "/Prefabs/" + LargeVillageId + ".prefab");
            definition.thumbnail = RenderThumbnail(world, LargeVillageId, definition.boundsMin, definition.boundsMax);
            definition.released = true;
            if (!ThuongMapValidator.Validate(definition, out string error)) throw new InvalidOperationException(error);
            catalog.maps = catalog.maps.Where(m => m != null && m.mapId != LargeVillageId).Append(definition).ToArray();
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("THUONG_LARGE_VILLAGE_INSTALLED: 42x36 units, 25 spawns, 10 stable task slots; original maps preserved.");
        }
        finally
        {
            if (world != null) Object.DestroyImmediate(world);
            EditorSceneManager.ClosePreviewScene(preview);
            blockers.Clear();
        }
    }

    private static void BuildLargeVillage(Transform root)
    {
        var grid = new GameObject("Terrain Grid", typeof(Grid));
        grid.transform.SetParent(root, false);
        var ground = Tiles(grid.transform, "Ground", Sprite("Grass"), -200);
        var road = Tiles(grid.transform, "Lanes", Sprite("Cobblestone"), -190);
        for (int y = -17; y < 19; y++) for (int x = -20; x < 22; x++)
        {
            var cell = new Vector3Int(x, y, 0);
            ground.map.SetTile(cell, ground.tile);
            bool plaza = x >= -4 && x <= 5 && y >= -3 && y <= 3;
            bool spokes = Mathf.Abs(y) <= 1 || x >= 0 && x <= 2;
            bool ring = (y >= 17 || y == -14 || y == -13) && x >= -18 && x <= 20 ||
                        (x == -18 || x == -17 || x == 19 || x == 20) && y >= -14 && y <= 18;
            bool cross = y >= 10 && y <= 11 && x >= -12 && x <= 20 ||
                         (x == -10 || x == -9) && y >= -13 && y <= 13 ||
                         (x == 12 || x == 13) && y >= -13 && y <= 16;
            if (plaza || spokes || ring || cross) road.map.SetTile(cell, road.tile);
        }

        // North: farmland and an open pen, with exits on both sides.
        Prop(root, "House1", new Vector2(-7, 13), new Vector2(3.6f, 1.3f));
        for (int y = 8; y <= 10; y++) for (int x = -8; x <= -4; x++)
            Prop(root, "Wheat", new Vector2(x, y));
        Prop(root, "Cart", new Vector2(-11, 14), new Vector2(1.4f, .6f));
        foreach (int x in new[] { 0, 2, 4, 6 })
            Prop(root, "Fence", new Vector2(x, 16.5f), new Vector2(1.4f, .25f));
        Prop(root, "Hay", new Vector2(6, 14), new Vector2(1, .5f));
        Prop(root, "Sacks", new Vector2(0, 14));
        for (int x = 14; x <= 18; x += 2)
            Prop(root, "Fence", new Vector2(x, 16), new Vector2(1.4f, .25f));

        // East: market fronts and storage. Tasks remain in front of the buildings.
        Prop(root, "House3", new Vector2(17, 5), new Vector2(3.6f, 1.3f));
        Prop(root, "House2", new Vector2(8, 6), new Vector2(3.6f, 1.3f));
        Prop(root, "MarketAwning", new Vector2(17, 12), new Vector2(1.7f, .7f));
        Prop(root, "MarketAwningGreen", new Vector2(11, 12), new Vector2(1.7f, .7f));
        Prop(root, "CrateStack", new Vector2(19, 3), new Vector2(.9f, .5f));
        Prop(root, "Planter", new Vector2(10, 4));
        Prop(root, "Sacks", new Vector2(15, 4));

        // South: woodcutters' camp and forest workshop, on the return loop.
        Prop(root, "House2", new Vector2(-13, -12), new Vector2(3.6f, 1.3f));
        Prop(root, "House1", new Vector2(17, -13), new Vector2(3.6f, 1.3f));
        Prop(root, "RuralCabin", new Vector2(5, -8), new Vector2(4.6f, 1.7f));
        for (int i = 0; i < 3; i++) Prop(root, "Woodpile", new Vector2(-6 + i * 1.5f, -9));
        Prop(root, "Campfire", new Vector2(8, -11));
        Prop(root, "Cart", new Vector2(-3, -14), new Vector2(1.4f, .6f));
        foreach (var point in new[] { new Vector2(-11,-6), new Vector2(-7,-14), new Vector2(12,-8), new Vector2(15,-6) })
            Prop(root, "Tree", point, new Vector2(.6f, .4f));

        // West: memorial lane with a south entrance and a north return path.
        // The wall deliberately makes the far station an 8-10 second walk.
        Blocker(root, new Rect(-14, 4, .5f, 13), "Memorial dividing wall");
        for (int y = 4; y < 17; y++)
        {
            var wall = Prop(root, "ManorWall", new Vector2(-13.75f, y));
            wall.transform.localScale = new Vector3(.5f, 1, 1);
            wall.AddComponent<ThuongMapOccluder>();
        }
        Prop(root, "Memorial", new Vector2(-17, 15.5f), new Vector2(1.5f, .7f));
        foreach (var point in new[] { new Vector2(-19,6), new Vector2(-16,8), new Vector2(-19,11) })
            Prop(root, "GraveRound", point, new Vector2(.5f, .3f));
        Prop(root, "House3", new Vector2(-8, 3.5f), new Vector2(3.6f, 1.3f));
        Prop(root, "Signpost", new Vector2(-16, 1));
        Prop(root, "Signpost", new Vector2(4, 3.8f));

        // Boundary trees preserve the character/sprite scale used by the small map.
        for (int x = -19; x <= 21; x += 2)
        {
            Prop(root, "Tree", new Vector2(x, 18), new Vector2(.6f, .3f));
            if (Mathf.Abs(x - 1) > 3) Prop(root, "Tree", new Vector2(x, -16.5f), new Vector2(.6f, .25f));
        }
        for (int y = -13; y <= 15; y += 3)
        {
            Prop(root, "Tree", new Vector2(-19, y), new Vector2(.5f, .4f));
            Prop(root, "Tree", new Vector2(21, y), new Vector2(.5f, .4f));
        }
        foreach (var point in new[]
        {
            new Vector2(-4,-3), new Vector2(5,-3), new Vector2(-4,4), new Vector2(6,4),
            new Vector2(-10,10), new Vector2(8,12), new Vector2(18,0), new Vector2(10,-10),
            new Vector2(-8,-12), new Vector2(-17,2), new Vector2(-17,17)
        }) Prop(root, "Lantern", point);
    }

    public static void InstallLargeVillageAndVerify()
    {
        InstallLargeVillage();
        ThuongMapChecks.Run();
        ThuongMapVisualChecks.Run();
    }
}
