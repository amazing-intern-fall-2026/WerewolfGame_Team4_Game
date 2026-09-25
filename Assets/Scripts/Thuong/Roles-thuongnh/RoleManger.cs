using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class RoleManger : MonoBehaviour
{
    public static RoleManger Instance;

    [System.NonSerialized]
    public Dictionary<int, BaseRole> playerRoles = new Dictionary<int, BaseRole>();

    private void Awake()
    {
        Instance = this;
    }

    public void AssignRole()
    {
        if (PlayerManager.Instance == null || PlayerManager.Instance.players == null || PlayerManager.Instance.players.Count == 0)
        {
            Debug.LogWarning("RoleManger: chưa có player nào để gán role.");
            return;
        }

        List<PlayerData> list = new List<PlayerData>(PlayerManager.Instance.players);
        list.RemoveAll(player => player == null);
        if (list.Count == 0)
        {
            Debug.LogWarning("[ROLE] Lobby chưa có người chơi hợp lệ để phân vai.");
            return;
        }
        Shuffle(list);
        playerRoles.Clear();

        List<RoleType> rolePool = BuildRolePool(list.Count);

        for (int i = 0; i < list.Count; i++)
        {
            list[i].votePower = 1;
            list[i].hasUseNightAction = false;
            list[i].hasVoted = false;
            list[i].isAlive = true;

            RoleType roleType = i < rolePool.Count ? rolePool[i] : RoleType.Villager;
            CreateRole(roleType, list[i]);
        }

        RoleAssignmentDebug.Log(PlayerManager.Instance.players, list.Count, playerRoles);
    }

    private List<RoleType> BuildRolePool(int playerCount)
    {
        var roles = new List<RoleType>();

        if (playerCount >= 5 && playerCount <= 8)
        {
            roles.Add(RoleType.DogSpirit);
            roles.Add(RoleType.Villager);
            roles.Add(RoleType.Villager);
            roles.Add(RoleType.Villager);
            roles.Add(RoleType.Seer);

            while (roles.Count < playerCount)
                roles.Add(RoleType.Villager);

            return roles;
        }

        if (playerCount == 12)
        {
            roles.Add(RoleType.DogSpirit);
            roles.Add(RoleType.SerpentSpirit);
            roles.Add(RoleType.Ogre);
            roles.Add(RoleType.Villager);
            roles.Add(RoleType.Villager);
            roles.Add(RoleType.Villager);
            roles.Add(RoleType.Villager);
            roles.Add(RoleType.Mayor);
            roles.Add(RoleType.Seer);
            roles.Add(RoleType.VillageGuardian);
            roles.Add(RoleType.Shaman);
            roles.Add(RoleType.WhiteHound);
            return roles;
        }

        roles.Add(RoleType.DogSpirit);
        roles.Add(RoleType.Seer);

        for (int i = 2; i < playerCount; i++)
            roles.Add(RoleType.Villager);

        return roles;
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
        NotifyAliveRoles(role => role.OnVoteStart());
    }

    public bool UseNightAbility(int playerID, int targetID)
    {
        return UseNightAbility(playerID, targetID, out _);
    }

    public bool UseNightAbility(int playerID, int targetID, out string feedback)
    {
        if (GameRoleManager.Instance == null || GameRoleManager.Instance.currentState != GameState.Night)
        {
            feedback = "Chỉ có thể dùng kỹ năng vào ban đêm.";
            return false;
        }
        if (!playerRoles.TryGetValue(playerID, out BaseRole role) ||
            role.owner == null || role.owner.playerID != playerID)
        {
            feedback = "Người chơi chưa được gán Role hợp lệ.";
            return false;
        }
        if (!role.owner.isAlive)
        {
            feedback = "Người chơi đã bị loại.";
            return false;
        }
        if (!role.HasNightAbility)
        {
            feedback = "Role này không có kỹ năng chủ động ban đêm.";
            return false;
        }
        if (role.owner.hasUseNightAction)
        {
            feedback = "Kỹ năng đã được dùng trong đêm này.";
            return false;
        }
        if (playerID == targetID)
        {
            feedback = "Không thể chọn chính mình.";
            return false;
        }

        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetID);
        if (target == null || !target.isAlive)
        {
            feedback = "Mục tiêu không tồn tại hoặc đã bị loại.";
            return false;
        }

        if (!role.TryUseNightAbility(targetID, out feedback))
            return false;
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
            case RoleType.DogSpirit:
                role = new DogSpirit(player);
                break;

            case RoleType.WhiteHound:
            case RoleType.WhiteWolf:
            case RoleType.RedNosedHound:
            case RoleType.WolfCub:
            case RoleType.WolfBoss:
                role = new WhiteHound(player);
                break;

            case RoleType.SerpentSpirit:
                role = new SerpentSpirit(player);
                break;

            case RoleType.Ogre:
                role = new Ogre(player);
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

            case RoleType.Hunter:
                role = new HunterRole(player);
                break;

            case RoleType.Shaman:
                role = new ShamanRole(player);
                break;

            case RoleType.WeaverOfFate:
                role = new WeaverOfFateRole(player);
                break;

            case RoleType.Idiot:
                role = new IdiotRole(player);
                break;

            case RoleType.Cursed:
                role = new CursedRole(player);
                break;

            case RoleType.Brat:
                role = new BratRole(player);
                break;

            case RoleType.Madman:
            case RoleType.Jester:
            case RoleType.Lover:
                role = new MadmanRole(player);
                break;

            case RoleType.FoxSpirit:
            case RoleType.Piper:
                role = new FoxSpiritRole(player);
                break;

            case RoleType.Killer:
            case RoleType.SerialKiller:
                role = new KillerRole(player);
                break;

            case RoleType.TuongMaster:
            case RoleType.Magistrate:
            case RoleType.DeathHerald:
                role = new VillagerRole(player);
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

