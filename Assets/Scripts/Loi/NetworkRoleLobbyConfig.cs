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

    private bool hasCustomizedRoles = false;
    public bool HasCustomizedRoles => hasCustomizedRoles;

    public void ResetCustomization()
    {
        hasCustomizedRoles = false;
    }

    private void InitializeDefaultRoles()
    {
        int count = LobbyManager.CurrentPlayerCount;
        if (count > 0)
        {
            InitializeDefaultRolesForPlayerCount(count);
            return;
        }

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

    public void InitializeDefaultRolesForPlayerCount(int playerCount)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
            return;

        if (hasCustomizedRoles)
            return;

        roleAmounts.Clear();
        playerCount = Mathf.Max(1, playerCount);

        if (playerCount == 1)
        {
            roleAmounts.Add(new RoleAmountData(RoleType.Villager, 1));
        }
        else if (playerCount == 2)
        {
            roleAmounts.Add(new RoleAmountData(RoleType.DogSpirit, 1));
            roleAmounts.Add(new RoleAmountData(RoleType.Villager, 1));
        }
        else if (playerCount == 3)
        {
            // Phòng 3 người: 1 Sói (DogSpirit), 1 Dân (Villager), 1 Thợ săn (Hunter)
            roleAmounts.Add(new RoleAmountData(RoleType.DogSpirit, 1));
            roleAmounts.Add(new RoleAmountData(RoleType.Villager, 1));
            roleAmounts.Add(new RoleAmountData(RoleType.Hunter, 1));
        }
        else if (playerCount == 4)
        {
            // Phòng 4 người: 1 Sói (DogSpirit), 2 Dân (Villager), 1 Thợ săn (Hunter)
            roleAmounts.Add(new RoleAmountData(RoleType.DogSpirit, 1));
            roleAmounts.Add(new RoleAmountData(RoleType.Villager, 2));
            roleAmounts.Add(new RoleAmountData(RoleType.Hunter, 1));
        }
        else if (playerCount == 5)
        {
            // Phòng 5 người: 1 Sói, 2 Dân, 1 Thợ săn, 1 Tiên tri
            roleAmounts.Add(new RoleAmountData(RoleType.DogSpirit, 1));
            roleAmounts.Add(new RoleAmountData(RoleType.Villager, 2));
            roleAmounts.Add(new RoleAmountData(RoleType.Hunter, 1));
            roleAmounts.Add(new RoleAmountData(RoleType.Seer, 1));
        }
        else
        {
            int wolves = Mathf.Max(1, playerCount / 4);
            int specials = 3;
            int villagers = Mathf.Max(1, playerCount - wolves - specials);

            roleAmounts.Add(new RoleAmountData(RoleType.DogSpirit, wolves));
            roleAmounts.Add(new RoleAmountData(RoleType.Hunter, 1));
            roleAmounts.Add(new RoleAmountData(RoleType.Seer, 1));
            roleAmounts.Add(new RoleAmountData(RoleType.VillageGuardian, 1));
            roleAmounts.Add(new RoleAmountData(RoleType.Villager, villagers));
        }

        Debug.Log(
            $"ROLE LOBBY CONFIG | Đã tự động thiết lập vai mặc định cho {playerCount} Player. Total Roles = {GetTotalRoleAmount()}"
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
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
        {
            Debug.LogWarning(
                "ROLE LOBBY CONFIG | Chỉ Server được thay đổi Role."
            );

            return;
        }

        hasCustomizedRoles = true;
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
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
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
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
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