using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class NetworkRoleCardUI : MonoBehaviour, IPointerClickHandler
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
        Button btn = GetComponent<Button>();
        if (btn == null)
        {
            btn = gameObject.AddComponent<Button>();
        }
        if (btn != null)
        {
            btn.interactable = true;
            if (btn.targetGraphic == null)
            {
                btn.targetGraphic = GetComponent<Image>();
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnRoleCardClicked);
        }

        if (roleText != null) roleText.raycastTarget = false;
        if (roleImage != null) roleImage.raycastTarget = false;

        if (NetworkRoleSync.LocalInstance != null)
        {
            OnRoleReceived(
                NetworkRoleSync.LocalInstance.LocalRole
            );
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnRoleCardClicked();
    }

    private void OnRoleCardClicked()
    {
        Debug.Log("[ROLE CARD] RolePanel clicked -> Mở thông tin Role chi tiết!");
        if (NetworkRoleDetailUI.Instance != null)
        {
            NetworkRoleDetailUI.Instance.ShowForLocalPlayer();
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
        return RoleInfoDatabase.GetLocalizedRoleName(role).ToUpper();
    }
}