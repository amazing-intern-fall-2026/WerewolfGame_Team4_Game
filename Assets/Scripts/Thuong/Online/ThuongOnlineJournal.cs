using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private const string JournalMessage = "thuong.online.match-journal.v1";
        private const int JournalMaxEntries = 512;
        private const int JournalChunkSize = 16;

        private readonly struct MatchJournalEntry
        {
            public readonly int Day;
            public readonly GamePhase Phase;
            public readonly string Text;
            public MatchJournalEntry(int day, GamePhase phase, string text)
            { Day = day; Phase = phase; Text = text; }
        }

        private readonly List<MatchJournalEntry> matchJournal = new();
        private List<MatchJournalEntry>[] incomingJournalChunks;
        private bool[] incomingJournalReceived;
        private uint incomingJournalEpoch, journalRevision, displayedJournalRevision = uint.MaxValue;
        private int incomingJournalCount;
        private bool journalPublished, journalReady, journalTruncated, journalFinishedRecorded, showJournal;
        private TMP_Text journalText, journalStatus;
        private GameObject journalPanel;

        private void RegisterJournalMessages() => network.CustomMessagingManager.RegisterNamedMessageHandler(JournalMessage, ReceiveJournal);
        private void UnregisterJournalMessages() => network?.CustomMessagingManager?.UnregisterNamedMessageHandler(JournalMessage);

        private void ResetJournal()
        {
            matchJournal.Clear();
            incomingJournalChunks = null;
            incomingJournalReceived = null;
            incomingJournalEpoch = matchEpoch;
            incomingJournalCount = 0;
            journalPublished = journalReady = journalTruncated = journalFinishedRecorded = showJournal = false;
            journalRevision++;
            displayedJournalRevision = uint.MaxValue;
        }

        private void AddJournal(string text)
        {
            AddJournalAt(text, Mathf.Max(0, game != null ? game.currentDay : 0),
                game != null ? game.currentPhase : GamePhase.Setup, -1);
        }

        private void AddJournalAt(string text, int day, GamePhase phase, int insertAt)
        {
            if (network == null || !network.IsServer || !rolesAssigned || string.IsNullOrWhiteSpace(text)) return;
            if (matchJournal.Count >= JournalMaxEntries)
            {
                if (!journalTruncated)
                {
                    matchJournal[JournalMaxEntries - 1] = new MatchJournalEntry(
                        Mathf.Max(0, game != null ? game.currentDay : 0), game != null ? game.currentPhase : GamePhase.Setup,
                        "Một số sự kiện tiếp theo được lược bớt để giới hạn dung lượng nhật ký.");
                    journalTruncated = true;
                    journalRevision++;
                }
                return;
            }
            string safe = TrimUtf8(text.Replace("\r", " ").Replace("\n", " "), 120);
            var entry = new MatchJournalEntry(Mathf.Max(0, day), phase, safe);
            if (insertAt < 0 || insertAt >= matchJournal.Count) matchJournal.Add(entry);
            else matchJournal.Insert(insertAt, entry);
            journalRevision++;
        }

        private void AddJournalFinal(string text)
        {
            if (matchJournal.Count < JournalMaxEntries) { AddJournal(text); return; }
            int day = Mathf.Max(0, game != null ? game.currentDay : 0);
            GamePhase phase = game != null ? game.currentPhase : GamePhase.GameOver;
            matchJournal[JournalMaxEntries - 2] = new MatchJournalEntry(day, phase,
                "Một số sự kiện trước đó được lược bớt để giới hạn dung lượng nhật ký.");
            matchJournal[JournalMaxEntries - 1] = new MatchJournalEntry(day, phase, TrimUtf8(text, 120));
            journalTruncated = true;
            journalRevision++;
        }

        private void RecordJournalAction(PlayerData actor, ThuongOnlineAction action, int argument, int secondArgument,
            int day, GamePhase phase, int insertAt)
        {
            if (actor == null) return;
            string actorName = actor.DisplayName;
            string target = argument >= 0 ? JournalPlayerName(argument) : null;
            string secondTarget = secondArgument >= 0 ? JournalPlayerName(secondArgument) : null;
            string entry = action switch
            {
                ThuongOnlineAction.Vote => $"{actorName} bỏ phiếu cho {target}.",
                ThuongOnlineAction.NoEliminationVote => $"{actorName} chọn không loại ai trong lượt bỏ phiếu.",
                ThuongOnlineAction.MakeLovers => $"{actorName} dùng Dệt Duyên lên {target} và {secondTarget}.",
                ThuongOnlineAction.NightAbility => $"{actorName} ({actor.roleType}) dùng kỹ năng" + (target == null ? "." : $" lên {target}."),
                ThuongOnlineAction.ShamanGoodCharm => $"{actorName} ({actor.roleType}) dùng bùa lành lên {target}.",
                ThuongOnlineAction.ShamanBadCharm => $"{actorName} ({actor.roleType}) dùng bùa dữ lên {target}.",
                ThuongOnlineAction.HunterTrap => $"{actorName} ({actor.roleType}) dùng phát bắn trả thù lên {target}.",
                ThuongOnlineAction.RealtimeAbility => $"{actorName} ({actor.roleType}) dùng kỹ năng tương tác gần" + (target == null ? "." : $" lên {target}."),
                ThuongOnlineAction.ReportCorpse => $"{actorName} báo một xác chết và gọi họp.",
                _ => null
            };
            if (!string.IsNullOrEmpty(entry)) AddJournalAt(entry, day, phase, insertAt);
        }

        private void RecordVillageDeath(DeathResult result)
        {
            if (network == null || !network.IsServer || !matchStarted || matchResult != null || result.Request.Target == null) return;
            PlayerData target = result.Request.Target;
            string name = target.DisplayName;
            if (result.Outcome == DeathOutcome.Killed)
            {
                string source = result.Request.Source != null ? $" bởi {result.Request.Source.DisplayName}"
                    : result.Request.SourceRole.HasValue ? $" bởi {result.Request.SourceRole.Value}" : string.Empty;
                AddJournal($"{name} bị loại do {DeathCauseName(result.Request.Cause)}{source}.");
            }
            else if (result.Outcome == DeathOutcome.Prevented && target.isWhiteHoundAwakened)
                AddJournal($"{name} thức tỉnh thành Bạch Khuyển sau đòn {DeathCauseName(result.Request.Cause)}.");
            else if (result.Outcome == DeathOutcome.Prevented && !string.IsNullOrWhiteSpace(result.Message) &&
                result.Message.IndexOf("bùa bảo vệ", StringComparison.OrdinalIgnoreCase) < 0)
                AddJournal($"Đòn {DeathCauseName(result.Request.Cause)} nhắm vào {name} bị chặn.");
        }

        private void RecordJournalVoteResult(VoteResolution result)
        {
            if (result == null || network == null || !network.IsServer || matchResult != null) return;
            string totals = result.Tallies.Count == 0 ? "không có phiếu hợp lệ" :
                string.Join(", ", result.Tallies.OrderBy(pair => pair.Key).Select(pair => $"{JournalPlayerName(pair.Key)}: {pair.Value}"));
            if (result.NoEliminationVotes > 0) totals += $", không loại ai: {result.NoEliminationVotes}";
            AddJournal($"Kết quả bỏ phiếu: {totals}. {result.Message}");
        }

        private string JournalPlayerName(int id) => players?.GetplayerByID(id)?.DisplayName ?? $"P{id + 1:00}";

        private void PublishJournalAfterMatch()
        {
            if (journalPublished || network == null || !network.IsServer || matchResult == null || game == null ||
                game.currentState != GameState.GameOver) return;
            if (!journalFinishedRecorded)
            {
                AddJournalFinal($"Ván kết thúc: {WinnerLabel(matchResult.WinnerId)}.");
                journalFinishedRecorded = true;
            }
            journalPublished = journalReady = true;
            incomingJournalCount = matchJournal.Count;
            journalRevision++;
            foreach (var member in serverPlayers.Values)
                if (!member.Offline) SendJournalTo(member.ClientId);
        }

        private void SendJournalTo(ulong clientId)
        {
            if (!journalPublished || !network.IsServer || clientId == network.LocalClientId ||
                !network.ConnectedClientsIds.Contains(clientId)) return;
            int chunkCount = Mathf.Max(1, Mathf.CeilToInt((float)matchJournal.Count / JournalChunkSize));
            for (int chunk = 0; chunk < chunkCount; chunk++)
            {
                int start = chunk * JournalChunkSize;
                int count = Mathf.Clamp(matchJournal.Count - start, 0, JournalChunkSize);
                using var writer = new FastBufferWriter(8192, Allocator.Temp);
                writer.WriteValueSafe(matchEpoch);
                writer.WriteValueSafe((ushort)chunk);
                writer.WriteValueSafe((ushort)chunkCount);
                writer.WriteValueSafe((ushort)matchJournal.Count);
                writer.WriteValueSafe((byte)count);
                for (int i = 0; i < count; i++)
                {
                    MatchJournalEntry entry = matchJournal[start + i];
                    writer.WriteValueSafe(entry.Day);
                    writer.WriteValueSafe((byte)entry.Phase);
                    writer.WriteValueSafe(Fixed128(entry.Text));
                }
                network.CustomMessagingManager.SendNamedMessage(JournalMessage, clientId, writer,
                    NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        private void ReceiveJournal(ulong sender, FastBufferReader reader)
        {
            if (network == null || network.IsServer || sender != NetworkManager.ServerClientId) return;
            try
            {
                reader.ReadValueSafe(out uint epoch);
                reader.ReadValueSafe(out ushort chunkIndex);
                reader.ReadValueSafe(out ushort chunkCount);
                reader.ReadValueSafe(out ushort totalEntries);
                reader.ReadValueSafe(out byte entryCount);
                if (epoch != matchEpoch || totalEntries > JournalMaxEntries || chunkCount == 0 || chunkCount > 32 ||
                    chunkIndex >= chunkCount || chunkCount != Mathf.Max(1, Mathf.CeilToInt((float)totalEntries / JournalChunkSize))) return;
                int expected = totalEntries == 0 ? 0 : Mathf.Min(JournalChunkSize, totalEntries - chunkIndex * JournalChunkSize);
                if (entryCount != expected) return;
                if (journalReady && incomingJournalEpoch == epoch) return;
                if (incomingJournalChunks == null || incomingJournalEpoch != epoch || incomingJournalCount != totalEntries)
                {
                    incomingJournalEpoch = epoch;
                    incomingJournalCount = totalEntries;
                    incomingJournalChunks = new List<MatchJournalEntry>[chunkCount];
                    incomingJournalReceived = new bool[chunkCount];
                }
                if (incomingJournalChunks.Length != chunkCount) return;
                var entries = new List<MatchJournalEntry>(entryCount);
                for (int i = 0; i < entryCount; i++)
                {
                    reader.ReadValueSafe(out int day);
                    reader.ReadValueSafe(out byte phase);
                    reader.ReadValueSafe(out FixedString128Bytes message);
                    if (day < 0 || day > 10000 || !Enum.IsDefined(typeof(GamePhase), (int)phase) || string.IsNullOrWhiteSpace(message.ToString())) return;
                    entries.Add(new MatchJournalEntry(day, (GamePhase)phase, message.ToString()));
                }
                incomingJournalChunks[chunkIndex] = entries;
                incomingJournalReceived[chunkIndex] = true;
                if (incomingJournalReceived.Any(value => !value)) return;
                matchJournal.Clear();
                foreach (var part in incomingJournalChunks) matchJournal.AddRange(part);
                if (matchJournal.Count != incomingJournalCount) { matchJournal.Clear(); return; }
                journalReady = true;
                journalRevision++;
            }
            catch (Exception error) when (error is OverflowException || error is ArgumentException || error is InvalidOperationException)
            { Debug.LogWarning("Bỏ qua một phần nhật ký ván không hợp lệ."); }
        }

        private void BuildJournalPanel(Transform parent)
        {
            var shade = LobbyPanel("Match journal shade", parent, Vector2.zero, Vector2.one, false);
            shade.GetComponent<Image>().color = new Color(0, 0, 0, .86f);
            journalPanel = shade.gameObject;
            var card = LobbyPanel("Match journal card", shade, new Vector2(.10f, .08f), new Vector2(.90f, .92f));
            LobbyText("Match journal title", card, "DIỄN BIẾN TRẬN ĐẤU", new Vector2(.04f, .91f), new Vector2(.75f, .99f), 30, TextAlignmentOptions.Center);
            journalStatus = LobbyText("Match journal status", card, "", new Vector2(.06f, .84f), new Vector2(.94f, .91f), 17, TextAlignmentOptions.Center);
            var viewport = LobbyPanel("Match journal viewport", card, new Vector2(.045f, .12f), new Vector2(.955f, .82f), false);
            viewport.GetComponent<Image>().color = new Color(.08f, .12f, .12f, .8f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Match journal entries", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.SetParent(viewport, false); contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(.5f, 1); contentRect.offsetMin = new Vector2(12, 0); contentRect.offsetMax = new Vector2(-12, 0);
            journalText = content.GetComponent<TMP_Text>();
            journalText.font = sceneHud != null && sceneHud.phaseText != null ? sceneHud.phaseText.font : null;
            journalText.fontSize = 20; journalText.color = ThuongMedievalTheme.Cream;
            journalText.alignment = TextAlignmentOptions.TopLeft; journalText.textWrappingMode = TextWrappingModes.Normal;
            journalText.overflowMode = TextOverflowModes.Overflow; journalText.raycastTarget = false; journalText.richText = false;
            var fitter = content.GetComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = contentRect; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
            LobbyButton("Close match journal", card, "ĐÓNG", new Vector2(.40f, .025f), new Vector2(.60f, .105f), () => showJournal = false);
            journalPanel.SetActive(false);
        }

        private void RefreshJournalPresentation()
        {
            if (journalPanel == null) return;
            bool visible = matchResult != null && visibleState == GameState.GameOver;
            journalPanel.SetActive(visible && showJournal);
            if (journalStatus != null)
                journalStatus.text = journalReady ? $"{matchJournal.Count} sự kiện · Nhật ký chỉ được công bố sau khi ván kết thúc."
                    : "Đang nhận các mốc diễn biến từ chủ phòng…";
            if (journalText == null || displayedJournalRevision == journalRevision) return;
            var text = new StringBuilder();
            for (int i = 0; i < matchJournal.Count; i++)
            {
                MatchJournalEntry entry = matchJournal[i];
                text.Append(i + 1).Append(". NGÀY ").Append(entry.Day <= 0 ? "CHUẨN BỊ" : entry.Day.ToString())
                    .Append(" · ").Append(JournalPhaseName(entry.Phase)).AppendLine();
                text.AppendLine(entry.Text).AppendLine();
            }
            journalText.text = journalReady ? text.ToString() : "Đang nhận nhật ký ván…";
            displayedJournalRevision = journalRevision;
        }

        private static string JournalPhaseName(GamePhase phase) => phase switch
        {
            GamePhase.RoleReveal or GamePhase.Setup => "CHUẨN BỊ",
            GamePhase.DayStart or GamePhase.Task or GamePhase.Event => "BAN NGÀY",
            GamePhase.Night or GamePhase.ResolveNight => "BAN ĐÊM",
            GamePhase.Discussion => "THẢO LUẬN",
            GamePhase.Voting or GamePhase.ResolveVote or GamePhase.DayResolution => "BỎ PHIẾU",
            GamePhase.GameOver => "KẾT THÚC",
            _ => phase.ToString().ToUpperInvariant()
        };

        private static string DeathCauseName(DeathCause cause) => cause switch
        {
            DeathCause.Vote => "bỏ phiếu", DeathCause.Monster => "Sói tấn công", DeathCause.Poison => "trúng độc",
            DeathCause.Trap => "bẫy Thợ Săn", DeathCause.Killer => "Sát Nhân", DeathCause.Lover => "liên kết người yêu",
            DeathCause.DeathHerald => "Sứ giả Cái chết", DeathCause.Ability => "kỹ năng", DeathCause.Curse => "lời nguyền",
            DeathCause.Disconnect => "mất kết nối", _ => "trường hợp đặc biệt"
        };
    }
}
