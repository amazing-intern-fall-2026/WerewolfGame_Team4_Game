using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NetworkVoteCardUI : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI playerNameText;

    [SerializeField]
    private TextMeshProUGUI statusText;

    [SerializeField]
    private Button cardButton;

    private int playerID;

    public void Setup(
        int id,
        string playerName,
        bool isAlive
    )
    {
        playerID = id;

        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        if (statusText != null)
        {
            statusText.text =
                isAlive ? "ALIVE" : "DEAD";
        }

        if (cardButton != null)
        {
            cardButton.interactable = isAlive;

            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(
                OnCardClicked
            );
        }
    }

    private void OnCardClicked()
    {
        if (NetworkPhaseSync.Instance == null)
        {
            Debug.LogWarning(
                "VOTE CARD: Không tìm thấy NetworkPhaseSync!"
            );

            return;
        }

        if (
            NetworkPhaseSync.Instance.CurrentPhase.Value
            != GamePhase.Voting
        )
        {
            Debug.LogWarning(
                "VOTE CARD: Chưa phải Voting!"
            );

            return;
        }

        NetworkPlayerVote[] votes =
            FindObjectsByType<NetworkPlayerVote>(
                FindObjectsSortMode.None
            );

        foreach (NetworkPlayerVote vote in votes)
        {
            if (!vote.IsOwner)
                continue;

            Debug.Log(
                "VOTE CARD: Local Player vote Player "
                + playerID
            );

            vote.VoteFromUI(playerID);

            return;
        }

        Debug.LogWarning(
            "VOTE CARD: Không tìm thấy "
            + "NetworkPlayerVote của Local Player!"
        );
    }

    public int GetPlayerID()
    {
        return playerID;
    }
}