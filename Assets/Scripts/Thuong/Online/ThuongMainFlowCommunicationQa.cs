#if THUONG_MAIN_FLOW_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private sealed partial class CommunicationFlowQa : MonoBehaviour
        {
            private const string Ack = "thuong.qa.communication.ack", Command = "thuong.qa.communication.step";
            private readonly Dictionary<int, HashSet<int>> acknowledgements = new();
            private int slot, command, checks;
            private string output, step;
            private double deadline;
            private double nextDiagnostic;
            private bool finished;
            private NetworkManager manager;
            private bool Host => slot == 0;
            private string Name => slot == 0 ? "Chủ phòng QA" : slot == 1 ? "Người chơi QA" : "Người nối lại QA";
            private static string Argument(string key)
            {
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, key);
                return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
            }

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
            private static void Install()
            {
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-communication-qa") < 0) return;
                var root = new GameObject("Communication main flow QA");
                DontDestroyOnLoad(root); root.AddComponent<CommunicationFlowQa>();
            }

            private void Start()
            {
                Application.targetFrameRate = 30;
                slot = int.Parse(Argument("-flow-slot")); output = Argument("-flow-output");
                deadline = Time.realtimeSinceStartupAsDouble + 240;
                File.WriteAllText(output, "START slot=" + slot + "\n");
                StartCoroutine(Guarded(Run()));
            }

            private void Update()
            {
                if (!finished && Time.realtimeSinceStartupAsDouble >= nextDiagnostic)
                {
                    nextDiagnostic = Time.realtimeSinceStartupAsDouble + 8;
                    var session = Active;
                    File.AppendAllText(output, "WAIT " + step + " | " + (session == null ? SceneManager.GetActiveScene().name :
                        $"match={session.matchStarted}, game={session.game.currentState}/{session.game.currentPhase}, local={session.localCommunicationEpoch}:{session.localCommunicationRevision}/{session.communicationAudience}, epoch={session.matchEpoch}, acks={session.communicationAcks.Count}, roster=" +
                        string.Join(";", session.serverPlayers.Values.Select(member => $"{member.Name}:{member.RevealRead}/{member.Ready}/{member.MapReady}/{member.Offline}"))) + "\n");
                }
                if (!finished && Time.realtimeSinceStartupAsDouble > deadline) Fail(new TimeoutException(step));
            }

            private IEnumerator Guarded(IEnumerator routine)
            {
                while (!finished)
                {
                    bool moved;
                    try { moved = routine.MoveNext(); }
                    catch (Exception error) { Fail(error); yield break; }
                    if (!moved) yield break;
                    yield return routine.Current;
                }
            }

            private void Fail(Exception error)
            {
                if (finished) return;
                finished = true; File.AppendAllText(output, "FAIL " + step + " " + error + "\n");
                Debug.LogException(error); Application.Quit(1);
            }

            private void Check(bool value, string description)
            {
                if (!value) throw new Exception(description);
                checks++; File.AppendAllText(output, "PASS " + description + "\n");
            }

            private bool Wait(bool ready, string description) { step = description; return ready; }
            private bool HasAck(int phase) => acknowledgements.TryGetValue(phase, out var peers) && peers.Count == 2;
            private void Acknowledge(int phase)
            {
                if (Host) return;
                using var writer = new FastBufferWriter(8, Allocator.Temp);
                writer.WriteValueSafe(phase); writer.WriteValueSafe(slot);
                manager.CustomMessagingManager.SendNamedMessage(Ack, NetworkManager.ServerClientId, writer);
            }
            private void Signal(int phase)
            {
                command = phase;
                using var writer = new FastBufferWriter(4, Allocator.Temp); writer.WriteValueSafe(phase);
                foreach (ulong id in manager.ConnectedClientsIds)
                    if (id != manager.LocalClientId) manager.CustomMessagingManager.SendNamedMessage(Command, id, writer);
            }
            private bool CurrentPolicyReady(ThuongOnlineSession session) => session.communicationAcks.Count == 3 &&
                session.communicationAcks.Values.All(value => value == session.communicationRevision);
            private bool Received(ThuongOnlineSession session, string text) => session.communicationChat.MessageHistory.Any(entry => entry.MessageText.ToString() == text);

            private void RegisterQaMessages()
            {
                manager.CustomMessagingManager.RegisterNamedMessageHandler(Ack, (sender, reader) =>
                {
                    if (!Host) return;
                    reader.ReadValueSafe(out int phase); reader.ReadValueSafe(out int member);
                    if (!acknowledgements.TryGetValue(phase, out var peers)) acknowledgements[phase] = peers = new HashSet<int>();
                    peers.Add(member);
                });
                manager.CustomMessagingManager.RegisterNamedMessageHandler(Command, (sender, reader) =>
                { if (sender == NetworkManager.ServerClientId) reader.ReadValueSafe(out command); });
            }

            private void CaptureFrame(string name, int width, int height)
            {
                var camera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
                if (camera == null) throw new Exception("No camera for visual verification.");
                var canvases = UnityEngine.Object.FindObjectsByType<Canvas>().Where(canvas => canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
                var previousCameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
                var previousDistances = canvases.Select(canvas => canvas.planeDistance).ToArray();
                var previousSorting = canvases.Select(canvas => canvas.sortingOrder).ToArray();
                var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
                var previousRect = camera.rect;
                var target = new RenderTexture(width, height, 24);
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    camera.targetTexture = target;
                    // Offscreen UI must use the entire surface, not the world's reserved viewport.
                    camera.rect = new Rect(0, 0, 1, 1);
                    foreach (var canvas in canvases)
                    {
                        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
                        canvas.planeDistance = camera.nearClipPlane + .1f;
                        canvas.sortingOrder += 20000;
                    }
                    Canvas.ForceUpdateCanvases();
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                        new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                    Check(texture.GetPixels32().Count(pixel => pixel.r > 20 || pixel.g > 20 || pixel.b > 20) > 5000,
                        "Visible rendered frame: " + name + " " + width + "x" + height);
                    File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(output), name + ".png"), texture.EncodeToPNG());
                }
                finally
                {
                    for (int i = 0; i < canvases.Length; i++)
                    {
                        canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                        canvases[i].worldCamera = previousCameras[i]; canvases[i].planeDistance = previousDistances[i];
                        canvases[i].sortingOrder = previousSorting[i];
                    }
                    camera.targetTexture = previousTarget; camera.rect = previousRect; RenderTexture.active = previousActive;
                    target.Release(); Destroy(target); Destroy(texture);
                    Canvas.ForceUpdateCanvases();
                }
            }

            private IEnumerator Run()
            {
                while (!Wait(NetworkManager.Singleton != null, "MainMenu manager")) yield return null;
                manager = NetworkManager.Singleton;
                Check(manager.NetworkConfig.ConnectionApproval, "Main lobby uses compatible connection approval");
                manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", ushort.Parse(Argument("-flow-port")), "0.0.0.0");
                RoomManager.CurrentRoomCode = "004281"; RoomManager.CurrentVoiceChannelId = "qa-communications-room";
                RoomManager.IsHostOfCurrentRoom = Host;
                Check(Host ? manager.StartHost() : manager.StartClient(), "Original NetworkManager started");
                RegisterQaMessages();
                if (Host) Check(manager.SceneManager.LoadScene(ThuongMainFlow.LobbyScene, LoadSceneMode.Single) == SceneEventProgressStatus.Started, "Original lobby loaded");

                for (int round = 1; round <= 2; round++)
                {
                    int phase = round * 100;
                    while (!Wait(SceneManager.GetActiveScene().name == ThuongMainFlow.LobbyScene && LobbyManager.Instance != null && LobbyManager.Instance.IsSpawned, "Shared lobby round " + round)) yield return null;
                    var nameInput = (TMP_InputField)typeof(LobbyManager).GetField("playerNameInput", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(LobbyManager.Instance);
                    Check(nameInput.gameObject.activeInHierarchy, "Shared lobby input stays visible");
                    nameInput.text = Name; LobbyManager.Instance.SubmitPlayerName();
                    Acknowledge(phase + 30);
                    if (Host)
                    {
                        while (!Wait(HasAck(phase + 30), "Both clients entered shared lobby for round " + round)) yield return null;
                        while (!Wait(LobbyManager.CurrentPlayerCount == 3 && manager.ConnectedClientsIds.Count == 3 &&
                            manager.ConnectedClientsIds.All(id => NetworkGamePlayerNameSetup.GetName(id).Contains("QA")), "Three named lobby members")) yield return null;
                        if (Argument("-flow-visual") != null)
                        {
                            yield return new WaitForSecondsRealtime(.5f);
                            CaptureFrame("lobby-" + round, 1280, 720);
                            yield return new WaitForSecondsRealtime(.3f);
                        }
                        if (Argument("-authority-qa") != null) ConfigureAuthorityRoles(round);
                        LobbyManager.Instance.StartGame();
                        Check(ThuongMainFlow.Pending, "Roster captured before scene transition");
                    }
                    while (!Wait(Active != null && Active.connected && Active.IsMainFlowSession, "Adopted Thuong connection")) yield return null;
                    var session = Active;
                    Check(NetworkManager.Singleton == manager, "Original connection preserved after lobby handoff");
                    if (Host)
                    {
                        while (!Wait(session.AllMapsReady && session.serverPlayers.Count == 3, "Three map acknowledgements")) yield return null;
                        Check(session.serverPlayers.Values.All(member => member.Name.Contains("QA")), "Unicode roster preserved");
                        Check(session.SetHostMapSelection(false, round == 1 ? "harvest_market_expanded" : "mist_swamp_expanded", GameEventType.None), "Host selects map");
                        while (!Wait(session.AllMapsReady, "Selected map acknowledged")) yield return null;
                        if (Argument("-authority-qa") != null) VerifyRoleModeConstraints(session);
                        session.AssignLobbyRolesOnServer();
                        if (Argument("-authority-qa") != null)
                            Check(session.roles.playerRoles.Values.Count(role => role.roleType == RoleType.DogSpirit) == 1 &&
                                session.roles.playerRoles.Values.Count(role => role.roleType == RoleType.Seer) == 1 &&
                                session.roles.playerRoles.Values.Count(role => role.roleType == RoleType.Villager) == 1,
                                "Configured role quantities assigned exactly once before private delivery");
                    }
                    while (!Wait(session.HasLocalRole, "Private role delivery")) yield return null;
                    if (Argument("-authority-qa") != null) VerifyRoundEpoch(session);
                    session.ViewLocalRole(); session.ReadLocalRole();
                    Check(session.CanToggleLobbyReady, "Read role enables Ready"); session.ToggleReady();
                    if (Host)
                    {
                        while (!Wait(session.serverPlayers.Values.All(member => member.RevealRead && member.Ready), "All Ready")) yield return null;
                        session.StartMatchOnServer();
                    }
                    while (!Wait(session.matchStarted && session.CommunicationAudience == ThuongCommunicationAudience.Living, "Living day communication permission")) yield return null;
                    if (Host)
                    {
                        while (!Wait(CurrentPolicyReady(session), "All communication permissions acknowledged")) yield return null;
                        Check(!session.AcceptChat(ulong.MaxValue, session.matchEpoch, session.communicationRevision, 1, "FORGED"), "Chat rejects unknown sender");
                        Check(!session.AcceptChat(manager.LocalClientId, session.matchEpoch, session.communicationRevision, 1, new string('x', 121)), "Chat rejects oversized input");
                        session.SendChatText("MỞ ĐẦU 0 / " + round);
                    }
                    else
                    {
                        while (!Wait(Received(session, "MỞ ĐẦU 0 / " + round), "Public chat start")) yield return null;
                        session.SendChatText("MỞ ĐẦU " + slot + " / " + round);
                    }
                    while (!Wait(Enumerable.Range(0, 3).All(id => Received(session, "MỞ ĐẦU " + id + " / " + round)), "Three-way day chat")) yield return null;
                    Check(true, "Three players receive Unicode chat from host and both clients"); Acknowledge(phase + 1);
                    if (Host)
                    {
                        while (!Wait(HasAck(phase + 1), "Peers confirm day chat before sanitization")) yield return null;
                        yield return new WaitForSecondsRealtime(.8f);
                        Check(session.SendChatText("<b>kiểm tra</b>"), "Formatted input submitted as plain chat text");
                    }
                    while (!Wait(Received(session, "‹b›kiểm tra‹/b›"), "Escaped rich text")) yield return null;
                    Check(true, "Chat cannot inject rich text formatting"); Acknowledge(phase + 10);
                    if (Host)
                    {
                        while (!Wait(HasAck(phase + 10), "Peers confirm sanitized chat")) yield return null;
                        if (Argument("-flow-visual") != null)
                        {
                            session.communicationUi.SetChatOpen(true); yield return new WaitForSecondsRealtime(.5f);
                            CaptureFrame("chat-1280x720", 1280, 720);
                            yield return new WaitForSecondsRealtime(.5f);
                            CaptureFrame("chat-1920x1080", 1920, 1080);
                            yield return new WaitForSecondsRealtime(.5f); session.communicationUi.SetChatOpen(false);
                        }
                        Signal(phase + 2);
                    }
                    while (!Wait(command >= phase + 2, "Reconnect stage")) yield return null;
                    if (round == 1 && slot == 2)
                    {
                        int playerId = session.localPlayerId;
                        Check(session.reconnectToken != Guid.Empty, "Private reconnect ticket received from shared lobby flow");
                        manager.Shutdown();
                        while (!Wait(!manager.IsListening && !manager.ShutdownInProgress && session.reconnectOfferUntil > 0, "Shutdown and reconnect offer")) yield return null;
                        Check(SceneManager.GetActiveScene().name == ThuongOnlineBootstrap.SceneName, "Disconnect stays in Thuong reconnect screen");
                        session.RetryConnection();
                        while (!Wait(Active != null && Active.connected && Active.privateStateReceived && Active.localPlayerId == playerId, "Rejoined original player and private role")) yield return null;
                        session = Active;
                        RegisterQaMessages();
                        Check(NetworkManager.Singleton == manager && session.localPlayerId == playerId, "Reconnect preserves player identity and transport");
                        Acknowledge(phase + 2);
                    }
                    if (Host)
                    {
                        if (round == 1)
                        {
                            while (!Wait(acknowledgements.TryGetValue(phase + 2, out var peers) && peers.Contains(2), "Client reconnect acknowledgement")) yield return null;
                            Check(session.serverPlayers.Count == 3 && session.serverPlayers.Values.All(member => !member.Offline), "Reconnect retains exact roster without duplicate player");
                        }
                    }
                    if (Argument("-authority-qa") != null)
                    {
                        var authority = VerifyAuthority(session, phase);
                        while (authority.MoveNext()) yield return authority.Current;
                    }
                    if (Host)
                    {
                        // Hold a controlled phase while network peers acknowledge its policy.
                        session.game.enabled = false; DayTimer.Instance.StopTimer();
                        session.game.currentState = GameState.Night; session.game.currentPhase = GamePhase.Night;
                        session.RefreshServerCommunications();
                        Check(!session.AcceptChat(manager.LocalClientId, session.matchEpoch, session.communicationRevision, 9000, "FORGED NIGHT MESSAGE"), "Server rejects living night chat despite forged request");
                        Signal(phase + 3);
                    }
                    while (!Wait(command >= phase + 3 && session.visibleState == GameState.Night && session.CommunicationAudience == ThuongCommunicationAudience.None, "Night communication block")) yield return null;
                    Check(!session.CanSendChat, "Living chat blocked at night"); Acknowledge(phase + 3);
                    if (Host)
                    {
                        while (!Wait(HasAck(phase + 3), "Peers confirm night block")) yield return null;
                        foreach (var member in session.serverPlayers.Values.Where(member => member.ClientId != manager.LocalClientId))
                            session.players.GetplayerByID(member.PlayerId).isAlive = false;
                        session.game.currentState = GameState.Discussion; session.game.currentPhase = GamePhase.Discussion;
                        session.RefreshServerCommunications();
                        while (!Wait(session.CommunicationAudience == ThuongCommunicationAudience.Living && CurrentPolicyReady(session), "Living/dead split acknowledged")) yield return null;
                        session.SendChatText("CHỈ NGƯỜI SỐNG / " + round); Signal(phase + 4);
                    }
                    while (!Wait(command >= phase + 4 && session.CommunicationAudience == (Host ? ThuongCommunicationAudience.Living : ThuongCommunicationAudience.Dead), "Private dead chat channel")) yield return null;
                    if (!Host)
                    {
                        session.SendChatText("NGƯỜI BỊ LOẠI " + slot + " / " + round);
                        while (!Wait(Received(session, "NGƯỜI BỊ LOẠI 1 / " + round) && Received(session, "NGƯỜI BỊ LOẠI 2 / " + round), "Dead-to-dead chat")) yield return null;
                        Check(!Received(session, "CHỈ NGƯỜI SỐNG / " + round), "Dead players receive each other and cannot receive living channel");
                        Acknowledge(phase + 4);
                    }
                    if (Host)
                    {
                        while (!Wait(HasAck(phase + 4), "Peers confirm isolated dead chat")) yield return null;
                        Check(!session.communicationChat.MessageHistory.Any(entry => entry.MessageText.ToString().StartsWith("NGƯỜI BỊ LOẠI")), "Living host cannot receive dead chat");
                        Check(!session.AcceptChat(manager.LocalClientId, session.matchEpoch - 1, session.communicationRevision, 9001, "STALE"), "Server rejects previous match chat");
                        session.tasks.progress = 100; session.EvaluateOnlineWin(false);
                        Check(session.matchResult != null && session.matchResult.Players.Count == 3, "Production win evaluation returns full result");
                    }
                    while (!Wait(session.matchResult != null && session.CommunicationAudience == ThuongCommunicationAudience.Room, "Result room communication")) yield return null;
                    Acknowledge(phase + 5);
                    if (Host)
                    {
                        while (!Wait(HasAck(phase + 5), "All results received")) yield return null;
                        if (round == 1) Check(session.ReturnToMainLobby(), "Whole room returns to original lobby and can replay");
                        else { Check(ThuongMainFlow.RoomCode == "004281", "Room code preserved through replay and reconnect"); session.LeaveSession(); }
                    }
                    if (round == 2 && !Host)
                    {
                        while (!Wait(!manager.IsListening, "Host exit")) yield return null;
                        Check(session.matchResult != null, "Client retains final result after host exit"); session.LeaveSession();
                    }
                }
                while (!Wait(SceneManager.GetActiveScene().name == ThuongMainFlow.MenuScene && !manager.IsListening, "Clean return to MainMenu")) yield return null;
                Check(!ThuongMainFlow.BelongsTo(manager) && Active == null, "Leaving clears handoff and gameplay communications");
                if (Argument("-authority-qa") != null)
                {
                    var freshRoom = VerifyFreshRoomShutdown();
                    while (freshRoom.MoveNext()) yield return freshRoom.Current;
                }
                finished = true; File.AppendAllText(output, "THUONG_COMMUNICATION_FLOW_PASSED checks=" + checks + "\n"); Application.Quit(0);
            }
        }
    }
}
#endif
