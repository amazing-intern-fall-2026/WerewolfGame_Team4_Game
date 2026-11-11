#if THUONG_MAIN_FLOW_QA
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private sealed partial class CommunicationFlowQa
        {
            private int leakedVotePackets;
            private uint previousAuthorityEpoch;
            private void VerifyRoundEpoch(ThuongOnlineSession session)
            {
                Check(session.matchEpoch > previousAuthorityEpoch, "A new match epoch survives the shared-lobby scene round trip");
                previousAuthorityEpoch = session.matchEpoch;
            }
            private static Dictionary<RoleType, int> SelectedRoles => new()
            { [RoleType.DogSpirit] = 1, [RoleType.Seer] = 1, [RoleType.Villager] = 1 };
            private static Dictionary<RoleType, int> UnsupportedRoles => new()
            { [RoleType.DogSpirit] = 1, [RoleType.Hunter] = 1, [RoleType.Villager] = 1 };

            private void ConfigureAuthorityRoles(int round)
            {
                var config = NetworkRoleLobbyConfig.Instance;
                Check(config != null, "Main lobby role configuration available");
                if (round == 1)
                    foreach (var role in RoleCatalog.AllConcreteRoles)
                        config.SetRoleAmount(role, SelectedRoles.TryGetValue(role, out int amount) ? amount : 0);
                else
                    Check(config.HasCustomizedRoles && SelectedRoles.All(entry => config.GetRoleAmount(entry.Key) == entry.Value),
                        "Host selection survives gameplay, reconnect and return to main lobby");
            }

            private void VerifyRoleModeConstraints(ThuongOnlineSession session)
            {
                var adapter = ThuongOnlineRoleConfigAdapter.Instance;
                Check(adapter != null && adapter.IsCustomized && NetworkGameRoleSetup.GetTotalRoleAmount() == 3,
                    "Configured quantities survive main-lobby handoff");
                adapter.HostSaveConfig(UnsupportedRoles);
                Check(!session.SetRoomRules(new ThuongRoomRules { mode = ThuongMatchMode.RealtimeProximity, preset = ThuongRolePreset.Basic }),
                    "Mode change refuses a saved Hunter configuration");
                adapter.HostSaveConfig(SelectedRoles);
                Check(session.SetRoomRules(new ThuongRoomRules { mode = ThuongMatchMode.RealtimeProximity, preset = ThuongRolePreset.Basic }),
                    "Realtime mode accepts supported configured roles");
                adapter.HostSaveConfig(UnsupportedRoles);
                adapter.HostSetRoleAmount(RoleType.Hunter, 1);
                Check(adapter.GetRoleAmount(RoleType.Hunter) == 0 && NetworkGameRoleSetup.GetRoleAmount(RoleType.Seer) == 1,
                    "Host cannot save or increment an unsupported realtime role");
                NetworkGameRoleSetup.Save(UnsupportedRoles.Select(entry => new RoleAmountData(entry.Key, entry.Value)).ToList());
                session.AssignLobbyRolesOnServer();
                Check(!session.rolesAssigned && !session.roomLocked, "Assignment validates again before locking or revealing roles");
                adapter.HostSaveConfig(SelectedRoles);
                Check(session.SetRoomRules(new ThuongRoomRules()), "Classic mode restored for communication regression");
            }

            private int PlayerId(ThuongOnlineSession session, int member)
            {
                string name = member == 0 ? "Chủ phòng QA" : member == 1 ? "Người chơi QA" : "Người nối lại QA";
                return session.publicPlayers.First(player => player.Name == name).PlayerId;
            }

            private void SendStaleVote(ThuongOnlineSession session, int target)
            {
                using var writer = new FastBufferWriter(32, Allocator.Temp);
                writer.WriteValueSafe(0x70000000u); writer.WriteValueSafe(session.matchEpoch - 1);
                writer.WriteValueSafe((byte)ThuongOnlineAction.Vote); writer.WriteValueSafe(target);
                writer.WriteValueSafe(-1); writer.WriteValueSafe(session.visibleDay); writer.WriteValueSafe((byte)session.visiblePhase);
                manager.CustomMessagingManager.SendNamedMessage(ThuongOnlineMessages.Action, NetworkManager.ServerClientId, writer);
            }

            private IEnumerator VerifyFreshRoomShutdown()
            {
                while (!Wait(NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.ShutdownInProgress,
                    "Fresh room transport is stopped")) yield return null;
                yield return new WaitForSecondsRealtime(.5f);
                manager = NetworkManager.Singleton;
                manager.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>().SetConnectionData("127.0.0.1", ushort.Parse(Argument("-flow-port")), "0.0.0.0");
                RoomManager.CurrentRoomCode = "004282"; RoomManager.CurrentVoiceChannelId = "qa-fresh-room";
                RoomManager.IsHostOfCurrentRoom = Host;
                if (Host) { acknowledgements.Clear(); command = 0; }
                Check(Host ? manager.StartHost() : manager.StartClient(), "A new room starts after leaving the previous room");
                RegisterQaMessages();
                if (Host)
                    Check(manager.SceneManager.LoadScene(ThuongMainFlow.LobbyScene, UnityEngine.SceneManagement.LoadSceneMode.Single) == SceneEventProgressStatus.Started,
                        "Fresh main lobby loads");
                while (!Wait(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == ThuongMainFlow.LobbyScene &&
                    LobbyManager.Instance != null && LobbyManager.Instance.IsSpawned, "Fresh lobby arrived")) yield return null;
                var nameInput = (TMPro.TMP_InputField)typeof(LobbyManager).GetField("playerNameInput",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(LobbyManager.Instance);
                nameInput.text = Name; LobbyManager.Instance.SubmitPlayerName(); Acknowledge(1000);
                if (Host)
                {
                    while (!Wait(HasAck(1000) && manager.ConnectedClientsIds.Count == 3 && LobbyManager.CurrentPlayerCount == 3, "Fresh lobby has all clients")) yield return null;
                    Check(!NetworkGameRoleSetup.HasSetup() && !ThuongOnlineRoleConfigAdapter.Instance.IsCustomized,
                        "A new room clears the previous host's role selection");
                    LobbyManager.Instance.StartGame();
                }
                while (!Wait(Active != null && Active.connected && Active.IsMainFlowSession, "Fresh gameplay session adopted")) yield return null;
                var session = Active;
                if (Host)
                {
                    while (!Wait(session.AllMapsReady && session.serverPlayers.Count == 3, "Fresh map acknowledgements")) yield return null;
                    session.AssignLobbyRolesOnServer();
                }
                while (!Wait(session.HasLocalRole, "Fresh private role")) yield return null;
                session.ViewLocalRole(); session.ReadLocalRole(); session.ToggleReady();
                if (Host)
                {
                    while (!Wait(session.serverPlayers.Values.All(member => member.Ready && member.RevealRead), "Fresh match ready")) yield return null;
                    session.StartMatchOnServer();
                }
                while (!Wait(session.matchStarted, "Fresh match started")) yield return null;
                Acknowledge(1001);
                if (Host)
                {
                    while (!Wait(HasAck(1001), "Clients started fresh match")) yield return null;
                    session.LeaveToMainMenu();
                }
                while (!Wait(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == ThuongMainFlow.MenuScene && !manager.IsListening,
                    "Genuine host exit returns the unfinished room to menu")) yield return null;
                Check(Active == null && !ThuongMainFlow.BelongsTo(manager),
                    "Recreated network handlers accept a genuine host exit and clean the unfinished room");
            }

            private IEnumerator VerifyAuthority(ThuongOnlineSession session, int phase)
            {
                leakedVotePackets = 0;
                if (!Host)
                    manager.CustomMessagingManager.RegisterNamedMessageHandler("loi.online.vote_sync.v1", (sender, reader) => leakedVotePackets++);
                Acknowledge(phase + 20);
                if (Host)
                {
                    while (!Wait(HasAck(phase + 20), "Authority observers installed")) yield return null;
                    session.game.enabled = false; DayTimer.Instance.StopTimer();
                    session.game.currentState = GameState.Voting; session.game.currentPhase = GamePhase.Voting;
                    VoteManager.Instance.StartVote(); session.SendAllPrivateStates(); session.BroadcastSnapshot();
                    Signal(phase + 21);
                }
                while (!Wait(command >= phase + 21 && session.visibleState == GameState.Voting, "Controlled online voting")) yield return null;

                if (slot == 1)
                {
                    using (var writer = new FastBufferWriter(16, Allocator.Temp))
                    {
                        writer.WriteValueSafe(PlayerId(session, 0)); writer.WriteValueSafe(PlayerId(session, 2));
                        manager.CustomMessagingManager.SendNamedMessage("loi.online.vote_request.v1", NetworkManager.ServerClientId, writer);
                    }
                    using (var writer = new FastBufferWriter(8, Allocator.Temp))
                    {
                        writer.WriteValueSafe((byte)1); writer.WriteValueSafe(session.matchEpoch);
                        manager.CustomMessagingManager.SendNamedMessage(NetworkReturnToMenuController.HostSessionTerminatedMessage, NetworkManager.ServerClientId, writer);
                    }
                    using (var empty = new FastBufferWriter(1, Allocator.Temp))
                        manager.CustomMessagingManager.SendNamedMessage(ThuongOnlineMessages.Action, NetworkManager.ServerClientId, empty);
                }
                Acknowledge(phase + 21);
                if (Host)
                {
                    while (!Wait(HasAck(phase + 21), "Forged requests delivered")) yield return null;
                    yield return new WaitForSecondsRealtime(.25f);
                    Check(manager.IsListening && session.serverPlayers.Count == 3, "Client cannot terminate host with a forged host-exit message");
                    Check(VoteManager.Instance.GetVotedTarget(PlayerId(session, 0)) == -1 && !session.players.GetplayerByID(PlayerId(session, 0)).hasVoted,
                        "Legacy request cannot cast a vote under the host player ID");
                    NetworkReturnToMenuController.BroadcastHostSessionTerminated(manager, session.matchEpoch - 1);
                    Signal(phase + 22);
                }
                while (!Wait(command >= phase + 22, "Valid votes after rejected requests")) yield return null;
                yield return new WaitForSecondsRealtime(.25f);
                Check(session.MatchStarted && session.OwnsNetwork(manager), "Old-epoch host-exit notification leaves current match running");
                Check(!session.CanVoteFor(session.localPlayerId), "Self-vote disabled through production UI policy");
                if (Host) session.RequestVote(PlayerId(session, 1));
                else if (slot == 1) session.RequestVote(PlayerId(session, 0));
                else session.RequestNoEliminationVote();
                while (!Wait(slot == 2 ? session.LocalVotedNoElimination : session.LocalConfirmedVoteTarget == PlayerId(session, Host ? 1 : 0),
                    "Own private vote acknowledgement")) yield return null;
                Check(session.CanVoteNoElimination, "Voting remains available for changing an accepted vote");
                Acknowledge(phase + 22);
                if (Host)
                {
                    while (!Wait(HasAck(phase + 22), "Every private vote acknowledged")) yield return null;
                    Check(session.players.players.All(player => player.hasVoted), "Vote history remains recorded without resetting hasVoted each frame");
                    Check(VoteManager.Instance.GetVotedTarget(PlayerId(session, 1)) == PlayerId(session, 0), "Server attributes client vote to its authenticated connection");
                    Signal(phase + 23);
                }
                while (!Wait(command >= phase + 23, "Revote and previous-epoch request")) yield return null;
                if (Host) session.RequestNoEliminationVote();
                else if (slot == 1) { SendStaleVote(session, PlayerId(session, 0)); session.RequestVote(PlayerId(session, 2)); }
                else session.RequestVote(PlayerId(session, 0));
                while (!Wait(Host ? session.LocalVotedNoElimination : session.LocalConfirmedVoteTarget == PlayerId(session, slot == 1 ? 2 : 0),
                    "Changed vote confirmed privately")) yield return null;
                Check(true, "Changing player/skip choices replaces the previous ballot; stale epoch cannot consume the new request sequence");
                Acknowledge(phase + 23);
                if (Host)
                {
                    while (!Wait(HasAck(phase + 23), "Changed votes acknowledged")) yield return null;
                    Signal(phase + 24);
                }
                while (!Wait(command >= phase + 24, "Final no-elimination ballot")) yield return null;
                if (slot == 2) session.RequestNoEliminationVote();
                while (!Wait(slot != 2 || session.LocalVotedNoElimination, "Player vote changed back to skip")) yield return null;
                Check(leakedVotePackets == 0, "No broadcast of per-player vote choices reaches clients");
                if (!Host)
                {
                    Check(session.players.players.Where(player => player.playerID != session.localPlayerId).All(player => !player.hasVoted),
                        "Other players' ballot state stays absent from the client model");
                    int before = ThuongOnlineRoleConfigAdapter.Instance.GetRoleAmount(RoleType.Hunter);
                    ThuongOnlineRoleConfigAdapter.Instance.HostSetRoleAmount(RoleType.Hunter, before + 1);
                    Check(ThuongOnlineRoleConfigAdapter.Instance.GetRoleAmount(RoleType.Hunter) == before, "Client cannot edit configured role quantities");
                }
                Acknowledge(phase + 24);
                if (Host)
                {
                    while (!Wait(HasAck(phase + 24), "Final ballots acknowledged")) yield return null;
                    VoteManager.Instance.ResolveVote();
                    var result = VoteManager.Instance.LastResolution;
                    Check(result.NoEliminationWon && result.NoEliminationVotes == 2 && result.Tallies.Values.Sum() == 1 && result.Eliminated == null,
                        "Two skip ballots and one changed player ballot resolve exactly once without duplicate votes");
                    session.BroadcastSnapshot(); Signal(phase + 25);
                }
                while (!Wait(command >= phase + 25 && session.voteNoEliminationWon && session.voteNoEliminationVotes == 2,
                    "Public vote result")) yield return null;
                Check(true, "Host and both clients receive identical public no-elimination result");
                Acknowledge(phase + 25);
                if (Host)
                {
                    while (!Wait(HasAck(phase + 25), "Public result acknowledged")) yield return null;
                    session.game.currentState = GameState.Day; session.game.currentPhase = GamePhase.Task;
                    session.BroadcastSnapshot(); Signal(phase + 26);
                }
                while (!Wait(command >= phase + 26 && session.visibleState == GameState.Day, "Authority regression complete")) yield return null;
                Acknowledge(phase + 26);
                if (Host) while (!Wait(HasAck(phase + 26), "Peers leave authority regression")) yield return null;
            }
        }
    }
}
#endif
