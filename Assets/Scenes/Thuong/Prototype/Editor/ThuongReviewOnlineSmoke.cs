using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Thuong.Online;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class ThuongRoomUpgradeOnlineChecks
{
    [MenuItem("Tools/Thuong/Review Improvements/Check Online Smoke And UI")]
    public static void RunReviewSmoke()
    {
        SessionState.SetBool(RunKey + ".ReviewSmoke", true);
        Run();
    }

    private static IEnumerator ExerciseReviewSmoke()
    {
        lastStep = "review: starting production host";
        Set(host, "portText", Port.ToString()); Call(host, "HostLan");
        while (!Get<bool>(host, "connected")) yield return null;
        var rules = new ThuongRoomRules { preset = ThuongRolePreset.Standard, automaticTimes = false, day = 150,
            village = new ThuongVillageSettings { meetingLimit = 2, meetingCooldown = 25, sabotageCooldown = 70,
                blackoutSeconds = 30, jamSeconds = 25, barricadeSeconds = 9, lanternSeconds = 45, wardSeconds = 55 } };
        Check((bool)Call(host, "SetRoomRules", rules), "Review: host applies eight custom village settings");
        AddClient(0); AddClient(1);
        lastStep = "review: synchronizing two clients and village rules";
        while (Net(host).ConnectedClientsIds.Count != 3 || !(bool)Property(host, "AllMapsReady") ||
            clients.Any(client => client.RoomRules.village.wardSeconds != 55)) yield return null;
        Check(clients.All(client => JsonUtility.ToJson(client.RoomRules.village) == JsonUtility.ToJson(rules.village)),
            "Review: two actual transports decode all village settings identically");
        Check(!(bool)Call(clients[0], "SetRoomRules", new ThuongRoomRules()), "Review: client cannot alter host village rules");

        Call(host, "OpenRulesSettings"); Call(host, "OpenVillageRulesSettings");
        yield return null; Capture("review-village-rules");
        Get<GameObject>(host, "rulesSettingsRoot").SetActive(false);
        Call(host, "OpenVillagePractice"); yield return null; Capture("review-village-practice");
        var practice = Get<ThuongVillagePractice>(host, "villagePractice");
        practice.Begin(3, Time.unscaledTimeAsDouble); Call(host, "RefreshVillagePractice");
        yield return null; Capture("review-practice-inventory"); Call(host, "CloseVillagePractice");
        Check(!host.MatchStarted && TaskManager.Instance.progress == 0, "Review: practice leaves live match and task progress unchanged");

        NetworkGameRoleSetup.Save(new List<RoleAmountData> { new(RoleType.Hunter, 2), new(RoleType.DogSpirit, 1) });
        Call(host, "AssignLobbyRolesOnServer");
        while (clients.Any(client => !client.HasLocalRole)) yield return null;
        var actionType = typeof(ThuongOnlineSession).Assembly.GetType("Thuong.Online.ThuongOnlineAction");
        foreach (var member in Members())
        {
            Call(host, "ApplyServerAction", Get<ulong>(member, "ClientId"), 1u, Enum.Parse(actionType, "RevealRead"), -1, -1,
                GameRoleManager.Instance.currentDay, GameRoleManager.Instance.currentPhase, Get<uint>(host, "matchEpoch"));
            Call(host, "SetServerReady", Get<ulong>(member, "ClientId"), true);
        }
        Call(host, "StartMatchOnServer");
        while (clients.Any(client => !client.MatchStarted)) yield return null;
        Check(!(bool)Call(host, "SetRoomRules", new ThuongRoomRules()), "Review: village rules lock after match starts");
        Check(clients.All(client => !Get<bool>(client, "journalReady")), "Review: live clients receive no post-match journal");

        var hunterClient = clients.First(client => Get<RoleType>(client, "privateRole") == RoleType.Hunter);
        var notes = Get<List<string>>(hunterClient, "notebookEntries");
        Call(hunterClient, "BindNotebookToRound", hunterClient.MatchEpoch); notes.Add("Ghi chú giữ qua reconnect");
        var game = GameRoleManager.Instance;
        game.EndDay(); game.FinishNight(); game.StartVoting();
        int hunterId = hunterClient.LocalPlayerId;
        foreach (var player in PlayerManager.Instance.players)
            if (player.playerID != hunterId) Check(VoteManager.Instance.TryVote(player.playerID, hunterId), "Review: accepted vote for target Hunter");
        game.FinishVoting(); Call(host, "BroadcastSnapshot"); Call(host, "SendAllPrivateStates");
        lastStep = "review: client receives revenge window";
        while (!hunterClient.CanSetHunterTrap) yield return null;
        Check(game.IsHunterRevengeWindow && game.currentDay == 1 && !host.HasCompletedMatch, "Review: parity cannot end match before remote Hunter shot");
        Check(notes.Count == 1, "Review: ordinary phase updates retain personal notes");
        var wolf = PlayerManager.Instance.players.Single(player => player.roleType == RoleType.DogSpirit);
        Set(hunterClient, "requestId", 2u); // RevealRead above already consumed request 1 on the host.
        Call(hunterClient, "SendAction", Enum.Parse(actionType, "HunterTrap"), wolf.playerID, -1);
        lastStep = "review: remote Hunter shot and journal publication";
        while (!host.HasCompletedMatch || clients.Any(client => !client.HasCompletedMatch || !Get<bool>(client, "journalReady"))) yield return null;
        Check(!wolf.isAlive && game.currentDay == 1, "Review: authenticated remote shot resolves before final result without advancing day");
        int count = Get<ICollection>(host, "matchJournal").Count;
        Check(count > 0 && clients.All(client => Get<ICollection>(client, "matchJournal").Count == count), "Review: post-match journal chunks reassemble completely on both clients");
        string JournalText(ThuongOnlineSession session) => string.Join("\n", Get<IEnumerable>(session, "matchJournal").Cast<object>()
            .Select(entry => Get<string>(entry, "Text")));
        Check(clients.All(client => JournalText(client).Contains("phát bắn trả thù")), "Review: accepted winning Hunter shot is included in published journal");
        Check(clients.All(client => !(bool)Property(client, "CanConfigureRules")), "Review: result cannot reopen room editing");
        Set(host, "showJournal", true); Call(host, "RefreshEndgamePresentation");
        yield return null; Capture("review-journal-export");
        Check(Get<Button>(host, "journalTextExportButton").interactable && Get<Button>(host, "journalJsonExportButton").interactable,
            "Review: completed and received journal enables both export formats");
        NetworkGameRoleSetup.Clear();
    }
}
