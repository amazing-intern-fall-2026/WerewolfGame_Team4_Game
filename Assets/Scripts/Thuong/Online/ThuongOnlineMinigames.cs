using System;
using System.Collections.Generic;
using System.Linq;
using Thuong.Minigames;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private readonly ThuongMinigameAuthority minigameAuthority = new();
        private readonly Dictionary<ulong, uint> minigameSequences = new();
        private readonly Dictionary<ulong, double> minigameBeginTimes = new();
        private ThuongMinigameCatalog minigameCatalog;
        private ThuongMinigameWindow minigameWindow;
        private ThuongMinigameState localMinigame;
        private uint minigameSequence;
        private double nextMinigameState, minigamePendingUntil;
        private bool minigamesReady;
        private readonly HashSet<ulong> dismissedAttempts = new();
        public bool IsPlayingTask => localMinigame != null || minigamePendingUntil > MinigameTime;
        private double MinigameTime => network != null && network.IsListening ? network.ServerTime.Time : Time.unscaledTimeAsDouble;
        private bool LocalMinigameInvalid => localMinigame != null &&
            (!connected || !matchStarted || visibleState != GameState.Day || visibleDay != localMinigame.day || matchResult != null ||
             localMinigame.stationId < 0 || localMinigame.stationId >= 16 ||
             (activeTaskMask & (1 << localMinigame.stationId)) == 0 ||
             localMinigame.result != ThuongMinigameResult.Succeeded && (completedTaskMask & (1 << localMinigame.stationId)) != 0 ||
             publicPlayers.Any(player => player.PlayerId == localPlayerId && !player.Alive));

        private void PrepareMinigames()
        {
            minigameCatalog = Resources.Load<ThuongMinigameCatalog>(ThuongMinigameCatalog.ResourcePath);
            string error = "Không tìm thấy catalog minigame. Chạy Tools/Thuong/Minigames/Install Assets.";
            minigamesReady = minigameCatalog != null && minigameCatalog.Validate(out error);
            if (!minigamesReady) { Debug.LogError("[Thuong Minigames] " + error); return; }
            foreach (var extra in minigameCatalog.additionalStations)
            {
                // These stations exist only in the online scene, leaving both offline prototypes intact.
                var root = Instantiate(minigameCatalog.stationPrefab, extra.position, Quaternion.identity);
                SceneManager.MoveGameObjectToScene(root, game.gameObject.scene);
                root.name = "Minigame station - " + minigameCatalog.Find(extra.task).taskId;
                var station = root.GetComponent<TaskStation>();
                station.task = extra.task; station.player = null; station.enabled = false;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.text = extra.task.taskName;
                var art = root.transform.Find("Medieval station art");
                if (art != null)
                {
                    var renderer = art.GetComponentInChildren<SpriteRenderer>();
                    var entry = minigameCatalog.Find(extra.task);
                    if (renderer != null) renderer.sprite = entry.subject != null ? entry.subject : minigameCatalog.trapCover;
                }
                if (!tasks.allTasks.Contains(extra.task)) tasks.allTasks.Add(extra.task);
            }
            if (FindObjectsByType<TaskStation>().Length > 16)
            {
                minigamesReady = false;
                Debug.LogError("[Thuong Minigames] Số trạm vượt mask 16 bit; không thể mở nhiệm vụ.");
            }
        }

        private void RegisterMinigameMessages()
        {
            network.CustomMessagingManager.RegisterNamedMessageHandler(ThuongMinigameWire.Request, ReceiveMinigameRequest);
            network.CustomMessagingManager.RegisterNamedMessageHandler(ThuongMinigameWire.State, ReceiveMinigameState);
        }
        private void UnregisterMinigameMessages()
        {
            network?.CustomMessagingManager?.UnregisterNamedMessageHandler(ThuongMinigameWire.Request);
            network?.CustomMessagingManager?.UnregisterNamedMessageHandler(ThuongMinigameWire.State);
        }

        private void RequestTaskMinigame(int station)
        {
            if (IsPlayingTask) return;
            if (!minigamesReady) { localActionFeedback = "Chưa có cấu hình minigame hợp lệ."; return; }
            if (minigameCatalog.Find(stations[station].task) == null)
            { SendAction(ThuongOnlineAction.CompleteTask, station); return; }
            minigamePendingUntil = MinigameTime + 3;
            SendMinigameRequest(0, station, 0, default);
        }

        private void SendMinigameInput(ThuongMinigameInput input)
        {
            if (localMinigame?.result == ThuongMinigameResult.Playing)
                SendMinigameRequest(1, localMinigame.stationId, localMinigame.attemptId, input);
        }

        private void SendMinigameRequest(byte operation, int station, ulong attempt, ThuongMinigameInput input)
        {
            if (!connected || network == null || !network.IsListening) return;
            uint seq = ++minigameSequence;
            if (network.IsServer)
            { ProcessMinigameRequest(network.LocalClientId, seq, operation, visibleDay, station, attempt, input); return; }
            using var writer = new FastBufferWriter(64, Allocator.Temp);
            writer.WriteValueSafe(seq); writer.WriteValueSafe(operation); writer.WriteValueSafe(visibleDay);
            writer.WriteValueSafe(station); writer.WriteValueSafe(attempt); writer.WriteValueSafe((byte)input.action);
            writer.WriteValueSafe(input.index); writer.WriteValueSafe(input.value);
            network.CustomMessagingManager.SendNamedMessage(ThuongMinigameWire.Request, NetworkManager.ServerClientId, writer);
        }

        private void ReceiveMinigameRequest(ulong sender, FastBufferReader reader)
        {
            if (!network.IsServer || !reader.TryBeginRead(ThuongMinigameWire.RequestBytes)) return;
            reader.ReadValueSafe(out uint seq); reader.ReadValueSafe(out byte operation); reader.ReadValueSafe(out int day);
            reader.ReadValueSafe(out int station); reader.ReadValueSafe(out ulong attempt); reader.ReadValueSafe(out byte action);
            reader.ReadValueSafe(out int index); reader.ReadValueSafe(out float value);
            ProcessMinigameRequest(sender, seq, operation, day, station, attempt, new ThuongMinigameInput((ThuongMinigameAction)action, index, value));
        }

        private bool CanPlayMinigame(ThuongOnlineServerPlayer member, int station, int day, out string reason)
        {
            reason = "Nhiệm vụ không còn khả dụng.";
            if (busy || !minigamesReady || !matchStarted || matchResult != null || game == null ||
                game.currentState != GameState.Day || game.currentDay != day || tasks == null ||
                players?.GetplayerByID(member.PlayerId)?.isAlive != true) return false;
            if (station < 0 || station >= stations.Length || station >= 16) return false;
            var target = stations[station];
            if (target == null || target.task == null || !tasks.currentTasks.Contains(target.task) || tasks.IsCompleted(target.task)) return false;
            if (Vector2.Distance(member.Position, target.transform.position) > target.interactionRadius + .25f)
            { reason = "Bạn đang đứng quá xa trạm nhiệm vụ."; return false; }
            reason = null; return true;
        }

        private void ProcessMinigameRequest(ulong sender, uint seq, byte operation, int day, int station,
            ulong attemptId, ThuongMinigameInput input)
        {
            if (!network.IsServer || !serverPlayers.TryGetValue(sender, out var member) || seq == 0 ||
                minigameSequences.TryGetValue(sender, out uint last) && seq <= last) return;
            minigameSequences[sender] = seq;
            if (operation == 2)
            {
                SendMinigameState(sender, minigameAuthority.Cancel(sender, attemptId, "Đã hủy nhiệm vụ."));
                member.Input = Vector2.zero;
                return;
            }
            if (!CanPlayMinigame(member, station, day, out string reason))
            {
                var cancelled = minigameAuthority.Cancel(sender, attemptId, reason);
                SendMinigameState(sender, cancelled);
                if (operation == 0) RejectMinigameBegin(member, reason);
                return;
            }
            double now = MinigameTime;
            if (operation == 0)
            {
                if (minigameBeginTimes.TryGetValue(sender, out double lastBegin) && now - lastBegin < .35) return;
                minigameBeginTimes[sender] = now;
                var entry = minigameCatalog.Find(stations[station].task);
                if (entry == null) { RejectMinigameBegin(member, "Nhiệm vụ này không dùng minigame."); return; }
                if (!minigameAuthority.TryBegin(sender, entry.taskId, station, day, entry.kind, entry.rules,
                    minigameCatalog.IngredientScores(), now, now + (DayTimer.Instance?.TimeRemaining ?? 0), out var state, out reason))
                { RejectMinigameBegin(member, reason); return; }
                member.Input = Vector2.zero;
                SendMinigameState(sender, state);
            }
            else if (operation == 1)
            {
                var current = minigameAuthority.Get(sender);
                if (current == null || current.stationId != station || current.day != day) return;
                var state = minigameAuthority.Apply(sender, attemptId, seq, input, now);
                FinishMinigame(sender, state);
            }
        }

        private void RejectMinigameBegin(ThuongOnlineServerPlayer member, string reason)
        {
            Reply(member, reason);
            SendMinigameState(member.ClientId, new ThuongMinigameState { result = ThuongMinigameResult.Cancelled, message = reason });
        }

        private void FinishMinigame(ulong owner, ThuongMinigameState state)
        {
            if (state == null || !serverPlayers.TryGetValue(owner, out var member)) return;
            if (state.result == ThuongMinigameResult.Succeeded)
            {
                // Authority consumes the attempt before this point. A duplicate packet cannot reach this reward twice.
                if (!CanPlayMinigame(member, state.stationId, state.day, out _) ||
                    minigameCatalog.Find(stations[state.stationId].task)?.taskId != state.taskId ||
                    !tasks.TryCompleteTask(member.PlayerId, stations[state.stationId].task))
                { state.result = ThuongMinigameResult.Cancelled; state.message = "Lượt đã hết hiệu lực; không cộng tiến độ."; }
                else state.message = "Hoàn thành nhiệm vụ!";
            }
            if (state.result != ThuongMinigameResult.Playing) { member.Input = Vector2.zero; Reply(member, state.message); }
            SendMinigameState(owner, state);
            if (state.result != ThuongMinigameResult.Playing) BroadcastSnapshot();
        }

        private void UpdateMinigames()
        {
            if (LocalMinigameInvalid) CloseLocalMinigame();
            if (network == null || !network.IsServer || !network.IsListening) return;
            foreach (ulong owner in minigameAuthority.Owners())
            {
                var state = minigameAuthority.Get(owner);
                if (!serverPlayers.TryGetValue(owner, out var member) || !CanPlayMinigame(member, state.stationId, state.day, out _))
                    SendMinigameState(owner, minigameAuthority.Cancel(owner, state.attemptId, "Lượt nhiệm vụ đã kết thúc."));
            }
            foreach (var state in minigameAuthority.Tick(MinigameTime))
            {
                // Tick removes terminal attempts, so retain ownership separately from the exported client view.
                if (attemptOwners.TryGetValue(state.attemptId, out ulong client)) FinishMinigame(client, state);
            }
            if (MinigameTime < nextMinigameState) return;
            nextMinigameState = MinigameTime + .1;
            foreach (ulong owner in minigameAuthority.Owners()) SendMinigameState(owner, minigameAuthority.Get(owner));
        }

        private readonly Dictionary<ulong, ulong> attemptOwners = new();
        private void SendMinigameState(ulong owner, ThuongMinigameState state)
        {
            if (state == null) return;
            if (state.result == ThuongMinigameResult.Playing) attemptOwners[state.attemptId] = owner;
            else attemptOwners.Remove(state.attemptId);
            if (network == null || !network.IsListening) return;
            if (owner == network.LocalClientId && network.IsHost) { ApplyMinigameState(state); return; }
            if (!network.ConnectedClientsIds.Contains(owner)) return;
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            ThuongMinigameWire.Write(writer, state);
            network.CustomMessagingManager.SendNamedMessage(ThuongMinigameWire.State, owner, writer);
        }

        private void ReceiveMinigameState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || network.IsServer) return;
            try { ApplyMinigameState(ThuongMinigameWire.Read(reader)); }
            catch (Exception exception) { Debug.LogWarning("[Thuong Minigames] Invalid state: " + exception.Message); }
        }

        private void ApplyMinigameState(ThuongMinigameState state)
        {
            minigamePendingUntil = 0;
            if (state.attemptId == 0) { localActionFeedback = state.message; return; }
            if (dismissedAttempts.Contains(state.attemptId)) return;
            if (!connected || !matchStarted || visibleState != GameState.Day || visibleDay != state.day || matchResult != null) return;
            var entry = minigameCatalog?.Find(state.taskId);
            if (entry == null || entry.kind != state.kind) { localMinigame = state; CancelLocalMinigame(); return; }
            try
            {
                if (minigameWindow == null)
                {
                    var root = new GameObject("Thuong Minigame Window");
                    SceneManager.MoveGameObjectToScene(root, game.gameObject.scene);
                    minigameWindow = root.AddComponent<ThuongMinigameWindow>();
                    minigameWindow.Open(minigameCatalog, entry, state, sceneHud?.phaseText?.font,
                        SendMinigameInput, CancelLocalMinigame, RetryMinigame, () => MinigameTime);
                }
                else if (localMinigame?.attemptId == state.attemptId) minigameWindow.Apply(state);
                else return;
                localMinigame = state;
            }
            catch (Exception exception)
            {
                localMinigame = state;
                CancelLocalMinigame();
                Debug.LogException(exception);
            }
        }

        private void CancelLocalMinigame()
        {
            var state = localMinigame;
            CloseLocalMinigame();
            if (state?.result == ThuongMinigameResult.Playing) SendMinigameRequest(2, state.stationId, state.attemptId, default);
        }
        private void RetryMinigame()
        {
            if (localMinigame == null || localMinigame.result == ThuongMinigameResult.Playing) return;
            int station = localMinigame.stationId;
            CloseLocalMinigame(); RequestTaskMinigame(station);
        }
        private void CloseLocalMinigame()
        {
            if (localMinigame != null) dismissedAttempts.Add(localMinigame.attemptId);
            localMinigame = null; minigamePendingUntil = 0;
            if (minigameWindow != null) { minigameWindow.Dispose(); Destroy(minigameWindow.gameObject); }
            minigameWindow = null;
        }
        private void ResetMinigames()
        {
            CloseLocalMinigame();
            foreach (ulong owner in minigameAuthority.Owners())
            { var state = minigameAuthority.Get(owner); minigameAuthority.Cancel(owner, state.attemptId, "Đã đóng phiên."); }
            attemptOwners.Clear(); minigameSequences.Clear(); minigameBeginTimes.Clear(); dismissedAttempts.Clear();
            minigameSequence = 0;
        }
    }
}
