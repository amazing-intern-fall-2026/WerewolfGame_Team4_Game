using System.Collections.Generic;
using Thuong.Online;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Presentation only. VoteButton sends one request through the session; the host
// returns the voter's own choice in its existing private state message.
[DefaultExecutionOrder(1000)]
public class ThuongOnlineVoteNetworkAdapter : MonoBehaviour
{
    public static ThuongOnlineVoteNetworkAdapter Instance { get; private set; }
    public static readonly Color VoteConfirmedGreen = new Color32(53, 199, 89, 255);
    private readonly Dictionary<Image, Color> imageColors = new();
    private readonly Dictionary<Button, ColorBlock> buttonColors = new();
    private bool votingLocked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("[ThuongOnlineVoteNetworkAdapter]").AddComponent<ThuongOnlineVoteNetworkAdapter>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ResetCaches();
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RestoreColors();
        if (Instance == this) Instance = null;
    }

    public void ResetCaches()
    {
        RestoreColors();
        imageColors.Clear(); buttonColors.Clear(); votingLocked = false;
    }

    public void LockVotingAndClearHighlight()
    {
        votingLocked = true;
        RestoreColors();
    }

    private void LateUpdate()
    {
        var session = ThuongOnlineSession.Active;
        if (session == null || !session.MatchStarted || session.VisibleState != GameState.Voting)
        { votingLocked = false; RestoreColors(); return; }
        if (votingLocked) return;

        int selected = session.LocalConfirmedVoteTarget;
        foreach (var vote in FindObjectsByType<VoteButton>(FindObjectsSortMode.None))
        {
            var button = vote.GetComponent<Button>();
            if (button == null) continue;
            var graphic = button.targetGraphic as Image ?? vote.GetComponent<Image>();
            if (graphic == null) continue;
            if (!imageColors.ContainsKey(graphic)) imageColors[graphic] = graphic.color;
            if (!buttonColors.ContainsKey(button)) buttonColors[button] = button.colors;

            bool confirmed = selected >= 0 && vote.targetID == selected;
            graphic.color = confirmed ? VoteConfirmedGreen : imageColors[graphic];
            ColorBlock colors = buttonColors[button];
            if (confirmed)
                colors.normalColor = colors.highlightedColor = colors.pressedColor =
                    colors.selectedColor = colors.disabledColor = VoteConfirmedGreen;
            button.colors = colors;
        }
    }

    private void RestoreColors()
    {
        foreach (var entry in imageColors)
            if (entry.Key != null) entry.Key.color = entry.Value;
        foreach (var entry in buttonColors)
            if (entry.Key != null) entry.Key.colors = entry.Value;
    }

    public static int GetConfirmedVotedTarget(int localId)
    {
        var session = ThuongOnlineSession.Active;
        return session != null && session.LocalPlayerId == localId ? session.LocalConfirmedVoteTarget : -1;
    }
}
