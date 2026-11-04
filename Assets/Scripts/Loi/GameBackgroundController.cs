using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Điều khiển hiển thị hình nền ban ngày (sáng) và ban đêm (tối) cho Game Scene
/// đồng bộ theo GamePhase từ hệ thống NetworkPhaseSync trong game Ma Sói.
/// - Ban đêm (Phase = Night): Hiển thị Background_Night, ẩn Background_Day.
/// - Tất cả Phase khác (RoleReveal, ResolveNight, DayStart, Event, Task, Discussion, Voting, ResolveVote, GameOver...):
///   Hiển thị Background_Day, ẩn Background_Night.
/// - Đồng bộ chuẩn Multiplayer giữa Host và tất cả Client qua sự kiện thay đổi Phase (không dùng Update liên tục).
/// </summary>
[DisallowMultipleComponent]
public class GameBackgroundController : MonoBehaviour
{
    public static GameBackgroundController Instance { get; private set; }

    [Header("Background GameObjects")]
    [Tooltip("GameObject hình nền ban ngày (sáng)")]
    [SerializeField] private GameObject backgroundDay;

    [Tooltip("GameObject hình nền ban đêm (tối)")]
    [SerializeField] private GameObject backgroundNight;

    [Header("Image Components")]
    [SerializeField] private Image dayImage;
    [SerializeField] private Image nightImage;

    [Header("Sprites")]
    [SerializeField] private Sprite daySprite;
    [SerializeField] private Sprite nightSprite;

    public GameObject BackgroundDay => backgroundDay;
    public GameObject BackgroundNight => backgroundNight;
    public GamePhase CurrentActivePhase { get; private set; } = GamePhase.DayStart;
    public bool IsNightActive => backgroundNight != null && backgroundNight.activeSelf;
    public bool IsDayActive => backgroundDay != null && backgroundDay.activeSelf;

    private Coroutine waitPhaseSyncCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        EnsureBackgroundElements();
    }

    private void OnEnable()
    {
        // 1. Đăng ký sự kiện thay đổi Phase tĩnh từ NetworkPhaseSync
        NetworkPhaseSync.OnPhaseChangedGlobal -= HandlePhaseChanged;
        NetworkPhaseSync.OnPhaseChangedGlobal += HandlePhaseChanged;

        // 2. Nếu NetworkPhaseSync đã tồn tại và spawn, đăng ký lắng nghe NetworkVariable
        if (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.CurrentPhase != null)
        {
            NetworkPhaseSync.Instance.CurrentPhase.OnValueChanged -= HandleNetworkVariablePhaseChanged;
            NetworkPhaseSync.Instance.CurrentPhase.OnValueChanged += HandleNetworkVariablePhaseChanged;

            if (NetworkPhaseSync.Instance.IsSpawned)
            {
                UpdateBackgroundByPhase(NetworkPhaseSync.Instance.CurrentPhase.Value);
            }
        }
    }

    private void OnDisable()
    {
        NetworkPhaseSync.OnPhaseChangedGlobal -= HandlePhaseChanged;

        if (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.CurrentPhase != null)
        {
            NetworkPhaseSync.Instance.CurrentPhase.OnValueChanged -= HandleNetworkVariablePhaseChanged;
        }

        if (waitPhaseSyncCoroutine != null)
        {
            StopCoroutine(waitPhaseSyncCoroutine);
            waitPhaseSyncCoroutine = null;
        }
    }

    private void Start()
    {
        // Khởi tạo ban đầu: Mặc định Day (trừ khi NetworkPhaseSync đã xác định là Night)
        if (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.IsSpawned)
        {
            UpdateBackgroundByPhase(NetworkPhaseSync.Instance.CurrentPhase.Value);
        }
        else
        {
            // Trạng thái ban đầu trước khi Network kết nối / spawn
            UpdateBackgroundByPhase(GamePhase.RoleReveal);

            // Bắt đầu coroutine nhẹ chờ NetworkPhaseSync spawn (chỉ chạy vài frame rồi kết thúc, không dùng Update)
            if (gameObject.activeInHierarchy)
            {
                waitPhaseSyncCoroutine = StartCoroutine(WaitForPhaseSyncRoutine());
            }
        }
    }

    /// <summary>
    /// Coroutine lắng nghe thời điểm NetworkPhaseSync spawn trên Client/Host để đồng bộ Phase ban đầu.
    /// Tự động kết thúc ngay khi đã đăng ký xong sự kiện (tránh kiểm tra liên tục trong Update).
    /// </summary>
    private IEnumerator WaitForPhaseSyncRoutine()
    {
        float timeout = 10f;
        float elapsed = 0f;

        while ((NetworkPhaseSync.Instance == null || !NetworkPhaseSync.Instance.IsSpawned) && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.IsSpawned)
        {
            NetworkPhaseSync.Instance.CurrentPhase.OnValueChanged -= HandleNetworkVariablePhaseChanged;
            NetworkPhaseSync.Instance.CurrentPhase.OnValueChanged += HandleNetworkVariablePhaseChanged;

            UpdateBackgroundByPhase(NetworkPhaseSync.Instance.CurrentPhase.Value);
            Debug.Log($"[GameBackgroundController] Đồng bộ Phase thành công từ NetworkPhaseSync: {NetworkPhaseSync.Instance.CurrentPhase.Value}");
        }

        waitPhaseSyncCoroutine = null;
    }

    private void HandlePhaseChanged(GamePhase newPhase)
    {
        UpdateBackgroundByPhase(newPhase);
    }

    private void HandleNetworkVariablePhaseChanged(GamePhase oldPhase, GamePhase newPhase)
    {
        UpdateBackgroundByPhase(newPhase);
    }

    /// <summary>
    /// Cập nhật hiển thị hình nền theo Phase:
    /// - Phase = Night: Hiện Background_Night, ẩn Background_Day.
    /// - Tất cả Phase khác (RoleReveal, ResolveNight, DayStart, Event, Task, Discussion, Voting, ResolveVote, GameOver...):
    ///   Hiện Background_Day, ẩn Background_Night.
    /// </summary>
    public void UpdateBackgroundByPhase(GamePhase phase)
    {
        CurrentActivePhase = phase;
        bool isNight = (phase == GamePhase.Night);

        if (backgroundNight != null && backgroundNight.activeSelf != isNight)
        {
            backgroundNight.SetActive(isNight);
        }

        if (backgroundDay != null && backgroundDay.activeSelf == isNight)
        {
            backgroundDay.SetActive(!isNight);
        }

        Debug.Log($"[GameBackgroundController] Phase = {phase} | Nền ban đêm: {isNight} | Nền ban ngày: {!isNight}");
    }

    /// <summary>
    /// Đảm bảo các GameObject Background_Day và Background_Night tồn tại trong Canvas
    /// với đầy đủ Sprite, AspectRatioFitter (EnvelopeParent 16:9) và Sibling Index dưới cùng.
    /// </summary>
    public void EnsureBackgroundElements()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindAnyObjectByType<Canvas>();
        }

        if (canvas == null)
        {
            Debug.LogWarning("[GameBackgroundController] Không tìm thấy Canvas trong scene!");
            return;
        }

        Transform canvasTransform = canvas.transform;

        // 1. Kiểm tra / Tìm / Tạo Background_Day
        if (backgroundDay == null)
        {
            Transform existingDay = canvasTransform.Find("Background_Day");
            if (existingDay != null)
            {
                backgroundDay = existingDay.gameObject;
            }
            else
            {
                backgroundDay = CreateBackgroundObject("Background_Day", canvasTransform);
            }
        }

        // 2. Kiểm tra / Tìm / Tạo Background_Night
        if (backgroundNight == null)
        {
            Transform existingNight = canvasTransform.Find("Background_Night");
            if (existingNight != null)
            {
                backgroundNight = existingNight.gameObject;
            }
            else
            {
                backgroundNight = CreateBackgroundObject("Background_Night", canvasTransform);
            }
        }

        // 3. Cấu hình Image & Sprite cho Background_Day
        if (dayImage == null && backgroundDay != null)
        {
            dayImage = backgroundDay.GetComponent<Image>();
        }
        if (dayImage != null)
        {
            if (daySprite != null)
            {
                dayImage.sprite = daySprite;
            }
#if UNITY_EDITOR
            else if (dayImage.sprite == null)
            {
                dayImage.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/sang.png");
            }
#endif
            dayImage.type = Image.Type.Simple;
            dayImage.color = Color.white;
            dayImage.raycastTarget = false;
        }

        // 4. Cấu hình Image & Sprite cho Background_Night
        if (nightImage == null && backgroundNight != null)
        {
            nightImage = backgroundNight.GetComponent<Image>();
        }
        if (nightImage != null)
        {
            if (nightSprite != null)
            {
                nightImage.sprite = nightSprite;
            }
#if UNITY_EDITOR
            else if (nightImage.sprite == null)
            {
                nightImage.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/toi.png");
            }
#endif
            nightImage.type = Image.Type.Simple;
            nightImage.color = Color.white;
            nightImage.raycastTarget = false;
        }

        // 5. Đảm bảo thứ tự Sibling Index dưới cùng (0 và 1) để không đè lên bất kỳ UI nào
        if (backgroundDay != null)
        {
            backgroundDay.transform.SetSiblingIndex(0);
        }
        if (backgroundNight != null)
        {
            backgroundNight.transform.SetSiblingIndex(1);
        }

        // 6. Trạng thái mặc định ban đầu: Day bật, Night tắt
        if (backgroundDay != null && !backgroundDay.activeSelf && (backgroundNight == null || !backgroundNight.activeSelf))
        {
            backgroundDay.SetActive(true);
        }
        if (backgroundNight != null && (backgroundDay != null && backgroundDay.activeSelf))
        {
            backgroundNight.SetActive(false);
        }
    }

    private GameObject CreateBackgroundObject(string objName, Transform parent)
    {
        GameObject bgObj = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AspectRatioFitter));
        bgObj.transform.SetParent(parent, false);

        RectTransform rt = bgObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        AspectRatioFitter arf = bgObj.GetComponent<AspectRatioFitter>();
        arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        arf.aspectRatio = 1920f / 1080f; // Tỉ lệ 16:9 chuẩn của sang.png và toi.png

        Image img = bgObj.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = Color.white;

        return bgObj;
    }

    /// <summary>
    /// Phương thức hỗ trợ kiểm thử tự động (Unit Test / Integration Test)
    /// </summary>
    public void SetPhaseForTesting(GamePhase phase)
    {
        UpdateBackgroundByPhase(phase);
    }
}

