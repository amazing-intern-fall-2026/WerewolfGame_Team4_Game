using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Thuong.Minigames;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ThuongMinigameChecks
{
    private static int checks;
    [MenuItem("Tools/Thuong/Minigames/Check Rules And Assets")]
    public static void Run()
    {
        checks = 0;
        Assets(); LocksAndLifecycle(); TimedGames(); Potions(); Traps();
        Directory.CreateDirectory("Logs/Minigames");
        File.WriteAllText("Logs/Minigames/rules.txt", "THUONG_MINIGAME_CHECKS_PASSED: " + checks + " assertions\n");
        Debug.Log("THUONG_MINIGAME_CHECKS_PASSED: " + checks + " assertions");
    }

    private static ThuongMinigameRules Rules(int target = 5, int misses = 2) => new()
    { target = target, allowedMistakes = misses, duration = 90, minimumDayRemaining = 10, interval = 1.6f, timingWindow = .4f };
    private static ThuongMinigameState Begin(ThuongMinigameAuthority server, ThuongMinigameKind kind, int target = 5, int misses = 2)
    {
        Require(server.TryBegin(1, "test.task", 0, 1, kind, Rules(target, misses), new[] { 2, -2, 0 }, 100, 200, out var state, out _), "Begin " + kind);
        return state;
    }

    private static void LocksAndLifecycle()
    {
        var server = new ThuongMinigameAuthority(); var s = Begin(server, ThuongMinigameKind.ChopWood);
        Require(!server.TryBegin(1, "other", 1, 1, s.kind, Rules(), null, 100, 200, out _, out _), "One attempt per owner");
        Require(!server.TryBegin(2, "test.task", 8, 1, s.kind, Rules(), null, 100, 200, out _, out _), "Lock shared task across station indices");
        Require(server.Apply(2, s.attemptId, 1, new(ThuongMinigameAction.Chop, 1), s.roundAt) == null, "Wrong owner rejected");
        Require(server.Cancel(2, s.attemptId, "bad") == null, "Wrong owner cannot cancel");
        Require(server.Apply(1, s.attemptId, 1, new(ThuongMinigameAction.MoveBasket, 0, float.NaN), 101) == null, "NaN rejected");
        Require(server.Cancel(1, s.attemptId, "cancel").result == ThuongMinigameResult.Cancelled && server.ActiveCount == 0, "Cancel releases movement/task lock");
        Require(server.TryBegin(2, "test.task", 8, 1, s.kind, Rules(), null, 101, 200, out var retry, out _), "Another owner can retry");
        Require(retry.attemptId != s.attemptId && server.Apply(1, s.attemptId, 3, new(ThuongMinigameAction.Chop, 1), s.roundAt) == null, "Late packet cannot apply to retry");
        Require(server.Tick(201).Single().result == ThuongMinigameResult.Expired && !server.IsPlaying(2), "Deadline releases lock");
        Require(server.Tick(202).Count == 0, "Expired result emitted once");
        Require(!server.TryBegin(3, "short", 0, 1, s.kind, Rules(), null, 100, 109, out _, out _), "Reject too little daylight");
        Require(!server.TryBegin(3, "bad", 16, 1, s.kind, Rules(), null, 100, 200, out _, out _), "Enforce 16-bit station mask");
        for (ulong owner = 0; owner < 25; owner++)
        {
            bool accepted = server.TryBegin(owner, "shared." + owner % 10, (int)(owner % 10), 2, s.kind, Rules(), null, 100, 200, out _, out _);
            Require(accepted == (owner < 10), "25 players contend for ten distinct tasks");
        }
        Require(server.ActiveCount == 10, "One owner per task with 25 players");
        foreach (ulong owner in server.Owners()) server.Cancel(owner, server.Get(owner).attemptId, "phase");
        Require(server.ActiveCount == 0, "Phase change clears every lock");
    }

    private static void TimedGames()
    {
        foreach (var kind in new[] { ThuongMinigameKind.ChopWood, ThuongMinigameKind.Harvest, ThuongMinigameKind.Eggs, ThuongMinigameKind.Defender })
        {
            var server = new ThuongMinigameAuthority(); var s = Begin(server, kind, 8); ulong id = s.attemptId; uint seq = 1;
            var action = kind == ThuongMinigameKind.ChopWood ? ThuongMinigameAction.Chop : ThuongMinigameAction.Harvest;
            while (s.result == ThuongMinigameResult.Playing)
            {
                if (kind == ThuongMinigameKind.ChopWood || kind == ThuongMinigameKind.Harvest)
                {
                    if (s.obstacle) { server.Tick(s.roundAt + .41); s = server.Get(1); continue; }
                    var early = server.Apply(1, id, seq++, new(action, s.round), s.roundAt - .7);
                    Require(early != null && early.score == s.score, "Early action gives no score");
                    s = server.Apply(1, id, seq++, new(action, s.round), s.roundAt);
                }
                else
                {
                    double at = s.roundAt;
                    var input = new ThuongMinigameInput(kind == ThuongMinigameKind.Eggs ? ThuongMinigameAction.MoveBasket : ThuongMinigameAction.AimShield, 0, s.lane);
                    server.Apply(1, id, seq++, input, at - .3);
                    var terminal = server.Tick(at + .01);
                    s = terminal.Count > 0 ? terminal.Single() : server.Get(1);
                }
                Require(s != null, "Round advances " + kind);
            }
            Require(s.result == ThuongMinigameResult.Succeeded && s.score == 8 && !server.IsPlaying(1), "Real action sequence wins " + kind);
            Require(server.Apply(1, id, seq++, new(action, s.round), s.roundAt) == null, "Duplicate win cannot be consumed twice " + kind);
        }
        {
            var server = new ThuongMinigameAuthority(); var s = Begin(server, ThuongMinigameKind.ChopWood, 5, 0);
            Require(server.Tick(s.roundAt + .41).Single().result == ThuongMinigameResult.Failed, "Missing wood fails the attempt only");
        }
        {
            var server = new ThuongMinigameAuthority(); var s = Begin(server, ThuongMinigameKind.Harvest, 8); uint seq = 1;
            while (!s.obstacle) s = server.Apply(1, s.attemptId, seq++, new(ThuongMinigameAction.Harvest, s.round), s.roundAt);
            Require(server.Apply(1, s.attemptId, seq, new(ThuongMinigameAction.Harvest, s.round), s.roundAt).result == ThuongMinigameResult.Failed, "Harvest obstacle fails");
        }
        {
            var server = new ThuongMinigameAuthority(); var s = Begin(server, ThuongMinigameKind.Eggs);
            server.Apply(1, s.attemptId, 1, new(ThuongMinigameAction.MoveBasket, 0, 1), s.startsAt + .01);
            var before = server.Get(1);
            Require(server.Apply(1, s.attemptId, 1, new(ThuongMinigameAction.MoveBasket, 0, 0), s.roundAt) == null, "Duplicate action sequence rejected");
            Require(server.Get(1).control == before.control, "Duplicate does not move basket");
            Require(server.Apply(1, s.attemptId, 2, new(ThuongMinigameAction.MoveBasket, 0, 2), s.startsAt + .2) == null, "Out-of-range control rejected");
        }
    }

    private static void Potions()
    {
        for (int trial = 0; trial < 12; trial++)
        {
            var server = new ThuongMinigameAuthority(); var s = Begin(server, ThuongMinigameKind.Potion); int[] points = { 2, -2, 0 };
            var combination = (from a in Enumerable.Range(0, 3) from b in Enumerable.Range(0, 3) from c in Enumerable.Range(0, 3)
                where ThuongMinigameAuthority.EvaluatePotion(points[a] + points[b] + points[c]) == s.recipe select new[] { a, b, c }).First();
            for (int i = 0; i < 3; i++) s = server.Apply(1, s.attemptId, (uint)(i + 1), new(ThuongMinigameAction.Ingredient, combination[i]), 101 + i * .2);
            Require(s.result == ThuongMinigameResult.Succeeded && s.ingredients.SequenceEqual(combination), "Server verifies three actual ingredients");
        }
        var wrongServer = new ThuongMinigameAuthority(); var wrong = Begin(wrongServer, ThuongMinigameKind.Potion);
        for (int i = 0; i < 3; i++) wrong = wrongServer.Apply(1, wrong.attemptId, (uint)(i + 1), new(ThuongMinigameAction.Ingredient, 2), 101 + i * .2);
        Require(wrong.result == ThuongMinigameResult.Failed, "Wrong recipe fails without changing role/health");
    }

    private static void Traps()
    {
        for (int trial = 0; trial < 10; trial++)
        {
            var server = new ThuongMinigameAuthority(); var s = Begin(server, ThuongMinigameKind.Trapsweeper);
            Require(s.cells.Length == 25 && s.cells.All(cell => cell == -2), "Initial packet does not reveal hidden traps");
            using (var writer = new FastBufferWriter(1024, Allocator.Temp))
            {
                ThuongMinigameWire.Write(writer, s);
                using var reader = new FastBufferReader(writer, Allocator.Temp);
                var read = ThuongMinigameWire.Read(reader);
                Require(read.cells.SequenceEqual(s.cells) && read.attemptId == s.attemptId && read.taskId == s.taskId, "Wire round-trip contains only public board");
            }
            s = server.Apply(1, s.attemptId, 1, new(ThuongMinigameAction.Reveal, 12), 101);
            Require(s.cells[12] == 0 && s.result != ThuongMinigameResult.Failed, "First opening and its neighbors are safe");
            if (s.result == ThuongMinigameResult.Succeeded) continue;
            bool[] hidden = HiddenTraps(server, 1);
            Require(hidden.Count(cell => cell) == 3, "Exactly three traps");
            uint seq = 2; double now = 101.2;
            for (int i = 0; i < 25 && s.result == ThuongMinigameResult.Playing; i++)
                if (!hidden[i] && s.cells[i] == -2)
                { s = server.Apply(1, s.attemptId, seq++, new(ThuongMinigameAction.Reveal, i), now); now += .2; }
            Require(s.score == 22 && s.result == ThuongMinigameResult.Succeeded, "Only opening all safe cells wins");
        }
        var losing = new ThuongMinigameAuthority(); var losingRules = Rules(); losingRules.traps = 12;
        Require(losing.TryBegin(1, "test.traps", 0, 1, ThuongMinigameKind.Trapsweeper, losingRules, null, 100, 200, out var board, out _), "Begin dense loss fixture");
        board = losing.Apply(1, board.attemptId, 1, new(ThuongMinigameAction.Flag, 0), 101);
        Require(board.cells[0] == -3, "Flag is exported");
        Require(losing.Apply(1, board.attemptId, 2, new(ThuongMinigameAction.Reveal, 0), 101.2) == null, "Flag prevents opening");
        board = losing.Apply(1, board.attemptId, 3, new(ThuongMinigameAction.Reveal, 12), 101.4);
        int trap = Array.FindIndex(HiddenTraps(losing, 1), 1, cell => cell);
        var failed = losing.Apply(1, board.attemptId, 4, new(ThuongMinigameAction.Reveal, trap), 101.6);
        Require(failed.result == ThuongMinigameResult.Failed && failed.cells[trap] == -9, "Opening a trap fails");
    }

    private static bool[] HiddenTraps(ThuongMinigameAuthority server, ulong owner)
    {
        // Test oracle only. Runtime views and network codecs have no access to this state.
        var attempts = (IDictionary)typeof(ThuongMinigameAuthority).GetField("active", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(server);
        return (bool[])attempts[owner].GetType().GetField("traps").GetValue(attempts[owner]);
    }
    private static void Assets()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<ThuongMinigameCatalog>(ThuongMinigameInstaller.CatalogPath);
        Require(catalog != null && catalog.Validate(out _), "Valid build-time catalog");
        Require(catalog.schemaVersion == 2 && catalog.entries.Count == 6 && catalog.additionalStations.Count == 0 && catalog.stationPrefab == null,
            "Six games with station placement owned by map definitions");
        Require(catalog.entries.All(entry => !entry.viewPrefab.GetComponentsInChildren<Component>(true).Any(component =>
            component is Camera || component is AudioListener || component is UnityEngine.EventSystems.EventSystem)), "Task prefab has no extra camera/listener/event system");
        Require(catalog.entries.Where(entry => entry.kind != ThuongMinigameKind.Trapsweeper).All(entry => entry.subject != null), "Source sprite for each task adapter");
        Require(catalog.trapCover != null && catalog.trapCleared != null && catalog.trapFlag != null, "Original Trapsweeper sprites referenced");
        var guids = new System.Collections.Generic.HashSet<string>();
        foreach (string path in Directory.GetFiles("Assets/Minigames/Trapsweeper", "*", SearchOption.AllDirectories))
        {
            if (path.EndsWith(".meta"))
            {
                string guid = Regex.Match(File.ReadAllText(path), @"(?m)^guid: ([a-f0-9]{32})").Groups[1].Value;
                Require(guid.Length == 32 && guids.Add(guid) && AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/') == path.Substring(0, path.Length - 5).Replace('\\', '/'), "Imported GUID resolves uniquely: " + path);
            }
            else if (!path.EndsWith(".png"))
                foreach (Match match in Regex.Matches(File.ReadAllText(path), @"guid: ([a-f0-9]{32})"))
                    if (!match.Groups[1].Value.StartsWith("0000000000000000"))
                        Require(!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(match.Groups[1].Value)), "Imported dependency exists: " + path);
            else Require(new FileInfo(path).Length > 150, "LFS binary hydrated: " + path);
        }
        Require(!EditorBuildSettings.scenes.Any(scene => scene.path.StartsWith("Assets/Minigames/")), "No demo scenes added to build settings");
        var demo = EditorSceneManager.OpenPreviewScene("Assets/Minigames/Trapsweeper/Trapsweeper.unity");
        try { Require(demo.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).All(component => component != null), "Imported demo has no missing scripts"); }
        finally { EditorSceneManager.ClosePreviewScene(demo); }
    }
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Minigame check failed: " + message); checks++; }
}
