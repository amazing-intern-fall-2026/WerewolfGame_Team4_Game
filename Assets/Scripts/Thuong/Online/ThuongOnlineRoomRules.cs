using System;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private const string RoomStateMessage = "thuong.online.room-state.v2";
        private ThuongRoomRules roomRules = new();
        private uint rulesRevision = 1, receivedRulesRevision;
        private ulong roomStateRevision;
        private uint publishedRulesRevision, publishedRoomEpoch;
        private int publishedStationCount;
        private GameObject rulesSettingsRoot;
        private Button rulesSettingsButton;
        private TMP_Text roomRulesSummary, rulesHelp;
        private readonly TMP_Text[] durationLabels = new TMP_Text[4];
        private readonly Button[] presetButtons = new Button[3];
        private TMP_Text automaticTimesLabel;
        private TMP_Text modeLabel;
        private ThuongRoomRules draftRules;
        private readonly int[] taskTargets = new int[16], taskDone = new int[16], taskOccupancy = new int[16], taskCapacity = new int[16];
        private ushort personalTaskMask;
        private int taskBoardDay;
        public ThuongRoomRules RoomRules => roomRules.Copy();
        public RoleType[] ConfigurableRoleTypes => roomRules.mode == ThuongMatchMode.RealtimeProximity
            ? new ThuongRoomRules { preset = ThuongRolePreset.Basic }.AllowedRoles : RoleCatalog.AllConcreteRoles;

        private bool CanConfigureRules => network != null && (!network.IsListening || network.IsServer) &&
            !busy && !roomLocked && !rolesAssigned && !matchStarted;

        private bool SetRoomRules(ThuongRoomRules candidate)
        {
            if (!CanConfigureRules || candidate == null || !candidate.IsValid) return false;
            if (candidate.mode == ThuongMatchMode.RealtimeProximity && NetworkGameRoleSetup.HasSetup() &&
                !RolePoolBuilder.TryBuildConfigured(ConnectedPlayerCount, NetworkGameRoleSetup.GetRoles(),
                    new ThuongRoomRules { preset = ThuongRolePreset.Basic }.AllowedRoles, out _, out string error))
            { status = error + " Hãy chỉnh vai trò trước khi đổi chế độ."; return false; }
            roomRules = candidate.Copy(); rulesRevision++;
            foreach (var member in serverPlayers.Values) member.Ready = false;
            rosterDirty = true;
            if (network.IsServer) { BroadcastRoster(); BroadcastSnapshot(); }
            return true;
        }

        private void ApplyRoomRulesForMatch()
        {
            var rules = roomRules.Resolve(serverPlayers.Count);
            game.nightDuration = rules.night;
            game.discussionDuration = rules.discussion;
            game.votingDuration = rules.voting;
            var timer = SceneComponent<DayTimer>();
            if (timer != null) timer.dayDuration = rules.day;
            tasks.ConfigureCooperative(serverPlayers.Count);
        }

        private void RegisterRoomMessages() => network.CustomMessagingManager.RegisterNamedMessageHandler(RoomStateMessage, ReceiveRoomState);
        private void UnregisterRoomMessages() => network?.CustomMessagingManager?.UnregisterNamedMessageHandler(RoomStateMessage);

        private void BroadcastRoomState()
        {
            if (leavingScene || network.ShutdownInProgress || network.CustomMessagingManager == null || game == null || tasks == null) return;
            int day = game.currentDay;
            int count = Math.Min(stations.Length, taskTargets.Length);
            bool changed = publishedRulesRevision != rulesRevision || publishedRoomEpoch != matchEpoch ||
                taskBoardDay != day || publishedStationCount != count;
            for (int slot = 0; slot < count; slot++)
            {
                var task = stations[slot].task;
                int target = tasks.TargetFor(task);
                int done = tasks.ContributionsFor(task);
                int capacity = tasks.Cooperative ? Mathf.Min(tasks.CapacityFor(task), Mathf.Max(0, target - done)) : 1;
                string id = minigameCatalog?.Find(task)?.taskId;
                int occupancy = id == null ? 0 : minigameAuthority.Occupancy(day, id);
                changed |= target != taskTargets[slot] || done != taskDone[slot] ||
                    capacity != taskCapacity[slot] || occupancy != taskOccupancy[slot];
                taskTargets[slot] = target;
                taskDone[slot] = done;
                taskCapacity[slot] = capacity;
                taskOccupancy[slot] = occupancy;
            }
            if (changed)
            {
                taskBoardDay = day;
                publishedStationCount = count;
                publishedRulesRevision = rulesRevision;
                publishedRoomEpoch = matchEpoch;
                roomStateRevision++;
            }

            // A personal contribution always changes the shared count, or resets with the board.
            foreach (ulong clientId in network.ConnectedClientsIds)
            {
                if (!serverPlayers.TryGetValue(clientId, out var member) || member.LastRoomStateRevision == roomStateRevision) continue;
                ushort personal = 0;
                for (int slot = 0; slot < count; slot++)
                    if (tasks.HasContributed(member.PlayerId, stations[slot].task)) personal |= (ushort)(1 << slot);
                if (clientId == network.LocalClientId) personalTaskMask = personal;
                else SendRoomState(clientId, personal, count);
                member.LastRoomStateRevision = roomStateRevision;
            }
        }

        private void SendRoomState(ulong clientId, ushort personal, int count)
        {
            using var writer = new FastBufferWriter(512, Allocator.Temp);
            writer.WriteValueSafe(matchEpoch); writer.WriteValueSafe(taskBoardDay); writer.WriteValueSafe(rulesRevision);
            writer.WriteValueSafe((byte)roomRules.preset); writer.WriteValueSafe((byte)roomRules.mode);
            writer.WriteValueSafe(roomRules.automaticTimes);
            writer.WriteValueSafe(roomRules.day); writer.WriteValueSafe(roomRules.night);
            writer.WriteValueSafe(roomRules.discussion); writer.WriteValueSafe(roomRules.voting);
            writer.WriteValueSafe(personal);
            writer.WriteValueSafe((byte)count);
            for (int slot = 0; slot < count; slot++)
            {
                writer.WriteValueSafe((byte)taskTargets[slot]); writer.WriteValueSafe((byte)taskDone[slot]);
                writer.WriteValueSafe((byte)taskOccupancy[slot]); writer.WriteValueSafe((byte)taskCapacity[slot]);
            }
            // Snapshot establishes the epoch and map before this packet can be read.
            network.CustomMessagingManager.SendNamedMessage(RoomStateMessage, clientId, writer,
                NetworkDelivery.ReliableFragmentedSequenced);
        }

        private void ReceiveRoomState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || network.IsServer) return;
            try
            {
                reader.ReadValueSafe(out uint epoch); reader.ReadValueSafe(out int day); reader.ReadValueSafe(out uint revision);
                if (epoch != matchEpoch || revision < receivedRulesRevision) return;
                reader.ReadValueSafe(out byte preset); reader.ReadValueSafe(out byte mode); reader.ReadValueSafe(out bool automatic);
                var rules = new ThuongRoomRules { preset = (ThuongRolePreset)preset, mode = (ThuongMatchMode)mode, automaticTimes = automatic };
                reader.ReadValueSafe(out rules.day); reader.ReadValueSafe(out rules.night);
                reader.ReadValueSafe(out rules.discussion); reader.ReadValueSafe(out rules.voting);
                reader.ReadValueSafe(out ushort personal); reader.ReadValueSafe(out byte count);
                if (!rules.IsValid || count > 16 || reader.Length - reader.Position != count * 4) return;
                var target = new int[16]; var done = new int[16]; var occupancy = new int[16]; var capacity = new int[16];
                for (int slot = 0; slot < count; slot++)
                {
                    reader.ReadValueSafe(out byte t); reader.ReadValueSafe(out byte d);
                    reader.ReadValueSafe(out byte o); reader.ReadValueSafe(out byte c);
                    if (t > 50 || d > t || c > MaxPlayers || o > c) return;
                    target[slot] = t; done[slot] = d; occupancy[slot] = o; capacity[slot] = c;
                }
                roomRules = rules; receivedRulesRevision = revision;
                taskBoardDay = day; personalTaskMask = personal;
                Array.Copy(target, taskTargets, 16); Array.Copy(done, taskDone, 16);
                Array.Copy(occupancy, taskOccupancy, 16); Array.Copy(capacity, taskCapacity, 16);
            }
            catch (Exception exception) when (exception is OverflowException || exception is ArgumentException)
            { Debug.LogWarning("[Thuong room] Gói thiết lập/nhiệm vụ không hợp lệ."); }
        }

        private void ResetTaskBoard()
        {
            publishedRulesRevision = 0;
            personalTaskMask = 0; taskBoardDay = 0; receivedRulesRevision = 0;
            Array.Clear(taskTargets, 0, 16); Array.Clear(taskDone, 0, 16);
            Array.Clear(taskOccupancy, 0, 16); Array.Clear(taskCapacity, 0, 16);
            selectedGuideStation = -1; guidePath.Clear();
        }

        private void RefreshRulesView()
        {
            int count = Mathf.Max(2, lobby.Count);
            if (roomRulesSummary != null) roomRulesSummary.text = roomRules.Summary(count);
            if (rulesSettingsButton != null) rulesSettingsButton.interactable = CanConfigureRules && network.IsServer;
            if (!CanConfigureRules && rulesSettingsRoot != null) rulesSettingsRoot.SetActive(false);
            else if (rulesSettingsRoot != null && rulesSettingsRoot.activeSelf && draftRules != null && draftRules.automaticTimes) RefreshDraftRules();
        }

        private void OpenRulesSettings()
        {
            if (!CanConfigureRules || lobbyCanvas == null) return;
            CloseMapSettings();
            draftRules = roomRules.Resolve(Mathf.Max(2, lobby.Count));
            if (rulesSettingsRoot == null) BuildRulesSettings();
            rulesSettingsRoot.SetActive(true); rulesSettingsRoot.transform.SetAsLastSibling(); RefreshDraftRules();
        }

        private void RefreshDraftRules()
        {
            var effective = draftRules.Resolve(Mathf.Max(2, lobby.Count));
            int[] seconds = { effective.day, effective.night, effective.discussion, effective.voting };
            for (int i = 0; i < 4; i++) durationLabels[i].text = seconds[i] + " giây";
            for (int i = 0; i < 3; i++) presetButtons[i].GetComponentInChildren<TMP_Text>().text =
                ((int)draftRules.preset == i ? "✓ " : "") + ThuongRoomRules.PresetName((ThuongRolePreset)i);
            automaticTimesLabel.text = (draftRules.automaticTimes ? "[x] " : "[ ] ") + "Thời gian tự đề xuất theo số người";
            if (modeLabel != null) modeLabel.text = draftRules.mode == ThuongMatchMode.RealtimeProximity
                ? "✓ TƯƠNG TÁC GẦN (E) · chỉ dùng role Cơ bản · kỹ năng Night có hiệu lực ngay · có xác/báo xác"
                : "✓ THEO PHASE CỔ ĐIỂN · giữ kỹ năng chờ xử lý cuối Night";
            for (int i = 0; i < presetButtons.Length; i++)
                if (presetButtons[i] != null)
                    presetButtons[i].interactable = draftRules.mode != ThuongMatchMode.RealtimeProximity || i == (int)ThuongRolePreset.Basic;
            rulesHelp.text = draftRules.preset == ThuongRolePreset.Basic ? "Dân làng, Khuyển tinh, Tiên tri và Bảo vệ. Phù hợp người mới."
                : draftRules.preset == ThuongRolePreset.Standard ? "Thêm Trưởng làng, Thằng khờ, Thợ săn, Pháp sư và Kẻ bị nguyền."
                : "Cho phép toàn bộ 17 role hiện có, gồm phe trung lập và role đặc biệt.";
        }

        private void ChangeDuration(int index, int delta)
        {
            draftRules = draftRules.Resolve(Mathf.Max(2, lobby.Count)); draftRules.automaticTimes = false;
            switch (index)
            {
                case 0: draftRules.day = Mathf.Clamp(draftRules.day + delta, 60, 300); break;
                case 1: draftRules.night = Mathf.Clamp(draftRules.night + delta, 15, 120); break;
                case 2: draftRules.discussion = Mathf.Clamp(draftRules.discussion + delta, 15, 180); break;
                case 3: draftRules.voting = Mathf.Clamp(draftRules.voting + delta, 15, 120); break;
            }
            RefreshDraftRules();
        }

        private void BuildRulesSettings()
        {
            var shade = LobbyPanel("Room rules shade", lobbyCanvas.transform, Vector2.zero, Vector2.one, false);
            shade.GetComponent<Image>().color = new Color(0, 0, 0, .85f); rulesSettingsRoot = shade.gameObject;
            var panel = LobbyPanel("Room rules", shade, new Vector2(.16f, .06f), new Vector2(.84f, .94f));
            LobbyText("Title", panel, "THIẾT LẬP VÁN CHƠI", new Vector2(.05f, .91f), new Vector2(.95f, .98f), 28, TextAlignmentOptions.Center);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                presetButtons[i] = LobbyButton("Role preset " + i, panel, "", new Vector2(.05f + i * .31f, .80f), new Vector2(.33f + i * .31f, .89f),
                    () =>
                    {
                        if (draftRules.mode == ThuongMatchMode.RealtimeProximity && index != (int)ThuongRolePreset.Basic) return;
                        draftRules.preset = (ThuongRolePreset)index; RefreshDraftRules();
                    });
                presetButtons[i].interactable = draftRules.mode != ThuongMatchMode.RealtimeProximity || i == (int)ThuongRolePreset.Basic;
            }
            rulesHelp = LobbyText("Preset help", panel, "", new Vector2(.06f, .70f), new Vector2(.94f, .79f), 18, TextAlignmentOptions.Center);
            rulesHelp.textWrappingMode = TextWrappingModes.Normal;
            var automatic = LobbyButton("Automatic times", panel, "", new Vector2(.06f, .61f), new Vector2(.94f, .69f),
                () => { draftRules.automaticTimes = !draftRules.automaticTimes; RefreshDraftRules(); });
            automaticTimesLabel = automatic.GetComponentInChildren<TMP_Text>();
            LobbyButton("Match mode", panel, "", new Vector2(.06f, .535f), new Vector2(.94f, .60f), () =>
            {
                draftRules.mode = draftRules.mode == ThuongMatchMode.ClassicQueued
                    ? ThuongMatchMode.RealtimeProximity : ThuongMatchMode.ClassicQueued;
                if (draftRules.mode == ThuongMatchMode.RealtimeProximity) draftRules.preset = ThuongRolePreset.Basic;
                RefreshDraftRules();
            });
            modeLabel = panel.Find("Match mode/Text")?.GetComponent<TMP_Text>();
            string[] labels = { "Ban ngày", "Ban đêm", "Thảo luận", "Bỏ phiếu" };
            for (int i = 0; i < 4; i++)
            {
                int index = i; float y = .445f - i * .09f;
                LobbyText(labels[i], panel, labels[i], new Vector2(.08f, y), new Vector2(.41f, y + .075f), 20);
                LobbyButton("Less " + i, panel, "−", new Vector2(.44f, y), new Vector2(.54f, y + .075f), () => ChangeDuration(index, index == 0 ? -15 : -5));
                durationLabels[i] = LobbyText("Duration " + i, panel, "", new Vector2(.55f, y), new Vector2(.78f, y + .075f), 21, TextAlignmentOptions.Center);
                LobbyButton("More " + i, panel, "+", new Vector2(.80f, y), new Vector2(.90f, y + .075f), () => ChangeDuration(index, index == 0 ? 15 : 5));
            }
            LobbyText("Task balance hint", panel, "Nhiệm vụ tự cân bằng theo số người · Tối đa +40% tổng tiến độ mỗi ngày", new Vector2(.05f, .08f), new Vector2(.95f, .145f), 16, TextAlignmentOptions.Center);
            LobbyButton("Cancel rules", panel, "HỦY", new Vector2(.08f, .035f), new Vector2(.45f, .115f), () => rulesSettingsRoot.SetActive(false));
            LobbyButton("Apply rules", panel, "ÁP DỤNG", new Vector2(.55f, .035f), new Vector2(.92f, .115f),
                () => { if (SetRoomRules(draftRules)) rulesSettingsRoot.SetActive(false); });
        }
    }
}
