using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Action Controls")]
    [SerializeField]
    private TextMeshProUGUI voteTitleText;

    [SerializeField]
    private TextMeshProUGUI selectedTargetText;

    [SerializeField]
    private TextMeshProUGUI statusMessageText;

    [SerializeField]
    private Button confirmVoteButton;

    [SerializeField]
    private Button cancelVoteButton;

    public static NetworkVoteUI Instance;

    private NetworkPhaseSync phaseSync;
    private bool isVoting;
    private int selectedTargetID = -1;
    private bool hasVotedThisPhase = false;
    private readonly Dictionary<int, NetworkVoteCardUI> spawnedCards = new Dictionary<int, NetworkVoteCardUI>();

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
        phaseSync = FindFirstObjectByType<NetworkPhaseSync>();

        EnsureUIReferences();

        if (confirmVoteButton != null)
        {
            confirmVoteButton.onClick.RemoveAllListeners();
            confirmVoteButton.onClick.AddListener(OnConfirmVoteClicked);
        }

        if (cancelVoteButton != null)
        {
            cancelVoteButton.onClick.RemoveAllListeners();
            cancelVoteButton.onClick.AddListener(OnCancelVoteClicked);
        }

        if (votePanel != null)
        {
            votePanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        NetworkPlayerNameSync.OnAnyPlayerNameChanged += OnPlayerNameChanged;
        NetworkPlayerVote.OnLocalVoteConfirmed += OnLocalVoteConfirmed;
        NetworkPlayerVote.OnVotesReset += OnVotesReset;
    }

    private void OnDisable()
    {
        NetworkPlayerNameSync.OnAnyPlayerNameChanged -= OnPlayerNameChanged;
        NetworkPlayerVote.OnLocalVoteConfirmed -= OnLocalVoteConfirmed;
        NetworkPlayerVote.OnVotesReset -= OnVotesReset;
    }

    private void EnsureUIReferences()
    {
        if (votePanel == null)
        {
            var vp = transform.Find("VotePanel");
            if (vp != null) votePanel = vp.gameObject;
            else
            {
                var found = GameObject.Find("VotePanel");
                if (found != null) votePanel = found;
            }
        }

        if (votePanel != null)
        {
            RectTransform vpRt = votePanel.GetComponent<RectTransform>();
            if (vpRt != null)
            {
                vpRt.anchorMin = new Vector2(0.5f, 0.5f);
                vpRt.anchorMax = new Vector2(0.5f, 0.5f);
                vpRt.pivot = new Vector2(0.5f, 0.5f);
                vpRt.anchoredPosition = Vector2.zero;
                vpRt.sizeDelta = new Vector2(1360, 780);
            }

            Image vpImg = votePanel.GetComponent<Image>();
            if (vpImg != null)
            {
                vpImg.color = new Color(0.06f, 0.08f, 0.14f, 0.96f);
            }

            if (voteCardContainer == null)
            {
                var vc = votePanel.transform.Find("VoteCardContainer");
                if (vc != null) voteCardContainer = vc;
            }

            if (voteCardContainer != null)
            {
                RectTransform vcRt = voteCardContainer.GetComponent<RectTransform>();
                if (vcRt != null)
                {
                    vcRt.anchorMin = new Vector2(0.5f, 0.5f);
                    vcRt.anchorMax = new Vector2(0.5f, 0.5f);
                    vcRt.pivot = new Vector2(0.5f, 0.5f);
                    vcRt.anchoredPosition = new Vector2(0, 10);
                    vcRt.sizeDelta = new Vector2(1260, 480);
                }

                Image vcImg = voteCardContainer.GetComponent<Image>();
                if (vcImg != null)
                {
                    vcImg.enabled = false;
                }

                GridLayoutGroup grid = voteCardContainer.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    grid.cellSize = new Vector2(270, 195);
                    grid.spacing = new Vector2(24, 20);
                    grid.childAlignment = TextAnchor.MiddleCenter;
                    grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
                    grid.constraintCount = 2;
                }
            }

            if (voteTitleText == null)
            {
                var vt = votePanel.transform.Find("VoteTitle");
                if (vt != null) voteTitleText = vt.GetComponent<TextMeshProUGUI>();
            }

            if (voteTitleText != null)
            {
                RectTransform vtRt = voteTitleText.GetComponent<RectTransform>();
                if (vtRt != null)
                {
                    vtRt.anchorMin = new Vector2(0.5f, 1f);
                    vtRt.anchorMax = new Vector2(0.5f, 1f);
                    vtRt.pivot = new Vector2(0.5f, 1f);
                    vtRt.anchoredPosition = new Vector2(0, -20);
                    vtRt.sizeDelta = new Vector2(600, 50);
                }

                voteTitleText.text = "BỎ PHIẾU TREO CỔ";
                voteTitleText.fontSize = 36;
                voteTitleText.fontStyle = FontStyles.Bold;
                voteTitleText.color = new Color(1f, 0.85f, 0.2f, 1f);
                voteTitleText.alignment = TextAlignmentOptions.Center;
            }

            if (selectedTargetText == null)
            {
                var st = votePanel.transform.Find("SelectedTargetText");
                if (st != null)
                {
                    selectedTargetText = st.GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    selectedTargetText = CreateSelectedTargetText(votePanel.transform);
                }
            }

            if (statusMessageText == null)
            {
                var sm = votePanel.transform.Find("StatusMessageText");
                if (sm != null)
                {
                    statusMessageText = sm.GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    statusMessageText = CreateStatusMessageText(votePanel.transform);
                }
            }

            Transform subTrans = votePanel.transform.Find("VoteSubtitle");
            if (subTrans != null)
            {
                var subTmp = subTrans.GetComponent<TextMeshProUGUI>();
                if (subTmp != null) subTmp.text = "Click vào thẻ người chơi để bỏ phiếu treo cổ.";
            }

            if (confirmVoteButton == null)
            {
                var cb = votePanel.transform.Find("ConfirmVoteButton");
                if (cb != null) confirmVoteButton = cb.GetComponent<Button>();
            }
            if (confirmVoteButton != null)
            {
                confirmVoteButton.gameObject.SetActive(false);
            }

            if (cancelVoteButton == null)
            {
                var canB = votePanel.transform.Find("CancelVoteButton");
                if (canB != null) cancelVoteButton = canB.GetComponent<Button>();
            }
            if (cancelVoteButton != null)
            {
                cancelVoteButton.gameObject.SetActive(false);
            }
        }
    }

    private TextMeshProUGUI CreateSelectedTargetText(Transform parent)
    {
        GameObject obj = new GameObject("SelectedTargetText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0, 35);
        rt.sizeDelta = new Vector2(800, 32);

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        if (voteTitleText != null && voteTitleText.font != null)
            tmp.font = voteTitleText.font;
        tmp.text = "Chưa chọn người chơi nào";
        tmp.fontSize = 22;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.35f, 0.9f, 1f, 1f);
        return tmp;
    }

    private TextMeshProUGUI CreateStatusMessageText(Transform parent)
    {
        GameObject obj = new GameObject("StatusMessageText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0, 68);
        rt.sizeDelta = new Vector2(800, 28);

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        if (voteTitleText != null && voteTitleText.font != null)
            tmp.font = voteTitleText.font;
        tmp.text = "";
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.85f, 0.4f, 1f);
        return tmp;
    }

    private Button CreateActionButton(Transform parent, string name, string label, Vector2 pos, Color btnColor)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(190, 48);

        Image img = obj.GetComponent<Image>();
        img.color = btnColor;

        Button btn = obj.GetComponent<Button>();
        btn.targetGraphic = img;

        GameObject textObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(obj.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        if (voteTitleText != null && voteTitleText.font != null)
            tmp.font = voteTitleText.font;
        tmp.text = label;
        tmp.fontSize = 22;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    public void Refresh()
    {
        if (votePanel != null && votePanel.activeSelf)
        {
            CreateVoteCards();
        }
    }

    private void OnPlayerNameChanged()
    {
        if (!isVoting)
            return;

        Debug.Log("VOTE UI: Player Name đã sync → cập nhật Card.");
        CreateVoteCards();
    }

    private void OnLocalVoteConfirmed(int voterId, int targetId)
    {
        hasVotedThisPhase = true;
        selectedTargetID = targetId;
        if (confirmVoteButton != null) confirmVoteButton.interactable = false;
        if (cancelVoteButton != null) cancelVoteButton.interactable = false;

        string targetName = GetPlayerDisplayName(targetId);
        if (selectedTargetText != null)
            selectedTargetText.text = $"Đã bỏ phiếu cho: <color=#FFDD44>{targetName}</color>";

        if (statusMessageText != null)
            statusMessageText.text = $"<color=#44FF88>ĐÃ BỎ PHIẾU CHO {targetName}! (Có thể click thẻ khác để đổi vote)</color>";

        foreach (var kvp in spawnedCards)
        {
            if (kvp.Value != null)
            {
                kvp.Value.SetSelected(kvp.Key == targetId);
            }
        }
    }

    private void OnVotesReset()
    {
        hasVotedThisPhase = false;
        selectedTargetID = -1;

        if (isVoting && votePanel != null && votePanel.activeSelf)
        {
            CreateVoteCards();
        }
    }

    private void Update()
    {
        if (phaseSync == null)
        {
            phaseSync = FindFirstObjectByType<NetworkPhaseSync>();
            if (phaseSync == null)
                return;
        }

        GamePhase currentPhase = phaseSync.CurrentPhase.Value;
        bool newIsVoting = currentPhase == GamePhase.Voting;

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
        Debug.Log("VOTE UI: Voting bắt đầu.");

        EnsureUIReferences();

        if (votePanel != null)
        {
            votePanel.SetActive(true);
        }

        int localPlayerID = GetLocalPlayerID();
        selectedTargetID = NetworkPlayerVote.GetCurrentChoice(localPlayerID);
        hasVotedThisPhase = selectedTargetID >= 0;

        CreateVoteCards();
    }

    private void HideVotePanel()
    {
        Debug.Log("VOTE UI: Voting kết thúc.");

        if (votePanel != null)
        {
            votePanel.SetActive(false);
        }

        ClearCards();
        selectedTargetID = -1;
        hasVotedThisPhase = false;
    }

    private int GetLocalPlayerID()
    {
        if (NetworkManager.Singleton != null)
        {
            return (int)NetworkManager.Singleton.LocalClientId;
        }

        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p != null && p.IsOwner)
                return (int)p.OwnerClientId;
        }

        return -1;
    }

    private bool IsLocalPlayerAlive(int localPlayerID)
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p != null && (p.IsOwner || (int)p.OwnerClientId == localPlayerID))
            {
                var state = p.GetComponent<NetworkPlayerStateSync>();
                if (state != null)
                    return state.State.Value == NetworkPlayerStateType.Alive;
            }
        }

        if (PlayerManager.Instance != null)
        {
            return PlayerManager.Instance.IsAlive(localPlayerID);
        }

        return true;
    }

    public void OnCardClicked(int targetID)
    {
        int localPlayerID = GetLocalPlayerID();
        if (!IsLocalPlayerAlive(localPlayerID))
        {
            Debug.LogWarning("VOTE UI: Bạn đã chết, không thể bỏ phiếu!");
            if (statusMessageText != null)
                statusMessageText.text = "<color=#FF4444>Bạn đã bị loại, không thể bỏ phiếu!</color>";
            return;
        }

        if (!spawnedCards.ContainsKey(targetID))
            return;

        // Nếu click trúng người chơi đang được chọn rồi
        if (hasVotedThisPhase && selectedTargetID == targetID)
        {
            string currName = GetPlayerDisplayName(targetID);
            Debug.Log($"VOTE UI: Bạn đang bỏ phiếu cho {currName} rồi.");
            if (statusMessageText != null)
                statusMessageText.text = $"<color=#FFDD44>Bạn đang bỏ phiếu cho {currName}. (Click thẻ khác để đổi vote)</color>";
            return;
        }

        selectedTargetID = targetID;

        // Cập nhật trạng thái chọn trên tất cả thẻ
        foreach (var kvp in spawnedCards)
        {
            if (kvp.Value != null)
            {
                kvp.Value.SetSelected(kvp.Key == selectedTargetID);
            }
        }

        string targetName = GetPlayerDisplayName(targetID);
        if (selectedTargetText != null)
        {
            selectedTargetText.text = $"Đã bỏ phiếu cho: <color=#FFDD44>{targetName}</color>";
        }

        // Bỏ phiếu trực tiếp hoặc đổi vote ngay khi click thẻ, không cần nhấn xác nhận
        SubmitVoteDirectly(targetID);
    }

    private void SubmitVoteDirectly(int targetID)
    {
        int localPlayerID = GetLocalPlayerID();

        // Tìm NetworkPlayerVote của Local Player
        NetworkPlayerVote localVote = null;
        NetworkPlayerVote[] allVotes = FindObjectsByType<NetworkPlayerVote>(FindObjectsSortMode.None);
        foreach (var v in allVotes)
        {
            if (v != null && (v.IsOwner || (int)v.OwnerClientId == localPlayerID))
            {
                localVote = v;
                break;
            }
        }

        if (localVote == null)
        {
            Debug.LogWarning("VOTE UI: Không tìm thấy NetworkPlayerVote của Local Player!");
            return;
        }

        Debug.Log($"VOTE UI: Local Player trực tiếp bỏ phiếu/đổi vote cho Player {targetID} qua click thẻ.");
        localVote.VoteFromUI(targetID);

        hasVotedThisPhase = true;
        selectedTargetID = targetID;

        foreach (var kvp in spawnedCards)
        {
            if (kvp.Value != null)
            {
                kvp.Value.SetSelected(kvp.Key == selectedTargetID);
            }
        }

        string targetName = GetPlayerDisplayName(targetID);
        if (selectedTargetText != null)
            selectedTargetText.text = $"Đã bỏ phiếu cho: <color=#FFDD44>{targetName}</color>";

        if (statusMessageText != null)
            statusMessageText.text = $"<color=#44FF88>ĐÃ BỎ PHIẾU CHO {targetName}! (Có thể click thẻ khác để đổi vote)</color>";
    }

    public void OnCancelVoteClicked()
    {
        // Không dùng khi click thẻ trực tiếp
    }

    public void OnConfirmVoteClicked()
    {
        if (selectedTargetID >= 0)
        {
            SubmitVoteDirectly(selectedTargetID);
        }
    }

    private string GetPlayerDisplayName(int playerId)
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p != null && (int)p.OwnerClientId == playerId)
            {
                var nameSync = p.GetComponent<NetworkPlayerNameSync>();
                if (nameSync != null && nameSync.HasValidName())
                    return nameSync.GetPlayerName();
            }
        }

        if (PlayerManager.Instance != null)
        {
            var pd = PlayerManager.Instance.GetplayerByID(playerId);
            if (pd != null && !string.IsNullOrWhiteSpace(pd.playerName))
                return pd.playerName;
        }

        if (NetworkGamePlayerNameSetup.HasName((ulong)playerId))
        {
            return NetworkGamePlayerNameSetup.GetName((ulong)playerId);
        }

        return "Player " + (playerId + 1);
    }

    private void CreateVoteCards()
    {
        ClearCards();

        if (voteCardPrefab == null)
        {
            Debug.LogWarning("VOTE UI: Vote Card Prefab đang NULL!");
            return;
        }

        if (voteCardContainer == null)
        {
            Debug.LogWarning("VOTE UI: Vote Card Container đang NULL!");
            return;
        }

        int localPlayerID = GetLocalPlayerID();
        bool localIsAlive = IsLocalPlayerAlive(localPlayerID);

        // Cập nhật trạng thái điều khiển theo vai trò người bỏ phiếu
        if (!localIsAlive)
        {
            if (statusMessageText != null)
                statusMessageText.text = "<color=#FF6666>BẠN ĐÃ BỊ LOẠI - KHÔNG THỂ BỎ PHIẾU</color>";
            if (confirmVoteButton != null) confirmVoteButton.interactable = false;
            if (cancelVoteButton != null) cancelVoteButton.interactable = false;
        }
        else if (hasVotedThisPhase && selectedTargetID >= 0)
        {
            string votedName = GetPlayerDisplayName(selectedTargetID);
            if (selectedTargetText != null) selectedTargetText.text = $"Đã bỏ phiếu cho: <color=#FFDD44>{votedName}</color>";
            if (statusMessageText != null) statusMessageText.text = $"<color=#44FF88>ĐÃ BỎ PHIẾU CHO {votedName}! (Có thể click thẻ khác để đổi vote)</color>";
            if (confirmVoteButton != null) confirmVoteButton.interactable = false;
            if (cancelVoteButton != null) cancelVoteButton.interactable = false;
        }
        else
        {
            if (selectedTargetID < 0)
            {
                if (selectedTargetText != null) selectedTargetText.text = "Chưa chọn người chơi nào";
                if (statusMessageText != null) statusMessageText.text = "Click vào thẻ người chơi bạn muốn bỏ phiếu.";
                if (confirmVoteButton != null) confirmVoteButton.interactable = false;
                if (cancelVoteButton != null) cancelVoteButton.interactable = false;
            }
        }

        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        // Đếm số lượng người chơi còn sống hợp lệ
        int alivePlayersCount = 0;
        foreach (PlayerController player in players)
        {
            if (player == null) continue;
            int pid = (int)player.OwnerClientId;
            if (NetworkGameManager.IsPlayerLeft((ulong)pid)) continue;

            bool alive = true;
            NetworkPlayerStateSync stateSync = player.GetComponent<NetworkPlayerStateSync>();
            if (stateSync != null && stateSync.State.Value != NetworkPlayerStateType.Alive)
                alive = false;
            if (PlayerManager.Instance != null && !PlayerManager.Instance.IsAlive(pid))
                alive = false;

            if (alive) alivePlayersCount++;
        }

        bool allowSelfVote = alivePlayersCount <= 1; // Cho phép hiển thị thẻ của chính mình khi test 1 người chơi

        foreach (PlayerController player in players)
        {
            if (player == null)
                continue;

            int playerID = (int)player.OwnerClientId;

            // Không vote chính mình nếu có từ 2 người chơi còn sống trở lên
            if (playerID == localPlayerID && !allowSelfVote)
                continue;

            NetworkPlayerStateSync stateSync = player.GetComponent<NetworkPlayerStateSync>();

            bool isDisconnected = (stateSync != null && stateSync.State.Value == NetworkPlayerStateType.Disconnected) ||
                                  (PlayerManager.Instance != null && PlayerManager.Instance.IsDisconnected(playerID));

            bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            bool notInServerClients = isServer && (NetworkManager.Singleton.ConnectedClients == null || !NetworkManager.Singleton.ConnectedClients.ContainsKey((ulong)playerID));

            // Bỏ qua nếu player đã thoát, ngắt kết nối
            if (isDisconnected ||
                NetworkGameManager.IsPlayerLeft((ulong)playerID) ||
                notInServerClients ||
                (PlayerManager.Instance != null && !PlayerManager.Instance.IsActivePlayer(playerID)))
            {
                continue;
            }

            bool isAlive = true;
            if (stateSync != null)
            {
                isAlive = stateSync.State.Value == NetworkPlayerStateType.Alive;
            }
            if (PlayerManager.Instance != null && !PlayerManager.Instance.IsAlive(playerID))
            {
                isAlive = false;
            }

            // Chỉ hiển thị mục tiêu hợp lệ (còn sống)
            if (!isAlive)
            {
                continue;
            }

            string playerName = GetPlayerDisplayName(playerID);
            Sprite avatarSprite = player.GetComponent<SpriteRenderer>()?.sprite;

            GameObject card = Instantiate(voteCardPrefab, voteCardContainer);
            NetworkVoteCardUI cardUI = card.GetComponent<NetworkVoteCardUI>();

            if (cardUI == null)
            {
                Debug.LogWarning("VOTE UI: Vote Card Prefab chưa có NetworkVoteCardUI!");
                Destroy(card);
                continue;
            }

            cardUI.Setup(playerID, playerName, isAlive, avatarSprite);
            cardUI.SetSelected(playerID == selectedTargetID);

            spawnedCards[playerID] = cardUI;

            Debug.Log($"VOTE UI: Tạo Vote Card | PlayerID = {playerID} | Name = {playerName} | Alive = {isAlive}");
        }
    }

    private void ClearCards()
    {
        spawnedCards.Clear();

        if (voteCardContainer == null)
            return;

        foreach (Transform child in voteCardContainer)
        {
            Destroy(child.gameObject);
        }
    }

    public int GetSelectedTargetID() => selectedTargetID;
    public bool HasVotedThisPhase() => hasVotedThisPhase;
}