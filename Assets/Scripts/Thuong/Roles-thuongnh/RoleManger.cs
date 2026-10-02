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

        List<RoleType> rolePool = RolePoolBuilder.Build(list.Count);

        for (int i = 0; i < list.Count; i++)
        {
            list[i].votePower = 1;
            list[i].hasUseNightAction = false;
            list[i].hasVoted = false;
            list[i].isAlive = true;

            CreateRole(rolePool[i], list[i]);
        }

        RoleAssignmentDebug.Log(PlayerManager.Instance.players, list.Count, playerRoles);
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

        if (!RoleActionRules.TryUseNightAbility(role, targetID, out feedback))
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
        BaseRole role = RoleCatalog.Create(type, player);

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

