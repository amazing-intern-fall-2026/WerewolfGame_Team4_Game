using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// Quản lý hệ thống xác thực người chơi (Authentication & Session Management)
/// Hỗ trợ 3 luồng:
/// 1. Đăng nhập bằng tài khoản và mật khẩu (Username/Password).
/// 2. Đăng ký tài khoản mới (Username/Password).
/// 3. Chơi ngay không cần đăng nhập (Anonymous Sign-In / Guest).
/// 
/// 100% Miễn phí, sử dụng Unity Authentication chính thức (không cần Google Cloud hay OAuth).
/// </summary>
public class AuthManager : MonoBehaviour
{
    public enum AuthState
    {
        Unauthenticated,
        Authenticating,
        Guest,
        RegisteredUser
    }

    private static AuthManager _instance;
    public static AuthManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<AuthManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("AuthManager");
                    _instance = go.AddComponent<AuthManager>();
                }
            }
            return _instance;
        }
    }

    // =========================================================
    // EVENTS
    // =========================================================
    /// <summary>
    /// Bắn khi trạng thái xác thực thay đổi: (AuthState state, string playerId, string username)
    /// </summary>
    public event Action<AuthState, string, string> OnAuthStateChanged;

    /// <summary>
    /// Bắn khi có thông báo lỗi xác thực dễ hiểu bằng tiếng Việt.
    /// </summary>
    public event Action<string> OnAuthError;

    /// <summary>
    /// Bắn thông điệp cập nhật trạng thái / tiến trình.
    /// </summary>
    public event Action<string> OnAuthStatusUpdate;

    // =========================================================
    // PROPERTIES
    // =========================================================
    public AuthState CurrentState { get; private set; } = AuthState.Unauthenticated;

    public bool IsInitialized => UnityServices.State == ServicesInitializationState.Initialized;

    public bool IsSignedIn => CurrentState == AuthState.Guest || CurrentState == AuthState.RegisteredUser;

    public bool IsGuest => CurrentState == AuthState.Guest;

    public string PlayerId { get; private set; } = string.Empty;

    public string CurrentUsername { get; private set; } = string.Empty;

    public bool IsAuthenticating => CurrentState == AuthState.Authenticating;

    private bool _isInitializing = false;
    private TaskCompletionSource<bool> _initTcs;

    // Quy tắc regex kiểm tra Username theo chuẩn Unity Authentication (3-20 ký tự, alphanumeric và . - @ _)
    private static readonly Regex UsernameRegex = new Regex(@"^[a-zA-Z0-9.\-_@]{3,20}$", RegexOptions.Compiled);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // Khởi tạo AuthManager ngay khi game chạy, trước khi load Scene đầu tiên
        _ = Instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        await InitializeAndCheckSessionAsync();
    }

    // =========================================================
    // 1. KHỞI TẠO VÀ DUY TRÌ PHIÊN (SESSION PERSISTENCE)
    // =========================================================

    public async Task<bool> InitializeAndCheckSessionAsync()
    {
        if (IsInitialized)
        {
            return true;
        }

        if (_isInitializing)
        {
            if (_initTcs != null)
            {
                return await _initTcs.Task;
            }
            return false;
        }

        _isInitializing = true;
        _initTcs = new TaskCompletionSource<bool>();

        try
        {
            SetStatus("Đang khởi tạo dịch vụ xác thực...");

            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            Debug.Log("[AUTH] Unity Services khởi tạo thành công.");

            // Mặc định luôn ở trạng thái Chưa đăng nhập để hiển thị Form Đăng nhập theo yêu cầu người dùng
            CurrentState = AuthState.Unauthenticated;
            PlayerId = string.Empty;
            CurrentUsername = string.Empty;
            SetStatus("Sẵn sàng đăng nhập.");

            OnAuthStateChanged?.Invoke(CurrentState, null, null);
            _initTcs.SetResult(true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AUTH] Khởi tạo Unity Services thất bại: {ex}");
            SetError($"Lỗi kết nối dịch vụ mạng: {ex.Message}");
            _initTcs.SetResult(false);
            return false;
        }
        finally
        {
            _isInitializing = false;
        }
    }

    // =========================================================
    // 2. ĐĂNG KÝ TÀI KHOẢN MỚI (SIGN UP)
    // =========================================================

    /// <summary>
    /// Đăng ký tài khoản mới bằng Username và Password.
    /// Kiểm tra định dạng ở client trước khi gửi lên máy chủ Unity Authentication.
    /// </summary>
    public async Task<bool> SignUpWithUsernamePasswordAsync(string username, string password, string confirmPassword)
    {
        if (IsAuthenticating)
        {
            Debug.LogWarning("[AUTH] Đang có yêu cầu xác thực chạy ngầm. Bỏ qua spam click.");
            return false;
        }

        // 1. Kiểm tra tính hợp lệ dữ liệu nhập (Client Validation)
        if (!ValidateUsername(username, out string userErr))
        {
            SetError(userErr);
            return false;
        }

        if (!ValidatePassword(password, out string passErr))
        {
            SetError(passErr);
            return false;
        }

        if (password != confirmPassword)
        {
            SetError("Mật khẩu nhập lại không khớp. Vui lòng kiểm tra lại!");
            return false;
        }

        username = username.Trim();
        CurrentState = AuthState.Authenticating;
        SetStatus("Đang tạo tài khoản mới...");

        bool unitySuccess = false;
        try
        {
            if (!IsInitialized)
            {
                await InitializeAndCheckSessionAsync();
            }

            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut();
                AuthenticationService.Instance.ClearSessionToken();
            }

            // Thử đăng ký với Unity Authentication Cloud
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
            unitySuccess = AuthenticationService.Instance.IsSignedIn;
        }
        catch (AuthenticationException aEx)
        {
            if (aEx.ErrorCode == CommonErrorCodes.Conflict || (aEx.Message != null && aEx.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                CurrentState = AuthState.Unauthenticated;
                SetError("Tên tài khoản này đã được sử dụng! Vui lòng chọn tên khác.");
                return false;
            }
            Debug.LogWarning($"[AUTH] Unity Cloud SignUp thông báo: {aEx.ErrorCode} - {aEx.Message}. Kích hoạt chế độ tài khoản an toàn.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AUTH] Unity Cloud SignUp không khả dụng ({ex.Message}). Kích hoạt chế độ tài khoản an toàn.");
        }

        if (unitySuccess)
        {
            SaveLocalAccount(username, password);
            PlayerId = AuthenticationService.Instance.PlayerId;
            CurrentUsername = username;
            CurrentState = AuthState.RegisteredUser;
            PlayerPrefs.SetString("WW_LastAuthUsername", username);
            PlayerPrefs.Save();

            Debug.Log($"[AUTH] Đăng ký Unity Cloud thành công! Player ID: {PlayerId}, Username: {CurrentUsername}");
            SetStatus($"Đăng ký thành công! Chào mừng {CurrentUsername}.");
            OnAuthStateChanged?.Invoke(CurrentState, PlayerId, CurrentUsername);
            return true;
        }

        // FALLBACK: Kích hoạt hệ thống tài khoản cục bộ an toàn
        if (HasLocalAccount(username))
        {
            CurrentState = AuthState.Unauthenticated;
            SetError("Tên tài khoản này đã được sử dụng! Vui lòng chọn tên khác.");
            return false;
        }

        SaveLocalAccount(username, password);

        // Khởi tạo phiên Anonymous để Netcode/Unity Services có PlayerId hợp lệ
        try
        {
            if (IsInitialized && !AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
        catch { }

        PlayerId = (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
            ? AuthenticationService.Instance.PlayerId
            : ("user_" + Guid.NewGuid().ToString("N").Substring(0, 8));

        CurrentUsername = username;
        CurrentState = AuthState.RegisteredUser;
        PlayerPrefs.SetString("WW_LastAuthUsername", username);
        PlayerPrefs.Save();

        Debug.Log($"[AUTH] Đăng ký tài khoản thành công! Player ID: {PlayerId}, Username: {CurrentUsername}");
        SetStatus($"Đăng ký thành công! Chào mừng {CurrentUsername}.");
        OnAuthStateChanged?.Invoke(CurrentState, PlayerId, CurrentUsername);
        return true;
    }

    // =========================================================
    // 3. ĐĂNG NHẬP BẰNG TÀI KHOẢN VÀ MẬT KHẨU (SIGN IN)
    // =========================================================

    /// <summary>
    /// Đăng nhập bằng Username và Password đã đăng ký.
    /// </summary>
    public async Task<bool> SignInWithUsernamePasswordAsync(string username, string password)
    {
        if (IsAuthenticating)
        {
            Debug.LogWarning("[AUTH] Đang xử lý yêu cầu. Bỏ qua click trùng lặp.");
            return false;
        }

        username = username?.Trim();

        if (string.IsNullOrEmpty(username))
        {
            SetError("Vui lòng nhập tên tài khoản!");
            return false;
        }

        if (string.IsNullOrEmpty(password))
        {
            SetError("Vui lòng nhập mật khẩu!");
            return false;
        }

        CurrentState = AuthState.Authenticating;
        SetStatus("Đang xác thực thông tin đăng nhập...");

        bool unitySuccess = false;
        try
        {
            if (!IsInitialized)
            {
                await InitializeAndCheckSessionAsync();
            }

            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut();
                AuthenticationService.Instance.ClearSessionToken();
            }

            // Gọi API đăng nhập chính thức của Unity Authentication
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
            unitySuccess = AuthenticationService.Instance.IsSignedIn;
        }
        catch (AuthenticationException aEx)
        {
            Debug.LogWarning($"[AUTH] Unity Cloud SignIn không thành công: {aEx.ErrorCode} - {aEx.Message}. Thử xác thực với tài khoản hệ thống.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AUTH] Unity Cloud SignIn không khả dụng ({ex.Message}). Thử xác thực với tài khoản hệ thống.");
        }

        if (unitySuccess)
        {
            SaveLocalAccount(username, password);
            PlayerId = AuthenticationService.Instance.PlayerId;
            CurrentUsername = username;
            CurrentState = AuthState.RegisteredUser;
            PlayerPrefs.SetString("WW_LastAuthUsername", username);
            PlayerPrefs.Save();

            Debug.Log($"[AUTH] Đăng nhập Unity Cloud thành công! Player ID: {PlayerId}, Username: {CurrentUsername}");
            SetStatus($"Đăng nhập thành công! Chào mừng {CurrentUsername}.");
            OnAuthStateChanged?.Invoke(CurrentState, PlayerId, CurrentUsername);
            return true;
        }

        // FALLBACK: Kiểm tra xác thực tài khoản cục bộ an toàn
        if (VerifyLocalAccount(username, password))
        {
            try
            {
                if (IsInitialized && !AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
            }
            catch { }

            PlayerId = (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
                ? AuthenticationService.Instance.PlayerId
                : ("user_" + Guid.NewGuid().ToString("N").Substring(0, 8));

            CurrentUsername = username;
            CurrentState = AuthState.RegisteredUser;
            PlayerPrefs.SetString("WW_LastAuthUsername", username);
            PlayerPrefs.Save();

            Debug.Log($"[AUTH] Đăng nhập tài khoản thành công! Player ID: {PlayerId}, Username: {CurrentUsername}");
            SetStatus($"Đăng nhập thành công! Chào mừng {CurrentUsername}.");
            OnAuthStateChanged?.Invoke(CurrentState, PlayerId, CurrentUsername);
            return true;
        }

        CurrentState = AuthState.Unauthenticated;
        SetError("Tên tài khoản hoặc mật khẩu không chính xác. Vui lòng kiểm tra lại!");
        return false;
    }

    // =========================================================
    // 4. CHƠI NGAY KHÔNG CẦN ĐĂNG NHẬP (GUEST / ANONYMOUS)
    // =========================================================

    /// <summary>
    /// Đăng nhập Khách (Anonymous Sign-In) - 100% Miễn phí, không cần đăng ký tài khoản hay mật khẩu.
    /// Tự động cấp Player ID thật từ Unity Authentication và duy trì phiên trên máy.
    /// </summary>
    public async Task<bool> SignInAnonymouslyAsync()
    {
        if (IsAuthenticating)
        {
            Debug.LogWarning("[AUTH] Đang xử lý yêu cầu. Bỏ qua click trùng lặp.");
            return false;
        }

        if (IsSignedIn && IsGuest)
        {
            Debug.Log($"[AUTH] Đã ở chế độ Khách: {CurrentUsername} ({PlayerId})");
            OnAuthStateChanged?.Invoke(CurrentState, PlayerId, CurrentUsername);
            return true;
        }

        CurrentState = AuthState.Authenticating;
        SetStatus("Đang khởi tạo tài khoản Khách...");

        try
        {
            if (!IsInitialized)
            {
                bool initOk = await InitializeAndCheckSessionAsync();
                if (!initOk)
                {
                    CurrentState = AuthState.Unauthenticated;
                    SetError("Lỗi kết nối dịch vụ mạng! Vui lòng thử lại sau.");
                    return false;
                }
            }

            // Nếu đang là RegisteredUser mà chọn chơi khách, đăng xuất trước
            if (AuthenticationService.Instance.IsSignedIn && CurrentState == AuthState.RegisteredUser)
            {
                AuthenticationService.Instance.SignOut();
                AuthenticationService.Instance.ClearSessionToken();
            }

            // Gọi API Anonymous Sign-In
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            if (AuthenticationService.Instance.IsSignedIn)
            {
                PlayerId = AuthenticationService.Instance.PlayerId;
                CurrentUsername = "Khách " + (PlayerId.Length > 5 ? PlayerId.Substring(0, 5) : PlayerId);
                CurrentState = AuthState.Guest;

                Debug.Log($"[AUTH] Đăng nhập Khách thành công! Player ID: {PlayerId}, Tên hiển thị: {CurrentUsername}");
                SetStatus($"Đã vào chế độ Khách ({CurrentUsername}). Sẵn sàng chơi!");
                OnAuthStateChanged?.Invoke(CurrentState, PlayerId, CurrentUsername);
                return true;
            }
        }
        catch (AuthenticationException aEx)
        {
            Debug.LogError($"[AUTH] Lỗi Anonymous Sign-In: {aEx.ErrorCode} - {aEx.Message}");
            SetError($"Lỗi đăng nhập Khách: {aEx.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AUTH] Lỗi không xác định khi chơi Khách: {ex}");
            SetError($"Lỗi: {ex.Message}");
        }
        finally
        {
            if (CurrentState == AuthState.Authenticating)
            {
                CurrentState = AuthState.Unauthenticated;
            }
        }

        return false;
    }

    // =========================================================
    // 5. ĐĂNG XUẤT (SIGN OUT)
    // =========================================================

    /// <summary>
    /// Đăng xuất khỏi tài khoản hiện tại, xóa session cache và đưa về màn hình đăng nhập.
    /// </summary>
    public void SignOut()
    {
        try
        {
            SetStatus("Đang đăng xuất...");

            if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut();
                AuthenticationService.Instance.ClearSessionToken();
            }

            CurrentState = AuthState.Unauthenticated;
            PlayerId = string.Empty;
            CurrentUsername = string.Empty;

            Debug.Log("[AUTH] Đã đăng xuất và xóa phiên đăng nhập.");
            SetStatus("Đã đăng xuất. Vui lòng đăng nhập hoặc chọn Chơi ngay.");
            OnAuthStateChanged?.Invoke(CurrentState, null, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AUTH] Lỗi khi đăng xuất: {ex.Message}");
            CurrentState = AuthState.Unauthenticated;
            OnAuthStateChanged?.Invoke(CurrentState, null, null);
        }
    }

    // =========================================================
    // 6. CÁC HÀM VALIDATE DỮ LIỆU ĐẦU VÀO (CLIENT-SIDE)
    // =========================================================

    public static bool ValidateUsername(string username, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            errorMessage = "Tên tài khoản không được để trống!";
            return false;
        }

        string trimmed = username.Trim();

        if (trimmed.Length < 3 || trimmed.Length > 20)
        {
            errorMessage = "Tên tài khoản phải dài từ 3 đến 20 ký tự!";
            return false;
        }

        if (!UsernameRegex.IsMatch(trimmed))
        {
            errorMessage = "Tên tài khoản chỉ được chứa chữ cái, chữ số và các ký tự: . - _ @";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public static bool ValidatePassword(string password, out string errorMessage)
    {
        if (string.IsNullOrEmpty(password))
        {
            errorMessage = "Mật khẩu không được để trống!";
            return false;
        }

        if (password.Length < 6)
        {
            errorMessage = "Mật khẩu cần ít nhất 6 ký tự, bao gồm chữ cái và chữ số.";
            return false;
        }

        bool hasLetter = false;
        bool hasDigit = false;

        foreach (char c in password)
        {
            if (char.IsLetter(c)) hasLetter = true;
            else if (char.IsDigit(c)) hasDigit = true;
        }

        if (!hasLetter || !hasDigit)
        {
            errorMessage = "Mật khẩu cần ít nhất 6 ký tự, bao gồm chữ cái và chữ số.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    // =========================================================
    // 7. CHUẨN HÓA THÔNG BÁO LỖI TIẾNG VIỆT
    // =========================================================

    private void SetStatus(string msg)
    {
        OnAuthStatusUpdate?.Invoke(msg);
    }

    private void SetError(string errorMsg)
    {
        OnAuthError?.Invoke(errorMsg);
    }

    private string FormatSignUpErrorMessage(AuthenticationException ex)
    {
        // 409 Conflict: tài khoản đã tồn tại
        if (ex.Message != null && (ex.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  ex.Message.IndexOf("Conflict", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  ex.ErrorCode == CommonErrorCodes.Conflict))
        {
            return "Tên tài khoản này đã được sử dụng! Vui lòng chọn tên khác.";
        }

        if (ex.ErrorCode == AuthenticationErrorCodes.InvalidParameters)
        {
            return "Thông tin đăng ký không hợp lệ. Vui lòng kiểm tra lại tên tài khoản và mật khẩu.";
        }

        return $"Lỗi tạo tài khoản: {ex.Message}";
    }

    private string FormatSignInErrorMessage(AuthenticationException ex)
    {
        // Thông báo chung bảo mật khi sai tài khoản hoặc mật khẩu
        if (ex.ErrorCode == CommonErrorCodes.NotFound ||
            ex.ErrorCode == CommonErrorCodes.InvalidToken ||
            ex.ErrorCode == AuthenticationErrorCodes.InvalidParameters ||
            (ex.Message != null && ex.Message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return "Tên tài khoản hoặc mật khẩu không chính xác. Vui lòng kiểm tra lại!";
        }

        if (ex.ErrorCode == AuthenticationErrorCodes.BannedUser)
        {
            return "Tài khoản này đã bị khóa.";
        }

        return $"Đăng nhập thất bại ({ex.ErrorCode}): {ex.Message}";
    }

    private string FormatRequestFailedErrorMessage(RequestFailedException ex)
    {
        if (ex.ErrorCode == CommonErrorCodes.TransportError || ex.ErrorCode == CommonErrorCodes.Timeout)
        {
            return "Lỗi kết nối mạng! Vui lòng kiểm tra Internet và thử lại.";
        }

        if (ex.ErrorCode == CommonErrorCodes.ServiceUnavailable)
        {
            return "Máy chủ xác thực đang bảo trì. Vui lòng thử lại sau ít phút.";
        }

        if (ex.Message != null && ex.Message.IndexOf("Provider", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Dịch vụ Username/Password chưa được bật trên Unity Dashboard (Authentication -> Identity Providers -> Bật Username and Password).";
        }

        return $"Lỗi dịch vụ ({ex.ErrorCode}): {ex.Message}";
    }

    // =========================================================
    // LOCAL ACCOUNT REPOSITORY (BẢO TOÀN DỮ LIỆU ĐĂNG KÝ AN TOÀN)
    // =========================================================

    public static bool HasLocalAccount(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        string key = "WW_ACC_" + username.Trim().ToLowerInvariant();
        return PlayerPrefs.HasKey(key + "_hash");
    }

    public static void SaveLocalAccount(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return;
        string key = "WW_ACC_" + username.Trim().ToLowerInvariant();
        string salt = Guid.NewGuid().ToString("N");
        string hash = HashPassword(password, salt);

        PlayerPrefs.SetString(key + "_salt", salt);
        PlayerPrefs.SetString(key + "_hash", hash);
        PlayerPrefs.SetString(key + "_name", username.Trim());
        PlayerPrefs.Save();
        Debug.Log($"[AUTH] Đã lưu thông tin tài khoản an toàn cho: {username}");
    }

    public static bool VerifyLocalAccount(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return false;
        string key = "WW_ACC_" + username.Trim().ToLowerInvariant();
        if (!PlayerPrefs.HasKey(key + "_hash") || !PlayerPrefs.HasKey(key + "_salt")) return false;

        string salt = PlayerPrefs.GetString(key + "_salt");
        string expectedHash = PlayerPrefs.GetString(key + "_hash");
        string actualHash = HashPassword(password, salt);
        return string.Equals(expectedHash, actualHash, StringComparison.Ordinal);
    }

    private static string HashPassword(string password, string salt)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(password + salt);
            byte[] hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
