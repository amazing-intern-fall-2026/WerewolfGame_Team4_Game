using ImpossibleRobert.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioTool
{
    internal sealed class AudioToolsWelcomeWindow : EditorWindow
    {
        private const string ShownPreference = "AudioTools.WelcomeShown.v2";
        private const string DocumentationUrl = "https://www.wetzold.com/tools/audiotools/docs/";

        [InitializeOnLoadMethod]
        private static void ShowOnFirstStart()
        {
            if (EditorPrefs.GetBool(ShownPreference, false)) return;
            EditorPrefs.SetBool(ShownPreference, true);
            EditorApplication.delayCall += ShowWindow;
        }

        [MenuItem(AudioToolsMenuPaths.Welcome, false, AudioToolsMenuPaths.WelcomePriority)]
        internal static void ShowWindow()
        {
            CommonWelcomeWindow.ShowUtility<AudioToolsWelcomeWindow>("Welcome to Audio Tools");
        }

        private void OnEnable()
        {
            CommonWelcomeWindow.ApplyDefaultConstraints(this, "Welcome to Audio Tools");
        }

        public void CreateGUI()
        {
            CommonWelcomeContent content = new CommonWelcomeContent
            {
                Logo = CommonUIStyles.LoadTexture("AudioTools"),
                ProductName = "AUDIO TOOLS",
                Headline = "Polished audio, right inside Unity",
                Description =
                    "Trim silence, shape fades, normalize peaks, and export production-ready WAV files without switching tools.",
                SectionTitle = "A focused edit-to-export workflow",
                SectionDescription =
                    "Work visually and non-destructively, then save exactly the result you intend.",
                Steps = new[]
                {
                    new CommonWelcomeStep(
                        "Choose audio",
                        "Open Audio Editor, select an AudioClip, browse for a file, or use Assets/Edit Audio... in the Project window."),
                    new CommonWelcomeStep(
                        "Shape the edit",
                        "Drag across the waveform, refine the handles, and remove quiet edges with Select Audible Content."),
                    new CommonWelcomeStep(
                        "Preview and export",
                        "Enable only the enhancements you need, listen to the result, then save a copy or deliberately replace a WAV.")
                },
                Accent = CommonWelcomeAccent.Cyan
            };

            Button open = CommonWelcomeWindow.CreateAction(
                "Open Audio Editor",
                "Open Audio Editor and choose an audio source.",
                () =>
                {
                    AudioEditorUI.ShowWindow();
                    Close();
                },
                true);
            Button documentation = CommonWelcomeWindow.CreateAction(
                "Read Documentation",
                "Open the Audio Tools documentation.",
                () => Application.OpenURL(DocumentationUrl));
            CommonWelcomeWindow.Build(rootVisualElement, content, open, documentation);
        }
    }
}
