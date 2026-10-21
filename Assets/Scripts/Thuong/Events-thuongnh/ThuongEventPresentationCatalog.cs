using System;
using UnityEngine;

[Serializable]
public sealed class ThuongEventVisual
{
    public GameEventType type;
    public string displayName;
    public Color dayTint = Color.white;
    public Color nightTint = new(.4f, .48f, .68f);
    public Sprite decoration;
    [Range(0, .06f)] public float volume = .018f;
}

[CreateAssetMenu(menuName = "Thuong/Event Presentation Catalog")]
public sealed class ThuongEventPresentationCatalog : ScriptableObject
{
    public const string ResourcePath = "ThuongEventPresentationCatalog";
    public const int ContentRevision = 2;
    public ThuongEventVisual[] events = Array.Empty<ThuongEventVisual>();
    public Sprite lantern, banner, stage, tuongMask;
    public ThuongEventVisual Find(GameEventType type) => Array.Find(events, e => e != null && e.type == type);

    public static bool IsVisible(GameEventType type, GamePhase phase)
    {
        bool day = phase == GamePhase.DayStart || phase == GamePhase.Task || phase == GamePhase.Event;
        bool night = phase == GamePhase.Night || phase == GamePhase.ResolveNight;
        return type switch
        {
            GameEventType.BloodMoon => night,
            GameEventType.ClearSky => day,
            GameEventType.Fog => day || night,
            GameEventType.HarvestFestival or GameEventType.GhostMonth or GameEventType.TuongNight =>
                day || night || phase == GamePhase.Discussion || phase == GamePhase.Voting || phase == GamePhase.ResolveVote || phase == GamePhase.DayResolution,
            _ => false
        };
    }
    public static string Name(GameEventType type) => type switch
    {
        GameEventType.BloodMoon => "ĐÊM TRĂNG MÁU",
        GameEventType.Fog => "LÀNG TRONG SƯƠNG",
        GameEventType.HarvestFestival => "HỘI MÙA GẶT",
        GameEventType.ClearSky => "NGÀY TRỜI QUANG",
        GameEventType.GhostMonth => "ĐÊM ĐÈN TƯỞNG NIỆM",
        GameEventType.TuongNight => "ĐÊM DIỄN TUỒNG",
        _ => ""
    };
}
