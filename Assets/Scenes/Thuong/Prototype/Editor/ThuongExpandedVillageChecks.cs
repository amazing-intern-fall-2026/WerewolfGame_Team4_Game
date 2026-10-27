using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Thuong.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ThuongExpandedVillageChecks
{
    private static int assertions;
    [MenuItem("Tools/Thuong/Maps/Check Expanded Village")]
    public static void Run()
    {
        CheckMap(AssetDatabase.LoadAssetAtPath<ThuongMapDefinition>(ThuongExpandedVillageInstaller.DefinitionPath),
            "Logs/ExpandedVillage", "THUONG_EXPANDED_VILLAGE_PASSED");
    }

    public static void CheckMap(ThuongMapDefinition map, string outputDirectory, string resultLabel = "THUONG_EXPANDED_MAP_PASSED")
    {
        assertions=0;
        Require(ThuongMapValidator.Validate(map,out string error),error ?? "Valid map definition");
        Require(map.zones.Length==9 && map.connections.Length==14,"Nine zones and fourteen routes");
        Require(map.zones.Select(z=>z.zoneId).Distinct().Count()==9,"Unique zone IDs");
        Require(map.connections.Select(c=>c.connectionId).Distinct().Count()==14,"Unique connection IDs");
        Require(map.connections.Count(c=>c.shortcut)==2,"Two public shortcuts");
        Require(Connected(map,null,null),"All zones connected");
        Require(Connected(map,"plaza",null),"Outer ring stays connected without central plaza");
        foreach(var connection in map.connections)
        {
            Require(Connected(map,null,connection.connectionId),"Alternative route after removing "+connection.connectionId);
            Require(connection.points.Length>=2 && connection.width>=2,"Usable connection width");
            Require(map.zones.Single(z=>z.zoneId==connection.fromZone).area.Contains(connection.points[0]) &&
                map.zones.Single(z=>z.zoneId==connection.toZone).area.Contains(connection.points[connection.points.Length-1]),"Route ends inside declared zones");
            for(int p=1;p<connection.points.Length;p++)
            {
                Vector2 a=connection.points[p-1],b=connection.points[p];
                int steps=Mathf.CeilToInt(Vector2.Distance(a,b)/.2f);
                for(int i=0;i<=steps;i++) Require(ThuongMapCollision.IsFree(map,Vector2.Lerp(a,b,(float)i/steps)),"Physical route clear: "+connection.connectionId+" at "+Vector2.Lerp(a,b,(float)i/steps));
                Require(ThuongMapCollision.HasLine(map,a,b),"Swept movement follows "+connection.connectionId);
            }
        }
        foreach(var zone in map.zones)
        {
            Require(map.connections.Count(c=>c.fromZone==zone.zoneId||c.toZone==zone.zoneId)>=2,"Two distinct connections for "+zone.zoneId);
            Require(ThuongMapCollision.IsFree(map,zone.reservedTaskPoint),"Future task anchor free: "+zone.zoneId);
        }
        var navigation=new ThuongMapNavigation(map);
        foreach(var spawn in map.spawns)
            Require(navigation.WalkingDistances(spawn).All(float.IsFinite),"Every spawn reaches every task");
        foreach(var station in map.stations)
        {
            Require(map.zones.Count(z=>z.area.Contains(station.position))==1,"Task belongs to one zone");
            Require(new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right}.Count(d=>
                ThuongMapCollision.IsFree(map,station.position+d*1.1f)&&ThuongMapCollision.HasLine(map,station.position+d*1.1f,station.position))>=2,"Task has two local approaches");
        }
        var report=new StringBuilder();
        float[] distances=navigation.WalkingDistances(map.spawns[12]);
        for(int i=0;i<distances.Length;i++) report.AppendLine(map.stations.Single(s=>s.maskSlot==i).stationId+": "+(distances[i]/3).ToString("F2")+" seconds to interaction area");
        var scene=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("Expanded village lifecycle"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        try
        {
            var runtime=root.AddComponent<ThuongMapRuntime>();
            Require(runtime.Bind(map,out error),error ?? "Runtime binds");
            var world=runtime.World;
            Require(runtime.Bind(map,out error)&&runtime.World==world,"Repeated binding reuses world");
            var colliders=world.GetComponentsInChildren<BoxCollider2D>(true).Where(c=>c.GetComponentInParent<TaskStation>()==null).ToArray();
            Require(colliders.Length==map.obstacles.Length,"Visual world has every server collider");
            foreach(var box in colliders)
            {
                Vector2 center=box.transform.TransformPoint(box.offset);
                Vector2 size=Vector2.Scale(box.size,box.transform.lossyScale);
                Require(map.obstacles.Any(r=>Vector2.Distance(r.center,center)<.001f&&Vector2.Distance(r.size,size)<.001f),"Client/server obstacle footprints agree");
            }
            var events=root.AddComponent<ThuongEventPresentation>();
            events.Bind(runtime,Resources.Load<ThuongEventPresentationCatalog>(ThuongEventPresentationCatalog.ResourcePath));
            foreach(GameEventType type in Enum.GetValues(typeof(GameEventType)))
                foreach(var phase in new[]{GamePhase.Task,GamePhase.Night,GamePhase.Discussion,GamePhase.Voting,GamePhase.GameOver})
                {
                    events.Apply(type,2,phase,true); events.Apply(type,2,phase,true);
                    Require(events.OverlayCount<=1,"Event snapshot idempotent");
                    if(type==GameEventType.Fog&&phase==GamePhase.Task)
                    {
                        var bands=world.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.name=="Fog band").ToArray();
                        Require(bands.Length>=11 && bands.All(s=>s.transform.localScale.x>=40) &&
                            bands.Max(s=>s.transform.localPosition.y)-bands.Min(s=>s.transform.localPosition.y)>=30,"Fog covers expanded map bounds");
                    }
                    if(phase==GamePhase.GameOver) Require(events.OverlayCount==0,"Game over clears overlay");
                }
            events.Clear(); runtime.Clear(); Require(runtime.World==null&&runtime.Stations.Length==0,"Leave clears runtime");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Directory.CreateDirectory(outputDirectory);
        report.Insert(0,resultLabel+": "+assertions+" assertions; "+map.mapId+"\n9 zones; 14 routes; 2 shortcuts; 25 spawns; 6 tasks\n");
        File.WriteAllText(Path.Combine(outputDirectory,"checks.txt"),report.ToString());
        Debug.Log(report.ToString());
    }

    private static bool Connected(ThuongMapDefinition map,string omittedZone,string omittedConnection)
    {
        string[] ids=map.zones.Where(z=>z.zoneId!=omittedZone).Select(z=>z.zoneId).ToArray();
        var seen=new HashSet<string>{ids[0]};
        for(int i=0;i<ids.Length;i++) foreach(var edge in map.connections)
        {
            if(edge.connectionId==omittedConnection||edge.fromZone==omittedZone||edge.toZone==omittedZone) continue;
            if(seen.Contains(edge.fromZone)) seen.Add(edge.toZone);
            if(seen.Contains(edge.toZone)) seen.Add(edge.fromZone);
        }
        return ids.All(seen.Contains);
    }

    [MenuItem("Tools/Thuong/Maps/Render Expanded Village")]
    public static void RenderViews()
    {
        RenderViews(AssetDatabase.LoadAssetAtPath<ThuongMapDefinition>(ThuongExpandedVillageInstaller.DefinitionPath),"Logs/ExpandedVillage");
    }

    public static void RenderViews(ThuongMapDefinition map, string outputDirectory)
    {
        var scene=EditorSceneManager.NewPreviewScene(); var root=new GameObject("Expanded village visual check");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        try
        {
            var runtime=root.AddComponent<ThuongMapRuntime>();
            if(!runtime.Bind(map,out string error)) throw new Exception(error);
            var cast=AssetDatabase.LoadAssetAtPath<ThuongMedievalCast>("Assets/Scenes/Thuong/Prototype/Art/Medieval/Unity/MedievalCast.asset");
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Thuong/Prototype/Art/World.mat");
            for(int i=0;i<25;i++)
            {
                var avatar=new GameObject("Preview player "+i,typeof(SpriteRenderer)); avatar.transform.SetParent(runtime.World.transform,false);
                avatar.transform.position=map.spawns[i];
                var sprite=avatar.GetComponent<SpriteRenderer>(); sprite.sprite=cast.idleSprites[i%cast.Count]; sprite.sharedMaterial=material;
                sprite.sortingOrder=ThuongDepthSort.Order(avatar.transform.position.y);
            }
            foreach(var station in runtime.Stations) if(station.marker!=null) station.marker.SetActive(true);
            var events=root.AddComponent<ThuongEventPresentation>(); events.Bind(runtime,Resources.Load<ThuongEventPresentationCatalog>(ThuongEventPresentationCatalog.ResourcePath));
            foreach(var phase in new[]{GamePhase.Task,GamePhase.Night})
            {
                events.Apply(GameEventType.None,1,phase,true);
                ThuongExpandedVillageInstaller.Render(runtime.World,map,Path.Combine(outputDirectory,phase+".png"),1280,1088);
            }
            foreach(GameEventType type in Enum.GetValues(typeof(GameEventType)))
            {
                if(type==GameEventType.None) continue;
                events.Apply(type,2,type==GameEventType.ClearSky||type==GameEventType.HarvestFestival?GamePhase.Task:GamePhase.Night,true);
                ThuongExpandedVillageInstaller.Render(runtime.World,map,Path.Combine(outputDirectory,type+".png"),1280,1088);
            }
            events.Clear();
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Debug.Log("THUONG_EXPANDED_MAP_VISUALS_PASSED: "+map.mapId+"; 8 views with 25 avatars.");
    }

    private static void Require(bool condition,string message)
    { assertions++; if(!condition) throw new InvalidOperationException("Expanded map: "+message); }
}
