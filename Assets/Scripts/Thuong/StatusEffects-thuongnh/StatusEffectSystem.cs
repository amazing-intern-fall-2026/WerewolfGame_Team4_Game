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
        if (target == null || !target.isAlive || GameRoleManager.Instance?.currentState == GameState.GameOver) return null;
        target.effects ??= new();
        var effect = target.effects.Find(item => item.HasSameSource(type, source?.playerID ?? -1));
        if (effect == null)
        {
            effect = new StatusEffectInstance(type, strength, durationTicks, tickOnPhase, source?.playerID ?? -1);
            target.effects.Add(effect);
        }
        else effect.Refresh(strength, durationTicks, tickOnPhase);
        if (type == StatusEffectType.Cursed) target.isCursed = true;
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
        if (players == null || phase == GamePhase.GameOver) return;
        using var batch = (deathSystem != null ? deathSystem : DeathResolver.Instance)?.BeginBatch();
        foreach (var player in players)
        {
            if (player?.effects == null || !player.isAlive) continue;
            // Death callbacks and expiry can remove effects while processing this phase.
            foreach (var effect in player.effects.ToArray())
            {
                if (!player.isAlive || !player.effects.Contains(effect) || effect.TickOnPhase != phase) continue;
                bool expired = effect.Tick(phase);
                player.NotifyChanged();
                if (!expired) continue;
                RemoveEffect(player, effect);
                if (effect.Type == StatusEffectType.Cursed && player.isAlive)
                    (deathSystem != null ? deathSystem : DeathResolver.Instance)?.TryKill(
                        new DeathRequest(player, DeathCause.Curse,
                            PlayerManager.Instance?.GetplayerByID(effect.SourcePlayerID)));
            }
        }
    }

    public bool RemoveEffect(PlayerData player, StatusEffectType type, int? sourcePlayerID = null)
    {
        if (player?.effects == null) return false;
        bool removed = false;
        foreach (var effect in player.effects.ToArray())
            if (effect.Type == type && (!sourcePlayerID.HasValue || effect.SourcePlayerID == sourcePlayerID.Value))
                removed |= RemoveEffect(player, effect);
        return removed;
    }

    public void ClearEffects(PlayerData player)
    {
        if (player?.effects == null) return;
        foreach (var effect in player.effects.ToArray()) RemoveEffect(player, effect);
    }

    private bool RemoveEffect(PlayerData player, StatusEffectInstance effect)
    {
        if (!player.effects.Remove(effect)) return false;
        if (effect.Type == StatusEffectType.Cursed) player.isCursed = player.HasEffect(StatusEffectType.Cursed);
        player.NotifyChanged();
        EffectRemoved?.Invoke(player, effect);
        return true;
    }
}
