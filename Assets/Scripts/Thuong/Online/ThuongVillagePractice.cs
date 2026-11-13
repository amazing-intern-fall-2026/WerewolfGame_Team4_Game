using UnityEngine;

namespace Thuong.Online
{
    // Local training state: no session, network messages, rewards or live players.
    public sealed class ThuongVillagePractice
    {
        private readonly ThuongVillageAuthority rules = new();
        public static readonly Vector2 Bell = new(-3, 2), PadA = new(-3, -2), PadB = new(3, -2), Repair = new(3, 2);
        public Vector2 Position { get; private set; }
        public int Stage { get; private set; }
        public bool Succeeded { get; private set; }
        public string Feedback { get; private set; }
        public float Progress(double now) => Stage == 1 ? rules.PairProgress(now) : rules.RepairProgress(now);

        public void Begin(int stage, double now)
        {
            Stage = Mathf.Clamp(stage, 0, 3); Position = Vector2.zero; Succeeded = false;
            rules.Reset(now - 15);
            if (Stage >= 2) rules.TrySabotage(2, 1, now, true, true, ThuongSabotageKind.Blackout, 0, true, out _);
            if (Stage == 3) rules.GrantTaskReward(0, 1, out _);
            Feedback = "Dùng WASD, phím mũi tên hoặc các nút để di chuyển. Nhấn E để thao tác.";
        }

        public void Move(Vector2 delta)
        {
            if (delta.sqrMagnitude < .000001f) return;
            rules.Release(0);
            Position = Vector2.Min(Vector2.one * 4.5f, Vector2.Max(Vector2.one * -4.5f, Position + delta));
        }

        private bool At(Vector2 point) => Vector2.Distance(Position, point) <= 1.1f;

        public bool Interact(double now)
        {
            if (Succeeded) return false;
            string reason;
            bool accepted;
            switch (Stage)
            {
                case 0:
                    accepted = rules.CanMeet(0, now, true, true, At(Bell), false, out reason);
                    if (accepted) { rules.CommitMeeting(0, now, false); Succeeded = true; reason = "Đã gọi họp! Mỗi người có một lượt, cả phòng có giới hạn và thời gian chờ."; }
                    break;
                case 1:
                    accepted = rules.JoinPair(0, 0, 1, now, At(PadA), out reason);
                    if (accepted)
                    {
                        rules.JoinPair(1, 1, 1, now, true, out _);
                        reason = "Bạn đồng hành mô phỏng đã kích hoạt bảng B. Đứng yên ba giây tại A.";
                    }
                    break;
                case 2:
                    accepted = rules.TryRepair(0, now, At(Repair), out reason);
                    if (accepted) reason = "Đang sửa: đứng yên tại điểm đỏ trong ba giây.";
                    break;
                default:
                    accepted = rules.TryUseItem(0, ThuongItemKind.Lantern, now, Vector2.Distance(Position, Repair) <= 2, out reason);
                    if (accepted) { Succeeded = true; reason = "Đã tiêu thụ đèn lồng và bật sáng 20 giây! Túi có hai ô; còi gọi họp từ xa, bùa chặn một đòn."; }
                    else reason = "Tới gần khu mất điện màu đỏ rồi nhấn DÙNG ĐÈN hoặc E.";
                    break;
            }
            Feedback = reason;
            return accepted;
        }

        public void Tick(double now)
        {
            if (Succeeded) return;
            bool paired = rules.Tick(1, now, true, (id, slot) => id == 1 || At(PadA), _ => At(Repair), out _, out _, out bool repaired);
            if (Stage == 1 && paired) { Succeeded = true; Feedback = "Hoàn thành nhiệm vụ đôi! Trong ván thật, cả phòng nhận thêm 5% tiến độ, tối đa một lần mỗi ngày."; }
            if (Stage == 2 && repaired) { Succeeded = true; Feedback = "Đã khắc phục mất điện! Chỉ Dân Làng sửa được; di chuyển sẽ hủy thao tác."; }
            if (Stage >= 2 && !Succeeded && rules.Sabotage == ThuongSabotageKind.None)
            {
                Begin(Stage, now);
                Feedback = "Phá hoại đã tự hết giờ. Bài tập đã bắt đầu lại để bạn thử thao tác.";
            }
        }
    }
}
