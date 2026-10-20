using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class StatusEffectSystem : MonoBehaviour
{
    public static StatusEffectSystem Instance;
    public DeathResolver deathSystem;
    public event Action<PlayerData, StatusEffectInstance> EffectApplied;
    public event Action<PlayerData, StatusEffectInstance> EffectRemoved;
    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public StatusEffectInstance ApplyEffect(PlayerData target, StatusEffectType type, int strength,
        int durationTicks, GamePhase tickOnPhase, PlayerData source = null)
    {
        if (target == null) return null;
        target.effects ??= new();
        var effect = target.effects.Find(item => item.HasSameSource(type, source?.playerID ?? -1));
        if (effect == null)
        {
            effect = new StatusEffectInstance(type, strength, durationTicks, tickOnPhase, source?.playerID ?? -1);
            target.effects.Add(effect);
        }
        else effect.Refresh(strength, durationTicks, tickOnPhase);
        target.NotifyChanged();
        EffectApplied?.Invoke(target, effect);
        return effect;
    }

    public bool ConsumeProtection(PlayerData target)
    {
        var effect = target?.effects?.Find(item => item.Type == StatusEffectType.Protected);
        if (effect == null) return false;
        RemoveEffect(target, effect);
        return true;
    }

    public void ProcessPhaseStart(GamePhase phase, IReadOnlyList<PlayerData> players)
    {
        if (players == null) return;
        foreach (var player in players)
        {
            if (player?.effects == null) continue;
            // Death callbacks and expiry can remove effects while processing this phase.
            foreach (var effect in player.effects.ToArray())
            {
                if (effect.TickOnPhase != phase) continue;
                bool expired = effect.Tick(phase);
                player.NotifyChanged();
                if (!expired) continue;
                RemoveEffect(player, effect);
                if (effect.Type == StatusEffectType.Cursed && player.isAlive)
                    (deathSystem != null ? deathSystem : DeathResolver.Instance)?.TryKill(
                        new DeathRequest(player, DeathCause.Curse));
            }
        }
    }

    private void RemoveEffect(PlayerData player, StatusEffectInstance effect)
    {
        if (!player.effects.Remove(effect)) return;
        player.NotifyChanged();
        EffectRemoved?.Invoke(player, effect);
    }
}
