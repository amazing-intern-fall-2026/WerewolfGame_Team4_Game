using System;
using UnityEngine;

namespace Thuong.Online
{
    public enum ThuongRolePreset : byte { Basic, Standard, Full }

    [Serializable]
    public sealed class ThuongRoomRules
    {
        public ThuongRolePreset preset = ThuongRolePreset.Standard;
        public bool automaticTimes = true;
        public int day = 120, night = 30, discussion = 60, voting = 30;

        public ThuongRoomRules Copy() => (ThuongRoomRules)MemberwiseClone();
        public bool IsValid => Enum.IsDefined(typeof(ThuongRolePreset), preset) &&
            day >= 60 && day <= 300 && night >= 15 && night <= 120 &&
            discussion >= 15 && discussion <= 180 && voting >= 15 && voting <= 120;

        public ThuongRoomRules Resolve(int count)
        {
            var result = Copy();
            if (!automaticTimes) return result;
            result.day = count <= 5 ? 90 : count <= 12 ? 120 : 150;
            result.night = count <= 5 ? 20 : 30;
            result.discussion = count <= 5 ? 30 : count <= 12 ? 60 : 90;
            result.voting = count <= 5 ? 20 : count <= 12 ? 30 : 45;
            return result;
        }

        public static string PresetName(ThuongRolePreset value) => value switch
        { ThuongRolePreset.Basic => "Cơ bản", ThuongRolePreset.Standard => "Tiêu chuẩn", _ => "Đầy đủ" };

        public RoleType[] AllowedRoles => preset == ThuongRolePreset.Full ? RoleCatalog.AllConcreteRoles :
            preset == ThuongRolePreset.Basic
                ? new[] { RoleType.Villager, RoleType.DogSpirit, RoleType.Seer, RoleType.VillageGuardian }
                : new[] { RoleType.Villager, RoleType.DogSpirit, RoleType.Seer, RoleType.VillageGuardian,
                    RoleType.Mayor, RoleType.Idiot, RoleType.Hunter, RoleType.Shaman, RoleType.Cursed };

        public string Summary(int count)
        {
            var rules = Resolve(count);
            return $"{PresetName(preset)} · Ngày {rules.day}s · Đêm {rules.night}s · Họp {rules.discussion}s · Vote {rules.voting}s";
        }
    }
}
