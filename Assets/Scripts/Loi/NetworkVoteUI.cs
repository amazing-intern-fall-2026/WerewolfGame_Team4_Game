using Unity.Netcode;
using UnityEngine;

public class NetworkVoteUI : MonoBehaviour
{
    [Header("Vote Panel")]
    [SerializeField]
    private GameObject votePanel;

    [Header("Vote Card")]
    [SerializeField]
    private GameObject voteCardPrefab;

    [SerializeField]
    private Transform voteCardContainer;

    private NetworkPhaseSync phaseSync;

    private bool isVoting;

    private void Start()
    {
        phaseSync =
            FindFirstObjectByType<NetworkPhaseSync>();

        if (votePanel != null)
        {
            votePanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (phaseSync == null)
        {
            phaseSync =
                FindFirstObjectByType<NetworkPhaseSync>();

            if (phaseSync == null)
                return;
        }

        GamePhase currentPhase =
            phaseSync.CurrentPhase.Value;

  

        bool newIsVoting =
            currentPhase == GamePhase.Voting;

        if (newIsVoting == isVoting)
            return;

        isVoting = newIsVoting;

        if (isVoting)
        {
            ShowVotePanel();
        }
        else
        {
            HideVotePanel();
        }
    }

    private void ShowVotePanel()
    {
        Debug.Log(
            "VOTE UI: Voting bắt đầu."
        );

        if (votePanel != null)
        {
            votePanel.SetActive(true);
        }

        CreateVoteCards();
    }

    private void HideVotePanel()
    {
        Debug.Log(
            "VOTE UI: Voting kết thúc."
        );

        if (votePanel != null)
        {
            votePanel.SetActive(false);
        }

        ClearCards();
    }

    private void CreateVoteCards()
    {
        ClearCards();

        if (voteCardPrefab == null)
        {
            Debug.LogWarning(
                "VOTE UI: Vote Card Prefab đang NULL!"
            );

            return;
        }

        if (voteCardContainer == null)
        {
            Debug.LogWarning(
                "VOTE UI: Vote Card Container đang NULL!"
            );

            return;
        }

        NetworkManager networkManager =
            NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogWarning(
                "VOTE UI: Không tìm thấy NetworkManager!"
            );

            return;
        }

        int localPlayerID = -1;

        PlayerController[] players =
            FindObjectsByType<PlayerController>(
                FindObjectsSortMode.None
            );

        foreach (PlayerController player in players)
        {
            if (!player.IsOwner)
                continue;

            localPlayerID =
                (int)player.OwnerClientId;

            break;
        }

        Debug.Log(
            "VOTE UI: Local Player ID = "
            + localPlayerID
        );

        foreach (PlayerController player in players)
        {
            if (player == null)
                continue;

            int playerID =
                (int)player.OwnerClientId;

            // Không cho vote chính mình
            if (playerID == localPlayerID)
                continue;

            NetworkPlayerStateSync stateSync =
                player.GetComponent<
                    NetworkPlayerStateSync
                >();

            bool isAlive = true;

            if (stateSync != null)
            {
                isAlive =
                    stateSync.State.Value ==
                    NetworkPlayerStateType.Alive;
            }

            GameObject card =
                Instantiate(
                    voteCardPrefab,
                    voteCardContainer
                );

            NetworkVoteCardUI cardUI =
                card.GetComponent<
                    NetworkVoteCardUI
                >();

            if (cardUI == null)
            {
                Debug.LogWarning(
                    "VOTE UI: Vote Card Prefab "
                    + "chưa có NetworkVoteCardUI!"
                );

                Destroy(card);
                continue;
            }

            string playerName =
                "Player " + playerID;

            cardUI.Setup(
                playerID,
                playerName,
                isAlive
            );

            Debug.Log(
                "VOTE UI: Tạo Vote Card"
                + " | PlayerID = "
                + playerID
                + " | Name = "
                + playerName
                + " | Alive = "
                + isAlive
            );
        }
    }

    private void ClearCards()
    {
        if (voteCardContainer == null)
            return;

        foreach (Transform child in voteCardContainer)
        {
            Destroy(child.gameObject);
        }
    }
}