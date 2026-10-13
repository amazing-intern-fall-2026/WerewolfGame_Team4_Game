using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Thuong.Online
{
    public static class ThuongOnlineBootstrap
    {
        public const string SceneName = "OnlineSceneGamePlay";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallActiveSceneFallback()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.name == SceneName)
                EnsureInstalled(activeScene);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneName)
                return;

            EnsureInstalled(scene);
        }

        public static string EnsureInstalled(Scene scene)
        {
            if (Object.FindAnyObjectByType<ThuongOnlineSession>() != null)
                return null;

            if (NetworkManager.Singleton != null)
            {
                const string message =
                    "Không thể khởi động Thuong Online vì đang có NetworkManager khác hoạt động. " +
                    "Hãy dừng phiên mạng cũ rồi mở trực tiếp scene OnlineSceneGamePlay.";
                Debug.LogError("[Thuong Online] " + message);
                return message;
            }

            var root = new GameObject("Thuong Online Runtime");
            if (scene.IsValid() && scene.isLoaded)
                SceneManager.MoveGameObjectToScene(root, scene);

            var transport = root.AddComponent<UnityTransport>();
            var network = root.AddComponent<NetworkManager>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ConnectionApproval = true,
                EnableSceneManagement = false,
                TickRate = 30,
                ClientConnectionBufferTimeout = 15
            };
            root.AddComponent<ThuongOnlineSession>();
            Debug.Log("[Thuong Online] Đã khởi động giao diện online cho OnlineSceneGamePlay.");
            return null;
        }
    }
}
