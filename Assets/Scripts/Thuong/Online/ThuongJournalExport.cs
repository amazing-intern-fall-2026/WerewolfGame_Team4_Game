using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Thuong.Online
{
    [Serializable]
    public sealed class ThuongJournalDocument
    {
        public int formatVersion = 1;
        public uint matchEpoch;
        public string exportedAtUtc, mode, winner, reason;
        public bool truncated;
        public ThuongVillageSettings villageRules;
        public ThuongJournalPlayer[] players;
        public ThuongJournalEvent[] events;
    }

    [Serializable]
    public sealed class ThuongJournalPlayer
    {
        public int id;
        public string name, role, faction;
        public bool alive, won;
    }

    [Serializable]
    public sealed class ThuongJournalEvent
    {
        public int order, day;
        public string phase, text;
    }

    public static class ThuongJournalExport
    {
        public static string WriteNew(string directory, ThuongJournalDocument document, bool json)
        {
            if (document == null || document.events == null || document.players == null)
                throw new ArgumentException("Nhật ký chưa đầy đủ.", nameof(document));
            Directory.CreateDirectory(directory);
            string name = $"Thuong-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}" + (json ? ".json" : ".txt");
            string path = Path.Combine(directory, name);
            string content = json ? JsonUtility.ToJson(document, true) : AsText(document);
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(content);
            return path;
        }

        public static string AsText(ThuongJournalDocument document)
        {
            var text = new StringBuilder("NHẬT KÝ TRẬN ĐẤU THƯƠNG\n");
            text.AppendLine("Thời gian xuất (UTC): " + document.exportedAtUtc);
            text.AppendLine("Chế độ: " + document.mode);
            text.AppendLine("Kết quả: " + document.winner);
            text.AppendLine("Lý do: " + document.reason);
            if (document.truncated) text.AppendLine("Một số sự kiện đã được lược bớt do giới hạn dung lượng.");
            text.AppendLine().AppendLine("NGƯỜI CHƠI");
            foreach (var player in document.players)
                text.AppendLine($"P{player.id + 1:00} · {SingleLine(player.name)} · {player.role} · {player.faction} · {(player.alive ? "Còn sống" : "Đã bị loại")} · {(player.won ? "THẮNG" : "THUA")}");
            text.AppendLine().AppendLine("DIỄN BIẾN");
            foreach (var entry in document.events)
                text.AppendLine($"{entry.order}. Ngày {entry.day} · {entry.phase}: {SingleLine(entry.text)}");
            return text.ToString();
        }

        private static string SingleLine(string value) => (value ?? "").Replace("\r", " ").Replace("\n", " ");
    }
}
