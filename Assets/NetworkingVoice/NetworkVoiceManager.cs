using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

public class NetworkVoiceManager : MonoBehaviour
{
    public static NetworkVoiceManager Instance { get; private set; }

    public static event Action<bool> OnMicrophoneMuteChanged;
    public static event Action<bool> OnOutputMuteChanged;

    public bool IsInitialized { get; private set; }
    public bool IsMicrophoneMuted { get; private set; }
    public bool IsMicrophoneOn => !IsMicrophoneMuted;
    public bool IsOutputMuted { get; private set; }
    public bool IsInLobbyVoice { get; private set; }
    public string CurrentChannelName { get; private set; }

    private bool isJoiningChannel;
    private string targetJoiningChannelName;

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            await VivoxService.Instance.InitializeAsync();
            await VivoxService.Instance.LoginAsync();

            IsInitialized = true;

            IsMicrophoneMuted = VivoxService.Instance.IsInputDeviceMuted;
            IsOutputMuted = VivoxService.Instance.IsOutputDeviceMuted;

            Debug.Log("VOICE | Vivox login successful.");
        }
        catch (Exception e)
        {
            Debug.LogError($"VOICE | Initialization failed: {e}");
        }
    }

    public async void JoinVoiceChannel(string channelName)
    {
        if (string.IsNullOrEmpty(channelName))
        {
            Debug.LogWarning("VOICE | Channel name rỗng. Bỏ qua yêu cầu vào voice.");
            return;
        }

        if (!IsInitialized || VivoxService.Instance == null || !VivoxService.Instance.IsLoggedIn)
        {
            Debug.LogWarning($"VOICE | Vivox chưa sẵn sàng. Đang chờ để vào kênh: {channelName}");
            StartCoroutine(WaitAndJoinVoiceChannel(channelName));
            return;
        }

        if (IsInLobbyVoice && CurrentChannelName == channelName)
        {
            Debug.Log($"VOICE | Đã ở trong kênh '{channelName}'. Bỏ qua yêu cầu trùng lặp.");
            return;
        }

        if (isJoiningChannel && targetJoiningChannelName == channelName)
        {
            Debug.Log($"VOICE | Đang trong quá trình vào kênh '{channelName}'.");
            return;
        }

        isJoiningChannel = true;
        targetJoiningChannelName = channelName;

        try
        {
            // Đảm bảo không còn bất kỳ kênh thoại nào khác đang kết nối (cách ly hoàn toàn các phòng)
            if (VivoxService.Instance != null && VivoxService.Instance.ActiveChannels != null)
            {
                var staleChannels = new System.Collections.Generic.List<string>();
                foreach (var activeKey in VivoxService.Instance.ActiveChannels.Keys)
                {
                    if (activeKey != channelName)
                    {
                        staleChannels.Add(activeKey);
                    }
                }

                foreach (var staleCh in staleChannels)
                {
                    Debug.Log($"VOICE | [Cách ly kênh] Rời kênh cũ '{staleCh}' để tránh nghe nhầm phòng khác.");
                    try
                    {
                        await VivoxService.Instance.LeaveChannelAsync(staleCh);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"VOICE | Lỗi khi rời kênh cũ '{staleCh}': {ex.Message}");
                    }
                }
            }

            Debug.Log($"VOICE | [Phòng riêng] Đang tham gia kênh thoại: {channelName}");

            await VivoxService.Instance.JoinGroupChannelAsync(
                channelName,
                ChatCapability.AudioOnly
            );

            CurrentChannelName = channelName;
            IsInLobbyVoice = true;
            IsMicrophoneMuted = VivoxService.Instance.IsInputDeviceMuted;
            IsOutputMuted = VivoxService.Instance.IsOutputDeviceMuted;

            Debug.Log($"VOICE | Đã tham gia thành công kênh thoại riêng: {channelName}");

            SetMicrophoneMuted(false);
        }
        catch (Exception e)
        {
            Debug.LogError($"VOICE | Không vào được kênh '{channelName}': {e.Message}");
            IsInLobbyVoice = false;
            StartCoroutine(RetryJoinVoiceChannel(channelName, 3f));
        }
        finally
        {
            isJoiningChannel = false;
            targetJoiningChannelName = null;
        }
    }

    private System.Collections.IEnumerator RetryJoinVoiceChannel(string channelName, float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);

        if (!IsInLobbyVoice && !isJoiningChannel && !string.IsNullOrEmpty(channelName))
        {
            Debug.Log($"VOICE | Thử lại tham gia kênh thoại: {channelName}...");
            JoinVoiceChannel(channelName);
        }
    }

    public async System.Threading.Tasks.Task LeaveVoiceChannelAsync()
    {
        if (string.IsNullOrEmpty(CurrentChannelName) && !IsInLobbyVoice)
        {
            return;
        }

        string leavingChannel = CurrentChannelName;
        Debug.Log($"VOICE | Đang rời toàn bộ kênh thoại: {leavingChannel}");

        try
        {
            if (VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn)
            {
                await VivoxService.Instance.LeaveAllChannelsAsync();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"VOICE | Lỗi khi rời kênh thoại '{leavingChannel}': {e.Message}");
        }
        finally
        {
            CurrentChannelName = null;
            IsInLobbyVoice = false;
            Debug.Log($"VOICE | Đã rời toàn bộ kênh thoại: {leavingChannel}");
        }
    }

    public void LeaveVoiceChannel()
    {
        _ = LeaveVoiceChannelAsync();
    }

    public void JoinLobbyVoice()
    {
        string channel = !string.IsNullOrEmpty(CurrentChannelName) ? CurrentChannelName : "werewolf-dev-test-lobby";
        JoinVoiceChannel(channel);
    }

    public void ToggleMicrophone()
    {
        if (!IsInitialized || !IsInLobbyVoice)
        {
            Debug.LogWarning("VOICE | Chưa sẵn sàng hoặc chưa vào Lobby voice.");
            return;
        }

        SetMicrophoneMuted(!IsMicrophoneMuted);
    }

    public void SetMicrophoneMuted(bool muted)
    {
        if (!IsInitialized)
            return;

        if (muted)
        {
            VivoxService.Instance.MuteInputDevice();
        }
        else
        {
            VivoxService.Instance.UnmuteInputDevice();
        }

        IsMicrophoneMuted = muted;

        Debug.Log($"VOICE | Microphone: {(muted ? "OFF" : "ON")}");

        OnMicrophoneMuteChanged?.Invoke(muted);
    }

    public void SetMicrophoneActive(bool active)
    {
        SetMicrophoneMuted(!active);
    }

    public void ToggleOutputAudio()
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("VOICE | Vivox chưa sẵn sàng.");
            return;
        }

        SetOutputMuted(!IsOutputMuted);
    }

    public void SetOutputMuted(bool muted)
    {
        if (!IsInitialized)
            return;

        if (muted)
        {
            VivoxService.Instance.MuteOutputDevice();
        }
        else
        {
            VivoxService.Instance.UnmuteOutputDevice();
        }

        IsOutputMuted = muted;

        Debug.Log($"VOICE | Output (Loa): {(muted ? "OFF" : "ON")}");

        OnOutputMuteChanged?.Invoke(muted);
    }

    public void SetOutputActive(bool active)
    {
        SetOutputMuted(!active);
    }
    public System.Collections.IEnumerator WaitAndJoinVoiceChannel(string channelName)
    {
        if (string.IsNullOrEmpty(channelName))
        {
            Debug.LogWarning("VOICE | Channel name rỗng. Bỏ qua chờ vào voice.");
            yield break;
        }

        float timeout = 15f;
        float elapsed = 0f;

        while ((!IsInitialized || VivoxService.Instance == null || !VivoxService.Instance.IsLoggedIn) && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!IsInitialized || VivoxService.Instance == null || !VivoxService.Instance.IsLoggedIn)
        {
            Debug.LogError($"VOICE | Vivox chưa sẵn sàng hoặc đăng nhập thất bại sau {timeout}s. Không thể vào kênh: {channelName}");
            yield break;
        }

        JoinVoiceChannel(channelName);
    }

    public System.Collections.IEnumerator WaitAndJoinLobbyVoice()
    {
        string channel = !string.IsNullOrEmpty(CurrentChannelName) ? CurrentChannelName : "werewolf-dev-test-lobby";
        yield return WaitAndJoinVoiceChannel(channel);
    }
}