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
            statusText.text = isAlive ? "ALIVE" : "DEAD";
        }

        if (cardButton != null)
        {
            cardButton.interactable = isAlive;

            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(OnCardClicked);
        }
    }

    private void OnCardClicked()
    {
        NetworkPlayerAction[] actions =
            FindObjectsByType<NetworkPlayerAction>(
                FindObjectsSortMode.None
            );

        foreach (NetworkPlayerAction action in actions)
        {
            if (!action.IsOwner)
                continue;

            action.RequestActionServerRpc(
                (ulong)playerID
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