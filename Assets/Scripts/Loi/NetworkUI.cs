using System;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Giao diện điều khiển kết nối mạng tại MainMenu.
/// Hỗ trợ tạo phòng Host với mã 6 chữ số và Client nhập mã 6 chữ số để tìm/kết nối đúng phòng.
/// </summary>
public class NetworkUI : MonoBehaviour
{
    [Header("UI References (Tự động gán/tạo nếu để trống)")]
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;

    private bool isConnecting = false;

    private void Awake()
    {
        EnsureUIReferences();
    }

    private void Start()
    {
        SetupAuthEvents();
        SetupButtonListeners();
        
        bool isSignedIn = AuthManager.Instance != null && AuthManager.Instance.IsSignedIn;
        string playerId = AuthManager.Instance != null ? AuthManager.Instance.PlayerId : null;
        string username = AuthManager.Instance != null ? AuthManager.Instance.CurrentUsername : null;
        UpdateAuthGating(isSignedIn, playerId, username);
    }

    private void OnDestroy()
    {
        TeardownAuthEvents();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // =========================================================
    // 1. TỰ ĐỘNG THIẾT LẬP / BỔ SUNG GIAO DIỆN (KHÔNG LÀM HỎNG SCENE)
    // =========================================================

    private void EnsureUIReferences()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return;

        // Tìm các nút đã có trong scene
        if (hostButton == null)
        {
            Transform hb = canvas.transform.Find("HostButton");
            if (hb != null) hostButton = hb.GetComponent<Button>();
        }

        if (clientButton == null)
        {
            Transform cb = canvas.transform.Find("ClientButton");
            if (cb != null) clientButton = cb.GetComponent<Button>();
        }

        // Đảm bảo AuthUI hoạt động trong scene
        if (FindFirstObjectByType<AuthUI>() == null)
        {
            gameObject.AddComponent<AuthUI>();
        }

        // Dọn dẹp các thành phần UI xác thực cũ nếu còn sót lại trên Canvas
        Transform oldGoogle = canvas.transform.Find("GoogleSignInButton");
        if (oldGoogle != null) Destroy(oldGoogle.gameObject);
        Transform oldGuest = canvas.transform.Find("GuestSignInButton");
        if (oldGuest != null) Destroy(oldGuest.gameObject);
        Transform oldAuthText = canvas.transform.Find("AuthAccountText");
        if (oldAuthText != null) Destroy(oldAuthText.gameObject);
        Transform oldSignOut = canvas.transform.Find("SignOutButton");
        if (oldSignOut != null) Destroy(oldSignOut.gameObject);

        // Tìm hoặc tạo ô nhập mã phòng
        if (roomCodeInput == null)
        {
            Transform inputTransform = canvas.transform.Find("RoomCodeInput");
            if (inputTransform != null)
            {
                roomCodeInput = inputTransform.GetComponent<TMP_InputField>();
            }
            else
            {
                CreateRoomCodeInputUI(canvas.transform);
            }
        }

        // Tìm hoặc tạo Text thông báo trạng thái
        if (statusText == null)
        {
            Transform stTransform = canvas.transform.Find("NetworkStatusText");
            if (stTransform != null)
            {
                statusText = stTransform.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                CreateStatusTextUI(canvas.transform);
            }
        }

        // Cấu hình InputField: chỉ nhận đúng 6 chữ số
        if (roomCodeInput != null)
        {
            roomCodeInput.characterLimit = 6;
            roomCodeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            roomCodeInput.onValueChanged.AddListener(OnRoomCodeInputChanged);
        }
    }

    private void CreateRoomCodeInputUI(Transform canvasTransform)
    {
        // Tạo container cho Input
        GameObject inputObj = new GameObject("RoomCodeInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObj.transform.SetParent(canvasTransform, false);

        RectTransform rect = inputObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        // Đặt ở vị trí giữa nút Host và nút Join hoặc ngay trên nút Join
        Vector2 pos = new Vector2(-350, -75);
        if (clientButton != null)
        {
            RectTransform cbRect = clientButton.GetComponent<RectTransform>();
            pos = new Vector2(cbRect.anchoredPosition.x, cbRect.anchoredPosition.y + 45);
        }
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(180, 36);

        Image bg = inputObj.GetComponent<Image>();
        bg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

        // Text Component
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(inputObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 0);
        textRect.offsetMax = new Vector2(-10, 0);

        TMP_FontAsset font = AuthUI.GetVietnameseFont();

        TextMeshProUGUI textComp = textObj.GetComponent<TextMeshProUGUI>();
        if (font != null)
        {
            textComp.font = font;
            textComp.fontSharedMaterial = font.material;
        }
        textComp.fontSize = 18;
        textComp.color = Color.white;
        textComp.alignment = TextAlignmentOptions.Center;
        textComp.fontStyle = FontStyles.Bold;

        // Placeholder Component
        GameObject placeholderObj = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        placeholderObj.transform.SetParent(inputObj.transform, false);
        RectTransform phRect = placeholderObj.GetComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(10, 0);
        phRect.offsetMax = new Vector2(-10, 0);

        TextMeshProUGUI phComp = placeholderObj.GetComponent<TextMeshProUGUI>();
        if (font != null)
        {
            phComp.font = font;
            phComp.fontSharedMaterial = font.material;
        }
        phComp.text = "Nhập mã 6 số...";
        phComp.fontSize = 15;
        phComp.fontStyle = FontStyles.Italic;
        phComp.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);
        phComp.alignment = TextAlignmentOptions.Center;

        roomCodeInput = inputObj.GetComponent<TMP_InputField>();
        roomCodeInput.textViewport = rect;
        roomCodeInput.textComponent = textComp;
        roomCodeInput.placeholder = phComp;
    }

    private void CreateStatusTextUI(Transform canvasTransform)
    {
        GameObject statusObj = new GameObject("NetworkStatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusObj.transform.SetParent(canvasTransform, false);

        RectTransform rect = statusObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Vector2 pos = new Vector2(-350, -170);
        if (clientButton != null)
        {
            RectTransform cbRect = clientButton.GetComponent<RectTransform>();
            pos = new Vector2(cbRect.anchoredPosition.x, cbRect.anchoredPosition.y - 45);
        }
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(340, 50);

        statusText = statusObj.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = AuthUI.GetVietnameseFont();
        if (font != null)
        {
            statusText.font = font;
            statusText.fontSharedMaterial = font.material;
        }
        statusText.fontSize = 14;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.textWrappingMode = TextWrappingModes.Normal;
    }

    private void SetupAuthEvents()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnAuthStateChanged += OnAuthStateChanged;
        }
    }

    private void TeardownAuthEvents()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnAuthStateChanged -= OnAuthStateChanged;
        }
    }

    private void OnAuthStateChanged(AuthManager.AuthState state, string playerId, string username)
    {
        bool isSignedIn = state == AuthManager.AuthState.RegisteredUser || state == AuthManager.AuthState.Guest;
        UpdateAuthGating(isSignedIn, playerId, username);
    }

    private void UpdateAuthGating(bool isSignedIn, string playerId, string username)
    {
        if (hostButton != null)
        {
            hostButton.interactable = isSignedIn;
        }

        if (clientButton != null)
        {
            clientButton.interactable = isSignedIn;
        }

        if (roomCodeInput != null)
        {
            roomCodeInput.interactable = isSignedIn;
        }

        if (isSignedIn)
        {
            SetStatus("Sẵn sàng. Tạo phòng hoặc nhập mã 6 số để tham gia.", Color.white);
        }
        else
        {
            SetStatus("Vui lòng Đăng nhập, Đăng ký hoặc chọn Chơi ngay để bắt đầu.", new Color(0.95f, 0.8f, 0.3f));
        }
    }

    private void SetupButtonListeners()
    {
        if (hostButton != null)
        {
            hostButton.onClick.RemoveAllListeners();
            hostButton.onClick.AddListener(StartHost);
        }

        if (clientButton != null)
        {
            clientButton.onClick.RemoveAllListeners();
            clientButton.onClick.AddListener(StartClient);
        }
    }

    private void OnRoomCodeInputChanged(string val)
    {
        // Tự động chuẩn hóa chỉ nhận chữ số và tối đa 6 ký tự
        if (string.IsNullOrEmpty(val)) return;

        string filtered = "";
        foreach (char c in val)
        {
            if (char.IsDigit(c) && filtered.Length < 6)
                filtered += c;
        }

        if (filtered != val)
        {
            roomCodeInput.text = filtered;
        }
    }

    private void SetStatus(string msg, Color col)
    {
        if (statusText != null)
        {
            statusText.text = msg;
            statusText.color = col;
        }
        Debug.Log($"NETWORK UI | Trạng thái: {msg}");
    }

    // =========================================================
    // 2. TẠO PHÒNG (HOST) VỚI MÃ 6 CHỮ SỐ
    // =========================================================

    public async void StartHost()
    {
        if (isConnecting)
            return;

        if (AuthManager.Instance == null || !AuthManager.Instance.IsSignedIn)
        {
            SetStatus("Vui lòng Đăng nhập, Đăng ký hoặc chọn Chơi ngay trước khi tạo phòng!", Color.red);
            return;
        }

        Debug.Log("NETWORK UI | Nhấn nút Tạo Phòng (Host)");

        if (NetworkManager.Singleton == null)
        {
            SetStatus("Lỗi: Không tìm thấy NetworkManager trong scene!", Color.red);
            return;
        }

        if (NetworkManager.Singleton.IsListening)
        {
            SetStatus("Cảnh báo: NetworkManager đã đang hoạt động!", Color.yellow);
            return;
        }

        isConnecting = true;
        SetStatus("Đang tạo phiên phòng mới và cấp phát mã...", Color.yellow);

        try
        {
            // 1. Sinh phiên phòng mới và mã phòng 6 chữ số
            RoomManager.RoomSessionData session = await RoomManager.CreateRoomSessionAsync();

            SetStatus($"Đã tạo mã phòng: {session.roomCode}. Đang khởi động Host...", Color.yellow);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            // 2. Thử khởi động Host với cơ chế tự động thử cổng (Auto-port fallback)
            // Đảm bảo khi test nhiều Host trên cùng 1 máy (MPPM) không bao giờ bị xung đột cổng!
            ushort initialPort = session.hostPort;
            bool result = false;
            ushort boundPort = initialPort;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                ushort portToTest = (ushort)(initialPort + attempt);

                if (transport != null)
                {
                    transport.SetConnectionData("127.0.0.1", portToTest, "127.0.0.1");
                }

                result = NetworkManager.Singleton.StartHost();

                if (result)
                {
                    boundPort = portToTest;
                    break;
                }
                else
                {
                    Debug.LogWarning($"NETWORK UI | Cổng {portToTest} bị từ chối hoặc đang được dùng. Đang thử cổng tiếp theo ({portToTest + 1})...");
                }
            }

            Debug.Log($"NETWORK UI | StartHost result: {result} trên cổng {boundPort}");

            if (result)
            {
                session.hostPort = boundPort;
                RoomManager.RegisterRoomInLocalRegistry(session);

                SetStatus($"Tạo phòng {session.roomCode} thành công (Cổng {boundPort})! Đang vào Lobby...", Color.green);

                // Đăng ký callback
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

                // Load sang Scene Lobby
                NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
            }
            else
            {
                isConnecting = false;
                await RoomManager.CloseCurrentRoomAsync();
                SetStatus($"Khởi động Host thất bại sau khi thử các cổng từ {initialPort} đến {initialPort + 9}! Hãy thử lại.", Color.red);
            }
        }
        catch (Exception ex)
        {
            isConnecting = false;
            await RoomManager.CloseCurrentRoomAsync();
            SetStatus($"Lỗi khi tạo phòng: {ex.Message}", Color.red);
            Debug.LogError($"NETWORK UI | Exception in StartHost: {ex}");
        }
    }

    // =========================================================
    // 3. VÀO PHÒNG (CLIENT) BẰNG MÃ 6 CHỮ SỐ
    // =========================================================

    public async void StartClient()
    {
        if (isConnecting)
            return;

        if (AuthManager.Instance == null || !AuthManager.Instance.IsSignedIn)
        {
            SetStatus("Vui lòng Đăng nhập, Đăng ký hoặc chọn Chơi ngay trước khi vào phòng!", Color.red);
            return;
        }

        string code = roomCodeInput != null ? roomCodeInput.text.Trim() : "";

        // Kiểm tra tính hợp lệ của mã phòng
        if (!RoomManager.IsValidRoomCode(code))
        {
            SetStatus("Vui lòng nhập đúng mã phòng gồm 6 chữ số (ví dụ: 004281)!", Color.red);
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            SetStatus("Lỗi: Không tìm thấy NetworkManager trong scene!", Color.red);
            return;
        }

        if (NetworkManager.Singleton.IsListening)
        {
            SetStatus("Cảnh báo: NetworkManager đã đang hoạt động!", Color.yellow);
            return;
        }

        isConnecting = true;
        SetStatus($"Đang tìm kiếm phòng '{code}'...", Color.yellow);

        try
        {
            // 1. Tra cứu thông tin phòng
            RoomLookupResult lookup = await RoomManager.LookupRoomAsync(code);

            if (!lookup.success || lookup.sessionData == null)
            {
                isConnecting = false;
                SetStatus(lookup.errorMessage ?? $"Không tìm thấy phòng {code}!", Color.red);
                return;
            }

            RoomManager.RoomSessionData session = lookup.sessionData;
            RoomManager.CurrentRoomCode = session.roomCode;
            RoomManager.CurrentVoiceChannelId = session.voiceChannelId;
            RoomManager.IsHostOfCurrentRoom = false;

            SetStatus($"Đã tìm thấy phòng {session.roomCode}. Đang kết nối...", Color.yellow);

            // 2. Cấu hình Transport
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                Debug.Log($"NETWORK UI | Client cấu hình kết nối tới {session.hostAddress}:{session.hostPort} (Phòng {session.roomCode})");
                transport.SetConnectionData(session.hostAddress, session.hostPort);
            }

            // 3. Đăng ký callback kiểm tra kết nối
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            // 4. Khởi động Client
            bool result = NetworkManager.Singleton.StartClient();
            Debug.Log($"NETWORK UI | StartClient result: {result}");

            if (!result)
            {
                isConnecting = false;
                SetStatus($"Không thể khởi động kết nối Client tới phòng {code}!", Color.red);
            }
            else
            {
                SetStatus($"Đang kết nối tới phòng {code}... Vui lòng đợi.", Color.yellow);
                StartCoroutine(ClientConnectionTimeoutWatchdog(10f));
            }
        }
        catch (Exception ex)
        {
            isConnecting = false;
            SetStatus($"Lỗi kết nối phòng: {ex.Message}", Color.red);
            Debug.LogError($"NETWORK UI | Exception in StartClient: {ex}");
        }
    }

    private IEnumerator ClientConnectionTimeoutWatchdog(float timeoutSeconds)
    {
        float timer = 0f;
        while (isConnecting && timer < timeoutSeconds)
        {
            timer += Time.unscaledDeltaTime;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                // Đã kết nối thành công, NGO SceneManager sẽ tự động load scene Lobby
                yield break;
            }
            yield return null;
        }

        if (isConnecting && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient))
        {
            isConnecting = false;
            SetStatus("Kết nối quá thời gian chờ (Timeout). Host có thể đã ngắt kết nối hoặc mạng bị chặn.", Color.red);
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            isConnecting = false;
            SetStatus("Kết nối thành công! Đang chuyển vào Lobby...", Color.green);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            isConnecting = false;
            SetStatus("Mất kết nối hoặc bị ngắt kết nối khỏi Host.", Color.red);
        }
    }
}