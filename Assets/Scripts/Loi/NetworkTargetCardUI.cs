using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NetworkTargetCardUI : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI playerNameText;

    [SerializeField]
    private TextMeshProUGUI statusText;

    [SerializeField]
    private Button cardButton;

    [SerializeField]
    private Button infoButton;

    private int playerID;
    private int subActionId;
    private string cachedPlayerName;

    private Image cardImage;
    private Color defaultCardColor = Color.white;
    private bool hasCachedDefaultColor;
    private bool isSelected;

    public bool IsSelected => isSelected;
    public string PlayerName => cachedPlayerName;

    public void Setup(
        int id,
        string playerName,
        bool isAlive,
        int subAction = 0
    )
    {
        playerID = id;
        subActionId = subAction;
        cachedPlayerName = playerName;

        if (cardImage == null)
        {
            cardImage = GetComponent<Image>();
        }

        if (cardImage != null && !hasCachedDefaultColor)
        {
            defaultCardColor = cardImage.color;
            hasCachedDefaultColor = true;
        }

        SetSelected(false);

        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        if (statusText != null)
        {
            statusText.text = isAlive ? "CÒN SỐNG" : "ĐÃ BỊ LOẠI";
            statusText.color = isAlive ? new Color(0.2f, 1f, 0.4f) : new Color(0.8f, 0.3f, 0.3f);
        }

        if (cardButton != null)
        {
            cardButton.interactable = isAlive;

            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(OnCardClicked);
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

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (cardImage == null)
        {
            cardImage = GetComponent<Image>();
        }

        if (cardImage != null)
        {
            if (selected)
            {
                // Hiệu ứng đánh dấu trực quan: màu viền vàng sáng / xanh neon
                cardImage.color = new Color(1f, 0.85f, 0.1f, 1f);
            }
            else
            {
                cardImage.color = defaultCardColor;
            }
        }

        transform.localScale = selected ? new Vector3(1.05f, 1.05f, 1f) : Vector3.one;
    }

    public Sprite GetAvatarSprite()
    {
        Transform avatarTrans = transform.Find("Avatar");
        if (avatarTrans != null)
        {
            Image img = avatarTrans.GetComponent<Image>();
            if (img != null) return img.sprite;
        }
        return null;
    }

    public void TriggerCardClickForTest()
    {
        OnCardClicked();
    }

    private void OnCardClicked()
    {
        // Nếu ở chế độ xem danh sách (subActionId < 0), click card sẽ mở thông tin vai trò
        if (subActionId < 0)
        {
            if (NetworkRoleDetailUI.Instance != null)
            {
                NetworkRoleDetailUI.Instance.ShowForPlayer(playerID, cachedPlayerName);
            }
            return;
        }

        // Nếu Role yêu cầu quy trình Xác Nhận mục tiêu (như Xà Tinh), chỉ chọn và đánh dấu mục tiêu
        if (NetworkTargetListUI.Instance != null && NetworkTargetListUI.Instance.RequiresConfirmation())
        {
            NetworkTargetListUI.Instance.OnCardSelected(this, playerID, cachedPlayerName);
            return;
        }

        NetworkPlayerAction[] actions =
            FindObjectsByType<NetworkPlayerAction>(
                FindObjectsSortMode.None
            );

        foreach (NetworkPlayerAction action in actions)
        {
            if (!action.IsOwner)
                continue;

            action.RequestActionServerRpc(
                (ulong)playerID,
                subActionId
            );

            NetworkTargetListUI.Instance?.CloseTargetList();
            NetworkSkillUI.Instance?.NotifyActionDispatched();
            return;
        }

        Debug.LogWarning(
            "TARGET CARD: Không tìm thấy NetworkPlayerAction của Player local."
        );
    }

    public int GetPlayerID()
    {
        return playerID;
    }
}