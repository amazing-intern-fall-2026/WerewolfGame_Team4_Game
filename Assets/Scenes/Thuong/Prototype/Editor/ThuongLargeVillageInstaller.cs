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
            definition.contentRevision = Mathf.Max(definition.contentRevision, 3);
            definition.boundsMin = new Vector2(-20, -17);
            definition.boundsMax = new Vector2(22, 19);
            definition.plaza = new Vector2(1, 0);
            definition.gate = new Vector2(1, -15);
            definition.stage = new Vector2(5, -2.5f);
            definition.spawns = Enumerable.Range(0, 25)
                .Select(i => new Vector2(1 + (i % 5 - 2) * .85f, (i / 5 - 2) * .85f)).ToArray();
            definition.lanterns = VillageLanterns;
            definition.obstacles = blockers.ToArray();
            definition.stationPrefab = catalog.Find("village").stationPrefab;
            definition.stations = BuildStations(0, minigames);
            Vector2[] positions = VillageTaskPositions;
            foreach (var station in definition.stations)
                station.position = positions[Array.FindIndex(Ids, id => station.stationId == "thuong.task." + id)];
            definition.worldPrefab = PrefabUtility.SaveAsPrefabAsset(world, Root + "/Prefabs/" + LargeVillageId + ".prefab");
            definition.thumbnail = RenderThumbnail(world, LargeVillageId, definition.boundsMin, definition.boundsMax);
            definition.released = true;
            if (!ThuongMapValidator.Validate(definition, out string error)) throw new InvalidOperationException(error);
            catalog.maps = catalog.maps.Where(m => m != null && m.mapId != LargeVillageId).Append(definition).ToArray();
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("THUONG_LARGE_VILLAGE_INSTALLED: revision 3, interlocking road loops and shortcuts, 25 spawns and six stable minigame slots.");
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
        var paths = Tiles(grid.transform, "Footpaths", Sprite("Soil"), -189);
        var soil = Tiles(grid.transform, "VillageFields", Sprite("Soil"), -195);
        FillVillageTile(paths.tile);
        FillVillageTile(soil.tile);
        for (int y = -17; y < 19; y++) for (int x = -20; x < 22; x++)
        {
            var cell = new Vector3Int(x, y, 0);
            ground.map.SetTile(cell, ground.tile);
            Vector2 point = new(x + .5f, y + .5f);
            bool paved = Vector2.Distance(point, new Vector2(1,0)) <= 3.2f ||
                VillageRoutes.Any(route => !route.dirt && route.Distance(point) <= route.width / 2);
            bool footpath = VillageRoutes.Any(route => route.dirt && route.Distance(point) <= route.width / 2);
            if (paved) road.map.SetTile(cell, road.tile);
            else if (footpath) paths.map.SetTile(cell, paths.tile);
        }

        // Buildings sit between the routes. They never form a continuous wall.
        VillageBuilding(root, "House1", new Vector2(-4,10), new Vector2(3.6f,1.3f));
        VillageBuilding(root, "House2", new Vector2(7,10.5f), new Vector2(3.6f,1.3f));
        VillageBuilding(root, "House3", new Vector2(12,5), new Vector2(3.6f,1.3f));
        VillageBuilding(root, "House3", new Vector2(-12,2.5f), new Vector2(3.6f,1.3f));
        VillageBuilding(root, "House2", new Vector2(-11,-14), new Vector2(3.6f,1.3f));
        VillageBuilding(root, "House1", new Vector2(14,-13.8f), new Vector2(3.6f,1.3f));
        VillageBuilding(root, "RuralCabin", new Vector2(-3,-9), new Vector2(4.6f,1.7f));

        // Broken field edges and small stalls leave room to enter from several sides.
        foreach (var plot in new[] { new RectInt(-12,10,4,5), new RectInt(-8,7,3,4) })
            for (int y=plot.yMin; y<plot.yMax; y++) for (int x=plot.xMin; x<plot.xMax; x++)
            {
                Vector2 point = new(x+.5f,y+.5f);
                if (DistanceToVillageRoad(point) < 1.7f || VillageTaskPositions.Any(task => Vector2.Distance(task,point)<1.8f)) continue;
                soil.map.SetTile(new Vector3Int(x,y,0),soil.tile);
                Prop(root,"Wheat",point);
            }
        Prop(root,"MarketAwning",new Vector2(17,12),new Vector2(1.7f,.7f));
        Prop(root,"MarketAwningGreen",new Vector2(19,-6.5f),new Vector2(1.7f,.7f));
        Prop(root,"Hay",new Vector2(0,15.5f),new Vector2(.9f,.45f));
        Prop(root,"Hay",new Vector2(5.5f,16.2f),new Vector2(.9f,.45f));
        foreach (var point in new[] { new Vector2(-.5f,16.5f), new Vector2(6.5f,16.7f), new Vector2(12,16.7f) })
            Prop(root,"Fence",point,new Vector2(1.4f,.25f));
        Prop(root,"CrateStack",new Vector2(14.5f,3),new Vector2(.9f,.5f));
        Prop(root,"Sacks",new Vector2(19,3.5f));
        Prop(root,"Planter",new Vector2(13,3.5f));
        Prop(root,"Cart",new Vector2(-11,16),new Vector2(1.4f,.6f));
        Prop(root,"Woodpile",new Vector2(-6,-13.8f));
        Prop(root,"Woodpile",new Vector2(-5,-13.5f));
        Prop(root,"Campfire",new Vector2(5,-9));

        // The memorial is an open garden: direct diagonal cut, north loop and south loop.
        Prop(root,"Memorial",new Vector2(-16,14.8f),new Vector2(1.5f,.7f));
        Prop(root,"GraveRound",new Vector2(-19,12.5f),new Vector2(.5f,.3f));
        Prop(root,"GraveRound",new Vector2(-14,12.5f),new Vector2(.5f,.3f));
        foreach (var point in new[] { new Vector2(-14,-1), new Vector2(6,5.5f), new Vector2(11,-5.5f) })
            Prop(root,"Signpost",point);

        // Sparse irregular groves replace the perimeter fence of identical trees.
        // Gaps line up with the outward paths, and all grass remains walkable.
        for (float x=-19; x<=21; x+=4.7f)
        {
            VillageTree(root,new Vector2(x,17.7f+Mathf.Sin(x*.7f)*.45f));
            VillageTree(root,new Vector2(x+.8f,-16.1f+Mathf.Sin(x*.5f)*.35f));
        }
        for (float y=-13; y<=16; y+=4.6f)
        {
            VillageTree(root,new Vector2(-19.1f+Mathf.Sin(y)*.3f,y));
            VillageTree(root,new Vector2(21.1f,y+1));
        }
        foreach (var point in new[] { new Vector2(-1,9.8f),new Vector2(0,8.5f),new Vector2(1.8f,10),
            new Vector2(6,-10),new Vector2(4.5f,-9.5f),new Vector2(5.5f,-10.5f),new Vector2(-16,-4.5f) }) VillageTree(root,point);
        foreach (var point in VillageLanterns) Prop(root,"Lantern",point);
    }

    private static readonly Vector2[] VillageTaskPositions =
    {
        new(-8,-4), new(-8,12), new(10,14), new(-8,-11), new(17,3),
        new(16,10), new(3,14), new(10,2), new(-17,12), new(9,-13)
    };
    private static readonly Vector2[] VillageLanterns =
    {
        new(-3.5f,-2.5f),new(5,3),new(-6,5),new(4,7),new(-10,9),new(7,14),
        new(18,6),new(13,-9),new(-10,-9),new(-16,6),new(-15,15),new(1,-15)
    };

    private readonly struct VillageRoute
    {
        public readonly bool dirt;
        public readonly float width;
        private readonly Vector2[] points;
        public VillageRoute(bool dirt,float width,params Vector2[] points)
        { this.dirt=dirt; this.width=width; this.points=points; }
        public float Distance(Vector2 point)
        {
            float result=float.PositiveInfinity;
            for (int i=1;i<points.Length;i++)
            {
                Vector2 segment=points[i]-points[i-1];
                float t=Mathf.Clamp01(Vector2.Dot(point-points[i-1],segment)/segment.sqrMagnitude);
                result=Mathf.Min(result,Vector2.Distance(point,points[i-1]+segment*t));
            }
            return result;
        }
    }

    // Interlocking loops, cross-town shortcuts and outward paths, rather than a radial grid.
    private static readonly VillageRoute[] VillageRoutes =
    {
        new(false,2.6f,new(1,0),new(-5,5),new(-8,12),new(-4,15),new(3,14),new(10,14),new(16,10),new(17,3),new(10,2),new(1,0)),
        new(false,2.6f,new(1,0),new(-8,-4),new(-14,-7),new(-13,-12),new(-8,-11),new(1,-13),new(9,-13),new(15,-10),new(17,-4),new(17,3)),
        new(false,2.3f,new(-8,12),new(-14,15),new(-17,12),new(-17,5),new(-15,1),new(-14,-7)),
        new(true,2.1f,new(-17,5),new(-10,7),new(-5,5),new(3,6),new(10,8),new(16,10)),
        new(true,2.1f,new(3,14),new(5,9),new(3,6),new(8,4),new(10,2),new(12,-3),new(9,-7),new(9,-13)),
        new(true,2f,new(-13,-12),new(-10,-7),new(-8,-4),new(-6,0),new(-7,2),new(-10,7),new(-14,15)),
        new(true,2.1f,new(-15,1),new(-7,2),new(-2,-4),new(3,-6),new(9,-7),new(15,-10)),
        new(true,2f,new(10,14),new(19,14),new(20,9),new(20,0),new(17,-4),new(19,-10),new(15,-10)),
        new(true,2f,new(-14,15),new(-18,16),new(-18,10),new(-19,5),new(-18,-3),new(-14,-7),new(-17,-12),new(-13,-12)),
        new(true,2f,new(-17,12),new(-13,10),new(-10,7)),
        new(false,2.6f,new(1,-13),new(1,-17)),
        new(true,2.2f,new(3,14),new(4,19)),
        new(true,2.2f,new(20,0),new(22,0)),
        new(true,2.2f,new(-18,-3),new(-20,-4))
    };

    private static float DistanceToVillageRoad(Vector2 point) => VillageRoutes.Min(route => route.Distance(point));
    private static void FillVillageTile(UnityEngine.Tilemaps.Tile tile)
    {
        // This existing soil sprite is smaller than a map cell and has a bottom pivot.
        // Center and fit only the new map's tiles so dirt paths form a continuous surface.
        Vector3 scale=new(1/tile.sprite.bounds.size.x,1/tile.sprite.bounds.size.y,1);
        tile.transform=Matrix4x4.TRS(-Vector3.Scale(tile.sprite.bounds.center,scale),Quaternion.identity,scale);
        EditorUtility.SetDirty(tile);
    }
    private static void VillageBuilding(Transform root,string sprite,Vector2 point,Vector2 footprint)
        => Prop(root,sprite,point,footprint).AddComponent<ThuongMapOccluder>();
    private static void VillageTree(Transform root,Vector2 point)
    {
        if (DistanceToVillageRoad(point)<1.8f || VillageTaskPositions.Any(task=>Vector2.Distance(task,point)<2)) return;
        Prop(root,"Tree",point,new Vector2(.6f,.4f));
    }

    public static void InstallLargeVillageAndVerify()
    {
        InstallLargeVillage();
        ThuongMapChecks.Run();
        ThuongMapVisualChecks.Run();
    }

    public static void RebuildOnlineVillageAndPlayers()
    {
        InstallLargeVillageAndVerify();
        ThuongOnlineMapSceneInstaller.Install();
        ThuongMapPlayerBuild.BuildBothPlayers();
    }
}
