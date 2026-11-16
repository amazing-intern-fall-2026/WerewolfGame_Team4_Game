using System;
using UnityEngine;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        /// <summary>
        /// Đồng bộ cấu hình điều kiện thắng Bạch Khuyển thức tỉnh:
        /// Giới hạn số dân làng tối đa để Bạch Khuyển thắng riêng là 1 (parity 1v1 hoặc 0 dân),
        /// ngăn chặn tuyệt đối trường hợp Bạch Khuyển vừa thức tỉnh đã thắng khi còn từ 2 Dân Làng trở lên sống sót.
        /// </summary>
        public void EnforceWhiteHoundWinVillageLimit(int limit = 1)
        {
            if (winSettings != null)
            {
                winSettings.specialWinVillageLimit = Mathf.Clamp(limit, 0, 1);
            }
        }

        /// <summary>
        /// Tra cứu ClientId từ PlayerId thông qua danh sách người chơi trên Server.
        /// </summary>
        public bool TryGetClientIdForPlayer(int playerId, out ulong clientId)
        {
            if (serverPlayers != null)
            {
                foreach (var pair in serverPlayers)
                {
                    if (pair.Value != null && pair.Value.PlayerId == playerId && !pair.Value.Offline)
                    {
                        clientId = pair.Key;
                        return true;
                    }
                }
            }
            clientId = ulong.MaxValue;
            return false;
        }

        public int GetVisibleDay() => visibleDay;
        public RoleType GetPrivateRole() => privateRole;
        public ushort GetPrivateFlags() => privateFlags;

        /// <summary>
        /// Áp dụng giới hạn kỹ năng Xà Tinh trong Thương Online:
        /// Ở Đêm 1 và Đêm 2, xóa cờ HasActiveLocalAbility (bit 8) để RoleAbilityUI ẩn nút CHỌN MỤC TIÊU.
        /// Từ Đêm 3 trở đi, cho phép hiển thị và sử dụng bình thường.
        /// </summary>
        public void EnforceSerpentSpiritNightRestriction()
        {
            if (privateRole == RoleType.SerpentSpirit)
            {
                int night = visibleDay > 0 ? visibleDay : (NightManager.Instance != null && NightManager.Instance.CurrentNightNumber > 0 ? NightManager.Instance.CurrentNightNumber : 1);
                if (night < 3)
                {
                    // Bit 8 là cờ HasActiveLocalAbility, xóa bit 8 để RoleAbilityUI ẩn nút chọn mục tiêu
                    privateFlags = (ushort)(privateFlags & ~8);
                }
            }
        }
    }
}
