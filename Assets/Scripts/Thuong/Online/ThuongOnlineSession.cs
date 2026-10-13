using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Thuong;
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
    public sealed class ThuongOnlineSession : MonoBehaviour
    {
        private const int MaxPlayers = 12;
        private const float MoveSpeed = 3f;
        private const float InputInterval = 1f / 20f;
        private const float SnapshotInterval = 1f / 10f;
        private const float PrivateInterval = 0.5f;
        private const int WindowId = 0x54484F4E;
        private const int GameplayWindowId = 0x47414D45;

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
        private bool gameplayHudExpanded;
        private float nextInputTime;
        private float nextSnapshotTime;
        private float nextPrivateTime;
        private ushort inputSequence;
        private uint requestId = 1;
        private uint snapshotRevision;
        private int localPlayerId = -1;
        private int selectedTargetId = -1;
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
        private Rect gameplayWindowRect = new(0, 12, 420, 170);

        private void Awake()
        {
            network = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
            playerName = "Player " + UnityEngine.Random.Range(100, 999);
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
            game = FindAnyObjectByType<GameRoleManager>();
            players = FindAnyObjectByType<PlayerManager>();
            roles = FindAnyObjectByType<RoleManager>();
            tasks = FindAnyObjectByType<TaskManager>();
            worldCamera = FindAnyObjectByType<ThuongTopDownCamera>();

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

            foreach (PrototypeControls controls in FindObjectsByType<PrototypeControls>())
                controls.enabled = false;
            foreach (ThuongFourRoleOfflineTest test in FindObjectsByType<ThuongFourRoleOfflineTest>())
                test.enabled = false;
            foreach (VoteButton button in FindObjectsByType<VoteButton>())
                button.enabled = false;

            GameHUD hud = FindAnyObjectByType<GameHUD>();
            if (hud != null) hud.gameObject.SetActive(false);

            stations = FindObjectsByType<TaskStation>()
                .OrderBy(station => station.task != null ? station.task.name : string.Empty)
                .ThenBy(station => station.transform.position.x)
                .ThenBy(station => station.transform.position.y)
                .ToArray();
            foreach (TaskStation station in stations)
            {
                station.enabled = false;
                if (station.marker != null && station.marker != station.gameObject)
                    station.marker.SetActive(false);
            }

            ThuongTopDownCharacter character = FindAnyObjectByType<ThuongTopDownCharacter>();
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
            UpdateAvatars();
            if (!connected || network == null || !network.IsListening) return;

            if (network.IsServer)
            {
                if (rosterDirty) BroadcastRoster();
                if (matchStarted) SimulateServerMovement();
                if (Time.unscaledTime >= nextSnapshotTime)
                {
                    nextSnapshotTime = Time.unscaledTime + SnapshotInterval;
                    BroadcastSnapshot();
                }
                if (matchStarted && Time.unscaledTime >= nextPrivateTime)
                {
                    nextPrivateTime = Time.unscaledTime + PrivateInterval;
                    SendAllPrivateStates();
                }
            }

            if (matchStarted && Time.unscaledTime >= nextInputTime)
            {
                nextInputTime = Time.unscaledTime + InputInterval;
                SendMovementInput(ReadMovementInput());
            }

            if (matchStarted && Keyboard.current?.eKey.wasPressedThisFrame == true)
                RequestNearestTask();
        }

        private Vector2 ReadMovementInput()
        {
            if (Keyboard.current == null || visibleState != GameState.Day) return Vector2.zero;
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
            bool canMove = game != null && game.currentState == GameState.Day;
            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Vector2 min = worldCamera != null ? worldCamera.boundsMin : new Vector2(-13, -11);
            Vector2 max = worldCamera != null ? worldCamera.boundsMax : new Vector2(15, 13);

            foreach (ThuongOnlineServerPlayer member in serverPlayers.Values)
            {
                PlayerData player = players?.GetplayerByID(member.PlayerId);
                Vector2 input = canMove && player != null && player.isAlive &&
                                Time.unscaledTime - member.LastInputTime <= 0.35f
                    ? member.Input : Vector2.zero;
                Vector2 next = member.Position + Vector2.ClampMagnitude(input, 1f) * MoveSpeed * delta;
                member.Position = new Vector2(
                    Mathf.Clamp(next.x, min.x, max.x),
                    Mathf.Clamp(next.y, min.y, max.y));
            }
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            if (roomLocked)
            {
                response.Approved = false;
                response.Reason = "Trận đã bắt đầu.";
                response.Pending = false;
                return;
            }
            if (serverPlayers.Count >= MaxPlayers && !serverPlayers.ContainsKey(request.ClientNetworkId))
            {
                response.Approved = false;
                response.Reason = "Phòng đã đầy.";
                response.Pending = false;
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = false;
            response.Pending = false;
            if (!serverPlayers.ContainsKey(request.ClientNetworkId))
            {
                string requestedName = request.Payload != null && request.Payload.Length > 0
                    ? Encoding.UTF8.GetString(request.Payload) : "Player";
                serverPlayers.Add(request.ClientNetworkId, new ThuongOnlineServerPlayer
                {
                    ClientId = request.ClientNetworkId,
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
            Vector2 origin = playerTemplate != null ? playerTemplate.transform.position : Vector2.zero;
            return origin + new Vector2((index % 4 - 1.5f) * 1.25f, -(index / 4) * 1.25f);
        }

        private void HandleServerStarted()
        {
            RegisterMessageHandlers();
            status = "Server đã sẵn sàng.";
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (network.IsServer && !serverPlayers.ContainsKey(clientId))
            {
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
                connected = true;
                busy = false;
                RegisterMessageHandlers();
                SendHello();
                status = network.IsHost ? "Đã tạo phòng." : "Đã tham gia phòng.";
            }

            if (network.IsServer) rosterDirty = true;
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (network.IsServer && serverPlayers.Remove(clientId))
            {
                rosterDirty = true;
                if (matchStarted)
                {
                    matchStarted = false;
                    status = "Trận đã dừng vì một người chơi mất kết nối.";
                    DayTimer.Instance?.StopTimer();
                    if (game != null) game.enabled = false;
                    BroadcastSnapshot();
                }
            }

            if (clientId == network.LocalClientId)
            {
                connected = false;
                matchStarted = false;
                busy = false;
                status = "Đã ngắt kết nối.";
            }
        }

        private void HandleNetworkStopped(bool _)
        {
            UnregisterMessageHandlers();
            connected = false;
            busy = false;
            roomLocked = false;
            matchStarted = false;
            sessionCode = string.Empty;
            ClearClientState();
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
        }

        private void UnregisterMessageHandlers()
        {
            if (!handlersRegistered) return;
            var messaging = network != null ? network.CustomMessagingManager : null;
            if (messaging != null)
            {
                messaging.UnregisterNamedMessageHandler(ThuongOnlineMessages.Hello);
                messaging.UnregisterNamedMessageHandler(ThuongOnlineMessages.Ready);
                messaging.UnregisterNamedMessageHandler(ThuongOnlineMessages.Input);
                messaging.UnregisterNamedMessageHandler(ThuongOnlineMessages.Action);
                messaging.UnregisterNamedMessageHandler(ThuongOnlineMessages.Roster);
                messaging.UnregisterNamedMessageHandler(ThuongOnlineMessages.Snapshot);
                messaging.UnregisterNamedMessageHandler(ThuongOnlineMessages.PrivateState);
            }
            handlersRegistered = false;
        }

        private async void HandleActiveSceneChanged(Scene previous, Scene current)
        {
            if (current.name == ThuongOnlineBootstrap.SceneName) return;
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
            reader.ReadValueSafe(out FixedString64Bytes value);
            reader.ReadValueSafe(out int characterIndex);
            UpdateServerIdentity(sender, value.ToString(), characterIndex);
        }

        private void UpdateServerIdentity(ulong clientId, string value, int characterIndex)
        {
            if (!serverPlayers.TryGetValue(clientId, out ThuongOnlineServerPlayer member)) return;
            member.Name = SanitizeName(value);
            member.CharacterIndex = ClampCharacterIndex(characterIndex);
            rosterDirty = true;
        }

        private void ToggleReady()
        {
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
            if (roomLocked || !serverPlayers.TryGetValue(clientId, out ThuongOnlineServerPlayer member)) return;
            member.Ready = ready;
            rosterDirty = true;
            if (serverPlayers.Count > 0 && serverPlayers.Values.All(player => player.Ready))
                StartMatchOnServer();
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
            if (!matchStarted || !serverPlayers.TryGetValue(sender, out ThuongOnlineServerPlayer member)) return;
            if (float.IsNaN(input.x) || float.IsInfinity(input.x) ||
                float.IsNaN(input.y) || float.IsInfinity(input.y)) return;
            if (sequence == member.LastInputSequence && member.LastInputTime > 0f) return;
            member.LastInputSequence = sequence;
            member.Input = Vector2.ClampMagnitude(input, 1f);
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
                        Ready = member.Ready
                    });
                    if (member.ClientId == network.LocalClientId) localPlayerId = member.PlayerId;
                }
            }
            using var writer = new FastBufferWriter(2048, Allocator.Temp);
            writer.WriteValueSafe((ushort)serverPlayers.Count);
            foreach (ThuongOnlineServerPlayer member in serverPlayers.Values)
            {
                writer.WriteValueSafe(member.ClientId);
                writer.WriteValueSafe(member.PlayerId);
                FixedString64Bytes name = Fixed64(member.Name);
                writer.WriteValueSafe(name);
                writer.WriteValueSafe(member.CharacterIndex);
                writer.WriteValueSafe(member.Ready);
            }
            network.CustomMessagingManager.SendNamedMessage(
                ThuongOnlineMessages.Roster, network.ConnectedClientsIds, writer);
        }

        private void ReceiveRoster(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out ushort count);
            lobby.Clear();
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong clientId);
                reader.ReadValueSafe(out int playerId);
                reader.ReadValueSafe(out FixedString64Bytes name);
                reader.ReadValueSafe(out int characterIndex);
                reader.ReadValueSafe(out bool ready);
                lobby.Add(new ThuongOnlineLobbyMember
                {
                    ClientId = clientId,
                    PlayerId = playerId,
                    Name = name.ToString(),
                    CharacterIndex = characterIndex,
                    Ready = ready
                });
                if (clientId == network.LocalClientId) localPlayerId = playerId;
            }
        }

        private void StartMatchOnServer()
        {
            if (!network.IsServer || roomLocked || serverPlayers.Count == 0 ||
                serverPlayers.Values.Any(member => !member.Ready)) return;
            if (players == null || game == null || roles == null || tasks == null)
            {
                status = "Scene thiếu gameplay manager của Thương.";
                return;
            }

            roomLocked = true;
            matchStarted = true;
            snapshotRevision = 0;
            players.prototypeLobbySize = 0;
            players.players.Clear();
            roles.playerRoles.Clear();
            privateFeedback.Clear();

            int index = 0;
            foreach (ThuongOnlineServerPlayer member in serverPlayers.Values)
            {
                member.Position = SpawnPosition(index++);
                member.Input = Vector2.zero;
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
            game.enabled = true;
            game.BeginGame();
            game.SkipRoleReveal();
            status = "Trận đã bắt đầu.";
            SendAllPrivateStates();
            BroadcastSnapshot();
        }

        private void BroadcastSnapshot()
        {
            if (!network.IsServer || network.ConnectedClientsIds.Count == 0) return;
            snapshotRevision++;

            GameState state = game != null ? game.currentState : GameState.RoleReveal;
            GamePhase phase = game != null ? game.currentPhase : GamePhase.Setup;
            int day = game != null ? game.currentDay : 0;
            float remaining = game != null && game.currentState == GameState.Day && DayTimer.Instance != null
                ? DayTimer.Instance.TimeRemaining : game != null ? game.PhaseTimeRemaining : 0f;
            float progress = tasks != null ? tasks.progress : 0f;
            float dailyProgress = tasks != null ? tasks.DailyProgress : 0f;
            string winner = game != null ? game.Winner : string.Empty;
            BuildTaskMasks(out activeTaskMask, out completedTaskMask);
            if (network.IsHost)
                ApplyHostSnapshot(state, phase, day, remaining, progress, dailyProgress, winner);

            using var writer = new FastBufferWriter(8192, Allocator.Temp);
            writer.WriteValueSafe(snapshotRevision);
            writer.WriteValueSafe(matchStarted);
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
                writer.WriteValueSafe(member.Position.x);
                writer.WriteValueSafe(member.Position.y);
            }
            network.CustomMessagingManager.SendNamedMessage(
                ThuongOnlineMessages.Snapshot, network.ConnectedClientsIds, writer,
                NetworkDelivery.UnreliableSequenced);
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
                    Position = member.Position
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
            if (sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out uint revision);
            if (revision < snapshotRevision && !network.IsServer) return;
            snapshotRevision = revision;
            reader.ReadValueSafe(out matchStarted);
            reader.ReadValueSafe(out byte state);
            reader.ReadValueSafe(out byte phase);
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
                reader.ReadValueSafe(out float x);
                reader.ReadValueSafe(out float y);
                publicPlayers.Add(new ThuongOnlinePublicPlayer
                {
                    ClientId = clientId,
                    PlayerId = playerId,
                    Name = name.ToString(),
                    CharacterIndex = characterIndex,
                    Alive = alive,
                    Position = new Vector2(x, y)
                });
                if (clientId == network.LocalClientId) localPlayerId = playerId;
            }

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
        }

        private void SendAllPrivateStates()
        {
            if (!network.IsServer || !matchStarted || players == null) return;
            foreach (ThuongOnlineServerPlayer member in serverPlayers.Values)
                SendPrivateState(member.ClientId, privateFeedback.TryGetValue(member.PlayerId, out string feedback)
                    ? feedback : string.Empty);
        }

        private void SendPrivateState(ulong clientId, string feedback)
        {
            if (!serverPlayers.TryGetValue(clientId, out ThuongOnlineServerPlayer member)) return;
            PlayerData player = players?.GetplayerByID(member.PlayerId);
            if (player == null) return;
            ushort flags = 0;
            if (player.isAlive) flags |= 1;
            if (player.hasVoted) flags |= 2;
            if (player.hasUseNightAction) flags |= 4;
            if (RoleManager.Instance != null && RoleManager.Instance.playerRoles.TryGetValue(player.playerID, out BaseRole role))
            {
                if (role.HasNightAbility) flags |= 8;
                if (role is HunterRole && !player.isAlive && player.hasHunterTrap) flags |= 16;
            }

            if (network.IsHost && clientId == network.LocalClientId)
            {
                localPlayerId = member.PlayerId;
                privateRole = player.roleType;
                privateFaction = player.faction;
                privateFlags = flags;
                if (!string.IsNullOrEmpty(feedback)) status = feedback;
            }

            using var writer = new FastBufferWriter(256, Allocator.Temp);
            writer.WriteValueSafe(member.PlayerId);
            writer.WriteValueSafe((byte)player.roleType);
            writer.WriteValueSafe((byte)player.faction);
            writer.WriteValueSafe(flags);
            FixedString128Bytes text = Fixed128(feedback);
            writer.WriteValueSafe(text);
            network.CustomMessagingManager.SendNamedMessage(
                ThuongOnlineMessages.PrivateState, clientId, writer);
        }

        private void ReceivePrivateState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out int playerId);
            reader.ReadValueSafe(out byte role);
            reader.ReadValueSafe(out byte faction);
            reader.ReadValueSafe(out privateFlags);
            reader.ReadValueSafe(out FixedString128Bytes feedback);
            localPlayerId = playerId;
            privateRole = (RoleType)role;
            privateFaction = (FactionType)faction;
            string message = feedback.ToString();
            if (!string.IsNullOrEmpty(message)) status = message;
            if (!network.IsServer)
            {
                PlayerData local = players?.GetplayerByID(playerId);
                if (local != null)
                {
                    local.roleType = privateRole;
                    local.faction = privateFaction;
                }
            }
        }

        private void RequestNearestTask()
        {
            if (localPlayerId < 0 || visibleState != GameState.Day ||
                !avatars.TryGetValue(localPlayerId, out ThuongOnlineAvatar avatar)) return;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < stations.Length && i < 16; i++)
            {
                ushort bit = (ushort)(1 << i);
                if ((activeTaskMask & bit) == 0 || (completedTaskMask & bit) != 0) continue;
                float distance = Vector2.Distance(avatar.Root.transform.position, stations[i].transform.position);
                if (distance < bestDistance) { best = i; bestDistance = distance; }
            }
            if (best < 0 || bestDistance > stations[best].interactionRadius + 0.25f)
            {
                status = "Không có nhiệm vụ khả dụng ở gần.";
                return;
            }
            SendAction(ThuongOnlineAction.CompleteTask, best);
        }

        private void SendAction(ThuongOnlineAction action, int argument)
        {
            uint id = requestId++;
            if (network.IsServer && network.LocalClientId == NetworkManager.ServerClientId)
            {
                ApplyServerAction(network.LocalClientId, id, action, argument);
                return;
            }
            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(id);
            writer.WriteValueSafe((byte)action);
            writer.WriteValueSafe(argument);
            network.CustomMessagingManager.SendNamedMessage(
                ThuongOnlineMessages.Action, NetworkManager.ServerClientId, writer);
        }

        private void ReceiveAction(ulong sender, FastBufferReader reader)
        {
            if (!network.IsServer) return;
            reader.ReadValueSafe(out uint id);
            reader.ReadValueSafe(out byte action);
            reader.ReadValueSafe(out int argument);
            ApplyServerAction(sender, id, (ThuongOnlineAction)action, argument);
        }

        private void ApplyServerAction(ulong sender, uint id, ThuongOnlineAction action, int argument)
        {
            if (!matchStarted || !serverPlayers.TryGetValue(sender, out ThuongOnlineServerPlayer member)) return;
            if (id <= member.LastRequestId) return;
            member.LastRequestId = id;
            PlayerData actor = players?.GetplayerByID(member.PlayerId);
            if (actor == null) { Reply(member, "Không tìm thấy người chơi."); return; }

            bool accepted = false;
            string feedback = string.Empty;
            switch (action)
            {
                case ThuongOnlineAction.CompleteTask:
                    accepted = TryCompleteTask(member, actor, argument, out feedback);
                    break;
                case ThuongOnlineAction.NightAbility:
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
                    accepted = VoteManager.Instance != null && VoteManager.Instance.TryVote(actor.playerID, argument);
                    feedback = accepted ? "Đã ghi nhận phiếu bầu." : "Phiếu bầu không hợp lệ.";
                    break;
                default:
                    feedback = "Yêu cầu không được hỗ trợ.";
                    break;
            }

            if (!accepted && string.IsNullOrWhiteSpace(feedback)) feedback = "Yêu cầu bị từ chối.";
            Reply(member, feedback);
            BroadcastSnapshot();
        }

        private bool TryCompleteTask(ThuongOnlineServerPlayer member, PlayerData actor, int stationId,
            out string feedback)
        {
            if (stationId < 0 || stationId >= stations.Length || stationId >= 16 || tasks == null)
            { feedback = "Trạm nhiệm vụ không hợp lệ."; return false; }
            TaskStation station = stations[stationId];
            if (!actor.isAlive || game == null || game.currentState != GameState.Day)
            { feedback = "Chỉ người còn sống mới làm nhiệm vụ ban ngày."; return false; }
            if (Vector2.Distance(member.Position, station.transform.position) > station.interactionRadius + 0.25f)
            { feedback = "Bạn đang đứng quá xa trạm nhiệm vụ."; return false; }
            bool accepted = tasks.TryCompleteTask(actor.playerID, station.task);
            feedback = accepted ? "Hoàn thành nhiệm vụ." : "Nhiệm vụ không còn khả dụng.";
            return accepted;
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
                    avatar.TargetPosition = state.Position;
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
                PreviousPosition = state.Position
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
            if (busy || network.IsListening) return;
            busy = true;
            status = "Đang tạo phòng Relay...";
            try
            {
                PrepareConnectionData();
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
                busy = false;
            }
        }

        private async void JoinRelay()
        {
            if (busy || network.IsListening || string.IsNullOrWhiteSpace(joinCode)) return;
            busy = true;
            status = "Đang tham gia Relay...";
            try
            {
                PrepareConnectionData();
                await EnsureUnityServices();
                relaySession = await MultiplayerService.Instance.JoinSessionByCodeAsync(
                    joinCode.Trim().ToUpperInvariant());
                sessionCode = relaySession.Code;
                status = "Đã tham gia phòng Relay.";
            }
            catch (Exception exception)
            {
                status = "Không tham gia được Relay: " + exception.Message;
                busy = false;
            }
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
            if (busy || network.IsListening || !TryGetPort(out ushort port)) return;
            busy = true;
            PrepareConnectionData();
            transport.SetConnectionData(address.Trim(), port, "0.0.0.0");
            if (!network.StartHost())
            {
                busy = false;
                status = "Không thể mở LAN Host.";
            }
            else status = "Đang mở LAN Host...";
        }

        private void JoinLan()
        {
            if (busy || network.IsListening || !TryGetPort(out ushort port)) return;
            busy = true;
            PrepareConnectionData();
            transport.SetConnectionData(address.Trim(), port);
            if (!network.StartClient())
            {
                busy = false;
                status = "Không thể khởi động LAN Client.";
            }
            else status = "Đang kết nối LAN...";
        }

        private void PrepareConnectionData()
        {
            playerName = SanitizeName(playerName);
            network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(playerName);
        }

        private bool TryGetPort(out ushort port)
        {
            if (ushort.TryParse(portText, out port) && port > 0) return true;
            status = "Port không hợp lệ.";
            return false;
        }

        private async void LeaveSession()
        {
            if (busy) return;
            busy = true;
            status = "Đang rời phòng...";
            try
            {
                if (relaySession != null)
                {
                    await relaySession.LeaveAsync();
                    relaySession = null;
                }
                else if (network.IsListening)
                {
                    network.Shutdown();
                }
                connected = false;
                status = "Đã rời phòng.";
                ClearClientState();
            }
            catch (Exception exception)
            {
                status = "Không thể rời phòng: " + exception.Message;
            }
            finally { busy = false; }
        }

        private void ClearClientState()
        {
            lobby.Clear();
            publicPlayers.Clear();
            serverPlayers.Clear();
            foreach (ThuongOnlineAvatar avatar in avatars.Values)
                if (avatar.Root != null) Destroy(avatar.Root);
            avatars.Clear();
            localPlayerId = -1;
            selectedTargetId = -1;
        }

        private void OnGUI()
        {
            if (matchStarted)
            {
                DrawGameplayHud();
                return;
            }

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
            GUILayout.Label("Scene: OnlineSceneGamePlay");
            GUILayout.Label(status, GUILayout.MinHeight(38));

            if (!connected)
            {
                GUILayout.Space(8);
                GUILayout.Label("Tên người chơi");
                playerName = GUILayout.TextField(playerName, 24);
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
            }
            else
            {
                if (!string.IsNullOrEmpty(sessionCode)) GUILayout.Label("Mã phòng: " + sessionCode);
                bool localReady = lobby.FirstOrDefault(item => item.ClientId == network.LocalClientId)?.Ready ?? false;
                DrawCharacterSelector(!localReady && !roomLocked);
                GUILayout.Label("Người chơi trong phòng:");
                foreach (ThuongOnlineLobbyMember member in lobby)
                {
                    string you = member.ClientId == network.LocalClientId ? " (Bạn)" : string.Empty;
                    GUILayout.Label($"P{member.PlayerId + 1:00}  {member.Name}{you}  |  NV {member.CharacterIndex + 1:00}  -  {(member.Ready ? "READY" : "CHỜ")}");
                }

                if (!matchStarted)
                {
                    GUILayout.Space(8);
                    GUILayout.Label("Khi tất cả người chơi Ready, trận sẽ tự bắt đầu.");
                    GUILayout.Label("Ready chưa phải gameplay; hãy chờ đủ người rồi mới Ready.");
                    GUI.enabled = !busy && !roomLocked;
                    if (GUILayout.Button("Đổi trạng thái Ready")) ToggleReady();
                    GUI.enabled = true;
                }
                GUILayout.Space(8);
                GUI.enabled = !busy;
                if (GUILayout.Button("Rời phòng")) LeaveSession();
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

        private void DrawGameplayHud()
        {
            bool voting = visibleState == GameState.Voting;
            if (voting) gameplayHudExpanded = true;

            float width = Mathf.Min(420f, Screen.width - 24f);
            float desiredHeight = gameplayHudExpanded ? 600f : 180f;
            float height = Mathf.Min(desiredHeight, Screen.height - 24f);
            float x = voting
                ? Mathf.Max(12f, (Screen.width - width) * 0.5f)
                : Mathf.Max(12f, Screen.width - width - 12f);
            gameplayWindowRect = new Rect(
                x,
                12f,
                width,
                height);
            gameplayWindowRect = GUI.Window(
                GameplayWindowId, gameplayWindowRect, DrawGameplayWindow,
                voting ? "BỎ PHIẾU" : "GAMEPLAY");
        }

        private void DrawGameplayWindow(int id)
        {
            GUILayout.Label($"Ngày {visibleDay}  |  {visibleState} / {visiblePhase}  |  {Mathf.CeilToInt(visibleRemaining)}s");
            GUILayout.Label($"Tiến độ: {visibleProgress:0}%  |  Hôm nay: {visibleDailyProgress:0}%");
            GUILayout.Label("Role: " + privateRole + "  |  Phe: " + privateFaction);
            GUILayout.Label("Di chuyển: WASD / phím mũi tên  |  Làm nhiệm vụ: E");
            if (!string.IsNullOrEmpty(status)) GUILayout.Label(status);
            if (!string.IsNullOrEmpty(visibleWinner)) GUILayout.Label("Chiến thắng: " + visibleWinner);

            if (visibleState == GameState.Voting)
            {
                gameplayHudExpanded = true;
                GUILayout.Label("BỎ PHIẾU ĐANG DIỄN RA - chọn một người rồi bấm Bỏ phiếu.");
            }
            else
            {
                gameplayHudExpanded = GUILayout.Toggle(gameplayHudExpanded, "Mở kỹ năng / chọn mục tiêu");
            }
            if (gameplayHudExpanded)
                DrawMatchActions();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Rời phòng")) LeaveSession();
            GUI.DragWindow(new Rect(0, 0, gameplayWindowRect.width, 24));
        }

        private void DrawMatchActions()
        {
            GUILayout.Space(6);
            GUILayout.Label("WASD/Mũi tên: di chuyển. E: làm nhiệm vụ gần nhất.");
            GUILayout.Label("Chọn mục tiêu:");
            bool hasTarget = false;
            bool selectedTargetIsValid = false;
            foreach (ThuongOnlinePublicPlayer target in publicPlayers)
            {
                if (!target.Alive || target.PlayerId == localPlayerId) continue;
                hasTarget = true;
                bool chosen = selectedTargetId == target.PlayerId;
                if (chosen) selectedTargetIsValid = true;
                if (GUILayout.Button((chosen ? "> " : string.Empty) + target.Name))
                {
                    selectedTargetId = target.PlayerId;
                    selectedTargetIsValid = true;
                }
            }

            if (!hasTarget)
                GUILayout.Label("Không có người chơi khác còn sống để chọn. Cần ít nhất 2 người để thử bỏ phiếu.");
            if (!selectedTargetIsValid) selectedTargetId = -1;

            GUI.enabled = selectedTargetId >= 0;
            if (visibleState == GameState.Night)
            {
                if (privateRole == RoleType.Shaman)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Bùa tốt")) SendAction(ThuongOnlineAction.ShamanGoodCharm, selectedTargetId);
                    if (GUILayout.Button("Bùa xấu")) SendAction(ThuongOnlineAction.ShamanBadCharm, selectedTargetId);
                    GUILayout.EndHorizontal();
                }
                else if (GUILayout.Button("Dùng kỹ năng đêm"))
                    SendAction(ThuongOnlineAction.NightAbility, selectedTargetId);
            }
            if (visibleState == GameState.Discussion && privateRole == RoleType.Hunter && (privateFlags & 1) == 0)
                if (GUILayout.Button("Đặt bẫy Thợ săn")) SendAction(ThuongOnlineAction.HunterTrap, selectedTargetId);
            if (visibleState == GameState.Voting)
                if (GUILayout.Button("Bỏ phiếu")) SendAction(ThuongOnlineAction.Vote, selectedTargetId);
            GUI.enabled = true;
        }

        private static string SanitizeName(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
            return TrimUtf8(value, 48);
        }

        private static FixedString64Bytes Fixed64(string value) => new(TrimUtf8(value ?? string.Empty, 60));
        private static FixedString128Bytes Fixed128(string value) => new(TrimUtf8(value ?? string.Empty, 120));

        private static string TrimUtf8(string value, int maxBytes)
        {
            while (value.Length > 0 && Encoding.UTF8.GetByteCount(value) > maxBytes)
                value = value.Substring(0, value.Length - 1);
            return value;
        }

        private void OnDestroy()
        {
            UnregisterMessageHandlers();
            if (network == null) return;
            network.OnServerStarted -= HandleServerStarted;
            network.OnClientConnectedCallback -= HandleClientConnected;
            network.OnClientDisconnectCallback -= HandleClientDisconnected;
            network.OnClientStopped -= HandleNetworkStopped;
            network.OnServerStopped -= HandleNetworkStopped;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            if (network.ConnectionApprovalCallback == ApproveConnection)
                network.ConnectionApprovalCallback = null;
        }
    }
}
