
using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

public class NetworkVoiceManager : MonoBehaviour
{
    public static NetworkVoiceManager Instance { get; private set; }

    public bool IsInitialized { get; private set; }

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


    public async void JoinLobbyVoice()
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("VOICE | Vivox chưa khởi tạo xong.");
            return;
        }

        try
        {
            await VivoxService.Instance.JoinGroupChannelAsync(
                "werewolf-dev-test-lobby",
                ChatCapability.AudioOnly
            );

            Debug.Log("VOICE | Đã tham gia kênh thử nghiệm Lobby.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("VOICE | Không vào được kênh: " + e.Message);
        }
    }

}
