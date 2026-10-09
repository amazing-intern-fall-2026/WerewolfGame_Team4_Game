using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

public class RoleManager : MonoBehaviour
{
    public static RoleManager Instance;
    public ThuongRoleDefinitionCatalog roleDefinitions;

    [System.NonSerialized]
    public Dictionary<int, BaseRole> playerRoles = new Dictionary<int, BaseRole>();

    private void Awake()
    {
        Instance = this;
        playerRoles ??= new Dictionary<int, BaseRole>();
    }

    public void AssignRole()
    {
        AssignRole(Random.Range(0, int.MaxValue));
    }

    public void AssignRole(int seed)
    {
        if (roleDefinitions != null && !roleDefinitions.Validate(out string catalogError))
        { Debug.LogError(catalogError); return; }
        if (PlayerManager.Instance == null || PlayerManager.Instance.players == null || PlayerManager.Instance.players.Count == 0)
        {
            Debug.LogWarning("RoleManager: chưa có player nào để gán role.");
            return;
        }

        List<PlayerData> list = new List<PlayerData>(PlayerManager.Instance.players);
        list.RemoveAll(player => player == null);
        if (list.Count == 0)
        {
            Debug.LogWarning("[ROLE] Lobby chưa có người chơi hợp lệ để phân vai.");
            return;
        }
        list.Sort((a, b) => a.playerID.CompareTo(b.playerID));
        var ids = new HashSet<int>();
        foreach (PlayerData player in list)
        {
            if (ids.Add(player.playerID)) continue;
            Debug.LogError("[ROLE] Lobby có Player ID trùng: " + player.playerID);
            return;
        }

        Shuffle(list, new System.Random(unchecked(seed ^ 0x5f3759df)));
        playerRoles.Clear();

        List<RoleType> rolePool = RolePoolBuilder.Build(list.Count, seed);

        for (int i = 0; i < list.Count; i++)
        {
            list[i].votePower = 1;
            list[i].hasUseNightAction = false;
            list[i].hasVoted = false;
            list[i].ResetForNewMatch();
            list[i].roleDefinition = null;

            CreateRole(rolePool[i], list[i]);
            list[i].roleDefinition = roleDefinitions != null ? roleDefinitions.Find(rolePool[i]) : null;
            list[i].NotifyChanged();
        }

        Debug.Log("[ROLE] Random seed: " + seed);
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
        var configuredActor = PlayerManager.Instance?.GetplayerByID(playerID);
        if (UsesConfiguredAbility(configuredActor) && AbilitySystem.Instance != null)
        {
            var result = AbilitySystem.Instance.TryUseAbility(configuredActor,
                PlayerManager.Instance.GetplayerByID(targetID));
            feedback = result.Message;
            return result.IsValid;
        }
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
        if (PlayerManager.Instance == null || !PlayerManager.Instance.IsAlive(playerID))
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
        if (role.owner.HasEffect(StatusEffectType.Silenced))
        {
            feedback = "Người chơi đang bị Silenced.";
            return false;
        }
        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetID);
        var targetValidation = TargetValidator.ValidateLegacy(role.owner, target);
        if (!targetValidation.IsValid)
        {
            feedback = targetValidation.Message;
            return false;
        }

        if (!RoleActionRules.TryUseNightAbility(role, targetID, out feedback))
            return false;
        role.owner.hasUseNightAction = role is not FoxSpiritRole foxSpirit ||
                                       foxSpirit.HasUsedAllNightActions;
        return true;
    }

    public static bool UsesConfiguredAbility(PlayerData player) =>
        player?.roleDefinition != null && !player.roleDefinition.useLegacyAbility;

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

    public void AssignConfiguredRoles()
    {
        playerRoles ??= new Dictionary<int, BaseRole>();
        playerRoles.Clear();
        foreach (var player in PlayerManager.Instance.players)
        {
            if (player?.roleDefinition == null) continue;
            CreateRole(player.roleDefinition.roleType, player);
            player.faction = player.roleDefinition.faction;
            player.NotifyChanged();
        }
    }

    private void Shuffle(List<PlayerData> list, System.Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            PlayerData temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}

internal static class RoleAssignmentDebug
{
    public static void Log(List<PlayerData> lobbyPlayers, int lobbyCount,
        Dictionary<int, BaseRole> assignedRoles)
    {
        var message = new StringBuilder();
        message.AppendLine($"[ROLE] Lobby có {lobbyCount} người chơi. Đã phân ngẫu nhiên {assignedRoles.Count} Role:");

        foreach (PlayerData player in lobbyPlayers)
        {
            if (player == null || !assignedRoles.ContainsKey(player.playerID))
                continue;

            string name = string.IsNullOrWhiteSpace(player.playerName)
                ? $"Player {player.playerID + 1}" : player.playerName;
            message.AppendLine($"- {name} (ID {player.playerID}): {player.roleType}");
        }

        Debug.Log(message.ToString().TrimEnd());
    }
}
