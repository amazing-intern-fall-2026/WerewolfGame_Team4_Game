using UnityEngine;

namespace Thuong.Online
{
    [DefaultExecutionOrder(-10000)]
    public sealed class ThuongOnlineSceneEntry : MonoBehaviour
    {
        private string startupError;

        private void Awake()
        {
            startupError = ThuongOnlineBootstrap.EnsureInstalled(gameObject.scene);
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(startupError)) return;

            const float width = 560f;
            Rect area = new Rect(
                Mathf.Max(12f, (Screen.width - width) * 0.5f),
                18f,
                Mathf.Min(width, Screen.width - 24f),
                118f);

            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 14f, area.y + 12f, area.width - 28f, area.height - 24f));
            GUILayout.Label("THƯƠNG ONLINE - KHÔNG THỂ KHỞI ĐỘNG");
            GUILayout.Space(6f);
            GUILayout.Label(startupError);
            GUILayout.EndArea();
        }
    }
}
