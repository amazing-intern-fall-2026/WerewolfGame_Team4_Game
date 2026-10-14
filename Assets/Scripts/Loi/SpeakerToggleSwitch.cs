using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Component nút bật/tắt Loa (Speaker / Audio Output) dạng trượt hoặc bấm nút.
/// Hỗ trợ:
/// - Chuyển động trượt mượt mà (Smooth sliding animation).
/// - Đồng bộ tự động 2 chiều với NetworkVoiceManager (Vivox Output Mute).
/// - Click hoặc kéo thả (Drag) trực tiếp con trượt.
/// - Đổi màu thanh trượt và nhãn trạng thái BẬT / TẮT.
/// </summary>
public class SpeakerToggleSwitch : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI References")]
    [Tooltip("RectTransform của con trượt (Knob / Handle)")]
    [SerializeField] private RectTransform handleRect;

    [Tooltip("Image của thanh nền (Background Track)")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("Image của con trượt (Handle)")]
    [SerializeField] private Image handleImage;

    [Tooltip("(Tùy chọn) Text hiển thị trạng thái ON / OFF")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Tooltip("(Tùy chọn) Icon Loa để đổi màu theo trạng thái")]
    [SerializeField] private Image speakerIcon;

    [Header("Slider Positioning")]
    [Tooltip("Tự động tính toán vị trí On/Off dựa theo kích thước của Background và Handle")]
    [SerializeField] private bool autoCalculatePositions = true;

    [Tooltip("Khoảng đệm từ mép thanh trượt tới con trượt")]
    [SerializeField] private float padding = 4f;

    [Tooltip("Vị trí trục X của handle khi TẮT (OFF)")]
    [SerializeField] private float offPositionX = -25f;

    [Tooltip("Vị trí trục X của handle khi BẬT (ON)")]
    [SerializeField] private float onPositionX = 25f;

    [Header("Animation Settings")]
    [Tooltip("Thời gian trượt (giây)")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float animationDuration = 0.18f;

    [Header("Visual Styling - Colors")]
    [SerializeField] private Color onBackgroundColor = new Color(0.2f, 0.8f, 0.35f, 1f); // Xanh lá
    [SerializeField] private Color offBackgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f); // Xám
    [SerializeField] private Color onHandleColor = Color.white;
    [SerializeField] private Color offHandleColor = Color.white;
    [SerializeField] private Color onIconColor = Color.white;
    [SerializeField] private Color offIconColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("Text Labels (Tùy chọn)")]
    [SerializeField] private string onText = "BẬT";
    [SerializeField] private string offText = "TẮT";
    [SerializeField] private Color onTextColor = new Color(0.2f, 0.85f, 0.45f, 1f);
    [SerializeField] private Color offTextColor = new Color(0.9f, 0.35f, 0.35f, 1f);

    [Header("Initial State")]
    [Tooltip("Trạng thái Loa ban đầu (true = Bật / Unmuted, false = Tắt / Muted)")]
    [SerializeField] private bool isOn = true;

    [Header("Events")]
    public UnityEvent<bool> onToggleChanged;

    private Coroutine slideCoroutine;
    private bool isDragging;
    private float dragStartX;
    private float handleStartDragX;

    public bool IsOn => isOn;

    private void Awake()
    {
        CalculatePositionsIfNeeded();
    }

    private void Start()
    {
        SyncWithVoiceManager();
        UpdateVisualsInstant(isOn);
    }

    private void OnEnable()
    {
        NetworkVoiceManager.OnOutputMuteChanged += OnVoiceOutputMuteChanged;
        SyncWithVoiceManager();
    }

    private void OnDisable()
    {
        NetworkVoiceManager.OnOutputMuteChanged -= OnVoiceOutputMuteChanged;
    }

    /// <summary>
    /// Đồng bộ trạng thái hiện tại từ NetworkVoiceManager.
    /// </summary>
    private void SyncWithVoiceManager()
    {
        if (NetworkVoiceManager.Instance != null && NetworkVoiceManager.Instance.IsInitialized)
        {
            // IsOutputMuted = true -> Loa đang Tắt (isOn = false)
            bool active = !NetworkVoiceManager.Instance.IsOutputMuted;
            if (isOn != active)
            {
                isOn = active;
                UpdateVisualsInstant(isOn);
            }
        }
    }

    private void OnVoiceOutputMuteChanged(bool isMuted)
    {
        bool active = !isMuted;
        if (isOn != active)
        {
            isOn = active;
            if (gameObject.activeInHierarchy)
            {
                AnimateToState(isOn);
            }
            else
            {
                UpdateVisualsInstant(isOn);
            }
            onToggleChanged?.Invoke(isOn);
        }
    }

    private void CalculatePositionsIfNeeded()
    {
        if (!autoCalculatePositions || handleRect == null || backgroundImage == null)
            return;

        RectTransform bgRect = backgroundImage.rectTransform;
        float bgWidth = bgRect.rect.width;
        float handleWidth = handleRect.rect.width;

        float maxTravel = (bgWidth - handleWidth) * 0.5f - padding;
        if (maxTravel > 0)
        {
            onPositionX = maxTravel;
            offPositionX = -maxTravel;
        }
    }

    /// <summary>
    /// Đổi trạng thái Bật / Tắt Loa (gọi từ UI Button OnClick hoặc script).
    /// </summary>
    public void Toggle()
    {
        SetState(!isOn, true, true);
    }

    /// <summary>
    /// Đặt trạng thái Loa cụ thể.
    /// </summary>
    public void SetState(bool active, bool animate = true, bool notifyVoiceManager = true)
    {
        if (isOn == active && !isDragging)
            return;

        isOn = active;

        if (animate && gameObject.activeInHierarchy)
        {
            AnimateToState(isOn);
        }
        else
        {
            UpdateVisualsInstant(isOn);
        }

        if (notifyVoiceManager)
        {
            ApplyToVoiceManager(isOn);
        }

        onToggleChanged?.Invoke(isOn);
    }

    private void ApplyToVoiceManager(bool active)
    {
        if (NetworkVoiceManager.Instance != null)
        {
            // active = true -> Không mute (mute = false)
            NetworkVoiceManager.Instance.SetOutputMuted(!active);
        }
        else
        {
            Debug.Log($"[SpeakerToggleSwitch] Đã chuyển Loa: {(active ? "BẬT" : "TẮT")} (NetworkVoiceManager chưa khởi tạo).");
        }
    }

    #region Drag Interactions

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        dragStartX = eventData.position.x;
        if (handleRect != null)
        {
            handleStartDragX = handleRect.anchoredPosition.x;
        }

        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
            slideCoroutine = null;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || handleRect == null)
            return;

        float delta = eventData.position.x - dragStartX;
        float newX = Mathf.Clamp(handleStartDragX + delta, Mathf.Min(offPositionX, onPositionX), Mathf.Max(offPositionX, onPositionX));
        handleRect.anchoredPosition = new Vector2(newX, handleRect.anchoredPosition.y);

        float minX = Mathf.Min(offPositionX, onPositionX);
        float maxX = Mathf.Max(offPositionX, onPositionX);
        float t = Mathf.InverseLerp(minX, maxX, newX);
        if (offPositionX > onPositionX)
            t = 1f - t;

        UpdateColors(t);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        isDragging = false;
        if (handleRect != null)
        {
            float currentX = handleRect.anchoredPosition.x;
            float distToOn = Mathf.Abs(currentX - onPositionX);
            float distToOff = Mathf.Abs(currentX - offPositionX);

            bool newState = distToOn < distToOff;
            SetState(newState, true, true);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging)
            return;

        Toggle();
    }

    #endregion

    #region Visual Animations & Updates

    private void AnimateToState(bool active)
    {
        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
        }

        slideCoroutine = StartCoroutine(SlideRoutine(active));
    }

    private IEnumerator SlideRoutine(bool targetActive)
    {
        float targetX = targetActive ? onPositionX : offPositionX;
        Color targetBgColor = targetActive ? onBackgroundColor : offBackgroundColor;
        Color targetHandleColor = targetActive ? onHandleColor : offHandleColor;

        Vector2 startPos = handleRect != null ? handleRect.anchoredPosition : Vector2.zero;
        Vector2 targetPos = new Vector2(targetX, startPos.y);

        Color startBgColor = backgroundImage != null ? backgroundImage.color : targetBgColor;
        Color startHandleColor = handleImage != null ? handleImage.color : targetHandleColor;

        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);

            // Smooth ease in-out
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (handleRect != null)
            {
                handleRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, smoothT);
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = Color.Lerp(startBgColor, targetBgColor, smoothT);
            }

            if (handleImage != null)
            {
                handleImage.color = Color.Lerp(startHandleColor, targetHandleColor, smoothT);
            }

            yield return null;
        }

        UpdateVisualsInstant(targetActive);
        slideCoroutine = null;
    }

    private void UpdateVisualsInstant(bool active)
    {
        CalculatePositionsIfNeeded();

        float targetX = active ? onPositionX : offPositionX;
        if (handleRect != null)
        {
            handleRect.anchoredPosition = new Vector2(targetX, handleRect.anchoredPosition.y);
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = active ? onBackgroundColor : offBackgroundColor;
        }

        if (handleImage != null)
        {
            handleImage.color = active ? onHandleColor : offHandleColor;
        }

        if (statusText != null)
        {
            statusText.text = active ? onText : offText;
            statusText.color = active ? onTextColor : offTextColor;
        }

        if (speakerIcon != null)
        {
            speakerIcon.color = active ? onIconColor : offIconColor;
        }
    }

    private void UpdateColors(float t)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = Color.Lerp(offBackgroundColor, onBackgroundColor, t);
        }

        if (handleImage != null)
        {
            handleImage.color = Color.Lerp(offHandleColor, onHandleColor, t);
        }
    }

    #endregion
}
