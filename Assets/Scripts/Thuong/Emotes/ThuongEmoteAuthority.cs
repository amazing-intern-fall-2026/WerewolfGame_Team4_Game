using System.Collections.Generic;
using UnityEngine;

namespace Thuong.Emotes
{
    internal struct ThuongEmoteState
    {
        public int Id;
        public double ExpiresAt;
        public GamePhase Phase;
        public int Day;
    }

    internal struct ThuongEmoteView
    {
        public int PlayerId;
        public int Id;
        public double ExpiresAt;
        public Transform Anchor;
    }

    // No role, faction, target or skill inventory is read by social expressions.
    public sealed class ThuongEmoteAuthority
    {
        private readonly Dictionary<ulong, double> nextAllowed = new();
        private readonly Dictionary<ulong, uint> lastSequence = new();

        public bool TryAccept(ulong sender, uint currentEpoch, uint epoch, uint sequence,
            int id, bool eligible, double now)
        {
            if (epoch != currentEpoch || sequence == 0 || !PlayerEmoteCatalog.IsValid(id) ||
                double.IsNaN(now) || double.IsInfinity(now) ||
                lastSequence.TryGetValue(sender, out uint previous) && sequence <= previous) return false;
            lastSequence[sender] = sequence;
            if (!eligible || nextAllowed.TryGetValue(sender, out double next) && now < next) return false;
            nextAllowed[sender] = now + PlayerEmoteCatalog.CooldownSeconds;
            return true;
        }

        public void Clear() { nextAllowed.Clear(); lastSequence.Clear(); }
    }
}
