using System;
using System.Collections.Generic;

namespace Thuong.Online
{
    public enum ThuongSabotageKind : byte { None, Blackout, Barricade, StationJam }
    public enum ThuongItemKind : byte { Lantern, Alarm, Ward }

    // Match-scoped rules. Positions, phase and identities are supplied only by
    // the authoritative session, never taken from a client's claimed identity.
    public sealed class ThuongVillageAuthority
    {
        public const int InventoryCapacity = 2;
        public const int MeetingLimit = 3;
        public const double MeetingCooldown = 45, SabotageCooldown = 45;
        public const double ChannelSeconds = 3, LanternSeconds = 20, WardSeconds = 30;
        private ThuongVillageSettings settings = new();
        public void Configure(ThuongVillageSettings candidate)
        {
            if (candidate == null || !candidate.IsValid) throw new ArgumentException("Luật làng không hợp lệ.", nameof(candidate));
            settings = candidate.Copy();
        }
        private sealed class Personal
        {
            public readonly List<ThuongItemKind> Items = new();
            public bool RangBell;
            public int RewardDay = -1, RewardsToday, Rewards;
            public double LanternUntil, WardUntil;
        }
        private readonly Dictionary<int, Personal> people = new();
        private readonly int[] pairOwners = { -1, -1 };
        private double lastNow, pairStarted = -1, repairStarted = -1;
        private int pairDay = -1, completedPairDay = -1, sabotageDay = -1, meetingsUsed;
        public double NextMeetingAt { get; private set; }
        public int MeetingsUsed => meetingsUsed;
        public double NextSabotageAt { get; private set; }
        public ThuongSabotageKind Sabotage { get; private set; }
        public int SabotageTarget { get; private set; } = -1;
        public double SabotageUntil { get; private set; }
        public int RepairOwner { get; private set; } = -1;
        public int PairFirst => pairOwners[0];
        public int PairSecond => pairOwners[1];
        public int CompletedPairDay => completedPairDay;
        public float PairProgress(double now) => pairStarted < 0 ? 0 : (float)Math.Clamp((now - pairStarted) / ChannelSeconds, 0, 1);
        public float RepairProgress(double now) => repairStarted < 0 ? 0 : (float)Math.Clamp((now - repairStarted) / ChannelSeconds, 0, 1);

        private Personal Person(int id)
        {
            if (!people.TryGetValue(id, out var person)) people[id] = person = new Personal();
            return person;
        }
        private bool ValidTime(double now) => double.IsFinite(now) && now >= lastNow;
        public void Reset(double now)
        {
            people.Clear(); lastNow = double.IsFinite(now) ? now : 0;
            NextMeetingAt = lastNow + 15; NextSabotageAt = lastNow + 15;
            meetingsUsed = 0;
            pairDay = completedPairDay = sabotageDay = -1;
            ClearPair(); ClearSabotage();
        }
        public IReadOnlyList<ThuongItemKind> Inventory(int id) => Person(id).Items.AsReadOnly();
        public double LanternUntil(int id) => Person(id).LanternUntil;
        public double WardUntil(int id) => Person(id).WardUntil;
        public bool HasRungBell(int id) => Person(id).RangBell;

        public bool CanMeet(int id, double now, bool aliveConnected, bool daytime, bool atBell, bool alarm, out string reason)
        {
            reason = "Chỉ người sống, còn kết nối mới gọi được họp ban ngày.";
            if (id < 0 || !ValidTime(now) || !aliveConnected || !daytime) return false;
            if (Sabotage != ThuongSabotageKind.None) { reason = "Hãy khắc phục phá hoại trước khi gọi họp."; return false; }
            if (meetingsUsed >= settings.meetingLimit) { reason = "Phòng đã dùng hết số lần họp khẩn trong ván."; return false; }
            if (now < NextMeetingAt) { reason = $"Chờ {Math.Ceiling(NextMeetingAt - now):0} giây nữa để gọi họp."; return false; }
            var person = Person(id);
            if (person.RangBell) { reason = "Bạn đã gọi họp khẩn trong ván này."; return false; }
            if (alarm && !person.Items.Contains(ThuongItemKind.Alarm)) { reason = "Bạn không có còi báo động."; return false; }
            if (!alarm && !atBell) { reason = "Hãy tới gần chuông làng."; return false; }
            reason = null; return true;
        }
        public void CommitMeeting(int id, double now, bool alarm)
        {
            var person = Person(id);
            if (alarm) person.Items.Remove(ThuongItemKind.Alarm);
            person.RangBell = true;
            meetingsUsed++;
            NextMeetingAt = now + settings.meetingCooldown;
            ClearPair();
        }
        public bool TrySabotage(int id, int day, double now, bool aliveConnectedWolf, bool daytime,
            ThuongSabotageKind kind, int target, bool validTarget, out string reason)
        {
            reason = "Chỉ Sói còn sống mới được phá hoại ban ngày.";
            if (id < 0 || !ValidTime(now) || !aliveConnectedWolf || !daytime) return false;
            if (!Enum.IsDefined(typeof(ThuongSabotageKind), kind) || kind == ThuongSabotageKind.None || target < 0 || !validTarget)
            { reason = "Vị trí phá hoại không hợp lệ."; return false; }
            if (Sabotage != ThuongSabotageKind.None) { reason = "Đang có một phá hoại cần xử lý."; return false; }
            if (sabotageDay == day) { reason = "Bầy Sói đã dùng lượt phá hoại hôm nay."; return false; }
            if (now < NextSabotageAt) { reason = "Phá hoại chưa hồi lại."; return false; }
            Sabotage = kind; SabotageTarget = target; sabotageDay = day;
            SabotageUntil = now + (kind == ThuongSabotageKind.Barricade ? settings.barricadeSeconds :
                kind == ThuongSabotageKind.StationJam ? settings.jamSeconds : settings.blackoutSeconds);
            NextSabotageAt = now + settings.sabotageCooldown;
            ClearPair(); reason = null; return true;
        }
        public bool TryRepair(int id, double now, bool eligibleAtRepairPoint, out string reason)
        {
            reason = "Tới gần điểm sửa và đứng yên trong 3 giây.";
            if (id < 0 || !ValidTime(now) || !eligibleAtRepairPoint || Sabotage == ThuongSabotageKind.None) return false;
            if (RepairOwner >= 0 && RepairOwner != id) { reason = "Đã có người đang khắc phục."; return false; }
            if (RepairOwner < 0) { RepairOwner = id; repairStarted = now; }
            reason = null; return true;
        }
        public bool JoinPair(int id, int slot, int day, double now, bool eligibleAtPad, out string reason)
        {
            reason = "Cần hai người khác nhau, mỗi người ở một bảng điều khiển.";
            if (id < 0 || slot < 0 || slot > 1 || !ValidTime(now) || !eligibleAtPad || completedPairDay == day) return false;
            if (pairDay != day) { ClearPair(); pairDay = day; }
            if (pairOwners[1 - slot] == id || pairOwners[slot] >= 0 && pairOwners[slot] != id) return false;
            pairOwners[slot] = id;
            if (pairOwners[0] >= 0 && pairOwners[1] >= 0 && pairStarted < 0) pairStarted = now;
            reason = null; return true;
        }
        // Losing range, a participant, or a phase cancels progress; it never
        // credits a disconnected player or leaves an occupied console behind.
        public bool Tick(int day, double now, bool daytime, Func<int, int, bool> validPair,
            Func<int, bool> validRepair, out int first, out int second, out bool repaired)
        {
            first = second = -1; repaired = false;
            if (!ValidTime(now)) return false;
            lastNow = now;
            if (Sabotage != ThuongSabotageKind.None && (!daytime || now >= SabotageUntil)) ClearSabotage();
            if (RepairOwner >= 0)
            {
                if (!daytime || !validRepair(RepairOwner)) { RepairOwner = -1; repairStarted = -1; }
                else if (now - repairStarted >= ChannelSeconds) { repaired = true; ClearSabotage(); }
            }
            if (!daytime || pairDay != day) { ClearPair(); return false; }
            for (int slot = 0; slot < 2; slot++)
                if (pairOwners[slot] >= 0 && !validPair(pairOwners[slot], slot)) { pairOwners[slot] = -1; pairStarted = -1; }
            if (pairOwners[0] < 0 || pairOwners[1] < 0 || pairStarted < 0 || now - pairStarted < ChannelSeconds) return false;
            first = pairOwners[0]; second = pairOwners[1]; completedPairDay = day;
            ClearPair(); return true;
        }
        public bool GrantTaskReward(int id, int day, out ThuongItemKind item)
        {
            item = default;
            if (id < 0) return false;
            var person = Person(id);
            if (person.RewardDay != day) { person.RewardDay = day; person.RewardsToday = 0; }
            if (person.Items.Count >= InventoryCapacity || person.RewardsToday >= 2) return false;
            item = (ThuongItemKind)((person.Rewards + id) % 3);
            person.Items.Add(item); person.Rewards++; person.RewardsToday++;
            return true;
        }
        public bool TryUseItem(int id, ThuongItemKind item, double now, bool eligible, out string reason)
        {
            reason = "Vật phẩm không khả dụng.";
            if (id < 0 || !ValidTime(now) || !eligible || !Enum.IsDefined(typeof(ThuongItemKind), item) || item == ThuongItemKind.Alarm) return false;
            var person = Person(id);
            if (!person.Items.Contains(item)) { reason = "Bạn không có vật phẩm này."; return false; }
            if (item == ThuongItemKind.Lantern && person.LanternUntil > now || item == ThuongItemKind.Ward && person.WardUntil > now)
            { reason = "Vật phẩm cùng loại vẫn đang có hiệu lực."; return false; }
            person.Items.Remove(item);
            if (item == ThuongItemKind.Lantern) person.LanternUntil = now + settings.lanternSeconds;
            else person.WardUntil = now + settings.wardSeconds;
            reason = null; return true;
        }
        public bool ConsumeWard(int id, double now)
        {
            if (!ValidTime(now) || Person(id).WardUntil <= now) return false;
            Person(id).WardUntil = 0; return true;
        }
        public void Release(int id)
        {
            for (int slot = 0; slot < 2; slot++)
                if (pairOwners[slot] == id) { pairOwners[slot] = -1; pairStarted = -1; }
            if (RepairOwner == id) { RepairOwner = -1; repairStarted = -1; }
        }
        public void ClearPair() { pairOwners[0] = pairOwners[1] = -1; pairStarted = -1; }
        public void ClearSabotage()
        {
            Sabotage = ThuongSabotageKind.None; SabotageTarget = -1;
            SabotageUntil = 0; RepairOwner = -1; repairStarted = -1;
        }
    }
}
