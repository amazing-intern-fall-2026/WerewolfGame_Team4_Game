using System.Collections.Generic;
using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class RoleManger : MonoBehaviour
{
    public static RoleManger Instance;

    [System.NonSerialized] public Dictionary<int, BaseRole> playerRoles =
        new Dictionary<int, BaseRole>();

    public bool RolesAssignedToPlayers { get; private set; }
    public event Action RolesAssigned;
    public event Action<int, string> AbilityResolved;


    private void Awake()
    {
        Instance = this;
        if (GetComponent<RoleAbilityUI>() == null)
            gameObject.AddComponent<RoleAbilityUI>();
    }


    public void AssignRole()
    {
        List<PlayerData> list =
            new List<PlayerData>(PlayerManger.Instance.players);

        Shuffle(list);
        playerRoles.Clear();
        RolesAssignedToPlayers = false;
        RoleType[] roles = { RoleType.DogSprit, RoleType.Mayor, RoleType.Seer, RoleType.VillageGuardian, RoleType.Idiot };
        for (int i = 0; i < list.Count; i++)
        {
            list[i].votPower = 1;
            CreateRole(i < roles.Length ? roles[i] : RoleType.Villager, list[i]);
        }

        RolesAssignedToPlayers = true;
        RolesAssigned?.Invoke();
    }

    public bool CanUseAbility(int playerID, LocalAbilityType abilityType)
    {
        PlayerData player = PlayerManger.Instance?.GetplayerByID(playerID);
        if (player == null || !player.isAlive || player.hasUseNightAction)
            return false;

        if (GameRoleManager.Instance != null && GameRoleManager.Instance.currentState != GameState.Nigt)
            return false;

        LocalRoleAbilityDefinition definition = LocalRoleAbilityCatalog.Get(player.roleType);
        return definition.type == abilityType && definition.requiresTarget && playerRoles.ContainsKey(playerID);
    }

    public bool TryUseAbility(int playerID, LocalAbilityType abilityType, int targetID, out string result)
    {
        result = "Không thể dùng chức năng này.";
        if (!CanUseAbility(playerID, abilityType))
            return false;

        PlayerData source = PlayerManger.Instance.GetplayerByID(playerID);
        PlayerData target = PlayerManger.Instance.GetplayerByID(targetID);
        if (target == null || !target.isAlive)
        {
            result = "Mục tiêu không hợp lệ.";
            return false;
        }

        if (source.playerID == target.playerID)
        {
            result = "Không thể chọn chính mình.";
            return false;
        }

        if (!playerRoles.TryGetValue(playerID, out BaseRole role))
            return false;

        result = role.UseNightAbility(targetID);
        if (string.IsNullOrEmpty(result))
        {
            result = "Không thể dùng chức năng lên mục tiêu này.";
            return false;
        }

        source.hasUseNightAction = true;
        AbilityResolved?.Invoke(playerID, result);
        return true;
    }

    public static string GetRoleDisplayName(RoleType roleType)
    {
        switch (roleType)
        {
            case RoleType.DogSprit: return "Dog Spirit";
            case RoleType.VillageGuardian: return "Village Guardian";
            case RoleType.Seer: return "Seer";
            case RoleType.Mayor: return "Mayor";
            case RoleType.Idiot: return "Idiot";
            case RoleType.Villager: return "Villager";
            default: return roleType.ToString();
        }
    }


    private void CreateRole(RoleType type, PlayerData player)
    {
        BaseRole role;

        switch (type)
        {
            case RoleType.DogSprit:
                role = new DogSpirit(player);
                break;

            case RoleType.Mayor:
                role = new MayorRole(player);
                break;

            case RoleType.Seer:
                role = new SeerRole(player);
                break;

            case RoleType.VillageGuardian:
                role = new GuardianRole(player);
                break;

            case RoleType.Idiot:
                role = new IdiotRole(player);
                break;

            case RoleType.Villager:
                role = new VillagerRole(player);
                break;

            default:
                Debug.LogError("Chưa có class xử lý role: " + type);
                return;
        }

        player.roleType = type;
        player.faction = role.faction;
        playerRoles[player.playerID] = role;

        role.OnGameStart();
    }


    private void Shuffle(List<PlayerData> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int random = Random.Range(i, list.Count);

            PlayerData temp = list[i];

            list[i] = list[random];
            list[random] = temp;
        }
    }
}
