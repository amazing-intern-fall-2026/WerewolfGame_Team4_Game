using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NetworkRoleLobbyUI : MonoBehaviour
{
    [Header("Role List")]
    [SerializeField] private Transform roleContent;
    [SerializeField] private GameObject roleRowPrefab;

    [Header("Role Count")]
    [SerializeField] private TextMeshProUGUI roleCountText;

    private NetworkRoleLobbyConfig roleConfig;

    private readonly List<NetworkRoleRowUI> roleRows =
        new List<NetworkRoleRowUI>();

    private void Start()
    {
        FindRoleConfig();
        BuildRoleList();
        SubscribeToRoleConfig();
        RefreshAllUI();
    }

    private void OnDestroy()
    {
        UnsubscribeFromRoleConfig();
    }

    private void FindRoleConfig()
    {
        roleConfig =
            FindFirstObjectByType<NetworkRoleLobbyConfig>();

        if (roleConfig == null)
        {
            Debug.LogError(
                "ROLE UI | Không tìm thấy NetworkRoleLobbyConfig."
            );

            return;
        }

        Debug.Log(
            "ROLE UI | Đã tìm thấy NetworkRoleLobbyConfig."
        );
    }

    private void SubscribeToRoleConfig()
    {
        if (roleConfig == null)
            return;

        roleConfig.OnRoleConfigChanged -= RefreshAllUI;
        roleConfig.OnRoleConfigChanged += RefreshAllUI;
    }

    private void UnsubscribeFromRoleConfig()
    {
        if (roleConfig == null)
            return;

        roleConfig.OnRoleConfigChanged -= RefreshAllUI;
    }

    private void BuildRoleList()
    {
        if (roleConfig == null)
            return;

        if (roleContent == null)
        {
            Debug.LogError(
                "ROLE UI | Chưa gán Role Content."
            );

            return;
        }

        if (roleRowPrefab == null)
        {
            Debug.LogError(
                "ROLE UI | Chưa gán Role Row Prefab."
            );

            return;
        }

        ClearRoleRows();

        RoleType[] roles =
            (RoleType[])System.Enum.GetValues(
                typeof(RoleType)
            );

        foreach (RoleType role in roles)
        {
            GameObject rowObject =
                Instantiate(
                    roleRowPrefab,
                    roleContent
                );

            NetworkRoleRowUI row =
                rowObject.GetComponent<NetworkRoleRowUI>();

            if (row == null)
            {
                Debug.LogError(
                    "ROLE UI | RoleRow prefab không có NetworkRoleRowUI."
                );

                Destroy(rowObject);
                continue;
            }

            row.Setup(
                role,
                roleConfig
            );

            roleRows.Add(row);
        }

        Debug.Log(
            "ROLE UI | Đã tạo " +
            roleRows.Count +
            " Role Row."
        );
    }

    private void ClearRoleRows()
    {
        for (int i = roleContent.childCount - 1; i >= 0; i--)
        {
            Destroy(
                roleContent.GetChild(i).gameObject
            );
        }

        roleRows.Clear();
    }

    private void RefreshAllUI()
    {
        RefreshAllRoleRows();
        RefreshRoleCount();
    }

    private void RefreshAllRoleRows()
    {
        for (int i = 0; i < roleRows.Count; i++)
        {
            if (roleRows[i] == null)
                continue;

            roleRows[i].RefreshAmount();
        }
    }

    private void RefreshRoleCount()
    {
        if (roleConfig == null)
            return;

        if (roleCountText == null)
            return;

        roleCountText.text =
            "ROLES: " +
            roleConfig.GetTotalRoleAmount();
    }
}