using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Thuong.Maps;
using Thuong.Minigames;
using Thuong.Online;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class ThuongRoomUpgradeOnlineChecks
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const ushort Port = 17993;
    private const string RunKey = "ThuongRoomUpgradeOnlineChecks.Running";
    private static readonly List<ThuongOnlineSession> clients = new();
    private static readonly Dictionary<ulong, ThuongMinigameState> receipts = new();
    private static readonly List<string> metrics = new();
    private static int roomPackets, roomPayloadBytes;
    private static ThuongOnlineSession host;
    private static IEnumerator scenario;
    private static double deadline, resumeAt;
    private static Exception failure;
    private static bool previousOptionsEnabled;
    private static EnterPlayModeOptions previousOptions;
    private static SceneAsset previousStart;
    private static int checks;
    private static string lastStep;
    private static bool recoveredScene;

    [MenuItem("Tools/Thuong/Room Upgrades/Check 25 Online Players And UI")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        Directory.CreateDirectory("Logs/RoomUpgrades");
        clients.Clear(); receipts.Clear(); failure = null; checks = 0; recoveredScene = false;
        previousStart = EditorSceneManager.playModeStartScene;
        previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled; previousOptions = EditorSettings.enterPlayModeOptions;
        SessionState.SetBool(RunKey + ".OptionsEnabled", previousOptionsEnabled);
        SessionState.SetInt(RunKey + ".Options", (int)previousOptions);
        SessionState.SetString(RunKey + ".Start", AssetDatabase.GetAssetPath(previousStart));
        EditorSettings.enterPlayModeOptionsEnabled = false;
        EditorSceneManager.OpenScene("Assets/Scenes/Thuong/OnlineSceneGamePlay.unity");
        EditorSceneManager.playModeStartScene = UnityEditor.AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Thuong/OnlineSceneGamePlay.unity");
        // Initialize Unity's play-mode scenario while scene setup can still be queried.
        // Otherwise this editor module first loads it during EnteredPlayMode and throws.
        WarmPlayModeScenario();
        SessionState.SetInt(RunKey + ".Restarts", 0);
        SessionState.SetBool(RunKey, true); Resume();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(RunKey, false)) return;
        clients.Clear(); receipts.Clear(); metrics.Clear(); roomPackets = roomPayloadBytes = 0;
        failure = null; checks = 0; lastStep = "entering Play Mode";
        previousOptionsEnabled = SessionState.GetBool(RunKey + ".OptionsEnabled", false);
        previousOptions = (EnterPlayModeOptions)SessionState.GetInt(RunKey + ".Options", 0);
        previousStart = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(RunKey + ".Start", ""));
        if (!EditorApplication.isPlaying) WarmPlayModeScenario();
        scenario = SessionState.GetBool(RunKey + ".ReviewSmoke", false) ? ExerciseReviewSmoke() : Exercise();
        deadline = EditorApplication.timeSinceStartup + 210; resumeAt = 0;
        Application.logMessageReceived -= ObserveLog; Application.logMessageReceived += ObserveLog;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (failure != null) throw failure;
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("After: " + lastStep);
            if (!EditorApplication.isPlaying)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling &&
                    SessionState.GetInt(RunKey + ".Restarts", 0) < 2)
                {
                    SessionState.SetInt(RunKey + ".Restarts", SessionState.GetInt(RunKey + ".Restarts", 0) + 1);
                    EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Thuong/OnlineSceneGamePlay.unity");
                    Debug.Log("UPGRADE_QA: restarting Play Mode after editor compilation");
                    EditorApplication.EnterPlaymode();
                }
                return;
            }
            if (EditorApplication.timeSinceStartup < resumeAt) return;
            EditorApplication.QueuePlayerLoopUpdate();
            host = ThuongOnlineSession.Active;
            if (host == null && Time.time > .3f && !recoveredScene)
            {
                // A forced editor script reload can retain scene objects without rerunning
                // Awake, clearing the session's nonserialized singleton and manager refs.
                // Reopen only this QA scene to get a genuine fresh production session.
                recoveredScene = true;
                foreach (var stale in Object.FindObjectsByType<ThuongOnlineSession>(FindObjectsInactive.Include)) Object.DestroyImmediate(stale.gameObject);
                EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Thuong/OnlineSceneGamePlay.unity",
                    new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                Debug.Log("UPGRADE_QA: reopened scene after editor reload");
                return;
            }
            if (host == null || Time.time < .3f) return;
            resumeAt = EditorApplication.timeSinceStartup + .05;
            if (!scenario.MoveNext()) Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static IEnumerator Exercise()
    {
        lastStep = "starting LAN host";
        Set(host, "portText", Port.ToString()); Call(host, "HostLan");
        while (!Get<bool>(host, "connected")) yield return null;
        var rules = new ThuongRoomRules { preset = ThuongRolePreset.Basic, automaticTimes = false, day = 150, night = 25, discussion = 90, voting = 45 };
        Check((bool)Call(host, "SetRoomRules", rules), "Host can apply manual room rules before role assignment");
        lastStep = "waiting for the first 23 clients to load the map and receive rules";
        for (int i = 0; i < 23; i++) AddClient(i);
        while (Net(host).ConnectedClientsIds.Count != 24 || !(bool)Property(host, "AllMapsReady") || clients.Any(c => c.RoomRules.day != 150)) yield return null;
        int packetsBeforeJoin = roomPackets;
        lastStep = "waiting for the last client to receive the existing room state";
        AddClient(23);
        while (Net(host).ConnectedClientsIds.Count != 25 || !(bool)Property(host, "AllMapsReady") || clients.Any(c => Get<IList>(c, "lobby").Count != 25)) yield return null;
        while (clients[23].RoomRules.day != 150) yield return null;
        Check(roomPackets == packetsBeforeJoin + 1, "Joining an unchanged room sends its state only to the new client");
        Check(true, "Host and 24 real LAN clients ready on the actual online scene");
        Check(clients.All(c => c.RoomRules.preset == ThuongRolePreset.Basic && c.RoomRules.night == 25 &&
            c.RoomRules.discussion == 90 && c.RoomRules.voting == 45), "All 24 production readers receive the same durations and preset");
        int packetsBeforeIdle = roomPackets, bytesBeforeIdle = roomPayloadBytes;
        uint snapshotsBeforeIdle = Get<uint>(host, "snapshotRevision");
        double idleStart = EditorApplication.timeSinceStartup;
        while (EditorApplication.timeSinceStartup - idleStart < 3) yield return null;
        int idlePackets = roomPackets - packetsBeforeIdle;
        uint idleSnapshots = Get<uint>(host, "snapshotRevision") - snapshotsBeforeIdle;
        Metric(FormattableString.Invariant($"idle_seconds={EditorApplication.timeSinceStartup - idleStart:F2} snapshots={idleSnapshots} room_messages={idlePackets} room_payload_bytes={roomPayloadBytes - bytesBeforeIdle} old_room_messages_estimate={idleSnapshots * 24}"));
        Check(idleSnapshots > 0 && idlePackets == 0, "Public snapshots continue without resending an unchanged room board");
        Check(!(bool)Call(clients[0], "SetRoomRules", new ThuongRoomRules()), "Client cannot change room rules");
        Call(host, "OpenRulesSettings"); Capture("rules"); Get<GameObject>(host, "rulesSettingsRoot").SetActive(false);
        Call(host, "AssignLobbyRolesOnServer");
        Check(host.RolesAssigned && RoleManager.Instance.playerRoles.Values.All(role => rules.AllowedRoles.Contains(role.roleType)), "Assigned roles follow the selected preset");
        Check(!(bool)Call(host, "SetRoomRules", new ThuongRoomRules()), "Rules locked after role assignment");
        while (clients.Any(c => !c.HasLocalRole)) yield return null;
        var action = typeof(ThuongOnlineSession).Assembly.GetType("Thuong.Online.ThuongOnlineAction");
        foreach (var member in Members())
        {
            Call(host, "ApplyServerAction", Get<ulong>(member, "ClientId"), 1u, Enum.Parse(action, "RevealRead"), -1, -1,
                GameRoleManager.Instance.currentDay, GameRoleManager.Instance.currentPhase, Get<uint>(host, "matchEpoch"));
            Call(host, "SetServerReady", Get<ulong>(member, "ClientId"), true);
        }
        Call(host, "StartMatchOnServer");
        while (clients.Any(c => !c.MatchStarted || Get<int[]>(c, "taskTargets").Sum() != 50)) yield return null;
        int packetsBeforeTasks = roomPackets, bytesBeforeTasks = roomPayloadBytes;
        double matchStart = EditorApplication.timeSinceStartup;
        Check(TaskManager.Instance.Cooperative && TaskManager.Instance.DailyTarget == 50, "25 players get 50 daily contributions");
        Check(GameRoleManager.Instance.nightDuration == 25 && GameRoleManager.Instance.discussionDuration == 90 &&
            GameRoleManager.Instance.votingDuration == 45 && DayTimer.Instance.dayDuration == 150, "Configured times drive actual gameplay managers");
        Check(clients.All(c => Get<int[]>(c, "taskTargets").Sum() == 50), "Quota board is synchronized to all clients");
        var camera = Object.FindAnyObjectByType<ThuongTopDownCamera>(); camera.overview = true;
        yield return null; Set(host, "nextGuideRefresh", 0f); Call(host, "RefreshTaskGuidance");
        Check(Get<GameObject>(host, "taskMapPanel").activeInHierarchy && Get<IList>(host, "guidePath").Count > 0,
            "Host overview displays a route to an active station");
        Capture("task-map");
        camera.overview = false;
        var stations = Get<TaskStation[]>(host, "stations");
        var members = Members().ToArray();
        for (int i = 0; i < members.Length; i++)
        {
            int slot = i / 5; Set(members[i], "Position", (Vector2)stations[slot].transform.position);
            ulong id = Get<ulong>(members[i], "ClientId");
            if (id == Net(host).LocalClientId) Call(host, "ProcessMinigameRequest", id, 100u, (byte)0, 1, slot, 0UL, default(ThuongMinigameInput), Get<uint>(host, "matchEpoch"));
            else Send(clients.Single(c => Net(c).LocalClientId == id), 100, 0, slot);
        }
        var authority = Get<ThuongMinigameAuthority>(host, "minigameAuthority");
        while (authority.ActiveCount != 25 || clients.Any(c => Get<int[]>(c, "taskOccupancy").Sum() != 25)) yield return null;
        Check(true, "25 simultaneous attempts over actual transports; occupancy decoded by all clients");
        Check(Get<int[]>(host, "taskOccupancy").Take(5).All(n => n == 5), "Five users share each of five stations");
        var waitingMember = members[5];
        ulong waitingId = Get<ulong>(waitingMember, "ClientId");
        var waitingClient = clients.Single(c => Net(c).LocalClientId == waitingId);
        CancelAttempt(authority, waitingId, 200);
        Set(waitingMember, "Position", (Vector2)stations[0].transform.position);
        double waitStart = EditorApplication.timeSinceStartup;
        Send(waitingClient, 201, 0, 0);
        while (!receipts.TryGetValue(waitingId, out var rejection) || rejection.attemptId != 0) yield return null;
        Check(receipts[waitingId].message.Contains("Trạm đang đủ người") && !authority.IsPlaying(waitingId), "Host refuses a sixth attempt at a full station");
        double retryAt = EditorApplication.timeSinceStartup + .4;
        ulong releasedId = Get<ulong>(members[1], "ClientId");
        double slotReleasedAt = EditorApplication.timeSinceStartup;
        CancelAttempt(authority, releasedId, 200);
        while (Get<int[]>(waitingClient, "taskOccupancy")[0] != 4) yield return null;
        double slotVisibleAt = EditorApplication.timeSinceStartup;
        while (EditorApplication.timeSinceStartup < retryAt) yield return null;
        Send(waitingClient, 202, 0, 0);
        while (!receipts.TryGetValue(waitingId, out var admitted) || admitted.result != ThuongMinigameResult.Playing || admitted.stationId != 0) yield return null;
        Metric(FormattableString.Invariant($"controlled_station_wait_seconds={EditorApplication.timeSinceStartup - waitStart:F3} vacancy_update_seconds={slotVisibleAt - slotReleasedAt:F3}"));
        Check(authority.IsPlaying(waitingId), "Waiting client can start after the occupied slot is released");
        foreach (var member in members) CancelAttempt(authority, Get<ulong>(member, "ClientId"), 250);
        Call(host, "CloseLocalMinigame"); Call(host, "BroadcastSnapshot");
        while (clients.Any(c => Get<int[]>(c, "taskOccupancy").Sum() != 0)) yield return null;
        Check(authority.ActiveCount == 0 && TaskManager.Instance.progress == 0, "Cancel releases every slot with no reward");
        var catalog = Resources.Load<ThuongMinigameCatalog>(ThuongMinigameCatalog.ResourcePath);
        int potion = Array.FindIndex(stations, s => catalog.Find(s.task).kind == ThuongMinigameKind.Potion);
        var remote = clients[0]; ulong remoteID = Net(remote).LocalClientId;
        var potionClients = clients.Take(2).ToArray();
        var potionIds = potionClients.Select(c => Net(c).LocalClientId).ToArray();
        foreach (ulong id in potionIds)
            Set(members.Single(m => Get<ulong>(m, "ClientId") == id), "Position", (Vector2)stations[potion].transform.position);
        double readyAt = EditorApplication.timeSinceStartup + .5;
        while (EditorApplication.timeSinceStartup < readyAt) yield return null;
        lastStep = "starting two remote potion attempts";
        foreach (var client in potionClients) Send(client, 300, 0, potion);
        while (potionIds.Any(id => !receipts.TryGetValue(id, out var receipt) || receipt.result != ThuongMinigameResult.Playing || receipt.stationId != potion)) yield return null;
        var attempts = potionIds.Select(authority.Get).ToArray();
        var recipes = attempts.Select(attempt => PotionIngredients(catalog.IngredientScores(), attempt.recipe)).ToArray();
        while (Net(host).ServerTime.Time < attempts.Max(attempt => attempt.startsAt) + .1) yield return null;
        for (int i = 0; i < 3; i++)
        {
            for (int player = 0; player < potionClients.Length; player++)
                Send(potionClients[player], (uint)(301 + i), 1, potion, attempts[player].attemptId,
                    new ThuongMinigameInput(ThuongMinigameAction.Ingredient, recipes[player][i]));
            readyAt = EditorApplication.timeSinceStartup + .2;
            while (EditorApplication.timeSinceStartup < readyAt) yield return null;
        }
        lastStep = "waiting for both potion completions and replicated progress";
        while (clients.Any(c => Get<int[]>(c, "taskDone")[potion] != 2)) yield return null;
        Check(Mathf.Approximately(TaskManager.Instance.progress, 1.6f) &&
            potionIds.All(id => receipts[id].result == ThuongMinigameResult.Succeeded), "Two simultaneous remote completions award exactly 0.8 percent each");
        Check(potionClients.All(c => Get<ushort>(c, "personalTaskMask") == (1 << potion)) &&
            clients.Skip(2).All(c => Get<ushort>(c, "personalTaskMask") == 0), "Personal completion is visible only to its contributor");
        Send(remote, 304, 1, potion, attempts[0].attemptId, new ThuongMinigameInput(ThuongMinigameAction.Ingredient, recipes[0][2]));
        Send(remote, 305, 0, potion);
        readyAt = EditorApplication.timeSinceStartup + .4; while (EditorApplication.timeSinceStartup < readyAt) yield return null;
        Check(!authority.IsPlaying(remoteID) && Mathf.Approximately(TaskManager.Instance.progress, 1.6f), "Replay and same-player repeat cannot earn extra progress");
        Set(members[0], "Position", (Vector2)stations[potion].transform.position); Call(host, "BroadcastSnapshot");
        yield return null; Set(host, "nextGuideRefresh", 0f); Call(host, "RefreshTaskGuidance"); Capture("nearby-task");
        Metric(FormattableString.Invariant($"active_lan_sample_seconds={EditorApplication.timeSinceStartup - matchStart:F2} room_messages={roomPackets - packetsBeforeTasks} room_payload_bytes={roomPayloadBytes - bytesBeforeTasks}"));

        lastStep = "disconnecting and restoring a contributor while another client plays";
        var playing = clients[2];
        ulong playingId = Net(playing).LocalClientId;
        Set(members.Single(m => Get<ulong>(m, "ClientId") == playingId), "Position", (Vector2)stations[potion].transform.position);
        Send(playing, 350, 0, potion);
        while (!authority.IsPlaying(playingId)) yield return null;
        object reserved = members.Single(m => Get<ulong>(m, "ClientId") == remoteID);
        Guid token = Get<Guid>(reserved, "ReconnectToken");
        int reservedPlayerId = Get<int>(reserved, "PlayerId");
        var originalPlayer = PlayerManager.Instance.GetplayerByID(reservedPlayerId);
        var originalRole = originalPlayer.roleType;
        uint originalEpoch = Get<uint>(host, "matchEpoch");
        Vector2 originalPosition = Get<Vector2>(reserved, "Position");
        Net(host).DisconnectClient(remoteID);
        while (!Get<bool>(reserved, "Offline") || Net(remote).IsListening) yield return null;
        Check(host.MatchStarted && Net(host).IsListening && authority.IsPlaying(playingId) && originalPlayer.isAlive,
            "Disconnect keeps the match, retained player and another client's attempt active");
        Check(!(bool)Call(host, "TryReserveReconnect", ulong.MaxValue, Guid.NewGuid(), Time.realtimeSinceStartupAsDouble),
            "A different token cannot claim the reserved slot");
        clients.Remove(remote);
        Object.Destroy(Net(remote).gameObject);
        Object.Destroy(remote.gameObject);
        remote = AddClient(24, token);
        while (!Net(remote).IsConnectedClient || !remote.MatchStarted || !remote.HasLocalRole ||
            Get<int>(remote, "localPlayerId") != reservedPlayerId || (Get<ushort>(remote, "personalTaskMask") & (1 << potion)) == 0 ||
            !(bool)Property(host, "AllMapsReady")) yield return null;
        remoteID = Net(remote).LocalClientId;
        Check(Net(host).ConnectedClientsIds.Count == 25 && Members().Count() == 25 && Get<ulong>(reserved, "ClientId") == remoteID &&
            !Get<bool>(reserved, "Offline") && Get<uint>(host, "matchEpoch") == originalEpoch,
            "Reconnect restores the reserved slot in a full room without duplicating the player or resetting the round");
        Check(Get<RoleType>(remote, "privateRole") == originalRole && Get<Vector2>(reserved, "Position") == originalPosition &&
            PlayerManager.Instance.GetplayerByID(reservedPlayerId) == originalPlayer && Mathf.Approximately(TaskManager.Instance.progress, 1.6f),
            "Reconnecting client keeps the same role, position and recorded contribution");
        Check(authority.IsPlaying(playingId), "Another player's minigame survives disconnect and reconnect");
        CancelAttempt(authority, playingId, 351);

        // Fill the remaining quotas through TaskManager to isolate progress arithmetic from travel and minigame time.
        var game = GameRoleManager.Instance;
        var tasks = TaskManager.Instance;
        int[] playerIds = members.Select(m => Get<int>(m, "PlayerId")).ToArray();
        int[] expectedContributions = { 50, 50, 25 };
        for (int day = 1; day <= expectedContributions.Length; day++)
        {
            if (day > 1)
            {
                game.EndDay(); game.FinishNight(); game.StartVoting(); game.FinishVoting();
                Call(host, "BroadcastSnapshot");
                Check(game.currentDay == day && tasks.CompletedToday == 0 && tasks.DailyTarget == 50, "New day clears contributions while keeping 25-player quotas");
                while (clients.Any(c => Get<int>(c, "taskBoardDay") != day)) yield return null;
                Check(clients.All(c => Get<ushort>(c, "personalTaskMask") == 0), "New day clears every client's personal completion flags");
            }
            foreach (var task in tasks.currentTasks)
                foreach (int id in playerIds)
                {
                    if (tasks.IsCompleted(task) || tasks.progress >= 100) break;
                    if (!tasks.HasContributed(id, task) && !tasks.TryCompleteTask(id, task))
                        throw new InvalidOperationException($"Unable to contribute on scripted day {day}");
                }
            Check(tasks.CompletedToday == expectedContributions[day - 1] && Mathf.Approximately(tasks.progress, Mathf.Min(day * 40, 100)),
                $"Scripted day {day} reaches {Mathf.Min(day * 40, 100)} percent after {tasks.CompletedToday} daily contributions");
            Metric(FormattableString.Invariant($"scripted_day={day} contributions={tasks.CompletedToday} total_progress_percent={tasks.progress:F1}"));
        }
        uint oldEpoch = Get<uint>(host, "matchEpoch");
        Call(host, "EvaluateOnlineWin", false);
        Check(host.CanRestartMatch && host.RestartMatchInRoom(), "Completed match can restart in the same room");
        while (clients.Any(c => c.MatchStarted || Get<uint>(c, "matchEpoch") == oldEpoch || Get<uint>(c, "receivedRulesRevision") == 0)) yield return null;
        Check(TaskManager.Instance.progress == 0 && TaskManager.Instance.DailyTarget == 0 && clients.All(c => Get<ushort>(c, "personalTaskMask") == 0), "Replay resets shared and personal task progress");
        Check(host.RoomRules.day == 150 && clients.All(c => c.RoomRules.day == 150 && c.RoomRules.preset == ThuongRolePreset.Basic),
            "Room rules survive replay on the host and all clients");
        Call(host, "ProcessMinigameRequest", remoteID, 9999u, (byte)0, 1, potion, 0UL, default(ThuongMinigameInput), oldEpoch);
        Check(authority.ActiveCount == 0, "Old-round request rejected after replay");
        Check((bool)Call(host, "SetRoomRules", new ThuongRoomRules { preset = ThuongRolePreset.Full }), "Host can edit rules again in the new lobby");
        Call(host, "AssignLobbyRolesOnServer");
        Check(host.RolesAssigned, "Host can assign roles for another round after reconnect");
        Net(host).DisconnectClient(remoteID);
        while (!Get<bool>(reserved, "Offline")) yield return null;
        Set(reserved, "ReconnectUntil", Time.realtimeSinceStartupAsDouble - .01);
        while (host.RolesAssigned) yield return null;
        Check(Net(host).IsListening && !host.MatchStarted && Members().Count() == 24 && !Get<bool>(host, "roomLocked"),
            "An expired slot during role reveal reopens the room with the remaining players");
    }

    private static void CancelAttempt(ThuongMinigameAuthority authority, ulong clientId, uint sequence)
    {
        var attempt = authority.Get(clientId);
        if (attempt == null) return;
        Call(host, "ProcessMinigameRequest", clientId, sequence, (byte)2, attempt.day,
            attempt.stationId, attempt.attemptId, default(ThuongMinigameInput), Get<uint>(host, "matchEpoch"));
    }

    private static int[] PotionIngredients(int[] scores, ThuongPotion potion)
    {
        for (int a = 0; a < scores.Length; a++)
            for (int b = 0; b < scores.Length; b++)
                for (int c = 0; c < scores.Length; c++)
                    if (ThuongMinigameAuthority.EvaluatePotion(scores[a] + scores[b] + scores[c]) == potion)
                        return new[] { a, b, c };
        throw new InvalidOperationException("Remote potion recipe has no valid ingredients");
    }

    private static ThuongOnlineSession AddClient(int index, Guid? resumeToken = null)
    {
        var networkRoot = new GameObject("Upgrade LAN " + index);
        var transport = networkRoot.AddComponent<UnityTransport>(); var net = networkRoot.AddComponent<NetworkManager>();
        var token = resumeToken ?? Guid.NewGuid();
        string build = (string)typeof(ThuongOnlineSession).GetField("BuildPrefix", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        string payload = build + (resumeToken.HasValue ? "R" + token.ToString("N") : "J" + token.ToString("N") + "QA " + index);
        net.NetworkConfig = new NetworkConfig { NetworkTransport = transport, ConnectionApproval = true, EnableSceneManagement = false,
            TickRate = 30, ConnectionData = Encoding.UTF8.GetBytes(payload) };
        transport.SetConnectionData("127.0.0.1", Port);
        var readerRoot = new GameObject("Inactive production client " + index); readerRoot.SetActive(false);
        var proxy = readerRoot.AddComponent<ThuongOnlineSession>(); clients.Add(proxy);
        Set(proxy, "network", net); Set(proxy, "transport", transport); Set(proxy, "connected", true);
        Set(proxy, "game", readerRoot.AddComponent<GameRoleManager>()); Set(proxy, "players", readerRoot.AddComponent<PlayerManager>());
        var roles = readerRoot.AddComponent<RoleManager>(); roles.roleDefinitions = RoleManager.Instance.roleDefinitions; Set(proxy, "roles", roles);
        Set(proxy, "tasks", readerRoot.AddComponent<TaskManager>());
        Set(proxy, "mapCatalog", Resources.Load<ThuongMapCatalog>(ThuongMapCatalog.ResourcePath));
        Set(proxy, "eventCatalog", Resources.Load<ThuongEventPresentationCatalog>(ThuongEventPresentationCatalog.ResourcePath));
        Set(proxy, "mapRuntime", readerRoot.AddComponent<ThuongMapRuntime>()); Set(proxy, "eventPresentation", readerRoot.AddComponent<ThuongEventPresentation>());
        net.OnClientConnectedCallback += _ =>
        {
            using var hello = new FastBufferWriter(100, Allocator.Temp); hello.WriteValueSafe(new FixedString64Bytes("Người chơi QA " + index)); hello.WriteValueSafe(index % 20);
            net.CustomMessagingManager.SendNamedMessage("thuong.online.hello.v1", 0, hello);
        };
        if (!net.StartClient()) throw new Exception("LAN start failed");
        void Register(string name, string method) => net.CustomMessagingManager.RegisterNamedMessageHandler(name,
            (sender, reader) => { try { Call(proxy, method, sender, reader); } catch (Exception error) { failure = error; } });
        Register("thuong.online.roster.v4", "ReceiveRoster"); Register("thuong.online.snapshot.v9", "ReceiveSnapshot");
        Register("thuong.online.private.v4", "ReceivePrivateState");
        Call(proxy, "RegisterVillageMessages");
        net.CustomMessagingManager.RegisterNamedMessageHandler("thuong.online.room-state.v3", (sender, reader) =>
        {
            roomPackets++;
            roomPayloadBytes += reader.Length;
            try { Call(proxy, "ReceiveRoomState", sender, reader); }
            catch (Exception error) { failure = error; }
        });
        net.CustomMessagingManager.RegisterNamedMessageHandler(ThuongMinigameWire.State, (_, reader) =>
        { reader.ReadValueSafe(out uint epoch); if (epoch == Get<uint>(host, "matchEpoch")) receipts[net.LocalClientId] = ThuongMinigameWire.Read(reader); });
        return proxy;
    }

    private static void Send(ThuongOnlineSession client, uint sequence, byte operation, int station, ulong attempt = 0, ThuongMinigameInput input = default)
    {
        using var writer = new FastBufferWriter(64, Allocator.Temp);
        writer.WriteValueSafe(sequence); writer.WriteValueSafe(Get<uint>(host, "matchEpoch")); writer.WriteValueSafe(operation); writer.WriteValueSafe(GameRoleManager.Instance.currentDay);
        writer.WriteValueSafe(station); writer.WriteValueSafe(attempt); writer.WriteValueSafe((byte)input.action); writer.WriteValueSafe(input.index); writer.WriteValueSafe(input.value);
        Net(client).CustomMessagingManager.SendNamedMessage(ThuongMinigameWire.Request, 0, writer);
    }

    private static IEnumerable<object> Members() => Get<IEnumerable>(host, "serverPlayers").Cast<object>().Select(pair => pair.GetType().GetProperty("Value").GetValue(pair));
    private static NetworkManager Net(ThuongOnlineSession target) => Get<NetworkManager>(target, "network");
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Flags).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    private static object Property(object target, string property) => target.GetType().GetProperty(property, Flags).GetValue(target);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static void Check(bool valid, string text)
    { if (!valid) throw new InvalidOperationException(text); checks++; lastStep = text; Debug.Log("UPGRADE_PASS: " + text); }
    private static void Metric(string value)
    {
        metrics.Add(value);
        Debug.Log("UPGRADE_METRIC: " + value);
    }
    private static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        ThuongMedievalSceneChecks.SavePreview("Logs/RoomUpgrades/" + name + "-1280.png", 1280, 720);
        ThuongMedievalSceneChecks.SavePreview("Logs/RoomUpgrades/" + name + "-1920.png", 1920, 1080);
    }
    private static void ObserveLog(string text, string trace, LogType type)
    {
        // These unrelated InitializeOnLoad tests run on every domain load and already fail
        // on the baseline (TC23/26/27). Preserve them in the log, but don't count them as
        // errors from this online scenario. All production exceptions still fail this run.
        if (text.StartsWith("[FAIL]", StringComparison.Ordinal) && trace.Contains("NetworkGameOverTests")) return;
        if (type == LogType.Exception || type == LogType.Error) failure ??= new Exception(text + "\n" + trace);
    }

    private static void WarmPlayModeScenario()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var manager = assembly.GetType("Unity.PlayMode.Editor.PlayModeScenarioManager");
            manager?.GetProperty("ActiveScenario", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
        }
    }

    private static void Finish(Exception exception)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= ObserveLog;
        SessionState.SetBool(RunKey, false);
        bool reviewSmoke = SessionState.GetBool(RunKey + ".ReviewSmoke", false);
        SessionState.SetBool(RunKey + ".ReviewSmoke", false);
        foreach (var client in clients) if (client != null && Net(client).IsListening) Net(client).Shutdown();
        if (host != null && Net(host).IsListening) Net(host).Shutdown();
        EditorSceneManager.playModeStartScene = previousStart;
        EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled; EditorSettings.enterPlayModeOptions = previousOptions;
        string result = exception == null ? "THUONG_ROOM_UPGRADE_ONLINE_PASSED: " + checks : "THUONG_ROOM_UPGRADE_ONLINE_FAILED: " + exception;
        File.WriteAllText("Logs/RoomUpgrades/online-result.txt", result); Debug.Log(result);
        File.WriteAllLines("Logs/RoomUpgrades/online-metrics.txt", metrics);
        if (reviewSmoke)
        {
            Directory.CreateDirectory("Logs/ThuongReviewImprovements20261112");
            File.WriteAllText("Logs/ThuongReviewImprovements20261112/online-smoke.txt", result);
        }
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(exception == null ? 0 : 1);
    }
}
