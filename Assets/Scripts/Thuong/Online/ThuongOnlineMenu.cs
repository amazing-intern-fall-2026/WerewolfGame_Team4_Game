using System.Linq;
using System.Reflection;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Thuong.Online
{
    internal sealed class ThuongOnlineMenu : MonoBehaviour
    {
        internal const string MainMenuName = "MainMenu";
        private static bool returningFromThuong;
        // The existing menu has an asynchronous connection step before IsListening is set.
        private static readonly FieldInfo Connecting = typeof(NetworkUI).GetField("isConnecting",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private NetworkUI menu;
        private NetworkManager menuNetwork;
        private Button entry;
        private bool switching;

        internal static void Install(Scene scene)
        {
            if (returningFromThuong)
            {
                returningFromThuong = false;
                var loader = SceneLoader.Instance;
                if (loader != null && loader.enabled)
                {
                    // The persistent loader subscribed to the previous menu's network manager.
                    loader.enabled = false;
                    loader.enabled = true;
                }
            }

            var roots = scene.GetRootGameObjects();
            var menu = roots.Select(root => root.GetComponent<NetworkUI>()).FirstOrDefault(ui => ui != null);
            if (menu == null || menu.GetComponent<ThuongOnlineMenu>() != null) return;
            var template = roots.Select(root => root.transform.Find("HostButton")?.GetComponent<Button>()).FirstOrDefault(button => button != null);
            if (template == null) return;
            var launch = menu.gameObject.AddComponent<ThuongOnlineMenu>();
            launch.menu = menu;
            launch.menuNetwork = NetworkManager.Singleton;
            launch.entry = Instantiate(template, template.transform.parent);
            launch.entry.name = "Thuong Online Button";
            launch.entry.onClick = new Button.ButtonClickedEvent();
            launch.entry.onClick.AddListener(launch.OpenThuong);
            var rect = (RectTransform)launch.entry.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .16f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(380, 84);
            var label = launch.entry.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "THƯƠNG ONLINE";
            launch.RefreshEntry();
        }

        private bool CanOpen => !switching && menuNetwork != null && NetworkManager.Singleton == menuNetwork &&
            !menuNetwork.IsListening && !menuNetwork.ShutdownInProgress && Connecting?.GetValue(menu) is false;

        private void Update() => RefreshEntry();

        private void RefreshEntry()
        {
            if (entry == null) return;
            entry.gameObject.SetActive(AuthManager.Instance.IsSignedIn);
            entry.interactable = CanOpen;
        }

        private async void OpenThuong()
        {
            if (!AuthManager.Instance.IsSignedIn || !CanOpen ||
                !Application.CanStreamedLevelBeLoaded(ThuongOnlineBootstrap.SceneName)) return;
            switching = true;
            // Netcode moved the idle menu manager to DontDestroyOnLoad.
            Destroy(menuNetwork.gameObject);
            await Awaitable.NextFrameAsync();
            SceneManager.LoadScene(ThuongOnlineBootstrap.SceneName);
        }

        internal static async void ReturnToMenu(ThuongOnlineSession session)
        {
            returningFromThuong = true;
            Destroy(session.gameObject);
            await Awaitable.NextFrameAsync();
            SceneManager.LoadScene(MainMenuName);
        }
    }
}
