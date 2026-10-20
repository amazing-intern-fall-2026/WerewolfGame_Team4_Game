using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Component nút bật/tắt microphone dạng trượt (Toggle Switch / Slider Switch).
/// Hỗ trợ:
/// - Chuyển động trượt mượt mà (Smooth sliding animation).
/// - Chuyển màu nền (Background color transition) hoặc đổi ảnh (Sprite swap On/Off).
/// - Click hoặc kéo thả (Drag) trực tiếp con trượt.
/// - Đồng bộ tự động 2 chiều với NetworkVoiceManager (Vivox Voice Chat).
/// </summary>
public class MicToggleSwitch : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
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

    [Tooltip("(Tùy chọn) Icon Micro để đổi màu theo trạng thái")]
    [SerializeField] private Image micIcon;

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

    [Header("Visual Styling - Sprites (Tùy chọn)")]
    [Tooltip("Sprite nền khi BẬT (ví dụ buttonOn)")]
    [SerializeField] private Sprite onSprite;

    [Tooltip("Sprite nền khi TẮT (ví dụ ButtonOff)")]
    [SerializeField] private Sprite offSprite;

    [Header("Text Labels (Tùy chọn)")]
    [SerializeField] private string onText = "BẬT";
    [SerializeField] private string offText = "TẮT";
    [SerializeField] private Color onTextColor = new Color(0.2f, 0.85f, 0.45f, 1f);
    [SerializeField] private Color offTextColor = new Color(0.9f, 0.35f, 0.35f, 1f);

    [Header("Initial State")]
    [Tooltip("Trạng thái Mic ban đầu (true = Bật / Unmuted, false = Tắt / Muted)")]
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
        // Kiểm tra trạng thái mic thực tế từ NetworkVoiceManager khi khởi động
        SyncWithVoiceManager();
        UpdateVisualsInstant(isOn);
    }

    private void OnEnable()
    {
        NetworkVoiceManager.OnMicrophoneMuteChanged += OnVoiceMuteChanged;
        SyncWithVoiceManager();
    }

    private void OnDisable()
    {
        NetworkVoiceManager.OnMicrophoneMuteChanged -= OnVoiceMuteChanged;
    }

    /// <summary>
    /// Đồng bộ trạng thái hiện tại từ NetworkVoiceManager.
    /// </summary>
    private void SyncWithVoiceManager()
    {
        if (NetworkVoiceManager.Instance != null && NetworkVoiceManager.Instance.IsInitialized)
        {
            // IsMicrophoneMuted = true -> Mic đang Tắt (isOn = false)
            bool active = !NetworkVoiceManager.Instance.IsMicrophoneMuted;
            if (isOn != active)
            {
                isOn = active;
                UpdateVisualsInstant(isOn);
            }
        }
    }

    private void OnVoiceMuteChanged(bool isMuted)
    {
        bool newOnState = !isMuted;
        if (isOn != newOnState)
        {
            isOn = newOnState;
            AnimateSlider(isOn);
            onToggleChanged?.Invoke(isOn);
        }
    }

    /// <summary>
    /// Tự tính toán khoảng trượt dựa theo kích thước RectTransform.
    /// </summary>
    public void CalculatePositionsIfNeeded()
    {
        if (!autoCalculatePositions) return;

        RectTransform bgRect = backgroundImage != null ? backgroundImage.rectTransform : transform as RectTransform;
        if (bgRect != null && handleRect != null)
        {
            float bgWidth = bgRect.rect.width;
            float handleWidth = handleRect.rect.width;

            if (bgWidth > handleWidth && handleWidth > 0)
            {
                float travel = (bgWidth - handleWidth) * 0.5f - padding;
                offPositionX = -Mathf.Max(0, travel);
                onPositionX = Mathf.Max(0, travel);
            }
        }
    }

    /// <summary>
    /// Được gọi khi người dùng click vào button/switch.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging) return;
        Toggle();
    }

    /// <summary>
    /// Đảo trạng thái Mic ON/OFF.
    /// </summary>
    public void Toggle()
    {
        SetState(!isOn, animate: true, notifyVoiceManager: true);
    }

    /// <summary>
    /// Đặt trạng thái cụ thể.
    /// </summary>
    public void SetState(bool state, bool animate = true, bool notifyVoiceManager = true)
    {
        isOn = state;

        if (animate && gameObject.activeInHierarchy)
        {
            AnimateSlider(isOn);
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
            if (!NetworkVoiceManager.Instance.IsInLobbyVoice)
            {
                // Nếu chưa vào voice channel, tham gia kênh và áp dụng trạng thái
                NetworkVoiceManager.Instance.JoinLobbyVoice();
            }

            NetworkVoiceManager.Instance.SetMicrophoneMuted(!active);
        }
        else
        {
            Debug.Log($"[MicToggleSwitch] Đã chuyển Mic: {(active ? "BẬT" : "TẮT")} (NetworkVoiceManager chưa khởi tạo).");
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
        if (handleRect == null) return;

        float deltaX = eventData.position.x - dragStartX;
        float currentX = Mathf.Clamp(handleStartDragX + deltaX, offPositionX, onPositionX);

        Vector2 pos = handleRect.anchoredPosition;
        pos.x = currentX;
        handleRect.anchoredPosition = pos;

        // Nội suy màu sắc trong lúc kéo
        float t = Mathf.InverseLerp(offPositionX, onPositionX, currentX);
        UpdateVisualsInterpolated(t);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        if (handleRect == null) return;

        float currentX = handleRect.anchoredPosition.x;
        float midPoint = (onPositionX + offPositionX) * 0.5f;

        // Thả tay: nếu vượt quá nửa điểm thì snap sang On hoặc Off
        bool targetState = currentX > midPoint;
        SetState(targetState, animate: true, notifyVoiceManager: true);
    }

    #endregion

    #region Visual Animations

    private void AnimateSlider(bool targetState)
    {
        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
        }

        slideCoroutine = StartCoroutine(SlideRoutine(targetState));
    }

    private IEnumerator SlideRoutine(bool targetState)
    {
        float targetX = targetState ? onPositionX : offPositionX;
        float startX = handleRect != null ? handleRect.anchoredPosition.x : targetX;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            // SmoothStep tạo chuyển động trượt mượt mà (ease-in-out)
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (handleRect != null)
            {
                Vector2 pos = handleRect.anchoredPosition;
                pos.x = Mathf.Lerp(startX, targetX, smoothT);
                handleRect.anchoredPosition = pos;
            }

            float colorT = Mathf.InverseLerp(offPositionX, onPositionX, handleRect != null ? handleRect.anchoredPosition.x : (targetState ? onPositionX : offPositionX));
            UpdateVisualsInterpolated(colorT);

            yield return null;
        }

        // Đảm bảo kết thúc chính xác
        if (handleRect != null)
        {
            Vector2 finalPos = handleRect.anchoredPosition;
            finalPos.x = targetX;
            handleRect.anchoredPosition = finalPos;
        }

        UpdateVisualsInstant(targetState);
        slideCoroutine = null;
    }

    private void UpdateVisualsInterpolated(float t)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = Color.Lerp(offBackgroundColor, onBackgroundColor, t);
        }

        if (handleImage != null)
        {
            handleImage.color = Color.Lerp(offHandleColor, onHandleColor, t);
        }

        if (micIcon != null)
        {
            micIcon.color = Color.Lerp(offIconColor, onIconColor, t);
        }

        if (statusText != null)
        {
            statusText.color = Color.Lerp(offTextColor, onTextColor, t);
        }
    }

    private void UpdateVisualsInstant(bool state)
    {
        CalculatePositionsIfNeeded();

        if (handleRect != null)
        {
            Vector2 pos = handleRect.anchoredPosition;
            pos.x = state ? onPositionX : offPositionX;
            handleRect.anchoredPosition = pos;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = state ? onBackgroundColor : offBackgroundColor;

            if (state && onSprite != null)
            {
                backgroundImage.sprite = onSprite;
            }
            else if (!state && offSprite != null)
            {
                backgroundImage.sprite = offSprite;
            }
        }

        if (handleImage != null)
        {
            handleImage.color = state ? onHandleColor : offHandleColor;
        }

        if (micIcon != null)
        {
            micIcon.color = state ? onIconColor : offIconColor;
        }

        if (statusText != null)
        {
            statusText.text = state ? onText : offText;
            statusText.color = state ? onTextColor : offTextColor;
        }
    }

    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        CalculatePositionsIfNeeded();
        if (!Application.isPlaying)
        {
            UpdateVisualsInstant(isOn);
        }
    }

    [ContextMenu("Gán vị trí hiện tại làm vị trí ON")]
    private void CaptureOnPosition()
    {
        if (handleRect != null)
        {
            onPositionX = handleRect.anchoredPosition.x;
            autoCalculatePositions = false;
        }
    }

    [ContextMenu("Gán vị trí hiện tại làm vị trí OFF")]
    private void CaptureOffPosition()
    {
        if (handleRect != null)
        {
            offPositionX = handleRect.anchoredPosition.x;
            autoCalculatePositions = false;
        }
    }
#endif
}
