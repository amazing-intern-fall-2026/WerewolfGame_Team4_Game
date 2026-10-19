using System;
using System.Collections.Generic;

namespace Thuong.Minigames
{
    // Rules use server time and explicit actions, never a client result or a physics replay.
    // All mutable state is scoped to a session. No Unity singletons or scene APIs are used here.
    public sealed class ThuongMinigameAuthority
    {
        private sealed class Attempt
        {
            public ulong owner;
            public ThuongMinigameState state;
            public ThuongMinigameRules rules;
            public Random random;
            public uint sequence;
            public double lastAction = double.NegativeInfinity, lastControl;
            public int[] ingredientScores;
            public readonly List<int> ingredients = new();
            public bool[] traps, revealed, flags;
            public bool planted;
        }

        private readonly Dictionary<ulong, Attempt> active = new();
        private readonly HashSet<string> locks = new(StringComparer.Ordinal);
        private ulong nextId = (ulong)DateTime.UtcNow.Ticks;
        public int ActiveCount => active.Count;
        public bool IsPlaying(ulong owner) => active.ContainsKey(owner);
        public ThuongMinigameState Get(ulong owner) => active.TryGetValue(owner, out var attempt) ? Export(attempt) : null;

        public bool TryBegin(ulong owner, string taskId, int station, int day, ThuongMinigameKind kind,
            ThuongMinigameRules rules, int[] ingredientScores, double now, double dayEndsAt,
            out ThuongMinigameState state, out string error)
        {
            state = null;
            if (rules == null || !rules.IsValid || string.IsNullOrEmpty(taskId) || station < 0 || station >= 16 ||
                !Enum.IsDefined(typeof(ThuongMinigameKind), kind) || !Finite(now) || !Finite(dayEndsAt))
            { error = "Cấu hình thử thách không hợp lệ."; return false; }
            if (active.ContainsKey(owner)) { error = "Bạn đang làm một nhiệm vụ khác."; return false; }
            if (locks.Contains(Key(day, taskId))) { error = "Một người khác đang làm nhiệm vụ này."; return false; }
            if (dayEndsAt - now < rules.minimumDayRemaining)
            { error = "Không còn đủ thời gian ban ngày để bắt đầu."; return false; }
            if (kind == ThuongMinigameKind.Potion && (ingredientScores == null || ingredientScores.Length != 3))
            { error = "Thiếu công thức pha thuốc."; return false; }

            var attempt = new Attempt
            {
                owner = owner, rules = rules.Copy(), random = new Random(Guid.NewGuid().GetHashCode()),
                ingredientScores = ingredientScores != null ? (int[])ingredientScores.Clone() : Array.Empty<int>(),
                state = new ThuongMinigameState
                {
                    attemptId = ++nextId, taskId = taskId, stationId = station, day = day, kind = kind,
                    result = ThuongMinigameResult.Playing, startsAt = now + .75,
                    expiresAt = Math.Min(now + rules.duration, dayEndsAt), target = rules.target,
                    allowedMistakes = rules.allowedMistakes, control = .5f, timingWindow = rules.timingWindow,
                    message = "Bắt đầu thử thách."
                },
                lastControl = now
            };
            if (kind == ThuongMinigameKind.Trapsweeper)
            {
                int count = rules.boardWidth * rules.boardWidth;
                attempt.traps = new bool[count]; attempt.revealed = new bool[count]; attempt.flags = new bool[count];
                attempt.state.boardWidth = rules.boardWidth; attempt.state.trapCount = rules.traps;
                attempt.state.target = count - rules.traps;
            }
            if (kind == ThuongMinigameKind.Potion)
            {
                var recipes = new List<ThuongPotion>();
                foreach (int a in ingredientScores)
                foreach (int b in ingredientScores)
                foreach (int c in ingredientScores)
                {
                    var recipe = EvaluatePotion(a + b + c);
                    if (recipe != ThuongPotion.Sludge && !recipes.Contains(recipe)) recipes.Add(recipe);
                }
                if (recipes.Count == 0) { error = "Không có công thức khả thi."; return false; }
                attempt.state.recipe = recipes[attempt.random.Next(recipes.Count)];
                attempt.state.target = 3;
            }
            NextRound(attempt);
            active.Add(owner, attempt); locks.Add(Key(day, taskId));
            state = Export(attempt); error = null;
            return true;
        }

        public ThuongMinigameState Apply(ulong owner, ulong id, uint sequence, ThuongMinigameInput input, double now)
        {
            if (!active.TryGetValue(owner, out var attempt) || attempt.state.attemptId != id ||
                sequence == 0 || sequence <= attempt.sequence || !Finite(now) || !Finite(input.value)) return null;
            attempt.sequence = sequence;
            if (now >= attempt.state.expiresAt) return Finish(attempt, ThuongMinigameResult.Expired, "Đã hết thời gian thử thách.");
            // The server advances missed rounds before accepting late inputs.
            Advance(attempt, now);
            if (attempt.state.result != ThuongMinigameResult.Playing) return Export(attempt);
            if (now < attempt.state.startsAt || now - attempt.lastAction < .08) return null;
            attempt.lastAction = now;
            var s = attempt.state;
            switch (s.kind)
            {
                case ThuongMinigameKind.ChopWood:
                case ThuongMinigameKind.Harvest:
                    var expected = s.kind == ThuongMinigameKind.ChopWood ? ThuongMinigameAction.Chop : ThuongMinigameAction.Harvest;
                    if (input.action != expected || input.index != s.round) return null;
                    if (Math.Abs(now - s.roundAt) > attempt.rules.timingWindow)
                    { s.message = "Chờ vật thể đến vùng đánh dấu."; return Export(attempt); }
                    if (s.obstacle) return Finish(attempt, ThuongMinigameResult.Failed, "Bạn đã chạm chướng ngại.");
                    s.score++; s.message = "Đúng nhịp!";
                    if (s.score >= s.target) return Finish(attempt, ThuongMinigameResult.Succeeded, "Hoàn thành thử thách.");
                    NextRound(attempt);
                    break;
                case ThuongMinigameKind.Eggs:
                    if (input.action != ThuongMinigameAction.MoveBasket || input.value < 0 || input.value > 1) return null;
                    float move = (float)Math.Max(0, now - attempt.lastControl) * 2.5f;
                    s.control += Clamp(input.value - s.control, -move, move);
                    attempt.lastControl = now;
                    break;
                case ThuongMinigameKind.Defender:
                    if (input.action != ThuongMinigameAction.AimShield || input.value < 0 || input.value >= 360) return null;
                    float delta = (input.value - s.control + 540) % 360 - 180;
                    float turn = (float)Math.Max(0, now - attempt.lastControl) * 720;
                    s.control = (s.control + Clamp(delta, -turn, turn) + 360) % 360;
                    attempt.lastControl = now;
                    break;
                case ThuongMinigameKind.Potion:
                    if (input.action != ThuongMinigameAction.Ingredient || input.index < 0 || input.index >= attempt.ingredientScores.Length) return null;
                    attempt.ingredients.Add(input.index); s.score = attempt.ingredients.Count;
                    s.message = $"Đã thêm {s.score}/3 nguyên liệu.";
                    if (s.score == 3)
                    {
                        int total = 0;
                        foreach (int index in attempt.ingredients) total += attempt.ingredientScores[index];
                        bool correct = EvaluatePotion(total) == s.recipe;
                        return Finish(attempt, correct ? ThuongMinigameResult.Succeeded : ThuongMinigameResult.Failed,
                            correct ? "Pha đúng loại thuốc được giao." : "Công thức chưa đúng. Hãy thử lại.");
                    }
                    break;
                case ThuongMinigameKind.Trapsweeper:
                    if (input.index < 0 || input.index >= attempt.traps.Length) return null;
                    if (input.action == ThuongMinigameAction.Flag)
                    {
                        if (!attempt.revealed[input.index]) attempt.flags[input.index] = !attempt.flags[input.index];
                    }
                    else if (input.action == ThuongMinigameAction.Reveal)
                    {
                        if (attempt.flags[input.index] || attempt.revealed[input.index]) return null;
                        if (!attempt.planted) Plant(attempt, input.index);
                        if (attempt.traps[input.index])
                        {
                            attempt.revealed[input.index] = true;
                            return Finish(attempt, ThuongMinigameResult.Failed, "Bạn đã mở trúng bẫy.");
                        }
                        Reveal(attempt, input.index);
                        s.score = 0;
                        for (int i = 0; i < attempt.revealed.Length; i++) if (attempt.revealed[i] && !attempt.traps[i]) s.score++;
                        if (s.score == s.target) return Finish(attempt, ThuongMinigameResult.Succeeded, "Đã gỡ hết ô an toàn.");
                    }
                    else return null;
                    break;
            }
            return Export(attempt);
        }

        public List<ThuongMinigameState> Tick(double now)
        {
            var ended = new List<ThuongMinigameState>();
            foreach (var attempt in new List<Attempt>(active.Values))
            {
                if (now >= attempt.state.expiresAt) Finish(attempt, ThuongMinigameResult.Expired, "Đã hết thời gian thử thách.");
                else Advance(attempt, now);
                if (attempt.state.result != ThuongMinigameResult.Playing) ended.Add(Export(attempt));
            }
            return ended;
        }

        public ThuongMinigameState Cancel(ulong owner, ulong id, string reason)
        {
            return active.TryGetValue(owner, out var attempt) && attempt.state.attemptId == id
                ? Finish(attempt, ThuongMinigameResult.Cancelled, reason) : null;
        }

        public List<ulong> Owners() => new(active.Keys);

        private void Advance(Attempt a, double now)
        {
            var s = a.state;
            if (s.kind == ThuongMinigameKind.Potion || s.kind == ThuongMinigameKind.Trapsweeper) return;
            double due = s.roundAt + (s.kind == ThuongMinigameKind.ChopWood || s.kind == ThuongMinigameKind.Harvest ? a.rules.timingWindow : 0);
            if (now <= due) return;
            bool automatic = s.kind == ThuongMinigameKind.Eggs || s.kind == ThuongMinigameKind.Defender;
            bool hit = s.kind == ThuongMinigameKind.Eggs ? Math.Abs(s.control - s.lane) <= .13 :
                s.kind == ThuongMinigameKind.Defender && Math.Abs((s.control - s.lane + 540) % 360 - 180) <= 32;
            if (automatic && hit) { s.score++; s.message = s.kind == ThuongMinigameKind.Eggs ? "Đã hứng trứng!" : "Đã chặn đòn!"; }
            else if (!s.obstacle) { s.mistakes++; s.message = "Trượt một lượt."; }
            if (s.score >= s.target) { Finish(a, ThuongMinigameResult.Succeeded, "Hoàn thành thử thách."); return; }
            if (s.mistakes > s.allowedMistakes) { Finish(a, ThuongMinigameResult.Failed, "Đã vượt số lần trượt cho phép."); return; }
            NextRound(a);
            // Never expose an old sequence of challenges after a stalled frame. Each new
            // challenge gets its full approach time, still bounded by the attempt deadline.
            if (s.roundAt < now + .8) s.roundAt = now + a.rules.interval;
        }

        private static float Clamp(float v, float min, float max) => Math.Max(min, Math.Min(max, v));
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        private static string Key(int day, string task) => day + ":" + task;
        public static ThuongPotion EvaluatePotion(int score) => score >= 5 ? ThuongPotion.StrongHealing :
            score > 0 ? ThuongPotion.Healing : score <= -5 ? ThuongPotion.StrongPoison : score < 0 ? ThuongPotion.Poison : ThuongPotion.Sludge;

        private static void NextRound(Attempt a)
        {
            var s = a.state;
            s.round++;
            s.roundAt = s.roundAt == 0 ? s.startsAt + a.rules.interval : s.roundAt + a.rules.interval;
            s.lane = s.kind == ThuongMinigameKind.Defender ? a.random.Next(0, 360) : (float)(.15 + a.random.NextDouble() * .7);
            s.obstacle = s.kind == ThuongMinigameKind.Harvest && s.round % 4 == 0;
        }

        private ThuongMinigameState Finish(Attempt a, ThuongMinigameResult result, string message)
        {
            a.state.result = result; a.state.message = message;
            active.Remove(a.owner); locks.Remove(Key(a.state.day, a.state.taskId));
            return Export(a);
        }

        private static void Plant(Attempt a, int safe)
        {
            int w = a.rules.boardWidth;
            var candidates = new List<int>();
            for (int i = 0; i < a.traps.Length; i++)
                if (Math.Abs(i % w - safe % w) > 1 || Math.Abs(i / w - safe / w) > 1) candidates.Add(i);
            for (int i = 0; i < a.rules.traps; i++)
            {
                int pick = a.random.Next(candidates.Count);
                a.traps[candidates[pick]] = true; candidates.RemoveAt(pick);
            }
            a.planted = true;
        }

        private static IEnumerable<int> Neighbors(int index, int w)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = index % w + dx, y = index / w + dy;
                if ((dx != 0 || dy != 0) && x >= 0 && y >= 0 && x < w && y < w) yield return y * w + x;
            }
        }
        private static int Clue(Attempt a, int index)
        {
            int count = 0;
            foreach (int neighbor in Neighbors(index, a.rules.boardWidth)) if (a.traps[neighbor]) count++;
            return count;
        }
        private static void Reveal(Attempt a, int index)
        {
            var queue = new Queue<int>(); queue.Enqueue(index);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (a.revealed[current] || a.flags[current] || a.traps[current]) continue;
                a.revealed[current] = true;
                if (Clue(a, current) == 0)
                    foreach (int neighbor in Neighbors(current, a.rules.boardWidth)) queue.Enqueue(neighbor);
            }
        }
        private static ThuongMinigameState Export(Attempt a)
        {
            var s = a.state;
            var copy = new ThuongMinigameState
            {
                attemptId = s.attemptId, taskId = s.taskId, stationId = s.stationId, day = s.day, kind = s.kind,
                result = s.result, startsAt = s.startsAt, expiresAt = s.expiresAt, roundAt = s.roundAt,
                target = s.target, score = s.score, mistakes = s.mistakes, allowedMistakes = s.allowedMistakes,
                round = s.round, lane = s.lane, control = s.control, timingWindow = s.timingWindow,
                obstacle = s.obstacle, recipe = s.recipe, boardWidth = s.boardWidth, trapCount = s.trapCount,
                message = s.message, ingredients = a.ingredients.ToArray()
            };
            if (a.traps != null)
            {
                copy.cells = new int[a.traps.Length];
                for (int i = 0; i < copy.cells.Length; i++)
                    copy.cells[i] = a.revealed[i] ? a.traps[i] ? -9 : Clue(a, i) : a.flags[i] ? -3 : -2;
            }
            return copy;
        }
    }
}
