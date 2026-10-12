using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

public class NetworkVoiceManager : MonoBehaviour
{
    public static NetworkVoiceManager Instance { get; private set; }

    public bool IsInitialized { get; private set; }
    public bool IsInLobbyVoice { get; private set; }
    public string CurrentChannelName { get; private set; }

    public bool IsMicrophoneMuted { get; private set; }
    public bool IsOutputMuted { get; private set; }

    public static event Action<bool> OnMicrophoneMuteChanged;
    public static event Action<bool> OnOutputMuteChanged;

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
            channelName = "werewolf-dev-test-lobby";
        }

        if (!IsInitialized || VivoxService.Instance == null || !VivoxService.Instance.IsLoggedIn)
        {
            Debug.LogWarning($"VOICE | Vivox chưa sẵn sàng. Đang chờ để vào kênh: {channelName}");
            StartCoroutine(WaitAndJoinVoiceChannel(channelName));
            return;
        }

        if (IsInLobbyVoice && CurrentChannelName == channelName)
        {
            Debug.Log($"VOICE | Đã ở trong kênh '{channelName}'.");
            return;
        }

        if (isJoiningChannel && targetJoiningChannelName == channelName)
        {
            return;
        }

        isJoiningChannel = true;
        targetJoiningChannelName = channelName;

        try
        {
            await VivoxService.Instance.JoinGroupChannelAsync(
                channelName,
                ChatCapability.AudioOnly
            );

            CurrentChannelName = channelName;
            IsInLobbyVoice = true;
            IsMicrophoneMuted = VivoxService.Instance.IsInputDeviceMuted;
            IsOutputMuted = VivoxService.Instance.IsOutputDeviceMuted;

            Debug.Log($"VOICE | Đã tham gia thành công kênh thoại: {channelName}");
            SetMicrophoneMuted(false);
        }
        catch (Exception e)
        {
            Debug.LogError($"VOICE | Không vào được kênh '{channelName}': {e.Message}");
            IsInLobbyVoice = false;
        }
        finally
        {
            isJoiningChannel = false;
            targetJoiningChannelName = null;
        }
    }

    public void JoinLobbyVoice()
    {
        string channel = !string.IsNullOrEmpty(CurrentChannelName) ? CurrentChannelName : "werewolf-dev-test-lobby";
        JoinVoiceChannel(channel);
    }

    public async Task LeaveVoiceChannelAsync()
    {
        if (string.IsNullOrEmpty(CurrentChannelName) && !IsInLobbyVoice)
        {
            return;
        }

        try
        {
            if (VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn)
            {
                await VivoxService.Instance.LeaveAllChannelsAsync();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"VOICE | Lỗi khi rời kênh thoại: {e.Message}");
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
    // ĐIỀU KHIỂN MICROPHONE (INPUT)
    // =========================================================

    public void ToggleMicrophone()
    {
        SetMicrophoneMuted(!IsMicrophoneMuted);
    }

    public void SetMicrophoneMuted(bool muted)
    {
        if (IsInitialized && VivoxService.Instance != null)
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

        IsMicrophoneMuted = muted;
        Debug.Log($"VOICE | Microphone: {(muted ? "OFF" : "ON")}");
        OnMicrophoneMuteChanged?.Invoke(muted);
    }

    public void SetMicrophoneActive(bool active)
    {
        SetMicrophoneMuted(!active);
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
        if (IsInitialized && VivoxService.Instance != null)
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

        IsOutputMuted = muted;
        Debug.Log($"VOICE | Output (Loa): {(muted ? "OFF" : "ON")}");
        OnOutputMuteChanged?.Invoke(muted);
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
            channelName = "werewolf-dev-test-lobby";
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
            Debug.LogError($"VOICE | Vivox chưa sẵn sàng sau {timeout}s. Không thể vào kênh: {channelName}");
            yield break;
        }

        JoinVoiceChannel(channelName);
    }

    public IEnumerator WaitAndJoinLobbyVoice()
    {
        string channel = !string.IsNullOrEmpty(CurrentChannelName) ? CurrentChannelName : "werewolf-dev-test-lobby";
        yield return WaitAndJoinVoiceChannel(channel);
    }
}
