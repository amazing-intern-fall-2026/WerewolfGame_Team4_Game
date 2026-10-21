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
    private TextMeshProUGUI voteCountText;

    [SerializeField]
    private Button cardButton;

    [SerializeField]
    private Button infoButton;

    private int playerID;
    private string cachedPlayerName;

    private void OnEnable()
    {
        NetworkPlayerVote.OnVoteCountChanged +=
            OnVoteCountChanged;
    }

    private void OnDisable()
    {
        NetworkPlayerVote.OnVoteCountChanged -=
            OnVoteCountChanged;
    }

    public void Setup(
        int id,
        string playerName,
        bool isAlive
    )
    {
        playerID = id;
        cachedPlayerName = playerName;

        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        if (statusText != null)
        {
            statusText.text =
                isAlive ? "CÒN SỐNG" : "ĐÃ BỊ LOẠI";
            statusText.color = isAlive ? new Color(0.2f, 1f, 0.4f) : new Color(0.8f, 0.3f, 0.3f);
        }

        if (voteCountText != null)
        {
            voteCountText.text = "0 PHIẾU";
        }

        if (cardButton != null)
        {
            cardButton.interactable = isAlive;

            cardButton.onClick.RemoveAllListeners();

            cardButton.onClick.AddListener(
                OnCardClicked
            );
        }

        if (infoButton != null)
        {
            infoButton.onClick.RemoveAllListeners();
            infoButton.onClick.AddListener(OnInfoButtonClicked);
        }
    }

    private void OnInfoButtonClicked()
    {
        if (NetworkRoleDetailUI.Instance != null)
        {
            NetworkRoleDetailUI.Instance.ShowForPlayer(playerID, cachedPlayerName);
        }
    }

    private void OnVoteCountChanged(
        int targetID,
        int voteCount)
    {
        if (targetID != playerID)
            return;

        if (voteCountText == null)
            return;

        voteCountText.text =
            $"{voteCount} PHIẾU";
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