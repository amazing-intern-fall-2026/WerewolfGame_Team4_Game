using System;
using Unity.Collections;
using Unity.Netcode;

namespace Thuong.Minigames
{
    public static class ThuongMinigameWire
    {
        public const string Request = "thuong.minigame.request.v1";
        public const string State = "thuong.minigame.state.v1";
        public const int RequestBytes = 30;

        public static void Write(FastBufferWriter writer, ThuongMinigameState s)
        {
            writer.WriteValueSafe(s.attemptId);
            writer.WriteValueSafe(new FixedString64Bytes(s.taskId ?? string.Empty));
            writer.WriteValueSafe(s.stationId); writer.WriteValueSafe(s.day);
            writer.WriteValueSafe((byte)s.kind); writer.WriteValueSafe((byte)s.result);
            writer.WriteValueSafe(s.startsAt); writer.WriteValueSafe(s.expiresAt); writer.WriteValueSafe(s.roundAt);
            writer.WriteValueSafe(s.target); writer.WriteValueSafe(s.score); writer.WriteValueSafe(s.mistakes);
            writer.WriteValueSafe(s.allowedMistakes); writer.WriteValueSafe(s.round);
            writer.WriteValueSafe(s.lane); writer.WriteValueSafe(s.control); writer.WriteValueSafe(s.timingWindow);
            writer.WriteValueSafe(s.obstacle); writer.WriteValueSafe((byte)s.recipe);
            writer.WriteValueSafe((byte)s.ingredients.Length);
            foreach (int ingredient in s.ingredients) writer.WriteValueSafe(ingredient);
            writer.WriteValueSafe(s.boardWidth); writer.WriteValueSafe(s.trapCount);
            writer.WriteValueSafe((byte)s.cells.Length);
            foreach (int cell in s.cells) writer.WriteValueSafe((sbyte)cell);
            string message = s.message ?? string.Empty;
            while (System.Text.Encoding.UTF8.GetByteCount(message) > 120) message = message.Substring(0, message.Length - 1);
            writer.WriteValueSafe(new FixedString128Bytes(message));
        }

        public static ThuongMinigameState Read(FastBufferReader reader)
        {
            var s = new ThuongMinigameState();
            reader.ReadValueSafe(out s.attemptId);
            reader.ReadValueSafe(out FixedString64Bytes taskId); s.taskId = taskId.ToString();
            reader.ReadValueSafe(out s.stationId); reader.ReadValueSafe(out s.day);
            reader.ReadValueSafe(out byte kind); s.kind = (ThuongMinigameKind)kind;
            reader.ReadValueSafe(out byte result); s.result = (ThuongMinigameResult)result;
            if (kind > (byte)ThuongMinigameKind.Trapsweeper || result > (byte)ThuongMinigameResult.Expired)
                throw new InvalidOperationException("Invalid minigame state.");
            reader.ReadValueSafe(out s.startsAt); reader.ReadValueSafe(out s.expiresAt); reader.ReadValueSafe(out s.roundAt);
            reader.ReadValueSafe(out s.target); reader.ReadValueSafe(out s.score); reader.ReadValueSafe(out s.mistakes);
            reader.ReadValueSafe(out s.allowedMistakes); reader.ReadValueSafe(out s.round);
            reader.ReadValueSafe(out s.lane); reader.ReadValueSafe(out s.control); reader.ReadValueSafe(out s.timingWindow);
            reader.ReadValueSafe(out s.obstacle);
            reader.ReadValueSafe(out byte recipe); s.recipe = (ThuongPotion)recipe;
            reader.ReadValueSafe(out byte ingredients);
            if (ingredients > 3) throw new InvalidOperationException("Invalid ingredient count.");
            s.ingredients = new int[ingredients];
            for (int i = 0; i < ingredients; i++) reader.ReadValueSafe(out s.ingredients[i]);
            reader.ReadValueSafe(out s.boardWidth); reader.ReadValueSafe(out s.trapCount);
            reader.ReadValueSafe(out byte cells);
            if (cells > 64 || cells != s.boardWidth * s.boardWidth) throw new InvalidOperationException("Invalid board size.");
            s.cells = new int[cells];
            for (int i = 0; i < cells; i++) { reader.ReadValueSafe(out sbyte cell); s.cells[i] = cell; }
            reader.ReadValueSafe(out FixedString128Bytes message); s.message = message.ToString();
            return s;
        }
    }
}
