using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Thuong.Online;
using UnityEditor;
using UnityEngine;

public static class ThuongEndgameChecks
{
    private static Type playerType;
    private static MethodInfo evaluate;
    private static int checks;

    [MenuItem("Tools/Thuong/Check Endgame Rules")]
    public static void Run()
    {
        var assembly = typeof(WinConditionManager).Assembly;
        playerType = assembly.GetType("Thuong.Online.ThuongWinPlayer", true);
        evaluate = assembly.GetType("Thuong.Online.ThuongMatchOutcome", true)
            .GetMethod("Evaluate", BindingFlags.Public | BindingFlags.Static);
        checks = 0;

        AssertWinner(Evaluate(P(Player(1, RoleType.Villager, FactionType.Villager, alive: false))), "Draw", "NobodyAlive");

        AssertWinner(Evaluate(P(
            Player(1, RoleType.Villager, FactionType.Villager),
            Player(2, RoleType.Madman, FactionType.Neutral, alive: false, votedOut: true)), dayFinished: true),
            "Madman", "MadmanVoted");

        AssertWinner(Evaluate(P(
            Player(1, RoleType.Villager, FactionType.Villager),
            Player(2, RoleType.Madman, FactionType.Neutral, alive: false)), dayFinished: true),
            "Villagers", "NoThreats");

        AssertWinner(Evaluate(P(
            Player(1, RoleType.Villager, FactionType.Villager, loverId: 2),
            Player(2, RoleType.FoxSpirit, FactionType.Neutral, loverId: 1, foxQualified: true))),
            "Lovers", "LoversLastTwo");

        object foxResult = Evaluate(P(
            Player(1, RoleType.FoxSpirit, FactionType.Neutral, foxQualified: true),
            Player(2, RoleType.Villager, FactionType.Villager)));
        AssertWinner(foxResult, "FoxSpirit", "FoxCharmed");
        AssertWonIds(foxResult, 1);

        object houndResult = Evaluate(P(
            Player(1, RoleType.WhiteHound, FactionType.Monster, awakened: true),
            Player(2, RoleType.Villager, FactionType.Villager)));
        AssertWinner(houndResult, "WhiteHound", "WhiteHoundSurvived");
        AssertWonIds(houndResult, 1);

        object killerResult = Evaluate(P(
            Player(1, RoleType.Killer, FactionType.Neutral),
            Player(2, RoleType.Villager, FactionType.Villager)));
        AssertWinner(killerResult, "Killer", "KillerSurvived");
        AssertWonIds(killerResult, 1);

        object taskResult = Evaluate(P(
            Player(1, RoleType.Villager, FactionType.Villager),
            Player(2, RoleType.Villager, FactionType.Villager, alive: false),
            Player(3, RoleType.DogSpirit, FactionType.Monster)), progress: 100);
        AssertWinner(taskResult, "Villagers", "Tasks");
        AssertWonIds(taskResult, 1, 2);

        object parityResult = Evaluate(P(
            Player(1, RoleType.DogSpirit, FactionType.Monster),
            Player(2, RoleType.Ogre, FactionType.Monster),
            Player(3, RoleType.Villager, FactionType.Villager),
            Player(4, RoleType.Villager, FactionType.Villager)));
        AssertWinner(parityResult, "Werewolves", "Parity");
        AssertWonIds(parityResult, 1, 2);

        Require(Evaluate(P(
            Player(1, RoleType.DogSpirit, FactionType.Monster),
            Player(2, RoleType.Villager, FactionType.Villager))) == null,
            "Two-player parity does not end the match");

        AssertWinner(Evaluate(P(
            Player(1, RoleType.DogSpirit, FactionType.Monster),
            Player(2, RoleType.Villager, FactionType.Villager)), day: 7, dayFinished: true),
            "Werewolves", "DayLimit");

        var noElimination = Settings(eliminationWin: false);
        AssertWinner(Evaluate(P(Player(1, RoleType.Villager, FactionType.Villager)),
            settings: noElimination, day: 7, dayFinished: true), "Draw", "TimeLimitDraw");

        AssertWinner(Evaluate(P(Player(1, RoleType.Piper, FactionType.Neutral))), "Draw", "Stalemate");

        Require(Evaluate(P(
            Player(1, RoleType.FoxSpirit, FactionType.Neutral),
            Player(2, RoleType.Piper, FactionType.Neutral),
            Player(3, RoleType.Jester, FactionType.Neutral))) == null,
            "Fox with two possible living targets can continue");

        Debug.Log($"THUONG_ENDGAME_RULES_PASSED: {checks} checks.");
    }

    private static object Evaluate(object[] players, ThuongOnlineWinSettings settings = null,
        int day = 1, float progress = 0, bool dayFinished = false)
    {
        Type listType = typeof(List<>).MakeGenericType(playerType);
        var typedPlayers = (IList)Activator.CreateInstance(listType);
        foreach (object player in players) typedPlayers.Add(player);
        settings ??= Settings();
        return evaluate.Invoke(null, new object[]
        {
            typedPlayers, settings, day, GamePhase.Voting, progress, 7, dayFinished
        });
    }

    private static object Player(int id, RoleType role, FactionType faction, bool alive = true,
        bool votedOut = false, int loverId = -1, bool awakened = false, bool foxQualified = false) =>
        Activator.CreateInstance(playerType, id, $"Player {id}", id, role, faction,
            alive, votedOut, loverId, awakened, foxQualified);

    private static object[] P(params object[] players) => players;

    private static ThuongOnlineWinSettings Settings(bool eliminationWin = true) => new()
    {
        eliminationWin = eliminationWin,
        minimumPlayersForParity = 3,
        specialWinVillageLimit = 2
    };

    private static void AssertWinner(object result, string winner, string reason)
    {
        Require(result != null, $"Expected {winner} result");
        Require(Read(result, "Winner").ToString() == winner, $"Winner is {winner}");
        Require(Read(result, "Reason").ToString() == reason, $"Win reason is {reason}");
    }

    private static void AssertWonIds(object result, params int[] expected)
    {
        var players = (IEnumerable)Read(result, "Players");
        int[] actual = players.Cast<object>()
            .Where(player => (bool)Read(player, "Won"))
            .Select(player => (int)Read(player, "Id"))
            .ToArray();
        Require(actual.SequenceEqual(expected.OrderBy(id => id)), "Winner player IDs match the rule");
    }

    private static object Read(object instance, string member)
    {
        Type type = instance.GetType();
        PropertyInfo property = type.GetProperty(member);
        if (property != null) return property.GetValue(instance);
        return type.GetField(member).GetValue(instance);
    }

    private static void Require(bool condition, string label)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("Endgame rules: " + label);
    }
}