using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class NetworkRoleCardUI : MonoBehaviour, IPointerClickHandler
{
    [Serializable]
    public class RoleImageEntry
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

    private void Awake()
    {
        RegisterImagesToDatabase();
    }

    public void RegisterImagesToDatabase()
    {
        if (roleImages == null) return;
        foreach (RoleImageEntry entry in roleImages)
        {
            if (entry != null && entry.image != null)
            {
                RoleInfoDatabase.RegisterRoleSprite(entry.role, entry.image);
            }
        }
    }

    private void OnEnable()
    {
        NetworkRoleSync.OnLocalRoleReceived +=
            OnRoleReceived;

        if (NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.HasAssignedRole)
        {
            OnRoleReceived(NetworkRoleSync.LocalInstance.LocalRole);
        }
    }

    private void OnDisable()
    {
        NetworkRoleSync.OnLocalRoleReceived -=
            OnRoleReceived;
    }

    private void Start()
    {
        RegisterImagesToDatabase();

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

        if (NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.HasAssignedRole)
        {
            OnRoleReceived(
                NetworkRoleSync.LocalInstance.LocalRole
            );
        }
        else
        {
            ResetCardUI();
        }
    }

    private void Update()
    {
        if (roleImage != null && (roleImage.sprite == null || !roleImage.enabled) && NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.HasAssignedRole)
        {
            OnRoleReceived(NetworkRoleSync.LocalInstance.LocalRole);
        }
    }

    public void ResetCardUI()
    {
        if (roleText != null) roleText.text = "";
        if (roleImage != null)
        {
            roleImage.sprite = null;
            roleImage.enabled = false;
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

    public void OnRoleReceived(RoleType role)
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

    public Sprite GetRoleImage(RoleType role)
    {
        // 1. Ưu tiên lấy Sprite từ RoleInfoDatabase
        Sprite dbSprite = RoleInfoDatabase.GetRoleSprite(role);
        if (dbSprite != null)
            return dbSprite;

        // 2. Tra cứu trong roleImages cục bộ và lưu vào Database
        if (roleImages != null)
        {
            foreach (RoleImageEntry entry in roleImages)
            {
                if (entry == null)
                    continue;

                if (entry.role == role && entry.image != null)
                {
                    RoleInfoDatabase.RegisterRoleSprite(role, entry.image);
                    return entry.image;
                }
            }
        }

        return null;
    }

    private string GetRoleName(RoleType role)
    {
        return RoleInfoDatabase.GetLocalizedRoleName(role).ToUpper();
    }
}
