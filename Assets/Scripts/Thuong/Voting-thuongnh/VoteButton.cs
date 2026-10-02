using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class VoteButton : MonoBehaviour
{
    public int voterID;
    public int targetID;
    private Button button;
    private TMP_Text nameLabel;
    private void Awake() { button = GetComponent<Button>(); }
    private void OnEnable()
    {
        button = GetComponent<Button>();
        button.onClick.RemoveListener(CastVote);
        button.onClick.AddListener(CastVote);
        Update();
    }
    private void OnDisable() { if (button != null) button.onClick.RemoveListener(CastVote); }
    private void Update()
    {
        Refresh();
    }
    public void Refresh()
    {
        if (button == null) button = GetComponent<Button>();
        if (nameLabel == null) nameLabel = transform.Find("Name")?.GetComponent<TMP_Text>();
        var voter = PlayerManager.Instance?.GetplayerByID(voterID);
        var target = PlayerManager.Instance?.GetplayerByID(targetID);
        button.interactable = GameRoleManager.Instance != null &&
            GameRoleManager.Instance.currentState == GameState.Voting &&
            voter != null && target != null && voter.isAlive && target.isAlive && !voter.hasVoted;
        if (nameLabel != null)
        {
            string prototypeName = "Player " + targetID;
            string displayName = target == null || string.IsNullOrWhiteSpace(target.playerName) ||
                target.playerName == prototypeName ? "Player " + (targetID + 1) : target.playerName;
            nameLabel.text = displayName + (targetID == voterID ? " / BẠN" : "") +
                (target == null ? " / UNKNOWN" : target.isAlive ? " / ALIVE" : " / DEAD");
        }
    }
    public void CastVote()
    {
        if (VoteManager.Instance != null && VoteManager.Instance.TryVote(voterID, targetID))
            Debug.Log($"Vote accepted: Player {voterID + 1} -> Player {targetID + 1}");
        Update();
    }
}

