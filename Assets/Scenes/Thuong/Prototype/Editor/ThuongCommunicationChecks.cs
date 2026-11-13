using System;
using Thuong.Minigames;
using Thuong.Online;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class ThuongCommunicationChecks
{
    private static int checks;
    private static void Require(bool value, string description)
    {
        if (!value) throw new InvalidOperationException(description);
        checks++;
    }

    [MenuItem("Tools/Thuong/Communications/Check Rules Voice And Assets")]
    public static void Run()
    {
        checks = 0;
        foreach (GamePhase phase in Enum.GetValues(typeof(GamePhase)))
        foreach (bool connected in new[] { false, true })
        foreach (bool started in new[] { false, true })
        foreach (bool finished in new[] { false, true })
        foreach (bool alive in new[] { false, true })
        {
            bool publicPhase = phase == GamePhase.DayStart || phase == GamePhase.Event || phase == GamePhase.Task ||
                phase == GamePhase.Discussion || phase == GamePhase.Voting;
            var expected = !connected ? ThuongCommunicationAudience.None : !started || finished ? ThuongCommunicationAudience.Room :
                !alive ? ThuongCommunicationAudience.Dead : publicPhase ? ThuongCommunicationAudience.Living : ThuongCommunicationAudience.None;
            Require(ThuongCommunicationRules.Resolve(connected, started, finished, alive, phase) == expected,
                "Communication rights: " + phase + "/" + connected + "/" + started + "/" + finished + "/" + alive);
        }
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
        Require(pipeline != null && GraphicsSettings.defaultRenderPipeline == pipeline, "Default render pipeline resolves");
        for (int i = 0; i < QualitySettings.names.Length; i++)
            Require(QualitySettings.GetRenderPipelineAssetAt(i) == pipeline, "Quality pipeline resolves: " + i);
        var renderers = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
        Require(renderers.arraySize > 0 && renderers.GetArrayElementAtIndex(0).objectReferenceValue != null, "2D renderer resolves");
        var catalog = Resources.Load<ThuongMinigameCatalog>(ThuongMinigameCatalog.ResourcePath);
        Require(catalog != null && catalog.Validate(out _), "Online minigame catalog remains valid");
        Require(catalog.entries.Find(entry => entry.kind == ThuongMinigameKind.Potion).subject != null, "Potion sprite resolves");
        Require(Array.Exists(catalog.ingredients, ingredient => ingredient.type == IngredientType.Neutral && ingredient.icon != null), "Neutral ingredient icon resolves");

        var root = new GameObject("Communication voice gate verification");
        try
        {
            var voice = root.AddComponent<NetworkVoiceManager>();
            voice.SetGameplayPermissions(root, false, false);
            voice.SetMicrophoneMuted(false); voice.SetOutputMuted(false);
            Require(voice.IsMicrophoneMuted && voice.IsOutputMuted, "Forbidden state cannot unmute mic or output");
            voice.SetGameplayPermissions(root, true, true);
            Require(voice.IsMicrophoneMuted && !voice.IsOutputMuted, "Allowed state preserves manual mic consent and speaker preference");
            voice.SetMicrophoneMuted(false);
            Require(!voice.IsMicrophoneMuted, "Manual microphone consent works when permitted");
            voice.SetGameplayPermissions(root, false, false);
            voice.ReleaseGameplayPermissions(root);
            Require(voice.IsMicrophoneMuted && !voice.IsOutputMuted, "Leaving restores speaker preference and keeps mic muted");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Debug.Log("THUONG_COMMUNICATION_CHECKS_PASSED: " + checks);
    }
}
