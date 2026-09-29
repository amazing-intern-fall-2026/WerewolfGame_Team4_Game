using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkRoleCardUI : MonoBehaviour
{
    [Serializable]
    private class RoleImageEntry
    {
        public RoleType role;
        public Sprite image;
    }

    [Header("UI")]
    [SerializeField]
    private Image roleImage;

    [SerializeField]
    private TextMeshProUGUI roleText;

    [Header("Role Images")]
    [SerializeField]
    private RoleImageEntry[] roleImages;

    private void OnEnable()
    {
        NetworkRoleSync.OnLocalRoleReceived +=
            OnRoleReceived;
    }

    private void OnDisable()
    {
        NetworkRoleSync.OnLocalRoleReceived -=
            OnRoleReceived;
    }

    private void Start()
    {
        if (NetworkRoleSync.LocalInstance != null)
        {
            OnRoleReceived(
                NetworkRoleSync.LocalInstance.LocalRole
            );
        }
    }

    private void OnRoleReceived(RoleType role)
    {
        Debug.Log(
            "ROLE CARD UI | Role = " + role
        );

        if (roleText != null)
        {
            roleText.text =
                GetRoleName(role);
        }

        Sprite roleSprite =
            GetRoleImage(role);

        if (roleImage != null)
        {
            roleImage.sprite =
                roleSprite;

            roleImage.enabled =
                roleSprite != null;
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