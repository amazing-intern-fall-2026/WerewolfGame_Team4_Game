using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NetworkRoleRowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI roleNameText;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;

    private RoleType roleType;
    private NetworkRoleLobbyConfig roleConfig;

    public void Setup(
        RoleType role,
        NetworkRoleLobbyConfig config)
    {
        roleType = role;
        roleConfig = config;

        if (roleNameText != null)
        {
            roleNameText.text =
                role.ToString();
        }

        RefreshAmount();

        if (minusButton != null)
        {
            minusButton.onClick.RemoveAllListeners();
            minusButton.onClick.AddListener(RemoveRole);
        }

        if (plusButton != null)
        {
            plusButton.onClick.RemoveAllListeners();
            plusButton.onClick.AddListener(AddRole);
        }
    }

    private void AddRole()
    {
        if (roleConfig == null)
            return;

        if (!roleConfig.IsServer)
            return;

        roleConfig.AddRole(roleType);

        RefreshAmount();
    }

    private void RemoveRole()
    {
        if (roleConfig == null)
            return;

        if (!roleConfig.IsServer)
            return;

        roleConfig.RemoveRole(roleType);

        RefreshAmount();
    }

    public void RefreshAmount()
    {
        if (roleConfig == null)
            return;

        if (amountText == null)
            return;

        amountText.text =
            roleConfig.GetRoleAmount(roleType).ToString();
    }
}