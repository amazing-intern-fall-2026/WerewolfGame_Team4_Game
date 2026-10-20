using System;
using UnityEngine;

namespace Thuong.Minigames
{
    public enum ThuongMinigameKind : byte { ChopWood, Harvest, Eggs, Potion, Defender, Trapsweeper }
    public enum ThuongMinigameResult : byte { Playing, Succeeded, Failed, Cancelled, Expired }
    public enum ThuongMinigameAction : byte { Chop, Harvest, MoveBasket, AimShield, Ingredient, Reveal, Flag }
    public enum ThuongPotion : byte { Sludge, Healing, StrongHealing, Poison, StrongPoison }

    [Serializable]
    public sealed class ThuongMinigameRules
    {
        [Min(1)] public int target = 5;
        [Min(0)] public int allowedMistakes = 2;
        [Min(1)] public float duration = 20;
        [Min(0)] public float minimumDayRemaining = 5;
        [Min(.5f)] public float interval = 1.6f;
        [Range(.15f, .6f)] public float timingWindow = .4f;
        [Range(3, 8)] public int boardWidth = 5;
        [Range(1, 20)] public int traps = 3;

        public ThuongMinigameRules Copy() => (ThuongMinigameRules)MemberwiseClone();
        public bool IsValid => target > 0 && target <= 50 && allowedMistakes >= 0 && allowedMistakes <= 10 &&
            Finite(duration) && duration >= 3 && duration <= 120 && Finite(minimumDayRemaining) &&
            minimumDayRemaining >= 0 && minimumDayRemaining <= duration && Finite(interval) && interval >= .5f &&
            interval <= 10 && Finite(timingWindow) && timingWindow >= .15f && timingWindow <= .6f &&
            timingWindow < interval / 2 && boardWidth >= 3 && boardWidth <= 8 && traps > 0 && traps <= boardWidth * boardWidth - 9;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // This is the entire client view. The trap layout and random generator never leave the server.
    public sealed class ThuongMinigameState
    {
        public ulong attemptId;
        public string taskId;
        public int stationId, day;
        public ThuongMinigameKind kind;
        public ThuongMinigameResult result;
        public double startsAt, expiresAt, roundAt;
        public int target, score, mistakes, allowedMistakes, round;
        public float lane, control, timingWindow;
        public bool obstacle;
        public ThuongPotion recipe;
        public int[] ingredients = Array.Empty<int>();
        // -2 covered, -3 flagged, -9 sprung trap, 0..8 adjacent traps.
        public int boardWidth, trapCount;
        public int[] cells = Array.Empty<int>();
        public string message;
    }

    public readonly struct ThuongMinigameInput
    {
        public readonly ThuongMinigameAction action;
        public readonly int index;
        public readonly float value;
        public ThuongMinigameInput(ThuongMinigameAction action, int index = 0, float value = 0)
        { this.action = action; this.index = index; this.value = value; }
    }

    public interface IThuongTaskMinigame : IDisposable
    {
        void Initialize(ThuongMinigameContext context);
        void Begin(ThuongMinigameState state);
        void Apply(ThuongMinigameState state);
        void Cancel();
    }

    public sealed class ThuongMinigameContext
    {
        public Camera camera;
        public Transform root;
        public ThuongMinigameEntry entry;
        public ThuongMinigameCatalog catalog;
        public TMPro.TMP_FontAsset font;
        public Action<ThuongMinigameInput> send;
        public Func<double> serverTime;
    }
}
