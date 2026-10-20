using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Reuse the scene's vote-row template, but expose every registered player through a scroll list.
public sealed class ThuongMeetingRosterUI : MonoBehaviour
{
    private GameHUD hud;
    private RectTransform content;
    private VoteButton template;
    private readonly Dictionary<int, VoteButton> rows = new();
    public void Initialize(GameHUD owner)
    {
        if (content != null || owner.meetingPanel == null) return;
        hud = owner;
        var original = hud.meetingPanel.GetComponentsInChildren<VoteButton>(true);
        if (original.Length == 0) return;
        template = original[0];
        var viewport = new GameObject("Full lobby viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        var rect = viewport.GetComponent<RectTransform>();
        rect.SetParent(hud.meetingPanel.transform, false);
        rect.anchorMin = new Vector2(.06f, .035f); rect.anchorMax = new Vector2(.95f, .67f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(.08f, .16f, .2f, .7f);
        var root = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content = root.GetComponent<RectTransform>();
        content.SetParent(rect, false);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
        var layout = root.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 5; layout.padding = new RectOffset(5, 5, 5, 5);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        root.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.GetComponent<ScrollRect>();
        scroll.viewport = rect; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
        foreach (var row in original)
        {
            row.transform.SetParent(content, false);
            ConfigureRow(row);
            rows[row.targetID] = row;
        }
        Refresh();
    }
    private static void ConfigureRow(VoteButton row)
    {
        var layout = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = 48;
    }
    private void Update() { if (content != null) Refresh(); }
    public void Refresh()
    {
        var players = PlayerManager.Instance?.players;
        if (players == null || content == null) return;
        var ids = new HashSet<int>(players.Where(player => player != null).Select(player => player.playerID));
        foreach (var player in players)
        {
            if (player == null) continue;
            if (!rows.TryGetValue(player.playerID, out var row))
            {
                row = Instantiate(template.gameObject, content).GetComponent<VoteButton>();
                row.targetID = player.playerID; row.name = "Vote Player " + (player.playerID + 1);
                ConfigureRow(row); rows.Add(player.playerID, row);
            }
            row.gameObject.SetActive(true);
            row.voterID = hud.localVoterID;
            row.Refresh();
        }
        foreach (var pair in rows) if (!ids.Contains(pair.Key)) pair.Value.gameObject.SetActive(false);
    }
}
