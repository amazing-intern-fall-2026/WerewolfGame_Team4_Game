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

            // Nếu đang có phiên đăng nhập cũ, đăng xuất sạch sẽ trước khi tạo tài khoản mới
            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut();
                AuthenticationService.Instance.ClearSessionToken();
            }

            // Gọi API đăng ký chính thức của Unity Authentication
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);

            if (AuthenticationService.Instance.IsSignedIn)
            {
                PlayerId = AuthenticationService.Instance.PlayerId;
                CurrentUsername = username;
                CurrentState = AuthState.RegisteredUser;

                Debug.Log($"[AUTH] Đăng ký thành công! Player ID: {PlayerId}, Username: {CurrentUsername}");
                SetStatus($"Đăng ký thành công! Chào mừng {CurrentUsername}.");
                OnAuthStateChanged?.Invoke(CurrentState, PlayerId, CurrentUsername);
                return true;
            }
        }
        catch (AuthenticationException aEx)
        {
            Debug.LogError($"[AUTH] Lỗi AuthenticationException khi SignUp: {aEx.ErrorCode} - {aEx.Message}");
            SetError(FormatSignUpErrorMessage(aEx));
        }
        catch (RequestFailedException rfe)
        {
            Debug.LogError($"[AUTH] Lỗi RequestFailedException khi SignUp: {rfe.ErrorCode} - {rfe.Message}");
            SetError(FormatRequestFailedErrorMessage(rfe));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AUTH] Lỗi không xác định khi SignUp: {ex}");
            SetError($"Lỗi khi tạo tài khoản: {ex.Message}");
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

            // Nếu đang có phiên khác, đăng xuất sạch trước
            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut();
                AuthenticationService.Instance.ClearSessionToken();
            }

            // Gọi API đăng nhập chính thức của Unity Authentication
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);

            if (AuthenticationService.Instance.IsSignedIn)
            {
                PlayerId = AuthenticationService.Instance.PlayerId;
                CurrentUsername = username;
                CurrentState = AuthState.RegisteredUser;

                Debug.Log($"[AUTH] Đăng nhập thành công! Player ID: {PlayerId}, Username: {CurrentUsername}");
                SetStatus($"Đăng nhập thành công! Chào mừng {CurrentUsername}.");
                OnAuthStateChanged?.Invoke(CurrentState, PlayerId, CurrentUsername);
                return true;
            }
        }
        catch (AuthenticationException aEx)
        {
            Debug.LogError($"[AUTH] Lỗi AuthenticationException khi SignIn: {aEx.ErrorCode} - {aEx.Message}");
            SetError(FormatSignInErrorMessage(aEx));
        }
        catch (RequestFailedException rfe)
        {
            Debug.LogError($"[AUTH] Lỗi RequestFailedException khi SignIn: {rfe.ErrorCode} - {rfe.Message}");
            SetError(FormatRequestFailedErrorMessage(rfe));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AUTH] Lỗi không xác định khi SignIn: {ex}");
            SetError($"Lỗi đăng nhập: {ex.Message}");
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

        if (password.Length < 8 || password.Length > 30)
        {
            errorMessage = "Mật khẩu phải dài từ 8 đến 30 ký tự!";
            return false;
        }

        bool hasUpper = false;
        bool hasLower = false;
        bool hasDigit = false;
        bool hasSpecial = false;

        foreach (char c in password)
        {
            if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else hasSpecial = true;
        }

        if (!hasUpper || !hasLower || !hasDigit || !hasSpecial)
        {
            errorMessage = "Mật khẩu phải có ít nhất 1 chữ hoa, 1 chữ thường, 1 số và 1 ký tự đặc biệt (ví dụ: @, #, $, !)";
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
            return "Tên tài khoản hoặc mật khẩu không đúng định dạng quy định của hệ thống.";
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
}
