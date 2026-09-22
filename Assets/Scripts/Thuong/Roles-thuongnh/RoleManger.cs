using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class RoleManger : MonoBehaviour
{
    public static RoleManger Instance;

    [System.NonSerialized]
    public Dictionary<int, BaseRole> playerRoles =
        new Dictionary<int, BaseRole>();


    private void Awake()
    {
        Instance = this;
    }


    public void AssignRole()
    {
        List<PlayerData> list =
            new List<PlayerData>(PlayerManger.Instance.players);

        Shuffle(list);
        playerRoles.Clear();
        RoleType[] roles = { RoleType.DogSprit, RoleType.Mayor, RoleType.Seer, RoleType.VillageGuardian, RoleType.Idiot, RoleType.SerpentSpirit };
        for (int i = 0; i < list.Count; i++)
        {
            list[i].votPower = 1;
            CreateRole(i < roles.Length ? roles[i] : RoleType.Villager, list[i]);
        }
        foreach (var pair in playerRoles)
        {
            Debug.Log(
                "ROLE ASSIGN | Player "
                + pair.Key
                + " → "
                + pair.Value.roleType
            );
        }
    }

    public void NotifyDayStart()
    {
        NotifyAliveRoles(role => role.OnDayStart());
    }

    public void NotifyNightStart()
    {
        NotifyAliveRoles(role => role.OnNightStart());
    }

    public void NotifyVoteStart()
    {
        NotifyAliveRoles(role => role.OnVotStart());
    }

    public bool UseNightAbility(int playerID, int targetID)
    {
        if (GameRoleManager.Instance == null ||
            GameRoleManager.Instance.currentState != GameState.Night ||
            !playerRoles.TryGetValue(playerID, out BaseRole role) ||
            role.owner == null || !role.owner.isAlive ||
            role.owner.hasUseNightAction)
            return false;

        PlayerData target = PlayerManger.Instance?.GetplayerByID(targetID);
        if (target == null || !target.isAlive)
            return false;

        role.UseNightAbility(targetID);
        role.owner.hasUseNightAction = true;
        return true;
    }

    private void NotifyAliveRoles(System.Action<BaseRole> callback)
    {
        foreach (BaseRole role in playerRoles.Values)
        {
            if (role?.owner != null && role.owner.isAlive)
                callback(role);
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

            case RoleType.SerpentSpirit:
                role = new SerpentSpirit(player);
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
