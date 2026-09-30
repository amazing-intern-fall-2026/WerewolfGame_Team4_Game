using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkRoleLobbyConfig : NetworkBehaviour
{
    public static NetworkRoleLobbyConfig Instance;

    [Serializable]
    public class RoleAmount
    {
        public RoleType role;
        public int amount;
    }

    [Header("Default Role Setup")]
    [SerializeField]
    private List<RoleAmount> defaultRoles =
        new List<RoleAmount>
        {
            new RoleAmount
            {
                role = RoleType.Villager,
                amount = 2
            },

            new RoleAmount
            {
                role = RoleType.DogSpirit,
                amount = 1
            },

            new RoleAmount
            {
                role = RoleType.Hunter,
                amount = 1
            }
        };

    private readonly NetworkList<RoleAmountData> roleAmounts =
        new NetworkList<RoleAmountData>();

    // UI có thể đăng ký để nhận thông báo khi Role List thay đổi
    public event Action OnRoleConfigChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        roleAmounts.OnListChanged += OnRoleListChanged;

        if (IsServer)
        {
            InitializeDefaultRoles();
        }
    }

    public override void OnNetworkDespawn()
    {
        roleAmounts.OnListChanged -= OnRoleListChanged;
    }

    private void InitializeDefaultRoles()
    {
        roleAmounts.Clear();

        if (defaultRoles == null)
            return;

        foreach (RoleAmount roleAmount in defaultRoles)
        {
            if (roleAmount == null)
                continue;

            if (roleAmount.amount <= 0)
                continue;

            roleAmounts.Add(
                new RoleAmountData(
                    roleAmount.role,
                    roleAmount.amount
                )
            );
        }

        Debug.Log(
            "ROLE LOBBY CONFIG | Default roles initialized."
        );
    }

    public int GetRoleAmount(RoleType role)
    {
        for (int i = 0; i < roleAmounts.Count; i++)
        {
            if (roleAmounts[i].Role == role)
                return roleAmounts[i].Amount;
        }

        return 0;
    }

    public int GetTotalRoleAmount()
    {
        int total = 0;

        for (int i = 0; i < roleAmounts.Count; i++)
        {
            total += roleAmounts[i].Amount;
        }

        return total;
    }

    public void SetRoleAmount(
        RoleType role,
        int amount)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "ROLE LOBBY CONFIG | Chỉ Server được thay đổi Role."
            );

            return;
        }

        amount = Mathf.Max(0, amount);

        for (int i = 0; i < roleAmounts.Count; i++)
        {
            if (roleAmounts[i].Role != role)
                continue;

            RoleAmountData data =
                roleAmounts[i];

            data.Amount = amount;

            roleAmounts[i] = data;

            return;
        }

        if (amount > 0)
        {
            roleAmounts.Add(
                new RoleAmountData(
                    role,
                    amount
                )
            );
        }
    }

    public void AddRole(RoleType role)
    {
        if (!IsServer)
            return;

        int currentAmount =
            GetRoleAmount(role);

        SetRoleAmount(
            role,
            currentAmount + 1
        );
    }

    public void RemoveRole(RoleType role)
    {
        if (!IsServer)
            return;

        int currentAmount =
            GetRoleAmount(role);

        if (currentAmount <= 0)
            return;

        SetRoleAmount(
            role,
            currentAmount - 1
        );
    }

    public List<RoleAmountData> GetRoleAmounts()
    {
        List<RoleAmountData> result =
            new List<RoleAmountData>();

        for (int i = 0; i < roleAmounts.Count; i++)
        {
            result.Add(
                roleAmounts[i]
            );
        }

        return result;
    }

    private void OnRoleListChanged(
        NetworkListEvent<RoleAmountData> changeEvent)
    {
        Debug.Log(
            "ROLE LOBBY CONFIG | Role configuration changed."
        );

        // Báo cho UI Host + Client refresh
        OnRoleConfigChanged?.Invoke();
    }
}

public struct RoleAmountData :
    INetworkSerializable,
    IEquatable<RoleAmountData>
{
    public RoleType Role;
    public int Amount;

    public RoleAmountData(
        RoleType role,
        int amount)
    {
        Role = role;
        Amount = amount;
    }

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(
            ref Role
        );

        serializer.SerializeValue(
            ref Amount
        );
    }

    public bool Equals(
        RoleAmountData other)
    {
        return Role == other.Role
            && Amount == other.Amount;
    }
}