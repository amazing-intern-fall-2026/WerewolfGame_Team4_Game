using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NetworkPlayerIdentityCardUI : MonoBehaviour, IPointerClickHandler
{
    [Serializable]
    public class RoleImageEntry
    {
        public RoleType role;
        public Sprite image;
    }

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private Image characterImage;
    [SerializeField] private TextMeshProUGUI roleNameText;

    [SerializeField] private Button cardButton;

    [Header("Role Images")]
    [SerializeField] private RoleImageEntry[] roleImages;

    public RoleImageEntry[] RoleImages => roleImages;

    private bool playerNameInitialized;

    private void OnEnable()
    {
        NetworkRoleSync.OnLocalRoleReceived += OnRoleReceived;
        NetworkRoleSync.OnLocalWhiteHoundAwakenedChanged += OnWhiteHoundAwakenedChanged;
    }

    private void OnDisable()
    {
        NetworkRoleSync.OnLocalRoleReceived -= OnRoleReceived;
        NetworkRoleSync.OnLocalWhiteHoundAwakenedChanged -= OnWhiteHoundAwakenedChanged;
    }

    private void Start()
    {
        if (cardButton == null)
        {
            cardButton = GetComponent<Button>();
            if (cardButton == null)
            {
                cardButton = gameObject.AddComponent<Button>();
            }
        }

        if (cardButton != null)
        {
            cardButton.interactable = true;
            if (cardButton.targetGraphic == null)
            {
                cardButton.targetGraphic = GetComponent<Image>();
            }
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(OnIdentityCardClicked);
        }

        // Đảm bảo các component con không cản trở click vào thẻ
        if (playerNameText != null) playerNameText.raycastTarget = false;
        if (roleNameText != null) roleNameText.raycastTarget = false;
        if (characterImage != null) characterImage.raycastTarget = false;

        Image cardImg = GetComponent<Image>();
        if (cardImg != null) cardImg.raycastTarget = true;

        TrySetLocalPlayerName();

        if (NetworkRoleSync.LocalInstance != null)
        {
            OnRoleReceived(
                NetworkRoleSync.LocalInstance.LocalRole
            );
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("[IDENTITY CARD] OnPointerClick event received!");
        OnIdentityCardClicked();
    }

    private void OnIdentityCardClicked()
    {
        Debug.Log("[IDENTITY CARD] Click PlayerIdentityCard -> Mở thông tin Role chi tiết!");
        if (NetworkRoleDetailUI.Instance != null)
        {
            NetworkRoleDetailUI.Instance.ShowForLocalPlayer();
        }
        else
        {
            Debug.LogError("[IDENTITY CARD] Không tìm thấy NetworkRoleDetailUI.Instance trong scene!");
        }
    }

    private void Update()
    {
        if (playerNameInitialized)
            return;

        TrySetLocalPlayerName();
    }

    private void TrySetLocalPlayerName()
    {
        if (NetworkRoleSync.LocalInstance == null)
            return;

        ulong clientId =
            NetworkRoleSync.LocalInstance.OwnerClientId;

        NetworkPlayerNameSync nameSync =
            NetworkRoleSync.LocalInstance
                .GetComponent<NetworkPlayerNameSync>();

        string playerName;

        if (nameSync != null)
        {
            playerName =
                nameSync.GetPlayerName();
        }
        else
        {
            playerName =
                "Player " + (clientId + 1);
        }

        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        playerNameInitialized = true;

        Debug.Log(
            "IDENTITY CARD | Local Player = "
            + playerName
            + " | ClientID = "
            + clientId
        );
    }

    private void OnRoleReceived(RoleType role)
    {
        Debug.Log(
            "IDENTITY CARD | Local Role = " + role
        );

        if (roleNameText != null)
        {
            roleNameText.text = GetRoleName(role);
        }

        Sprite roleSprite = GetRoleImage(role);

        if (characterImage != null)
        {
            characterImage.sprite = roleSprite;
            characterImage.enabled = roleSprite != null;
        }
    }

    private Sprite GetRoleImage(RoleType role)
    {
        if (roleImages == null)
            return null;

        foreach (RoleImageEntry entry in roleImages)
        {
            if (entry == null)
                continue;

            if (entry.role == role)
                return entry.image;
        }

        return null;
    }

    private void OnWhiteHoundAwakenedChanged(bool awakened)
    {
        if (NetworkRoleSync.LocalInstance != null)
        {
            OnRoleReceived(NetworkRoleSync.LocalInstance.LocalRole);
        }
    }

    private string GetRoleName(RoleType role)
    {
        if ((role == RoleType.WhiteHound || role == RoleType.WhiteWolf) &&
            NetworkRoleSync.LocalInstance != null &&
            NetworkRoleSync.LocalInstance.IsWhiteHoundAwakened)
        {
            return "BẠCH KHUYỂN (DOGSPIRIT)";
        }

        return RoleInfoDatabase.GetLocalizedRoleName(role).ToUpper();
    }
}