using Unity.Netcode;
using UnityEngine;

public class NetworkTargetListUI : MonoBehaviour
{
    [Header("Target Panel")]
    [SerializeField]
    private GameObject targetPanel;

    [Header("Player Card")]
    [SerializeField]
    private GameObject playerCardPrefab;

    [SerializeField]
    private Transform cardContainer;

    private void OnEnable()
    {
        NetworkPlayerNameSync.OnAnyPlayerNameChanged +=
            OnPlayerNameChanged;
    }

    private void OnDisable()
    {
        NetworkPlayerNameSync.OnAnyPlayerNameChanged -=
            OnPlayerNameChanged;
    }

    private void OnPlayerNameChanged()
    {
        if (targetPanel == null)
            return;

        if (!targetPanel.activeSelf)
            return;

        Debug.Log(
            "TARGET LIST: Player Name đã sync → cập nhật Card."
        );

        ShowTargetList();
    }

    public void ShowTargetList()
    {
        if (targetPanel != null)
        {
            targetPanel.SetActive(true);
        }

        ClearCards();

        NetworkManager networkManager =
            NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogWarning(
                "TARGET LIST: Không tìm thấy NetworkManager!"
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
            "TARGET LIST: Local Player ID = "
            + localPlayerID
        );

        if (players == null ||
            players.Length == 0)
        {
            Debug.LogWarning(
                "TARGET LIST: Chưa tìm thấy Player Network nào!"
            );

            return;
        }

        Debug.Log(
            "TARGET LIST: Tìm thấy "
            + players.Length
            + " Player Network."
        );

        // ==========================================
        // LẤY ROLE CỦA PLAYER LOCAL
        // ==========================================

        RoleType localRole =
            RoleType.Villager;

        if (NetworkRoleSync.LocalInstance != null)
        {
            localRole =
                NetworkRoleSync.LocalInstance.LocalRole;
        }

        bool isGuardian =
            localRole == RoleType.VillageGuardian;

        Debug.Log(
            "TARGET LIST: Local Role = "
            + localRole
            + " | Is Guardian = "
            + isGuardian
        );

        // ==========================================
        // TẠO TARGET CARD
        // ==========================================

        foreach (PlayerController player in players)
        {
            if (player == null)
                continue;

            int playerID =
                (int)player.OwnerClientId;

            // ==========================================
            // KHÔNG CHO TỰ TARGET
            // NGOẠI TRỪ GUARDIAN
            // ==========================================

            if (!isGuardian &&
                playerID == localPlayerID)
            {
                continue;
            }

            if (playerCardPrefab == null)
            {
                Debug.LogWarning(
                    "TARGET LIST: "
                    + "Player Card Prefab đang NULL!"
                );

                return;
            }

            if (cardContainer == null)
            {
                Debug.LogWarning(
                    "TARGET LIST: "
                    + "Card Container đang NULL!"
                );

                return;
            }

            GameObject card =
                Instantiate(
                    playerCardPrefab,
                    cardContainer
                );

            NetworkTargetCardUI cardUI =
                card.GetComponent<NetworkTargetCardUI>();

            if (cardUI == null)
            {
                Debug.LogWarning(
                    "TARGET LIST: "
                    + "PlayerCard chưa có "
                    + "NetworkTargetCardUI!"
                );

                Destroy(card);

                continue;
            }

            // ==========================================
            // KIỂM TRA ALIVE
            // ==========================================

            bool isAlive = true;

            NetworkPlayerStateSync stateSync =
                player.GetComponent<NetworkPlayerStateSync>();

            if (stateSync != null)
            {
                isAlive =
                    stateSync.State.Value ==
                    NetworkPlayerStateType.Alive;
            }

            // ==========================================
            // LẤY PLAYER NAME
            // ==========================================

            NetworkPlayerNameSync nameSync =
                player.GetComponent<NetworkPlayerNameSync>();

            Debug.Log(
                "TARGET NAME DEBUG | Player = "
                + (playerID + 1)
                + " | nameSync = "
                + (nameSync != null ? "FOUND" : "NULL")
                + " | Name = "
                + (nameSync != null
                    ? nameSync.GetPlayerName()
                    : "N/A")
            );

            string playerName =
                "Player " + (playerID + 1);

            if (nameSync != null)
            {
                playerName =
                    nameSync.GetPlayerName();
            }

            Debug.Log(
                "TARGET NAME TEST"
                + " | Player = "
                + (playerID + 1)
                + " | Name = "
                + playerName
            );

            // ==========================================
            // SETUP CARD
            // ==========================================

            cardUI.Setup(
                playerID,
                playerName,
                isAlive
            );

            Debug.Log(
                "TARGET LIST: Tạo Card"
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
        if (cardContainer == null)
            return;

        foreach (Transform child in cardContainer)
        {
            Destroy(child.gameObject);
        }
    }
}