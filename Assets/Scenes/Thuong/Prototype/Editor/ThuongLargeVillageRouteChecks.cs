using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Thuong.Maps;
using UnityEngine;
using UnityEngine.Tilemaps;

// Check the painted roads and the actual footprint collision together.
// Local access from two directions alone does not prove an alternate cross-town route exists.
public static class ThuongLargeVillageRouteChecks
{
    public static int Run(ThuongMapDefinition map)
    {
        int checks=0;
        void Require(bool condition,string message)
        { checks++; if (!condition) throw new InvalidOperationException("Village route check failed: "+message); }
        var surfaces=map.worldPrefab.GetComponentsInChildren<Tilemap>(true)
            .Where(surface=>surface.name=="Lanes" || surface.name=="Footpaths").ToArray();
        Require(surfaces.Length==2,"Paved roads and dirt shortcuts both exist");
        int width=Mathf.RoundToInt(map.boundsMax.x-map.boundsMin.x);
        int height=Mathf.RoundToInt(map.boundsMax.y-map.boundsMin.y);
        var free=new bool[width*height];
        Vector2 Point(int index)=>map.boundsMin+new Vector2(index%width+.5f,index/width+.5f);
        for (int i=0;i<free.Length;i++)
            free[i]=ThuongMapCollision.IsFree(map,Point(i)) && surfaces.Any(surface=>surface.HasTile(surface.WorldToCell(Point(i))));

        List<Vector2> FindRoute(Vector2 from,Vector2 to,Rect? closed=null)
        {
            bool Allowed(int i)=>free[i] && (!closed.HasValue || !closed.Value.Contains(Point(i)));
            var parents=Enumerable.Repeat(-2,free.Length).ToArray();
            var pending=new Queue<int>();
            for (int i=0;i<free.Length;i++)
                if (Allowed(i) && Vector2.Distance(Point(i),from)<=1.3f && ThuongMapCollision.HasLine(map,from,Point(i)))
                { parents[i]=-1; pending.Enqueue(i); }
            while (pending.Count>0)
            {
                int current=pending.Dequeue(); Vector2 point=Point(current);
                if (Vector2.Distance(point,to)<=1.3f && ThuongMapCollision.HasLine(map,point,to))
                {
                    var path=new List<Vector2>();
                    for (int cursor=current;cursor>=0;cursor=parents[cursor]) path.Add(Point(cursor));
                    path.Reverse(); return path;
                }
                for (int dy=-1;dy<=1;dy++) for (int dx=-1;dx<=1;dx++)
                {
                    int x=current%width+dx,y=current/width+dy;
                    if (dx==0 && dy==0 || x<0 || y<0 || x>=width || y>=height) continue;
                    int next=y*width+x;
                    if (parents[next]!=-2 || !Allowed(next) ||
                        closed.HasValue && closed.Value.Contains((point+Point(next))/2) ||
                        !ThuongMapCollision.HasLine(map,point,Point(next))) continue;
                    parents[next]=current; pending.Enqueue(next);
                }
            }
            return null;
        }

        var report=new StringBuilder();
        Rect plazaClosed=new(map.plaza-Vector2.one*3.5f,Vector2.one*7);
        foreach (var station in map.stations.OrderBy(s=>s.maskSlot))
        {
            int approaches=new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right}.Count(direction=>
                ThuongMapCollision.IsFree(map,station.position+direction*1.5f) &&
                ThuongMapCollision.HasLine(map,station.position+direction*1.5f,station.position));
            Require(approaches>=3,"Three clear local approaches: "+station.stationId);
            var primary=FindRoute(map.plaza,station.position);
            Require(primary!=null && primary.Count>=4,"Task is served by the painted road network: "+station.stationId);
            Vector2 midpoint=primary[primary.Count/2];
            // Close the full corridor, including its diagonal width, so a parallel
            // pixel row on the same road cannot masquerade as a different route.
            Rect closed=new(midpoint-Vector2.one*2.7f,Vector2.one*5.4f);
            var alternate=FindRoute(map.plaza,station.position,closed);
            Require(alternate!=null,"Task still has a road route with its primary corridor closed: "+station.stationId);
            // All task districts can also connect without the central plaza.
            var bypass=FindRoute(map.stations[0].position,station.position,plazaClosed);
            Require(bypass!=null,"District remains connected with the central plaza closed: "+station.stationId);
            report.AppendLine(station.stationId+": approaches="+approaches+", primary_cells="+primary.Count+
                ", alternate_cells="+alternate.Count+", plaza_bypass_cells="+bypass.Count);
        }
        Require(!map.obstacles.Any(rect=>rect.width<1 && rect.height>8),"No long dividing wall across a district");
        report.AppendLine("THUONG_LARGE_VILLAGE_ROUTES_PASSED: "+checks+" assertions");
        Directory.CreateDirectory("Logs/Maps");
        File.WriteAllText("Logs/Maps/village-large-routes.txt",report.ToString());
        Debug.Log(report.ToString()); return checks;
    }
}
