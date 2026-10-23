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

    [SerializeField]
    private UnityEngine.UI.Button closeButton;

    [SerializeField]
    private UnityEngine.UI.Button hudPlayerListButton;

    public static NetworkTargetListUI Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseTargetList);
        }

        if (hudPlayerListButton != null)
        {
            hudPlayerListButton.onClick.RemoveAllListeners();
            hudPlayerListButton.onClick.AddListener(() => ShowTargetList(-1));
        }
    }

    public void CloseTargetList()
    {
        if (targetPanel != null)
        {
            targetPanel.SetActive(false);
        }
    }

    public void Refresh()
    {
        if (targetPanel != null && targetPanel.activeSelf)
        {
            ShowTargetList(currentSubActionId);
        }
    }

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

    private int currentSubActionId;

    private void OnPlayerNameChanged()
    {
        if (targetPanel == null)
            return;

        if (!targetPanel.activeSelf)
            return;

        Debug.Log(
            "TARGET LIST: Player Name đã sync → cập nhật Card."
        );

        ShowTargetList(currentSubActionId);
    }

    public void ShowTargetList(int subActionId = 0)
    {
        currentSubActionId = subActionId;

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

        bool isShamanSave =
            localRole == RoleType.Shaman &&
            currentSubActionId == 1;

        bool allowSelfTarget =
            isGuardian || isShamanSave || currentSubActionId < 0;

        Debug.Log(
            "TARGET LIST: Local Role = "
            + localRole
            + " | SubAction = "
            + currentSubActionId
            + " | AllowSelfTarget = "
            + allowSelfTarget
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
            // NGOẠI TRỪ GUARDIAN HOẶC SHAMAN DÙNG BÙA LỢI
            // ==========================================

            if (!allowSelfTarget &&
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

            NetworkPlayerStateSync stateSync =
                player.GetComponent<NetworkPlayerStateSync>();

            bool isDisconnected = (stateSync != null && stateSync.State.Value == NetworkPlayerStateType.Disconnected) ||
                                  (PlayerManager.Instance != null && PlayerManager.Instance.IsDisconnected(playerID));

            bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            bool notInServerClients = isServer && (NetworkManager.Singleton.ConnectedClients == null || !NetworkManager.Singleton.ConnectedClients.ContainsKey((ulong)playerID));

            // Bỏ qua hoàn toàn nếu player đã thoát, ngắt kết nối, hoặc không còn trong danh sách active players
            if (isDisconnected ||
                NetworkGameManager.IsPlayerLeft((ulong)playerID) ||
                notInServerClients ||
                (PlayerManager.Instance != null && !PlayerManager.Instance.IsActivePlayer(playerID)))
            {
                continue;
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
                isAlive,
                currentSubActionId
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