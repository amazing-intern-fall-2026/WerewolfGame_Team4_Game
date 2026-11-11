using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private const string CommunicationPolicy = "thuong.communication.policy.v1";
        private const string CommunicationAck = "thuong.communication.ack.v1";
        private const string ChatRequest = "thuong.communication.chat-request.v1";
        private const string ChatDelivery = "thuong.communication.chat.v1";
        private readonly Dictionary<ulong, uint> communicationAcks = new(), chatSequences = new();
        private readonly Dictionary<ulong, double> chatLastSent = new();
        private readonly string[] voiceGroups = new string[4];
        private string communicationFingerprint, localVoiceGroup;
        private uint communicationRevision, localCommunicationRevision, chatSequence;
        private uint localCommunicationEpoch;
        private double nextCommunicationSend, nextVoiceAttempt;
        private ThuongCommunicationAudience communicationAudience;
        private ThuongCommunicationAudience readableChatAudience;
        private GameObject communicationRoot;
        private NetworkChatUI communicationUi;
        private NetworkChatManager communicationChat;
        private TMP_Text communicationLabel, microphoneLabel, speakerLabel;
        private Button microphoneButton, speakerButton;
        private NetworkVoiceManager communicationVoice;
        private int voiceRequestVersion;
        private bool communicationDisposed;
        private double nextLocalChatAt;
        private readonly List<(uint epoch, uint revision, byte audience, NetworkChatManager.ChatMessageEntry entry)> pendingChat = new();

        public bool CanSendChat => connected && network != null && network.IsListening && !leavingScene &&
            localCommunicationEpoch == matchEpoch && communicationAudience != ThuongCommunicationAudience.None;
        public string ChatPermissionReason => CanSendChat ? ThuongCommunicationRules.Description(communicationAudience)
            : "Chưa được trao đổi trong pha này.";
        public ThuongCommunicationAudience CommunicationAudience => CanSendChat ? communicationAudience : ThuongCommunicationAudience.None;

        private void PrepareCommunications()
        {
            communicationRoot = new GameObject("Thuong Communications", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            communicationRoot.transform.SetParent(transform, false);
            var canvas = communicationRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1800;
            var scaler = communicationRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            communicationChat = NetworkChatManager.Instance;
            if (communicationChat == null)
            {
                var root = new GameObject("Thuong Chat Messages"); root.transform.SetParent(communicationRoot.transform, false);
                communicationChat = root.AddComponent<NetworkChatManager>();
            }
            communicationChat.ClearHistory();
            communicationUi = communicationRoot.AddComponent<NetworkChatUI>();
            var bar = LobbyPanel("Communication controls", (RectTransform)communicationRoot.transform,
                new Vector2(.745f, .705f), new Vector2(.99f, .785f), false);
            bar.GetComponent<Image>().color = new Color32(32, 31, 46, 245);
            microphoneButton = LobbyButton("Microphone", bar, "MIC: TẮT", new Vector2(0, .36f), new Vector2(.48f, 1), () => communicationVoice?.ToggleMicrophone());
            microphoneLabel = microphoneButton.GetComponentInChildren<TMP_Text>();
            speakerButton = LobbyButton("Speaker", bar, "LOA: BẬT", new Vector2(.51f, .36f), Vector2.one,
                () => communicationVoice?.ToggleOutputAudio());
            speakerLabel = speakerButton.GetComponentInChildren<TMP_Text>();
            communicationLabel = LobbyText("Communication permission", bar, "", Vector2.zero, new Vector2(1, .36f), 13);
            communicationLabel.color = new Color32(247, 232, 193, 255);
            communicationLabel.fontSizeMin = 11;
            communicationRoot.SetActive(false);
        }

        private void RegisterCommunicationMessages()
        {
            var messaging = network.CustomMessagingManager;
            messaging.RegisterNamedMessageHandler(CommunicationPolicy, ReceiveCommunicationPolicy);
            messaging.RegisterNamedMessageHandler(CommunicationAck, ReceiveCommunicationAck);
            messaging.RegisterNamedMessageHandler(ChatRequest, ReceiveChatRequest);
            messaging.RegisterNamedMessageHandler(ChatDelivery, ReceiveChatDelivery);
        }

        private void UnregisterCommunicationMessages()
        {
            var messaging = network?.CustomMessagingManager;
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(CommunicationPolicy);
            messaging.UnregisterNamedMessageHandler(CommunicationAck);
            messaging.UnregisterNamedMessageHandler(ChatRequest);
            messaging.UnregisterNamedMessageHandler(ChatDelivery);
        }

        private ThuongCommunicationAudience ServerAudience(ThuongOnlineServerPlayer member)
        {
            bool available = connected && !leavingScene && !member.Offline && network.IsListening &&
                network.ConnectedClientsIds.Contains(member.ClientId) && (!matchStarted || member.MapReady);
            bool alive = players?.GetplayerByID(member.PlayerId)?.isAlive ?? true;
            return ThuongCommunicationRules.Resolve(available, matchStarted, matchResult != null, alive, game.currentPhase);
        }

        private void RefreshServerCommunications()
        {
            if (!connected || !network.IsServer || !network.IsListening || game == null || leavingScene) return;
            string signature = $"{matchEpoch}/{game.currentDay}/{game.currentPhase}/{matchStarted}/{matchResult != null}/" +
                string.Join(";", serverPlayers.Values.OrderBy(member => member.ClientId).Select(member => $"{member.ClientId}:{ServerAudience(member)}"));
            if (signature == communicationFingerprint) return;
            communicationFingerprint = signature;
            communicationRevision++;
            communicationAcks.Clear();
            // A player changing groups must not retain the current living voice channel.
            for (int i = 1; i < voiceGroups.Length; i++) voiceGroups[i] = "ww-" + Guid.NewGuid().ToString("N");
            nextCommunicationSend = 0;
        }

        private void UpdateCommunications()
        {
            if (communicationDisposed || communicationRoot == null) return;
            bool active = connected && network != null && network.IsListening && !leavingScene;
            communicationRoot.SetActive(active);
            if (!active)
            {
                if (!string.IsNullOrEmpty(localVoiceGroup) || communicationAudience != ThuongCommunicationAudience.None)
                    ApplyCommunicationPolicy(matchEpoch, 0, ThuongCommunicationAudience.None, string.Empty);
                return;
            }
            if (network.IsServer)
            {
                RefreshServerCommunications();
                if (Time.unscaledTimeAsDouble >= nextCommunicationSend)
                {
                    nextCommunicationSend = Time.unscaledTimeAsDouble + .5;
                    foreach (var member in serverPlayers.Values)
                    {
                        if (!network.ConnectedClientsIds.Contains(member.ClientId) ||
                            communicationAcks.TryGetValue(member.ClientId, out uint ack) && ack == communicationRevision) continue;
                        var audience = ServerAudience(member);
                        string channel = audience == ThuongCommunicationAudience.None ? string.Empty : voiceGroups[(int)audience];
                        if (member.ClientId == network.LocalClientId)
                        {
                            ApplyCommunicationPolicy(matchEpoch, communicationRevision, audience, channel);
                            communicationAcks[member.ClientId] = communicationRevision;
                        }
                        else
                        {
                            using var writer = new FastBufferWriter(100, Allocator.Temp);
                            writer.WriteValueSafe(matchEpoch); writer.WriteValueSafe(communicationRevision);
                            writer.WriteValueSafe((byte)audience); writer.WriteValueSafe(new FixedString64Bytes(channel));
                            network.CustomMessagingManager.SendNamedMessage(CommunicationPolicy, member.ClientId, writer);
                        }
                    }
                }
            }
            bool permitted = CanSendChat;
            communicationUi.SetCommunicationAvailability(permitted, ChatPermissionReason);
            bool voiceReady = communicationVoice != null && communicationVoice.IsInLobbyVoice &&
                communicationVoice.CurrentChannelName == localVoiceGroup && permitted;
            microphoneButton.interactable = voiceReady;
            speakerButton.interactable = voiceReady;
            microphoneLabel.text = communicationVoice != null && !communicationVoice.IsMicrophoneMuted ? "MIC: BẬT" : "MIC: TẮT";
            speakerLabel.text = communicationVoice == null || !communicationVoice.IsOutputMuted ? "LOA: BẬT" : "LOA: TẮT";
            communicationLabel.text = !permitted ? ChatPermissionReason : voiceReady ? ChatPermissionReason : "Chat sẵn sàng · Thoại chưa kết nối";
            if (permitted && !voiceReady && AuthManager.Instance.IsSignedIn && Time.unscaledTimeAsDouble >= nextVoiceAttempt)
            {
                nextVoiceAttempt = Time.unscaledTimeAsDouble + 10;
                _ = ChangeVoiceGroupAsync(localVoiceGroup);
            }
        }

        private void ReceiveCommunicationPolicy(ulong sender, FastBufferReader reader)
        {
            if (network.IsServer || sender != NetworkManager.ServerClientId) return;
            try
            {
                reader.ReadValueSafe(out uint epoch); reader.ReadValueSafe(out uint revision);
                reader.ReadValueSafe(out byte audience); reader.ReadValueSafe(out FixedString64Bytes group);
                if (epoch != matchEpoch || revision < localCommunicationRevision || audience > (byte)ThuongCommunicationAudience.Dead || reader.Position != reader.Length) return;
                ApplyCommunicationPolicy(epoch, revision, (ThuongCommunicationAudience)audience, group.ToString());
                using var writer = new FastBufferWriter(8, Allocator.Temp);
                writer.WriteValueSafe(epoch); writer.WriteValueSafe(revision);
                network.CustomMessagingManager.SendNamedMessage(CommunicationAck, NetworkManager.ServerClientId, writer);
            }
            catch (Exception error) when (error is OverflowException || error is ArgumentException)
            { Debug.LogWarning("[Thuong chat] Bỏ qua quyền trao đổi không hợp lệ."); }
        }

        private void ReceiveCommunicationAck(ulong sender, FastBufferReader reader)
        {
            if (!network.IsServer || reader.Length - reader.Position != 8 || !serverPlayers.ContainsKey(sender)) return;
            reader.ReadValueSafe(out uint epoch); reader.ReadValueSafe(out uint revision);
            if (epoch == matchEpoch && revision == communicationRevision) communicationAcks[sender] = revision;
        }

        private void ApplyCommunicationPolicy(uint epoch, uint revision, ThuongCommunicationAudience audience, string group)
        {
            bool changed = audience != communicationAudience || epoch != localCommunicationEpoch || localVoiceGroup != group;
            if (epoch != localCommunicationEpoch || audience != ThuongCommunicationAudience.None && audience != readableChatAudience)
                communicationChat?.ClearHistory();
            if (audience != ThuongCommunicationAudience.None) readableChatAudience = audience;
            localCommunicationEpoch = epoch; localCommunicationRevision = revision;
            communicationAudience = audience; localVoiceGroup = group;
            foreach (var message in pendingChat.ToArray())
            {
                if (message.epoch == epoch && message.revision == revision && message.audience == (byte)audience)
                    communicationChat?.ReceiveThuongMessage(message.entry);
                if (message.epoch != epoch || message.revision <= revision) pendingChat.Remove(message);
            }
            if (changed)
            {
                nextVoiceAttempt = Time.unscaledTimeAsDouble + 10;
                _ = ChangeVoiceGroupAsync(group);
            }
        }

        private async Task ChangeVoiceGroupAsync(string group)
        {
            int version = ++voiceRequestVersion;
            if (communicationVoice == null)
            {
                communicationVoice = NetworkVoiceManager.Existing;
                if (communicationVoice == null && AuthManager.Instance.IsSignedIn) communicationVoice = NetworkVoiceManager.Instance;
                if (communicationVoice == null) return;
            }
            var voice = communicationVoice;
            voice.SetGameplayPermissions(this, false, false);
            await voice.LeaveVoiceChannelAsync();
            if (this == null || communicationDisposed || version != voiceRequestVersion || !connected || string.IsNullOrEmpty(group) || !AuthManager.Instance.IsSignedIn) return;
            await voice.JoinVoiceChannelAsync(group);
            if (this == null || communicationDisposed || version != voiceRequestVersion || group != localVoiceGroup || !CanSendChat) return;
            voice.SetGameplayPermissions(this, voice.IsInLobbyVoice && voice.CurrentChannelName == group, voice.IsInLobbyVoice && voice.CurrentChannelName == group);
        }

        public bool SendChatText(string rawText)
        {
            if (!CanSendChat || Time.unscaledTimeAsDouble < nextLocalChatAt || !TrySanitizeChat(rawText, out string text)) return false;
            uint sequence = ++chatSequence;
            if (network.IsServer)
            {
                bool accepted = AcceptChat(network.LocalClientId, matchEpoch, localCommunicationRevision, sequence, text);
                if (accepted) nextLocalChatAt = Time.unscaledTimeAsDouble + .75;
                return accepted;
            }
            using var writer = new FastBufferWriter(560, Allocator.Temp);
            writer.WriteValueSafe(matchEpoch); writer.WriteValueSafe(localCommunicationRevision); writer.WriteValueSafe(sequence);
            writer.WriteValueSafe(new FixedString512Bytes(text));
            network.CustomMessagingManager.SendNamedMessage(ChatRequest, NetworkManager.ServerClientId, writer);
            nextLocalChatAt = Time.unscaledTimeAsDouble + .75;
            return true;
        }

        private void ReceiveChatRequest(ulong sender, FastBufferReader reader)
        {
            if (!network.IsServer) return;
            try
            {
                reader.ReadValueSafe(out uint epoch); reader.ReadValueSafe(out uint revision); reader.ReadValueSafe(out uint sequence);
                reader.ReadValueSafe(out FixedString512Bytes text);
                if (reader.Position == reader.Length) AcceptChat(sender, epoch, revision, sequence, text.ToString());
            }
            catch (Exception error) when (error is OverflowException || error is ArgumentException)
            { Debug.LogWarning("[Thuong chat] Bỏ qua tin nhắn không hợp lệ."); }
        }

        private bool AcceptChat(ulong sender, uint epoch, uint revision, uint sequence, string rawText)
        {
            RefreshServerCommunications();
            double now = network.ServerTime.Time;
            if (epoch != matchEpoch || revision != communicationRevision || sequence == 0 ||
                !serverPlayers.TryGetValue(sender, out var member) ||
                chatSequences.TryGetValue(sender, out uint previous) && sequence <= previous ||
                chatLastSent.TryGetValue(sender, out double last) && now - last < .75 ||
                !TrySanitizeChat(rawText, out string text)) return false;
            var audience = ServerAudience(member);
            if (audience == ThuongCommunicationAudience.None) return false;
            chatSequences[sender] = sequence; chatLastSent[sender] = now;
            var entry = new NetworkChatManager.ChatMessageEntry
            {
                SenderClientId = sender, SenderName = Fixed64(SafeChatText(member.Name)),
                MessageText = new FixedString512Bytes(text), TimestampTicks = DateTime.UtcNow.Ticks
            };
            foreach (var recipient in serverPlayers.Values)
            {
                if (ServerAudience(recipient) != audience) continue;
                if (recipient.ClientId == network.LocalClientId) communicationChat?.ReceiveThuongMessage(entry);
                else
                {
                    using var writer = new FastBufferWriter(700, Allocator.Temp);
                    writer.WriteValueSafe(matchEpoch); writer.WriteValueSafe(communicationRevision); writer.WriteValueSafe((byte)audience);
                    writer.WriteValueSafe(entry.SenderClientId); writer.WriteValueSafe(entry.SenderName);
                    writer.WriteValueSafe(entry.MessageText); writer.WriteValueSafe(entry.TimestampTicks);
                    network.CustomMessagingManager.SendNamedMessage(ChatDelivery, recipient.ClientId, writer);
                }
            }
            return true;
        }

        private void ReceiveChatDelivery(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || network.IsServer) return;
            try
            {
                reader.ReadValueSafe(out uint epoch); reader.ReadValueSafe(out uint revision); reader.ReadValueSafe(out byte audience);
                if (epoch != matchEpoch || audience > (byte)ThuongCommunicationAudience.Dead || revision < localCommunicationRevision) return;
                var entry = new NetworkChatManager.ChatMessageEntry();
                reader.ReadValueSafe(out entry.SenderClientId); reader.ReadValueSafe(out entry.SenderName);
                reader.ReadValueSafe(out entry.MessageText); reader.ReadValueSafe(out entry.TimestampTicks);
                if (reader.Position != reader.Length) return;
                if (revision == localCommunicationRevision && audience == (byte)communicationAudience)
                    communicationChat?.ReceiveThuongMessage(entry);
                else if (revision > localCommunicationRevision)
                {
                    pendingChat.Add((epoch, revision, audience, entry));
                    if (pendingChat.Count > NetworkChatManager.MaxHistoryCount) pendingChat.RemoveAt(0);
                }
            }
            catch (Exception error) when (error is OverflowException || error is ArgumentException)
            { Debug.LogWarning("[Thuong chat] Bỏ qua tin nhắn không hợp lệ."); }
        }

        private static string SafeChatText(string value) => value?.Replace('<', '‹').Replace('>', '›') ?? string.Empty;
        private static bool TrySanitizeChat(string value, out string text)
        {
            text = string.Empty;
            if (string.IsNullOrWhiteSpace(value) || value.Length > NetworkChatManager.MaxMessageLength) return false;
            text = SafeChatText(new string(value.Trim().Where(character => !char.IsControl(character)).ToArray()));
            return text.Length > 0 && System.Text.Encoding.UTF8.GetByteCount(text) <= 480;
        }

        public string ChatPlayerName(ulong clientId) => lobby.FirstOrDefault(member => member.ClientId == clientId)?.Name ?? "Người chơi";
        public void PauseForChat()
        {
            if (connected && matchStarted) SendMovementInput(Vector2.zero);
            Thuong.Emotes.PlayerEmoteWheelUI.CancelFor(this);
        }

        private void ResetCommunications()
        {
            communicationFingerprint = null; communicationAcks.Clear(); chatSequences.Clear(); chatLastSent.Clear();
            chatSequence = 0; communicationChat?.ClearHistory();
            nextLocalChatAt = 0; pendingChat.Clear();
            ApplyCommunicationPolicy(matchEpoch, 0, ThuongCommunicationAudience.None, string.Empty);
        }

        private void DisposeCommunications()
        {
            if (communicationDisposed) return;
            communicationDisposed = true; ++voiceRequestVersion;
            if (communicationRoot != null) communicationRoot.SetActive(false);
            if (communicationVoice != null)
            {
                communicationVoice.SetGameplayPermissions(this, false, false);
                _ = ReleaseVoiceAsync(communicationVoice);
            }
        }

        private async Task ReleaseVoiceAsync(NetworkVoiceManager voice)
        {
            await voice.LeaveVoiceChannelAsync();
            if (voice != null) voice.ReleaseGameplayPermissions(this);
        }
    }
}
