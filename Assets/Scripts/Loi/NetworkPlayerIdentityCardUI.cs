using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NetworkPlayerIdentityCardUI : MonoBehaviour
{
    [Serializable]
    private class RoleImageEntry
    {
        public RoleType role;
        public Sprite image;
    }

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private Image characterImage;
    [SerializeField] private TextMeshProUGUI roleNameText;

    [Header("Role Images")]
    [SerializeField] private RoleImageEntry[] roleImages;

    private bool playerNameInitialized;

    private void OnEnable()
    {
        NetworkRoleSync.OnLocalRoleReceived += OnRoleReceived;
    }

    private void OnDisable()
    {
        NetworkRoleSync.OnLocalRoleReceived -= OnRoleReceived;
    }

    private void Start()
    {
        TrySetLocalPlayerName();

        if (NetworkRoleSync.LocalInstance != null)
        {
            OnRoleReceived(
                NetworkRoleSync.LocalInstance.LocalRole
            );
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

    private string GetRoleName(RoleType role)
    {
        switch (role)
        {
            case RoleType.Villager:
                return "VILLAGER";

            case RoleType.DogSpirit:
                return "DOG SPIRIT";

            case RoleType.Seer:
                return "SEER";

            case RoleType.VillageGuardian:
                return "VILLAGE GUARDIAN";

            case RoleType.Mayor:
                return "MAYOR";

            case RoleType.Idiot:
                return "IDIOT";

            case RoleType.WhiteHound:
                return "WHITE HOUND";

            case RoleType.SerpentSpirit:
                return "SERPENT SPIRIT";

            case RoleType.Ogre:
                return "OGRE";

            case RoleType.Hunter:
                return "HUNTER";

            case RoleType.Shaman:
                return "SHAMAN";

            case RoleType.WeaverOfFate:
                return "WEAVER OF FATE";

            case RoleType.Cursed:
                return "CURSED";

            case RoleType.Brat:
                return "BRAT";

            case RoleType.Madman:
                return "MADMAN";

            case RoleType.FoxSpirit:
                return "FOX SPIRIT";

            case RoleType.Killer:
                return "KILLER";

            default:
                return role.ToString();
        }
    }
}