using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Thuong.Online;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ThuongReviewImprovementChecks
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly List<string> passed = new();
    private static void Require(bool value, string label)
    { if (!value) throw new InvalidOperationException(label); passed.Add(label); }
    private static void Set(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, Flags);
        if (value != null && field.FieldType.IsPrimitive && value.GetType() != field.FieldType)
            value = Convert.ChangeType(value, field.FieldType);
        field.SetValue(target, value);
    }
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);

    [MenuItem("Tools/Thuong/Review Improvements/Check Regressions")]
    public static void Run()
    {
        passed.Clear();
        CheckSettings(); CheckPractice(); CheckExport(); CheckSpriteFallback(); CheckSessionState(); CheckJournalChunks(); CheckHunter();
        Directory.CreateDirectory("Logs/ThuongReviewImprovements20261112");
        File.WriteAllLines("Logs/ThuongReviewImprovements20261112/regressions.txt", new[] { "PASS checks=" + passed.Count }.Concat(passed));
        Debug.Log("THUONG_REVIEW_IMPROVEMENTS_PASSED checks=" + passed.Count);
        ThuongVillageMergeChecks.Run();
    }

    private static void CheckSettings()
    {
        var settings = new ThuongVillageSettings { meetingLimit = 1, meetingCooldown = 20, sabotageCooldown = 60,
            blackoutSeconds = 25, jamSeconds = 30, barricadeSeconds = 10, lanternSeconds = 40, wardSeconds = 55 };
        var room = new ThuongRoomRules { village = settings };
        Require(room.IsValid, "Custom village rules accepted");
        var copy = room.Copy(); copy.village.meetingLimit = 4;
        Require(room.village.meetingLimit == 1, "Editing draft does not mutate applied room rules");
        using (var writer = new FastBufferWriter(64, Allocator.Temp))
        {
            settings.WriteTo(writer);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            var received = ThuongVillageSettings.ReadFrom(reader);
            Require(JsonUtility.ToJson(settings) == JsonUtility.ToJson(received), "All village settings survive wire round trip");
            Require(reader.Position == reader.Length, "Village wire consumes exactly its payload");
        }
        foreach (var field in typeof(ThuongVillageSettings).GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var invalid = settings.Copy(); field.SetValue(invalid, 999);
            Require(!invalid.IsValid, "Reject excessive " + field.Name);
            using var writer = new FastBufferWriter(64, Allocator.Temp); invalid.WriteTo(writer);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            bool rejected = false;
            try { ThuongVillageSettings.ReadFrom(reader); } catch (ArgumentException) { rejected = true; }
            Require(rejected, "Reject invalid received " + field.Name);
        }
        var authority = new ThuongVillageAuthority(); authority.Configure(settings); authority.Reset(0);
        settings.meetingLimit = 0;
        Require(authority.CanMeet(0, 15, true, true, true, false, out _), "Authority retains an immutable settings copy");
        authority.CommitMeeting(0, 15, false);
        Require(authority.NextMeetingAt == 35 && !authority.CanMeet(1, 100, true, true, true, false, out _), "Custom meeting cooldown and cap enforced");
        authority.Configure(settings); authority.Reset(0);
        Require(!authority.CanMeet(0, 15, true, true, true, false, out _), "Zero meeting quota disables meetings");
        settings.meetingLimit = 1; authority.Configure(settings);
        foreach (var kind in new[] { ThuongSabotageKind.Blackout, ThuongSabotageKind.StationJam, ThuongSabotageKind.Barricade })
        {
            authority.Reset(0);
            Require(authority.TrySabotage(2, 1, 15, true, true, kind, 0, true, out _), "Custom sabotage can start: " + kind);
            double duration = kind == ThuongSabotageKind.Blackout ? 25 : kind == ThuongSabotageKind.StationJam ? 30 : 10;
            Require(authority.SabotageUntil == 15 + duration && authority.NextSabotageAt == 75, "Custom sabotage duration and cooldown: " + kind);
        }
        authority.Reset(0); authority.GrantTaskReward(0, 1, out _);
        Require(authority.TryUseItem(0, ThuongItemKind.Lantern, 15, true, out _) && authority.LanternUntil(0) == 55, "Custom lantern duration enforced");
        authority.GrantTaskReward(2, 1, out _);
        Require(authority.TryUseItem(2, ThuongItemKind.Ward, 15, true, out _) && authority.WardUntil(2) == 70, "Custom ward duration enforced");
    }

    private static void CheckPractice()
    {
        var practice = new ThuongVillagePractice(); practice.Begin(0, 20);
        Require(!practice.Interact(20), "Practice bell rejects distant caller");
        practice.Move(ThuongVillagePractice.Bell);
        Require(practice.Interact(20) && practice.Succeeded, "Practice bell completes by proximity and interaction");
        practice.Begin(1, 30); practice.Move(ThuongVillagePractice.PadA); practice.Interact(30); practice.Tick(32);
        Require(!practice.Succeeded, "Practice pair requires full hold");
        practice.Move(Vector2.right * .01f); practice.Tick(33);
        Require(!practice.Succeeded, "Any movement cancels practice pair channel");
        practice.Interact(34); practice.Tick(37);
        Require(practice.Succeeded, "Practice pair can restart and complete");
        practice.Begin(2, 40); practice.Move(ThuongVillagePractice.Repair); practice.Interact(40); practice.Tick(43);
        Require(practice.Succeeded, "Practice repair completes after stationary hold");
        practice.Begin(3, 50);
        Require(!practice.Interact(50), "Practice lantern requires affected area");
        practice.Move(ThuongVillagePractice.Repair);
        Require(practice.Interact(50) && practice.Succeeded, "Practice lantern is consumed successfully");
        Require(!practice.Interact(51), "Practice does not reward repeated completed actions");
        practice.Begin(2, 60); practice.Tick(81);
        Require(!practice.Succeeded && practice.Stage == 2, "Expired practice sabotage restarts without granting completion");
    }

    private static void CheckExport()
    {
        var document = new ThuongJournalDocument
        {
            matchEpoch = 7, exportedAtUtc = "2026-11-12T00:00:00Z", winner = "Dân Làng", reason = "Đủ nhiệm vụ", mode = "Cổ điển",
            villageRules = new ThuongVillageSettings(), truncated = true,
            players = new[] { new ThuongJournalPlayer { id = 0, name = "Thương \"A\"\n../", role = "Tiên tri", faction = "Dân Làng", alive = true, won = true } },
            events = new[] { new ThuongJournalEvent { order = 1, day = 1, phase = "BỎ PHIẾU", text = "Bỏ phiếu cho người chơi Đặng." } }
        };
        string directory = Path.GetFullPath("Logs/ThuongReviewImprovements20261112/ExportChecks");
        string json = ThuongJournalExport.WriteNew(directory, document, true);
        string text = ThuongJournalExport.WriteNew(directory, document, false);
        string second = ThuongJournalExport.WriteNew(directory, document, true);
        Require(json != second && File.Exists(json) && File.Exists(text), "Repeated export creates distinct files without overwriting");
        Require(Path.GetDirectoryName(json) == directory && !Path.GetFileName(json).Contains("Thương"), "Player names cannot affect export path");
        var decoded = JsonUtility.FromJson<ThuongJournalDocument>(File.ReadAllText(json));
        Require(decoded.players[0].name == document.players[0].name && decoded.events[0].text == document.events[0].text, "JSON preserves Vietnamese, quotes and newlines");
        Require(decoded.truncated && decoded.villageRules.meetingLimit == 3, "Export preserves truncation and match rules");
        Require(File.ReadAllText(text).Contains("BỎ PHIẾU") && File.ReadAllText(text).Contains("lược bớt"), "Text export contains readable events and truncation notice");
    }

    private static void CheckSpriteFallback()
    {
        var instanceField = typeof(NetworkRoleDetailUI).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = instanceField.GetValue(null);
        var root = new GameObject("Review missing role images"); root.SetActive(false);
        try
        {
            var detail = root.AddComponent<NetworkRoleDetailUI>(); instanceField.SetValue(null, detail);
            RoleInfoDatabase.ClearSpriteCache();
            Require(detail.GetAssignedRoleSprite((RoleType)999) == null, "Missing assigned image returns null directly");
            Require(RoleInfoDatabase.GetRoleSprite((RoleType)999) == null && detail.GetRoleSprite((RoleType)999) == null,
                "Missing role image terminates both fallback paths without recursion");
            Set(detail, "roleImages", new[] { new NetworkRoleDetailUI.RoleImageEntry { role = RoleType.Seer, image = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero) } });
            var assigned = detail.GetAssignedRoleSprite(RoleType.Seer);
            Require(assigned != null && RoleInfoDatabase.GetRoleSprite(RoleType.Seer) == assigned, "Assigned scene portrait remains preferred");
            Object.DestroyImmediate(assigned);
        }
        finally { Object.DestroyImmediate(root); instanceField.SetValue(null, previous); RoleInfoDatabase.ClearSpriteCache(); }
    }

    private static void CheckSessionState()
    {
        var root = new GameObject("Review inactive session"); root.SetActive(false);
        var session = root.AddComponent<ThuongOnlineSession>();
        Keyboard keyboard = null;
        try
        {
            Guid token = Guid.NewGuid(); Set(session, "reconnectToken", token); Set(session, "matchEpoch", (uint)7);
            Call(session, "BindNotebookToRound", (uint)7);
            var notes = Get<List<string>>(session, "notebookEntries"); notes.Add("Ghi chú tiếng Việt");
            Set(session, "connected", true); Set(session, "clientHadRoles", true); Set(session, "matchStarted", true);
            Call(session, "HandleNetworkStopped", false);
            Require(notes.Count == 1 && Get<double>(session, "reconnectOfferUntil") > Time.realtimeSinceStartupAsDouble, "Transport loss preserves notebook while offering reconnect");
            Call(session, "ResetReplicatedRound", (uint)7);
            Require(notes.Count == 1, "Same match resync retains saved notes");
            Call(session, "ResetReplicatedRound", (uint)8);
            Require(notes.Count == 0, "New match clears notebook");
            notes.Add("Old room"); Set(session, "reconnectToken", Guid.NewGuid()); Call(session, "BindNotebookToRound", (uint)8);
            Require(notes.Count == 0, "Different room clears notebook even when epoch matches");
            notes.Add("Voluntary leave"); Call(session, "ClearClientState", false);
            Require(notes.Count == 0, "Explicit leave clears notebook");
            Require(!(bool)Call(session, "ExportMatchJournal", true), "Unfinished match cannot export journal");
            Set(session, "rolesAssigned", true); Set(session, "privateStateReceived", true);
            Set(session, "matchEpoch", (uint)8); Set(session, "privateMatchEpoch", (uint)8); Set(session, "privateFlags", 1);
            Set(session, "privateRole", RoleType.Killer); Set(session, "winSettings", new ThuongOnlineWinSettings { specialWinVillageLimit = 3 });
            Require(session.LocalRoleInformation().Restrictions.Contains("3 dân") && !session.LocalRoleInformation().Restrictions.Contains("duy nhất"), "Killer description uses authoritative special win limit");
            Set(session, "privateRole", RoleType.WhiteHound); Set(session, "privateFlags", 1 | 128);
            Require(session.LocalRoleInformation().Faction == FactionType.Neutral && session.LocalRoleInformation().SkillDescription.Contains("đối địch"), "Awakened hound description identifies its separate allegiance");
            Set(session, "privateRole", RoleType.Seer); Set(session, "roomRules", new ThuongRoomRules { preset = ThuongRolePreset.Basic, mode = ThuongMatchMode.RealtimeProximity });
            Require(session.LocalRoleInformation().SkillDescription.Contains("nhấn E"), "Realtime role description teaches proximity input");
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); InputSystem.Update();
            Set(session, "localMapReady", true); Set(session, "visibleState", GameState.Day); Set(session, "privateFlags", 1);
            Require(((Vector2)Call(session, "ReadMovementInput")).y > 0, "Gameplay movement receives held key when overlays are closed");
            var panel = new GameObject("Review village modal"); panel.transform.SetParent(root.transform); Set(session, "villagePanel", panel);
            Require((Vector2)Call(session, "ReadMovementInput") == Vector2.zero, "Village modal blocks held movement key");
            Set(session, "interactionKeyPending", true); Call(session, "ProcessContextInteractionInput");
            Require(!Get<bool>(session, "interactionKeyPending"), "Village modal cancels pending contextual interaction");
        }
        finally { if (keyboard != null) InputSystem.RemoveDevice(keyboard); Object.DestroyImmediate(root); }
    }

    private static void CheckJournalChunks()
    {
        var root = new GameObject("Review journal receiver"); root.SetActive(false);
        try
        {
            var session = root.AddComponent<ThuongOnlineSession>();
            Set(session, "network", root.AddComponent<NetworkManager>()); Set(session, "matchEpoch", 7u);
            void Deliver(int chunk, bool truncated = true, ulong sender = 0, uint epoch = 7)
            {
                using var writer = new FastBufferWriter(4096, Allocator.Temp);
                writer.WriteValueSafe(epoch); writer.WriteValueSafe((ushort)chunk); writer.WriteValueSafe((ushort)3);
                writer.WriteValueSafe((ushort)33); writer.WriteValueSafe(truncated);
                int count = chunk == 2 ? 1 : 16; writer.WriteValueSafe((byte)count);
                for (int i = 0; i < count; i++)
                {
                    writer.WriteValueSafe(1); writer.WriteValueSafe((byte)GamePhase.HunterRevenge);
                    writer.WriteValueSafe(new FixedString128Bytes("Mốc " + (chunk * 16 + i)));
                }
                using var reader = new FastBufferReader(writer, Allocator.Temp);
                Call(session, "ReceiveJournal", sender, reader);
            }
            Deliver(0, sender: 99);
            Require(Get<object>(session, "incomingJournalChunks") == null, "Non-host cannot send journal chunks");
            Deliver(0, epoch: 6);
            Require(Get<object>(session, "incomingJournalChunks") == null, "Old epoch journal chunks are rejected");
            Deliver(2);
            Require(!Get<bool>(session, "journalReady"), "Last chunk arriving first does not expose a partial journal");
            Deliver(0);
            Require(!Get<bool>(session, "journalReady"), "Missing middle chunk keeps journal unavailable");
            Deliver(1, truncated: false);
            Require(!Get<bool>(session, "journalReady"), "Inconsistent chunk truncation flag is rejected");
            Deliver(1);
            var journal = Get<System.Collections.IList>(session, "matchJournal");
            Require(Get<bool>(session, "journalReady") && journal.Count == 33 && Get<bool>(session, "journalTruncated"),
                "Three out-of-order chunks assemble fully and preserve truncation flag");
            Require(Get<string>(journal[0], "Text") == "Mốc 0" && Get<string>(journal[32], "Text") == "Mốc 32", "Journal preserves chronological ordering across chunks");
            Deliver(0);
            Require(journal.Count == 33, "Duplicate journal chunk does not duplicate events");
            Require(!(bool)Call(session, "ExportMatchJournal", false), "Received journal still cannot export before final match result");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void CheckHunter()
    {
        var oldPlayers = PlayerManager.Instance; var oldRoles = RoleManager.Instance; var oldGame = GameRoleManager.Instance;
        var oldVotes = VoteManager.Instance; var oldWins = WinConditionManager.Instance; var oldDeath = DeathResolver.Instance;
        var oldTasks = TaskManager.Instance; var oldTimer = DayTimer.Instance; var oldNight = NightManager.Instance;
        var oldEvent = EventManager.Instance;
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameRoleManager.Instance = null;
            T Add<T>() where T : Component
            { var root = new GameObject(typeof(T).Name); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene); return root.AddComponent<T>(); }
            var players = Add<PlayerManager>(); PlayerManager.Instance = players;
            var roles = Add<RoleManager>(); RoleManager.Instance = roles;
            var game = Add<GameRoleManager>(); GameRoleManager.Instance = game;
            var votes = Add<VoteManager>(); VoteManager.Instance = votes;
            var wins = Add<WinConditionManager>(); WinConditionManager.Instance = wins;
            DeathResolver.Instance = Add<DeathResolver>(); TaskManager.Instance = Add<TaskManager>();
            DayTimer.Instance = Add<DayTimer>(); NightManager.Instance = Add<NightManager>();
            typeof(EventManager).GetProperty("Instance").SetValue(null, null);
            int evaluations = 0, evaluatedDay = 0; bool evaluatedFinished = false;
            wins.InstallMatchEvaluator(game, finished =>
            { if (evaluations++ == 0) { evaluatedDay = game.currentDay; evaluatedFinished = finished; } });
            void Roster(bool secondHunter = false)
            {
                players.players = Enumerable.Range(0, 4).Select(id => new PlayerData { playerID = id, playerName = "QA " + id,
                    isAlive = true, status = new PlayerStatus(), roleType = id == 0 || secondHunter && id == 1 ? RoleType.Hunter : id == 2 ? RoleType.DogSpirit : RoleType.Villager,
                    faction = id == 2 ? FactionType.Monster : FactionType.Villager }).ToList();
                roles.playerRoles.Clear();
                foreach (var player in players.players) roles.playerRoles[player.playerID] = RoleCatalog.Create(player.roleType, player);
                game.EnableHunterRevengeWindow = true; game.manualLogicPrototype = false;
                game.currentDay = 1; game.currentState = GameState.Voting; game.currentPhase = GamePhase.Voting;
                Set(game, "started", true); evaluations = 0; votes.StartVote();
                for (int id = 1; id < 4; id++) Require(votes.TryVote(id, 0), "Hunter vote accepted for voter " + id);
            }
            Roster(); game.FinishVoting();
            Require(game.IsHunterRevengeWindow && game.currentDay == 1 && evaluations == 0, "Voted Hunter gets a window before day advancement or result evaluation");
            game.StartVoting();
            Require(game.IsHunterRevengeWindow, "Revenge window cannot reopen voting");
            Require(!game.TryShootHunter(3, 2, out _), "Living non-Hunter cannot shoot");
            Require(game.TryShootHunter(0, 2, out _) && !players.IsAlive(2), "Hunter shot resolves immediately through death resolver");
            Require(!game.TryShootHunter(0, 3, out _), "Hunter cannot shoot twice");
            Require(game.MustWaitForHunterRevenge && evaluations == 0, "Result waits until accepted action has returned for journaling");
            Call(game, "Update");
            Require(game.currentDay == 2 && evaluatedDay == 1 && evaluatedFinished, "Vote continuation evaluates original day exactly before advancing");
            Roster(); game.FinishVoting(); Set(game, "<PhaseTimeRemaining>k__BackingField", 0f);
            Require(!game.TryShootHunter(0, 2, out _), "Expired revenge request is rejected before timeout continuation ticks");
            Call(game, "Update");
            Require(players.players[0].hasUsedHunterShot && !players.players[0].hasHunterTrap && game.currentDay == 2, "Timeout consumes unused shot and releases day continuation");
            Roster(true); game.FinishVoting(); game.TryShootHunter(0, 1, out _);
            Require(game.HasPendingHunterRevenge && !players.IsAlive(1), "Chained Hunter death receives its own pending shot");
            Require(game.TryShootHunter(1, 2, out _), "Second Hunter can shoot within same bounded window");
            Call(game, "Update"); Require(game.currentDay == 2, "Chained shots advance day only once");
            Roster(); game.currentState = GameState.Night; game.currentPhase = GamePhase.Night;
            players.players[0].isAlive = false; players.players[0].hasHunterTrap = true;
            game.FinishNight();
            Require(game.IsHunterRevengeWindow && evaluations == 0, "Night resolution also waits for pending Hunter");
            game.TryShootHunter(0, 2, out _); Call(game, "Update");
            Require(game.currentPhase == GamePhase.Discussion && game.currentDay == 1 && !evaluatedFinished, "Night revenge resumes discussion without advancing day");
            game.EnableHunterRevengeWindow = false;
            Require(!game.HasPendingHunterRevenge, "Legacy scenes do not opt into online revenge flow");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            PlayerManager.Instance = oldPlayers; RoleManager.Instance = oldRoles; GameRoleManager.Instance = oldGame;
            VoteManager.Instance = oldVotes; WinConditionManager.Instance = oldWins; DeathResolver.Instance = oldDeath;
            TaskManager.Instance = oldTasks; DayTimer.Instance = oldTimer; NightManager.Instance = oldNight;
            typeof(EventManager).GetProperty("Instance").SetValue(null, oldEvent);
        }
    }
}
