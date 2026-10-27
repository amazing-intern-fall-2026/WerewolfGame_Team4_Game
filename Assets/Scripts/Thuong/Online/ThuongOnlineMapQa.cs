#if THUONG_MAP_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Thuong.Online
{
    // Included only in the explicit QA build. No extra messages or controls in production builds.
    public sealed partial class ThuongOnlineSession
    {
        private readonly HashSet<string> qaReceipts = new();
        private readonly HashSet<string> qaMaps = new();
        private readonly HashSet<GameEventType> qaEvents = new();
        private readonly HashSet<string> qaSent = new();
        private bool qaDone;
        private bool qaPresentationOverride;
        private bool qaMismatchSeen,qaMismatchSent;
        private string qaFailure;
        private int qaAssertions;
        private float qaStarted;

        internal IEnumerator RunMapQa(bool host,string output)
        {
            qaStarted = Time.realtimeSinceStartup;
            Application.runInBackground = true;
            QaCheck(SelectedMap?.mapId == "village_large" && localMapReady, "Online scene starts with the large village ready");
            var sceneEntry = game.gameObject.scene.GetRootGameObjects().Select(root => root.GetComponent<ThuongOnlineSceneEntry>()).FirstOrDefault(entry => entry != null);
            QaCheck(sceneEntry?.InitialMap == SelectedMap, "Default map comes from the saved scene configuration");
            QaCheck(!game.gameObject.scene.GetRootGameObjects().Any(root => root.name == "Thuong Online Map Preview" && root.activeSelf),
                "Editor preview is disabled before the live online map is created");
            portText="17986"; address="127.0.0.1"; playerName=host?"Map QA Host":"Map QA Client";
            SetLobbyCapacity(2);
            if (!host) mapCatalog.Find("harvest_market").contentRevision++;
            if (host) HostLan(); else JoinLan();
            yield return new WaitForSecondsRealtime(.5f);
            network.CustomMessagingManager.RegisterNamedMessageHandler("thuong.qa.receipt",(sender,reader)=>
            {
                if (!network.IsServer) return;
                reader.ReadValueSafe(out byte type); reader.ReadValueSafe(out byte state);
                qaReceipts.Add(sender+":"+type+":"+state);
            });
            network.CustomMessagingManager.RegisterNamedMessageHandler("thuong.qa.done",(sender,reader)=>
            { if (sender==NetworkManager.ServerClientId) qaDone=true; });
            network.CustomMessagingManager.RegisterNamedMessageHandler("thuong.qa.mismatch",(sender,reader)=>
            { if(network.IsServer) qaMismatchSeen=true; });
            network.CustomMessagingManager.RegisterNamedMessageHandler("thuong.qa.restore",(sender,reader)=>
            { if(sender==NetworkManager.ServerClientId) { mapCatalog.Find("harvest_market").contentRevision--; acknowledgedMapEpoch=0; } });
            while (!connected || host && serverPlayers.Count<2)
            { if (!QaAlive(output)) yield break; yield return null; }
            if (host)
            {
                ApplyMapVariant(new Thuong.Maps.ThuongMapVariant(mapCatalog.Find("village"), GameEventType.None));
                while (!AllMapsReady) { if (!QaAlive(output)) yield break; yield return null; }
                for (int i=0;i<mapCatalog.Released.Length;i++)
                {
                    SelectMap(1);
                    QaCheck(!AllMapsReady,"Map switch requires fresh acknowledgement");
                    AssignLobbyRolesOnServer(); QaCheck(!rolesAssigned,"Role assignment blocked before map-ready");
                    if(i==0)
                    {
                        while(!qaMismatchSeen) { if(!QaAlive(output)) yield break; yield return null; }
                        QaCheck(!AllMapsReady,"Real client with mismatched revision cannot start");
                        using(var restore=new FastBufferWriter(1,Allocator.Temp))
                        { restore.WriteValueSafe(true); network.CustomMessagingManager.SendNamedMessage("thuong.qa.restore",serverPlayers.Keys.First(id=>id!=network.LocalClientId),restore); }
                    }
                    while (!AllMapsReady) { if (!QaAlive(output)) yield break; yield return null; }
                    QaCheck(SelectedMap!=null && stations.Length==6,"Full map asset and minigame stations loaded in player");
                    foreach (var spawn in SelectedMap.spawns) QaCheck(Thuong.Maps.ThuongMapCollision.IsFree(SelectedMap,spawn),"Valid spawn");
                }
                yield return QaBenchmark(output);
                while (SelectedMap.mapId != "village_large")
                {
                    SelectMap(1);
                    while (!AllMapsReady) { if (!QaAlive(output)) yield break; yield return null; }
                }
                AssignLobbyRolesOnServer(); QaCheck(rolesAssigned,"Roles assigned after content acknowledged");
            }
            while (!matchStarted)
            {
                if (!QaAlive(output)) yield break;
                QaObserve();
                if (HasLocalRole && !localRoleConfirmed) { ViewLocalRole(); ReadLocalRole(); ToggleReady(); }
                if (host && rolesAssigned && serverPlayers.Values.All(p=>p.Ready)) StartMatchOnServer();
                yield return null;
            }
            QaCheck(visibleState==GameState.Day,"Real match enters day");
            if (host)
            {
                game.enabled=false; DayTimer.Instance.StopTimer();
                QaCheck(tasks.currentTasks.Count==4,"Four tasks per day retained");
                ulong client=serverPlayers.Keys.First(id=>id!=network.LocalClientId);
                foreach (GameEventType type in Enum.GetValues(typeof(GameEventType)))
                {
                    if (type==GameEventType.None) continue;
                    selectedMapEvent = type;
                    EventManager.Instance.ConfigureMapVariant(type);
                    foreach (GamePhase phase in new[]{GamePhase.Task,GamePhase.Night})
                    {
                        game.SetPhase(phase); BroadcastSnapshot(); BroadcastSnapshot();
                        string receipt=client+":"+(byte)type+":"+(byte)phase;
                        while (!qaReceipts.Contains(receipt)) { if (!QaAlive(output)) yield break; yield return null; }
                        QaCheck(eventPresentation.OverlayCount<=1,"Duplicate snapshots keep one overlay");
                    }
                }
                game.currentDay++; game.SetPhase(GamePhase.Task); BroadcastSnapshot();
                QaCheck(publicEvent==selectedMapEvent && EventManager.Instance.GetEventForDay(game.currentDay)==selectedMapEvent,
                    "Next day retains the chosen event map");
                using (var end=new FastBufferWriter(1,Allocator.Temp))
                { end.WriteValueSafe(true); network.CustomMessagingManager.SendNamedMessage("thuong.qa.done",client,end); }
                yield return new WaitForSecondsRealtime(1);
            }
            else
            {
                while (!qaDone) { if (!QaAlive(output)) yield break; QaObserve(); yield return null; }
                QaCheck(qaMaps.Count==mapCatalog.Released.Length && qaMaps.Contains("village_large"),"Independent client loaded all released map layouts");
                QaCheck(qaEvents.Count==6,"Independent client displayed all six public events");
            }
            eventPresentation.Clear(); QaCheck(eventPresentation.OverlayCount==0,"Leave cleans overlay");
            var previousWorld=mapRuntime.World;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Map QA exit scene"));
            QaCheck(mapRuntime.World==null && (previousWorld==null || !previousWorld.activeSelf),"Scene exit immediately deactivates the map before asynchronous network leave");
            while(network!=null && (network.IsListening || network.ShutdownInProgress))
            { if(!QaAlive(output)) yield break; yield return null; }
            yield return new WaitForSecondsRealtime(.5f);
            QaCheck(network==null || !network.IsListening,"Scene leave completes network shutdown before player exit");
            if (!QaAlive(output)) yield break;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output,"THUONG_MAP_INDEPENDENT_"+(host?"HOST":"CLIENT")+"_PASSED: "+qaAssertions+" assertions\n");
            Application.Quit(0);
        }

        private void QaObserve()
        {
            if(!localMapReady && !qaMismatchSent)
            {
                qaMismatchSent=true;
                using var mismatch=new FastBufferWriter(1,Allocator.Temp); mismatch.WriteValueSafe(true);
                network.CustomMessagingManager.SendNamedMessage("thuong.qa.mismatch",NetworkManager.ServerClientId,mismatch);
            }
            if (SelectedMap!=null && localMapReady) qaMaps.Add(SelectedMap.mapId);
            if (publicEvent==GameEventType.None || publicEventDay!=visibleDay) return;
            qaEvents.Add(publicEvent);
            if (!qaSent.Add(publicEvent+":"+visiblePhase)) return;
            using var receipt=new FastBufferWriter(2,Allocator.Temp);
            receipt.WriteValueSafe((byte)publicEvent); receipt.WriteValueSafe((byte)visiblePhase);
            network.CustomMessagingManager.SendNamedMessage("thuong.qa.receipt",NetworkManager.ServerClientId,receipt);
        }
        private IEnumerator QaBenchmark(string output)
        {
            Application.runInBackground=true; Application.targetFrameRate=-1; QualitySettings.vSyncCount=0;
            bool previousOverview=worldCamera.overview; Rect previousViewport=Camera.main.rect;
            qaPresentationOverride=true; worldCamera.overview=true; Camera.main.rect=new Rect(0,0,1,1); worldCamera.Refresh(true);
            if(lobbyCanvas!=null) lobbyCanvas.enabled=false;
            if(lobbyRaycaster!=null) lobbyRaycaster.enabled=false;
            eventPresentation.Apply(GameEventType.None,1,GamePhase.Task,true);
            var standins=new GameObject("QA 23 visual stand-ins");
            for (int i=2;i<25;i++)
            {
                var avatar=new GameObject("Render avatar "+i,typeof(SpriteRenderer)); avatar.transform.SetParent(standins.transform,false);
                avatar.transform.position=SelectedMap.spawns[i]; var sprite=avatar.GetComponent<SpriteRenderer>();
                sprite.sprite=characterCast.idleSprites[i%characterCast.Count];
                sprite.sharedMaterial=mapRuntime.World.GetComponentInChildren<SpriteRenderer>().sharedMaterial;
                sprite.sortingOrder=ThuongDepthSort.Order(avatar.transform.position.y);
            }
            string summary="25 visible avatars = 2 network players + 23 static render stand-ins.\n"+
                "Full map overview, lobby hidden, batchmode, VSync off; engine frame time (not GPU timestamp).\n"+
                SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName+" / "+Screen.width+"x"+Screen.height+"\n";
            var originalVariant=new Thuong.Maps.ThuongMapVariant(SelectedMap,selectedMapEvent);
            var scenarios=new[]{("village",GameEventType.None),("mist_swamp",GameEventType.Fog)}
                .Concat(mapCatalog.Released.Where(m=>m.mapId == "village_large" || m.mapId.EndsWith("_expanded",StringComparison.Ordinal))
                    .SelectMany(m=>new[]{(m.mapId,GameEventType.None),(m.mapId,GameEventType.Fog)})).ToArray();
            foreach (var scenario in scenarios)
            {
                var definition=mapCatalog.Find(scenario.Item1);
                if(definition==null) continue;
                ApplyMapVariant(new Thuong.Maps.ThuongMapVariant(definition,GameEventType.None));
                while(!AllMapsReady) { if(!QaAlive(output)) yield break; yield return null; }
                foreach(Transform avatar in standins.transform)
                {
                    int index=avatar.GetSiblingIndex()+2; avatar.position=SelectedMap.spawns[index];
                    avatar.GetComponent<SpriteRenderer>().sortingOrder=ThuongDepthSort.Order(avatar.position.y);
                }
                worldCamera.Refresh(true);
                var times=new List<float>();
                for (int frame=0;frame<150;frame++)
                {
                    eventPresentation.Apply(scenario.Item2,1,GamePhase.Task,true);
                    yield return null;
                    if(frame>=30) times.Add(Time.unscaledDeltaTime*1000);
                }
                times.Sort(); summary+=scenario.Item1+(scenario.Item2==GameEventType.None?"_day":"_fog")+": mean_ms="+times.Average().ToString("F2")+
                    "; p95_ms="+times[(int)(times.Count*.95f)].ToString("F2")+"; allocated_bytes="+UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()+"\n";
            }
            qaPresentationOverride=false; Destroy(standins); eventPresentation.Clear(); ApplyMapVariant(originalVariant);
            worldCamera.overview=previousOverview; Camera.main.rect=previousViewport; worldCamera.Refresh(true);
            while(!AllMapsReady) { if(!QaAlive(output)) yield break; yield return null; }
            File.WriteAllText(output+".performance.txt",summary);
        }
        private void QaCheck(bool condition,string label) { qaAssertions++; if (!condition) qaFailure=label; }
        private bool QaAlive(string output)
        {
            if (qaFailure==null && Time.realtimeSinceStartup-qaStarted<150) return true;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output,"THUONG_MAP_INDEPENDENT_FAILED: "+(qaFailure??"timeout: "+status)); Application.Quit(1); return false;
        }
    }
    internal sealed class ThuongMapQaDriver : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args=Environment.GetCommandLineArgs();
            if (!args.Contains("--thuong-map-qa")) return;
            new GameObject("Map independent QA driver").AddComponent<ThuongMapQaDriver>();
        }
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"--thuong-map-qa");
            while (ThuongOnlineSession.Active==null) yield return null;
            yield return ThuongOnlineSession.Active.RunMapQa(args[index+1]=="host",args[index+2]);
        }
    }
}
#endif
