using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

public class NetworkVoiceManager : MonoBehaviour
{
    private static NetworkVoiceManager _instance;
    public static NetworkVoiceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<NetworkVoiceManager>();
                if (_instance == null)
                {
                    var go = new GameObject("NetworkVoiceManager");
                    _instance = go.AddComponent<NetworkVoiceManager>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    public bool IsInitialized { get; private set; }
    public bool IsInLobbyVoice { get; private set; }
    public string CurrentChannelName { get; private set; }

    private bool _isMicrophoneMuted = true;
    public bool IsMicrophoneMuted
    {
        get
        {
            if (IsInitialized && VivoxService.Instance != null && VivoxService.Instance.InitializationState == VivoxInitializationState.Initialized)
            {
                return VivoxService.Instance.IsInputDeviceMuted;
            }
            return _isMicrophoneMuted;
        }
        private set => _isMicrophoneMuted = value;
    }

    private bool _isOutputMuted = false;
    public bool IsOutputMuted
    {
        get
        {
            if (IsInitialized && VivoxService.Instance != null && VivoxService.Instance.InitializationState == VivoxInitializationState.Initialized)
            {
                return VivoxService.Instance.IsOutputDeviceMuted;
            }
            return _isOutputMuted;
        }
        private set => _isOutputMuted = value;
    }

    public static event Action<bool> OnMicrophoneMuteChanged;
    public static event Action<bool> OnOutputMuteChanged;

    private readonly SemaphoreSlim _voiceLock = new SemaphoreSlim(1, 1);
    private Task _ongoingLeaveTask;
    private bool _isJoiningChannel;
    private string _targetJoiningChannelName;
    private bool _isApplicationQuitting;

    private async void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Mặc định an toàn: Micro luôn TẮT (Muted)
        _isMicrophoneMuted = true;
        _isOutputMuted = false;

        await EnsureInitializedAndLoggedInAsync();
    }

    private void OnApplicationQuit()
    {
        _isApplicationQuitting = true;
        Debug.Log("[VOICE] Ứng dụng đang đóng (OnApplicationQuit).");
        CurrentChannelName = null;
        IsInLobbyVoice = false;
    }

    // =========================================================
    // KHỞI TẠO VÀ ĐĂNG NHẬP VIVOX
    // =========================================================

    public async Task<bool> EnsureInitializedAndLoggedInAsync()
    {
        if (_isApplicationQuitting)
        {
            return false;
        }

        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                Debug.Log("[VOICE] Đang khởi tạo UnityServices...");
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("[VOICE] Đang đăng nhập AuthenticationService (Anonymous)...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            if (VivoxService.Instance == null)
            {
                Debug.LogError("[VOICE] VivoxService.Instance là null!");
                return false;
            }

            if (VivoxService.Instance.InitializationState != VivoxInitializationState.Initialized)
            {
                Debug.Log("[VOICE] Đang khởi tạo Vivox SDK (InitializeAsync)...");
                await VivoxService.Instance.InitializeAsync();
            }

            if (!VivoxService.Instance.IsLoggedIn)
            {
                Debug.Log("[VOICE] Đang đăng nhập Vivox (LoginAsync)...");
                await VivoxService.Instance.LoginAsync();
            }

            IsInitialized = (VivoxService.Instance.InitializationState == VivoxInitializationState.Initialized && VivoxService.Instance.IsLoggedIn);
            Debug.Log($"[VOICE] Vivox login thành công. IsInitialized={IsInitialized}");

            if (IsInitialized)
            {
                // Áp dụng trạng thái mute an toàn vào Vivox ngay khi login xong
                if (_isMicrophoneMuted)
                {
                    VivoxService.Instance.MuteInputDevice();
                }
                else
                {
                    VivoxService.Instance.UnmuteInputDevice();
                }

                if (_isOutputMuted)
                {
                    VivoxService.Instance.MuteOutputDevice();
                }
                else
                {
                    VivoxService.Instance.UnmuteOutputDevice();
                }
                Debug.Log($"[VOICE] Đã thiết lập trạng thái mute ban đầu cho Vivox: MicMuted={_isMicrophoneMuted}, OutputMuted={_isOutputMuted}");
            }

            return IsInitialized;
        }
        catch (Exception e)
        {
            Debug.LogError($"[VOICE] Khởi tạo Vivox thất bại: {e}");
            return false;
        }
    }

    // =========================================================
    // THAM GIA KÊNH THOẠI (JOIN CHANNEL)
    // =========================================================

    public async void JoinVoiceChannel(string channelName)
    {
        await JoinVoiceChannelAsync(channelName);
    }

    public async Task JoinVoiceChannelAsync(string channelName)
    {
        if (_isApplicationQuitting)
        {
            return;
        }

        if (string.IsNullOrEmpty(channelName))
        {
            Debug.LogError("VOICE | JoinVoiceChannel: channelName rỗng. Không thể join.");
            return;
        }

        // Chờ tiến trình rời kênh trước đó (nếu đang chạy dở) hoàn tất
        if (_ongoingLeaveTask != null && !_ongoingLeaveTask.IsCompleted)
        {
            Debug.Log("[VOICE] Đang chờ tiến trình rời kênh cũ hoàn tất trước khi vào kênh mới...");
            await _ongoingLeaveTask;
        }

        // Đảm bảo Vivox đã được khởi tạo và đăng nhập
        if (!IsInitialized || VivoxService.Instance == null || VivoxService.Instance.InitializationState != VivoxInitializationState.Initialized || !VivoxService.Instance.IsLoggedIn)
        {
            Debug.LogWarning($"VOICE | Vivox chưa sẵn sàng. Đang tiến hành khởi tạo/đăng nhập để vào kênh: {channelName}");
            bool ready = await EnsureInitializedAndLoggedInAsync();
            if (!ready)
            {
                Debug.LogWarning($"VOICE | Không thể vào kênh '{channelName}' do Vivox chưa sẵn sàng.");
                return;
            }
        }

        if (IsInLobbyVoice && CurrentChannelName == channelName)
        {
            Debug.Log($"VOICE | Đã ở trong kênh '{channelName}'.");
            return;
        }

        if (_isJoiningChannel && _targetJoiningChannelName == channelName)
        {
            return;
        }

        // Nếu đang ở kênh khác, rời kênh cũ trước theo đúng quy định của Vivox
        if (IsInLobbyVoice && !string.IsNullOrEmpty(CurrentChannelName) && CurrentChannelName != channelName)
        {
            Debug.Log($"VOICE | Đang rời kênh cũ '{CurrentChannelName}' trước khi vào kênh mới '{channelName}'...");
            await LeaveVoiceChannelAsync();
        }

        _isJoiningChannel = true;
        _targetJoiningChannelName = channelName;

        try
        {
            Debug.Log($"VOICE | Bắt đầu JoinGroupChannelAsync vào kênh: {channelName}...");
            await VivoxService.Instance.JoinGroupChannelAsync(
                channelName,
                ChatCapability.AudioOnly
            );

            CurrentChannelName = channelName;
            IsInLobbyVoice = true;

            // QUY TẮC BẢO MẬT VOICE: Tuyệt đối KHÔNG tự bật micro khi vào kênh thoại!
            // Áp dụng trạng thái người chơi đã chọn (_isMicrophoneMuted mặc định = true)
            SetMicrophoneMuted(_isMicrophoneMuted);
            SetOutputMuted(_isOutputMuted);

            Debug.Log($"VOICE | Đã tham gia thành công kênh thoại: {channelName}. MicMuted={IsMicrophoneMuted}, OutputMuted={IsOutputMuted}");
        }
        catch (Exception e)
        {
            Debug.LogError($"VOICE | Không vào được kênh '{channelName}': {e.Message}");
            IsInLobbyVoice = false;
        }
        finally
        {
            _isJoiningChannel = false;
            _targetJoiningChannelName = null;
        }
    }

    public void JoinLobbyVoice()
    {
        string channel = CurrentChannelName;
        if (!string.IsNullOrEmpty(channel))
        {
            JoinVoiceChannel(channel);
        }
        else
        {
            Debug.LogWarning("VOICE | JoinLobbyVoice được gọi nhưng chưa có channel ID. Hãy dùng JoinVoiceChannel(channelName) trực tiếp.");
        }
    }

    // =========================================================
    // RỜI KÊNH THOẠI (LEAVE CHANNEL)
    // =========================================================

    public Task LeaveVoiceChannelAsync()
    {
        // Chống race condition: nếu đã có một tác vụ leave đang thực thi, dùng chung tác vụ đó
        if (_ongoingLeaveTask != null && !_ongoingLeaveTask.IsCompleted)
        {
            Debug.Log("[VOICE] Tiến trình rời kênh đang thực thi, chờ tiến trình này hoàn tất...");
            return _ongoingLeaveTask;
        }

        _ongoingLeaveTask = LeaveVoiceChannelInternalAsync();
        return _ongoingLeaveTask;
    }

    private async Task LeaveVoiceChannelInternalAsync()
    {
        if (_isApplicationQuitting)
        {
            Debug.Log("[VOICE] Ứng dụng đang đóng, bỏ qua gọi LeaveAllChannelsAsync.");
            CurrentChannelName = null;
            IsInLobbyVoice = false;
            return;
        }

        // Kiểm tra trạng thái Vivox SDK thực tế
        if (VivoxService.Instance == null || VivoxService.Instance.InitializationState != VivoxInitializationState.Initialized)
        {
            Debug.Log("[VOICE] Vivox SDK chưa khởi tạo hoặc đã uninitialized, bỏ qua gọi API LeaveAllChannelsAsync.");
            CurrentChannelName = null;
            IsInLobbyVoice = false;
            return;
        }

        if (!VivoxService.Instance.IsLoggedIn)
        {
            Debug.Log("[VOICE] Vivox chưa đăng nhập, bỏ qua LeaveAllChannelsAsync.");
            CurrentChannelName = null;
            IsInLobbyVoice = false;
            return;
        }

        if (string.IsNullOrEmpty(CurrentChannelName) && !IsInLobbyVoice && VivoxService.Instance.ActiveChannels.Count == 0)
        {
            Debug.Log("[VOICE] Hiện không ở trong kênh thoại nào, không cần gọi LeaveAllChannelsAsync.");
            return;
        }

        try
        {
            Debug.Log($"[VOICE] [1/3] Đang rời kênh thoại Vivox ({CurrentChannelName ?? "Tất cả kênh"})...");
            await VivoxService.Instance.LeaveAllChannelsAsync();
            Debug.Log("[VOICE] [1/3] Đã rời kênh thoại Vivox thành công.");
        }
        catch (OperationCanceledException oce)
        {
            Debug.Log($"[VOICE] Thao tác rời kênh bị hủy (SDK đang uninitialize hoặc ứng dụng đang tắt): {oce.Message}");
        }
        catch (Exception e)
        {
            if (e.Message.Contains("uninitialized") || e.Message.Contains("Uninitialized"))
            {
                Debug.Log($"[VOICE] Vivox SDK uninitialized trong quá trình rời kênh: {e.Message}");
            }
            else
            {
                Debug.LogWarning($"VOICE | Lỗi khi rời kênh thoại: {e.Message}");
            }
        }
        finally
        {
            CurrentChannelName = null;
            IsInLobbyVoice = false;
        }
    }

    public void LeaveVoiceChannel()
    {
        _ = LeaveVoiceChannelAsync();
    }

    // =========================================================
    // ĐĂNG XUẤT VÀ UNINITIALIZE VIVOX (LOGOUT & SHUTDOWN)
    // =========================================================

    /// <summary>
    /// Đăng xuất Vivox sau khi đảm bảo thao tác rời kênh thoại đã hoàn tất.
    /// </summary>
    public async Task LogoutVivoxAsync()
    {
        if (_isApplicationQuitting)
        {
            Debug.Log("[VOICE] Ứng dụng đang đóng, bỏ qua LogoutAsync.");
            return;
        }

        // Đảm bảo kênh thoại đã được rời hoàn tất trước khi Logout
        await LeaveVoiceChannelAsync();

        if (VivoxService.Instance == null || VivoxService.Instance.InitializationState != VivoxInitializationState.Initialized)
        {
            Debug.Log("[VOICE] Vivox SDK chưa khởi tạo hoặc đã uninitialized, bỏ qua LogoutAsync.");
            return;
        }

        if (!VivoxService.Instance.IsLoggedIn)
        {
            Debug.Log("[VOICE] Vivox chưa đăng nhập hoặc đã đăng xuất trước đó.");
            return;
        }

        try
        {
            Debug.Log("[VOICE] [2/3] Đang đăng xuất Vivox (LogoutAsync)...");
            await VivoxService.Instance.LogoutAsync();
            Debug.Log("[VOICE] [2/3] Đã đăng xuất Vivox thành công.");
        }
        catch (OperationCanceledException oce)
        {
            Debug.Log($"[VOICE] Đăng xuất Vivox bị hủy do SDK teardown: {oce.Message}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VOICE] Lỗi khi đăng xuất Vivox: {e.Message}");
        }
    }

    /// <summary>
    /// Uninitialize Vivox SDK an toàn, đưa SDK về trạng thái Uninitialized.
    /// </summary>
    public void UninitializeVivox()
    {
        if (VivoxService.Instance == null)
        {
            return;
        }

        if (VivoxService.Instance.InitializationState != VivoxInitializationState.Initialized)
        {
            Debug.Log($"[VOICE] Vivox SDK đang ở trạng thái {VivoxService.Instance.InitializationState}, bỏ qua Uninitialize.");
            IsInitialized = false;
            return;
        }

        try
        {
            Debug.Log("[VOICE] [3/3] Đang uninitialize Vivox SDK...");
            VivoxService.Instance.Uninitialize();
            IsInitialized = false;
            CurrentChannelName = null;
            IsInLobbyVoice = false;
            Debug.Log("[VOICE] [3/3] Đã uninitialize Vivox SDK hoàn tất.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VOICE] Lỗi khi uninitialize Vivox: {e.Message}");
        }
    }

    /// <summary>
    /// Quy trình tắt hoàn chỉnh theo thứ tự chuẩn: Leave Channel → Logout → Uninitialize.
    /// </summary>
    public async Task ShutdownVivoxAsync()
    {
        await _voiceLock.WaitAsync();
        try
        {
            Debug.Log("[VOICE] Bắt đầu quy trình tắt Vivox theo thứ tự: Leave Channel → Logout → Uninitialize");

            // [1/3] Rời kênh
            await LeaveVoiceChannelAsync();

            // [2/3] Đăng xuất
            await LogoutVivoxAsync();

            // [3/3] Uninitialize
            UninitializeVivox();

            Debug.Log("[VOICE] Hoàn tất quy trình tắt Vivox an toàn.");
        }
        finally
        {
            _voiceLock.Release();
        }
    }

    // =========================================================
    // ĐIỀU KHIỂN MICROPHONE (INPUT)
    // =========================================================

    public void ToggleMicrophone()
    {
        SetMicrophoneMuted(!IsMicrophoneMuted);
    }

    public void SetMicrophoneMuted(bool muted)
    {
        _isMicrophoneMuted = muted;

        if (IsInitialized && VivoxService.Instance != null && VivoxService.Instance.InitializationState == VivoxInitializationState.Initialized)
        {
            if (muted)
            {
                VivoxService.Instance.MuteInputDevice();
            }
            else
            {
                VivoxService.Instance.UnmuteInputDevice();
            }
        }

        bool actualState = IsMicrophoneMuted;
        Debug.Log($"VOICE | Microphone: {(actualState ? "OFF (MUTED)" : "ON (UNMUTED)")} (Vivox={actualState})");
        OnMicrophoneMuteChanged?.Invoke(actualState);
    }

    public void SetMicrophoneActive(bool active)
    {
        SetMicrophoneMuted(!active);
    }

    /// <summary>
    /// Đặt lại micro về trạng thái TẮT an toàn (ví dụ khi vào Game Scene, đổi Scene, bắt đầu ván mới).
    /// </summary>
    public void ResetToSafeDefaultMute()
    {
        SetMicrophoneMuted(true);
    }

    // =========================================================
    // ĐIỀU KHIỂN LOA (OUTPUT)
    // =========================================================

    public void ToggleOutputAudio()
    {
        SetOutputMuted(!IsOutputMuted);
    }

    public void SetOutputMuted(bool muted)
    {
        _isOutputMuted = muted;

        if (IsInitialized && VivoxService.Instance != null && VivoxService.Instance.InitializationState == VivoxInitializationState.Initialized)
        {
            if (muted)
            {
                VivoxService.Instance.MuteOutputDevice();
            }
            else
            {
                VivoxService.Instance.UnmuteOutputDevice();
            }
        }

        bool actualState = IsOutputMuted;
        Debug.Log($"VOICE | Output (Loa): {(actualState ? "OFF (MUTED)" : "ON (UNMUTED)")} (Vivox={actualState})");
        OnOutputMuteChanged?.Invoke(actualState);
    }

    public void SetOutputActive(bool active)
    {
        SetOutputMuted(!active);
    }

    // =========================================================
    // COROUTINE CHỜ KHỞI TẠO VÀ THAM GIA KÊNH
    // =========================================================

    public IEnumerator WaitAndJoinVoiceChannel(string channelName)
    {
        if (string.IsNullOrEmpty(channelName))
        {
            Debug.LogError("VOICE | WaitAndJoinVoiceChannel: channelName rỗng. Không thể join.");
            yield break;
        }

        if (_isApplicationQuitting)
        {
            yield break;
        }

        // Nếu chưa khởi tạo/đăng nhập, kích hoạt quy trình EnsureInitializedAndLoggedInAsync
        if (!IsInitialized || VivoxService.Instance == null || VivoxService.Instance.InitializationState != VivoxInitializationState.Initialized || !VivoxService.Instance.IsLoggedIn)
        {
            Task<bool> initTask = EnsureInitializedAndLoggedInAsync();
            while (!initTask.IsCompleted)
            {
                yield return null;
            }
        }

        float timeout = 15f;
        float elapsed = 0f;

        while ((!IsInitialized || VivoxService.Instance == null || VivoxService.Instance.InitializationState != VivoxInitializationState.Initialized || !VivoxService.Instance.IsLoggedIn) && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!IsInitialized || VivoxService.Instance == null || VivoxService.Instance.InitializationState != VivoxInitializationState.Initialized || !VivoxService.Instance.IsLoggedIn)
        {
            Debug.LogError($"VOICE | Vivox chưa sẵn sàng sau {timeout}s. Không thể vào kênh: {channelName}");
            yield break;
        }

        JoinVoiceChannel(channelName);
    }

    public IEnumerator WaitAndJoinLobbyVoice()
    {
        // Chỉ dùng nếu đã có CurrentChannelName
        string channel = CurrentChannelName;
        if (!string.IsNullOrEmpty(channel))
        {
            yield return WaitAndJoinVoiceChannel(channel);
        }
        else
        {
            Debug.LogWarning("VOICE | WaitAndJoinLobbyVoice được gọi nhưng chưa có channel ID.");
        }
    }
}
