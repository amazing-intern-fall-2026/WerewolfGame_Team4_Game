using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Thuong.Maps;
using Thuong.Minigames;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public static class ThuongExpandedVillageInstaller
{
    public const string MapId = "village_expanded";
    public const string DefinitionPath = ThuongMapInstaller.Root + "/Definitions/" + MapId + ".asset";
    private const int LayoutRevision = 2;
    private static readonly Vector2 Min = new(-20, -17), Max = new(20, 17);
    private static readonly List<Rect> Obstacles = new();
    private static Material material;
    private static bool[,] walkable;
    private static ThuongMapZone[] zones;
    private static ThuongMapConnection[] connections;

    [MenuItem("Tools/Thuong/Maps/Install Expanded Village")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var minigames = Resources.Load<ThuongMinigameCatalog>(ThuongMinigameCatalog.ResourcePath);
        var catalog = Resources.Load<ThuongMapCatalog>(ThuongMapCatalog.ResourcePath);
        if (catalog == null || minigames == null || !minigames.Validate(out _))
            throw new InvalidOperationException("Install the existing maps and minigames first.");
        material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Thuong/Prototype/Art/World.mat");
        zones = BuildZones(); connections = BuildConnections(); Obstacles.Clear();
        walkable = new bool[40, 34];
        var world = new GameObject("World " + MapId);
        // An isolated preview scene prevents other worlds/cameras appearing in captures.
        var scene = EditorSceneManager.NewPreviewScene();
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(world, scene);
        try
        {
            BuildTerrain(world.transform);
            BuildLandmarks(world.transform);
            var definition = AssetDatabase.LoadAssetAtPath<ThuongMapDefinition>(DefinitionPath);
            bool fresh = definition == null;
            if (fresh) { definition = ScriptableObject.CreateInstance<ThuongMapDefinition>(); AssetDatabase.CreateAsset(definition, DefinitionPath); }
            definition.mapId = MapId; definition.displayName = "Làng Liên Hoàn";
            definition.contentRevision = Mathf.Max(LayoutRevision, definition.contentRevision);
            definition.boundsMin = Min; definition.boundsMax = Max;
            definition.zones = zones; definition.connections = connections;
            definition.obstacles = Obstacles.ToArray();
            definition.spawns = Enumerable.Range(0, 25).Select(i => new Vector2(.5f + (i % 5 - 2) * .85f, .5f + (i / 5 - 2) * .85f)).ToArray();
            definition.plaza = new Vector2(.5f, .5f); definition.gate = new Vector2(.5f, -13.4f);
            definition.stage = new Vector2(.5f, -15.2f);
            definition.lanterns = new[] { new Vector2(-10,2.6f), new Vector2(10,2.6f), new Vector2(-2,8),
                new Vector2(3,-7), new Vector2(-10,8), new Vector2(10,-8), new Vector2(-3,2.8f), new Vector2(4,2.8f) };
            definition.stationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/Thuong/Prototype/Integration/Prefabs/TaskStation.prefab");
            definition.stations = Stations(minigames);
            definition.worldPrefab = PrefabUtility.SaveAsPrefabAsset(world, ThuongMapInstaller.Root + "/Prefabs/" + MapId + ".prefab");
            definition.thumbnail = Render(world, definition, ThuongMapInstaller.Root + "/Art/Thumbnail_" + MapId + ".png", 640, 544);
            definition.released = true;
            if (!ThuongMapValidator.Validate(definition, out string error)) throw new Exception(error);
            catalog.maps = catalog.maps.Where(m => m != null && m.mapId != MapId).Append(definition).ToArray();
            EditorUtility.SetDirty(definition); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            Debug.Log("THUONG_EXPANDED_VILLAGE_INSTALLED: 9 zones, 14 connections, 6 minigames, 25 spawns.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        ThuongExpandedVillageChecks.Run();
    }

    public static void InstallAndRender()
    {
        Install(); ThuongExpandedVillageChecks.RenderViews();
    }

    public static void VerifyAndBuild()
    {
        InstallAndRender();
        ThuongMapChecks.Run();
        ThuongMapPlayerBuild.BuildBothPlayers();
    }

    private static ThuongMapZone[] BuildZones()
    {
        return new[]
        {
            Zone("forest", "RỪNG BẪY", -13.5f,10.5f, new Vector2(-11,12)),
            Zone("fields", "RUỘNG LÚA", .5f,11.5f, new Vector2(3,10)),
            Zone("coop", "CHUỒNG TRẠI", 13.5f,10.5f, new Vector2(11,8)),
            Zone("workshop", "XƯỞNG MỘC", -13.5f,.5f, new Vector2(-11,2)),
            Zone("plaza", "QUẢNG TRƯỜNG", .5f,.5f, new Vector2(-2,.5f)),
            Zone("apothecary", "NHÀ THUỐC", 13.5f,.5f, new Vector2(11,-1.5f)),
            Zone("warehouse", "KHO HÀNG", -13.5f,-10.5f, new Vector2(-11,-11)),
            Zone("market", "CHỢ / CỔNG NAM", .5f,-10.5f, new Vector2(.5f,-12.5f)),
            Zone("watch", "TRẠM GÁC", 13.5f,-10.5f, new Vector2(11,-12))
        };
    }

    private static ThuongMapZone Zone(string id, string name, float x, float y, Vector2 futureTask)
        => new() { zoneId = id, displayName = name, area = new Rect(x - 4.5f, y - 3.5f, 9, 7), reservedTaskPoint = futureTask };

    private static ThuongMapConnection[] BuildConnections()
    {
        return new[]
        {
            Connect("forest","fields"), Connect("fields","coop"), Connect("coop","apothecary"),
            Connect("apothecary","watch"), Connect("watch","market"), Connect("market","warehouse"),
            Connect("warehouse","workshop"), Connect("workshop","forest"), Connect("fields","plaza"),
            Connect("workshop","plaza"), Connect("plaza","apothecary"), Connect("plaza","market"),
            Connect("workshop","fields",true), Connect("apothecary","market",true)
        };
    }

    private static ThuongMapConnection Connect(string from, string to, bool shortcut = false)
        => new() { connectionId = from + "." + to, fromZone = from, toZone = to, shortcut = shortcut,
            width = shortcut ? 2.2f : 3, points = new[] { zones.Single(z => z.zoneId == from).area.center, zones.Single(z => z.zoneId == to).area.center } };

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        => Vector2.Distance(p, a + (b - a) * Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude));

    private static void BuildTerrain(Transform root)
    {
        var terrain = new GameObject("Terrain", typeof(Grid)); terrain.transform.SetParent(root, false);
        var ground = Tiles(terrain.transform, "Grass", Sprite("Grass"), -200, Color.white);
        var gardens = Tiles(terrain.transform, "Enclosed gardens", Sprite("Grass"), -195, new Color(.64f,.78f,.62f));
        var lanes = Tiles(terrain.transform, "Public roads", Sprite("Cobblestone"), -190, Color.white);
        var alleys = Tiles(terrain.transform, "Public shortcuts", Sprite("RuralPath"), -189, Color.white);
        for (int y = 0; y < 34; y++) for (int x = 0; x < 40; x++)
        {
            Vector2 center = Min + new Vector2(x + .5f, y + .5f);
            var cell = new Vector3Int(x - 20,y - 17,0);
            ground.map.SetTile(cell, ground.tile);
            bool room = zones.Any(z => z.area.Contains(center));
            bool road = connections.Any(c => !c.shortcut && SegmentDistance(center,c.points[0],c.points[1]) <= c.width / 2);
            bool alley = connections.Any(c => c.shortcut && SegmentDistance(center,c.points[0],c.points[1]) <= c.width / 2);
            walkable[x,y] = room || road || alley;
            if (road || room && zones.Any(z => z.area.Contains(center) && Vector2.Distance(center,z.area.center) < 2.2f)) lanes.map.SetTile(cell,lanes.tile);
            else if (alley) alleys.map.SetTile(cell,alleys.tile);
            if (!walkable[x,y]) gardens.map.SetTile(cell,gardens.tile);
        }
        MergeGardenObstacles(root);
        var hedges = new GameObject("Garden boundaries").transform; hedges.SetParent(root,false);
        for (int y = 0; y < 34; y++) for (int x = 0; x < 40; x++)
        {
            if (walkable[x,y]) continue;
            Vector2 p = Min + new Vector2(x+.5f,y+.5f);
            if (FreeCell(x,y-1)) Fence(hedges,p+Vector2.down*.5f,false);
            if (FreeCell(x,y+1)) Fence(hedges,p+Vector2.up*.5f,false);
            if (FreeCell(x-1,y)) Fence(hedges,p+Vector2.left*.5f,true);
            if (FreeCell(x+1,y)) Fence(hedges,p+Vector2.right*.5f,true);
            if (x%2==0 && y%2==0) Prop(hedges,"Tree",p,default,.8f);
        }
        // Physical perimeter coincides with map bounds; trees make that edge legible.
        for (int x = -19; x <= 19; x += 2)
        { Prop(hedges,"Tree",new Vector2(x,16),default,.85f); Prop(hedges,"Tree",new Vector2(x,-16.7f),default,.85f); }
    }

    private static bool FreeCell(int x,int y) => x>=0 && y>=0 && x<40 && y<34 && walkable[x,y];

    private static void MergeGardenObstacles(Transform root)
    {
        var previous = new Dictionary<(int start,int length),int>();
        for (int y=0;y<34;y++)
        {
            var current = new Dictionary<(int,int),int>();
            for (int x=0;x<40;)
            {
                if (walkable[x,y]) { x++; continue; }
                int start=x; while(x<40 && !walkable[x,y]) x++;
                var key=(start,x-start);
                if (previous.TryGetValue(key,out int index))
                { Rect rect=Obstacles[index]; rect.height++; Obstacles[index]=rect; current[key]=index; }
                else { current[key]=Obstacles.Count; Obstacles.Add(new Rect(Min.x+start,Min.y+y,x-start,1)); }
            }
            previous=current;
        }
        foreach (var rect in Obstacles.ToArray()) PhysicalBox(root,rect,"Enclosed garden");
    }

    private static void Fence(Transform root,Vector2 position,bool vertical)
    {
        var prop=Prop(root,"Fence",position);
        // Rotate a low boundary rail, rather than blocking with the tall tree canopy.
        prop.transform.localScale=new Vector3(.7f,.7f,1);
        if(vertical) prop.transform.rotation=Quaternion.Euler(0,0,90);
    }

    private static void BuildLandmarks(Transform root)
    {
        var props=new GameObject("Village landmarks").transform; props.SetParent(root,false);
        Prop(props,"Tent",new(-16,12.4f),new(2, .6f));
        Prop(props,"Woodpile",new(-16,8),new(1,.45f));
        for(int y=12;y<=14;y++) for(int x=-3;x<=-1;x++) Prop(props,"Wheat",new(x,y));
        Prop(props,"Hay",new(3,13),new(.8f,.4f));
        Prop(props,"Cart",new(3,9),new(.9f,.5f));
        House(props,"House2",new(16,11.5f));
        Prop(props,"Hay",new(16.5f,8),new(.8f,.4f));
        Prop(props,"Sacks",new(10,12),new(.6f,.4f));
        House(props,"House1",new(-16,2.5f));
        Prop(props,"Woodpile",new(-16,-1.7f),new(1,.45f));
        Prop(props,"Cart",new(-11,-2),new(.9f,.5f));
        Prop(props,"Well",new(-2.7f,2.8f),new(.8f,.45f));
        Prop(props,"Signpost",new(3.3f,2.8f));
        House(props,"House3",new(16,2.5f));
        Prop(props,"Planter",new(16,-2),new(1.4f,.35f));
        Prop(props,"Barrel",new(17,-2),new(.55f,.4f));
        House(props,"House2",new(-16,-8.6f));
        Prop(props,"CrateStack",new(-11,-12.3f),new(.8f,.5f));
        Prop(props,"Cart",new(-16,-12.5f),new(.9f,.5f));
        Prop(props,"Sacks",new(-10,-8.5f),new(.6f,.4f));
        Prop(props,"MarketStall",new(-2,-8.5f),new(1.6f,.6f));
        Prop(props,"MarketAwningGreen",new(3,-12.5f),new(1.6f,.6f));
        Prop(props,"Crate",new(-2,-12.5f),new(.6f,.4f));
        Prop(props,"Memorial",new(16,-8.5f),new(1.2f,.6f));
        Prop(props,"Signpost",new(10.3f,-12.5f));
        Prop(props,"Torch0",new(16,-12.5f));
        foreach(var zone in zones)
        {
            Prop(props,"Torch0",new Vector2(zone.area.xMin+.6f,zone.area.yMin+.4f));
            var label=new GameObject("Zone name - "+zone.zoneId,typeof(TextMeshPro)); label.transform.SetParent(root,false);
            label.transform.position=new Vector3(zone.area.center.x,zone.area.yMin+.5f,-.1f);
            var text=label.GetComponent<TextMeshPro>();
            text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Scenes/Thuong/Prototype/Art/PrototypeFont.asset");
            text.text=zone.displayName; text.fontSize=3.2f; text.alignment=TextAlignmentOptions.Center;
            text.color=new Color(1,.93f,.68f); text.rectTransform.sizeDelta=new Vector2(7,.7f);
            text.GetComponent<MeshRenderer>().sortingOrder=4000;
        }
    }

    private static void House(Transform root,string sprite,Vector2 position)
        => Prop(root,sprite,position,new Vector2(3.3f,1)).AddComponent<ThuongMapOccluder>();

    private static ThuongMapStation[] Stations(ThuongMinigameCatalog catalog)
    {
        var anchors=new Dictionary<string,(Vector2 p,string art)>
        {
            ["thuong.task.wood"]=(new(-15.5f,.5f),"Woodpile"),
            ["thuong.task.harvest"]=(new(-1.5f,12),"Wheat"),
            ["thuong.task.eggs"]=(new(15.5f,10),"Hay"),
            ["thuong.task.potion"]=(new(15.5f,.5f),"Planter"),
            ["thuong.task.defender"]=(new(15.5f,-10.5f),"Signpost"),
            ["thuong.task.traps"]=(new(-15,10.5f),"Grave")
        };
        return catalog.entries.Select((e,slot)=>new ThuongMapStation { stationId=e.taskId,maskSlot=slot,task=e.task,
            position=anchors[e.taskId].p,sprite=Sprite(anchors[e.taskId].art) }).ToArray();
    }

    private static GameObject Prop(Transform root,string name,Vector2 position,Vector2 footprint=default,float scale=1)
    {
        var prop=new GameObject(name,typeof(SpriteRenderer),typeof(ThuongDepthSort)); prop.transform.SetParent(root,false);
        prop.transform.position=position; prop.transform.localScale=Vector3.one*scale;
        var renderer=prop.GetComponent<SpriteRenderer>(); renderer.sprite=Sprite(name); renderer.sharedMaterial=material;
        if(renderer.sprite==null) throw new InvalidOperationException("Missing map sprite: "+name);
        if(footprint!=Vector2.zero)
        {
            var rect=new Rect(position-new Vector2(footprint.x/2,0),footprint);
            Obstacles.Add(rect); PhysicalBox(root,rect,name+" footprint");
        }
        return prop;
    }

    private static void PhysicalBox(Transform root,Rect rect,string name)
    {
        var obj=new GameObject(name,typeof(BoxCollider2D)); obj.transform.SetParent(root,false);
        obj.transform.position=rect.center; obj.GetComponent<BoxCollider2D>().size=rect.size;
    }

    private static (Tilemap map,Tile tile) Tiles(Transform root,string name,Sprite sprite,int order,Color color)
    {
        var obj=new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer)); obj.transform.SetParent(root,false);
        var renderer=obj.GetComponent<TilemapRenderer>(); renderer.sortingOrder=order; renderer.sharedMaterial=material;
        string path=ThuongMapInstaller.Root+"/Definitions/Expanded"+name.Replace(" ","")+".asset";
        var tile=AssetDatabase.LoadAssetAtPath<Tile>(path);
        if(tile==null) { tile=ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile,path); }
        tile.sprite=sprite; tile.color=color; tile.colliderType=Tile.ColliderType.None; EditorUtility.SetDirty(tile);
        return(obj.GetComponent<Tilemap>(),tile);
    }

    private static Sprite Sprite(string name)
        => AssetDatabase.LoadAssetAtPath<Sprite>(ThuongMapInstaller.Root+"/Art/"+name+".png") ??
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/Thuong/Prototype/Art/Medieval/Sprites/World/"+name+".png");

    public static Sprite Render(GameObject world,ThuongMapDefinition map,string path,int width,int height)
    {
        var cameraRoot=new GameObject("Expanded map preview",typeof(Camera));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraRoot,world.scene);
        var camera=cameraRoot.GetComponent<Camera>(); camera.scene=world.scene;
        camera.orthographic=true; camera.aspect=(float)width/height;
        camera.orthographicSize=Mathf.Max((map.boundsMax.y-map.boundsMin.y)/2+1.5f,(map.boundsMax.x-map.boundsMin.x)/(2*camera.aspect)+1.5f);
        Vector2 center=(map.boundsMin+map.boundsMax)/2;
        camera.transform.position=new Vector3(center.x,center.y,-10); camera.backgroundColor=new Color(.12f,.16f,.12f); camera.clearFlags=CameraClearFlags.SolidColor;
        var target=new RenderTexture(width,height,24); var previous=RenderTexture.active;
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
        try
        {
            camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path,texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous; camera.targetTexture=null; target.Release();
            Object.DestroyImmediate(target); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraRoot);
        }
        if(!path.StartsWith("Assets/",StringComparison.Ordinal)) return null;
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite;
        importer.mipmapEnabled=false; importer.filterMode=FilterMode.Point; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
