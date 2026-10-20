using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class VoteButton : MonoBehaviour
{
    public int voterID;
    public int targetID;
    private Button button;
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
        var online = Thuong.Online.ThuongOnlineSession.Active;
        if (online != null)
        {
            button.interactable = online.CanVoteFor(targetID);
            return;
        }
        var voter = PlayerManager.Instance?.GetplayerByID(voterID);
        var target = PlayerManager.Instance?.GetplayerByID(targetID);
        button.interactable = GameRoleManager.Instance != null &&
            GameRoleManager.Instance.currentState == GameState.Voting &&
            voter != null && target != null && voter.isAlive && target.isAlive && !voter.hasVoted;
    }
    public void CastVote()
    {
        var online = Thuong.Online.ThuongOnlineSession.Active;
        if (online != null)
        {
            if (online.CanVoteFor(targetID)) online.RequestVote(targetID);
            return;
        }
        if (VoteManager.Instance != null && VoteManager.Instance.TryVote(voterID, targetID))
            Debug.Log($"Vote accepted: Player {voterID + 1} -> Player {targetID + 1}");
        Update();
    }
}

