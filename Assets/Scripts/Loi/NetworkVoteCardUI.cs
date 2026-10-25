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
    private Image avatarImage;

    [SerializeField]
    private Image cardBackgroundImage;

    [SerializeField]
    private GameObject selectedIndicator;

    [SerializeField]
    private Button cardButton;

    [SerializeField]
    private Button infoButton;

    private int playerID = -1;
    private string cachedPlayerName = "";
    private bool isPlayerAlive = true;
    private bool isCardSelected = false;

    private static readonly Color NormalCardColor = new Color(0.12f, 0.15f, 0.22f, 0.95f);
    private static readonly Color SelectedCardColor = new Color(0.24f, 0.35f, 0.52f, 1f);

    private void OnEnable()
    {
        NetworkPlayerVote.OnVoteCountChanged += OnVoteCountChanged;
    }

    private void OnDisable()
    {
        NetworkPlayerVote.OnVoteCountChanged -= OnVoteCountChanged;
    }

    public void Setup(
        int id,
        string playerName,
        bool isAlive,
        Sprite avatarSprite = null
    )
    {
        playerID = id;
        cachedPlayerName = playerName;
        isPlayerAlive = isAlive;

        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        if (statusText != null)
        {
            statusText.text = isAlive ? "CÒN SỐNG" : "ĐÃ BỊ LOẠI";
            statusText.color = isAlive ? new Color(0.2f, 1f, 0.4f) : new Color(0.8f, 0.3f, 0.3f);
        }

        if (voteCountText != null)
        {
            voteCountText.text = "0 PHIẾU";
        }

        if (avatarImage != null)
        {
            if (avatarSprite != null)
            {
                avatarImage.sprite = avatarSprite;
                avatarImage.color = Color.white;
            }
            avatarImage.preserveAspect = true;
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

        SetSelected(false);
    }

    public void SetSelected(bool isSelected)
    {
        isCardSelected = isSelected;

        if (selectedIndicator != null)
        {
            selectedIndicator.SetActive(isSelected);
        }

        if (cardBackgroundImage != null)
        {
            cardBackgroundImage.color = isSelected ? SelectedCardColor : NormalCardColor;
        }
    }

    public bool IsSelected => isCardSelected;

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

        voteCountText.text = $"{voteCount} PHIẾU";
    }

    private void OnCardClicked()
    {
        if (!isPlayerAlive)
            return;

        if (NetworkVoteUI.Instance != null)
        {
            NetworkVoteUI.Instance.OnCardClicked(playerID);
        }
        else
        {
            Debug.LogWarning("VOTE CARD: Không tìm thấy NetworkVoteUI.Instance!");
        }
    }

    public int GetPlayerID()
    {
        return playerID;
    }

    public string GetPlayerName()
    {
        return cachedPlayerName;
    }

    public bool IsPlayerAlive()
    {
        return isPlayerAlive;
    }
}