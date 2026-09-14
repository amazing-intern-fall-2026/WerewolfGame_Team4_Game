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
        var voter = PlayerManger.Instance?.GetplayerByID(voterID);
        var target = PlayerManger.Instance?.GetplayerByID(targetID);
        button.interactable = GameManager.Instance != null &&
            GameManager.Instance.currentState == GameState.Voting &&
            voter != null && target != null && voter.isAlive && target.isAlive && !voter.hasVoted;
    }
    public void CastVote()
    {
        if (VoteManger.Instance != null && VoteManger.Instance.TryVote(voterID, targetID))
            Debug.Log($"Vote accepted: Player {voterID + 1} -> Player {targetID + 1}");
        Update();
    }
}
