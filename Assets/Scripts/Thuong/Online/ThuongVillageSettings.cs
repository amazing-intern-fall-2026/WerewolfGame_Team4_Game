using System;
using Unity.Netcode;

namespace Thuong.Online
{
    [Serializable]
    public sealed class ThuongVillageSettings
    {
        public int meetingLimit = 3, meetingCooldown = 45, sabotageCooldown = 45;
        public int blackoutSeconds = 20, jamSeconds = 20, barricadeSeconds = 8;
        public int lanternSeconds = 20, wardSeconds = 30;

        public bool IsValid => meetingLimit >= 0 && meetingLimit <= 6 &&
            meetingCooldown >= 15 && meetingCooldown <= 120 && sabotageCooldown >= 15 && sabotageCooldown <= 120 &&
            blackoutSeconds >= 5 && blackoutSeconds <= 45 && jamSeconds >= 5 && jamSeconds <= 45 &&
            barricadeSeconds >= 3 && barricadeSeconds <= 15 && lanternSeconds >= 5 && lanternSeconds <= 60 &&
            wardSeconds >= 5 && wardSeconds <= 60;
        public ThuongVillageSettings Copy() => (ThuongVillageSettings)MemberwiseClone();

        public void WriteTo(FastBufferWriter writer)
        {
            writer.WriteValueSafe(meetingLimit); writer.WriteValueSafe(meetingCooldown); writer.WriteValueSafe(sabotageCooldown);
            writer.WriteValueSafe(blackoutSeconds); writer.WriteValueSafe(jamSeconds); writer.WriteValueSafe(barricadeSeconds);
            writer.WriteValueSafe(lanternSeconds); writer.WriteValueSafe(wardSeconds);
        }

        public static ThuongVillageSettings ReadFrom(FastBufferReader reader)
        {
            var value = new ThuongVillageSettings();
            reader.ReadValueSafe(out value.meetingLimit); reader.ReadValueSafe(out value.meetingCooldown); reader.ReadValueSafe(out value.sabotageCooldown);
            reader.ReadValueSafe(out value.blackoutSeconds); reader.ReadValueSafe(out value.jamSeconds); reader.ReadValueSafe(out value.barricadeSeconds);
            reader.ReadValueSafe(out value.lanternSeconds); reader.ReadValueSafe(out value.wardSeconds);
            if (!value.IsValid) throw new ArgumentException("Luật làng nhận được không hợp lệ.");
            return value;
        }
    }
}
