using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Thuong.Maps;
using Thuong.Online;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Runs real LAN transports in one editor process, keeping the saved scenes untouched.
public static class ThuongOnlineChecks
{
    private const string Key = "ThuongOnlineUIFormChecks";
    private const string Output = "Logs/OnlineUIForm";
    private const ushort Port = 17977;
    private sealed class Peer
    {
        public NetworkManager Network;
        public uint Revision, VoteRevision, Epoch;
        public int PublicCount, RosterCount, VoteTally;
        public bool Started, HasRole, RolesAssigned, HasRead;
        public int Skin;
        public uint MapEpoch;
        public string MapId;
        public GameEventType MapEvent;
        public string Result;
        public int PrivatePlayerId = -1, PublicLocalPlayerId = -1;
        public bool HasMatchResult;
        public byte MatchWinner, MatchReason;
        public int MatchPlayerCount, MatchWinnerCount;
    }
    private static readonly List<Peer> peers = new();
    private static int stage, checks;
    private static double began, next;
    private static ThuongOnlineSession session;
    private static GameHUD hud;
    private static BaseRole[] assignedRoles;
    private static int exitCode;
    private static double cleanupDeadline;
    private static int mapCycle;
    private static int mapSettingsStage;
    private static ThuongMapVariant[] mapVariants;

    public static void Run()
    {
        mapCycle = 0;
        mapSettingsStage = 0;
        mapVariants = Resources.Load<ThuongMapCatalog>(ThuongMapCatalog.ResourcePath)
            .GetVariants(Resources.Load<ThuongEventPresentationCatalog>(ThuongEventPresentationCatalog.ResourcePath));
        Directory.CreateDirectory(Output);
        for (int count = 1; count <= 25; count++)
        for (int seed = 0; seed < 32; seed++)
        {
            var pool = RolePoolBuilder.Build(count, seed);
            Require(pool.Count == count && pool.All(role => RoleCatalog.AllConcreteRoles.Contains(role)), "Role pool uses exactly the connected player count and concrete roles");
        }
        ThuongGameplayIntegrationChecks.Run();
        EditorSceneManager.OpenScene("Assets/Scenes/Thuong/OnlineSceneGamePlay.unity");
        SessionState.SetBool(Key + "OriginalOptions", EditorSettings.enterPlayModeOptionsEnabled);
        EditorSettings.enterPlayModeOptionsEnabled = false;
        SessionState.SetBool(Key, true);
        Resume();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        began = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - began > 180) throw new Exception("Online checks timed out at stage " + stage);
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
            EditorApplication.QueuePlayerLoopUpdate();
            session = ThuongOnlineSession.Active;
            if (session == null || Time.time < .5f) return;
            hud = Object.FindAnyObjectByType<GameHUD>();
            // Entering Play Mode reloads the editor domain and clears this cache.
            mapVariants ??= Resources.Load<ThuongMapCatalog>(ThuongMapCatalog.ResourcePath)
                .GetVariants(Resources.Load<ThuongEventPresentationCatalog>(ThuongEventPresentationCatalog.ResourcePath));
            switch (stage)
            {
                case 0:
                    Require(hud != null && hud.gameObject.activeSelf, "Original scene HUD remains installed");
                    Require(!hud.GetComponent<Canvas>().enabled, "Gameplay canvas hidden in lobby");
                    Set(session, "portText", Port.ToString());
                    Invoke(session, "SetLobbyCapacity", 25);
                    Require(Field<bool>(session, "randomMapSelection"), "Random remains the default map mode");
                    Require(!(bool)Invoke(session, "SetHostMapSelection", false, "missing-map", GameEventType.None), "Invalid manual selection does not replace the current configuration");
                    Invoke(session, "HostLan");
                    Require(Field<bool>(session, "mapDrawComplete") && mapVariants.Any(v => v.Map == session.SelectedMap && v.EventType == session.SelectedMapEvent),
                        "Creating a room automatically draws a released normal/event map: " + session.Feedback);
                    uint initialMapEpoch = Field<uint>(session, "mapEpoch");
                    var initialMap = session.SelectedMap; var initialMapEvent = session.SelectedMapEvent;
                    Invoke(session, "EnsureRoomMapSelection"); Invoke(session, "EnsureRoomMapSelection");
                    Require(Field<uint>(session, "mapEpoch") == initialMapEpoch && session.SelectedMap == initialMap && session.SelectedMapEvent == initialMapEvent,
                        "Repeated initialization never rerolls the room's map");
                    for (int i = 1; i < 25; i++) AddPeer(i);
                    stage = 1;
                    break;
                case 1:
                    if (session.GetComponent<NetworkManager>().ConnectedClientsIds.Count != 25 ||
                        Field<System.Collections.ICollection>(session, "lobby").Count != 25 || peers.Any(peer => peer.RosterCount != 25)) return;
                    var members = Field<System.Collections.IEnumerable>(session, "lobby").Cast<object>().ToArray();
                    if (peers.Any(peer => !members.Any(member => Field<ulong>(member, "ClientId") == peer.Network.LocalClientId &&
                        Field<int>(member, "CharacterIndex") == peer.Skin))) return;
                    if (members.Any(member => !Field<bool>(member, "MapReady"))) return;
                    Require(peers.All(peer => peer.MapId == session.SelectedMap.mapId && peer.MapEvent == session.SelectedMapEvent),
                        "All transports receive the same base map and normal/event variant");
                    if (mapCycle < mapVariants.Length)
                    {
                        Require((bool)Invoke(session, "ApplyMapVariant", mapVariants[mapCycle++]), "QA can traverse each normal/event map");
                        Require(!(bool)typeof(ThuongOnlineSession).GetProperty("AllMapsReady",BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session), "Changing map invalidates client acknowledgements");
                        Invoke(session, "AssignLobbyRolesOnServer"); Require(!session.RolesAssigned,"Cannot assign roles before fresh map acknowledgements");
                        return;
                    }
                    if (!CheckMapSelectionControls()) return;
                    Require(members.All(member => !Field<bool>(member, "Ready")), "Readiness is locked until roles are assigned and read");
                    Invoke(session, "RefreshLobbyView");
                    var lobbyContent = Field<RectTransform>(session, "lobbyContent");
                    var avatars = lobbyContent.GetComponentsInChildren<Image>().Where(image => image.name == "Player Avatar").ToArray();
                    Require(avatars.Length == 25 && avatars.All(image => image.sprite != null) && avatars.Select(image => image.sprite).Distinct().Count() == 20,
                        $"Twenty-five lobby rows use selected avatars (rows={avatars.Length}, sprites={avatars.Select(image => image.sprite).Distinct().Count()})");
                    Require(lobbyContent.GetComponentInParent<ScrollRect>().verticalScrollbar != null, "Full lobby has a draggable scrollbar");
                    Require(Field<Canvas>(session, "lobbyCanvas").enabled, "Native lobby canvas is visible before role assignment");
                    Require(Field<Button>(session, "mapSettingsButton").interactable, "Host can configure map mode before assigning roles");
                    Require(Field<TMP_Text>(session, "mapVariantLabel").text == (session.SelectedMapEvent == GameEventType.None ? "MAP BÌNH THƯỜNG" : ThuongEventPresentationCatalog.Name(session.SelectedMapEvent)),
                        "Lobby shows the selected normal/event map");
                    var thumbnail=Field<Image>(session,"mapPreview").rectTransform;
                    var capacityButton=Field<Button>(session,"lobbyIncreaseButton").GetComponent<RectTransform>();
                    var thumbnailCorners=new Vector3[4]; var capacityCorners=new Vector3[4];
                    thumbnail.GetWorldCorners(thumbnailCorners); capacityButton.GetWorldCorners(capacityCorners);
                    Require(!new Rect(thumbnailCorners[0],thumbnailCorners[2]-thumbnailCorners[0]).Overlaps(new Rect(capacityCorners[0],capacityCorners[2]-capacityCorners[0])),"Map thumbnail does not cover capacity controls");
                    ThuongMedievalSceneChecks.SavePreview(Output + "/lobby.png");
                    Invoke(session, "SetLobbyCapacity", 2);
                    Require(Field<int>(session, "lobbyCapacity") == 25, "Capacity cannot exclude connected players");
                    Invoke(session, "StartMatchOnServer");
                    Require(!session.MatchStarted, "Host cannot start before assigning roles");
                    uint previous = Field<uint>(session, "snapshotRevision");
                    Field<Button>(session, "lobbyRoleButton").onClick.Invoke();
                    Require(session.RolesAssigned && !session.MatchStarted && Field<uint>(session, "snapshotRevision") > previous, "Role assignment preserves snapshot revision and waits before gameplay");
                    Require(RoleManager.Instance.playerRoles.Count == 25 && GameRoleManager.Instance.currentState == GameState.RoleReveal, "Twenty-five server roles and real reveal phase");
                    Require(EventManager.Instance.IsMapVariant && EventManager.Instance.GetEventForDay(1) == session.SelectedMapEvent &&
                        EventManager.Instance.GetEventForDay(8) == session.SelectedMapEvent, "Role assignment keeps the event map for the full match");
                    uint lockedMapEpoch = Field<uint>(session, "mapEpoch");
                    Require(!(bool)Invoke(session, "SetHostMapSelection", false, "village", GameEventType.None) && Field<uint>(session, "mapEpoch") == lockedMapEpoch,
                        "Map settings cannot change after role assignment");
                    Invoke(session, "RefreshLobbyView");
                    Require(!Field<Button>(session, "mapSettingsButton").interactable, "Map settings button locks after assigning roles");
                    assignedRoles = RoleManager.Instance.playerRoles.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray();
                    next = EditorApplication.timeSinceStartup + 1;
                    stage = 2;
                    break;
                case 2:
                    if (peers.Any(peer => !peer.RolesAssigned || !peer.HasRole || peer.PublicCount != 25)) return;
                    Require(peers.All(peer => peer.PrivatePlayerId == peer.PublicLocalPlayerId), "Each client receives only its own role identity");
                    Require(!hud.GetComponent<Canvas>().enabled && !GameRoleManager.Instance.enabled, "HUD and gameplay stay paused during the role card");
                    Require(session.PhaseTimeRemaining == 0, "Online role reading has no countdown");
                    Require(hud.meetingPanel.GetComponentsInChildren<VoteButton>(true).Count(row => row.gameObject.activeSelf) == 25, "Prototype vote roster contains all twenty-five players");
                    Field<Button>(session, "lobbyRoleButton").onClick.Invoke();
                    Invoke(hud.GetComponent<RoleAbilityUI>(), "Update");
                    Require(Field<GameObject>(hud.GetComponent<RoleAbilityUI>(), "revealWindow").activeSelf, "Owner sees role portrait, faction and guide card");
                    ThuongMedievalSceneChecks.SavePreview(Output + "/role-card.png");
                    session.ReadLocalRole();
                    Invoke(session, "RefreshLobbyView");
                    Require(!session.ShowRoleCard && Field<Canvas>(session, "lobbyCanvas").enabled && !session.MatchStarted,
                        "OK dismisses the owner card and returns to lobby without starting gameplay");
                    foreach (var peer in peers.Take(23)) SendAction(peer.Network, 1, 6, -1);
                    stage = 3;
                    next = EditorApplication.timeSinceStartup + .7;
                    break;
                case 3:
                    if (peers.Take(23).Any(peer => !peer.HasRead)) return;
                    Require(!peers.Last().HasRead, "A client can still be reading while others return to lobby");
                    Field<Button>(session, "lobbyReadyButton").onClick.Invoke();
                    foreach (var peer in peers.Take(23)) SendReady(peer.Network);
                    SendAction(peers.Last().Network, 1, 6, -1);
                    Invoke(session, "BroadcastSnapshot");
                    stage = 4;
                    next = EditorApplication.timeSinceStartup + .5;
                    break;
                case 4:
                    if (peers.Any(peer => !peer.HasRead)) return;
                    Require(!session.MatchStarted && GameRoleManager.Instance.currentState == GameState.RoleReveal && !GameRoleManager.Instance.enabled,
                        "Confirming the final role still waits for Ready and host start");
                    Field<Button>(session, "lobbyStartButton").onClick.Invoke();
                    Require(!session.MatchStarted, "Host cannot start while the final player is Not Ready");
                    SendReady(peers.Last().Network);
                    stage = 5;
                    next = EditorApplication.timeSinceStartup + .5;
                    break;
                case 5:
                    if (Field<System.Collections.IEnumerable>(session, "lobby").Cast<object>().Any(member => !Field<bool>(member, "Ready"))) return;
                    Invoke(session, "RefreshLobbyView");
                    Require(Field<RectTransform>(session, "lobbyContent").GetComponentsInChildren<TMP_Text>().Count(label => label.name == "Player Status" && label.text == "READY") == 25,
                        "All twenty-five Ready badges are synchronized");
                    ThuongMedievalSceneChecks.SavePreview(Output + "/lobby-ready.png");
                    Field<Button>(session, "lobbyStartButton").onClick.Invoke();
                    Invoke(session, "RefreshLobbyView");
                    session.RefreshHud(hud);
                    Require(session.MatchStarted && GameRoleManager.Instance.currentState == GameState.Day && hud.GetComponent<Canvas>().enabled && !Field<Canvas>(session, "lobbyCanvas").enabled,
                        "Host Start transitions from ready lobby into gameplay and removes the lobby canvas");
                    Require(assignedRoles.SequenceEqual(RoleManager.Instance.playerRoles.OrderBy(pair => pair.Key).Select(pair => pair.Value)), "Entering gameplay keeps the previously revealed roles");
                    NormalizeRoles();
                    Invoke(session, "SendPrivateState", 0UL, "PRIVATE_HOST_ONLY");
                    Require(session.Feedback == "PRIVATE_HOST_ONLY" && Field<string>(session, "status") != "PRIVATE_HOST_ONLY", "Private ability feedback never enters the public snapshot status");
                    if (ThuongMinigameOnlineChecks.Enabled)
                    {
                        ThuongMinigameOnlineChecks.Begin(session, peers.Select(peer => peer.Network).ToArray());
                        stage = 50;
                        break;
                    }
                    stage = 51;
                    goto case 51;
                case 50:
                    if (!ThuongMinigameOnlineChecks.Tick()) return;
                    stage = 51;
                    goto case 51;
                case 51:
                    GameRoleManager.Instance.EndDay();
                    GameRoleManager.Instance.FinishNight();
                    GameRoleManager.Instance.StartVoting();
                    Invoke(session, "BroadcastSnapshot");
                    Invoke(session, "SendAllPrivateStates");
                    stage = 6;
                    next = EditorApplication.timeSinceStartup + .5;
                    break;
                case 6:
                    Require(session.CanVoteFor(0), "Prototype self-vote rule preserved");
                    session.RequestVote(2);
                    Require(PlayerManager.Instance.GetplayerByID(0).hasVoted, "Host vote reaches authoritative manager");
                    Require(!VoteManager.Instance.TryVote(0, 3), "A second vote is rejected");
                    foreach (var peer in peers) SendAction(peer.Network, 2, 5, 2);
                    stage = 7;
                    next = EditorApplication.timeSinceStartup + .7;
                    break;
                case 7:
                    if (PlayerManager.Instance.players.Any(player => !player.hasVoted)) return;
                    Require(PlayerManager.Instance.players.All(player => player.hasVoted), "Votes received from all twenty-four LAN clients");
                    GameRoleManager.Instance.FinishVoting();
                    Require(VoteManager.Instance.LastResolution.Eliminated?.playerID == 2 && VoteManager.Instance.LastResolution.Tallies[2] == 26,
                        "Weighted tally eliminates the selected player");
                    Invoke(session, "BroadcastSnapshot");
                    stage = 8;
                    next = EditorApplication.timeSinceStartup + .6;
                    break;
                case 8:
                    if (peers.Any(peer => peer.VoteRevision != 1 || peer.VoteTally != 26)) return;
                    Require(hud.meetingPanel.activeSelf && hud.meetingTitle.text.Contains("KẾT QUẢ"), "Vote result survives the immediate transition to next day");
                    Require(hud.meetingPanel.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text.Contains("26 phiếu")), "Result rows show weighted tally");
                    ThuongMedievalSceneChecks.SavePreview(Output + "/vote-result.png");
                    StartVote();
                    Require(VoteManager.Instance.TryVote(1, 3) && VoteManager.Instance.TryVote(4, 5), "Tie votes accepted");
                    GameRoleManager.Instance.FinishVoting();
                    Require(VoteManager.Instance.LastResolution.IsTie && VoteManager.Instance.LastResolution.Eliminated == null, "Tie removes nobody");
                    StartVote();
                    GameRoleManager.Instance.FinishVoting();
                    Require(VoteManager.Instance.LastResolution.Tallies.Count == 0 && VoteManager.Instance.LastResolution.Eliminated == null, "No votes removes nobody");
                    Invoke(session, "BroadcastSnapshot");
                    stage = 9;
                    next = EditorApplication.timeSinceStartup + .5;
                    break;
                case 9:
                    if (peers.Any(peer => peer.VoteRevision != 3 || peer.Result != "Không có phiếu hợp lệ.")) return;
                    if (ThuongOnlineLogicChecks.Enabled)
                    {
                        ThuongOnlineLogicChecks.Begin(session, peers.Select(peer => peer.Network).ToArray());
                        stage = 91;
                        break;
                    }
                    TaskManager.Instance.progress = 100;
                    Invoke(session, "EvaluateOnlineWin", false);
                    Require(GameRoleManager.Instance.currentState == GameState.GameOver, "Completing all tasks ends the authoritative match");
                    Invoke(session, "BroadcastSnapshot");
                    stage = 10;
                    next = EditorApplication.timeSinceStartup + .5;
                    break;
                case 91:
                    if (!ThuongOnlineLogicChecks.Tick()) return;
                    foreach (var peer in peers) peer.Network.Shutdown();
                    stage = 11;
                    next = EditorApplication.timeSinceStartup + .7;
                    break;
                case 10:
                    if (peers.Any(peer => !peer.HasMatchResult)) return;
                    byte villagers = OutcomeValue("Thuong.Online.ThuongWinner", "Villagers");
                    byte tasks = OutcomeValue("Thuong.Online.ThuongWinReason", "Tasks");
                    Require(peers.All(peer => peer.MatchWinner == villagers && peer.MatchReason == tasks &&
                        peer.MatchPlayerCount == 25 && peer.MatchWinnerCount == 24),
                        "All LAN clients receive the task winner, reason and complete per-player win roster");
                    foreach (var peer in peers) peer.Network.Shutdown();
                    stage = 11;
                    next = EditorApplication.timeSinceStartup + .7;
                    break;
                case 11:
                    if (session.GetComponent<NetworkManager>().IsListening) return;
                    Require(!session.MatchStarted && PlayerManager.Instance.players.Count == 0 && !Field<bool>(session, "mapDrawComplete"), "Disconnect clears gameplay and map-draw state");
                    Require((bool)Invoke(session, "SetHostMapSelection", false, "lantern_cemetery", GameEventType.Fog), "Manual selection is available before creating a room");
                    Invoke(session, "HostLan");
                    Require(session.GetComponent<NetworkManager>().IsHost && !session.MatchStarted && Field<bool>(session, "mapDrawComplete"), "Fresh host draws another map after leaving match");
                    Require(session.SelectedMap.mapId == "lantern_cemetery" && session.SelectedMapEvent == GameEventType.Fog, "Creating a manual room retains the configured map and event instead of rerolling");
                    Invoke(session, "LeaveSession");
                    stage = 12;
                    next = EditorApplication.timeSinceStartup + .7;
                    break;
                case 12:
                    if (session.GetComponent<NetworkManager>().IsListening) return;
                    Finish(null);
                    break;
            }
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static bool CheckMapSelectionControls()
    {
        Invoke(session, "RefreshLobbyView");
        switch (mapSettingsStage)
        {
            case 0:
                uint previousEpoch = Field<uint>(session, "mapEpoch");
                var previousMap = session.SelectedMap; var previousEvent = session.SelectedMapEvent;
                Field<Button>(session, "mapSettingsButton").onClick.Invoke();
                Field<Button>(session, "manualMapOption").onClick.Invoke();
                Field<Button>(session, "nextDraftMap").onClick.Invoke();
                ClickMapSettings("Cancel map settings");
                Require(Field<uint>(session, "mapEpoch") == previousEpoch && session.SelectedMap == previousMap && session.SelectedMapEvent == previousEvent &&
                    Field<bool>(session, "randomMapSelection"), "Cancel leaves map, event and selection mode unchanged");
                Field<Button>(session, "mapSettingsButton").onClick.Invoke();
                Field<Button>(session, "manualMapOption").onClick.Invoke();
                Set(session, "draftMapId", "village"); Set(session, "draftMapEvent", GameEventType.None);
                Invoke(session, "RefreshMapSettings");
                Require(Field<Button>(session, "nextDraftMap").interactable && Field<Button>(session, "nextDraftEvent").interactable,
                    "Manual mode enables map and event controls");
                ThuongMedievalSceneChecks.SavePreview(Output + "/map-settings-manual.png");
                ClickMapSettings("Apply map settings");
                Require(!Field<bool>(session, "randomMapSelection") && session.SelectedMap.mapId == "village" && session.SelectedMapEvent == GameEventType.None,
                    "Applying manual normal map uses the selected layout without an event");
                Require(!Field<GameObject>(session, "mapSettingsRoot").activeSelf &&
                    !(bool)typeof(ThuongOnlineSession).GetProperty("AllMapsReady", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session),
                    "Applying settings closes the dialog and requires fresh client acknowledgements");
                mapSettingsStage++;
                return false;
            case 1:
                Require(session.SelectedMap.mapId == "village" && session.SelectedMapEvent == GameEventType.None, "All peers acknowledge the manually selected normal map");
                ThuongMedievalSceneChecks.SavePreview(Output + "/lobby-manual-normal.png");
                Field<Button>(session, "mapSettingsButton").onClick.Invoke();
                Field<Button>(session, "nextDraftMap").onClick.Invoke();
                Field<Button>(session, "nextDraftEvent").onClick.Invoke();
                ClickMapSettings("Apply map settings");
                Require(session.SelectedMap.mapId == "harvest_market" && session.SelectedMapEvent == GameEventType.BloodMoon,
                    "Map and event arrow controls select a specific event map");
                mapSettingsStage++;
                return false;
            case 2:
                Require(session.SelectedMap.mapId == "harvest_market" && session.SelectedMapEvent == GameEventType.BloodMoon,
                    "All peers acknowledge the manually selected event map");
                Field<Button>(session, "mapSettingsButton").onClick.Invoke();
                Field<Button>(session, "randomMapOption").onClick.Invoke();
                Require(!Field<Button>(session, "nextDraftMap").interactable && !Field<Button>(session, "nextDraftEvent").interactable,
                    "Random mode disables manual controls");
                ThuongMedievalSceneChecks.SavePreview(Output + "/map-settings-random.png");
                ClickMapSettings("Apply map settings");
                Require(Field<bool>(session, "randomMapSelection") && mapVariants.Any(v => v.Map == session.SelectedMap && v.EventType == session.SelectedMapEvent),
                    "Switching back to random rolls one valid normal/event map");
                mapSettingsStage++;
                return false;
            default: return true;
        }
    }

    private static void ClickMapSettings(string name) => Field<GameObject>(session, "mapSettingsRoot")
        .GetComponentsInChildren<Button>(true).Single(button => button.name == name).onClick.Invoke();

    private static void NormalizeRoles()
    {
        foreach (var player in PlayerManager.Instance.players)
        {
            player.ResetForNewMatch(); player.roleType = RoleType.Villager; player.faction = FactionType.Villager;
            player.roleDefinition = RoleManager.Instance.roleDefinitions.Find(RoleType.Villager);
            RoleManager.Instance.playerRoles[player.playerID] = new VillagerRole(player);
        }
        // Keep a living threat while this existing scenario exercises voting,
        // otherwise the new online elimination rule correctly ends the match.
        var wolf = PlayerManager.Instance.GetplayerByID(24);
        wolf.roleType = RoleType.DogSpirit; wolf.faction = FactionType.Monster;
        wolf.roleDefinition = RoleManager.Instance.roleDefinitions.Find(RoleType.DogSpirit);
        RoleManager.Instance.playerRoles[wolf.playerID] = new DogSpirit(wolf);
        PlayerManager.Instance.GetplayerByID(0).votePower = 2;
    }

    private static void StartVote()
    {
        GameRoleManager.Instance.EndDay(); GameRoleManager.Instance.FinishNight(); GameRoleManager.Instance.StartVoting();
    }

    private static void AddPeer(int index)
    {
        var root = new GameObject("LAN test client " + index);
        var transport = root.AddComponent<UnityTransport>();
        var network = root.AddComponent<NetworkManager>();
        network.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport, ConnectionApproval = true, EnableSceneManagement = false, TickRate = 30,
            ConnectionData = Encoding.UTF8.GetBytes("THUONG6|Người chơi " + index + "_" + new string('A', 24))
        };
        transport.SetConnectionData("127.0.0.1", Port);
        var peer = new Peer { Network = network, Skin = index % 20 }; peers.Add(peer);
        network.OnClientConnectedCallback += _ =>
        {
            using var hello = new FastBufferWriter(128, Allocator.Temp);
            FixedString64Bytes name = new("Người chơi " + index + "_" + new string('A', 24));
            hello.WriteValueSafe(name); hello.WriteValueSafe(index % 20);
            network.CustomMessagingManager.SendNamedMessage("thuong.online.hello.v1", 0, hello);
            SendReady(network);
        };
        Require(network.StartClient(), "LAN client starts");
        network.CustomMessagingManager.RegisterNamedMessageHandler("thuong.online.roster.v3", (_, reader) =>
        {
            reader.ReadValueSafe(out int capacity); reader.ReadValueSafe(out ushort count);
            Require(capacity == 25, "Lobby capacity synchronized"); peer.RosterCount = count;
        });
        network.CustomMessagingManager.RegisterNamedMessageHandler("thuong.online.private.v3", (_, reader) =>
        {
            reader.ReadValueSafe(out int player); reader.ReadValueSafe(out byte role); reader.ReadValueSafe(out byte faction);
            Require(RoleCatalog.AllConcreteRoles.Contains((RoleType)role), "Private role uses playable implementation");
            peer.PrivatePlayerId = player;
            reader.ReadValueSafe(out ushort flags);
            peer.HasRead = (flags & 2048) != 0;
            peer.HasRole = true;
        });
        network.CustomMessagingManager.RegisterNamedMessageHandler("thuong.online.snapshot.v6", (_, reader) => ReadSnapshot(peer, reader));
    }

    private static void SendReady(NetworkManager network)
    {
        using var writer = new FastBufferWriter(4, Allocator.Temp);
        writer.WriteValueSafe(true);
        network.CustomMessagingManager.SendNamedMessage("thuong.online.ready.v1", 0, writer);
    }

    private static void SendAction(NetworkManager network, uint id, byte action, int target)
    {
        using var writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe(id); writer.WriteValueSafe((uint)typeof(ThuongOnlineSession).GetField("matchEpoch", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session)); writer.WriteValueSafe(action); writer.WriteValueSafe(target); writer.WriteValueSafe(-1);
        writer.WriteValueSafe(GameRoleManager.Instance.currentDay); writer.WriteValueSafe((byte)GameRoleManager.Instance.currentPhase);
        network.CustomMessagingManager.SendNamedMessage("thuong.online.action.v3", 0, writer);
    }

    private static void ReadSnapshot(Peer peer, FastBufferReader reader)
    {
        reader.ReadValueSafe(out uint revision);
        Require(revision > peer.Revision, "Snapshot revision always increases across lobby and gameplay"); peer.Revision = revision;
        reader.ReadValueSafe(out peer.Epoch);
        reader.ReadValueSafe(out peer.Started); reader.ReadValueSafe(out peer.RolesAssigned);
        reader.ReadValueSafe(out bool eliminationWin);
        reader.ReadValueSafe(out int parityMinimum); reader.ReadValueSafe(out int villageLimit); reader.ReadValueSafe(out int maxDay);
        reader.ReadValueSafe(out FixedString64Bytes mapId); reader.ReadValueSafe(out int mapRevision);
        reader.ReadValueSafe(out uint mapEpoch); reader.ReadValueSafe(out int eventRevision);
        reader.ReadValueSafe(out byte mapEvent); reader.ReadValueSafe(out int eventDay);
        var definition=Resources.Load<Thuong.Maps.ThuongMapCatalog>(Thuong.Maps.ThuongMapCatalog.ResourcePath).Find(mapId.ToString());
        Require(eventRevision == ThuongEventPresentationCatalog.ContentRevision && definition!=null && mapRevision == definition.contentRevision,"Map/event content versions synchronized");
        Require(mapEvent <= 6 && (mapEvent == 0 ? eventDay == 0 : eventDay > 0),"Snapshot exposes only current public event");
        peer.MapId = mapId.ToString();
        peer.MapEvent = (GameEventType)mapEvent;
        if (peer.MapEpoch != mapEpoch)
        {
            peer.MapEpoch = mapEpoch;
            using var ack = new FastBufferWriter(100,Allocator.Temp);
            ack.WriteValueSafe(mapId); ack.WriteValueSafe(mapRevision); ack.WriteValueSafe(mapEpoch); ack.WriteValueSafe(eventRevision); ack.WriteValueSafe(true);
            peer.Network.CustomMessagingManager.SendNamedMessage("thuong.online.map-ready.v1",0,ack);
        }
        reader.ReadValueSafe(out byte state); reader.ReadValueSafe(out byte phase);
        reader.ReadValueSafe(out int day);
        for (int i = 0; i < 3; i++) reader.ReadValueSafe(out float value);
        reader.ReadValueSafe(out ushort active); reader.ReadValueSafe(out ushort complete);
        reader.ReadValueSafe(out FixedString64Bytes winner); reader.ReadValueSafe(out FixedString128Bytes status);
        reader.ReadValueSafe(out ushort count); peer.PublicCount = count;
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out ulong client); reader.ReadValueSafe(out int player);
            if (client == peer.Network.LocalClientId) peer.PublicLocalPlayerId = player;
            reader.ReadValueSafe(out FixedString64Bytes name); reader.ReadValueSafe(out int skin);
            reader.ReadValueSafe(out bool alive); reader.ReadValueSafe(out float x); reader.ReadValueSafe(out float y);
        }
        reader.ReadValueSafe(out peer.VoteRevision); reader.ReadValueSafe(out int voteDay);
        reader.ReadValueSafe(out int eliminated); reader.ReadValueSafe(out bool tie);
        reader.ReadValueSafe(out FixedString512Bytes message); peer.Result = message.ToString();
        reader.ReadValueSafe(out ushort tallyCount); peer.VoteTally = 0;
        for (int i = 0; i < tallyCount; i++)
        {
            reader.ReadValueSafe(out int player); reader.ReadValueSafe(out int tally); peer.VoteTally += tally;
        }
        reader.ReadValueSafe(out bool hasResult);
        peer.HasMatchResult = hasResult;
        if (hasResult)
        {
            reader.ReadValueSafe(out peer.MatchWinner); reader.ReadValueSafe(out peer.MatchReason);
            reader.ReadValueSafe(out int resultDay); reader.ReadValueSafe(out byte resultPhase);
            reader.ReadValueSafe(out FixedString64Bytes neutralLabel);
            reader.ReadValueSafe(out ushort finalCount); peer.MatchPlayerCount = finalCount; peer.MatchWinnerCount = 0;
            for (int i = 0; i < finalCount; i++)
            {
                reader.ReadValueSafe(out int id); reader.ReadValueSafe(out FixedString64Bytes finalName);
                reader.ReadValueSafe(out int character); reader.ReadValueSafe(out byte role); reader.ReadValueSafe(out byte faction);
                reader.ReadValueSafe(out bool alive); reader.ReadValueSafe(out bool awakened); reader.ReadValueSafe(out bool won);
                if (won) peer.MatchWinnerCount++;
                reader.ReadValueSafe(out bool hasDeathRecord); reader.ReadValueSafe(out byte cause); reader.ReadValueSafe(out int source);
            }
        }
        Require(reader.Position == reader.Length, "Full fragmented snapshot decodes without missing or extra fields");
    }

    private static void Finish(Exception exception)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Key, false);
        EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + "OriginalOptions", false);
        string result = exception == null ? $"THUONG_ONLINE_UI_FORM_PASSED: {checks} checks; 25 avatar/name/status rows, assign roles, open role card without a countdown, OK to ready lobby, host start, voting/results and rehost."
            : "THUONG_ONLINE_UI_FORM_FAILED: " + exception;
        File.WriteAllText(Output + "/result.txt", result);
        if (exception == null) Debug.Log(result); else Debug.LogError(result);
        exitCode = exception == null ? 0 : 1;
        cleanupDeadline = EditorApplication.timeSinceStartup + 8;
        foreach (var peer in peers) if (peer.Network != null && peer.Network.IsListening) peer.Network.Shutdown();
        if (session != null && session.GetComponent<NetworkManager>().IsListening) session.GetComponent<NetworkManager>().Shutdown();
        EditorApplication.update += CompleteShutdown;
    }

    private static byte OutcomeValue(string typeName, string name) => Convert.ToByte(Enum.Parse(
        typeof(WinConditionManager).Assembly.GetType(typeName, true), name));

    private static void CompleteShutdown()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        bool stopped = peers.All(peer => peer.Network == null || !peer.Network.IsListening) &&
            (session == null || !session.GetComponent<NetworkManager>().IsListening);
        if (!stopped && EditorApplication.timeSinceStartup < cleanupDeadline) return;
        EditorApplication.update -= CompleteShutdown;
        EditorApplication.Exit(exitCode);
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static object Invoke(object target, string name, params object[] values) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, values);
    private static void Require(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
}
