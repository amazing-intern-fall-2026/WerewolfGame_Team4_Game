using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Thuong;
using Thuong.Emotes;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Thuong.Online
{
    [DefaultExecutionOrder(-9000)]
    public sealed partial class ThuongOnlineSession : MonoBehaviour
    {
        private const int MaxPlayers = 25;
        private const float MoveSpeed = 3f;
        private const float InputInterval = 1f / 20f;
        private const float SnapshotInterval = 1f / 10f;
        private const float PrivateInterval = 0.5f;
        private const int WindowId = 0x54484F4E;

        private readonly SortedDictionary<ulong, ThuongOnlineServerPlayer> serverPlayers = new();
        private readonly List<ThuongOnlineLobbyMember> lobby = new();
        private readonly List<ThuongOnlinePublicPlayer> publicPlayers = new();
        private readonly Dictionary<int, ThuongOnlineAvatar> avatars = new();
        private readonly Dictionary<int, string> privateFeedback = new();

        private NetworkManager network;
        private UnityTransport transport;
        private ISession relaySession;
        private GameRoleManager game;
        private PlayerManager players;
        private RoleManager roles;
        private TaskManager tasks;
        private TaskStation[] stations = Array.Empty<TaskStation>();
        private GameObject playerTemplate;
        private ThuongTopDownCamera worldCamera;
        private ThuongMedievalCast characterCast;

        private bool handlersRegistered;
        private bool busy;
        private bool connected;
        private bool rosterDirty;
        private bool roomLocked;
        private bool matchStarted;
        private bool rolesAssigned;
        private bool leavingScene;
        private float nextInputTime;
        private float nextSnapshotTime;
        private float nextPrivateTime;
        private ushort inputSequence;
        private uint requestId = 1;
        private uint snapshotRevision;
        private int localPlayerId = -1;
        private int selectedCharacterIndex;
        private ushort activeTaskMask;
        private ushort completedTaskMask;
        private GameState visibleState = GameState.RoleReveal;
        private GamePhase visiblePhase = GamePhase.Setup;
        private int visibleDay;
        private float visibleRemaining;
        private float visibleProgress;
        private float visibleDailyProgress;
        private string visibleWinner = string.Empty;
        private RoleType privateRole;
        private FactionType privateFaction;
        private ushort privateFlags;

        private string playerName;
        private string address = "127.0.0.1";
        private string portText = "7777";
        private string joinCode = string.Empty;
        private string sessionCode = string.Empty;
        private string status = "Chọn Host hoặc Join để bắt đầu.";
        private Rect windowRect = new(0, 0, 520, 500);
        private int lobbyCapacity = MaxPlayers;

        private void Awake()
        {
            Active = this;
            network = GetComponent<NetworkManager>();
            IsMainFlowSession = network == null && ThuongMainFlow.CanAdopt(NetworkManager.Singleton);
            if (IsMainFlowSession) network = NetworkManager.Singleton;
            transport = network.GetComponent<UnityTransport>();
            previousApproval = network.ConnectionApprovalCallback;
            reconnectToken = IsMainFlowSession ? ThuongMainFlow.ReconnectToken : Guid.Empty;
            playerName = IsMainFlowSession ? ThuongMainFlow.PlayerName(network.LocalClientId) : AuthManager.Instance.IsSignedIn ? SanitizeName(AuthManager.Instance.CurrentUsername) :
                "Player " + UnityEngine.Random.Range(100, 999);
            PrepareScene();

            network.ConnectionApprovalCallback = ApproveConnection;
            network.OnServerStarted += HandleServerStarted;
            network.OnClientConnectedCallback += HandleClientConnected;
            network.OnClientDisconnectCallback += HandleClientDisconnected;
            network.OnClientStopped += HandleNetworkStopped;
            network.OnServerStopped += HandleNetworkStopped;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        private void PrepareScene()
        {
            game = SceneComponent<GameRoleManager>();
            players = SceneComponent<PlayerManager>();
            roles = SceneComponent<RoleManager>();
            tasks = SceneComponent<TaskManager>();
            worldCamera = SceneComponent<ThuongTopDownCamera>();
            PrepareOnlineLogic();

            if (game != null)
            {
                game.manualLogicPrototype = true;
                game.enabled = false;
            }

            if (players != null)
            {
                players.prototypeLobbySize = 0;
                players.players ??= new List<PlayerData>();
                players.players.Clear();
            }

            foreach (PrototypeControls controls in SceneComponents<PrototypeControls>())
                controls.enabled = false;
            foreach (ThuongFourRoleOfflineTest test in SceneComponents<ThuongFourRoleOfflineTest>())
                test.enabled = false;
            PreparePresentation();
            PrepareOnlineWinRules();
            PrepareMinigames();
            PrepareOnlineMaps();
            PrepareVillage();
            PrepareEmotes();
            PrepareTaskGuidance();
            PrepareCommunications();
            foreach (TaskStation station in stations)
            {
                station.enabled = false;
                if (station.marker != null && station.marker != station.gameObject)
                    station.marker.SetActive(false);
            }

            ThuongTopDownCharacter character = SceneComponent<ThuongTopDownCharacter>();
            if (character != null)
            {
                characterCast = character.cast;
                PlayerMovement movement = character.GetComponentInParent<PlayerMovement>();
                playerTemplate = movement != null ? movement.gameObject : character.transform.parent.gameObject;
                playerTemplate.SetActive(false);
            }
        }

        private void Update()
        {
            UpdateMainFlowHandshake();
            UpdateCommunications();
            RefreshLobbyView();
            UpdateAvatars();
            UpdateMinigames();
            UpdateVillage();
            RefreshMapPresentation();
            RefreshTaskGuidance();
            UpdateTutorialInput();
            if (!connected || network == null || !network.IsListening) return;

            if (network.IsServer)
            {
                if (rosterDirty) BroadcastRoster();
                if (matchStarted) SimulateServerMovement();
                if (Time.unscaledTime >= nextSnapshotTime)
                {
                    nextSnapshotTime = Time.unscaledTime + SnapshotInterval;
                    ExpireReconnects();
                    BroadcastSnapshot();
                    BroadcastRoomState();
                }
                if (rolesAssigned && Time.unscaledTime >= nextPrivateTime)
                {
                    nextPrivateTime = Time.unscaledTime + PrivateInterval;
                    SendAllPrivateStates();
                }
            }

            if (matchStarted && visibleState != GameState.GameOver && Time.unscaledTime >= nextInputTime)
            {
                nextInputTime = Time.unscaledTime + InputInterval;
                SendMovementInput(ReadMovementInput());
            }

            if (NetworkChatUI.Instance?.IsInputFocused != true)
                ProcessContextInteractionInput();
        }

        private Vector2 ReadMovementInput()
        {
            bool movablePhase = visibleState == GameState.Day ||
                roomRules.mode == ThuongMatchMode.RealtimeProximity && visibleState == GameState.Night && visiblePhase == GamePhase.Night;
            if (Keyboard.current == null || !movablePhase || !LocalPlayerAlive || IsPlayingTask || NotebookIsOpen || TutorialIsOpen || VillagePanelOpen || InteractionMenuOpen || !localMapReady ||
                PlayerEmoteWheelUI.IsOpenFor(this) || NetworkChatUI.Instance?.IsInputFocused == true) return Vector2.zero;
            float x = 0f;
            float y = 0f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        private void SendMovementInput(Vector2 input)
        {
            inputSequence++;
            if (network.IsServer && network.LocalClientId == NetworkManager.ServerClientId)
            {
                ApplyServerInput(network.LocalClientId, inputSequence, input);
                return;
            }

            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(inputSequence);
            writer.WriteValueSafe(input.x);
            writer.WriteValueSafe(input.y);
            network.CustomMessagingManager.SendNamedMessage(
                ThuongOnlineMessages.Input, NetworkManager.ServerClientId, writer,
                NetworkDelivery.UnreliableSequenced);
        }

        private void SimulateServerMovement()
        {
            bool canMove = game != null && (game.currentState == GameState.Day ||
                roomRules.mode == ThuongMatchMode.RealtimeProximity && game.currentState == GameState.Night && game.currentPhase == GamePhase.Night);
            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            foreach (ThuongOnlineServerPlayer member in serverPlayers.Values)
            {
                PlayerData player = players?.GetplayerByID(member.PlayerId);
                Vector2 input = canMove && !minigameAuthority.IsPlaying(member.ClientId) && player != null && player.isAlive &&
                                Time.unscaledTime - member.LastInputTime <= 0.35f
                    ? member.Input : Vector2.zero;
                if (member.MapReady && localMapReady && SelectedMap != null)
                    member.Position = Thuong.Maps.ThuongMapCollision.Move(SelectedMap, member.Position,
                        Vector2.ClampMagnitude(input, 1f) * MoveSpeed * delta, VillageBarrier);
            }
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            string payload = request.Payload != null ? Encoding.UTF8.GetString(request.Payload) : "";
            if (!TryReadConnectionPayload(payload, out Guid token, out bool resume, out string requestedName))
            {
                response.Approved = false;
                response.Reason = "Khác phiên bản game. Host và người chơi cần cùng bản Thương Online mới.";
                response.Pending = false;
                return;
            }
            if (!localMapReady)
            {
                response.Approved = false; response.Reason = mapError; response.Pending = false; return;
            }
            if (resume)
            {
                response.Approved = TryReserveReconnect(request.ClientNetworkId, token, Time.realtimeSinceStartupAsDouble);
                response.CreatePlayerObject = false;
                response.Pending = false;
                if (!response.Approved) response.Reason = "Không còn chỗ kết nối lại hoặc chỗ đang được dùng.";
                return;
            }
            if (roomLocked)
            {
                response.Approved = false;
                response.Reason = "Phòng đã chốt danh sách và phân vai.";
                response.Pending = false;
                return;
            }
            if (serverPlayers.Count >= lobbyCapacity && !serverPlayers.ContainsKey(request.ClientNetworkId))
            {
                response.Approved = false;
                response.Reason = "Phòng đã đầy.";
                response.Pending = false;
                return;
            }
            if (serverPlayers.Values.Any(member => member.ClientId != request.ClientNetworkId && member.ReconnectToken == token))
            {
                response.Approved = false;
                response.Reason = "Phiên người chơi đang được sử dụng.";
                response.Pending = false;
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = false;
            response.Pending = false;
            if (!serverPlayers.ContainsKey(request.ClientNetworkId))
            {
                serverPlayers.Add(request.ClientNetworkId, new ThuongOnlineServerPlayer
                {
                    ClientId = request.ClientNetworkId,
                    ReconnectToken = token,
                    PlayerId = NextAvailablePlayerId(),
                    Name = SanitizeName(requestedName),
                    CharacterIndex = 0,
                    Position = SpawnPosition(serverPlayers.Count)
                });
                rosterDirty = true;
            }
        }

        private int NextAvailablePlayerId()
        {
            int id = 0;
            while (serverPlayers.Values.Any(value => value.PlayerId == id)) id++;
            return id;
        }

        private Vector2 SpawnPosition(int index)
        {
            if (SelectedMap != null) return SelectedMap.spawns[Mathf.Abs(index) % SelectedMap.spawns.Length];
            Vector2 origin = playerTemplate != null ? playerTemplate.transform.position : Vector2.zero;
            return origin + new Vector2((index % 4 - 1.5f) * 1.25f, -(index / 4) * 1.25f);
        }

        private void HandleServerStarted()
        {
            RegisterMessageHandlers();
            if (!EnsureRoomMapSelection()) { network.Shutdown(); return; }
            status = "Server đã sẵn sàng.";
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (network.IsServer && reconnectApprovals.Remove(clientId, out var returning) &&
                !AttachReturningClient(clientId, returning, Time.realtimeSinceStartupAsDouble))
            {
                network.DisconnectClient(clientId, "Hết thời gian kết nối lại.");
                return;
            }
            if (network.IsServer && !serverPlayers.ContainsKey(clientId))
            {
                if (clientId != network.LocalClientId)
                {
                    network.DisconnectClient(clientId, "Không tìm thấy chỗ trong phòng.");
                    return;
                }
                serverPlayers.Add(clientId, new ThuongOnlineServerPlayer
                {
                    ClientId = clientId,
                    PlayerId = NextAvailablePlayerId(),
                    Name = "Player " + (serverPlayers.Count + 1),
                    CharacterIndex = 0,
                    Position = SpawnPosition(serverPlayers.Count)
                });
            }

            if (clientId == network.LocalClientId)
            {
                if (network.IsServer && serverPlayers.TryGetValue(clientId, out var localMember)) localMember.MapReady = localMapReady;
                bool resumed = reconnectOfferUntil > 0;
                hostSessionTerminated = false;
                connected = true;
                busy = relayOperation;
                reconnectOfferUntil = 0;
                RegisterMessageHandlers();
                SendHello();
                status = network.IsHost ? "Đã tạo phòng." : resumed ? "Đã kết nối lại. Đang đồng bộ ván…" : "Đã tham gia phòng.";
            }

            if (network.IsServer) rosterDirty = true;
            if (network.IsServer && journalPublished) SendJournalTo(clientId);
            if (network.IsServer && IsMainFlowSession) SendMainFlowTicket(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (leavingScene || network == null || explicitLeave) return;
            if (network.IsServer)
            {
                if (!network.ShutdownInProgress) HandleServerPeerLost(clientId);
                return;
            }

            if (clientId == network.LocalClientId || clientId == NetworkManager.ServerClientId)
            {
                OfferReconnect();
                connected = false;
                matchStarted = matchResult != null;
                busy = relayOperation;
                status = matchResult != null ? "Đã ngắt kết nối. Kết quả ván vẫn được giữ lại." :
                    !string.IsNullOrEmpty(network.DisconnectReason) ? network.DisconnectReason : "Đã mất kết nối với chủ phòng.";
            }
        }

        private void HandleNetworkStopped(bool wasServer)
        {
            if (leavingScene || explicitLeave) return;
            if (!wasServer) OfferReconnect();
            ClearMapPresentation();
            ResetMinigames();
            UnregisterMessageHandlers();
            connected = false;
            busy = relayOperation;
            // Peers can read their final result even after the host leaves.
            // LeaveSession explicitly clears it when the user exits this screen.
            if (matchResult != null)
            {
                matchStarted = true;
                return;
            }
            roomLocked = false;
            matchStarted = false;
            sessionCode = string.Empty;
            ClearClientState(reconnectOfferUntil > Time.realtimeSinceStartupAsDouble && !hostSessionTerminated);
        }

        private void RegisterMessageHandlers()
        {
            if (handlersRegistered || network.CustomMessagingManager == null) return;
            handlersRegistered = true;
            var messaging = network.CustomMessagingManager;
            messaging.RegisterNamedMessageHandler(ThuongOnlineMessages.Hello, ReceiveHello);
            messaging.RegisterNamedMessageHandler(ThuongOnlineMessages.Ready, ReceiveReady);
            messaging.RegisterNamedMessageHandler(ThuongOnlineMessages.Input, ReceiveInput);
            messaging.RegisterNamedMessageHandler(ThuongOnlineMessages.Action, ReceiveAction);
            messaging.RegisterNamedMessageHandler(ThuongOnlineMessages.Roster, ReceiveRoster);
            messaging.RegisterNamedMessageHandler(ThuongOnlineMessages.Snapshot, ReceiveSnapshot);
            messaging.RegisterNamedMessageHandler(ThuongOnlineMessages.PrivateState, ReceivePrivateState);
            messaging.RegisterNamedMessageHandler(LeaveMessage, ReceiveVoluntaryLeave);
            RegisterMinigameMessages();
            RegisterEmoteMessages();
            RegisterRoomMessages();
            RegisterMainFlowMessages();
            RegisterCommunicationMessages();
            RegisterVillageMessages();
            messaging.RegisterNamedMessageHandler(MapReadyMessage, ReceiveMapAcknowledgement);
        }

        private void UnregisterMessageHandlers()
        {
            if (!handlersRegistered) return;
            var messaging = network != null ? network.CustomMessagingManager : null;
            if (messaging != null)
            {
                UnregisterMinigameMessages();
                UnregisterEmoteMessages();
                UnregisterRoomMessages();
                UnregisterMainFlowMessages();
                UnregisterCommunicationMessages();
                UnregisterVillageMessages();
                messaging.UnregisterNamedMessageHandler(MapReadyMessage);
                messaging.UnregisterNamedMessageHandler(ThuongOnlineMessages.Hello);
                messaging.UnregisterNamedMessageHandler(ThuongOnlineMessages.Ready);
                messaging.UnregisterNamedMessageHandler(ThuongOnlineMessages.Input);
                messaging.UnregisterNamedMessageHandler(ThuongOnlineMessages.Action);
                messaging.UnregisterNamedMessageHandler(ThuongOnlineMessages.Roster);
                messaging.UnregisterNamedMessageHandler(ThuongOnlineMessages.Snapshot);
                messaging.UnregisterNamedMessageHandler(ThuongOnlineMessages.PrivateState);
                messaging.UnregisterNamedMessageHandler(LeaveMessage);
            }
            handlersRegistered = false;
        }

        private async void HandleActiveSceneChanged(Scene previous, Scene current)
        {
            if (current.name == ThuongOnlineBootstrap.SceneName) return;
            if (leavingScene) return;
            leavingScene = true;
            // Stop this scene's overlays and hooks before an asynchronous Relay
            // leave, so a newly loaded scene immediately uses its own gameplay.
            enabled = false;
            ResetEmotes();
            UnregisterMessageHandlers();
            if (game != null) game.enabled = false;
            if (DayTimer.Instance != null && DayTimer.Instance.gameObject.scene == previous) DayTimer.Instance.StopTimer();
            ResetMinigames();
            if (Active == this) Active = null;
            ClearMapPresentation();
            mapRuntime?.Clear();
            foreach (var avatar in avatars.Values)
                if (avatar.Root != null) { avatar.Root.SetActive(false); Destroy(avatar.Root); }
            avatars.Clear();
            SetGameplayHudVisible(false);
            ResetEndgamePresentation();
            if (lobbyCanvas != null) lobbyCanvas.gameObject.SetActive(false);
            RemoveOnlineWinRules();
            RestoreOtherGameOverManager();
            DisposeCommunications();
            if (IsMainFlowSession) return;
            try
            {
                if (relaySession != null)
                {
                    await relaySession.LeaveAsync();
                    relaySession = null;
                }
                else if (network != null && network.IsListening)
                {
                    network.Shutdown();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Thuong Online] Không thể đóng session khi rời scene: " + exception.Message);
            }
            if (network != null && network.IsListening && !network.ShutdownInProgress) network.Shutdown();
            double shutdownDeadline = Time.realtimeSinceStartupAsDouble + 5;
            while (network != null && (network.IsListening || network.ShutdownInProgress) && Time.realtimeSinceStartupAsDouble < shutdownDeadline)
                await Task.Yield();
            if (this != null) Destroy(gameObject);
        }

        private void SendHello()
        {
            if (network.IsServer && network.LocalClientId == NetworkManager.ServerClientId)
            {
                UpdateServerIdentity(network.LocalClientId, playerName, selectedCharacterIndex);
                return;
            }
            using var writer = new FastBufferWriter(128, Allocator.Temp);
            FixedString64Bytes value = Fixed64(playerName);
            writer.WriteValueSafe(value);
            writer.WriteValueSafe(selectedCharacterIndex);
            network.CustomMessagingManager.SendNamedMessage(
                ThuongOnlineMessages.Hello, NetworkManager.ServerClientId, writer);
        }

        private void ReceiveHello(ulong sender, FastBufferReader reader)
        {
            if (!network.IsServer) return;
            try
            {
                reader.ReadValueSafe(out FixedString64Bytes value);
                reader.ReadValueSafe(out int characterIndex);
                UpdateServerIdentity(sender, value.ToString(), characterIndex);
                if (IsMainFlowSession) SendMainFlowTicket(sender);
            }
            catch (Exception error) when (error is OverflowException || error is ArgumentException)
            { network.DisconnectClient(sender, "Gói kết nối không hợp lệ hoặc khác phiên bản."); }
        }

        private void UpdateServerIdentity(ulong clientId, string value, int characterIndex)
        {
            if (roomLocked || !serverPlayers.TryGetValue(clientId, out ThuongOnlineServerPlayer member)) return;
            member.Name = SanitizeName(value);
            member.CharacterIndex = ClampCharacterIndex(characterIndex);
            rosterDirty = true;
        }

        private void ToggleReady()
        {
            if (!CanToggleLobbyReady) return;
            bool current = lobby.FirstOrDefault(item => item.ClientId == network.LocalClientId)?.Ready ?? false;
            bool next = !current;
            if (network.IsServer && network.LocalClientId == NetworkManager.ServerClientId)
            {
                SetServerReady(network.LocalClientId, next);
                return;
            }
            using var writer = new FastBufferWriter(4, Allocator.Temp);
            writer.WriteValueSafe(next);
            network.CustomMessagingManager.SendNamedMessage(
                ThuongOnlineMessages.Ready, NetworkManager.ServerClientId, writer);
        }

        private void ReceiveReady(ulong sender, FastBufferReader reader)
        {
            if (!network.IsServer) return;
            reader.ReadValueSafe(out bool ready);
            SetServerReady(sender, ready);
        }

        private void SetServerReady(ulong clientId, bool ready)
        {
            if (matchStarted || !rolesAssigned || !serverPlayers.TryGetValue(clientId, out ThuongOnlineServerPlayer member) || member.Offline) return;
            if (ready && !member.MapReady)
            { Reply(member, "Chưa thể Ready: map/revision chưa tải xong."); return; }
            // Ready is also an explicit acknowledgement of the assigned role.
            // This covers a delayed OK acknowledgement.
            if (ready) member.RevealRead = true;
            member.Ready = ready;
            rosterDirty = true;
            Reply(member, ready ? "Đã Ready. Đang chờ chủ phòng bắt đầu game." : "Đã hủy Ready.");
            BroadcastRoster();
        }

        private void ReceiveInput(ulong sender, FastBufferReader reader)
        {
            if (!network.IsServer) return;
            reader.ReadValueSafe(out ushort sequence);
            reader.ReadValueSafe(out float x);
            reader.ReadValueSafe(out float y);
            ApplyServerInput(sender, sequence, new Vector2(x, y));
        }

        private void ApplyServerInput(ulong sender, ushort sequence, Vector2 input)
        {
            if (!matchStarted || matchResult != null || !serverPlayers.TryGetValue(sender, out ThuongOnlineServerPlayer member) ||
                member.Offline || !member.MapReady) return;
            if (float.IsNaN(input.x) || float.IsInfinity(input.x) ||
                float.IsNaN(input.y) || float.IsInfinity(input.y)) return;
            if (sequence == member.LastInputSequence && member.LastInputTime > 0f) return;
            member.LastInputSequence = sequence;
            member.Input = Vector2.ClampMagnitude(input, 1f);
            if (member.Input.sqrMagnitude > .0001f) village.Release(member.PlayerId);
            member.LastInputTime = Time.unscaledTime;
        }

        private void BroadcastRoster()
        {
            if (!network.IsServer || network.ConnectedClientsIds.Count == 0) return;
            rosterDirty = false;
            if (network.IsHost)
            {
                lobby.Clear();
                foreach (ThuongOnlineServerPlayer member in serverPlayers.Values)
                {
                    lobby.Add(new ThuongOnlineLobbyMember
                    {
                        ClientId = member.ClientId,
                        PlayerId = member.PlayerId,
                        Name = member.Name,
                        CharacterIndex = member.CharacterIndex,
                        Ready = member.Ready,
                        MapReady = member.MapReady,
                        Offline = member.Offline
                    });
                    if (member.ClientId == network.LocalClientId) localPlayerId = member.PlayerId;
                }
            }
            using var writer = new FastBufferWriter(4096, Allocator.Temp);
            writer.WriteValueSafe(lobbyCapacity);
            writer.WriteValueSafe((ushort)serverPlayers.Count);
            foreach (ThuongOnlineServerPlayer member in serverPlayers.Values)
            {
                writer.WriteValueSafe(member.ClientId);
                writer.WriteValueSafe(member.PlayerId);
                FixedString64Bytes name = Fixed64(member.Name);
                writer.WriteValueSafe(name);
                writer.WriteValueSafe(member.CharacterIndex);
                writer.WriteValueSafe(member.Ready);
                writer.WriteValueSafe(member.MapReady);
                writer.WriteValueSafe(member.Offline);
            }
            network.CustomMessagingManager.SendNamedMessage(
                ThuongOnlineMessages.Roster, network.ConnectedClientsIds, writer,
                NetworkDelivery.ReliableFragmentedSequenced);
        }

        private void ReceiveRoster(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || network.IsServer) return;
            reader.ReadValueSafe(out lobbyCapacity);
            reader.ReadValueSafe(out ushort count);
            lobby.Clear();
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong clientId);
                reader.ReadValueSafe(out int playerId);
                reader.ReadValueSafe(out FixedString64Bytes name);
                reader.ReadValueSafe(out int characterIndex);
                reader.ReadValueSafe(out bool ready);
                reader.ReadValueSafe(out bool mapReady);
                reader.ReadValueSafe(out bool offline);
                lobby.Add(new ThuongOnlineLobbyMember
                {
                    ClientId = clientId,
                    PlayerId = playerId,
                    Name = name.ToString(),
                    CharacterIndex = characterIndex,
                    Ready = ready, MapReady = mapReady, Offline = offline
                });
                if (clientId == network.LocalClientId) localPlayerId = playerId;
            }
        }

        private void AssignLobbyRolesOnServer()
        {
            if (!network.IsServer || roomLocked || serverPlayers.Count < 2) return;
            if (!AllMapsReady) { status = "Đang chờ mọi người xác nhận map/event cùng phiên bản."; return; }
            if (scopedWinManager == null || !scopedWinManager.HasMatchEvaluator(game))
            {
                status = "Không tìm thấy bộ luật thắng / thua của Thương Online.";
                return;
            }
            if (serverPlayers.Keys.Any(id => !network.ConnectedClientsIds.Contains(id))) return;
            if (players == null || game == null || roles == null || tasks == null)
            {
                status = "Scene thiếu gameplay manager của Thương.";
                return;
            }

            var configuration = NetworkGameRoleSetup.HasSetup() ? NetworkGameRoleSetup.GetRoles() :
                ThuongOnlineRoleConfigAdapter.DefaultRolesForRoom(serverPlayers.Count, roomRules)
                    .Select(entry => new RoleAmountData(entry.Key, entry.Value)).ToList();
            if (!RolePoolBuilder.TryBuildConfigured(serverPlayers.Count, configuration, ConfigurableRoleTypes,
                out var selectedPool, out string roleError))
            { status = roleError; return; }

            roomLocked = true;
            matchEpoch = IsMainFlowSession ? ThuongMainFlow.NextMatchEpoch(matchEpoch) : checked(matchEpoch + 1);
            rolesAssigned = true;
            ResetVillageRound();
            matchStarted = false;
            ResetCorpseState();
            localRoleCardOpen = false;
            localRoleConfirmed = false;
            players.prototypeLobbySize = 0;
            players.players.Clear();
            roles.playerRoles.Clear();
            privateFeedback.Clear();
            ResetVotePresentation();
            ObserveVoteResults();

            int index = 0;
            foreach (ThuongOnlineServerPlayer member in serverPlayers.Values)
            {
                member.Position = SpawnPosition(index++);
                member.Input = Vector2.zero;
                member.RevealRead = false;
                member.Ready = false;
                var player = new PlayerData
                {
                    playerID = member.PlayerId,
                    playerName = member.Name,
                    isAlive = true,
                    votePower = 1,
                    status = new PlayerStatus()
                };
                players.RegisterPlayer(player);
            }

            game.manualLogicPrototype = false;
            // Assign the real roles now, then hold the simulation until the lobby is ready.
            game.enabled = false;
            ConfigureMapEventForMatch();
            ApplyRoomRulesForMatch();
            roles.rolePoolOverride = roomRules.AllowedRoles;
            roles.exactRolePoolOverride = selectedPool;
            try { game.BeginGame(); }
            finally { roles.rolePoolOverride = null; roles.exactRolePoolOverride = null; }
            if (roles.playerRoles.Count != serverPlayers.Count)
            {
                status = "Không thể phân đủ vai trò. Hãy rời phòng và tạo lại.";
                network.Shutdown();
                return;
            }
            status = "Đã phân vai. Bấm Xem phân vai để đọc role, không giới hạn thời gian.";
            foreach (var assigned in players.players)
                AddJournal($"{assigned.DisplayName} nhận vai {assigned.roleType}.");
            BroadcastRoster();
            SendAllPrivateStates();
            BroadcastSnapshot();
        }

        private void StartMatchOnServer()
        {
            if (!network.IsServer || !rolesAssigned || matchStarted || serverPlayers.Count < 2 ||
                !AllMapsReady || serverPlayers.Values.Any(member => !member.RevealRead || !member.Ready)) return;
            matchStarted = true;
            village.Reset(VillageNow);
            game.SkipRoleReveal();
            AddJournal("Ván chơi bắt đầu.");
            game.enabled = true;
            status = "Trận đã bắt đầu.";
            privateFeedback.Clear();
            localActionFeedback = string.Empty;
            SendAllPrivateStates();
            BroadcastSnapshot();
        }

        private void BroadcastSnapshot()
        {
            if (leavingScene || network == null || network.CustomMessagingManager == null || network.ShutdownInProgress ||
                !network.IsListening || !network.IsServer || network.ConnectedClientsIds.Count == 0) return;
            snapshotRevision++;

            GameState state = game != null ? game.currentState : GameState.RoleReveal;
            GamePhase phase = game != null ? game.currentPhase : GamePhase.Setup;
            if (visiblePhase != phase)
            {
                privateFeedback.Clear();
                localActionFeedback = string.Empty;
            }
            int day = game != null ? game.currentDay : 0;
            float remaining = game != null && game.currentState == GameState.Day && DayTimer.Instance != null
                ? DayTimer.Instance.TimeRemaining : game != null ? game.PhaseTimeRemaining : 0f;
            if (rolesAssigned && !matchStarted) remaining = 0f;
            float progress = tasks != null ? tasks.progress : 0f;
            float dailyProgress = tasks != null ? tasks.DailyProgress : 0f;
            string winner = game != null ? game.Winner : string.Empty;
            BuildTaskMasks(out activeTaskMask, out completedTaskMask);
            if (network.IsHost)
                ApplyHostSnapshot(state, phase, day, remaining, progress, dailyProgress, winner);

            foreach (var recipient in serverPlayers.Values)
            {
            if (recipient.Offline || recipient.ClientId == network.LocalClientId || !network.ConnectedClientsIds.Contains(recipient.ClientId)) continue;
            using var writer = new FastBufferWriter(8192, Allocator.Temp);
            writer.WriteValueSafe(snapshotRevision);
            writer.WriteValueSafe(matchEpoch);
            writer.WriteValueSafe(matchStarted);
            writer.WriteValueSafe(rolesAssigned);
            writer.WriteValueSafe((byte)roomRules.mode);
            WriteWinSettings(writer);
            WriteMapState(writer, day);
            writer.WriteValueSafe((byte)state);
            writer.WriteValueSafe((byte)phase);
            writer.WriteValueSafe(day);
            writer.WriteValueSafe(remaining);
            writer.WriteValueSafe(progress);
            writer.WriteValueSafe(dailyProgress);
            writer.WriteValueSafe(activeTaskMask);
            writer.WriteValueSafe(completedTaskMask);
            FixedString64Bytes winnerValue = Fixed64(winner);
            writer.WriteValueSafe(winnerValue);
            FixedString128Bytes statusValue = Fixed128(status);
            writer.WriteValueSafe(statusValue);
            writer.WriteValueSafe((ushort)serverPlayers.Count);
            foreach (ThuongOnlineServerPlayer member in serverPlayers.Values)
            {
                PlayerData player = players?.GetplayerByID(member.PlayerId);
                writer.WriteValueSafe(member.ClientId);
                writer.WriteValueSafe(member.PlayerId);
                FixedString64Bytes name = Fixed64(member.Name);
                writer.WriteValueSafe(name);
                writer.WriteValueSafe(member.CharacterIndex);
                writer.WriteValueSafe(player == null || player.isAlive);
                bool hidden = !CanObserveVillagePosition(recipient.PlayerId, member.Position, member.PlayerId);
                writer.WriteValueSafe(hidden);
                writer.WriteValueSafe(hidden ? 0 : member.Position.x);
                writer.WriteValueSafe(hidden ? 0 : member.Position.y);
            }
            WriteCorpseSnapshot(writer, recipient.PlayerId);
            WriteVoteResult(writer);
            WriteMatchResult(writer);
            network.CustomMessagingManager.SendNamedMessage(
                ThuongOnlineMessages.Snapshot, recipient.ClientId, writer,
                NetworkDelivery.ReliableFragmentedSequenced);
            }
            if (network.IsHost) SyncCorpseMarkers(onlineCorpses.Where(corpse => CanObserveVillagePosition(localPlayerId, corpse.Position)).ToArray());
        }

        private void ApplyHostSnapshot(GameState state, GamePhase phase, int day, float remaining,
            float progress, float dailyProgress, string winner)
        {
            visibleState = state;
            visiblePhase = phase;
            visibleDay = day;
            visibleRemaining = remaining;
            visibleProgress = progress;
            visibleDailyProgress = dailyProgress;
            visibleWinner = winner ?? string.Empty;
            publicPlayers.Clear();
            foreach (ThuongOnlineServerPlayer member in serverPlayers.Values)
            {
                PlayerData player = players?.GetplayerByID(member.PlayerId);
                publicPlayers.Add(new ThuongOnlinePublicPlayer
                {
                    ClientId = member.ClientId,
                    PlayerId = member.PlayerId,
                    Name = member.Name,
                    CharacterIndex = member.CharacterIndex,
                    Alive = player == null || player.isAlive,
                    Hidden = !CanObserveVillagePosition(network.IsHost ? localPlayerId : -1, member.Position, member.PlayerId),
                    Position = CanObserveVillagePosition(localPlayerId, member.Position, member.PlayerId) ? member.Position : Vector2.zero
                });
            }
            SyncAvatars();
            RefreshStationMarkers();
        }

        private void BuildTaskMasks(out ushort active, out ushort completed)
        {
            active = 0;
            completed = 0;
            if (tasks == null) return;
            for (int i = 0; i < stations.Length && i < 16; i++)
            {
                TaskData task = stations[i].task;
                if (task == null) continue;
                ushort bit = (ushort)(1 << i);
                if (tasks.currentTasks.Contains(task)) active |= bit;
                if (tasks.IsCompleted(task)) completed |= bit;
            }
        }

        private void ReceiveSnapshot(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || network.IsServer) return;
            reader.ReadValueSafe(out uint revision);
            if (revision <= snapshotRevision) return;
            snapshotRevision = revision;
            reader.ReadValueSafe(out uint epoch);
            if (epoch != matchEpoch) ResetReplicatedRound(epoch);
            reader.ReadValueSafe(out matchStarted);
            reader.ReadValueSafe(out rolesAssigned);
            reader.ReadValueSafe(out byte mode);
            if (!Enum.IsDefined(typeof(ThuongMatchMode), (ThuongMatchMode)mode)) return;
            roomRules.mode = (ThuongMatchMode)mode;
            clientHadRoles = rolesAssigned;
            ReadWinSettings(reader);
            ReadMapState(reader);
            reader.ReadValueSafe(out byte state);
            reader.ReadValueSafe(out byte phase);
            if (visiblePhase != (GamePhase)phase) localActionFeedback = string.Empty;
            reader.ReadValueSafe(out visibleDay);
            reader.ReadValueSafe(out visibleRemaining);
            reader.ReadValueSafe(out visibleProgress);
            reader.ReadValueSafe(out visibleDailyProgress);
            reader.ReadValueSafe(out activeTaskMask);
            reader.ReadValueSafe(out completedTaskMask);
            reader.ReadValueSafe(out FixedString64Bytes winner);
            reader.ReadValueSafe(out FixedString128Bytes serverStatus);
            visibleState = (GameState)state;
            visiblePhase = (GamePhase)phase;
            visibleWinner = winner.ToString();
            string receivedStatus = serverStatus.ToString();
            if (!string.IsNullOrEmpty(receivedStatus)) status = receivedStatus;

            reader.ReadValueSafe(out ushort count);
            publicPlayers.Clear();
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong clientId);
                reader.ReadValueSafe(out int playerId);
                reader.ReadValueSafe(out FixedString64Bytes name);
                reader.ReadValueSafe(out int characterIndex);
                reader.ReadValueSafe(out bool alive);
                reader.ReadValueSafe(out bool hidden);
                reader.ReadValueSafe(out float x);
                reader.ReadValueSafe(out float y);
                publicPlayers.Add(new ThuongOnlinePublicPlayer
                {
                    ClientId = clientId,
                    PlayerId = playerId,
                    Name = name.ToString(),
                    CharacterIndex = characterIndex,
                    Alive = alive,
                    Hidden = hidden,
                    Position = new Vector2(x, y)
                });
                if (clientId == network.LocalClientId) localPlayerId = playerId;
            }

            if (!ReadCorpseSnapshot(reader)) return;
            ReadVoteResult(reader);
            ReadMatchResult(reader);
            if (!localMapReady) status = mapError;
            RefreshMapPresentation();

            SyncClientPlayerData();
            SyncAvatars();
            RefreshStationMarkers();
        }

        private void SyncClientPlayerData()
        {
            if (players == null || network.IsServer) return;
            players.players ??= new List<PlayerData>();
            players.players.RemoveAll(player => player == null ||
                publicPlayers.All(item => item.PlayerId != player.playerID));
            foreach (ThuongOnlinePublicPlayer state in publicPlayers)
            {
                PlayerData player = players.GetplayerByID(state.PlayerId);
                if (player == null)
                {
                    player = new PlayerData
                    {
                        playerID = state.PlayerId,
                        status = new PlayerStatus()
                    };
                    players.RegisterPlayer(player);
                }
                player.playerName = state.Name;
                player.isAlive = state.Alive;
            }
            ApplyLocalPrivateData();
        }

        private void SendAllPrivateStates()
        {
            if (!network.IsServer || !rolesAssigned || players == null) return;
            foreach (ThuongOnlineServerPlayer member in serverPlayers.Values)
                SendPrivateState(member.ClientId, privateFeedback.TryGetValue(member.PlayerId, out string feedback)
                    ? feedback : string.Empty);
        }

        private void SendPrivateState(ulong clientId, string feedback)
        {
            if (!serverPlayers.TryGetValue(clientId, out ThuongOnlineServerPlayer member) || member.Offline ||
                !network.ConnectedClientsIds.Contains(clientId)) return;
            PlayerData player = players?.GetplayerByID(member.PlayerId);
            if (player == null) return;
            ushort flags = 0;
            if (player.isAlive) flags |= 1;
            if (player.hasVoted) flags |= 2;
            bool configured = RoleManager.UsesConfiguredAbility(player);
            if (configured ? game.abilities.ValidateActor(player).FailureReason == ActionFailureReason.AlreadyUsed : player.hasUseNightAction) flags |= 4;
            if (player.CanVote) flags |= 32;
            if (player.HasEffect(StatusEffectType.Silenced)) flags |= 64;
            if (player.isWhiteHoundAwakened) flags |= 128;
            if (member.RevealRead) flags |= 2048;
            if (configured && player.roleDefinition.abilityType != AbilityType.None) flags |= 8;
            if (RoleManager.Instance != null && RoleManager.Instance.playerRoles.TryGetValue(player.playerID, out BaseRole role))
            {
                if (!configured && (role.HasNightAbility || role is WeaverOfFateRole)) flags |= 8;
                if (role is HunterRole && !player.isAlive && player.hasHunterTrap) flags |= 16;
                if (role is ShamanRole shaman)
                {
                    if (shaman.HasGoodCharm) flags |= 256;
                    if (shaman.HasBadCharm) flags |= 512;
                }
                if (role is WeaverOfFateRole weaver && weaver.HasUsedAbility) flags |= 1024;
            }
            int votedTarget = VoteManager.Instance != null ? VoteManager.Instance.GetVotedTarget(player.playerID) : -1;
            int[] allowedTargets = AllowedTargets(player);

            if (network.IsHost && clientId == network.LocalClientId)
            {
                localPlayerId = member.PlayerId;
                privateRole = player.roleType;
                privateFaction = player.faction;
                privateFlags = flags;
                CaptureLocalLogic(player, allowedTargets);
                privateStateReceived = true;
                localVotedTarget = votedTarget;
                localActionFeedback = feedback ?? string.Empty;
            }

            using var writer = new FastBufferWriter(4096, Allocator.Temp);
            writer.WriteValueSafe(member.PlayerId);
            writer.WriteValueSafe((byte)player.roleType);
            writer.WriteValueSafe((byte)player.faction);
            writer.WriteValueSafe(flags);
            writer.WriteValueSafe(votedTarget);
            FixedString512Bytes text = Fixed512(feedback);
            writer.WriteValueSafe(text);
            WritePrivateLogic(writer, player, allowedTargets);
            network.CustomMessagingManager.SendNamedMessage(
                ThuongOnlineMessages.PrivateState, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        private void ReceivePrivateState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || network.IsServer) return;
            reader.ReadValueSafe(out int playerId);
            reader.ReadValueSafe(out byte role);
            reader.ReadValueSafe(out byte faction);
            reader.ReadValueSafe(out ushort flags);
            reader.ReadValueSafe(out int votedTarget);
            reader.ReadValueSafe(out FixedString512Bytes feedback);
            if (!ReadPrivateLogic(reader)) return;
            privateFlags = flags; localVotedTarget = votedTarget;
            localPlayerId = playerId;
            privateRole = (RoleType)role;
            privateFaction = (FactionType)faction;
            privateStateReceived = true;
            string message = feedback.ToString();
            localActionFeedback = message;
            ApplyLocalPrivateData();
        }

        private void RequestNearestTask()
        {
            if (IsPlayingTask || localPlayerId < 0 || !LocalPlayerAlive || visibleState != GameState.Day ||
                !avatars.TryGetValue(localPlayerId, out ThuongOnlineAvatar avatar)) return;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < stations.Length && i < 16; i++)
            {
                ushort bit = (ushort)(1 << i);
                if ((activeTaskMask & bit) == 0 || (completedTaskMask & bit) != 0 || (personalTaskMask & bit) != 0) continue;
                float distance = Vector2.Distance(avatar.Root.transform.position, stations[i].transform.position);
                if (distance < bestDistance) { best = i; bestDistance = distance; }
            }
            if (best < 0 || bestDistance > stations[best].interactionRadius + 0.25f)
            {
                status = "Không có nhiệm vụ khả dụng ở gần.";
                return;
            }
            RequestTaskMinigame(best);
        }

        private void SendAction(ThuongOnlineAction action, int argument, int secondArgument = -1)
        {
            if (!connected || !rolesAssigned) return;
            uint id = requestId++;
            if (network.IsServer && network.LocalClientId == NetworkManager.ServerClientId)
            {
                ApplyServerAction(network.LocalClientId, id, action, argument, secondArgument, visibleDay, visiblePhase, matchEpoch);
                return;
            }
            using var writer = new FastBufferWriter(32, Allocator.Temp);
            writer.WriteValueSafe(id);
            writer.WriteValueSafe(matchEpoch);
            writer.WriteValueSafe((byte)action);
            writer.WriteValueSafe(argument);
            writer.WriteValueSafe(secondArgument);
            writer.WriteValueSafe(visibleDay);
            writer.WriteValueSafe((byte)visiblePhase);
            network.CustomMessagingManager.SendNamedMessage(
                ThuongOnlineMessages.Action, NetworkManager.ServerClientId, writer);
        }

        private void ReceiveAction(ulong sender, FastBufferReader reader)
        {
            if (!network.IsServer || !reader.TryBeginRead(sizeof(uint) * 2 + sizeof(byte) * 2 + sizeof(int) * 3)) return;
            reader.ReadValueSafe(out uint id);
            reader.ReadValueSafe(out uint epoch);
            reader.ReadValueSafe(out byte action);
            reader.ReadValueSafe(out int argument);
            reader.ReadValueSafe(out int secondArgument);
            reader.ReadValueSafe(out int day);
            reader.ReadValueSafe(out byte phase);
            ApplyServerAction(sender, id, (ThuongOnlineAction)action, argument, secondArgument, day, (GamePhase)phase, epoch);
        }

        private void ApplyServerAction(ulong sender, uint id, ThuongOnlineAction action, int argument,
            int secondArgument, int day, GamePhase phase, uint epoch)
        {
            if (!rolesAssigned || !serverPlayers.TryGetValue(sender, out ThuongOnlineServerPlayer member) ||
                member.Offline || !member.MapReady) return;
            if (epoch != matchEpoch) { Reply(member, "Thao tác thuộc ván cũ. Hãy chọn lại."); return; }
            if (matchResult != null || game != null && game.currentState == GameState.GameOver)
            { Reply(member, "Ván đã kết thúc. Không thể thực hiện thêm thao tác."); return; }
            if (id <= member.LastRequestId) return;
            double actionNow = VillageNow;
            if (actionNow - member.ActionWindow >= 1) { member.ActionWindow = actionNow; member.ActionsInWindow = 0; }
            if (++member.ActionsInWindow > 30) return;
            member.LastRequestId = id;
            // Reading a role belongs to the lobby. Its acknowledgement must not
            // depend on gameplay phase/day values from a preceding snapshot.
            if (action == ThuongOnlineAction.RevealRead)
            {
                if (!matchStarted && game != null && game.currentState == GameState.RoleReveal)
                {
                    member.RevealRead = true;
                    Reply(member, "Đã đọc vai trò. Bấm Ready trong lobby khi bạn sẵn sàng.");
                }
                return;
            }
            if (!matchStarted)
            { Reply(member, "Ván chưa bắt đầu. Hãy bấm Ready trong lobby."); return; }
            if (game == null || day != game.currentDay || phase != game.currentPhase)
            { Reply(member, "Giai đoạn đã thay đổi. Hãy chọn lại thao tác."); return; }
            PlayerData actor = players?.GetplayerByID(member.PlayerId);
            if (actor == null) { Reply(member, "Không tìm thấy người chơi."); return; }

            if (roomRules.mode == ThuongMatchMode.RealtimeProximity &&
                action is ThuongOnlineAction.MakeLovers or ThuongOnlineAction.NightAbility or
                    ThuongOnlineAction.ShamanGoodCharm or ThuongOnlineAction.ShamanBadCharm)
            { Reply(member, "Mode tương tác gần dùng phím E; kỹ năng chọn người toàn map đã tắt."); return; }

            int journalInsertAt = matchJournal.Count;
            int journalDay = game.currentDay;
            GamePhase journalPhase = game.currentPhase;
            bool accepted = false;
            string feedback = string.Empty;
            switch (action)
            {
                case ThuongOnlineAction.MakeLovers:
                    accepted = TryMakeLovers(actor, argument, secondArgument, out feedback);
                    break;
                case ThuongOnlineAction.CompleteTask:
                    accepted = TryCompleteTask(member, actor, argument, out feedback);
                    break;
                case ThuongOnlineAction.NightAbility:
                    if (actor.roleType == RoleType.Brat && roles != null &&
                        roles.playerRoles.TryGetValue(actor.playerID, out BaseRole bratBaseRole) &&
                        bratBaseRole is BratRole brat)
                        accepted = brat.TryPeek(out feedback);
                    else
                        accepted = RoleManager.Instance != null &&
                                   RoleManager.Instance.UseNightAbility(actor.playerID, argument, out feedback);
                    break;
                case ThuongOnlineAction.ShamanGoodCharm:
                case ThuongOnlineAction.ShamanBadCharm:
                    accepted = TryUseShaman(actor, argument,
                        action == ThuongOnlineAction.ShamanGoodCharm, out feedback);
                    break;
                case ThuongOnlineAction.HunterTrap:
                    accepted = TrySetHunterTrap(actor, argument, out feedback);
                    break;
                case ThuongOnlineAction.Vote:
                    var vote = VoteManager.Instance != null ? VoteManager.Instance.ValidateVote(actor.playerID, argument)
                        : ActionValidationResult.Fail(ActionFailureReason.MatchNotRunning, "Chưa có hệ thống bỏ phiếu.");
                    accepted = vote.IsValid && VoteManager.Instance.TryVote(actor.playerID, argument);
                    feedback = accepted ? "Đã ghi nhận phiếu bầu." : vote.Message;
                    break;
                case ThuongOnlineAction.NoEliminationVote:
                    var noEliminationVote = VoteManager.Instance != null
                        ? VoteManager.Instance.ValidateNoEliminationVote(actor.playerID)
                        : ActionValidationResult.Fail(ActionFailureReason.MatchNotRunning, "Chưa có hệ thống bỏ phiếu.");
                    accepted = noEliminationVote.IsValid && VoteManager.Instance.TryVoteNoElimination(actor.playerID);
                    feedback = accepted ? "Đã ghi nhận lựa chọn không loại ai." : noEliminationVote.Message;
                    break;
                case ThuongOnlineAction.RealtimeAbility:
                    accepted = TryRealtimeAbility(member, actor, argument, out feedback);
                    break;
                case ThuongOnlineAction.ReportCorpse:
                    accepted = TryReportCorpse(member, actor, argument, out feedback);
                    break;
                case ThuongOnlineAction.EmergencyMeeting:
                case ThuongOnlineAction.Sabotage:
                case ThuongOnlineAction.RepairSabotage:
                case ThuongOnlineAction.PairConsole:
                case ThuongOnlineAction.UseItem:
                    accepted = TryVillageAction(member, action, argument, secondArgument, out feedback);
                    break;
                default:
                    feedback = "Yêu cầu không được hỗ trợ.";
                    break;
            }

            if (!accepted && string.IsNullOrWhiteSpace(feedback)) feedback = "Yêu cầu bị từ chối.";
            if (accepted) RecordJournalAction(actor, action, argument, secondArgument, journalDay, journalPhase, journalInsertAt);
            Reply(member, feedback);
            nextSnapshotTime = 0;
        }

        private bool TryCompleteTask(ThuongOnlineServerPlayer member, PlayerData actor, int stationId,
            out string feedback)
        {
            if (stationId < 0 || stationId >= stations.Length || stationId >= 16 || tasks == null)
            { feedback = "Trạm nhiệm vụ không hợp lệ."; return false; }
            feedback = "Nhiệm vụ online chỉ được hoàn thành qua minigame.";
            return false;
        }

        private static bool TryUseShaman(PlayerData actor, int targetId, bool good, out string feedback)
        {
            if (RoleManager.Instance == null ||
                !RoleManager.Instance.playerRoles.TryGetValue(actor.playerID, out BaseRole baseRole) ||
                baseRole is not ShamanRole shaman || !shaman.SelectCharm(good))
            { feedback = "Bùa này không khả dụng."; return false; }
            return RoleManager.Instance.UseNightAbility(actor.playerID, targetId, out feedback);
        }

        private static bool TrySetHunterTrap(PlayerData actor, int targetId, out string feedback)
        {
            if (GameRoleManager.Instance != null && GameRoleManager.Instance.EnableHunterRevengeWindow)
                return GameRoleManager.Instance.TryShootHunter(actor.playerID, targetId, out feedback);
            if (GameRoleManager.Instance == null || GameRoleManager.Instance.currentState != GameState.Discussion ||
                RoleManager.Instance == null ||
                !RoleManager.Instance.playerRoles.TryGetValue(actor.playerID, out BaseRole baseRole) ||
                baseRole is not HunterRole hunter)
            { feedback = "Bẫy Thợ săn không khả dụng."; return false; }
            return hunter.TrySetTrap(targetId, out feedback);
        }

        private void Reply(ThuongOnlineServerPlayer member, string feedback)
        {
            privateFeedback[member.PlayerId] = feedback;
            SendPrivateState(member.ClientId, feedback);
        }

        private void SyncAvatars()
        {
            var activeIds = new HashSet<int>(publicPlayers.Select(item => item.PlayerId));
            foreach (int staleId in avatars.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            {
                if (avatars[staleId].Root != null) Destroy(avatars[staleId].Root);
                avatars.Remove(staleId);
            }

            foreach (ThuongOnlinePublicPlayer state in publicPlayers)
            {
                EnsureAvatar(state);
                if (avatars.TryGetValue(state.PlayerId, out ThuongOnlineAvatar avatar))
                {
                    avatar.Root.SetActive(!state.Hidden);
                    avatar.TargetPosition = state.Position;
                    if (avatar.CharacterIndex != state.CharacterIndex)
                    {
                        avatar.Root.GetComponentInChildren<ThuongTopDownCharacter>(true)?.SetSkin(state.CharacterIndex);
                        avatar.CharacterIndex = state.CharacterIndex;
                    }
                    if (state.PlayerId == localPlayerId)
                    {
                        if (worldCamera != null && worldCamera.target != avatar.Root.transform)
                            worldCamera.Follow(avatar.Root.transform);
                        avatar.Root.GetComponentInChildren<ThuongTopDownCharacter>(true)?.SetSelected(true);
                    }
                    var data = players?.GetplayerByID(state.PlayerId);
                    if (data != null) PlayerNameTag.AttachOrCreate(avatar.Root.transform, data);
                }
            }
        }

        private void EnsureAvatar(ThuongOnlinePublicPlayer state)
        {
            if (avatars.ContainsKey(state.PlayerId) || playerTemplate == null) return;
            GameObject clone = Instantiate(playerTemplate, state.Position, Quaternion.identity);
            clone.name = "OnlinePlayer_" + state.PlayerId;
            PlayerMovement movement = clone.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.playerID = state.PlayerId;
                movement.enabled = false;
            }
            PlayerInput input = clone.GetComponent<PlayerInput>();
            if (input != null) input.enabled = false;
            Rigidbody2D body = clone.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.bodyType = RigidbodyType2D.Kinematic;
                body.simulated = false;
            }
            clone.SetActive(true);
            ThuongTopDownCharacter character = clone.GetComponentInChildren<ThuongTopDownCharacter>(true);
            character?.SetSkin(state.CharacterIndex);
            var avatar = new ThuongOnlineAvatar
            {
                Root = clone,
                Animator = clone.GetComponentInChildren<Animator>(true),
                TargetPosition = state.Position,
                PreviousPosition = state.Position,
                CharacterIndex = state.CharacterIndex
            };
            avatars[state.PlayerId] = avatar;
            if (state.PlayerId == localPlayerId) worldCamera?.Follow(clone.transform);
        }

        private void UpdateAvatars()
        {
            float factor = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
            foreach (ThuongOnlineAvatar avatar in avatars.Values)
            {
                if (avatar.Root == null) continue;
                Vector2 previous = avatar.Root.transform.position;
                Vector2 next = Vector2.Lerp(previous, avatar.TargetPosition, factor);
                avatar.Root.transform.position = new Vector3(next.x, next.y, avatar.Root.transform.position.z);
                bool walking = Vector2.Distance(next, avatar.PreviousPosition) > 0.001f;
                if (avatar.Animator != null && avatar.Animator.runtimeAnimatorController != null)
                {
                    avatar.Animator.SetBool("isWalking", walking);
                    Vector2 direction = next - avatar.PreviousPosition;
                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        avatar.Animator.SetFloat("LastInputX", direction.x);
                        avatar.Animator.SetFloat("LastInputY", direction.y);
                    }
                }
                avatar.PreviousPosition = next;
            }
        }

        private void RefreshStationMarkers()
        {
            for (int i = 0; i < stations.Length && i < 16; i++)
            {
                if (stations[i].marker == null || stations[i].marker == stations[i].gameObject) continue;
                ushort bit = (ushort)(1 << i);
                stations[i].marker.SetActive(matchStarted && visibleState == GameState.Day &&
                    (activeTaskMask & bit) != 0 && (completedTaskMask & bit) == 0);
            }
        }

        private async void HostRelay()
        {
            if (busy || network.IsListening || network.ShutdownInProgress) return;
            busy = relayOperation = true;
            status = "Đang tạo phòng Relay...";
            try
            {
                PrepareConnectionData(false);
                await EnsureUnityServices();
                var options = new SessionOptions
                {
                    Name = "Thuong Werewolf " + DateTime.UtcNow.ToString("HHmmss"),
                    MaxPlayers = MaxPlayers,
                    IsPrivate = true
                }.WithRelayNetwork();
                relaySession = await MultiplayerService.Instance.CreateSessionAsync(options);
                sessionCode = relaySession.Code;
                status = "Đã tạo phòng Relay. Mã: " + sessionCode;
            }
            catch (Exception exception)
            {
                status = "Không tạo được Relay: " + exception.Message;
            }
            finally { busy = relayOperation = false; }
        }

        private void JoinRelay() => _ = JoinRelayConnection(false);

        private async Task JoinRelayConnection(bool resume)
        {
            if (busy || network.IsListening || network.ShutdownInProgress || string.IsNullOrWhiteSpace(joinCode)) return;
            busy = relayOperation = true;
            status = resume ? "Đang kết nối lại qua Relay…" : "Đang tham gia Relay...";
            try
            {
                if (resume) await CloseRelayMembership();
                PrepareConnectionData(resume);
                clientUsedRelay = true;
                await EnsureUnityServices();
                relaySession = await MultiplayerService.Instance.JoinSessionByCodeAsync(
                    joinCode.Trim().ToUpperInvariant());
                sessionCode = relaySession.Code;
            }
            catch (Exception exception)
            {
                status = "Không tham gia được Relay: " + (string.IsNullOrEmpty(network.DisconnectReason) ? exception.Message : network.DisconnectReason);
            }
            finally { busy = relayOperation = false; }
        }

        private static async Task EnsureUnityServices()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions();
                options.SetProfile("thuong-" + Guid.NewGuid().ToString("N").Substring(0, 10));
                await UnityServices.InitializeAsync(options);
            }
            else if (UnityServices.State == ServicesInitializationState.Initializing)
            {
                while (UnityServices.State == ServicesInitializationState.Initializing)
                    await Task.Yield();
            }
            if (UnityServices.State != ServicesInitializationState.Initialized)
                throw new InvalidOperationException("Unity Services chưa khởi tạo thành công.");
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private void HostLan()
        {
            if (busy || network.IsListening || network.ShutdownInProgress || !TryGetPort(out ushort port)) return;
            busy = true;
            status = "Đang mở LAN Host...";
            PrepareConnectionData(false);
            transport.SetConnectionData(address.Trim(), port, "0.0.0.0");
            if (!network.StartHost())
            {
                busy = false;
                status = "Không thể mở LAN Host.";
            }
        }

        private void JoinLan() => StartLanClient(false);

        private void StartLanClient(bool resume)
        {
            if (busy || network.IsListening || network.ShutdownInProgress || !TryGetPort(out ushort port)) return;
            busy = true;
            status = resume ? "Đang kết nối lại qua LAN…" : "Đang kết nối LAN...";
            PrepareConnectionData(resume);
            clientUsedRelay = false;
            transport.SetConnectionData(address.Trim(), port);
            if (!network.StartClient())
            {
                busy = false;
                status = "Không thể khởi động LAN Client.";
            }
        }

        private void PrepareConnectionData(bool resume)
        {
            playerName = SanitizeName(playerName);
            if (!resume)
            {
                reconnectToken = Guid.NewGuid();
                reconnectOfferUntil = 0;
                clientHadRoles = false;
            }
            network.NetworkConfig.ConnectionData = ConnectionPayload(reconnectToken, playerName, resume);
        }

        private bool TryGetPort(out ushort port)
        {
            if (ushort.TryParse(portText, out port) && port > 0) return true;
            status = "Port không hợp lệ.";
            return false;
        }

        private async void LeaveSession()
        {
            if (IsMainFlowSession) { LeaveMainFlowRoom(); return; }
            if (busy) return;
            busy = explicitLeave = true;
            reconnectOfferUntil = 0;
            clientHadRoles = false;
            status = "Đang rời phòng...";
            try
            {
                NotifyVoluntaryLeave();
                if (network.IsServer) NetworkReturnToMenuController.BroadcastHostSessionTerminated(network, matchEpoch);
                await Task.Delay(200);
                if (network.IsListening && !network.ShutdownInProgress) network.Shutdown();
                while (network.IsListening || network.ShutdownInProgress) await Task.Yield();
                UnregisterMessageHandlers();
                await CloseRelayMembership();
                connected = false;
                status = "Đã rời phòng.";
                ClearClientState();
            }
            catch (Exception exception)
            {
                status = "Không thể rời phòng: " + exception.Message;
                connected = false;
                ClearClientState();
            }
            finally { busy = explicitLeave = false; }
        }

        private void ClearClientState(bool preserveNotebook = false)
        {
            ResetCommunications();
            ResetEmotes();
            if (preserveNotebook) SuspendNotebook();
            else ResetNotebook();
            ResetVillageView(); ResetJournal();
            ResetCorpseState();
            CloseTutorial();
            ClearMapPresentation();
            ResetMinigames();
            ResetTaskBoard();
            lobby.Clear();
            publicPlayers.Clear();
            serverPlayers.Clear();
            reconnectApprovals.Clear();
            foreach (ThuongOnlineAvatar avatar in avatars.Values)
                if (avatar.Root != null) Destroy(avatar.Root);
            avatars.Clear();
            localPlayerId = -1;
            snapshotRevision = 0;
            matchStarted = roomLocked = false;
            rolesAssigned = false;
            localRoleCardOpen = false;
            localRoleConfirmed = false;
            privateStateReceived = false;
            privateFlags = 0;
            matchEpoch = privateMatchEpoch = 0;
            roomRules = new ThuongRoomRules(); rulesRevision = 1; receivedRulesRevision = 0;
            localEffects.Clear(); localAllowedTargets.Clear();
            localVotedTarget = -1;
            inputSequence = 0;
            requestId = 1;
            activeTaskMask = completedTaskMask = 0;
            privateFeedback.Clear();
            localActionFeedback = string.Empty;
            matchResult = null;
            ResetEndgamePresentation();
            ResetVotePresentation();
            if (game != null) game.ResetForOnlineLobby();
            if (roles != null) roles.playerRoles.Clear();
            if (players != null) players.players.Clear();
            if (tasks != null) tasks.ResetForNewMatch();
            RefreshStationMarkers();
        }

        private void OnGUI()
        {
            if (leavingScene || matchStarted || connected) return;

            float width = Mathf.Min(520f, Screen.width - 24f);
            float height = Mathf.Min(connected ? 630f : 680f, Screen.height - 24f);
            windowRect = new Rect(
                Mathf.Max(12f, (Screen.width - width) * 0.5f),
                Mathf.Max(12f, (Screen.height - height) * 0.5f),
                width,
                height);
            windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "THƯƠNG ONLINE");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(status, GUILayout.MinHeight(38));

            if (reconnectOfferUntil > 0 || relaySession != null)
            {
                double remaining = reconnectOfferUntil - Time.realtimeSinceStartupAsDouble;
                if (reconnectOfferUntil > 0)
                    GUILayout.Label(remaining > 0 ? $"Thử kết nối lại trong {Math.Ceiling(remaining)} giây." : "Đã hết thời gian thử kết nối lại.");
                GUI.enabled = !busy && !network.IsListening && !network.ShutdownInProgress && remaining > 0;
                if (GUILayout.Button("Kết nối lại")) RetryConnection();
                GUI.enabled = !busy;
                if (GUILayout.Button("Rời phòng")) LeaveSession();
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Space(8);
                GUILayout.Label("Tên người chơi");
                playerName = GUILayout.TextField(playerName, 24);
                DrawLobbyCapacity(!busy);
                DrawMapSelector();
                DrawCharacterSelector(true);
                GUILayout.Space(4);
                GUILayout.Label("LAN Address / Port");
                GUILayout.BeginHorizontal();
                address = GUILayout.TextField(address, 40);
                portText = GUILayout.TextField(portText, 5, GUILayout.Width(70));
                GUILayout.EndHorizontal();
                bool hasName = !string.IsNullOrWhiteSpace(playerName);
                bool validPort = ushort.TryParse(portText, out ushort portValue) && portValue > 0;
                bool hasLanData = hasName && !string.IsNullOrWhiteSpace(address) && validPort;
                GUI.enabled = !busy && hasLanData;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Host LAN")) HostLan();
                if (GUILayout.Button("Join LAN")) JoinLan();
                GUILayout.EndHorizontal();
                GUILayout.Space(8);
                GUI.enabled = !busy && hasName;
                if (GUILayout.Button("Host Relay")) HostRelay();
                GUILayout.Label("Mã Relay");
                joinCode = GUILayout.TextField(joinCode, 12).ToUpperInvariant();
                GUI.enabled = !busy && hasName && !string.IsNullOrWhiteSpace(joinCode);
                if (GUILayout.Button("Join Relay")) JoinRelay();
                GUI.enabled = true;

                GUILayout.FlexibleSpace();
                GUILayout.Label("Nhập đủ thông tin rồi chọn Host hoặc Join.");
                GUI.enabled = !busy && !network.IsListening && !network.ShutdownInProgress;
                if (Application.CanStreamedLevelBeLoaded(ThuongOnlineMenu.MainMenuName) && GUILayout.Button("Về menu chính"))
                {
                    leavingScene = true;
                    ThuongOnlineMenu.ReturnToMenu(this);
                }
                GUI.enabled = true;
            }
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, windowRect.width, 24));
        }

        private void DrawCharacterSelector(bool canChange)
        {
            int count = characterCast != null ? characterCast.Count : 0;
            if (count <= 0)
            {
                GUILayout.Label("Không tìm thấy bộ nhân vật của Thương.");
                return;
            }

            selectedCharacterIndex = ClampCharacterIndex(selectedCharacterIndex);
            GUILayout.Space(8);
            GUILayout.Label("Chọn nhân vật có sẵn");

            Rect previewArea = GUILayoutUtility.GetRect(96f, 104f, GUILayout.ExpandWidth(true));
            GUI.Box(previewArea, GUIContent.none);
            Sprite sprite = characterCast.idleSprites[selectedCharacterIndex];
            if (sprite != null && sprite.texture != null)
            {
                Rect spriteRect = sprite.rect;
                Rect uv = new Rect(
                    spriteRect.x / sprite.texture.width,
                    spriteRect.y / sprite.texture.height,
                    spriteRect.width / sprite.texture.width,
                    spriteRect.height / sprite.texture.height);
                float side = Mathf.Min(88f, previewArea.height - 8f);
                Rect destination = new Rect(
                    previewArea.center.x - side * 0.5f,
                    previewArea.center.y - side * 0.5f,
                    side,
                    side);
                GUI.DrawTextureWithTexCoords(destination, sprite.texture, uv, true);
            }

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && canChange;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< Nhân vật trước")) SelectCharacter(selectedCharacterIndex - 1);
            GUILayout.Label($"Nhân vật {selectedCharacterIndex + 1:00} / {count:00}", GUILayout.Width(130f));
            if (GUILayout.Button("Nhân vật sau >")) SelectCharacter(selectedCharacterIndex + 1);
            GUILayout.EndHorizontal();
            GUI.enabled = previousEnabled;
        }

        private void SelectCharacter(int index)
        {
            int next = WrapCharacterIndex(index);
            if (next == selectedCharacterIndex) return;
            selectedCharacterIndex = next;
            if (connected && !roomLocked)
                SendHello();
        }

        private int WrapCharacterIndex(int index)
        {
            int count = characterCast != null ? characterCast.Count : 0;
            if (count <= 0) return 0;
            return (index % count + count) % count;
        }

        private int ClampCharacterIndex(int index)
        {
            int count = characterCast != null ? characterCast.Count : 0;
            return count <= 0 ? 0 : Mathf.Clamp(index, 0, count - 1);
        }

        private static string SanitizeName(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
            return TrimUtf8(value, 48);
        }

        private static FixedString64Bytes Fixed64(string value) => new(TrimUtf8(value ?? string.Empty, 60));
        private static FixedString128Bytes Fixed128(string value) => new(TrimUtf8(value ?? string.Empty, 120));
        private static FixedString512Bytes Fixed512(string value) => new(TrimUtf8(value ?? string.Empty, 500));

        private static string TrimUtf8(string value, int maxBytes)
        {
            while (value.Length > 0 && Encoding.UTF8.GetByteCount(value) > maxBytes)
                value = value.Substring(0, value.Length - 1);
            return value;
        }

        private void OnDestroy()
        {
            DisposeVillage();
            DisposeCommunications();
            ResetEmotes();
            PlayerEmoteWheelUI.DisposeFor(this);
            leavingScene = true;
            DisposeMapUi();
            DisposeTaskGuidance();
            ClearMapPresentation();
            ResetMinigames();
            if (Active == this) Active = null;
            if (game != null) game.PhaseChanged -= HandleOnlineLogicPhaseChanged;
            StopObservingRealtimeDeaths();
            ResetCorpseState();
            RemoveOnlineWinRules();
            RestoreOtherGameOverManager();
            if (endgameCanvas != null) Destroy(endgameCanvas.gameObject);
            if (observedVoteManager != null) observedVoteManager.VoteResolved -= HandleVoteResolved;
            if (observedNetworkAbilities != null) observedNetworkAbilities.AbilityResolved -= HandleNetworkAbilityResolved;
            UnregisterMessageHandlers();
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            if (network == null) return;
            network.OnServerStarted -= HandleServerStarted;
            network.OnClientConnectedCallback -= HandleClientConnected;
            network.OnClientDisconnectCallback -= HandleClientDisconnected;
            network.OnClientStopped -= HandleNetworkStopped;
            network.OnServerStopped -= HandleNetworkStopped;
            if (network.ConnectionApprovalCallback == ApproveConnection)
                network.ConnectionApprovalCallback = previousApproval;
        }
    }
}
