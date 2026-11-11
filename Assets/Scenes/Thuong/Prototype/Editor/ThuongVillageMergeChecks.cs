using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Thuong.Online;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

// Explicit merge checks: no InitializeOnLoad hook and no player build.
public static class ThuongVillageMergeChecks
{
    private static readonly List<string> passed = new();
    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        passed.Add(description);
    }

    [MenuItem("Tools/Thuong/Village/Check Merge Rules")]
    public static void Run()
    {
        passed.Clear();
        ThuongOnlineRoleConfigTestExecution.Run();
        ThuongOnlineAuthorityChecks.Run();
        CheckMeetings();
        CheckSabotage();
        CheckPair();
        CheckItems();
        CheckLobbyVersion();
        Directory.CreateDirectory("Logs/VillageMerge20261111");
        File.WriteAllLines("Logs/VillageMerge20261111/village-results.txt",
            new[] { "PASS checks=" + passed.Count }.Concat(passed));
        Debug.Log("THUONG_VILLAGE_MERGE_PASSED checks=" + passed.Count);
    }

    private static void CheckMeetings()
    {
        var rules = new ThuongVillageAuthority(); rules.Reset(0);
        Require(!rules.CanMeet(0, 14, true, true, true, false, out _), "Opening meeting grace cannot be skipped");
        Require(!rules.CanMeet(0, 15, false, true, true, false, out _), "Dead or disconnected caller is rejected");
        Require(!rules.CanMeet(0, 15, true, false, true, false, out _), "Night meeting is rejected");
        Require(!rules.CanMeet(0, 15, true, true, false, false, out _), "Bell requires proximity");
        Require(!rules.CanMeet(0, 15, true, true, false, true, out _), "Remote meeting requires an alarm item");
        Require(rules.CanMeet(0, 15, true, true, true, false, out _), "Live caller at bell can meet after grace");
        rules.CommitMeeting(0, 15, false);
        Require(rules.MeetingsUsed == 1 && rules.HasRungBell(0), "Meeting consumes personal and room quota");
        Require(!rules.CanMeet(0, 60, true, true, true, false, out _), "Personal meeting cannot be repeated");
        Require(!rules.CanMeet(1, 59, true, true, true, false, out _), "Room meeting cooldown is enforced");
        Require(rules.CanMeet(1, 60, true, true, true, false, out _), "Another caller may meet after cooldown");
        rules.CommitMeeting(1, 60, false);
        Require(rules.CanMeet(2, 105, true, true, true, false, out _), "Third room meeting is available");
        rules.CommitMeeting(2, 105, false);
        Require(!rules.CanMeet(3, 150, true, true, true, false, out _), "Fourth room meeting is rejected");
        rules.Reset(300);
        Require(rules.MeetingsUsed == 0 && !rules.HasRungBell(0), "New round resets all meeting quotas");
        Require(rules.GrantTaskReward(1, 1, out var alarm) && alarm == ThuongItemKind.Alarm, "Alarm reward setup");
        Require(rules.CanMeet(1, 315, true, true, false, true, out _), "Alarm permits a remote meeting");
        rules.CommitMeeting(1, 315, true);
        Require(rules.Inventory(1).Count == 0 && rules.HasRungBell(1) && rules.MeetingsUsed == 1,
            "Remote meeting consumes item and the same personal quota");
    }

    private static void CheckSabotage()
    {
        var rules = new ThuongVillageAuthority(); rules.Reset(0);
        Require(!rules.TrySabotage(0, 1, 15, false, true, ThuongSabotageKind.Blackout, 0, true, out _), "Non-wolf sabotage is rejected");
        Require(!rules.TrySabotage(0, 1, 15, true, false, ThuongSabotageKind.Blackout, 0, true, out _), "Night sabotage is rejected");
        Require(!rules.TrySabotage(0, 1, 15, true, true, (ThuongSabotageKind)99, 0, true, out _), "Unknown sabotage is rejected");
        Require(!rules.TrySabotage(0, 1, 15, true, true, ThuongSabotageKind.Blackout, 0, false, out _), "Invalid target is rejected");
        Require(!rules.TrySabotage(0, 1, 14, true, true, ThuongSabotageKind.Blackout, 0, true, out _), "Opening sabotage grace is enforced");
        Require(rules.TrySabotage(0, 1, 15, true, true, ThuongSabotageKind.Blackout, 0, true, out _), "Wolf may activate blackout");
        Require(!rules.CanMeet(1, 15, true, true, true, false, out _), "Active sabotage blocks emergency meeting");
        Require(!rules.TrySabotage(2, 1, 16, true, true, ThuongSabotageKind.Barricade, 0, true, out _), "Sabotages cannot overlap");
        Require(!rules.TryRepair(1, 16, false, out _), "Repair validates eligibility and location");
        Require(rules.TryRepair(1, 16, true, out _), "Eligible repair begins");
        Require(!rules.TryRepair(2, 16, true, out _), "A repair has only one owner");
        rules.Tick(1, 18, true, (_, _) => true, _ => true, out _, out _, out bool repaired);
        Require(!repaired && rules.Sabotage == ThuongSabotageKind.Blackout, "Repair cannot finish before three seconds");
        rules.Tick(1, 19, true, (_, _) => true, _ => true, out _, out _, out repaired);
        Require(repaired && rules.Sabotage == ThuongSabotageKind.None, "Three-second repair clears sabotage");
        Require(!rules.TrySabotage(0, 1, 60, true, true, ThuongSabotageKind.StationJam, 0, true, out _), "Wolf team cannot repeat sabotage in the same day");
        Require(rules.TrySabotage(0, 2, 60, true, true, ThuongSabotageKind.StationJam, 0, true, out _), "Next day permits station jam after cooldown");
        rules.Tick(2, 81, true, (_, _) => true, _ => true, out _, out _, out _);
        Require(rules.Sabotage == ThuongSabotageKind.None, "Unrepaired station jam expires");
        Require(rules.TrySabotage(0, 3, 105, true, true, ThuongSabotageKind.Barricade, 0, true, out _), "Barricade is available next day");
        rules.Tick(3, 113, true, (_, _) => true, _ => true, out _, out _, out _);
        Require(rules.Sabotage == ThuongSabotageKind.None, "Barricade expires after eight seconds");
    }

    private static void CheckPair()
    {
        var rules = new ThuongVillageAuthority(); rules.Reset(0);
        Require(!rules.JoinPair(0, 0, 1, 15, false, out _), "Pair console validates the participant location");
        Require(rules.JoinPair(0, 0, 1, 15, true, out _), "First participant reserves console A");
        Require(!rules.JoinPair(0, 1, 1, 15, true, out _), "One player cannot operate both consoles");
        Require(!rules.JoinPair(1, 0, 1, 15, true, out _), "Another player cannot steal an occupied console");
        Require(rules.JoinPair(1, 1, 1, 15, true, out _), "Second participant reserves console B");
        rules.Tick(1, 16, true, (id, _) => id != 1, _ => true, out _, out _, out _);
        Require(rules.PairFirst == 0 && rules.PairSecond == -1 && rules.PairProgress(16) == 0, "Losing a participant cancels progress and releases their console");
        Require(rules.JoinPair(2, 1, 1, 17, true, out _), "Replacement participant starts a fresh hold");
        Require(!rules.Tick(1, 19, true, (_, _) => true, _ => true, out _, out _, out _), "Replacement cannot inherit old hold time");
        Require(rules.Tick(1, 20, true, (_, _) => true, _ => true, out int first, out int second, out _) && first == 0 && second == 2,
            "A complete hold returns both verified participants");
        Require(!rules.JoinPair(0, 0, 1, 21, true, out _), "Completed daily pair task cannot be farmed");
        Require(rules.JoinPair(0, 0, 2, 22, true, out _) && rules.JoinPair(2, 1, 2, 22, true, out _), "Pair task reopens next day");
        rules.Release(2);
        Require(rules.PairSecond == -1 && rules.PairProgress(23) == 0, "Disconnect releases a participant and resets progress");
        rules.Tick(2, 24, false, (_, _) => true, _ => true, out _, out _, out _);
        Require(rules.PairFirst == -1 && rules.PairSecond == -1, "Leaving daytime clears pair reservations");
    }

    private static void CheckItems()
    {
        var rules = new ThuongVillageAuthority(); rules.Reset(0);
        Require(rules.GrantTaskReward(0, 1, out var first) && first == ThuongItemKind.Lantern, "First task reward setup");
        Require(rules.GrantTaskReward(0, 1, out _), "Second task may grant a reward");
        Require(!rules.GrantTaskReward(0, 2, out _), "Full inventory rejects another reward even on a new day");
        Require(!rules.TryUseItem(0, ThuongItemKind.Lantern, 15, false, out _), "Ineligible player cannot consume an item");
        Require(rules.Inventory(0).Count == 2, "Rejected item use does not consume inventory");
        Require(rules.TryUseItem(0, ThuongItemKind.Lantern, 15, true, out _) && rules.LanternUntil(0) == 35, "Lantern consumes one item and lasts twenty seconds");
        Require(!rules.TryUseItem(0, ThuongItemKind.Lantern, 16, true, out _), "Used item cannot be consumed again");
        rules.Reset(0);
        rules.GrantTaskReward(0, 1, out _); rules.GrantTaskReward(0, 1, out _);
        rules.TryUseItem(0, ThuongItemKind.Lantern, 15, true, out _);
        Require(!rules.GrantTaskReward(0, 1, out _), "Daily reward cap holds after freeing an inventory slot");
        Require(rules.GrantTaskReward(0, 2, out var ward) && ward == ThuongItemKind.Ward, "New day resets reward quota");
        Require(rules.TryUseItem(0, ThuongItemKind.Ward, 16, true, out _) && rules.WardUntil(0) == 46, "Ward consumes an item and lasts thirty seconds");
        Require(rules.ConsumeWard(0, 17) && !rules.ConsumeWard(0, 18), "Ward can block exactly one attack");
        rules.Reset(0); rules.GrantTaskReward(2, 1, out _); rules.TryUseItem(2, ThuongItemKind.Ward, 15, true, out _);
        Require(!rules.ConsumeWard(2, 45), "Expired ward does not block an attack");
        rules.Reset(100);
        Require(rules.Inventory(0).Count == 0 && rules.WardUntil(2) == 0 && rules.LanternUntil(0) == 0, "New round clears inventory and active effects");
        Require(!rules.TryUseItem(0, (ThuongItemKind)99, 115, true, out _), "Unknown item is rejected");
    }

    private static void CheckLobbyVersion()
    {
        var approve = typeof(ThuongMainFlow).GetMethod("ApproveLobby", BindingFlags.Static | BindingFlags.NonPublic);
        var response = new NetworkManager.ConnectionApprovalResponse();
        var request = new NetworkManager.ConnectionApprovalRequest { Payload = Encoding.UTF8.GetBytes("THUONG12|lobby") };
        approve.Invoke(null, new object[] { request, response });
        Require(response.Approved, "Current lobby protocol is approved");
        request.Payload = Encoding.UTF8.GetBytes("THUONG11|lobby"); response = new NetworkManager.ConnectionApprovalResponse();
        approve.Invoke(null, new object[] { request, response });
        Require(!response.Approved, "Old lobby protocol is rejected before loading incompatible gameplay");
    }
}
