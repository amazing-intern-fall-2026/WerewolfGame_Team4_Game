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

    public void ShowTargetList()
    {
        // =========================================
        // 1. Hiện Target Panel
        // =========================================

        if (targetPanel != null)
        {
            targetPanel.SetActive(true);
        }

        // =========================================
        // 2. Xóa Card cũ
        // =========================================

        ClearCards();

        // =========================================
        // 3. Kiểm tra NetworkManager
        // =========================================

        NetworkManager networkManager =
            NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogWarning(
                "TARGET LIST: Không tìm thấy NetworkManager!"
            );

            return;
        }

        // =========================================
        // 4. Tìm Player local
        // =========================================

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

        // =========================================
        // 5. Kiểm tra Player
        // =========================================

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

        // =========================================
        // 6. Tạo Card từ Player Network thật
        // =========================================

        foreach (PlayerController player in players)
        {
            if (player == null)
                continue;

            int playerID =
                (int)player.OwnerClientId;

            // =====================================
            // Không cho chọn chính mình
            // =====================================

            if (playerID == localPlayerID)
            {
                continue;
            }

            // =====================================
            // Kiểm tra PlayerCard
            // =====================================

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

            // =====================================
            // Tạo Card
            // =====================================

            GameObject card =
                Instantiate(
                    playerCardPrefab,
                    cardContainer
                );

            // =====================================
            // Lấy NetworkTargetCardUI
            // =====================================

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

            // =====================================
            // Lấy trạng thái Player
            // =====================================

            bool isAlive = true;

            NetworkPlayerStateSync stateSync =
                player.GetComponent<NetworkPlayerStateSync>();

            if (stateSync != null)
            {
                isAlive =
                    stateSync.State.Value ==
                    NetworkPlayerStateType.Alive;
            }

            // =====================================
            // Tên Player
            // =====================================

            string playerName =
                "Player " + playerID;

            // =====================================
            // Setup Card
            // =====================================

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

    // =============================================
    // Xóa toàn bộ Card cũ
    // =============================================

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