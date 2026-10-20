using System;
using System.Collections.Generic;
using System.Linq;

namespace Thuong.Online
{
    [Serializable]
    public sealed class ThuongOnlineWinSettings
    {
        public bool eliminationWin = true;
        public int minimumPlayersForParity = 3;
        public int specialWinVillageLimit = 2;
    }

    internal enum ThuongWinner : byte { Villagers, Werewolves, Lovers, Madman, FoxSpirit, WhiteHound, Killer, Draw }
    internal enum ThuongWinReason : byte
    {
        Tasks, NoThreats, Parity, DayLimit, LoversLastTwo, MadmanVoted,
        FoxCharmed, WhiteHoundSurvived, KillerSurvived, NobodyAlive, Stalemate, TimeLimitDraw, NoOpponents
    }

    // Captured only on the authoritative server, and published only after GameOver.
    internal readonly struct ThuongWinPlayer
    {
        public readonly int Id, CharacterIndex, LoverId;
        public readonly string Name;
        public readonly RoleType Role;
        public readonly FactionType Faction;
        public readonly bool Alive, VotedOut, Awakened, FoxQualified;
        public bool IsWhiteHound => Awakened && (Role == RoleType.WhiteHound || Role == RoleType.WhiteWolf);
        public bool IsKiller => Role == RoleType.Killer;
        public bool IsWolf => Faction == FactionType.Monster && !IsWhiteHound && !IsKiller;
        public bool IsVillager => Faction == FactionType.Villager && !IsWhiteHound && !IsKiller &&
            Role != RoleType.FoxSpirit && Role != RoleType.Madman;

        public ThuongWinPlayer(int id, string name, int characterIndex, RoleType role, FactionType faction,
            bool alive, bool votedOut, int loverId, bool awakened, bool foxQualified)
        {
            Id = id; Name = name; CharacterIndex = characterIndex; Role = role; Faction = faction;
            Alive = alive; VotedOut = votedOut; LoverId = loverId; Awakened = awakened; FoxQualified = foxQualified;
        }
    }

    internal readonly struct ThuongFinalPlayer
    {
        public readonly int Id, CharacterIndex;
        public readonly string Name;
        public readonly RoleType Role;
        public readonly FactionType Faction;
        public readonly bool Alive, Awakened, Won;
        public ThuongFinalPlayer(int id, string name, int characterIndex, RoleType role, FactionType faction,
            bool alive, bool awakened, bool won)
        {
            Id = id; Name = name; CharacterIndex = characterIndex; Role = role; Faction = faction;
            Alive = alive; Awakened = awakened; Won = won;
        }
    }

    internal sealed class ThuongMatchResult
    {
        public ThuongWinner Winner { get; }
        public ThuongWinReason Reason { get; }
        public int Day { get; }
        public GamePhase Phase { get; }
        public IReadOnlyList<ThuongFinalPlayer> Players { get; }
        public string WinnerId => Winner switch
        {
            ThuongWinner.Villagers => "Villagers", ThuongWinner.Werewolves => "Werewolves",
            ThuongWinner.Lovers => "Lovers", ThuongWinner.Madman => "Madman",
            ThuongWinner.FoxSpirit => "Fox Spirit", ThuongWinner.WhiteHound => "White Wolf",
            ThuongWinner.Killer => "Killer", _ => "Draw"
        };

        public ThuongMatchResult(ThuongWinner winner, ThuongWinReason reason, int day, GamePhase phase,
            IEnumerable<ThuongFinalPlayer> players)
        {
            Winner = winner; Reason = reason; Day = day; Phase = phase;
            Players = Array.AsReadOnly(players.OrderBy(player => player.Id).ToArray());
        }
    }

    internal static class ThuongMatchOutcome
    {
        // This evaluator has no scene side effects. Legacy scenes never call it.
        public static ThuongMatchResult Evaluate(IReadOnlyList<ThuongWinPlayer> players,
            ThuongOnlineWinSettings settings, int day, GamePhase phase, float progress, int maxDay, bool dayFinished)
        {
            if (players.Count == 0) return null;
            var alive = players.Where(player => player.Alive).ToArray();
            ThuongMatchResult Finish(ThuongWinner winner, ThuongWinReason reason, Func<ThuongWinPlayer, bool> wins) =>
                new(winner, reason, day, phase, players.Select(player => new ThuongFinalPlayer(
                    player.Id, player.Name, player.CharacterIndex, player.Role, player.Faction,
                    player.Alive, player.Awakened, wins(player))));
            ThuongMatchResult Draw(ThuongWinReason reason) => Finish(ThuongWinner.Draw, reason, _ => false);

            if (alive.Length == 0) return Draw(ThuongWinReason.NobodyAlive);
            if (dayFinished && day == 1 && players.Any(player => player.Role == RoleType.Madman && !player.Alive && player.VotedOut))
                return Finish(ThuongWinner.Madman, ThuongWinReason.MadmanVoted,
                    player => player.Role == RoleType.Madman && !player.Alive && player.VotedOut);
            if (alive.Length == 2 && alive[0].LoverId == alive[1].Id && alive[1].LoverId == alive[0].Id)
                return Finish(ThuongWinner.Lovers, ThuongWinReason.LoversLastTwo,
                    player => player.Id == alive[0].Id || player.Id == alive[1].Id);
            if (alive.Any(player => player.Role == RoleType.FoxSpirit && player.FoxQualified))
                return Finish(ThuongWinner.FoxSpirit, ThuongWinReason.FoxCharmed,
                    player => player.Alive && player.Role == RoleType.FoxSpirit && player.FoxQualified);

            int wolves = alive.Count(player => player.IsWolf);
            int village = alive.Count(player => player.IsVillager);
            int whiteHounds = alive.Count(player => player.IsWhiteHound);
            int killers = alive.Count(player => player.IsKiller);
            int villageLimit = Math.Max(0, settings.specialWinVillageLimit);
            if (whiteHounds == 1 && wolves == 0 && killers == 0 && village <= villageLimit)
                return Finish(ThuongWinner.WhiteHound, ThuongWinReason.WhiteHoundSurvived,
                    player => player.Alive && player.IsWhiteHound);
            if (killers == 1 && wolves == 0 && whiteHounds == 0 && village <= villageLimit)
                return Finish(ThuongWinner.Killer, ThuongWinReason.KillerSurvived,
                    player => player.Alive && player.IsKiller);

            // Team victories include dead teammates, using their final allegiance.
            if (progress >= 100 && players.Any(player => player.IsVillager))
                return Finish(ThuongWinner.Villagers, ThuongWinReason.Tasks, player => player.IsVillager);
            if (settings.eliminationWin)
            {
                if (village > 0 && wolves + whiteHounds + killers == 0)
                    return Finish(ThuongWinner.Villagers, ThuongWinReason.NoThreats, player => player.IsVillager);
                if (wolves > 0 && wolves == alive.Length)
                    return Finish(ThuongWinner.Werewolves, ThuongWinReason.NoOpponents, player => player.IsWolf);
                if (players.Count >= Math.Max(3, settings.minimumPlayersForParity) && wolves > 0 &&
                    whiteHounds + killers == 0 && wolves >= alive.Length - wolves)
                    return Finish(ThuongWinner.Werewolves, ThuongWinReason.Parity, player => player.IsWolf);
            }

            if (dayFinished && day >= Math.Max(1, maxDay))
                return wolves > 0
                    ? Finish(ThuongWinner.Werewolves, ThuongWinReason.DayLimit, player => player.IsWolf)
                    : Draw(ThuongWinReason.TimeLimitDraw);
            // A remaining Fox can still charm two living targets; allow that play.
            if (village + wolves + whiteHounds + killers == 0 &&
                !(alive.Length >= 3 && alive.Any(player => player.Role == RoleType.FoxSpirit)))
                return Draw(ThuongWinReason.Stalemate);
            return null;
        }
    }
}
