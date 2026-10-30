using UnityEngine;

// Public social signals only. This catalog deliberately has no role or faction data.
namespace Thuong.Emotes
{
public static class PlayerEmoteCatalog
{
    public const int Count = 8;
    public const float DisplaySeconds = 3f;
    public const float CooldownSeconds = 1.5f;
    public const float WheelDeadZone = 68f;

    private static readonly string[] labels =
    {
        "Vui vẻ", "Ngạc nhiên", "Nghi ngờ", "Tức giận",
        "Buồn", "Sợ hãi", "Cười lớn", "Yêu thích"
    };

    public static bool IsValid(int id) => id >= 1 && id <= Count;
    public static string Label(int id) => IsValid(id) ? labels[id - 1] : "Biểu cảm";

    // Clockwise, starting at twelve o'clock. Zero means cancel/no selection.
    public static int Select(Vector2 offset, float deadZone = WheelDeadZone)
    {
        if (offset.sqrMagnitude <= deadZone * deadZone) return 0;
        float angle = Mathf.Repeat(Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg + 22.5f, 360f);
        return Mathf.FloorToInt(angle / 45f) + 1;
    }

    public static Vector2 Direction(int id)
    {
        float angle = (id - 1) * 45f * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
    }

    public static bool CanExpress(bool alive, GamePhase phase)
    {
        if (!alive) return false;
        switch (phase)
        {
            case GamePhase.DayStart:
            case GamePhase.Event:
            case GamePhase.Task:
            case GamePhase.Discussion:
            case GamePhase.Voting:
            case GamePhase.Night:
                return true;
            default:
                return false;
        }
    }
}

}
