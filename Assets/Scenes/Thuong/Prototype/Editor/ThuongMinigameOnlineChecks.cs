using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Thuong.Minigames;
using Thuong.Online;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Extends the existing real-transport 25-player scenario. No test messages can award a client-reported win.
public static class ThuongMinigameOnlineChecks
{
    private static ThuongOnlineSession session;
    private static NetworkManager[] peers;
    private static ThuongMinigameAuthority authority;
    private static ThuongMinigameCatalog catalog;
    private static TaskStation[] stations;
    private static readonly Dictionary<ulong, ThuongMinigameState> received = new();
    private static readonly Dictionary<ulong, uint> sequences = new();
    private static int stage, wood, traps, lastSentRound, checks;
    private static double until;
    private static float timer;
    private static Vector2 lockedPosition;
    private static ThuongMinigameState attempt;

    [MenuItem("Tools/Thuong/Minigames/Check Online With 25 Players")]
    public static void Run()
    {
        ThuongMinigameChecks.Run();
        SessionState.SetBool("ThuongMinigameOnlineChecks", true);
        ThuongOnlineChecks.Run();
    }

    public static bool Enabled => SessionState.GetBool("ThuongMinigameOnlineChecks", false);
    public static void Begin(ThuongOnlineSession value, NetworkManager[] clients)
    {
        session = value; peers = clients; checks = stage = lastSentRound = 0; received.Clear(); sequences.Clear();
        authority = Field<ThuongMinigameAuthority>(session, "minigameAuthority");
        catalog = Resources.Load<ThuongMinigameCatalog>(ThuongMinigameCatalog.ResourcePath);
        stations = Field<TaskStation[]>(session, "stations");
        wood = Array.FindIndex(stations, station => catalog.Find(station.task)?.kind == ThuongMinigameKind.ChopWood);
        traps = Array.FindIndex(stations, station => catalog.Find(station.task)?.kind == ThuongMinigameKind.Trapsweeper);
        Require(stations.Length == 10 && TaskManager.Instance.allTasks.Count == 10, "Six existing tasks plus four separate new tasks");
        TaskManager.Instance.currentTasks.Clear(); TaskManager.Instance.currentTasks.AddRange(TaskManager.Instance.allTasks);
        TaskManager.Instance.progress = 0;
        DayTimer.Instance.dayDuration = 100; DayTimer.Instance.StartTimer();
        foreach (NetworkManager peer in peers)
        {
            var captured = peer;
            peer.CustomMessagingManager.RegisterNamedMessageHandler(ThuongMinigameWire.State, (_, reader) =>
            { received[captured.LocalClientId] = ThuongMinigameWire.Read(reader); });
        }
        foreach (var member in Members().Values) Set(member, "Position", (Vector2)stations[wood].transform.position);
        Call(session, "BroadcastSnapshot");
        Call(session, "RequestTaskMinigame", wood);
        attempt = authority.Get(0);
        Require(attempt != null && session.IsPlayingTask && TaskManager.Instance.progress == 0, "E opens a host minigame without reward");
        timer = DayTimer.Instance.TimeRemaining; lockedPosition = Position(0);
        Call(session, "ApplyServerInput", 0UL, (ushort)500, Vector2.right);
        foreach (var peer in peers) Send(peer, 0, wood, 0);
        Send(peers[0], 1, wood, attempt.attemptId, new(ThuongMinigameAction.Chop, attempt.round));
        until = Time.unscaledTimeAsDouble + 1.2;
    }

    public static bool Tick()
    {
        double time = Time.unscaledTimeAsDouble;
        var net = session.GetComponent<NetworkManager>();
        switch (stage)
        {
            case 0:
                if (time < until || received.Count != 24) return false;
                Require(authority.ActiveCount == 1 && authority.Get(0).score == 0, "24 competing requests and wrong-owner action cannot take host lock");
                Require(Position(0) == lockedPosition, "Server locks movement even with forged movement input");
                Require(DayTimer.Instance.TimeRemaining < timer - .5f && Time.timeScale == 1 && SceneManager.GetActiveScene().name == "OnlineSceneGamePlay", "Timer and active online scene continue while playing");
                Require(UnityEngine.Object.FindObjectsByType<AudioListener>().Count(listener => listener.enabled) == 1 &&
                    UnityEngine.Object.FindObjectsByType<EventSystem>().Length == 1, "One audio listener and one event system");
                var window = Field<ThuongMinigameWindow>(session, "minigameWindow");
                var local = Field<GameObject>(window, "localRoot");
                Require(local.scene.GetPhysicsScene2D() != Physics2D.defaultPhysicsScene, "Separate local physics scene");
                Directory.CreateDirectory("Logs/Minigames");
                ThuongMedievalSceneChecks.SavePreview("Logs/Minigames/wood.png");
                Call(session, "CancelLocalMinigame");
                Require(authority.ActiveCount == 0 && TaskManager.Instance.progress == 0, "Cancel releases lock without reward");
                Call(session, "RequestTaskMinigame", wood);
                Require(authority.IsPlaying(0), "Reopening obtains another attempt");
                // Exercise external task completion (including another server system), even though
                // normal competing minigame requests cannot bypass the exclusive lock.
                Require(TaskManager.Instance.TryCompleteTask(1, stations[wood].task), "External completion fixture");
                Call(session, "BroadcastSnapshot"); session.RefreshHud(UnityEngine.Object.FindAnyObjectByType<GameHUD>());
                Call(session, "UpdateMinigames");
                Require(!session.IsPlayingTask && !authority.IsPlaying(0), "A task completed elsewhere closes the old modal and releases its attempt");
                TaskManager.Instance.ResetForNewMatch(); TaskManager.Instance.currentTasks.AddRange(TaskManager.Instance.allTasks);
                Call(session, "BroadcastSnapshot");
                Send(peers[0], 0, wood, 0); stage = 1; until = time + .5;
                break;
            case 1:
                if (time < until || authority.Get(peers[0].LocalClientId) == null) return false;
                attempt = authority.Get(peers[0].LocalClientId);
                Require(attempt.attemptId != Field<HashSet<ulong>>(session, "dismissedAttempts").First(), "Retry receives a new attempt ID");
                stage = 2;
                break;
            case 2:
                var current = authority.Get(peers[0].LocalClientId);
                if (current != null)
                {
                    if (net.ServerTime.Time >= current.roundAt - .08 && lastSentRound != current.round)
                    {
                        lastSentRound = current.round;
                        var input = new ThuongMinigameInput(ThuongMinigameAction.Chop, current.round);
                        Send(peers[0], 1, wood, current.attemptId, input);
                        Send(peers[0], 1, wood, current.attemptId, input, true); // exact replay, same sequence
                    }
                    return false;
                }
                if (!received.TryGetValue(peers[0].LocalClientId, out var finished) || finished.attemptId != attempt.attemptId || finished.result == ThuongMinigameResult.Playing) return false;
                Require(finished.result == ThuongMinigameResult.Succeeded && TaskManager.Instance.progress == 10 && TaskManager.Instance.IsCompleted(stations[wood].task),
                    $"Remote client wins through real transport and awards once (result={finished.result}, score={finished.score}, mistakes={finished.mistakes}, progress={TaskManager.Instance.progress}, message={finished.message})");
                Send(peers[0], 1, wood, attempt.attemptId, new(ThuongMinigameAction.Chop, lastSentRound));
                var actionType = typeof(ThuongOnlineSession).Assembly.GetType("Thuong.Online.ThuongOnlineAction");
                Call(session, "SendAction", Enum.ToObject(actionType, 0), traps, -1);
                until = time + .4; stage = 3;
                break;
            case 3:
                if (time < until) return false;
                Require(TaskManager.Instance.progress == 10 && !TaskManager.Instance.IsCompleted(stations[traps].task), "Duplicate result and legacy CompleteTask cannot bypass minigame");
                Set(Members()[peers[0].LocalClientId], "Position", (Vector2)stations[traps].transform.position);
                Send(peers[0], 0, traps, 0); until = time + .5; stage = 4;
                break;
            case 4:
                if (time < until || authority.Get(peers[0].LocalClientId) == null) return false;
                attempt = authority.Get(peers[0].LocalClientId);
                Require(received[peers[0].LocalClientId].cells.All(cell => cell == -2), "Remote client receives no hidden trap layout");
                // A player's elimination invalidates the attempt before another action can be processed.
                int actor = Field<int>(Members()[peers[0].LocalClientId], "PlayerId");
                PlayerManager.Instance.GetplayerByID(actor).isAlive = false;
                Call(session, "UpdateMinigames");
                Require(!authority.IsPlaying(peers[0].LocalClientId) && TaskManager.Instance.progress == 10, "Elimination cancels attempt without reward");
                PlayerManager.Instance.GetplayerByID(actor).isAlive = true;
                until = time + .5; stage = 5;
                break;
            case 5:
                if (time < until) return false;
                Set(Members()[0UL], "Position", (Vector2)stations[traps].transform.position);
                Call(session, "BroadcastSnapshot"); Call(session, "RequestTaskMinigame", traps);
                Require(session.IsPlayingTask && authority.IsPlaying(0), "Host opens traps after cancellation");
                attempt = authority.Get(0);
                ThuongMedievalSceneChecks.SavePreview("Logs/Minigames/traps.png");
                GameRoleManager.Instance.EndDay();
                Call(session, "BroadcastSnapshot"); session.RefreshHud(UnityEngine.Object.FindAnyObjectByType<GameHUD>());
                Require(!session.IsPlayingTask, "Modal closes before night HUD");
                Call(session, "UpdateMinigames");
                Require(authority.ActiveCount == 0, "Night invalidates all attempts");
                Send(peers[0], 1, traps, attempt.attemptId, new(ThuongMinigameAction.Reveal, 0));
                until = time + .4; stage = 6;
                break;
            case 6:
                if (time < until) return false;
                Require(TaskManager.Instance.progress == 10 && authority.ActiveCount == 0, "Late packet from previous phase gives no reward");
                Require(UnityEngine.Object.FindObjectsByType<ThuongTaskMinigameView>().Length == 0 &&
                    UnityEngine.Object.FindObjectsByType<ThuongMinigameWindow>().Length == 0, "No task objects remain after closing");
                foreach (var peer in peers) peer.CustomMessagingManager.UnregisterNamedMessageHandler(ThuongMinigameWire.State);
                DayTimer.Instance.dayDuration = 45;
                File.WriteAllText("Logs/Minigames/online.txt", "THUONG_MINIGAME_ONLINE_PASSED: " + checks + " assertions with 25 LAN peers\n");
                Debug.Log("THUONG_MINIGAME_ONLINE_PASSED: " + checks + " assertions with 25 LAN peers");
                SessionState.SetBool("ThuongMinigameOnlineChecks", false);
                return true;
        }
        return false;
    }

    private static void Send(NetworkManager peer, byte operation, int station, ulong attempt, ThuongMinigameInput input = default, bool repeat = false)
    {
        ulong owner = peer.LocalClientId;
        if (!sequences.ContainsKey(owner)) sequences[owner] = 0;
        uint seq = repeat ? sequences[owner] : ++sequences[owner];
        using var writer = new FastBufferWriter(64, Allocator.Temp);
        writer.WriteValueSafe(seq); writer.WriteValueSafe(operation); writer.WriteValueSafe(GameRoleManager.Instance.currentDay);
        writer.WriteValueSafe(station); writer.WriteValueSafe(attempt); writer.WriteValueSafe((byte)input.action);
        writer.WriteValueSafe(input.index); writer.WriteValueSafe(input.value);
        peer.CustomMessagingManager.SendNamedMessage(ThuongMinigameWire.Request, 0, writer);
    }
    private static IDictionary Members() => Field<IDictionary>(session, "serverPlayers");
    private static Vector2 Position(ulong id) => Field<Vector2>(Members()[id], "Position");
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(obj);
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(obj, value);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(obj, args);
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Minigame online check failed: " + message); checks++; }
}
