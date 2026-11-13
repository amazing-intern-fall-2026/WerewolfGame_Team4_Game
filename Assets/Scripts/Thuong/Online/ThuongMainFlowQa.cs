#if THUONG_MAIN_FLOW_QA
using System;
using System.IO;
using System.Linq;
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
        // Included only in the independent process verification build.
        private sealed class MainFlowQaRunner : MonoBehaviour
        {
            private const string Ack = "thuong.main-flow.qa.result.v1";
            private bool host, began, named, finished;
            private string output;
            private NetworkManager manager;
            private int stage, round, acknowledgedRound, readyEpoch;
            private Scene lastLobbyScene;
            private ThuongOnlineSession readySession;
            private ThuongOnlineSession emoteSentSession, emoteCheckedSession;
            private double deadline, next;
            private string LocalName => host ? "Chủ phòng QA" : "Người chơi QA";
            private string ExpectedMap => round == 1 ? "harvest_market_expanded" : "mist_swamp_expanded";

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
            private static void Install()
            {
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-communication-qa") >= 0) return;
                if (Argument("-flow-role") == null) return;
                var runner = new GameObject("Main Flow QA Runner").AddComponent<MainFlowQaRunner>();
                DontDestroyOnLoad(runner.gameObject);
            }

            private static string Argument(string key)
            {
                var args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, key);
                return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
            }

            private void Awake()
            {
                host = Argument("-flow-role") == "host";
                output = Argument("-flow-output");
                deadline = Time.realtimeSinceStartupAsDouble + 150;
                File.WriteAllText(output, "START " + (host ? "HOST" : "CLIENT") + "\n");
            }

            private void Check(bool value, string description)
            {
                if (!value) throw new Exception(description);
                File.AppendAllText(output, "PASS " + description + "\n");
            }

            private void Update()
            {
                if (finished) return;
                try { Tick(); }
                catch (Exception error)
                {
                    finished = true;
                    File.AppendAllText(output, "FAIL stage=" + stage + " round=" + round + " " + error + "\n");
                    Debug.LogException(error);
                    Application.Quit(1);
                }
            }

            private void Tick()
            {
                if (Time.realtimeSinceStartupAsDouble > deadline)
                    throw new TimeoutException("Main flow timeout; scene=" + SceneManager.GetActiveScene().name +
                        "; session=" + (Active != null ? Active.status : "none"));
                if (!began)
                {
                    manager = NetworkManager.Singleton;
                    if (manager == null) return;
                    var transport = manager.GetComponent<UnityTransport>();
                    transport.SetConnectionData("127.0.0.1", ushort.Parse(Argument("-flow-port")), "0.0.0.0");
                    RoomManager.CurrentRoomCode = "004281";
                    RoomManager.CurrentVoiceChannelId = "qa-main-flow-004281";
                    RoomManager.IsHostOfCurrentRoom = host;
                    Check(host ? manager.StartHost() : manager.StartClient(), "Original main menu NetworkManager started");
                    if (host)
                    {
                        manager.CustomMessagingManager.RegisterNamedMessageHandler(Ack, (sender, reader) =>
                        {
                            reader.ReadValueSafe(out int value);
                            if (sender != NetworkManager.ServerClientId) acknowledgedRound = value;
                        });
                        Check(manager.SceneManager.LoadScene(ThuongMainFlow.LobbyScene, LoadSceneMode.Single) ==
                            SceneEventProgressStatus.Started, "Main lobby loads through NGO");
                    }
                    began = true;
                    return;
                }

                CheckManager();
                string scene = SceneManager.GetActiveScene().name;
                if (scene == ThuongMainFlow.LobbyScene && LobbyManager.Instance != null && LobbyManager.Instance.IsSpawned)
                {
                    if (lastLobbyScene != SceneManager.GetActiveScene())
                    {
                        lastLobbyScene = SceneManager.GetActiveScene();
                        named = false;
                        if (!host) stage = 0;
                        next = Time.realtimeSinceStartupAsDouble + 1;
                    }
                    if (!named)
                    {
                        var input = FindFirstObjectByType<TMP_InputField>();
                        Check(input != null, "Lobby player name input exists");
                        input.text = LocalName;
                        LobbyManager.Instance.SubmitPlayerName();
                        named = true;
                    }
                    if (host && (stage == 0 || stage == 5) && LobbyManager.CurrentPlayerCount == 2 &&
                        manager.ConnectedClientsIds.Any(id => id != NetworkManager.ServerClientId &&
                            NetworkGamePlayerNameSetup.GetName(id) == "Người chơi QA") &&
                        Time.realtimeSinceStartupAsDouble >= next)
                    {
                        Check(ThuongMainFlow.GameplayAvailable, "Thuong scene is included in normal build");
                        round++;
                        LobbyManager.Instance.StartGame();
                        stage = 1;
                        Check(ThuongMainFlow.Pending, "Lobby captured handoff roster before loading");
                    }
                    return;
                }

                var session = Active;
                if (scene == ThuongOnlineBootstrap.SceneName && session != null && session.connected)
                {
                    if (stage == 1 && host && session.AllMapsReady)
                    {
                        Check(session.IsMainFlowSession, "Thuong adopted original connection");
                        Check(session.serverPlayers.Count == 2 && session.serverPlayers.Values.Any(p => p.Name == "Chủ phòng QA") &&
                            session.serverPlayers.Values.Any(p => p.Name == "Người chơi QA"), "Unicode names and exact roster preserved");
                        Check(session.SetHostMapSelection(false, ExpectedMap, round == 1 ? GameEventType.Fog : GameEventType.None),
                            "Host can choose map and event after main lobby");
                        stage = 2;
                    }
                    if (host && stage == 2 && session.AllMapsReady)
                    {
                        session.AssignLobbyRolesOnServer();
                        Check(session.rolesAssigned && session.roles.playerRoles.Count == 2, "Only Thuong role assignment runs");
                        stage = 3;
                    }
                    if (session.HasLocalRole && !session.matchStarted &&
                        (readySession != session || readyEpoch != session.privateMatchEpoch))
                    {
                        session.ViewLocalRole();
                        session.ReadLocalRole();
                        Check(session.CanToggleLobbyReady, "Local role read enables Ready");
                        session.ToggleReady();
                        readySession = session;
                        readyEpoch = (int)session.privateMatchEpoch;
                    }
                    if (host && stage == 3 && session.serverPlayers.Values.All(p => p.RevealRead && p.Ready))
                    {
                        session.StartMatchOnServer();
                        Check(session.matchStarted && session.game.currentState == GameState.Day, "All players enter Thuong gameplay after Ready");
                        stage = 4;
                        next = Time.realtimeSinceStartupAsDouble + 1;
                    }
                    if (session.matchStarted && session.visibleState == GameState.Day && !host && round < 1)
                    {
                        round = 1;
                        Check(session.SelectedMap.mapId == "harvest_market_expanded", "Client received selected first map");
                    }
                    if (session.matchStarted && session.visibleState == GameState.Day && emoteSentSession != session)
                    {
                        Check(session.TrySendEmote(host ? 1 : 3), "Emote can be sent after lobby handoff");
                        emoteSentSession = session;
                    }
                    if (session.matchStarted && session.emoteStates.Count == 2 && emoteCheckedSession != session)
                    {
                        Check(session.emoteStates.Values.Any(value => value.Id == 1) &&
                            session.emoteStates.Values.Any(value => value.Id == 3), "Both peers receive emotes in main flow");
                        emoteCheckedSession = session;
                    }
                    if (host && stage == 4 && Time.realtimeSinceStartupAsDouble >= next)
                    {
                        if (emoteCheckedSession != session) return;
                        session.tasks.progress = 100;
                        session.EvaluateOnlineWin(false);
                        Check(session.matchResult != null, "Normal win evaluation produces final result");
                        stage = 6;
                    }
                    if (!host && session.matchResult != null && stage != 6)
                    {
                        round = session.SelectedMap.mapId == "harvest_market_expanded" ? 1 : 2;
                        Check(session.matchResult.Players.Count == 2, "Client receives complete final result");
                        using var writer = new FastBufferWriter(8, Allocator.Temp);
                        writer.WriteValueSafe(round);
                        manager.CustomMessagingManager.SendNamedMessage(Ack, NetworkManager.ServerClientId, writer);
                        stage = 6;
                    }
                    if (host && stage == 6 && acknowledgedRound == round)
                    {
                        if (round == 1)
                        {
                            Check(session.ReturnToMainLobby(), "Host returns whole room to main lobby without reconnecting");
                            stage = 5;
                        }
                        else
                        {
                            Check(ThuongMainFlow.RoomCode == "004281", "Room code preserved across replay");
                            session.LeaveSession();
                            stage = 7;
                        }
                    }
                }
                if (!host && scene == ThuongMainFlow.LobbyScene) stage = 0;
                if (!host && session != null && session.matchResult != null && !manager.IsListening && !session.mainFlowLeaving)
                {
                    Check(round == 2, "Final result remains visible after host exits");
                    session.LeaveSession();
                    stage = 7;
                }
                if (stage == 7 && scene == ThuongMainFlow.MenuScene && !manager.IsListening)
                {
                    Check(!ThuongMainFlow.Pending && !ThuongMainFlow.BelongsTo(manager), "Leaving clears handoff state and keeps main menu available");
                    finished = true;
                    File.AppendAllText(output, "THUONG_MAIN_FLOW_PASSED\n");
                    Application.Quit(0);
                }
            }

            private void CheckManager()
            {
                if (manager == null || NetworkManager.Singleton != manager)
                    throw new Exception("Original NetworkManager was replaced");
            }
        }
    }
}
#endif
