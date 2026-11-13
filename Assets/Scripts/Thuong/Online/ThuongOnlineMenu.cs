using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Thuong.Online
{
    internal sealed class ThuongOnlineMenu : MonoBehaviour
    {
        internal const string MainMenuName = "MainMenu";
        private static bool returningFromThuong;

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

            // The regular Host/Join buttons now lead through the shared lobby.
            ThuongMainFlow.PrepareLobbyNetwork(NetworkManager.Singleton);
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
