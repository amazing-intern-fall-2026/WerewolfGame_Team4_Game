namespace Thuong.Online
{
    public enum ThuongCommunicationAudience : byte { None, Room, Living, Dead }

    public static class ThuongCommunicationRules
    {
        public static ThuongCommunicationAudience Resolve(bool connected, bool started, bool finished, bool alive, GamePhase phase)
        {
            if (!connected) return ThuongCommunicationAudience.None;
            if (!started || finished) return ThuongCommunicationAudience.Room;
            if (!alive) return ThuongCommunicationAudience.Dead;
            return phase is GamePhase.DayStart or GamePhase.Event or GamePhase.Task or GamePhase.Discussion or GamePhase.Voting
                ? ThuongCommunicationAudience.Living : ThuongCommunicationAudience.None;
        }

        public static string Description(ThuongCommunicationAudience audience) => audience switch
        {
            ThuongCommunicationAudience.Room => "Trao đổi trong phòng",
            ThuongCommunicationAudience.Living => "Kênh người còn sống",
            ThuongCommunicationAudience.Dead => "Kênh người bị loại",
            _ => "Tạm ngưng trao đổi trong pha này"
        };
    }
}
