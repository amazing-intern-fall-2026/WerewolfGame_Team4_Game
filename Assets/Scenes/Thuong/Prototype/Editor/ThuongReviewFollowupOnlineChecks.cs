using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Thuong.Online;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static partial class ThuongRoomUpgradeOnlineChecks
{
    private const string FollowupOutput = "Logs/ThuongFollowup20261113";

    [MenuItem("Tools/Thuong/Review Improvements/Check Guardian Notebook And Emote Hint")]
    public static void RunFollowupFixes()
    {
        ThuongEmoteSystemChecks.Run();
        ThuongReviewImprovementChecks.Run();
        Directory.CreateDirectory(FollowupOutput);
        SessionState.SetBool(RunKey + ".Followup", true);
        RunReviewSmoke();
    }

    private static IEnumerator ExerciseFollowupFixes()
    {
        Check(host.RestartMatchInRoom(), "Followup: completed match restarts for a real proximity round");
        while (clients.Any(client => client.MatchStarted || client.HasLocalRole)) yield return null;
        Check((bool)Call(host, "SetRoomRules", new ThuongRoomRules { preset = ThuongRolePreset.Basic,
            mode = ThuongMatchMode.RealtimeProximity, automaticTimes = false, day = 150, night = 90, discussion = 90, voting = 45 }),
            "Followup: proximity room accepts its supported roles");
        AddClient(2);
        while (Net(host).ConnectedClientsIds.Count != 4 || !(bool)Property(host, "AllMapsReady") ||
            clients.Any(client => client.RoomRules.mode != ThuongMatchMode.RealtimeProximity)) yield return null;
        NetworkGameRoleSetup.Save(new List<RoleAmountData> { new(RoleType.VillageGuardian, 2), new(RoleType.DogSpirit, 1), new(RoleType.Seer, 1) });
        Call(host, "AssignLobbyRolesOnServer");
        while (clients.Any(client => !client.HasLocalRole)) yield return null;
        var actionType = typeof(ThuongOnlineSession).Assembly.GetType("Thuong.Online.ThuongOnlineAction");
        var ability = Enum.Parse(actionType, "RealtimeAbility");
        foreach (var member in Members())
        {
            Call(host, "ApplyServerAction", Get<ulong>(member, "ClientId"), 1u, Enum.Parse(actionType, "RevealRead"), -1, -1,
                GameRoleManager.Instance.currentDay, GameRoleManager.Instance.currentPhase, host.MatchEpoch);
            Call(host, "SetServerReady", Get<ulong>(member, "ClientId"), true);
        }
        Call(host, "StartMatchOnServer");
        while (clients.Any(client => !client.MatchStarted)) yield return null;
        yield return null;
        var ui = CaptureFollowupUi();
        while (ui.MoveNext()) yield return ui.Current;

        var guardian = clients.First(client => Get<RoleType>(client, "privateRole") == RoleType.VillageGuardian);
        var players = PlayerManager.Instance;
        var guard = players.GetplayerByID(guardian.LocalPlayerId);
        object guardMember = Members().Single(member => Get<int>(member, "PlayerId") == guard.playerID);
        var wolf = players.players.Single(player => player.roleType == RoleType.DogSpirit);
        var seer = players.players.Single(player => player.roleType == RoleType.Seer);
        var other = players.players.Single(player => player.roleType == RoleType.VillageGuardian && player != guard);
        object Member(int id) => Members().Single(member => Get<int>(member, "PlayerId") == id);
        ThuongOnlineSession Source(int id) => Get<ulong>(Member(id), "ClientId") == Net(host).LocalClientId ? host
            : clients.Single(client => client.LocalPlayerId == id);
        void Request(int actorId, int targetId)
        {
            var source = Source(actorId);
            Set(source, "requestId", Get<uint>(Member(actorId), "LastRequestId") + 1);
            Call(source, "SendAction", ability, targetId, -1);
        }
        var game = GameRoleManager.Instance;
        game.EndDay(); Call(host, "BroadcastSnapshot"); Call(host, "SendAllPrivateStates");
        while (clients.Any(client => client.VisibleState != GameState.Night)) yield return null;
        // The production reader runs on an inactive proxy in this one-process NGO
        // harness. Give its local interaction picker an avatar anchor explicitly.
        var avatarType = typeof(ThuongOnlineSession).Assembly.GetType("Thuong.Online.ThuongOnlineAvatar");
        var anchor = new GameObject("Followup remote guardian anchor");
        anchor.transform.position = Get<Vector2>(guardMember, "Position");
        var avatar = Activator.CreateInstance(avatarType); Set(avatar, "Root", anchor);
        Get<IDictionary>(guardian, "avatars")[guard.playerID] = avatar;
        bool OffersSelf() => ((IEnumerable)Call(guardian, "BuildInteractionOptions")).Cast<object>()
            .Any(option => Get<int>(option, "Id") == guard.playerID && Get<string>(option, "Label") == "Bảo vệ bản thân");
        Check(OffersSelf(), "Followup: remote guardian interaction menu offers self protection");
        Check(guardian.LocalRoleInformation().SkillDescription.Contains("bản thân"), "Followup: proximity role card explains self protection");
        uint before = Get<uint>(guardMember, "LastRequestId"); Request(guard.playerID, guard.playerID);
        while (Get<uint>(guardMember, "LastRequestId") == before || (Get<ushort>(guardian, "privateFlags") & 4) == 0) yield return null;
        Check(guard.status.isProtected && guard.hasUseNightAction, "Followup: authenticated remote self protection applies on the host");
        Check(!OffersSelf(), "Followup: used protection disappears from the remote interaction menu");
        before = Get<uint>(guardMember, "LastRequestId"); Request(guard.playerID, other.playerID);
        while (Get<uint>(guardMember, "LastRequestId") == before) yield return null;
        Check(!other.status.isProtected, "Followup: guardian cannot spend a second protection in the same night");
        foreach (var actor in new[] { wolf, seer })
        {
            object member = Member(actor.playerID); before = Get<uint>(member, "LastRequestId"); Request(actor.playerID, actor.playerID);
            while (Get<uint>(member, "LastRequestId") == before) yield return null;
            Check(actor.isAlive && !actor.hasUseNightAction && !actor.status.isProtected,
                "Followup: " + actor.roleType + " cannot self target or consume its night action");
        }
        Set(Member(wolf.playerID), "Position", Get<Vector2>(guardMember, "Position"));
        object wolfMember = Member(wolf.playerID); before = Get<uint>(wolfMember, "LastRequestId"); Request(wolf.playerID, guard.playerID);
        while (Get<uint>(wolfMember, "LastRequestId") == before) yield return null;
        Check(guard.isAlive && wolf.hasUseNightAction, "Followup: self protection blocks a real proximity wolf attack");

        void NextNight()
        {
            game.FinishNight(); game.StartVoting(); game.FinishVoting(); game.EndDay();
            Call(host, "BroadcastSnapshot"); Call(host, "SendAllPrivateStates");
        }
        NextNight();
        while (clients.Any(client => Get<int>(client, "visibleDay") != 2) || (Get<ushort>(guardian, "privateFlags") & 4) != 0) yield return null;
        Check(!OffersSelf(), "Followup: self protection is not offered on the next consecutive night");
        before = Get<uint>(guardMember, "LastRequestId"); Request(guard.playerID, guard.playerID);
        while (Get<uint>(guardMember, "LastRequestId") == before) yield return null;
        Check(!guard.hasUseNightAction && !guard.status.isProtected, "Followup: forged consecutive self protection is rejected without spending the turn");
        Vector2 guardPosition = Get<Vector2>(guardMember, "Position");
        Set(Member(other.playerID), "Position", guardPosition + Vector2.right * 4);
        before = Get<uint>(guardMember, "LastRequestId"); Request(guard.playerID, other.playerID);
        while (Get<uint>(guardMember, "LastRequestId") == before) yield return null;
        Check(!guard.hasUseNightAction && !other.status.isProtected, "Followup: remote protection still enforces interaction range");
        Set(Member(other.playerID), "Position", guardPosition);
        before = Get<uint>(guardMember, "LastRequestId"); Request(guard.playerID, other.playerID);
        while (Get<uint>(guardMember, "LastRequestId") == before) yield return null;
        Check(other.status.isProtected && guard.hasUseNightAction, "Followup: a different nearby target remains valid on night two");
        NextNight();
        while (Get<int>(guardian, "visibleDay") != 3) yield return null;
        before = Get<uint>(guardMember, "LastRequestId"); Request(guard.playerID, guard.playerID);
        while (Get<uint>(guardMember, "LastRequestId") == before) yield return null;
        Check(guard.status.isProtected, "Followup: guardian can self protect again after protecting someone else");
        NextNight(); NextNight();
        while (Get<int>(guardian, "visibleDay") != 5) yield return null;
        before = Get<uint>(guardMember, "LastRequestId"); Request(guard.playerID, guard.playerID);
        while (Get<uint>(guardMember, "LastRequestId") == before) yield return null;
        Check(guard.status.isProtected, "Followup: skipping a night also breaks the consecutive-target restriction");
        game.FinishNight(); game.StartVoting(); game.FinishVoting();
        Check(game.StartReportedMeeting(), "Followup: an extra daytime meeting can advance the day without a new night");
        game.StartVoting(); game.FinishVoting(); game.EndDay();
        Call(host, "BroadcastSnapshot"); Call(host, "SendAllPrivateStates");
        while (Get<int>(guardian, "visibleDay") != 7) yield return null;
        before = Get<uint>(guardMember, "LastRequestId"); Request(guard.playerID, guard.playerID);
        while (Get<uint>(guardMember, "LastRequestId") == before) yield return null;
        Check(NightManager.Instance.CurrentNightNumber == 6 && !guard.hasUseNightAction && !guard.status.isProtected,
            "Followup: an extra daytime meeting cannot bypass the consecutive-night protection rule");
        Call(host, "ResetCorpseState");
        Check(Get<IDictionary>(host, "realtimeGuardianTargets").Count == 0, "Followup: round reset clears guardian target history");
        NetworkGameRoleSetup.Clear();
    }

    private static IEnumerator CaptureFollowupUi()
    {
        var hud = UnityEngine.Object.FindAnyObjectByType<GameHUD>();
        var instructions = hud.transform.Find("TaskPanel/Instructions").GetComponent<TMP_Text>();
        Check(instructions.text.Contains($"Giữ [{host.EmoteKey}]") && !UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Any(label => label.name == "Keyboard hint"), "Followup: emote instructions live in the sidebar instead of over the footer buttons");
        Check(instructions.text.Contains("Giữ E") && hud.transform.Find("Footer").GetComponent<TMP_Text>().text.Contains("4 NGƯỜI"),
            "Followup: sidebar and player count refresh when the next round changes mode and roster size");
        Call(host, "BindNotebookToRound", host.MatchEpoch); Call(host, "ToggleNotebook");
        var notes = Get<List<string>>(host, "notebookEntries");
        for (int i = 1; i <= 50; i++) notes.Add($"Ngày 1 · Ghi chú {i:00}: Quan sát người chơi tại chuông làng. " + new string('x', 180));
        Call(host, "RefreshNotebookHistory");
        yield return null;
        var history = Get<TMP_Text>(host, "notebookHistory");
        var scroll = Get<ScrollRect>(host, "notebookScroll");
        Check(history.text.Contains("Ghi chú 01:") && history.text.Contains("Ghi chú 50:"), "Followup: all fifty saved notes are rendered");
        Check(scroll.content.rect.height > scroll.viewport.rect.height && !history.enableAutoSizing,
            "Followup: long history grows inside a scrollable viewport with readable font size");
        scroll.verticalScrollbar.value = 1;
        Check(scroll.verticalNormalizedPosition > .99f, "Followup: scrollbar can reach the oldest note");
        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
        {
            ThuongMedievalSceneChecks.SavePreview($"{FollowupOutput}/notebook-{size.x}x{size.y}.png", size.x, size.y, camera =>
            {
                InspectNotebookGlyph(scroll, history, false);
                Rect Bounds(RectTransform rect)
                {
                    var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                    var points = corners.Select(point => camera.WorldToScreenPoint(point)).ToArray();
                    return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
                }
                Rect input = Bounds((RectTransform)Get<TMP_InputField>(host, "notebookInput").transform);
                Check(!Get<RectTransform>(host, "notebookPanel").GetComponentsInChildren<Button>().Any(button => input.Overlaps(Bounds((RectTransform)button.transform))),
                    "Followup: notebook input and buttons do not overlap at " + size);
            });
        }
        yield return null;
        scroll.verticalScrollbar.value = 0;
        Check(scroll.verticalNormalizedPosition < .01f, "Followup: scrollbar can reach the newest note");
        scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, 3) });
        Check(scroll.verticalNormalizedPosition > 0, "Followup: mouse wheel scrolls the history toward older notes");
        Get<TMP_InputField>(host, "notebookInput").text = "Mục mới số 51";
        Call(host, "SaveNotebookEntry");
        Check(notes.Count == 50 && !history.text.Contains("Ghi chú 01:") && history.text.Contains("Mục mới số 51") && scroll.verticalNormalizedPosition < .01f,
            "Followup: saving note 51 keeps the latest fifty and scrolls to the new entry");
        yield return null;
        InspectNotebookGlyph(scroll, history, true, false);
        var save = Get<RectTransform>(host, "notebookPanel").GetComponentsInChildren<Button>()
            .Single(button => button.GetComponentInChildren<TMP_Text>().text == "GHI");
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)save.transform).TransformPoint(((RectTransform)save.transform).rect.center))
        };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(save.transform),
            "Followup: notebook Save button receives clicks above village controls");
        ThuongMedievalSceneChecks.SavePreview(FollowupOutput + "/notebook-latest.png", 1280, 720,
            _ => InspectNotebookGlyph(scroll, history, true));
        Call(host, "ToggleNotebook");
        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
            ThuongMedievalSceneChecks.SavePreview($"{FollowupOutput}/gameplay-{size.x}x{size.y}.png", size.x, size.y);
    }

    private static void InspectNotebookGlyph(ScrollRect scroll, TMP_Text history, bool latest, bool reposition = true)
    {
        if (reposition)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(history.rectTransform);
            scroll.Rebuild(CanvasUpdate.PostLayout);
            scroll.verticalNormalizedPosition = latest ? 0 : 1;
        }
        history.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        var visible = history.textInfo.characterInfo.Take(history.textInfo.characterCount).Where(character => character.isVisible);
        var glyph = latest ? visible.Last() : visible.First();
        Vector2 center = scroll.viewport.InverseTransformPoint(history.rectTransform.TransformPoint((glyph.topLeft + glyph.bottomRight) / 2));
        Debug.Log($"FOLLOWUP_NOTEBOOK_GEOMETRY: latest={latest} center={center} viewport={scroll.viewport.rect} contentHeight={scroll.content.rect.height} preferred={history.preferredHeight}");
        Check(scroll.viewport.rect.Contains(center), latest
            ? "Followup: the newest note has visible glyphs inside the viewport"
            : "Followup: the oldest note has visible glyphs inside the viewport");
    }
}
