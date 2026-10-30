using System.Collections.Generic;
using System.Linq;
using Thuong.Emotes;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private const string EmoteRequestMessage = "thuong.online.emote.request.v1";
        private const string EmoteStateMessage = "thuong.online.emote.state.v1";
        private readonly ThuongEmoteAuthority emoteAuthority = new();
        private readonly Dictionary<int, ThuongEmoteState> emoteStates = new();
        private uint emoteSequence;
        private double nextLocalEmoteAt;
        [SerializeField] private Key emoteKey = Key.T;

        public Key EmoteKey => emoteKey;
        public Scene EmoteScene => game.gameObject.scene;
        public GamePhase EmotePhase => visiblePhase;
        public uint EmoteEpoch => matchEpoch;
        public double EmoteTime => network != null && network.IsListening ? network.ServerTime.Time : Time.unscaledTimeAsDouble;
        public float EmoteCooldownRemaining => (float)System.Math.Max(0, nextLocalEmoteAt - EmoteTime);
        public TMP_FontAsset EmoteFont => sceneHud?.phaseText?.font ?? TMP_Settings.defaultFontAsset;
        public bool IsEmoteSessionActive => !leavingScene && connected && network != null && network.IsListening;
        public bool CanExpressEmote => IsEmoteSessionActive && matchStarted && matchResult == null &&
            localMapReady && !IsPlayingTask && publicPlayers.Any(p => p.PlayerId == localPlayerId &&
                PlayerEmoteCatalog.CanExpress(p.Alive, visiblePhase));

        internal bool HasEmoteAvatar(int id) => avatars.TryGetValue(id, out var avatar) && avatar.Root != null;
        internal IEnumerable<ThuongEmoteView> VisibleEmotes
        {
            get
            {
                if (!IsEmoteSessionActive || !matchStarted || matchResult != null) yield break;
                foreach (var player in publicPlayers)
                {
                    if (!player.Alive || !emoteStates.TryGetValue(player.PlayerId, out var state) ||
                        state.ExpiresAt <= EmoteTime || state.Phase != visiblePhase || state.Day != visibleDay ||
                        !avatars.TryGetValue(player.PlayerId, out var avatar) || avatar.Root == null) continue;
                    yield return new ThuongEmoteView
                    {
                        PlayerId = player.PlayerId, Id = state.Id, ExpiresAt = state.ExpiresAt, Anchor = avatar.Root.transform
                    };
                }
            }
        }

        private void PrepareEmotes() => PlayerEmoteWheelUI.Ensure(this);
        private void RegisterEmoteMessages()
        {
            network.CustomMessagingManager.RegisterNamedMessageHandler(EmoteRequestMessage, ReceiveEmoteRequest);
            network.CustomMessagingManager.RegisterNamedMessageHandler(EmoteStateMessage, ReceiveEmoteState);
        }
        private void UnregisterEmoteMessages()
        {
            network?.CustomMessagingManager?.UnregisterNamedMessageHandler(EmoteRequestMessage);
            network?.CustomMessagingManager?.UnregisterNamedMessageHandler(EmoteStateMessage);
        }

        public void PauseForEmoteWheel()
        {
            if (IsEmoteSessionActive && matchStarted) SendMovementInput(Vector2.zero);
        }

        public bool TrySendEmote(int id)
        {
            if (!CanExpressEmote || !PlayerEmoteCatalog.IsValid(id) || EmoteCooldownRemaining > 0) return false;
            nextLocalEmoteAt = EmoteTime + PlayerEmoteCatalog.CooldownSeconds;
            SendEmoteRequest(id);
            return true;
        }

        private void SendEmoteRequest(int id)
        {
            uint sequence = ++emoteSequence;
            if (network.IsServer)
            {
                AcceptEmote(network.LocalClientId, matchEpoch, sequence, id, visiblePhase, visibleDay);
                return;
            }
            using var writer = new FastBufferWriter(14, Allocator.Temp);
            writer.WriteValueSafe(matchEpoch); writer.WriteValueSafe(sequence); writer.WriteValueSafe((byte)id);
            writer.WriteValueSafe((byte)visiblePhase); writer.WriteValueSafe(visibleDay);
            network.CustomMessagingManager.SendNamedMessage(EmoteRequestMessage, NetworkManager.ServerClientId, writer);
        }

        private void ReceiveEmoteRequest(ulong sender, FastBufferReader reader)
        {
            if (!network.IsServer || reader.Length - reader.Position != 14) return;
            reader.ReadValueSafe(out uint epoch); reader.ReadValueSafe(out uint sequence); reader.ReadValueSafe(out byte id);
            reader.ReadValueSafe(out byte phase); reader.ReadValueSafe(out int day);
            AcceptEmote(sender, epoch, sequence, id, (GamePhase)phase, day);
        }

        private bool AcceptEmote(ulong sender, uint epoch, uint sequence, int id, GamePhase phase, int day)
        {
            if (!network.IsServer || !serverPlayers.TryGetValue(sender, out var member)) return false;
            var player = players?.GetplayerByID(member.PlayerId);
            bool eligible = connected && matchStarted && matchResult == null && member.MapReady &&
                game != null && phase == game.currentPhase && day == game.currentDay &&
                !minigameAuthority.IsPlaying(sender) && player != null && PlayerEmoteCatalog.CanExpress(player.isAlive, phase);
            if (!emoteAuthority.TryAccept(sender, matchEpoch, epoch, sequence, id, eligible, EmoteTime)) return false;
            var state = new ThuongEmoteState
            {
                Id = id, ExpiresAt = EmoteTime + PlayerEmoteCatalog.DisplaySeconds, Phase = phase, Day = day
            };
            emoteStates[member.PlayerId] = state;
            using var writer = new FastBufferWriter(22, Allocator.Temp);
            writer.WriteValueSafe(matchEpoch); writer.WriteValueSafe(member.PlayerId); writer.WriteValueSafe((byte)id);
            writer.WriteValueSafe(state.ExpiresAt); writer.WriteValueSafe((byte)phase); writer.WriteValueSafe(day);
            foreach (ulong client in network.ConnectedClientsIds)
                if (client != network.LocalClientId)
                    network.CustomMessagingManager.SendNamedMessage(EmoteStateMessage, client, writer);
            return true;
        }

        private void ReceiveEmoteState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || network.IsServer || reader.Length - reader.Position != 22) return;
            reader.ReadValueSafe(out uint epoch); reader.ReadValueSafe(out int playerId); reader.ReadValueSafe(out byte id);
            reader.ReadValueSafe(out double expiresAt); reader.ReadValueSafe(out byte phase); reader.ReadValueSafe(out int day);
            if (epoch != matchEpoch || !PlayerEmoteCatalog.IsValid(id) || double.IsNaN(expiresAt) ||
                double.IsInfinity(expiresAt) || expiresAt <= EmoteTime || expiresAt > EmoteTime + PlayerEmoteCatalog.DisplaySeconds + 1) return;
            emoteStates[playerId] = new ThuongEmoteState { Id = id, ExpiresAt = expiresAt, Phase = (GamePhase)phase, Day = day };
        }

        private void ResetEmotes()
        {
            emoteAuthority.Clear(); emoteStates.Clear(); emoteSequence = 0; nextLocalEmoteAt = 0;
            PlayerEmoteWheelUI.CancelFor(this);
        }
    }
}
