using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý giao diện HUD Voice (Microphone & Loa/Speaker) trong Game Scene cho game Ma Sói màn hình ngang (Landscape).
/// Tuân thủ nghiêm ngặt các quy tắc:
/// 1. Vị trí góc trên bên phải (Top-Right), kích thước nhỏ gọn (~220x96 dp), không che Player Card, Skill buttons hay Chat UI.
/// 2. Khi vào Game Scene, micro luôn ở trạng thái TẮT (Muted) an toàn theo mặc định.
/// 3. Tuyệt đối không tự động unmute khi Awake, Start, đổi Scene, vào voice channel hay reconnect.
/// 4. Người chơi phải chủ động bấm nút Microphone để bật micro của mình.
/// 5. Nút Speaker cho phép bật/tắt nhận âm thanh từ người chơi khác (mặc định BẬT).
/// 6. Đồng bộ 2 chiều với NetworkVoiceManager và trạng thái phần cứng Vivox thực tế.
/// </summary>
public class GameVoiceHUD : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("RectTransform khung chứa Voice HUD")]
    [SerializeField] private RectTransform containerRect;

    [Tooltip("Panel nút Microphone")]
    [SerializeField] private RectTransform micPanel;

    [Tooltip("Nút / Switch Microphone")]
    [SerializeField] private MicToggleSwitch micToggle;

    [Tooltip("Panel nút Loa (Speaker)")]
    [SerializeField] private RectTransform speakerPanel;

    [Tooltip("Nút / Switch Loa (Speaker)")]
    [SerializeField] private SpeakerToggleSwitch speakerToggle;

    [Tooltip("Button component trên panel Mic để chạm toàn bộ panel")]
    [SerializeField] private Button micPanelButton;

    [Tooltip("Button component trên panel Loa để chạm toàn bộ panel")]
    [SerializeField] private Button speakerPanelButton;

    [Header("Layout Settings")]
    [SerializeField] private Vector2 panelPosition = new Vector2(-20f, -20f);
    [SerializeField] private Vector2 panelSize = new Vector2(248f, 116f);

    public bool IsMicrophoneOn => micToggle != null && micToggle.IsOn;
    public bool IsSpeakerOn => speakerToggle != null && speakerToggle.IsOn;

    private void Awake()
    {
        EnsureUIReferences();
        ApplySafeDefaultMute();
    }

    private void Start()
    {
        ApplySafeDefaultMute();
        StartCoroutine(SyncVoiceStateAfterInit());
    }

    private void OnEnable()
    {
        NetworkVoiceManager.OnMicrophoneMuteChanged += HandleMicrophoneMuteChanged;
        NetworkVoiceManager.OnOutputMuteChanged += HandleOutputMuteChanged;

        if (micPanelButton != null)
        {
            micPanelButton.onClick.RemoveListener(OnMicPanelClicked);
            micPanelButton.onClick.AddListener(OnMicPanelClicked);
        }

        if (speakerPanelButton != null)
        {
            speakerPanelButton.onClick.RemoveListener(OnSpeakerPanelClicked);
            speakerPanelButton.onClick.AddListener(OnSpeakerPanelClicked);
        }

        SyncVisualsWithManager();
    }

    private void OnDisable()
    {
        NetworkVoiceManager.OnMicrophoneMuteChanged -= HandleMicrophoneMuteChanged;
        NetworkVoiceManager.OnOutputMuteChanged -= HandleOutputMuteChanged;

        if (micPanelButton != null)
        {
            micPanelButton.onClick.RemoveListener(OnMicPanelClicked);
        }

        if (speakerPanelButton != null)
        {
            speakerPanelButton.onClick.RemoveListener(OnSpeakerPanelClicked);
        }
    }

    /// <summary>
    /// Đảm bảo khi vào Game Scene, micro luôn bị MUTE (tắt) an toàn.
    /// </summary>
    private void ApplySafeDefaultMute()
    {
        if (NetworkVoiceManager.Instance != null)
        {
            // Reset về trạng thái Muted an toàn
            NetworkVoiceManager.Instance.ResetToSafeDefaultMute();
        }

        if (micToggle != null)
        {
            micToggle.SetState(false, animate: false, notifyVoiceManager: false);
        }
    }

    private IEnumerator SyncVoiceStateAfterInit()
    {
        // Chờ NetworkVoiceManager sẵn sàng để đồng bộ trạng thái chính xác
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (NetworkVoiceManager.Instance != null && NetworkVoiceManager.Instance.IsInitialized)
            {
                break;
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        SyncVisualsWithManager();
    }

    private void SyncVisualsWithManager()
    {
        if (NetworkVoiceManager.Instance != null)
        {
            // Mic: Muted = true -> IsOn = false
            bool micActive = !NetworkVoiceManager.Instance.IsMicrophoneMuted;
            if (micToggle != null && micToggle.IsOn != micActive)
            {
                micToggle.SetState(micActive, animate: false, notifyVoiceManager: false);
            }

            // Speaker: Muted = true -> IsOn = false
            bool speakerActive = !NetworkVoiceManager.Instance.IsOutputMuted;
            if (speakerToggle != null && speakerToggle.IsOn != speakerActive)
            {
                speakerToggle.SetState(speakerActive, animate: false, notifyVoiceManager: false);
            }
        }
        else
        {
            // Mặc định: Mic Tắt, Loa Bật
            if (micToggle != null && micToggle.IsOn)
            {
                micToggle.SetState(false, animate: false, notifyVoiceManager: false);
            }
            if (speakerToggle != null && !speakerToggle.IsOn)
            {
                speakerToggle.SetState(true, animate: false, notifyVoiceManager: false);
            }
        }
    }

    private void HandleMicrophoneMuteChanged(bool isMuted)
    {
        bool active = !isMuted;
        if (micToggle != null && micToggle.IsOn != active)
        {
            micToggle.SetState(active, animate: true, notifyVoiceManager: false);
        }
    }

    private void HandleOutputMuteChanged(bool isMuted)
    {
        bool active = !isMuted;
        if (speakerToggle != null && speakerToggle.IsOn != active)
        {
            speakerToggle.SetState(active, animate: true, notifyVoiceManager: false);
        }
    }

    private void OnMicPanelClicked()
    {
        ToggleMicrophone();
    }

    private void OnSpeakerPanelClicked()
    {
        ToggleSpeaker();
    }

    public void ToggleMicrophone()
    {
        if (micToggle != null)
        {
            micToggle.Toggle();
        }
        if (NetworkVoiceManager.Instance != null)
        {
            NetworkVoiceManager.Instance.SetMicrophoneMuted(!IsMicrophoneOn);
        }
    }

    public void ToggleSpeaker()
    {
        if (speakerToggle != null)
        {
            speakerToggle.Toggle();
        }
        if (NetworkVoiceManager.Instance != null)
        {
            NetworkVoiceManager.Instance.SetOutputMuted(!IsSpeakerOn);
        }
    }

    /// <summary>
    /// Tự động tìm kiếm hoặc dựng cây UI Voice HUD tại góc trên bên phải nếu chưa tồn tại.
    /// </summary>
    public void EnsureUIReferences()
    {
        if (containerRect == null)
        {
            containerRect = transform as RectTransform;
        }

        if (containerRect != null)
        {
            containerRect.anchorMin = new Vector2(1f, 1f);
            containerRect.anchorMax = new Vector2(1f, 1f);
            containerRect.pivot = new Vector2(1f, 1f);
            containerRect.anchoredPosition = panelPosition;
            containerRect.sizeDelta = panelSize;
        }

        // 1. Kiểm tra / tạo VoiceMicPanel
        if (micPanel == null)
        {
            Transform found = transform.Find("VoiceMicPanel");
            if (found != null)
            {
                micPanel = found as RectTransform;
            }
            else
            {
                micPanel = CreatePanelObject("VoiceMicPanel", 0f, "Micro (Mic)", "", isMic: true);
            }
        }

        if (micPanel != null)
        {
            Transform micIcon = micPanel.Find("Icon");
            if (micIcon != null) Destroy(micIcon.gameObject);

            if (micToggle == null)
            {
                micToggle = micPanel.GetComponentInChildren<MicToggleSwitch>(true);
            }
            if (micPanelButton == null)
            {
                micPanelButton = micPanel.GetComponent<Button>();
            }
        }

        // 2. Kiểm tra / tạo VoiceSpeakerPanel
        if (speakerPanel == null)
        {
            Transform found = transform.Find("VoiceSpeakerPanel");
            if (found != null)
            {
                speakerPanel = found as RectTransform;
            }
            else
            {
                speakerPanel = CreatePanelObject("VoiceSpeakerPanel", -58f, "Loa (Speaker)", "", isMic: false);
            }
        }

        if (speakerPanel != null)
        {
            Transform spkIcon = speakerPanel.Find("Icon");
            if (spkIcon != null) Destroy(spkIcon.gameObject);

            if (speakerToggle == null)
            {
                speakerToggle = speakerPanel.GetComponentInChildren<SpeakerToggleSwitch>(true);
            }
            if (speakerPanelButton == null)
            {
                speakerPanelButton = speakerPanel.GetComponent<Button>();
            }
        }
    }

    private RectTransform CreatePanelObject(string panelName, float yOffset, string title, string iconStr, bool isMic)
    {
        GameObject panelObj = new GameObject(panelName, typeof(RectTransform), typeof(Image), typeof(Button));
        panelObj.transform.SetParent(transform, false);

        RectTransform rt = panelObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(0f, 54f);

        Image bg = panelObj.GetComponent<Image>();
        bg.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);

        Button btn = panelObj.GetComponent<Button>();
        btn.targetGraphic = bg;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        btn.colors = cb;

        // Title (canh lề thoáng đẹp không icon unicode)
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(panelObj.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(0f, 0.5f);
        titleRt.pivot = new Vector2(0f, 0.5f);
        titleRt.anchoredPosition = new Vector2(16f, 9f);
        titleRt.sizeDelta = new Vector2(145f, 22f);

        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.text = title;
        titleTmp.fontSize = 15;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = Color.white;
        titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Status Label
        GameObject statusObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusObj.transform.SetParent(panelObj.transform, false);
        RectTransform statusRt = statusObj.GetComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0f, 0.5f);
        statusRt.anchorMax = new Vector2(0f, 0.5f);
        statusRt.pivot = new Vector2(0f, 0.5f);
        statusRt.anchoredPosition = new Vector2(16f, -10f);
        statusRt.sizeDelta = new Vector2(145f, 18f);

        TextMeshProUGUI statusTmp = statusObj.GetComponent<TextMeshProUGUI>();
        statusTmp.text = isMic ? "TẮT" : "BẬT";
        statusTmp.fontSize = 13;
        statusTmp.fontStyle = FontStyles.Bold;
        statusTmp.color = isMic ? new Color(0.9f, 0.35f, 0.35f, 1f) : new Color(0.2f, 0.85f, 0.45f, 1f);
        statusTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Switch Slider Track
        GameObject switchObj = new GameObject(
            isMic ? "MicToggleSwitch" : "SpeakerToggleSwitch",
            typeof(RectTransform),
            typeof(Image),
            isMic ? typeof(MicToggleSwitch) : typeof(SpeakerToggleSwitch)
        );
        switchObj.transform.SetParent(panelObj.transform, false);
        RectTransform switchRt = switchObj.GetComponent<RectTransform>();
        switchRt.anchorMin = new Vector2(1f, 0.5f);
        switchRt.anchorMax = new Vector2(1f, 0.5f);
        switchRt.pivot = new Vector2(1f, 0.5f);
        switchRt.anchoredPosition = new Vector2(-12f, 0f);
        switchRt.sizeDelta = new Vector2(62f, 32f);

        Image switchBg = switchObj.GetComponent<Image>();
        switchBg.color = isMic ? new Color(0.5f, 0.5f, 0.5f, 1f) : new Color(0.2f, 0.8f, 0.35f, 1f);

        // Switch Handle (Knob)
        GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObj.transform.SetParent(switchObj.transform, false);
        RectTransform handleRt = handleObj.GetComponent<RectTransform>();
        handleRt.anchorMin = new Vector2(0.5f, 0.5f);
        handleRt.anchorMax = new Vector2(0.5f, 0.5f);
        handleRt.pivot = new Vector2(0.5f, 0.5f);
        handleRt.anchoredPosition = new Vector2(isMic ? -16f : 16f, 0f);
        handleRt.sizeDelta = new Vector2(24f, 24f);

        Image handleImg = handleObj.GetComponent<Image>();
        handleImg.color = Color.white;

        // Wire references using Reflection for private serialized fields
        if (isMic)
        {
            var sw = switchObj.GetComponent<MicToggleSwitch>();
            SetPrivateField(sw, "handleRect", handleRt);
            SetPrivateField(sw, "backgroundImage", switchBg);
            SetPrivateField(sw, "handleImage", handleImg);
            SetPrivateField(sw, "statusText", statusTmp);
            SetPrivateField(sw, "isOn", false);
            sw.CalculatePositionsIfNeeded();
        }
        else
        {
            var sw = switchObj.GetComponent<SpeakerToggleSwitch>();
            SetPrivateField(sw, "handleRect", handleRt);
            SetPrivateField(sw, "backgroundImage", switchBg);
            SetPrivateField(sw, "handleImage", handleImg);
            SetPrivateField(sw, "statusText", statusTmp);
            SetPrivateField(sw, "isOn", true);
        }

        return rt;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null) return;
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }
}
