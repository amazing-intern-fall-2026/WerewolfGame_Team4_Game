using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Giao diện điều khiển Đăng nhập, Đăng ký và Chơi ngay (Khách) cho game Ma Sói.
/// Thiết kế chuẩn modal chiếm 2/3 màn hình, to rõ, tối ưu cho thiết bị di động (Android & iOS).
/// Hỗ trợ font tiếng Việt 100% không bị lỗi glyph, hiển thị trực tiếp Form Đăng nhập ngay khi mở game.
/// </summary>
public class AuthUI : MonoBehaviour
{
    [Header("Panel Containers")]
    [SerializeField] private GameObject authCardRoot;
    [SerializeField] private GameObject modalDialog;
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject registerPanel;
    [SerializeField] private GameObject userBadgePanel;

    [Header("Login Form References")]
    [SerializeField] private TMP_InputField loginUsernameInput;
    [SerializeField] private TMP_InputField loginPasswordInput;
    [SerializeField] private Button loginSubmitButton;
    [SerializeField] private Button goToRegisterButton;
    [SerializeField] private Button playAsGuestButton;
    [SerializeField] private TextMeshProUGUI loginStatusText;

    [Header("Register Form References")]
    [SerializeField] private TMP_InputField regUsernameInput;
    [SerializeField] private TMP_InputField regPasswordInput;
    [SerializeField] private TMP_InputField regConfirmPasswordInput;
    [SerializeField] private Button registerSubmitButton;
    [SerializeField] private Button backToLoginButton;
    [SerializeField] private TextMeshProUGUI regStatusText;

    [Header("User Badge References (Khi đã đăng nhập)")]
    [SerializeField] private TextMeshProUGUI userBadgeText;
    [SerializeField] private Button signOutButton;

    private static TMP_FontAsset _vietnameseFont;

    private void Awake()
    {
        EnsureUIReferences();
    }

    private void Start()
    {
        SetupButtonListeners();
        SetupAuthEvents();

        // Kiểm tra nếu đã đăng nhập trước đó (ví dụ vừa thoát phòng từ Game/Lobby về MainMenu)
        if (AuthManager.Instance != null && AuthManager.Instance.IsSignedIn)
        {
            UpdateUIState(AuthManager.Instance.CurrentState, AuthManager.Instance.PlayerId, AuthManager.Instance.CurrentUsername);
        }
        else
        {
            // Lần đầu mở game hoặc chưa đăng nhập: hiển thị Form Đăng nhập
            ShowLoginPanel();
        }
    }

    private void OnDestroy()
    {
        TeardownAuthEvents();
    }

    // =========================================================
    // 1. LẮNG NGHE SỰ KIỆN AUTH MANAGER
    // =========================================================

    private void SetupAuthEvents()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnAuthStateChanged -= OnAuthStateChanged;
            AuthManager.Instance.OnAuthStateChanged += OnAuthStateChanged;
            AuthManager.Instance.OnAuthStatusUpdate -= OnAuthStatusUpdate;
            AuthManager.Instance.OnAuthStatusUpdate += OnAuthStatusUpdate;
            AuthManager.Instance.OnAuthError -= OnAuthError;
            AuthManager.Instance.OnAuthError += OnAuthError;
        }
    }

    private void TeardownAuthEvents()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnAuthStateChanged -= OnAuthStateChanged;
            AuthManager.Instance.OnAuthStatusUpdate -= OnAuthStatusUpdate;
            AuthManager.Instance.OnAuthError -= OnAuthError;
        }
    }

    private void OnAuthStateChanged(AuthManager.AuthState state, string playerId, string username)
    {
        UpdateUIState(state, playerId, username);
    }

    private void OnAuthStatusUpdate(string msg)
    {
        SetCurrentStatus(msg, new Color(0.95f, 0.8f, 0.3f));
    }

    private void OnAuthError(string errorMsg)
    {
        SetCurrentStatus(errorMsg, new Color(1f, 0.35f, 0.35f));
        SetButtonsInteractable(true);
    }

    private void UpdateUIState(AuthManager.AuthState state, string playerId, string username)
    {
        SetButtonsInteractable(true);

        if (state == AuthManager.AuthState.RegisteredUser || state == AuthManager.AuthState.Guest)
        {
            // Đã đăng nhập: ẩn modal 2/3 màn hình, hiển thị huy hiệu người chơi
            if (authCardRoot != null) authCardRoot.SetActive(false);
            if (userBadgePanel != null) userBadgePanel.SetActive(true);

            if (userBadgeText != null)
            {
                if (state == AuthManager.AuthState.RegisteredUser)
                {
                    userBadgeText.text = $"<color=#68D391>Tài khoản:</color> <b>{username}</b>";
                }
                else
                {
                    userBadgeText.text = $"<color=#F6AD55>Chế độ Khách:</color> <b>{username}</b>";
                }
            }

            ClearPasswordFields();
        }
        else
        {
            // Chưa đăng nhập: luôn hiện popup đăng nhập 2/3 màn hình
            if (authCardRoot != null) authCardRoot.SetActive(true);
            if (userBadgePanel != null) userBadgePanel.SetActive(false);
            ShowLoginPanel();
        }
    }

    private void ClearPasswordFields()
    {
        if (loginPasswordInput != null) loginPasswordInput.text = string.Empty;
        if (regPasswordInput != null) regPasswordInput.text = string.Empty;
        if (regConfirmPasswordInput != null) regConfirmPasswordInput.text = string.Empty;
    }

    // =========================================================
    // 2. CHUYỂN ĐỔI GIỮA CÁC FORM
    // =========================================================

    public void ShowLoginPanel()
    {
        if (authCardRoot != null)
        {
            authCardRoot.SetActive(true);
            authCardRoot.transform.SetAsLastSibling();
        }
        if (loginPanel != null) loginPanel.SetActive(true);
        if (registerPanel != null) registerPanel.SetActive(false);
        if (userBadgePanel != null) userBadgePanel.SetActive(false);

        SetCurrentStatus(string.Empty, Color.white);
    }

    public void ShowRegisterPanel()
    {
        if (authCardRoot != null)
        {
            authCardRoot.SetActive(true);
            authCardRoot.transform.SetAsLastSibling();
        }
        if (loginPanel != null) loginPanel.SetActive(false);
        if (registerPanel != null) registerPanel.SetActive(true);
        if (userBadgePanel != null) userBadgePanel.SetActive(false);

        SetCurrentStatus("Quy định: Tên 3-20 ký tự. Mật khẩu cần ít nhất 6 ký tự, bao gồm chữ cái và chữ số.", new Color(0.82f, 0.88f, 0.98f));
    }

    private void SetCurrentStatus(string message, Color color)
    {
        if (loginPanel != null && loginPanel.activeSelf && loginStatusText != null)
        {
            loginStatusText.text = message;
            loginStatusText.color = color;
        }
        else if (registerPanel != null && registerPanel.activeSelf && regStatusText != null)
        {
            regStatusText.text = message;
            regStatusText.color = color;
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (loginSubmitButton != null) loginSubmitButton.interactable = interactable;
        if (goToRegisterButton != null) goToRegisterButton.interactable = interactable;
        if (playAsGuestButton != null) playAsGuestButton.interactable = interactable;
        if (registerSubmitButton != null) registerSubmitButton.interactable = interactable;
        if (backToLoginButton != null) backToLoginButton.interactable = interactable;
    }

    // =========================================================
    // 3. XỬ LÝ SỰ KIỆN CLICK NÚT BẤM
    // =========================================================

    private void SetupButtonListeners()
    {
        if (loginSubmitButton != null)
        {
            loginSubmitButton.onClick.RemoveAllListeners();
            loginSubmitButton.onClick.AddListener(OnLoginSubmitClicked);
        }

        if (goToRegisterButton != null)
        {
            goToRegisterButton.onClick.RemoveAllListeners();
            goToRegisterButton.onClick.AddListener(ShowRegisterPanel);
        }

        if (playAsGuestButton != null)
        {
            playAsGuestButton.onClick.RemoveAllListeners();
            playAsGuestButton.onClick.AddListener(OnPlayAsGuestClicked);
        }

        if (registerSubmitButton != null)
        {
            registerSubmitButton.onClick.RemoveAllListeners();
            registerSubmitButton.onClick.AddListener(OnRegisterSubmitClicked);
        }

        if (backToLoginButton != null)
        {
            backToLoginButton.onClick.RemoveAllListeners();
            backToLoginButton.onClick.AddListener(ShowLoginPanel);
        }

        if (signOutButton != null)
        {
            signOutButton.onClick.RemoveAllListeners();
            signOutButton.onClick.AddListener(OnSignOutClicked);
        }
    }

    private async void OnLoginSubmitClicked()
    {
        string username = loginUsernameInput != null ? loginUsernameInput.text : string.Empty;
        string password = loginPasswordInput != null ? loginPasswordInput.text : string.Empty;

        if (string.IsNullOrWhiteSpace(username))
        {
            SetCurrentStatus("Vui lòng nhập tên tài khoản!", new Color(1f, 0.4f, 0.4f));
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            SetCurrentStatus("Vui lòng nhập mật khẩu!", new Color(1f, 0.4f, 0.4f));
            return;
        }

        SetButtonsInteractable(false);
        SetCurrentStatus("Đang kiểm tra thông tin tài khoản...", new Color(0.95f, 0.8f, 0.3f));

        bool success = await AuthManager.Instance.SignInWithUsernamePasswordAsync(username, password);
        if (!success)
        {
            SetButtonsInteractable(true);
        }
    }

    private async void OnRegisterSubmitClicked()
    {
        string username = regUsernameInput != null ? regUsernameInput.text : string.Empty;
        string password = regPasswordInput != null ? regPasswordInput.text : string.Empty;
        string confirmPass = regConfirmPasswordInput != null ? regConfirmPasswordInput.text : string.Empty;

        SetButtonsInteractable(false);
        SetCurrentStatus("Đang gửi yêu cầu đăng ký lên hệ thống...", new Color(0.95f, 0.8f, 0.3f));

        bool success = await AuthManager.Instance.SignUpWithUsernamePasswordAsync(username, password, confirmPass);
        if (!success)
        {
            SetButtonsInteractable(true);
        }
    }

    private async void OnPlayAsGuestClicked()
    {
        SetButtonsInteractable(false);
        SetCurrentStatus("Đang khởi tạo tài khoản Khách...", new Color(0.95f, 0.8f, 0.3f));

        bool success = await AuthManager.Instance.SignInAnonymouslyAsync();
        if (!success)
        {
            SetButtonsInteractable(true);
        }
    }

    private void OnSignOutClicked()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.SignOut();
        }
        ShowLoginPanel();
    }

    // =========================================================
    // 4. QUẢN LÝ FONT TIẾNG VIỆT (KHÔNG BỊ LỖI GLYPH)
    // =========================================================

    public static TMP_FontAsset GetVietnameseFont()
    {
        if (_vietnameseFont != null) return _vietnameseFont;

        // 1. Tải LiberationSans SDF và LiberationSans SDF - Fallback từ Resources
        TMP_FontAsset mainFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        TMP_FontAsset fallbackFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");

        if (mainFont != null)
        {
            if (fallbackFont != null)
            {
                if (mainFont.fallbackFontAssetTable == null)
                {
                    mainFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
                }
                if (!mainFont.fallbackFontAssetTable.Contains(fallbackFont))
                {
                    mainFont.fallbackFontAssetTable.Insert(0, fallbackFont);
                }

                // Nạp đầy đủ toàn bộ ký tự tiếng Việt có dấu vào fallback atlas động
                string allVietnameseChars = "aAàÀảẢãÃáÁạẠăĂằẰẳẲẵẴắẮặẶâÂầẦẩẨẫẪấẤậẬbBcCdDđĐeEèÈẻẺẽẼéÉẹẸêÊềỀểỂễỄếẾệỆ" +
                                            "fFgGhHiIìÌỉỈĩĨíÍịỊjJkKlLmMnNoOòÒỏỎõÕóÓọỌôÔồỒổỔỗỖốỐộỘơƠờỜởỞỡỠớỚợỢ" +
                                            "pPqQrRsStTuUùÙủỦũŨúÚụỤưƯừỪửỬữỮứỨựỰvVwWxXyYỳỲỷỶỹỸýÝỵỴzZ" +
                                            "0123456789!@#$%^&*()_+-=[]{}|;':\",./<>?~` 🐺⚡👤📝←→•● ";
                fallbackFont.TryAddCharacters(allVietnameseChars);
            }
            _vietnameseFont = mainFont;
            return _vietnameseFont;
        }

        // 2. Tìm từ bất kỳ TextMeshProUGUI nào đã có sẵn trong Canvas
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            var existingTmp = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
            if (existingTmp != null && existingTmp.font != null)
            {
                _vietnameseFont = existingTmp.font;
                return _vietnameseFont;
            }
        }

        // 3. Fallback TMP_Settings mặc định
        _vietnameseFont = TMP_Settings.defaultFontAsset;
        return _vietnameseFont;
    }

    // =========================================================
    // 5. TỰ ĐỘNG KHỞI TẠO UI KÍCH THƯỚC 2/3 MÀN HÌNH (SELF-HEALING)
    // =========================================================

    private void EnsureUIReferences()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindAnyObjectByType<Canvas>();
        }
        if (canvas == null) return;

        // Nếu đã có tham chiếu và còn tồn tại dưới canvas, chỉ cần đồng bộ lại trạng thái
        if (authCardRoot != null && authCardRoot.transform.parent == canvas.transform &&
            userBadgePanel != null && userBadgePanel.transform.parent == canvas.transform)
        {
            bool signedIn = AuthManager.Instance != null && AuthManager.Instance.IsSignedIn;
            authCardRoot.SetActive(!signedIn);
            userBadgePanel.SetActive(signedIn);
            if (signedIn)
            {
                UpdateUIState(AuthManager.Instance.CurrentState, AuthManager.Instance.PlayerId, AuthManager.Instance.CurrentUsername);
            }
            return;
        }

        // Xóa sạch root cũ nếu có phiên bản trước đó trong canvas (xử lý tương thích cả PlayMode và EditMode)
        while (canvas.transform.Find("AuthCardRoot") != null)
        {
            Transform oldRoot = canvas.transform.Find("AuthCardRoot");
            if (Application.isPlaying)
            {
                Destroy(oldRoot.gameObject);
                break;
            }
            else
            {
                DestroyImmediate(oldRoot.gameObject);
            }
        }

        while (canvas.transform.Find("UserBadgePanel") != null)
        {
            Transform oldBadge = canvas.transform.Find("UserBadgePanel");
            if (Application.isPlaying)
            {
                Destroy(oldBadge.gameObject);
                break;
            }
            else
            {
                DestroyImmediate(oldBadge.gameObject);
            }
        }

        CreateFullAuthUI(canvas.transform);
    }

    private void CreateFullAuthUI(Transform canvasTransform)
    {
        // 1. Tạo Root Overlay: Phủ toàn bộ màn hình với nền tối mờ (Dim Backdrop)
        GameObject rootObj = new GameObject("AuthCardRoot", typeof(RectTransform), typeof(Image));
        rootObj.transform.SetParent(canvasTransform, false);
        rootObj.transform.SetAsLastSibling();
        authCardRoot = rootObj;

        RectTransform rootRect = rootObj.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image rootBg = rootObj.GetComponent<Image>();
        rootBg.color = new Color(0f, 0f, 0f, 0.70f); // Làm mờ cảnh nền phía sau để tập trung vào hộp thoại

        // 2. Tạo Hộp thoại chính (Modal Dialog): Chiếm đúng 2/3 màn hình ở chính giữa
        GameObject dialogObj = new GameObject("ModalDialog", typeof(RectTransform), typeof(Image));
        dialogObj.transform.SetParent(rootObj.transform, false);
        modalDialog = dialogObj;

        RectTransform dialogRect = dialogObj.GetComponent<RectTransform>();
        // Chiếm 2/3 chiều ngang (từ 0.166 đến 0.834) và 80% chiều dọc (từ 0.10 đến 0.90)
        dialogRect.anchorMin = new Vector2(0.166f, 0.10f);
        dialogRect.anchorMax = new Vector2(0.834f, 0.90f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.offsetMin = Vector2.zero;
        dialogRect.offsetMax = Vector2.zero;

        Image dialogBg = dialogObj.GetComponent<Image>();
        dialogBg.color = new Color(0.10f, 0.12f, 0.17f, 0.98f);

        // Tạo khung viền nổi bật (Border Glow)
        GameObject borderObj = new GameObject("BorderOutline", typeof(RectTransform), typeof(Image));
        borderObj.transform.SetParent(dialogObj.transform, false);
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-2, -2);
        borderRect.offsetMax = new Vector2(2, 2);
        Image borderImg = borderObj.GetComponent<Image>();
        borderImg.color = new Color(0.24f, 0.35f, 0.52f, 0.40f);
        borderObj.transform.SetAsFirstSibling();

        // 3. Tạo Login Panel
        CreateLoginPanelUI(dialogObj.transform);

        // 4. Tạo Register Panel
        CreateRegisterPanelUI(dialogObj.transform);

        // 5. Tạo User Badge Panel (hiển thị khi đã đăng nhập)
        CreateUserBadgeUI(canvasTransform);

        // Khởi tạo trạng thái hiển thị ban đầu dựa trên trạng thái đăng nhập thực tế (tránh chớp tắt frame khi vừa thoát phòng)
        bool alreadySignedIn = AuthManager.Instance != null && AuthManager.Instance.IsSignedIn;
        if (authCardRoot != null) authCardRoot.SetActive(!alreadySignedIn);
        if (userBadgePanel != null) userBadgePanel.SetActive(alreadySignedIn);
        if (alreadySignedIn)
        {
            UpdateUIState(AuthManager.Instance.CurrentState, AuthManager.Instance.PlayerId, AuthManager.Instance.CurrentUsername);
        }
    }

    private void CreateLoginPanelUI(Transform parent)
    {
        GameObject panel = new GameObject("LoginPanel", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        loginPanel = panel;

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Tiêu đề to nổi bật (42px Bold)
        CreateText(panel.transform, "TitleText", "ĐĂNG NHẬP MA SÓI", new Vector2(0, 280), new Vector2(700, 60), 42, Color.white, FontStyles.Bold);

        // Phụ đề hướng dẫn (24px)
        CreateText(panel.transform, "SubGuideText", "Đăng nhập tài khoản hoặc chọn Chơi ngay để bắt đầu", new Vector2(0, 224), new Vector2(700, 36), 24, new Color(0.72f, 0.78f, 0.88f), FontStyles.Italic);

        // Ô nhập Username: Rộng 560 x 88, font 28px
        loginUsernameInput = CreateInputField(panel.transform, "LoginUsernameInput", "Tên tài khoản...", new Vector2(0, 138), new Vector2(560, 88), false);

        // Ô nhập Password: Rộng 560 x 88, font 28px (Khoảng cách với username: 24px)
        loginPasswordInput = CreateInputField(panel.transform, "LoginPasswordInput", "Mật khẩu...", new Vector2(0, 26), new Vector2(560, 88), true);

        // Nút ĐĂNG NHẬP: 560 x 96, font 32px Bold (Khoảng cách với ô mật khẩu: 30px)
        loginSubmitButton = CreateButton(panel.transform, "LoginSubmitBtn", "ĐĂNG NHẬP", new Vector2(0, -96), new Vector2(560, 96), new Color(0.18f, 0.48f, 0.88f, 1f), 32);

        // Nút CHƠI NGAY (KHÁCH): To nổi bật 560 x 96, font 30px Bold (Khoảng cách với nút Đăng nhập: 28px)
        playAsGuestButton = CreateButton(panel.transform, "PlayAsGuestBtn", "CHƠI NGAY (KHÁCH - MIỄN PHÍ)", new Vector2(0, -220), new Vector2(560, 96), new Color(0.16f, 0.65f, 0.46f, 1f), 30);

        // Nút Chuyển sang Đăng ký: 560 x 76, font 26px Bold (Khoảng cách với nút Khách: 26px)
        goToRegisterButton = CreateButton(panel.transform, "GoToRegisterBtn", "Chưa có tài khoản? Đăng ký ngay", new Vector2(0, -332), new Vector2(560, 76), new Color(0.16f, 0.20f, 0.28f, 0.95f), 26);

        // Text thông báo trạng thái: 700 x 44, font 24px
        loginStatusText = CreateText(panel.transform, "LoginStatusText", "Sẵn sàng đăng nhập hoặc chọn Chơi ngay.", new Vector2(0, -400), new Vector2(700, 44), 24, new Color(0.8f, 0.86f, 0.95f), FontStyles.Normal);
    }

    private void CreateRegisterPanelUI(Transform parent)
    {
        GameObject panel = new GameObject("RegisterPanel", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        registerPanel = panel;
        registerPanel.SetActive(false); // Ẩn mặc định

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Tiêu đề To Đẹp (42px Bold)
        CreateText(panel.transform, "RegTitleText", "ĐĂNG KÝ TÀI KHOẢN", new Vector2(0, 290), new Vector2(700, 60), 42, Color.white, FontStyles.Bold);

        // Hướng dẫn quy định tài khoản (22px)
        CreateText(panel.transform, "RegSubGuideText", "Nhập thông tin đăng ký tài khoản người chơi Ma Sói mới", new Vector2(0, 236), new Vector2(700, 36), 22, new Color(0.72f, 0.78f, 0.88f), FontStyles.Italic);

        // Ô nhập Username: Rộng 560 x 82, font 28px
        regUsernameInput = CreateInputField(panel.transform, "RegUsernameInput", "Tên tài khoản (3 - 20 ký tự)...", new Vector2(0, 150), new Vector2(560, 82), false);

        // Ô nhập Password: Rộng 560 x 82, font 28px (Khoảng cách với username: 24px)
        regPasswordInput = CreateInputField(panel.transform, "RegPasswordInput", "Mật khẩu (tối thiểu 6 ký tự, gồm chữ cái và số)...", new Vector2(0, 44), new Vector2(560, 82), true);

        // Ô nhập lại Password: Rộng 560 x 82, font 28px (Khoảng cách với password: 24px)
        regConfirmPasswordInput = CreateInputField(panel.transform, "RegConfirmPasswordInput", "Nhập lại mật khẩu để xác nhận...", new Vector2(0, -62), new Vector2(560, 82), true);

        // Nút Đăng ký: To nổi bật 560 x 96, font 32px Bold (Khoảng cách với ô nhập: 31px)
        registerSubmitButton = CreateButton(panel.transform, "RegisterSubmitBtn", "TẠO TÀI KHOẢN MỚI", new Vector2(0, -182), new Vector2(560, 96), new Color(0.18f, 0.48f, 0.88f, 1f), 32);

        // Nút Quay lại Đăng nhập: 560 x 76, font 26px Bold (Khoảng cách với nút Đăng ký: 24px)
        backToLoginButton = CreateButton(panel.transform, "BackToLoginBtn", "ĐÃ CÓ TÀI KHOẢN? QUAY LẠI ĐĂNG NHẬP", new Vector2(0, -292), new Vector2(560, 76), new Color(0.16f, 0.20f, 0.28f, 0.95f), 26);

        // Text thông báo lỗi / trạng thái: 700 x 44, font 24px
        regStatusText = CreateText(panel.transform, "RegStatusText", "", new Vector2(0, -362), new Vector2(700, 44), 24, new Color(0.82f, 0.88f, 0.98f), FontStyles.Normal);
    }

    private void CreateUserBadgeUI(Transform canvasTransform)
    {
        GameObject badgeObj = new GameObject("UserBadgePanel", typeof(RectTransform), typeof(Image));
        badgeObj.transform.SetParent(canvasTransform, false);
        userBadgePanel = badgeObj;
        userBadgePanel.SetActive(false);

        RectTransform rect = badgeObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-350, 165); // Nằm ngay phía trên nút Host ở bên trái với khoảng cách thoáng
        rect.sizeDelta = new Vector2(420, 76);

        Image bg = badgeObj.GetComponent<Image>();
        bg.color = new Color(0.11f, 0.14f, 0.20f, 0.96f);

        // Text thông tin user
        string initialBadgeText = "Đã đăng nhập";
        if (AuthManager.Instance != null && AuthManager.Instance.IsSignedIn && !string.IsNullOrEmpty(AuthManager.Instance.CurrentUsername))
        {
            initialBadgeText = AuthManager.Instance.CurrentState == AuthManager.AuthState.RegisteredUser
                ? $"<color=#68D391>Tài khoản:</color> <b>{AuthManager.Instance.CurrentUsername}</b>"
                : $"<color=#F6AD55>Chế độ Khách:</color> <b>{AuthManager.Instance.CurrentUsername}</b>";
        }
        userBadgeText = CreateText(badgeObj.transform, "UserBadgeText", initialBadgeText, new Vector2(-65, 0), new Vector2(270, 50), 24, Color.white, FontStyles.Bold);
        userBadgeText.alignment = TextAlignmentOptions.Left;

        // Nút Đăng xuất
        signOutButton = CreateButton(badgeObj.transform, "SignOutBtn", "Đăng xuất", new Vector2(145, 0), new Vector2(115, 54), new Color(0.52f, 0.18f, 0.18f, 0.95f), 22);
    }

    // =========================================================
    // 6. CÁC HÀM TIỆN ÍCH TẠO UI ELEMENT (GÁN FONT CHUẨN)
    // =========================================================

    private TextMeshProUGUI CreateText(Transform parent, string name, string content, Vector2 pos, Vector2 size, float fontSize, Color color, FontStyles style)
    {
        GameObject textObj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(parent, false);

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        
        // Gán font tiếng Việt chuẩn
        TMP_FontAsset font = GetVietnameseFont();
        if (font != null)
        {
            tmp.font = font;
            tmp.fontSharedMaterial = font.material;
        }

        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;

        return tmp;
    }

    private Button CreateButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, Color bgColor, float fontSize = 16f)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = img;

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();

        // Gán font tiếng Việt chuẩn
        TMP_FontAsset font = GetVietnameseFont();
        if (font != null)
        {
            tmp.font = font;
            tmp.fontSharedMaterial = font.material;
        }

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    private TMP_InputField CreateInputField(Transform parent, string name, string placeholderText, Vector2 pos, Vector2 size, bool isPassword)
    {
        GameObject inputObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObj.transform.SetParent(parent, false);

        RectTransform rect = inputObj.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Image bg = inputObj.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.12f, 0.98f);

        TMP_FontAsset font = GetVietnameseFont();

        // Text component
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(inputObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24, 0); // Đệm chữ thụt vào trong thoải mái
        textRect.offsetMax = new Vector2(-24, 0);

        TextMeshProUGUI textComp = textObj.GetComponent<TextMeshProUGUI>();
        if (font != null)
        {
            textComp.font = font;
            textComp.fontSharedMaterial = font.material;
        }
        textComp.fontSize = 28;
        textComp.color = Color.white;
        textComp.alignment = TextAlignmentOptions.Left;

        // Placeholder component
        GameObject phObj = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        phObj.transform.SetParent(inputObj.transform, false);
        RectTransform phRect = phObj.GetComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(24, 0);
        phRect.offsetMax = new Vector2(-24, 0);

        TextMeshProUGUI phComp = phObj.GetComponent<TextMeshProUGUI>();
        if (font != null)
        {
            phComp.font = font;
            phComp.fontSharedMaterial = font.material;
        }
        phComp.text = placeholderText;
        phComp.fontSize = 26;
        phComp.fontStyle = FontStyles.Italic;
        phComp.color = new Color(0.6f, 0.68f, 0.78f, 0.75f);
        phComp.alignment = TextAlignmentOptions.Left;

        TMP_InputField inputField = inputObj.GetComponent<TMP_InputField>();
        inputField.textViewport = rect;
        inputField.textComponent = textComp;
        inputField.placeholder = phComp;

        if (isPassword)
        {
            inputField.contentType = TMP_InputField.ContentType.Password;
            inputField.inputType = TMP_InputField.InputType.Password;
            inputField.asteriskChar = '*';
        }

        return inputField;
    }
}
