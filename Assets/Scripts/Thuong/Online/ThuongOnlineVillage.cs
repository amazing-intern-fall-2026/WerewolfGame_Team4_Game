using System;
using System.Collections.Generic;
using System.Linq;
using Thuong.Maps;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private const string VillageStateMessage = "thuong.online.village-state.v1";
        private readonly ThuongVillageAuthority village = new();
        private readonly List<ThuongItemKind> villageInventory = new();
        private Vector2 bellPoint;
        private readonly Vector2[] pairPoints = new Vector2[2];
        private bool villageLandmarksReady, localBellUsed;
        private ThuongSabotageKind visibleSabotage;
        private int visibleSabotageTarget = -1, visiblePairDoneDay = -1, visibleRepairOwner = -1, visibleMeetingsUsed;
        private int visiblePairFirst = -1, visiblePairSecond = -1;
        private float visiblePairProgress, visibleRepairProgress;
        private double visibleSabotageUntil, localLanternUntil, localWardUntil, nextVillageSend, visibleMeetingAt, visibleSabotageAt;
        private DeathResolver villageDeathResolver;
        private double VillageNow => network != null && network.IsListening ? network.ServerTime.Time : Time.unscaledTimeAsDouble;
        private bool VillageDay => matchStarted && matchResult == null && game != null && game.currentState == GameState.Day;
        private bool LocalVillageDay => connected && matchStarted && matchResult == null && LocalPlayerAlive && visibleState == GameState.Day;

        private void RegisterVillageMessages()
        {
            network.CustomMessagingManager.RegisterNamedMessageHandler(VillageStateMessage, ReceiveVillageState);
            RegisterJournalMessages();
        }
        private void UnregisterVillageMessages()
        {
            network?.CustomMessagingManager?.UnregisterNamedMessageHandler(VillageStateMessage);
            UnregisterJournalMessages();
        }
        private void PrepareVillage()
        {
            villageDeathResolver = SceneComponent<DeathResolver>();
            if (villageDeathResolver != null)
            {
                villageDeathResolver.ConsumeAdditionalProtection = ConsumeVillageWard;
                villageDeathResolver.DeathResolved += RecordVillageDeath;
            }
        }
        private void DisposeVillage()
        {
            if (villageDeathResolver != null)
            {
                if (villageDeathResolver.ConsumeAdditionalProtection == ConsumeVillageWard)
                    villageDeathResolver.ConsumeAdditionalProtection = null;
                villageDeathResolver.DeathResolved -= RecordVillageDeath;
                villageDeathResolver = null;
            }
            DisposeVillageUi();
        }
        private void ResetVillageRound()
        {
            village.Configure(roomRules.village);
            village.Reset(VillageNow);
            ResetVillageView(); ResetJournal();
            BuildVillageLandmarks();
            nextVillageSend = 0;
        }
        private void ResetVillageView()
        {
            visibleSabotage = ThuongSabotageKind.None; visibleSabotageTarget = -1;
            villageInventory.Clear(); localBellUsed = false;
            visibleMeetingsUsed = 0;
            visiblePairDoneDay = visiblePairFirst = visiblePairSecond = visibleRepairOwner = -1;
            localLanternUntil = localWardUntil = visibleSabotageUntil = visibleMeetingAt = visibleSabotageAt = 0;
            visiblePairProgress = visibleRepairProgress = 0;
            CloseVillagePanel();
        }
        private void BuildVillageLandmarks()
        {
            villageLandmarksReady = false;
            if (SelectedMap == null) return;
            bellPoint = FreeVillagePoint(SelectedMap.plaza);
            pairPoints[0] = FreeVillagePoint(bellPoint + Vector2.left * 3);
            pairPoints[1] = FreeVillagePoint(bellPoint + Vector2.right * 3);
            villageLandmarksReady = Vector2.Distance(pairPoints[0], pairPoints[1]) >= 2.5f &&
                ThuongMapCollision.IsFree(SelectedMap, bellPoint) &&
                pairPoints.All(point => ThuongMapCollision.IsFree(SelectedMap, point));
            RebuildVillageMarkers();
        }
        private Vector2 FreeVillagePoint(Vector2 desired)
        {
            if (ThuongMapCollision.IsFree(SelectedMap, desired)) return desired;
            for (int radius = 1; radius <= 12; radius++)
            for (int direction = 0; direction < 16; direction++)
            {
                float angle = direction * Mathf.PI / 8;
                Vector2 point = desired + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * .5f;
                if (ThuongMapCollision.IsFree(SelectedMap, point)) return point;
            }
            return SelectedMap.spawns.FirstOrDefault(point => ThuongMapCollision.IsFree(SelectedMap, point));
        }
        private bool VillageEligible(ThuongOnlineServerPlayer member) => member != null && !member.Offline && member.MapReady &&
            network.ConnectedClientsIds.Contains(member.ClientId) && players?.GetplayerByID(member.PlayerId)?.isAlive == true &&
            !minigameAuthority.IsPlaying(member.ClientId);
        private ThuongOnlineServerPlayer VillageMember(int id) => serverPlayers.Values.FirstOrDefault(member => member.PlayerId == id);
        private bool AtVillagePoint(ThuongOnlineServerPlayer member, Vector2 point, float range = 1.4f) =>
            villageLandmarksReady && VillageEligible(member) && Vector2.Distance(member.Position, point) <= range &&
            ThuongMapCollision.HasLine(SelectedMap, member.Position, point);
        private bool IsVillageChanneling(ulong clientId) => serverPlayers.TryGetValue(clientId, out var member) &&
            (village.RepairOwner == member.PlayerId || village.PairFirst == member.PlayerId || village.PairSecond == member.PlayerId);

        private void UpdateVillage()
        {
            RefreshVillageUi();
            if (!connected || network == null || !network.IsListening || !network.IsServer) return;
            if (matchStarted && matchResult == null)
            {
                var previousSabotage = village.Sabotage;
                bool complete = village.Tick(game.currentDay, VillageNow, VillageDay,
                    (id, slot) => AtVillagePoint(VillageMember(id), pairPoints[slot], 1.1f),
                    id => AtVillagePoint(VillageMember(id), SabotagePoint(village.Sabotage, village.SabotageTarget)),
                    out int first, out int second, out bool repaired);
                if (previousSabotage != ThuongSabotageKind.None && village.Sabotage == ThuongSabotageKind.None)
                    AddJournal(repaired ? "Đã khắc phục phá hoại." : "Phá hoại đã kết thúc.");
                if (complete && tasks.TryCompletePairedTask(first, second))
                {
                    AddJournal($"{VillageName(first)} và {VillageName(second)} hoàn thành nhiệm vụ đôi (+5%).");
                    GrantVillageReward(first); GrantVillageReward(second);
                    nextSnapshotTime = 0;
                }
            }
            if (rolesAssigned && VillageNow >= nextVillageSend)
            {
                nextVillageSend = VillageNow + .2;
                foreach (var member in serverPlayers.Values)
                    if (!member.Offline && network.ConnectedClientsIds.Contains(member.ClientId)) SendVillageState(member);
            }
            PublishJournalAfterMatch();
        }
        private bool TryVillageAction(ThuongOnlineServerPlayer member, ThuongOnlineAction action, int argument, int second, out string reason)
        {
            reason = "Tính năng không khả dụng ở giai đoạn hiện tại.";
            if (!VillageEligible(member) || !matchStarted || matchResult != null) return false;
            var player = players.GetplayerByID(member.PlayerId);
            switch (action)
            {
                case ThuongOnlineAction.EmergencyMeeting:
                    return TryVillageMeeting(member, false, out reason);
                case ThuongOnlineAction.Sabotage:
                    var kind = (ThuongSabotageKind)argument;
                    if (!ValidSabotageTarget(kind, second)) { reason = "Điểm phá hoại không hợp lệ hoặc đang có người đứng trong cửa."; return false; }
                    if (!village.TrySabotage(member.PlayerId, game.currentDay, VillageNow,
                        player.faction == FactionType.Monster, VillageDay, kind, second, true, out reason)) return false;
                    AddJournal($"{VillageName(member.PlayerId)} phá hoại: {SabotageLabel(kind)} · {SabotageTargetLabel(kind, second)}.");
                    village.Release(member.PlayerId); nextVillageSend = 0;
                    return true;
                case ThuongOnlineAction.RepairSabotage:
                    if (player.faction != FactionType.Villager)
                    { reason = "Chỉ Dân Làng mới có thể khắc phục phá hoại."; return false; }
                    bool repair = village.TryRepair(member.PlayerId, VillageNow,
                        VillageDay && AtVillagePoint(member, SabotagePoint(village.Sabotage, village.SabotageTarget)), out reason);
                    if (repair) { member.Input = Vector2.zero; reason = "Đang sửa. Ở lại gần điểm sửa trong 3 giây; di chuyển để hủy."; }
                    return repair;
                case ThuongOnlineAction.PairConsole:
                    bool pair = village.JoinPair(member.PlayerId, argument, game.currentDay, VillageNow,
                        VillageDay && argument >= 0 && argument < 2 && AtVillagePoint(member, pairPoints[Mathf.Clamp(argument, 0, 1)], 1.1f) &&
                        village.RepairOwner != member.PlayerId, out reason);
                    if (pair) { member.Input = Vector2.zero; reason = "Đã kích hoạt bảng điều khiển. Cần người thứ hai ở bảng còn lại."; }
                    return pair;
                case ThuongOnlineAction.UseItem:
                    var item = (ThuongItemKind)argument;
                    if (item == ThuongItemKind.Alarm) return TryVillageMeeting(member, true, out reason);
                    if (item == ThuongItemKind.Lantern && (player.faction != FactionType.Villager || village.Sabotage != ThuongSabotageKind.Blackout ||
                        village.SabotageUntil <= VillageNow || !BlackoutArea(village.SabotageTarget).Contains(member.Position)))
                    { reason = "Dân Làng chỉ dùng đèn lồng khi đang ở khu mất điện."; return false; }
                    if (!village.TryUseItem(member.PlayerId, item, VillageNow,
                        VillageDay || game.currentState == GameState.Night && game.currentPhase == GamePhase.Night, out reason)) return false;
                    AddJournal($"{VillageName(member.PlayerId)} dùng {ItemLabel(item)}.");
                    reason = item == ThuongItemKind.Lantern ? $"Đèn lồng sáng trong {roomRules.village.lanternSeconds} giây, mở rộng tầm nhìn khi mất điện."
                        : $"Bùa chặn một đòn Sói / Sát nhân / kỹ năng trong {roomRules.village.wardSeconds} giây.";
                    nextVillageSend = 0; return true;
            }
            return false;
        }
        private bool TryVillageMeeting(ThuongOnlineServerPlayer member, bool alarm, out string reason)
        {
            if (!village.CanMeet(member.PlayerId, VillageNow, VillageEligible(member), VillageDay,
                AtVillagePoint(member, bellPoint), alarm, out reason)) return false;
            if (!game.StartReportedMeeting()) { reason = "Không thể mở cuộc họp lúc này."; return false; }
            village.CommitMeeting(member.PlayerId, VillageNow, alarm);
            ResetMinigames();
            AddJournal($"{VillageName(member.PlayerId)} gọi họp bằng {(alarm ? "còi báo động" : "chuông làng")}.");
            status = $"{VillageName(member.PlayerId)} đã gọi họp khẩn cấp.";
            reason = status; nextVillageSend = nextSnapshotTime = 0;
            return true;
        }
        private void GrantVillageReward(int id)
        {
            if (village.GrantTaskReward(id, game.currentDay, out var item))
            {
                AddJournal($"{VillageName(id)} nhận {ItemLabel(item)} từ nhiệm vụ.");
                var member = VillageMember(id);
                if (member != null) Reply(member, $"Nhận {ItemLabel(item)}. Mở TÚI ĐỒ để sử dụng.");
                nextVillageSend = 0;
            }
        }
        private bool ConsumeVillageWard(PlayerData target)
        {
            if (network == null || !network.IsServer || !matchStarted || matchResult != null ||
                !village.ConsumeWard(target.playerID, VillageNow)) return false;
            AddJournal($"Bùa của {VillageName(target.playerID)} chặn một đòn tấn công.");
            nextVillageSend = 0; return true;
        }
        private int SabotageTargetCount(ThuongSabotageKind kind) => SelectedMap == null ? 0 : kind switch
        {
            ThuongSabotageKind.Blackout => Mathf.Max(1, SelectedMap.zones.Length),
            ThuongSabotageKind.Barricade => SelectedMap.connections.Length,
            ThuongSabotageKind.StationJam => stations.Length,
            _ => 0
        };
        private Rect BlackoutArea(int index) => SelectedMap.zones.Length > 0 && index >= 0 && index < SelectedMap.zones.Length
            ? SelectedMap.zones[index].area : Rect.MinMaxRect(SelectedMap.boundsMin.x, SelectedMap.boundsMin.y, SelectedMap.boundsMax.x, SelectedMap.boundsMax.y);
        private Rect BarricadeArea(int index)
        {
            var connection = SelectedMap.connections[index];
            var points = connection.points;
            Vector2 center = (points[0] + points[points.Length - 1]) * .5f;
            Vector2 direction = points[points.Length - 1] - points[0];
            Vector2 size = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
                ? new Vector2(.35f, connection.width + .5f) : new Vector2(connection.width + .5f, .35f);
            return new Rect(center - size * .5f, size);
        }
        private Vector2 SabotagePoint(ThuongSabotageKind kind, int index)
        {
            if (SelectedMap == null || index < 0 || index >= SabotageTargetCount(kind)) return bellPoint;
            return kind switch
            {
                ThuongSabotageKind.Blackout => FreeVillagePoint(BlackoutArea(index).center),
                ThuongSabotageKind.Barricade => FreeVillagePoint(BarricadeArea(index).center),
                ThuongSabotageKind.StationJam => stations[index].transform.position,
                _ => bellPoint
            };
        }
        private bool ValidSabotageTarget(ThuongSabotageKind kind, int index)
        {
            if (!villageLandmarksReady || index < 0 || index >= SabotageTargetCount(kind)) return false;
            if (kind == ThuongSabotageKind.Barricade)
            {
                if (SelectedMap.connections[index].points.Length < 2) return false;
                var area = BarricadeArea(index); area.min -= Vector2.one * .35f; area.max += Vector2.one * .35f;
                return !serverPlayers.Values.Any(member => !member.Offline && area.Contains(member.Position)) &&
                    !area.Contains(bellPoint) && !pairPoints.Any(area.Contains);
            }
            return kind != ThuongSabotageKind.StationJam || stations[index]?.task != null && tasks.currentTasks.Contains(stations[index].task);
        }
        private Rect? VillageBarrier => SelectedMap != null && village.Sabotage == ThuongSabotageKind.Barricade &&
            village.SabotageUntil > VillageNow ? BarricadeArea(village.SabotageTarget) : null;
        private bool VillageHasLine(Vector2 from, Vector2 to) => ThuongMapCollision.HasLine(SelectedMap, from, to, VillageBarrier);
        private bool VillageStationJammed(int station) => village.Sabotage == ThuongSabotageKind.StationJam &&
            village.SabotageTarget == station && village.SabotageUntil > VillageNow;
        private bool CanObserveVillagePosition(int observerId, Vector2 position, int subjectId = -1)
        {
            if (observerId == subjectId || !matchStarted || matchResult != null) return true;
            var observer = VillageMember(observerId);
            var person = players?.GetplayerByID(observerId);
            if (observer == null || person == null) return false;
            if (!person.isAlive && game.currentState == GameState.Night) return false;
            if (village.Sabotage != ThuongSabotageKind.Blackout || village.SabotageUntil <= VillageNow ||
                !BlackoutArea(village.SabotageTarget).Contains(position) || person.faction == FactionType.Monster) return true;
            if (!person.isAlive) return false;
            float vision = village.LanternUntil(observerId) > VillageNow ? 6 : 2.5f;
            return Vector2.Distance(observer.Position, position) <= vision && VillageHasLine(observer.Position, position);
        }
        private void SendVillageState(ThuongOnlineServerPlayer member)
        {
            using var writer = new FastBufferWriter(256, Allocator.Temp);
            writer.WriteValueSafe(matchEpoch);
            writer.WriteValueSafe((byte)village.Sabotage); writer.WriteValueSafe(village.SabotageTarget); writer.WriteValueSafe(village.SabotageUntil);
            writer.WriteValueSafe(village.NextMeetingAt); writer.WriteValueSafe(village.MeetingsUsed); writer.WriteValueSafe(village.NextSabotageAt);
            writer.WriteValueSafe(village.CompletedPairDay); writer.WriteValueSafe(village.PairFirst); writer.WriteValueSafe(village.PairSecond);
            writer.WriteValueSafe(village.PairProgress(VillageNow)); writer.WriteValueSafe(village.RepairOwner); writer.WriteValueSafe(village.RepairProgress(VillageNow));
            writer.WriteValueSafe(village.HasRungBell(member.PlayerId));
            writer.WriteValueSafe(village.LanternUntil(member.PlayerId)); writer.WriteValueSafe(village.WardUntil(member.PlayerId));
            var items = village.Inventory(member.PlayerId); writer.WriteValueSafe((byte)items.Count);
            foreach (var item in items) writer.WriteValueSafe((byte)item);
            if (member.ClientId == network.LocalClientId)
            {
                var reader = new FastBufferReader(writer, Allocator.Temp);
                try { ApplyVillageState(reader); } finally { reader.Dispose(); }
            }
            else network.CustomMessagingManager.SendNamedMessage(VillageStateMessage, member.ClientId, writer, NetworkDelivery.ReliableSequenced);
        }
        private void ReceiveVillageState(ulong sender, FastBufferReader reader)
        {
            if (network.IsServer || sender != NetworkManager.ServerClientId) return;
            try { ApplyVillageState(reader); }
            catch (Exception error) when (error is OverflowException || error is ArgumentException) { Debug.LogWarning("Bỏ qua trạng thái làng không hợp lệ."); }
        }
        private void ApplyVillageState(FastBufferReader reader)
        {
            reader.ReadValueSafe(out uint epoch); if (epoch != matchEpoch) return;
            reader.ReadValueSafe(out byte kind); reader.ReadValueSafe(out int target); reader.ReadValueSafe(out double until);
            if (kind > (byte)ThuongSabotageKind.StationJam || !double.IsFinite(until)) return;
            visibleSabotage = (ThuongSabotageKind)kind; visibleSabotageTarget = target; visibleSabotageUntil = until;
            reader.ReadValueSafe(out visibleMeetingAt); reader.ReadValueSafe(out visibleMeetingsUsed); reader.ReadValueSafe(out visibleSabotageAt);
            reader.ReadValueSafe(out visiblePairDoneDay); reader.ReadValueSafe(out visiblePairFirst); reader.ReadValueSafe(out visiblePairSecond);
            reader.ReadValueSafe(out visiblePairProgress); reader.ReadValueSafe(out visibleRepairOwner); reader.ReadValueSafe(out visibleRepairProgress);
            reader.ReadValueSafe(out localBellUsed); reader.ReadValueSafe(out localLanternUntil); reader.ReadValueSafe(out localWardUntil);
            reader.ReadValueSafe(out byte count); if (count > ThuongVillageAuthority.InventoryCapacity) return;
            villageInventory.Clear();
            for (int i = 0; i < count; i++) { reader.ReadValueSafe(out byte item); if (item > (byte)ThuongItemKind.Ward) return; villageInventory.Add((ThuongItemKind)item); }
        }
        private string VillageName(int id) => players?.GetplayerByID(id)?.DisplayName ?? "P" + (id + 1);
        private static string SabotageLabel(ThuongSabotageKind kind) => kind switch
        { ThuongSabotageKind.Blackout => "Mất điện", ThuongSabotageKind.Barricade => "Chặn đường", ThuongSabotageKind.StationJam => "Hỏng trạm", _ => "Không có phá hoại" };
        private static string ItemLabel(ThuongItemKind item) => item switch
        { ThuongItemKind.Lantern => "Đèn lồng", ThuongItemKind.Alarm => "Còi báo động", _ => "Bùa bảo vệ" };
        private string SabotageTargetLabel(ThuongSabotageKind kind, int index)
        {
            if (SelectedMap == null || index < 0 || index >= SabotageTargetCount(kind)) return "Chưa có điểm phù hợp";
            return kind switch
            {
                ThuongSabotageKind.Blackout => SelectedMap.zones.Length > 0 ? SelectedMap.zones[index].displayName : "Cả làng",
                ThuongSabotageKind.Barricade => SelectedMap.connections[index].connectionId,
                ThuongSabotageKind.StationJam => stations[index].task?.taskName ?? "Trạm nhiệm vụ",
                _ => ""
            };
        }
    }
}
