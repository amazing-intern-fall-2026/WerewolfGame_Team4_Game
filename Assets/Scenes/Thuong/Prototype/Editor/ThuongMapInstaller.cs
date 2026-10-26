using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Thuong.Maps;
using Thuong.Minigames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public static partial class ThuongMapInstaller
{
    public const string Root = "Assets/Scenes/Thuong/Maps";
    private const string ExistingArt = "Assets/Scenes/Thuong/Prototype/Art/Medieval/Sprites/World/";
    private static Material material;
    private static readonly List<Rect> blockers = new();
    private static readonly string[] Ids = { "water", "harvest", "fence", "wood", "storage", "lamps", "eggs", "potion", "defender", "traps" };
    private static readonly string[] Icons = { "Well", "Wheat", "Fence", "Woodpile", "CrateStack", "Torch0", "Hay", "Planter", "Signpost", "Grave" };

    [MenuItem("Tools/Thuong/Maps/Install Maps And Events")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (string path in new[] { Root, Root + "/Prefabs", Root + "/Definitions", Root + "/Resources", Root + "/Art" })
            Directory.CreateDirectory(path);
        AssetDatabase.Refresh();
        foreach (string file in Directory.GetFiles(Root + "/Art", "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = file.EndsWith("TuongTheatreSign.png") ? 384 : file.EndsWith("Water.png") || file.EndsWith("FloorPlank.png") ? 32 : 48;
            importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            bool terrain = file.EndsWith("Water.png") || file.EndsWith("FloorPlank.png") || Path.GetFileName(file).StartsWith("SwampWater") || file.EndsWith("SwampGround.png") || file.EndsWith("RuralPath.png");
            settings.spriteAlignment = (int)(terrain ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
            settings.spritePivot = terrain ? new Vector2(.5f,.5f) : new Vector2(.5f, 0);
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
        material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Thuong/Prototype/Art/World.mat");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Thuong/OnlineSceneGamePlay.unity");
        var minigames = Resources.Load<ThuongMinigameCatalog>(ThuongMinigameCatalog.ResourcePath);
        if (minigames == null) throw new Exception("Install minigames first.");
        var original = scene.GetRootGameObjects().Single(r => r.name == "Map").transform.Find("Medieval Village");
        var definitions = new List<ThuongMapDefinition>();
        string[] ids = { "village", "harvest_market", "lantern_cemetery", "mist_swamp", "woodland_camp", "abandoned_manor" };
        string[] names = { "Làng Trung Tâm", "Chợ Mùa Gặt", "Nghĩa Trang Đèn Lồng", "Đầm Sương", "Trại Rừng", "Dinh Thự Bỏ Hoang" };
        for (int index = 0; index < ids.Length; index++)
        {
            blockers.Clear();
            GameObject world;
            if (index == 0)
            {
                world = Object.Instantiate(original.gameObject);
                foreach (var mood in world.GetComponentsInChildren<ThuongVillageMood>(true)) Object.DestroyImmediate(mood);
                foreach (var box in world.GetComponentsInChildren<BoxCollider2D>(true))
                {
                    Vector2 size = Vector2.Scale(box.size, box.transform.lossyScale);
                    Vector2 center = box.transform.TransformPoint(box.offset);
                    blockers.Add(new Rect(center - size / 2, size));
                }
            }
            else { world = new GameObject(names[index]); BuildWorld(world.transform, index); }
            world.name = "World " + ids[index];
            var definition = Asset<ThuongMapDefinition>(Root + "/Definitions/" + ids[index] + ".asset");
            definition.mapId = ids[index]; definition.displayName = names[index]; definition.contentRevision = index==3||index==4?2:1;
            definition.boundsMin = new(-13, -11); definition.boundsMax = new(15, 13);
            definition.obstacles = blockers.ToArray();
            definition.spawns = Enumerable.Range(0, 25).Select(i => new Vector2(1 + (i % 5 - 2) * .85f, (i / 5 - 2) * .85f)).ToArray();
            definition.plaza = new Vector2(1, 0); definition.gate = new Vector2(1, -9);
            definition.stage = new[] { new Vector2(-4,6),new Vector2(4,-8),new Vector2(5,-9),new Vector2(4,8),new Vector2(1,-8),new Vector2(-9,8) }[index];
            definition.lanterns = new[] { new Vector2(-4, -3), new Vector2(5, -3), new Vector2(-4, 4), new Vector2(6, 4), new Vector2(-9, 1), new Vector2(10, 0) };
            definition.stationPrefab = minigames.stationPrefab;
            definition.stations = BuildStations(index, minigames);
            definition.worldPrefab = PrefabUtility.SaveAsPrefabAsset(world, Root + "/Prefabs/" + ids[index] + ".prefab");
            // Render the actual world as a thumbnail, rather than a schematic mockup.
            definition.thumbnail = RenderThumbnail(world, ids[index]);
            definition.released = true;
            EditorUtility.SetDirty(definition); definitions.Add(definition); Object.DestroyImmediate(world);
        }
        var catalog = Asset<ThuongMapCatalog>(Root + "/Resources/ThuongMapCatalog.asset");
        catalog.maps = definitions.ToArray(); EditorUtility.SetDirty(catalog);
        var events = Asset<ThuongEventPresentationCatalog>(Root + "/Resources/ThuongEventPresentationCatalog.asset");
        events.lantern = Sprite("Lantern"); events.banner = Sprite("Banner"); events.stage = Sprite("StageBoards"); events.tuongMask = Sprite("TuongTheatreSign");
        events.events = Enumerable.Range(1, 6).Select(i => new ThuongEventVisual
        {
            type = (GameEventType)i, displayName = ThuongEventPresentationCatalog.Name((GameEventType)i), decoration = Sprite(i == 1 ? "Torch0" : "Lantern"),
            dayTint = i == 2 ? new Color(.75f, .85f, .88f) : i == 4 ? new Color(1, .96f, .86f) : Color.white,
            nightTint = i == 1 ? new Color(.65f, .25f, .3f) : i == 5 ? new Color(.42f, .57f, .69f) : new Color(.4f, .48f, .68f),
            volume = i == 1 || i == 4 ? 0 : i == 6 ? .025f : .012f
        }).ToArray(); EditorUtility.SetDirty(events);
        AssetDatabase.SaveAssets();
        InstallLargeVillage();
        Debug.Log("THUONG_MAPS_INSTALLED: seven map prefabs, stable task slots, thumbnails and six events.");
        ThuongMapChecks.Run();
    }

    private static ThuongMapStation[] BuildStations(int index, ThuongMinigameCatalog catalog)
    {
        Vector2[] positions = index == 0 ? new[] { new Vector2(-6,-2), new Vector2(-7,3), new Vector2(8,3), new Vector2(-7,-6), new Vector2(8,-4), new Vector2(1,5), new Vector2(-10,0), new Vector2(6,4), new Vector2(10,-2), new Vector2(-5,-5) } :
            index == 1 ? new[] { new Vector2(-8,2), new Vector2(-7,7), new Vector2(8,6), new Vector2(-7,-6), new Vector2(8,3), new Vector2(1,6), new Vector2(-9,-2), new Vector2(8,-3), new Vector2(9,-6), new Vector2(0,8) } :
            index == 2 ? new[] { new Vector2(-7,-4), new Vector2(-4,-7), new Vector2(7,-6), new Vector2(-8,-7), new Vector2(8,-3), new Vector2(1,7), new Vector2(-9,-1), new Vector2(6,-4), new Vector2(10,-8), new Vector2(0,5) } :
            new[] { new Vector2(-8,1), new Vector2(-6,6), new Vector2(8,5), new Vector2(-7,-5), new Vector2(8,-4), new Vector2(1,7), new Vector2(-8,-2), new Vector2(6,2), new Vector2(9,-7), new Vector2(-2,-7) };
        if (index == 4) positions = new[] {new Vector2(-6,2),new Vector2(-9,6),new Vector2(10,3),new Vector2(-8,-6),new Vector2(6,-4),new Vector2(1,7),new Vector2(-9,-3),new Vector2(6,6),new Vector2(10,-8),new Vector2(-3,-8)};
        if (index == 5) positions[1] = new Vector2(-6,4);
        return Enumerable.Range(0, 10).Select(slot => new ThuongMapStation
        {
            stationId = "thuong.task." + Ids[slot], maskSlot = slot, position = positions[slot], sprite = Sprite(Icons[slot]),
            task = slot < 6 ? AssetDatabase.LoadAssetAtPath<TaskData>("Assets/Scenes/Thuong/Prototype/Data/Task" + (slot + 1) + ".asset") :
                AssetDatabase.LoadAssetAtPath<TaskData>("Assets/Scenes/Thuong/Prototype/Integration/Minigames/Tasks/" + char.ToUpperInvariant(Ids[slot][0]) + Ids[slot].Substring(1) + ".asset")
        }).ToArray();
    }

    private static void BuildWorld(Transform root, int index)
    {
        var grid = new GameObject("Terrain Grid", typeof(Grid)); grid.transform.SetParent(root, false);
        var floor = Tiles(grid.transform, "Ground", Sprite(index == 5 ? "FloorPlank" : index==3?"SwampGround":"Grass"), -200);
        var lanes = Tiles(grid.transform, "Lanes", Sprite(index==3||index==4?"RuralPath":"Cobblestone"), -190);
        for (int y = -11; y < 13; y++) for (int x = -13; x < 15; x++)
        {
            var cell = new Vector3Int(x, y, 0); floor.map.SetTile(cell, floor.tile);
            if (Mathf.Abs(y) <= 1 || x >= 0 && x <= 2 || (y == 5 || y == -5) && x > -11 && x < 13 ||
                (x == -8 || x == 9) && y > -9 && y < 10) lanes.map.SetTile(cell, lanes.tile);
        }
        if (index == 1)
        {
            for (int i = 0; i < 3; i++)
            {
                Prop(root, i==0?"MarketStall":i==1?"MarketAwning":"MarketAwningGreen", new Vector2(-7 + i * 7, 9), new Vector2(1.7f, .7f));
                Prop(root, "Sacks", new Vector2(-8 + i * 7, 8));
            }
            Prop(root, "House1", new Vector2(10, 8), new Vector2(3.5f, 1.4f));
            for (int y = 6; y <= 8; y++) for (int x = -10; x < -4; x++) Prop(root, "Wheat", new Vector2(x, y));
            Prop(root, "Cart", new Vector2(-5, -8)); Prop(root, "Hay", new Vector2(-10, -4));
        }
        if (index == 2)
        {
            Prop(root, "Memorial", new Vector2(1, 9), new Vector2(1.5f, .7f));
            foreach (int side in new[] { -1, 1 }) for (int y = 2; y <= 8; y += 3) for (int x = 5; x <= 10; x += 3)
                Prop(root, (x + y) % 2 == 0 ? "Grave" : "GraveRound", new Vector2(side * x, y), new Vector2(.5f, .3f));
            Prop(root, "House2", new Vector2(-8, -9), new Vector2(3.3f, 1.1f));
            Prop(root, "House3", new Vector2(9, -5), new Vector2(2.5f, .8f));
            for (int x = -3; x <= 4; x++) Prop(root, "Fence", new Vector2(x, -9));
        }
        if (index == 3 || index == 4)
        {
            var water = SwampWater(grid.transform);
            // Two separated pools leave both north/south routes and broad bridges open.
            foreach (var pond in index==3 ? new[] { new Rect(-10, 3, 4, 2), new Rect(5, -1, 5, 2) } : new[] {new Rect(3,7,2,4)})
            {
                for (int y = (int)pond.yMin; y < pond.yMax; y++) for (int x = (int)pond.xMin; x < pond.xMax; x++)
                {
                    int row=y==(int)pond.yMax-1?0:y==(int)pond.yMin?2:1;
                    int column=x==(int)pond.xMin?0:x==(int)pond.xMax-1?2:1;
                    water.map.SetTile(new Vector3Int(x,y,0),water.tiles[row*3+column]);
                }
                if (pond.x < 0)
                {
                    Blocker(root,new Rect(-10,3,1.3f,2),"Water bank west"); Blocker(root,new Rect(-7.3f,3,1.3f,2),"Water bank east");
                    var bridge=Prop(root,"SwampBridge",new Vector2(-8,3)); bridge.GetComponent<ThuongDepthSort>().enabled=false; bridge.GetComponent<SpriteRenderer>().sortingOrder=-180;
                }
                else if(index==4)
                {
                    Blocker(root,new Rect(3,7,2,1.2f),"Stream south"); Blocker(root,new Rect(3,9.2f,2,1.8f),"Stream north");
                    var bridge=Prop(root,"SwampBridgeHorizontal",new Vector2(4,8.2f)); bridge.GetComponent<ThuongDepthSort>().enabled=false; bridge.GetComponent<SpriteRenderer>().sortingOrder=-180;
                }
                else Blocker(root, pond, "Water footprint");
            }
            Prop(root, "Tent", new Vector2(-6, 8), new Vector2(2.6f, 1));
            Prop(root, "RuralCabin", new Vector2(9, 8), new Vector2(4.6f, 1.7f));
            Prop(root, "Tent", new Vector2(-8, -8), new Vector2(2.6f, 1));
            Prop(root, "Campfire", new Vector2(5, -6));
            if (index == 3)
            {
                var altar=Prop(root,"SwampAltar0",new Vector2(0,9),new Vector2(1.6f,.65f));
                var loop=altar.AddComponent<ThuongSpriteLoop>(); loop.frames=Enumerable.Range(0,4).Select(i=>Sprite("SwampAltar"+i)).ToArray(); loop.framesPerSecond=6; loop.staysBrightAtNight=true;
                foreach(var point in new[]{new Vector2(-10,2.5f),new Vector2(-6,5.2f),new Vector2(5,1.2f),new Vector2(10,-1.5f)}) Prop(root,"SwampReed",point);
            }
            else for (int i=0;i<4;i++) Prop(root,"Woodpile",new Vector2(-10+i*1.5f,-6.5f));
            for (int i = 0; i < 5; i++) Prop(root, "Tree", new Vector2(-10 + i * 4.8f, 10.5f), new Vector2(.6f, .4f));
        }
        if (index == 5)
        {
            // Open twin corridors around four rooms; high walls fade for nearby avatars.
            foreach (Vector2 center in new[] { new Vector2(-5, 4), new Vector2(7, 4), new Vector2(-5,-5), new Vector2(7,-5) })
            {
                for (int x=-2;x<=2;x++)
                { var wall=Prop(root,"ManorWall",center+new Vector2(x,1.7f)); wall.AddComponent<ThuongMapOccluder>(); }
                Blocker(root,new Rect(center+new Vector2(-2.5f,1.7f),new Vector2(5,.45f)),"Interior wall footprint");
            }
            Prop(root, "Memorial", new Vector2(1, 10), new Vector2(1.6f, .6f));
        }
        for (int x = -12; x <= 14; x += 2)
        {
            Prop(root, "Tree", new Vector2(x, 11.5f), new Vector2(.6f, .4f));
            if (Mathf.Abs(x - 1) > 3) Prop(root, "Tree", new Vector2(x, -10.6f), new Vector2(.6f, .25f));
        }
        for (int y = -8; y <= 10; y += 3)
        { Prop(root, "Tree", new Vector2(-12.3f, y), new Vector2(.5f,.4f)); Prop(root, "Tree", new Vector2(14.3f,y), new Vector2(.5f,.4f)); }
        foreach (Vector2 p in new[] { new Vector2(-4,-3), new Vector2(5,-3), new Vector2(-4,4), new Vector2(6,4) }) Prop(root, "Lantern", p);
    }

    private static GameObject Prop(Transform root, string name, Vector2 position, Vector2 size = default)
    {
        var prop = new GameObject(name, typeof(SpriteRenderer), typeof(ThuongDepthSort)); prop.transform.SetParent(root, false); prop.transform.position = position;
        var renderer = prop.GetComponent<SpriteRenderer>(); renderer.sprite = Sprite(name); renderer.sharedMaterial = material;
        if (size != Vector2.zero) Blocker(prop.transform, new Rect(position - new Vector2(size.x / 2, 0), size), name + " footprint");
        return prop;
    }
    private static void Blocker(Transform root, Rect rect, string name)
    {
        blockers.Add(rect); var obstacle = new GameObject(name, typeof(BoxCollider2D)); obstacle.transform.SetParent(root, false);
        obstacle.transform.position = rect.center; obstacle.GetComponent<BoxCollider2D>().size = rect.size;
    }
    private static (Tilemap map, Tile tile) Tiles(Transform root, string name, Sprite sprite, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer)); go.transform.SetParent(root, false);
        go.GetComponent<TilemapRenderer>().sortingOrder = order; go.GetComponent<TilemapRenderer>().sharedMaterial = material;
        var tile = Asset<Tile>(Root + "/Definitions/" + name.Replace(" ", "") + "Tile_" + sprite.name + ".asset");
        tile.sprite = sprite; tile.colliderType = Tile.ColliderType.None; EditorUtility.SetDirty(tile);
        return (go.GetComponent<Tilemap>(), tile);
    }
    private static (Tilemap map,AnimatedTile[] tiles) SwampWater(Transform root)
    {
        var go=new GameObject("Animated swamp water",typeof(Tilemap),typeof(TilemapRenderer)); go.transform.SetParent(root,false);
        go.GetComponent<TilemapRenderer>().sortingOrder=-185; go.GetComponent<TilemapRenderer>().sharedMaterial=material;
        var map=go.GetComponent<Tilemap>(); map.animationFrameRate=4;
        var tiles=new AnimatedTile[9];
        for(int row=0;row<3;row++) for(int column=0;column<3;column++)
        {
            var tile=Asset<AnimatedTile>(Root+"/Definitions/SwampWater"+row+column+".asset");
            tile.m_AnimatedSprites=new[]{0,1,2,1}.Select(frame=>Sprite("SwampWater"+row+column+"_"+frame)).ToArray();
            tile.m_MinSpeed=tile.m_MaxSpeed=1; tile.m_TileColliderType=Tile.ColliderType.None;
            EditorUtility.SetDirty(tile); tiles[row*3+column]=tile;
        }
        return(map,tiles);
    }
    private static Sprite RenderThumbnail(GameObject world, string id, Vector2? boundsMin = null, Vector2? boundsMax = null)
    {
        var cameraRoot = new GameObject("Map preview", typeof(Camera)); var camera = cameraRoot.GetComponent<Camera>();
        camera.scene = world.scene;
        camera.orthographic = true; camera.orthographicSize = 13; camera.transform.position = new Vector3(1,1,-10);
        if (boundsMin.HasValue && boundsMax.HasValue)
        {
            Vector2 size = boundsMax.Value - boundsMin.Value;
            camera.orthographicSize = Mathf.Max(size.y / 2, size.x / (2 * (512f / 448))) + 1;
            Vector2 center = (boundsMin.Value + boundsMax.Value) / 2;
            camera.transform.position = new Vector3(center.x, center.y, -10);
        }
        camera.backgroundColor = new Color(.14f,.17f,.13f); camera.clearFlags = CameraClearFlags.SolidColor;
        var texture = new RenderTexture(512, 448, 24); camera.targetTexture = texture;
        var previous = RenderTexture.active;
        // Only this world is visible while capturing, without mutating the saved scene.
        foreach (Transform child in world.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        camera.cullingMask = 1 << 31; camera.Render(); RenderTexture.active = texture;
        var image = new Texture2D(512,448,TextureFormat.RGBA32,false); image.ReadPixels(new Rect(0,0,512,448),0,0); image.Apply();
        string path = Root + "/Art/Thumbnail_" + id + ".png"; File.WriteAllBytes(path,image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(image); Object.DestroyImmediate(cameraRoot);
        foreach (Transform child in world.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 0;
        AssetDatabase.ImportAsset(path); var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite;
        importer.mipmapEnabled=false; importer.filterMode=FilterMode.Point; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/" + name + ".png") ?? AssetDatabase.LoadAssetAtPath<Sprite>(ExistingArt + name + ".png");
    private static T Asset<T>(string path) where T : ScriptableObject
    { var value = AssetDatabase.LoadAssetAtPath<T>(path); if (value == null) { value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value,path); } return value; }
}
