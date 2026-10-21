using System;
using System.IO;
using System.Linq;
using System.Text;
using Thuong.Maps;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ThuongMapChecks
{
    private static int checks;
    [MenuItem("Tools/Thuong/Maps/Check Content Collision And Events")]
    public static void Run()
    {
        checks = 0; Directory.CreateDirectory("Logs/Maps");
        var report = new StringBuilder();
        var catalog = Resources.Load<ThuongMapCatalog>(ThuongMapCatalog.ResourcePath);
        Require(catalog != null && catalog.Released.Length == 6, "Six maps included in runtime catalog");
        foreach(string name in new[]{"Water","FloorPlank"})
        {
            var tileSprite=AssetDatabase.LoadAssetAtPath<Sprite>(ThuongMapInstaller.Root+"/Art/"+name+".png");
            Require(tileSprite!=null && Vector2.Distance(tileSprite.pivot,tileSprite.rect.size*.5f)<.01f,"Terrain sprite aligns with the cell and collision: "+name);
        }
        for(int row=0;row<3;row++) for(int column=0;column<3;column++)
        {
            var water=AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.AnimatedTile>(ThuongMapInstaller.Root+"/Definitions/SwampWater"+row+column+".asset");
            Require(water!=null && water.m_AnimatedSprites.Length==4 && water.m_TileColliderType==UnityEngine.Tilemaps.Tile.ColliderType.None &&
                water.m_AnimatedSprites.All(s=>s!=null && s.rect.size==new Vector2(48,48) && Vector2.Distance(s.pivot,new Vector2(24,24))<.01f),"Animated water variants preserve 48px centered cells; collision stays server-owned");
        }
        var ids = catalog.maps.Select(m => m.mapId).ToArray(); Require(ids.Distinct().Count() == ids.Length, "Unique map IDs");
        var reference = catalog.maps[0].stations.OrderBy(s => s.maskSlot).Select(s => (s.stationId, s.task)).ToArray();
        foreach (var map in catalog.Released)
        {
            Require(ThuongMapValidator.Validate(map, out string error), map.mapId + ": " + error);
            Require(map.stations.OrderBy(s => s.maskSlot).Select(s => (s.stationId, s.task)).SequenceEqual(reference), "Same explicit task slots on " + map.mapId);
            foreach (var spawn in map.spawns) Require(ThuongMapCollision.IsFree(map, spawn), "Spawn footprint free");
            foreach (var spawn in map.spawns)
                Require(ThuongMapValidator.WalkingDistances(map,spawn,1).All(float.IsFinite),"Every spawn reaches every station");
            foreach (var station in map.stations)
            {
                int approaches=new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right}.Count(direction =>
                    ThuongMapCollision.IsFree(map,station.position+direction*1.1f) &&
                    ThuongMapCollision.HasLine(map,station.position+direction*1.1f,station.position));
                Require(approaches>=2,"At least two approaches to "+map.mapId+" / "+station.stationId);
                Require(Vector2.Distance(station.position,map.stage)>2,"Event stage stays clear of task anchors");
            }
            var distances = ThuongMapValidator.WalkingDistances(map, map.spawns[12]);
            Require(distances.All(float.IsFinite), "All ten stations reachable on " + map.mapId);
            report.AppendLine(map.mapId + " seconds at 3 units/s: " + string.Join(", ", distances.Select(d => (d / 3).ToString("F2"))));
            foreach (var obstacle in map.obstacles.Where(r => r.width > .1f && r.height > .1f))
            {
                Vector2 start = new(obstacle.xMin - .5f, obstacle.center.y - ThuongMapCollision.Offset.y);
                if (!ThuongMapCollision.IsFree(map, start)) continue;
                Vector2 stopped = ThuongMapCollision.Move(map, start, new Vector2(obstacle.width + 2, 0));
                Require(stopped.x < obstacle.xMin && ThuongMapCollision.IsFree(map, stopped), "Swept footprint prevents tunnelling");
                Vector2 diagonal = ThuongMapCollision.Move(map, start, new Vector2(3, .7f));
                Require(ThuongMapCollision.IsFree(map, diagonal), "Diagonal slide remains outside obstacle");
                for (int i = 0; i < 80; i++) diagonal = ThuongMapCollision.Move(map, diagonal, new Vector2(.12f, .08f));
                Require(ThuongMapCollision.IsFree(map, diagonal), "Repeated corner pushing remains valid");
            }
            Require(ThuongMapCollision.Move(map, map.spawns[0], new Vector2(float.NaN,0)) == map.spawns[0], "Reject nonfinite movement");
            var root = new GameObject("Map lifecycle check");
            var runtime = root.AddComponent<ThuongMapRuntime>();
            Require(runtime.Bind(map,out error) && runtime.Stations.Length == 10, "Runtime binds all stations");
            var world = runtime.World; Require(runtime.Bind(map,out error) && runtime.World == world, "Repeated bind reuses world");
            var events = root.AddComponent<ThuongEventPresentation>();
            var visuals = Resources.Load<ThuongEventPresentationCatalog>(ThuongEventPresentationCatalog.ResourcePath);
            events.Bind(runtime,visuals);
            foreach (GameEventType type in Enum.GetValues(typeof(GameEventType)))
            {
                if (type == GameEventType.None) continue;
                Require(visuals.Find(type) != null, "Every map supports all six event variants");
                foreach (var phase in new[] { GamePhase.Task,GamePhase.Night,GamePhase.ResolveNight,GamePhase.Discussion,GamePhase.Voting })
                {
                    events.Apply(type,2,phase,true); events.Apply(type,2,phase,true);
                    Require(events.OverlayCount <= 1,"Repeated snapshot does not duplicate overlay");
                    if (!ThuongEventPresentationCatalog.IsVisible(type,phase))
                        Require(!runtime.World.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith("Event overlay")),"Event hidden outside configured phase window");
                }
                events.Apply(GameEventType.None,3,GamePhase.Task,true); Require(events.OverlayCount == 0,"Next day clears event");
                events.Apply(type,2,GamePhase.Night,true); events.Apply(type,2,GamePhase.GameOver,true);
                Require(events.OverlayCount == 0,"GameOver clears event");
                events.Apply(type,2,GamePhase.Night,true); events.Apply(type,2,GamePhase.Night,false);
                Require(events.OverlayCount == 0,"Leave clears event");
            }
            Object.DestroyImmediate(root);
        }
        Require(!ThuongEventPresentationCatalog.IsVisible(GameEventType.Fog,GamePhase.Discussion),"Fog reduced during dialogue");
        Require(!ThuongEventPresentationCatalog.IsVisible(GameEventType.BloodMoon,GamePhase.Task),"BloodMoon starts at night");
        Require(!ThuongEventPresentationCatalog.IsVisible(GameEventType.ClearSky,GamePhase.Night),"ClearSky respects normal night");
        report.AppendLine("THUONG_MAP_CHECKS_PASSED: " + checks + " assertions");
        File.WriteAllText("Logs/Maps/content-collision-events.txt",report.ToString()); Debug.Log(report.ToString());
    }
    private static void Require(bool condition,string message) { checks++; if (!condition) throw new Exception("Map check failed: " + message); }
}
