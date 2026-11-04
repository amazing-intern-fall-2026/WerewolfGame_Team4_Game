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

public static class ThuongExpandedMapsInstaller
{
    public static readonly string[] MapIds = { "harvest_market_expanded", "lantern_cemetery_expanded", "mist_swamp_expanded", "woodland_camp_expanded", "abandoned_manor_expanded" };
    private enum Theme { Market, Cemetery, Swamp, Camp, Manor }
    private sealed class Layout
    {
        public string id, name;
        public Theme theme;
        public string[] names, landmarks;
        public Vector2[] centers;
        public int[] taskZones;
        public (int,int)[] shortcuts;
    }

    private const string Root = ThuongMapInstaller.Root;
    private const int Revision = 2;
    private static readonly Vector2 Min = new(-20,-17), Max = new(20,17);
    private static readonly string[] ZoneIds = { "north_west", "north", "north_east", "west", "plaza", "east", "south_west", "south", "south_east" };
    private static readonly string[] TaskIds = { "wood", "harvest", "eggs", "potion", "defender", "traps" };
    private static readonly string[] TaskArt = { "Woodpile", "Wheat", "Hay", "Planter", "Signpost", "Grave" };
    private static readonly (int,int)[] MainEdges = { (0,1),(1,2),(2,5),(5,8),(8,7),(7,6),(6,3),(3,0),(1,4),(3,4),(4,5),(4,7) };

    [MenuItem("Tools/Thuong/Maps/Install Other Expanded Maps")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var catalog=Resources.Load<ThuongMapCatalog>(ThuongMapCatalog.ResourcePath);
        var minigames=Resources.Load<ThuongMinigameCatalog>(ThuongMinigameCatalog.ResourcePath);
        if(catalog==null||minigames==null||!minigames.Validate(out _)) throw new InvalidOperationException("Existing maps/minigames are required.");
        var created=new List<ThuongMapDefinition>();
        foreach(var layout in Layouts())
        {
            var map=new Builder(layout,minigames).Build();
            ThuongExpandedVillageChecks.CheckMap(map,"Logs/ExpandedMaps/"+map.mapId);
            created.Add(map);
        }
        // Publish only after every layout has passed its content/collision checks.
        foreach(var map in created) { map.released=true; EditorUtility.SetDirty(map); }
        catalog.maps=catalog.maps.Where(m=>m!=null&&!MapIds.Contains(m.mapId)).Concat(created).ToArray();
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        Debug.Log("THUONG_OTHER_EXPANDED_MAPS_INSTALLED: five distinct layouts; originals and expanded village retained.");
    }

    [MenuItem("Tools/Thuong/Maps/Check Other Expanded Maps")]
    public static void Check()
    {
        foreach(string id in MapIds)
            ThuongExpandedVillageChecks.CheckMap(AssetDatabase.LoadAssetAtPath<ThuongMapDefinition>(Root+"/Definitions/"+id+".asset"),"Logs/ExpandedMaps/"+id);
        ThuongMapChecks.Run();
    }

    [MenuItem("Tools/Thuong/Maps/Render Other Expanded Maps")]
    public static void RenderViews()
    {
        foreach(string id in MapIds)
            ThuongExpandedVillageChecks.RenderViews(AssetDatabase.LoadAssetAtPath<ThuongMapDefinition>(Root+"/Definitions/"+id+".asset"),"Logs/ExpandedMaps/"+id);
    }

    public static void InstallAndRender() { Install(); RenderViews(); ContactSheet(); ThuongExpandedVillageChecks.Run(); ThuongMapChecks.Run(); }
    public static void VerifyAndBuild() { InstallAndRender(); ThuongMapPlayerBuild.BuildBothPlayers(); }
    public static void VerifyIntegrationAndBuild() { ThuongMapInstaller.InstallLargeVillage(); ThuongExpandedVillageChecks.Run(); Check(); ThuongMapPlayerBuild.BuildBothPlayers(); }

    public static void ContactSheet()
    {
        string[] ids=new[]{ThuongExpandedVillageInstaller.MapId}.Concat(MapIds).ToArray();
        var sheet=new Texture2D(1920,1088,TextureFormat.RGBA32,false);
        try
        {
            for(int i=0;i<ids.Length;i++)
            {
                var tile=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    if(!tile.LoadImage(File.ReadAllBytes(Root+"/Art/Thumbnail_"+ids[i]+".png"))||tile.width!=640||tile.height!=544)
                        throw new InvalidOperationException("Unexpected map thumbnail dimensions: "+ids[i]);
                    sheet.SetPixels32(i%3*640,(1-i/3)*544,640,544,tile.GetPixels32());
                }
                finally { Object.DestroyImmediate(tile); }
            }
            sheet.Apply(); Directory.CreateDirectory("Logs/ExpandedMaps");
            File.WriteAllBytes("Logs/ExpandedMaps/all-expanded-maps.png",sheet.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(sheet); }
    }

    private static Vector2[] Grid(float left=-13.5f,float right=13.5f)
        => new[] { new Vector2(left,10.5f),new Vector2(.5f,11.5f),new Vector2(right,10.5f),
            new Vector2(left,.5f),new Vector2(.5f,.5f),new Vector2(right,.5f),
            new Vector2(left,-10.5f),new Vector2(.5f,-10.5f),new Vector2(right,-10.5f) };

    private static IEnumerable<Layout> Layouts()
    {
        yield return new Layout { id=MapIds[0],name="Chợ Mùa Gặt Mở Rộng",theme=Theme.Market,centers=Grid(),
            names=new[]{"LỀU THỢ SĂN","RUỘNG HÀNG","CHUỒNG CHỢ","KHO GỖ","SÂN CHỢ","QUẦY THUỐC","KHO HÀNG","CỔNG CHỢ","CHỐT GÁC"},
            landmarks=new[]{"Tent","Hay","House2","Woodpile","Well","MarketAwning","CrateStack","MarketStall","Signpost"},
            taskZones=new[]{3,1,2,5,8,0},shortcuts=new[]{(1,5),(3,7)} };
        var cemetery=Grid(); cemetery[1]=new(-.5f,11.5f); cemetery[3]=new(-14.5f,.5f); cemetery[4]=new(.5f,-.5f); cemetery[5]=new(14.5f,-.5f); cemetery[7]=new(.5f,-11.5f);
        yield return new Layout { id=MapIds[1],name="Nghĩa Trang Đèn Lồng Mở Rộng",theme=Theme.Cemetery,centers=cemetery,
            names=new[]{"MỘ CỔ","ĐÀI TƯỞNG NIỆM","VƯỜN NGƯỜI GÁC","NHÀ GIỮ MỘ","SÂN ĐÈN LỒNG","NHÀ THẢO DƯỢC","BÃI CỦI","CỔNG NGHĨA TRANG","CHỐT GÁC"},
            landmarks=new[]{"GraveRound","Memorial","Hay","House3","Lantern","Planter","Woodpile","Memorial","Tent"},
            taskZones=new[]{6,2,3,5,8,0},shortcuts=new[]{(0,4),(4,8)} };
        var swamp=Grid(); swamp[3]=new(-13.5f,-.5f); swamp[7]=new(.5f,-11.5f);
        yield return new Layout { id=MapIds[2],name="Đầm Sương Liên Đảo",theme=Theme.Swamp,centers=swamp,
            names=new[]{"TRẠI BẪY","VƯỜN NỔI","CHÒI CHĂN NUÔI","BẾN GỖ","ĐẢO TRUNG TÂM","LỀU THẢO DƯỢC","KHO VEN ĐẦM","BẾN NAM","ĐẢO CANH GÁC"},
            landmarks=new[]{"Tent","Planter","RuralCabin","Woodpile","SwampAltar0","Tent","CrateStack","Cart","Memorial"},
            taskZones=new[]{3,1,2,5,8,0},shortcuts=new[]{(3,7),(1,5)} };
        var camp=Grid(-12.5f,12.5f); camp[2]=new(12.5f,9.5f); camp[3]=new(-14.5f,.5f); camp[4]=new(-.5f,.5f); camp[5]=new(14.5f,.5f); camp[7]=new(.5f,-11.5f);
        yield return new Layout { id=MapIds[3],name="Trại Rừng Nhiều Lối",theme=Theme.Camp,centers=camp,
            names=new[]{"BÃI CỦI","VƯỜN TRẠI","CHÒI GÁC BẮC","LỀU THỢ SĂN","SÂN LỬA TRẠI","LỀU THẢO DƯỢC","CHUỒNG TRẠI","CỔNG TRẠI","KHO DỤNG CỤ"},
            landmarks=new[]{"Woodpile","Hay","RuralCabin","Tent","Campfire","Tent","House3","Signpost","CrateStack"},
            taskZones=new[]{0,1,6,5,2,3},shortcuts=new[]{(0,4),(6,4)} };
        var manor=Grid(); manor[1]=new(.5f,10.5f);
        yield return new Layout { id=MapIds[4],name="Dinh Thự Hai Cánh",theme=Theme.Manor,centers=manor,
            names=new[]{"KHO GỖ","NHÀ KÍNH","NHÀ BẾP","HÀNH LANG TÂY","ĐẠI SẢNH","PHÒNG DƯỢC","KHO DỤNG CỤ","CỬA CHÍNH","PHÒNG BẢO VỆ"},
            landmarks=new[]{"Woodpile","Planter","Sacks","Curtain","Memorial","Planter","CrateStack","Banner","Signpost"},
            taskZones=new[]{0,1,2,5,8,6},shortcuts=new[]{(1,3),(5,7)} };
    }

    private sealed class Builder
    {
        private readonly Layout layout;
        private readonly ThuongMinigameCatalog minigames;
        private readonly Material material;
        private readonly List<Rect> obstacles=new();
        private readonly bool[,] free=new bool[40,34];
        private ThuongMapDefinition map;
        private Transform root;
        private int placedProps;

        public Builder(Layout source,ThuongMinigameCatalog tasks)
        {
            layout=source; minigames=tasks;
            material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Thuong/Prototype/Art/World.mat");
        }

        public ThuongMapDefinition Build()
        {
            string path=Root+"/Definitions/"+layout.id+".asset";
            map=AssetDatabase.LoadAssetAtPath<ThuongMapDefinition>(path);
            if(map==null) { map=ScriptableObject.CreateInstance<ThuongMapDefinition>(); AssetDatabase.CreateAsset(map,path); }
            map.mapId=layout.id; map.displayName=layout.name; map.contentRevision=Mathf.Max(Revision,map.contentRevision);
            map.boundsMin=Min; map.boundsMax=Max; map.released=false;
            map.zones=layout.centers.Select((p,i)=>new ThuongMapZone { zoneId=ZoneIds[i],displayName=layout.names[i],area=new Rect(p-new Vector2(4.5f,3.5f),new Vector2(9,7)),reservedTaskPoint=p+new Vector2(2,-1.6f) }).ToArray();
            if(layout.theme==Theme.Swamp) map.zones[5].reservedTaskPoint=layout.centers[5]+new Vector2(0,2.5f);
            map.connections=MainEdges.Select(e=>Connection(e.Item1,e.Item2,false)).Concat(layout.shortcuts.Select(e=>Connection(e.Item1,e.Item2,true))).ToArray();
            map.plaza=layout.centers[4]; map.gate=layout.centers[7]+Vector2.down*2.8f; map.stage=new Vector2(.5f,-15.2f);
            map.spawns=Enumerable.Range(0,25).Select(i=>map.plaza+new Vector2((i%5-2)*.85f,(i/5-2)*.85f)).ToArray();
            map.lanterns=map.zones.Select(z=>z.area.center+new Vector2(-2.7f,2.2f)).ToArray();
            map.stationPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/Thuong/Prototype/Integration/Prefabs/TaskStation.prefab");
            map.stations=minigames.entries.Select((e,slot)=>
            {
                int task=Array.FindIndex(TaskIds,id=>e.taskId=="thuong.task."+id);
                if(task<0) throw new InvalidOperationException("Unknown minigame "+e.taskId);
                return new ThuongMapStation { stationId=e.taskId,maskSlot=slot,task=e.task,position=layout.centers[layout.taskZones[task]]+new Vector2(-1.8f,-1.3f),sprite=Sprite(TaskArt[task]) };
            }).ToArray();
            var scene=EditorSceneManager.NewPreviewScene(); var world=new GameObject("World "+layout.id); root=world.transform;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(world,scene);
            try
            {
                Terrain(); Landmarks(); map.obstacles=obstacles.ToArray();
                map.worldPrefab=PrefabUtility.SaveAsPrefabAsset(world,Root+"/Prefabs/"+layout.id+".prefab");
                map.thumbnail=ThuongExpandedVillageInstaller.Render(world,map,Root+"/Art/Thumbnail_"+layout.id+".png",640,544);
                EditorUtility.SetDirty(map); AssetDatabase.SaveAssets();
                Debug.Log("Expanded layout authored: "+layout.id+"; "+placedProps+" landmarks/props; "+obstacles.Count+" obstacle footprints.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return map;
        }

        private ThuongMapConnection Connection(int a,int b,bool shortcut)
        {
            Vector2 from=layout.centers[a],to=layout.centers[b];
            var points=new List<Vector2>{from};
            if(layout.theme==Theme.Manor&&shortcut)
            {
                float middle=(from.x+to.x)/2;
                points.Add(new Vector2(middle,from.y)); points.Add(new Vector2(middle,to.y));
            }
            else if(layout.theme==Theme.Swamp&&!shortcut)
            {
                Vector2 middle=(from+to)/2;
                if(Mathf.Abs(from.x-to.x)<3) middle.x+=from.x<0?-2:from.x>6?2:1.8f;
                else if(Mathf.Abs(from.y-to.y)<3) middle.y+=from.y>5?1.2f:from.y< -5?-1.2f:1.5f;
                else middle+=new Vector2(1,-1);
                points.Add(middle);
            }
            else if(layout.theme==Theme.Camp&&!shortcut)
            {
                Vector2 middle=(from+to)/2;
                middle+=Mathf.Abs(from.x-to.x)>Mathf.Abs(from.y-to.y)?new Vector2(0,a%2==0?1:-1):new Vector2(a%2==0?1:-1,0);
                points.Add(middle);
            }
            points.Add(to);
            return new ThuongMapConnection { connectionId=ZoneIds[a]+"."+ZoneIds[b],fromZone=ZoneIds[a],toZone=ZoneIds[b],width=shortcut?2.4f:3,shortcut=shortcut,points=points.ToArray() };
        }

        private static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
            => Vector2.Distance(p,a+(b-a)*Mathf.Clamp01(Vector2.Dot(p-a,b-a)/(b-a).sqrMagnitude));
        private bool OnRoute(Vector2 p,ThuongMapConnection c)
            => Enumerable.Range(1,c.points.Length-1).Any(i=>SegmentDistance(p,c.points[i-1],c.points[i])<=c.width/2);

        private bool InRoom(Vector2 point,ThuongMapZone zone)
        {
            if(!zone.area.Contains(point)) return false;
            if(layout.theme!=Theme.Swamp&&layout.theme!=Theme.Camp) return true;
            Vector2 delta=point-zone.area.center;
            return Mathf.Abs(delta.x)<=3.5f||Mathf.Abs(delta.y)<=2.5f;
        }

        private void Terrain()
        {
            var grid=new GameObject("Terrain",typeof(Grid)); grid.transform.SetParent(root,false);
            bool swamp=layout.theme==Theme.Swamp, manor=layout.theme==Theme.Manor;
            var ground=Tiles(grid.transform,"Ground",Sprite(swamp?"SwampGround":"Grass"),-200,manor?new Color(.23f,.23f,.28f):Color.white);
            var closed=Tiles(grid.transform,"Closed plots",Sprite("Grass"),-195,
                layout.theme==Theme.Cemetery?new Color(.65f,.68f,.72f):new Color(.55f,.72f,.56f));
            string road=swamp||manor?"FloorPlank":layout.theme==Theme.Camp?"RuralPath":"Cobblestone";
            var roads=Tiles(grid.transform,"Roads",Sprite(road),-190,Color.white);
            var alleys=Tiles(grid.transform,"Side passages",Sprite(swamp||manor?"FloorPlank":"RuralPath"),-189,manor?new Color(.85f,.83f,.8f):Color.white);
            Tilemap water=null;
            if(swamp) water=Layer(grid.transform,"Animated water",-194);
            for(int y=0;y<34;y++) for(int x=0;x<40;x++)
            {
                Vector2 p=Min+new Vector2(x+.5f,y+.5f); var cell=new Vector3Int(x-20,y-17,0);
                ground.map.SetTile(cell,ground.tile);
                bool room=map.zones.Any(z=>InRoom(p,z));
                bool main=map.connections.Any(c=>!c.shortcut&&OnRoute(p,c));
                bool side=map.connections.Any(c=>c.shortcut&&OnRoute(p,c));
                free[x,y]=room||main||side;
                if(main||(room&&(manor||map.zones.Any(z=>Vector2.Distance(p,z.area.center)<2)))) roads.map.SetTile(cell,roads.tile);
                else if(side) alleys.map.SetTile(cell,alleys.tile);
                if(!free[x,y]&&!swamp&&!manor) closed.map.SetTile(cell,closed.tile);
            }
            if(swamp)
            {
                water.animationFrameRate=4;
                for(int y=0;y<34;y++) for(int x=0;x<40;x++) if(!free[x,y])
                {
                    int row=Free(x,y+1)?0:Free(x,y-1)?2:1;
                    int col=Free(x-1,y)?0:Free(x+1,y)?2:1;
                    var tile=AssetDatabase.LoadAssetAtPath<AnimatedTile>(Root+"/Definitions/SwampWater"+row+col+".asset");
                    if(tile==null) throw new InvalidOperationException("Missing animated water tile.");
                    water.SetTile(new Vector3Int(x-20,y-17,0),tile);
                }
            }
            MergeClosedCells();
            Boundaries();
        }

        private bool Free(int x,int y)=>x>=0&&y>=0&&x<40&&y<34&&free[x,y];

        private void MergeClosedCells()
        {
            var previous=new Dictionary<(int,int),int>();
            for(int y=0;y<34;y++)
            {
                var current=new Dictionary<(int,int),int>();
                for(int x=0;x<40;)
                {
                    if(free[x,y]) { x++; continue; }
                    int start=x; while(x<40&&!free[x,y]) x++;
                    var key=(start,x-start);
                    if(previous.TryGetValue(key,out int index)) { Rect rect=obstacles[index]; rect.height++; obstacles[index]=rect; current[key]=index; }
                    else { current[key]=obstacles.Count; obstacles.Add(new Rect(Min.x+start,Min.y+y,x-start,1)); }
                }
                previous=current;
            }
            foreach(var obstacle in obstacles) Box(obstacle,"Terrain footprint");
        }

        private void Boundaries()
        {
            for(int y=0;y<34;y++) for(int x=0;x<40;x++)
            {
                if(free[x,y]) continue;
                Vector2 p=Min+new Vector2(x+.5f,y+.5f);
                if(layout.theme!=Theme.Swamp)
                {
                    if(Free(x,y-1)) Rail(p+Vector2.down*.5f,false);
                    if(Free(x,y+1)) Rail(p+Vector2.up*.5f,false);
                    if(Free(x-1,y)) Rail(p+Vector2.left*.5f,true);
                    if(Free(x+1,y)) Rail(p+Vector2.right*.5f,true);
                }
                if(layout.theme==Theme.Cemetery&&x%3==0&&y%3==0) Prop((x+y)%2==0?"Grave":"GraveRound",p,.9f);
                else if(layout.theme==Theme.Swamp&&x%3==0&&y%3==0&&(Free(x+1,y)||Free(x,y+1))) Prop("SwampReed",p,.85f);
                else if(layout.theme!=Theme.Manor&&layout.theme!=Theme.Swamp&&x%2==0&&y%2==0) Prop("Tree",p,layout.theme==Theme.Camp?1:.8f);
            }
            if(layout.theme!=Theme.Manor&&layout.theme!=Theme.Swamp)
                for(int x=-19;x<20;x+=2) { Prop("Tree",new(x,16),.8f); Prop("Tree",new(x,-16.7f),.8f); }
        }

        private void Rail(Vector2 p,bool vertical)
        {
            bool wall=layout.theme==Theme.Manor;
            var rail=Prop(wall?"ManorWall":"Fence",p,wall?1:.7f);
            if(wall) { rail.transform.localScale=new Vector3(1,.65f,1); rail.AddComponent<ThuongMapOccluder>(); }
            if(vertical) rail.transform.rotation=Quaternion.Euler(0,0,90);
        }

        private void Landmarks()
        {
            for(int zone=0;zone<9;zone++)
            {
                string main=layout.landmarks[zone];
                float scale=main.StartsWith("House")||main=="RuralCabin" ? .75f : main=="Tent" ? .85f : 1;
                if(!TryPlace(zone,main,Footprint(main)*scale,scale))
                    throw new InvalidOperationException("No clear landmark pocket in "+layout.id+" / "+ZoneIds[zone]+" for "+main);
                string[] extras=layout.theme switch
                {
                    Theme.Market=>new[]{"MarketAwningGreen","Sacks","Cart"},
                    Theme.Cemetery=>new[]{"Grave","GraveRound","Lantern"},
                    Theme.Swamp=>new[]{"SwampReed","Crate","Lantern"},
                    Theme.Camp=>new[]{"Woodpile","Barrel","Campfire"},
                    _=>new[]{"Crate","Barrel","Curtain"}
                };
                foreach(string extra in extras) TryPlace(zone,extra,Footprint(extra),1);
                Label(map.zones[zone]);
            }
            int harvestZone=layout.taskZones[1];
            for(int x=-3;x<=-1;x++) for(int y=1;y<=2;y++)
            {
                Vector2 p=layout.centers[harvestZone]+new Vector2(x,y);
                if(!obstacles.Any(r=>r.Contains(p))&&map.stations.All(s=>Vector2.Distance(s.position,p)>1.4f)) Prop("Wheat",p);
            }
            if(layout.theme==Theme.Swamp)
                foreach(var connection in map.connections.Where(c=>!c.shortcut))
                {
                    Vector2 mid=connection.points[connection.points.Length/2];
                    if(map.zones.Any(z=>z.area.Contains(mid))) continue;
                    var bridge=Prop(Mathf.Abs(connection.points[0].x-connection.points[connection.points.Length-1].x)>5?"SwampBridgeHorizontal":"SwampBridge",mid);
                    bridge.GetComponent<ThuongDepthSort>().enabled=false; bridge.GetComponent<SpriteRenderer>().sortingOrder=-180;
                }
        }

        private static Vector2 Footprint(string sprite)
        {
            if(sprite.StartsWith("House")||sprite=="RuralCabin") return new(3.2f,1);
            return sprite switch
            {
                "Tent"=>new(2,.7f), "MarketStall" or "MarketAwning" or "MarketAwningGreen"=>new(1.6f,.6f),
                "Planter"=>new(1.4f,.35f), "Cart"=>new(1,.5f), "Memorial"=>new(1.2f,.6f),
                "Woodpile"=>new(1,.45f), "Campfire"=>new(.75f,.4f), "SwampBridgeHorizontal"=>new(1.2f,.45f),
                "Curtain" or "Banner"=>new(.7f,.25f), "Grave" or "GraveRound"=>new(.45f,.3f),
                _=>new(.6f,.4f)
            };
        }

        private bool TryPlace(int index,string sprite,Vector2 size,float scale)
        {
            var offsets=new[]{new Vector2(2.9f,1.2f),new Vector2(-2.9f,1.2f),new Vector2(2.9f,1.7f),new Vector2(-2.9f,1.7f),
                new Vector2(3.2f,2.1f),new Vector2(-3.2f,2.1f),new Vector2(3.2f,-2.4f),new Vector2(-3.2f,-2.4f),new Vector2(2.9f,-2.4f),new Vector2(-2.9f,-2.4f),
                new Vector2(0,2.5f),new Vector2(3.5f,.3f),new Vector2(-3.5f,.3f),new Vector2(2.5f,2.2f),new Vector2(-2.5f,2.2f)};
            foreach(var offset in offsets)
            {
                Vector2 p=layout.centers[index]+offset; var rect=new Rect(p-new Vector2(size.x/2,0),size);
                if(!CanPlace(rect,map.zones[index])) continue;
                var prop=Prop(sprite,p,scale); obstacles.Add(rect); Box(rect,sprite+" footprint"); placedProps++;
                if(sprite.StartsWith("House")||sprite=="RuralCabin") prop.AddComponent<ThuongMapOccluder>();
                if(sprite=="SwampAltar0")
                { var loop=prop.AddComponent<ThuongSpriteLoop>(); loop.frames=Enumerable.Range(0,4).Select(i=>Sprite("SwampAltar"+i)).ToArray(); loop.framesPerSecond=6; loop.staysBrightAtNight=true; }
                return true;
            }
            return false;
        }

        private bool CanPlace(Rect rect,ThuongMapZone zone)
        {
            if(rect.xMin<zone.area.xMin+.3f||rect.xMax>zone.area.xMax-.3f||rect.yMin<zone.area.yMin+.3f||rect.yMax>zone.area.yMax-.3f) return false;
            if(obstacles.Any(r=>Grow(r,.18f).Overlaps(rect))) return false;
            foreach(var connection in map.connections)
            {
                var guard=Grow(rect,connection.shortcut ? .7f : 1);
                for(int i=1;i<connection.points.Length;i++)
                {
                    Vector2 a=connection.points[i-1],b=connection.points[i]; int steps=Mathf.CeilToInt(Vector2.Distance(a,b)/.35f);
                    for(int s=0;s<=steps;s++) if(guard.Contains(Vector2.Lerp(a,b,(float)s/steps))) return false;
                }
            }
            if(map.spawns.Any(p=>Grow(rect,.5f).Contains(p))||map.stations.Any(s=>Grow(rect,1.5f).Contains(s.position))||map.zones.Any(z=>Grow(rect,.7f).Contains(z.reservedTaskPoint))) return false;
            return true;
        }

        private static Rect Grow(Rect rect,float amount)=>Rect.MinMaxRect(rect.xMin-amount,rect.yMin-amount,rect.xMax+amount,rect.yMax+amount);

        private GameObject Prop(string name,Vector2 position,float scale=1)
        {
            var obj=new GameObject(name,typeof(SpriteRenderer),typeof(ThuongDepthSort)); obj.transform.SetParent(root,false); obj.transform.position=position; obj.transform.localScale=Vector3.one*scale;
            var renderer=obj.GetComponent<SpriteRenderer>(); renderer.sprite=Sprite(name); renderer.sharedMaterial=material;
            if(renderer.sprite==null) throw new InvalidOperationException("Missing map sprite: "+name);
            return obj;
        }

        private void Label(ThuongMapZone zone)
        {
            var obj=new GameObject("Zone name - "+zone.zoneId,typeof(TextMeshPro)); obj.transform.SetParent(root,false); obj.transform.position=new Vector3(zone.area.center.x,zone.area.yMin+.5f,-.1f);
            var text=obj.GetComponent<TextMeshPro>(); text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Scenes/Thuong/Prototype/Art/PrototypeFont.asset");
            text.text=zone.displayName; text.fontSize=3; text.alignment=TextAlignmentOptions.Center; text.color=new Color(1,.93f,.68f); text.rectTransform.sizeDelta=new Vector2(8,.7f); text.GetComponent<MeshRenderer>().sortingOrder=4000;
        }

        private void Box(Rect rect,string name)
        {
            var obj=new GameObject(name,typeof(BoxCollider2D)); obj.transform.SetParent(root,false); obj.transform.position=rect.center; obj.GetComponent<BoxCollider2D>().size=rect.size;
        }

        private Tilemap Layer(Transform parent,string name,int order)
        {
            var obj=new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer)); obj.transform.SetParent(parent,false);
            var renderer=obj.GetComponent<TilemapRenderer>(); renderer.sortingOrder=order; renderer.sharedMaterial=material; return obj.GetComponent<Tilemap>();
        }

        private (Tilemap map,Tile tile) Tiles(Transform parent,string name,Sprite sprite,int order,Color color)
        {
            string path=Root+"/Definitions/"+layout.id+"_"+name.Replace(" ","")+".asset";
            var tile=AssetDatabase.LoadAssetAtPath<Tile>(path);
            if(tile==null) { tile=ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile,path); }
            tile.sprite=sprite; tile.color=color; tile.colliderType=Tile.ColliderType.None; EditorUtility.SetDirty(tile);
            return(Layer(parent,name,order),tile);
        }

        private static Sprite Sprite(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/"+name+".png")??AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/Thuong/Prototype/Art/Medieval/Sprites/World/"+name+".png");
    }
}
