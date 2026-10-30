#if UNITY_EDITOR || THUONG_EMOTE_QA
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Thuong.Emotes;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private double emoteQaStartAt = double.PositiveInfinity;
        private int emoteQaChecks;
        private bool emoteQaPeerLocked;

        internal IEnumerator RunEmoteQa(bool host, string output)
        {
            Application.runInBackground = true;
            portText = "17987"; address = "127.0.0.1"; playerName = host ? "Thuong Host" : "Thuong Client";
            SetLobbyCapacity(2);
            if (host) HostLan(); else JoinLan();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!connected || network.CustomMessagingManager == null || host && serverPlayers.Count < 2)
            { EmoteQaDeadline(deadline); yield return null; }
            network.CustomMessagingManager.RegisterNamedMessageHandler("thuong.emote.qa.begin", (sender, reader) =>
            { if (sender == NetworkManager.ServerClientId) reader.ReadValueSafe(out emoteQaStartAt); });
            network.CustomMessagingManager.RegisterNamedMessageHandler("thuong.emote.qa.locked", (sender, reader) =>
            { if (network.IsServer && serverPlayers.ContainsKey(sender)) emoteQaPeerLocked = true; });
            if (host)
            {
                while (!AllMapsReady) { EmoteQaDeadline(deadline); yield return null; }
                AssignLobbyRolesOnServer();
            }
            while (!matchStarted)
            {
                EmoteQaDeadline(deadline);
                if (HasLocalRole && !localRoleConfirmed) { ViewLocalRole(); ReadLocalRole(); ToggleReady(); }
                if (host && rolesAssigned && serverPlayers.Values.All(p => p.Ready)) StartMatchOnServer();
                yield return null;
            }
            if (host)
            {
                game.enabled = false; DayTimer.Instance?.StopTimer();
                game.SetPhase(GamePhase.Task); BroadcastSnapshot();
            }
            while (!CanExpressEmote || avatars.Count < 2) { EmoteQaDeadline(deadline); yield return null; }
            int remoteId = publicPlayers.First(p => p.PlayerId != localPlayerId).PlayerId;
            if (host)
            {
                yield return new WaitForSecondsRealtime(1);
                emoteQaStartAt = EmoteTime + 1;
                ulong peer = serverPlayers.Keys.First(id => id != network.LocalClientId);
                using var writer = new FastBufferWriter(8, Allocator.Temp);
                writer.WriteValueSafe(emoteQaStartAt);
                network.CustomMessagingManager.SendNamedMessage("thuong.emote.qa.begin", peer, writer);
            }
            while (EmoteTime < emoteQaStartAt) { EmoteQaDeadline(deadline); yield return null; }
            EmoteQaCheck(TrySendEmote(host ? 1 : 8), "Thuong session sends an emote");
            EmoteQaCheck(!TrySendEmote(3), "Local cooldown blocks spam");
            while (EmoteTime < emoteQaStartAt + .5) yield return null;
            EmoteQaCheck(emoteStates.TryGetValue(localPlayerId, out var own) && own.Id == (host ? 1 : 8), "Host confirms own emote");
            EmoteQaCheck(emoteStates.TryGetValue(remoteId, out var remote) && remote.Id == (host ? 8 : 1), "Other player's emote arrives");
            EmoteQaCheck(VisibleEmotes.Count() == 2, "Expressions attach to Thuong avatars");
            double expires = own.ExpiresAt;
            SendEmoteRequest(2);
            while (EmoteTime < emoteQaStartAt + 1) yield return null;
            EmoteQaCheck(emoteStates[localPlayerId].ExpiresAt == expires, "Host blocks bypassed cooldown");
            if (host)
            {
                EmoteQaCheck(!AcceptEmote(ulong.MaxValue, matchEpoch, 1, 1, visiblePhase, visibleDay), "Unknown sender rejected");
                var ui = FindAnyObjectByType<PlayerEmoteWheelUI>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(PlayerEmoteWheelUI).GetMethod("OpenWheel", flags).Invoke(ui, null);
                typeof(PlayerEmoteWheelUI).GetMethod("SetSelection", flags).Invoke(ui, new object[] { 8 });
                EmoteQaCheck(PlayerEmoteWheelUI.IsOpenFor(this), "Wheel belongs to Thuong session");
                EmoteQaCheck(serverPlayers[network.LocalClientId].Input == Vector2.zero, "Opening wheel stops movement");
                yield return new WaitForEndOfFrame();
                CaptureEmoteQa(ui, Path.Combine(Path.GetDirectoryName(output), "thuong-player-wheel.png"));
                typeof(PlayerEmoteWheelUI).GetMethod("CloseWheel", flags).Invoke(ui, new object[] { false });
                EmoteQaCheck(!PlayerEmoteWheelUI.IsOpen, "Cancel releases input");
            }
            while (EmoteTime < emoteQaStartAt + 2) yield return null;
            SendEmoteRequest(255);
            while (EmoteTime < emoteQaStartAt + 2.5) yield return null;
            EmoteQaCheck(emoteStates[localPlayerId].ExpiresAt == expires, "Invalid expression rejected");
            while (EmoteTime < emoteQaStartAt + 3.6) yield return null;
            EmoteQaCheck(!VisibleEmotes.Any(), "Both expressions expire on server time");
            if (host) { game.SetPhase(GamePhase.ResolveNight); BroadcastSnapshot(); }
            deadline = Time.realtimeSinceStartup + 15;
            while (visiblePhase != GamePhase.ResolveNight) { EmoteQaDeadline(deadline); yield return null; }
            EmoteQaCheck(!TrySendEmote(1), "Resolve phase blocks expressions");
            if (host)
            {
                while (!emoteQaPeerLocked) { EmoteQaDeadline(deadline); yield return null; }
                game.SetPhase(GamePhase.Task); BroadcastSnapshot();
            }
            else
            {
                using var receipt = new FastBufferWriter(1, Allocator.Temp);
                receipt.WriteValueSafe(true);
                network.CustomMessagingManager.SendNamedMessage("thuong.emote.qa.locked", NetworkManager.ServerClientId, receipt);
            }
            while (visiblePhase != GamePhase.Task) { EmoteQaDeadline(deadline); yield return null; }
            EmoteQaCheck(TrySendEmote(5), "Expressions work after phase resumes");
            while (!emoteStates.TryGetValue(remoteId, out var resumed) || resumed.Id != 5)
            { EmoteQaDeadline(deadline); yield return null; }
            ResetEmotes();
            EmoteQaCheck(emoteStates.Count == 0 && EmoteCooldownRemaining == 0, "Reset clears emotes and cooldown");
            File.WriteAllText(output, "PASS: " + emoteQaChecks + " Thuong real-peer checks\n");
            yield return new WaitForSecondsRealtime(host ? 2 : .3f);
            network.Shutdown();
            yield return null;
            Application.Quit(0);
        }

        private void CaptureEmoteQa(PlayerEmoteWheelUI ui, string path)
        {
            var camera = Camera.main; var canvas = ui.GetComponent<Canvas>();
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var previousViewport = camera.rect;
            int previousOrder = canvas.sortingOrder;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                camera.rect = new Rect(0, 0, 1, 1);
                canvas.sortingOrder = 32760;
                camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                EmoteQaCheck(texture.GetPixels32().Count(p => p.r > 150 && p.g > 100) > 5000, "Thuong player renders wheel");
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                camera.rect = previousViewport;
                canvas.sortingOrder = previousOrder;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
                target.Release(); Destroy(target); Destroy(texture);
            }
        }
        private static void EmoteQaDeadline(float deadline)
        { if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Thuong emote QA connection/setup timed out"); }
        private void EmoteQaCheck(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Thuong emote QA: " + label);
            emoteQaChecks++; Debug.Log("THUONG EMOTE QA PASS: " + label);
        }
    }

    internal sealed class ThuongEmoteQaDriver : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Environment.GetCommandLineArgs().Contains("--thuong-emote-qa"))
                new GameObject("Thuong emote QA driver").AddComponent<ThuongEmoteQaDriver>();
        }
        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--thuong-emote-qa");
            float deadline = Time.realtimeSinceStartup + 30;
            while (ThuongOnlineSession.Active == null && Time.realtimeSinceStartup < deadline) yield return null;
            string output = args[index + 2];
            var run = ThuongOnlineSession.Active.RunEmoteQa(args[index + 1] == "host", output);
            while (true)
            {
                bool next; object current = null;
                try { next = run.MoveNext(); if (next) current = run.Current; }
                catch (Exception error)
                { File.WriteAllText(output, "FAIL: " + error); Debug.LogException(error); Application.Quit(1); yield break; }
                if (!next) break;
                yield return current;
            }
        }
    }
}
#endif
