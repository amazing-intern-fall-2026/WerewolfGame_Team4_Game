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