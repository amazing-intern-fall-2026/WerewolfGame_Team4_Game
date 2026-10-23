using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Thuong.Maps;
using Thuong.Minigames;
using Thuong.Online;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Extends the actual-scene 25-transport scenario; two peers also use the production client readers.
public static class ThuongOnlineLogicChecks
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Key = "ThuongOnlineLogicChecks";
    private static ThuongOnlineSession host;
    private static NetworkManager[] peers;
    private static ThuongOnlineSession first, second;
    private static IEnumerator scenario;
    private static Func<bool> waiting;
    private static double waitDeadline;
    private static int step;
    private static Exception callbackError;
    private static int checks;
    private static readonly List<string> assertions = new();
    public static bool Enabled => SessionState.GetBool(Key, false);

    [MenuItem("Tools/Thuong/Online/Check Complete Logic With 25 Players")]
    public static void Run()
    {
        ThuongMinigameChecks.Run();
        SessionState.SetBool("ThuongMinigameOnlineChecks", true);
        SessionState.SetBool(Key, true);
        ThuongOnlineChecks.Run();
    }
    public static void Begin(ThuongOnlineSession session, NetworkManager[] clients)
    {
        host = session; peers = clients; checks = 0; assertions.Clear(); callbackError = null;
        first = ClientReaders(peers[0]); second = ClientReaders(peers[1]);
        scenario = Exercise(); waiting = null; step = 0;
    }
    public static bool Tick()
    {
        if (callbackError != null) throw new Exception("Client reader failed", callbackError);
        if (waiting != null && !waiting())
        {
            if (EditorApplication.timeSinceStartup > waitDeadline)
                throw new Exception($"Online logic wait {step} timed out after: {assertions.LastOrDefault()}");
            return false;
        }
        waiting = null;
        if (!scenario.MoveNext())
        {
            Directory.CreateDirectory("Logs/ThuongOnlineLogic");
            File.WriteAllLines("Logs/ThuongOnlineLogic/result.txt", new[] { $"PASS: {checks} online logic assertions; 25 live transports, 2 production client readers." }.Concat(assertions));
            Debug.Log($"THUONG_ONLINE_LOGIC_PASSED: {checks} assertions.");
            SessionState.SetBool(Key, false);
            return true;
        }
        waiting = scenario.Current as Func<bool>;
        step++; waitDeadline = EditorApplication.timeSinceStartup + 20;
        return false;
    }

    private static IEnumerator Exercise()
    {
        var players = PlayerManager.Instance; var roles = RoleManager.Instance;
        var game = GameRoleManager.Instance; var ability = AbilitySystem.Instance;
        var status = StatusEffectSystem.Instance; var votes = VoteManager.Instance;
        var night = NightManager.Instance; var deaths = DeathResolver.Instance;
        PlayerData P(int id) => players.GetplayerByID(id);
        foreach (var player in players.players)
        {
            player.ResetForNewMatch(); player.roleType = RoleType.Villager; player.faction = FactionType.Villager;
            player.roleDefinition = roles.roleDefinitions.Find(RoleType.Villager);
            roles.playerRoles[player.playerID] = new VillagerRole(player);
        }
        P(24).roleType = RoleType.DogSpirit; P(24).faction = FactionType.Monster;
        P(24).roleDefinition = roles.roleDefinitions.Find(RoleType.DogSpirit); roles.playerRoles[24] = new DogSpirit(P(24));
        var extra = ScriptableObject.CreateInstance<ThuongRoleDefinition>();
        extra.roleType = RoleType.Cursed; extra.abilityType = AbilityType.GrantExtraVote; extra.allowSelfTarget = true;
        extra.maxUsesPerPhase = 2; extra.effectStrength = 2; extra.effectTicksOnPhase = GamePhase.DayResolution;
        P(1).roleType = RoleType.Cursed; P(1).roleDefinition = extra; roles.playerRoles[1] = new CursedRole(P(1));
        P(2).roleType = RoleType.Shaman; P(2).roleDefinition = null; roles.playerRoles[2] = new ShamanRole(P(2));
        P(3).roleType = RoleType.Cursed; P(3).roleDefinition = null; roles.playerRoles[3] = new CursedRole(P(3));
        ability.SetMatchRunning(false); ability.SetMatchRunning(true); night.ResetForNewMatch();
        game.currentDay = 1; game.enabled = false; TaskManager.Instance.progress = 0;
        game.nightDuration = game.discussionDuration = game.votingDuration = 300;
        game.StartDay(); game.EndDay();
        SendState();
        yield return (Func<bool>)(() => first.HasLocalRole && second.HasLocalRole && first.VisibleState == GameState.Night);
        Require(game.HasIntegratedLogic && game.statusEffects == status && game.abilities == ability, "Online scene owns all logic references");
        Require(!NetworkGameOverManager.Instance.enabled, "Legacy global game over stays suspended by Thuong only");
        Require(first.HasActiveLocalAbility && first.CanUseLocalAbility(out _), "Generic configured ability is available on a remote peer");
        Require(first.CanTargetLocalAbility(1) && !host.CanTargetLocalAbility(0), "Private target metadata respects self-target configuration");
        first.RequestAbility(1);
        yield return (Func<bool>)(() => night.PendingActionCount == 1);
        SendState();
        yield return (Func<bool>)(() => first.CanUseLocalAbility(out _));
        Require(first.CanUseLocalAbility(out _), "Remote multi-use ability stays available after its first use");
        first.RequestAbility(2);
        yield return (Func<bool>)(() => night.PendingActionCount == 2);
        SendState();
        yield return (Func<bool>)(() => !first.CanUseLocalAbility(out _));
        Require(!first.CanUseLocalAbility(out _) && night.PendingActionCount == 2, "Remote exhausted ability is locked");
        first.RequestAbility(4);
        yield return null; yield return null;
        Require(night.PendingActionCount == 2, "Server refuses an extra action from the same peer");

        status.ApplyEffect(P(2), StatusEffectType.Silenced, 1, 1, GamePhase.Discussion);
        SendState();
        yield return (Func<bool>)(() => second.LocalStatusSummary.Contains("Khóa kỹ năng"));
        Require(!second.CanUseLocalAbility(out _), "Silenced reaches the production client and locks abilities");
        second.RequestAbility(24, shaman: true, goodCharm: false);
        yield return null; yield return null;
        Require(((ShamanRole)roles.playerRoles[2]).HasBadCharm, "Rejected Shaman request does not consume its inventory");
        status.RemoveEffect(P(2), StatusEffectType.Silenced); SendState();
        yield return (Func<bool>)(() => second.CanUseLocalAbility(out _));
        second.RequestAbility(24, shaman: true, goodCharm: false);
        yield return (Func<bool>)(() => P(24).HasEffect(StatusEffectType.Silenced));
        Require(!roles.UseNightAbility(24, 5, out _), "Shaman bridge blocks wolf abilities without modifying Nhat roles");
        Require(roles.UseNightAbility(3, 5, out _) && P(5).isCursed, "Legacy Cursed is adapted inside Thuong");
        game.FinishNight(); SendState();
        yield return (Func<bool>)(() => first.LocalVoteWeight == 3 && second.LocalVoteWeight == 3);
        Require(first.LocalStatusSummary.Contains("Thêm phiếu") && second.LocalStatusSummary.Contains("Thêm phiếu"), "Independent private states contain their own Extra Vote");
        Require(P(1).votePower == 1 && P(2).votePower == 1, "Temporary votes never change base vote power");
        Require(P(5).isAlive && P(5).effects.Single(effect => effect.Type == StatusEffectType.Cursed).RemainingTicks == 1,
            "Curse counts its first discussion without killing early");
        game.StartVoting();
        status.ApplyEffect(P(1), StatusEffectType.CannotVote, 1, 1, GamePhase.DayResolution);
        SendState();
        yield return (Func<bool>)(() => first.VisibleState == GameState.Voting && first.LocalVoteWeight == 0 && second.VisibleState == GameState.Voting);
        Require(!first.CanVoteFor(24) && first.LocalStatusSummary.Contains("Cấm bỏ phiếu"), "Cannot Vote is visible and disables remote voting");
        first.RequestVote(24); second.RequestVote(6);
        yield return (Func<bool>)(() => P(2).hasVoted);
        Require(!P(1).hasVoted && votes.GetVotedTarget(1) == -1, "Blocked peer cannot submit a ballot to the server");
        Require(votes.TryVote(3, 7), "A ballot can be recorded before its caster is blocked");
        status.ApplyEffect(P(3), StatusEffectType.CannotVote, 1, 1, GamePhase.DayResolution);
        game.FinishVoting(); SendState();
        Require(votes.LastResolution.Tallies.Count == 1 && votes.LastResolution.Tallies[6] == 3 &&
            votes.LastResolution.Eliminated?.playerID == 6, "Authoritative tally adds Extra Vote and removes a newly blocked ballot");
        yield return (Func<bool>)(() => first.LocalVoteWeight == 1 && second.LocalVoteWeight == 1 && !first.LocalStatusSummary.Contains("Thêm phiếu"));
        Require(first.LocalVoteWeight == 1 && first.LocalStatusSummary == "", "Effects expire after counting and the client returns to its base vote");
        game.EndDay(); game.FinishNight(); SendState();
        Require(!P(5).isAlive && P(5).lastDeathCause == DeathCause.Curse && P(5).lastDeathSourceID == 3,
            "Curse expires through the central death resolver with its source");
        Require(P(6).lastDeathCause == DeathCause.Vote, "Vote death has its own recorded cause");

        deaths.TryKill(new DeathRequest(P(24), DeathCause.Ability, P(2))); SendState();
        yield return (Func<bool>)(() => Field<object>(first, "matchResult") != null && Field<object>(second, "matchResult") != null);
        Require(game.Winner == "Villagers" && !ability.MatchIsRunning && !Field<bool>(DayTimer.Instance, "running"),
            "Online victory freezes abilities and timers after the complete death chain");
        Require(Cause(first, 5) == DeathCause.Curse && Cause(second, 6) == DeathCause.Vote && Cause(first, 24) == DeathCause.Ability,
            "Both production client readers preserve distinct final death causes");
        Require(!second.CanRestartMatch && !second.RestartMatchInRoom(), "A client cannot restart the host's match");
        int aliveBefore = players.GetAlivePlayers().Count(); first.RequestVote(4); first.RequestAbility(4);
        yield return null; yield return null;
        Require(players.GetAlivePlayers().Count() == aliveBefore && game.currentState == GameState.GameOver,
            "Late gameplay requests cannot change a finished result");
        Call(host, "RefreshEndgamePresentation");
        Require(Field<Button>(host, "outcomeRestartButton").gameObject.activeSelf, "Host result UI exposes the restart button");
        Directory.CreateDirectory("Logs/ThuongOnlineLogic");
        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            ThuongMedievalSceneChecks.SavePreview("Logs/ThuongOnlineLogic/online-result.png");
        uint previousEpoch = Field<uint>(host, "matchEpoch");
        Field<Button>(host, "outcomeRestartButton").onClick.Invoke();
        yield return (Func<bool>)(() => !first.RolesAssigned && !second.RolesAssigned && Field<object>(first, "matchResult") == null);
        Require(host.GetComponent<NetworkManager>().ConnectedClientsIds.Count == 25 && !host.MatchStarted && !host.RolesAssigned,
            "Restart keeps all 25 connections and returns to role assignment");
        Require(!first.HasLocalRole && !second.HasLocalRole && first.LocalStatusSummary == "" && second.LocalStatusSummary == "",
            "Restart clears all private roles results and status effects on clients");
        Require(Field<uint>(host, "matchEpoch") > previousEpoch && night.PendingActionCount == 0,
            "A fresh match epoch invalidates queued actions from the old match");
        Require(Members().All(member => !Field<bool>(member, "Ready") && !Field<bool>(member, "RevealRead")),
            "Restart requires every member to read and ready again");
        Call(host, "AssignLobbyRolesOnServer");
        yield return (Func<bool>)(() => first.HasLocalRole && second.HasLocalRole);
        first.ViewLocalRole(); first.ReadLocalRole(); second.ViewLocalRole(); second.ReadLocalRole();
        host.ViewLocalRole(); host.ReadLocalRole();
        foreach (var peer in peers.Skip(2)) SendRaw(peer, 6000, 6, -1, Field<uint>(host, "matchEpoch"));
        yield return (Func<bool>)(() => Members().All(member => Field<bool>(member, "RevealRead")));
        foreach (var peer in peers) SendReady(peer);
        Field<Button>(host, "lobbyReadyButton").onClick.Invoke();
        yield return (Func<bool>)(() => Members().All(member => Field<bool>(member, "Ready")));
        Call(host, "StartMatchOnServer"); DayTimer.Instance.StopTimer(); game.enabled = false;
        yield return (Func<bool>)(() => first.MatchStarted && second.MatchStarted);
        Require(host.MatchStarted && game.currentState == GameState.Day && players.players.All(player => player.isAlive &&
            !player.hasDeathRecord && !player.wasVotedOut && player.effects.Count == 0), "Second match starts clean in the same room");
        using (var writer = new FastBufferWriter(64, Allocator.Temp))
        {
            writer.WriteValueSafe((uint)9000); writer.WriteValueSafe(previousEpoch); writer.WriteValueSafe((byte)0);
            writer.WriteValueSafe(game.currentDay); writer.WriteValueSafe(0); writer.WriteValueSafe(0UL);
            writer.WriteValueSafe((byte)0); writer.WriteValueSafe(0); writer.WriteValueSafe(0f);
            peers[0].CustomMessagingManager.SendNamedMessage(ThuongMinigameWire.Request, 0, writer);
        }
        SendRaw(peers[0], 9000, 1, 5, previousEpoch);
        yield return (Func<bool>)(() => Field<IDictionary>(host, "privateFeedback").Contains(1) &&
            ((string)Field<IDictionary>(host, "privateFeedback")[1]).Contains("ván cũ"));
        Require(night.PendingActionCount == 0 && players.players.All(player => player.isAlive), "Old-epoch network request cannot act on the second match");
        Require(!Field<IDictionary>(host, "minigameSequences").Contains(peers[0].LocalClientId) &&
            Field<ThuongMinigameAuthority>(host, "minigameAuthority").Get(peers[0].LocalClientId) == null,
            "Old-epoch minigame request cannot start a task or consume a new-match sequence");
        var neutralRule = ScriptableObject.CreateInstance<NeutralSurvivalWinRule>();
        neutralRule.maximumOtherSurvivors = 24; neutralRule.requireNoWolves = neutralRule.requireNoOpposingKillers = false;
        neutralRule.winnerLabel = "Người sống sót";
        var neutralDefinition = ScriptableObject.CreateInstance<ThuongRoleDefinition>(); neutralDefinition.neutralWinRule = neutralRule;
        P(23).faction = FactionType.Neutral; P(23).roleDefinition = neutralDefinition;
        P(23).NotifyChanged();
        yield return (Func<bool>)(() => Field<object>(first, "matchResult") != null && Field<object>(second, "matchResult") != null);
        Require(game.Winner == neutralRule.winnerLabel && (string)Field<object>(first, "matchResult").GetType()
            .GetProperty("WinnerId").GetValue(Field<object>(first, "matchResult")) == neutralRule.winnerLabel,
            "A custom neutral win asset is evaluated by the host and its label reaches clients");
        var finalPlayers = (IEnumerable)Field<object>(second, "matchResult").GetType().GetProperty("Players")
            .GetValue(Field<object>(second, "matchResult"));
        Require(finalPlayers.Cast<object>().Where(player => Field<bool>(player, "Won")).Select(player => Field<int>(player, "Id"))
            .SequenceEqual(new[] { 23 }), "Custom neutral victory marks only qualified neutral players as winners");
        Require(host.RestartMatchInRoom(), "A custom neutral result can also return to the same-room lobby");
        Call(host, "AssignLobbyRolesOnServer");
        yield return (Func<bool>)(() => first.HasLocalRole && second.HasLocalRole);
        UnityEngine.Object.Destroy(neutralDefinition); UnityEngine.Object.Destroy(neutralRule);
        UnityEngine.Object.Destroy(extra);
    }

    private static ThuongOnlineSession ClientReaders(NetworkManager peer)
    {
        var root = new GameObject("Inactive production reader " + peer.LocalClientId); root.SetActive(false);
        var proxy = root.AddComponent<ThuongOnlineSession>();
        var players = root.AddComponent<PlayerManager>(); var game = root.AddComponent<GameRoleManager>();
        var roles = root.AddComponent<RoleManager>(); roles.roleDefinitions = RoleManager.Instance.roleDefinitions;
        var tasks = root.AddComponent<TaskManager>();
        Set(proxy, "network", peer); Set(proxy, "transport", peer.GetComponent<UnityTransport>());
        Set(proxy, "game", game); Set(proxy, "players", players); Set(proxy, "roles", roles); Set(proxy, "tasks", tasks);
        Set(proxy, "connected", true); Set(proxy, "requestId", (uint)1000);
        Set(proxy, "mapCatalog", Resources.Load<ThuongMapCatalog>(ThuongMapCatalog.ResourcePath));
        Set(proxy, "eventCatalog", Resources.Load<ThuongEventPresentationCatalog>(ThuongEventPresentationCatalog.ResourcePath));
        Set(proxy, "mapRuntime", root.AddComponent<ThuongMapRuntime>());
        Set(proxy, "eventPresentation", root.AddComponent<ThuongEventPresentation>());
        void Handle(string method, ulong sender, FastBufferReader reader)
        { try { Call(proxy, method, sender, reader); } catch (Exception error) { callbackError = error; } }
        peer.CustomMessagingManager.RegisterNamedMessageHandler("thuong.online.private.v4", (sender, reader) => Handle("ReceivePrivateState", sender, reader));
        peer.CustomMessagingManager.RegisterNamedMessageHandler("thuong.online.snapshot.v6", (sender, reader) => Handle("ReceiveSnapshot", sender, reader));
        peer.CustomMessagingManager.RegisterNamedMessageHandler("thuong.online.roster.v3", (sender, reader) => Handle("ReceiveRoster", sender, reader));
        return proxy;
    }
    private static IEnumerable<object> Members() => Field<IEnumerable>(host, "serverPlayers").Cast<object>()
        .Select(pair => pair.GetType().GetProperty("Value").GetValue(pair));
    private static DeathCause Cause(ThuongOnlineSession peer, int playerID)
    {
        var result = Field<object>(peer, "matchResult");
        var players = (IEnumerable)result.GetType().GetProperty("Players").GetValue(result);
        var player = players.Cast<object>().Single(value => Field<int>(value, "Id") == playerID);
        Require(Field<bool>(player, "HasDeathRecord"), "Final player contains a death record");
        return Field<DeathCause>(player, "DeathCause");
    }
    private static void SendState() { Call(host, "SendAllPrivateStates"); Call(host, "BroadcastSnapshot"); }
    private static void SendReady(NetworkManager peer)
    {
        using var writer = new FastBufferWriter(4, Allocator.Temp); writer.WriteValueSafe(true);
        peer.CustomMessagingManager.SendNamedMessage("thuong.online.ready.v1", 0, writer);
    }
    private static void SendRaw(NetworkManager peer, uint id, byte action, int target, uint epoch)
    {
        using var writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe(id); writer.WriteValueSafe(epoch); writer.WriteValueSafe(action);
        writer.WriteValueSafe(target); writer.WriteValueSafe(-1); writer.WriteValueSafe(GameRoleManager.Instance.currentDay);
        writer.WriteValueSafe((byte)GameRoleManager.Instance.currentPhase);
        peer.CustomMessagingManager.SendNamedMessage("thuong.online.action.v3", 0, writer);
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    private static void Require(bool valid, string message)
    { if (!valid) throw new Exception("Online logic check failed: " + message); checks++; assertions.Add(message); }
}
