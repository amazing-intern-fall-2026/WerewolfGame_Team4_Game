using System;
using UnityEngine;

public enum StatusEffectType { Protected, CannotVote, ExtraVote, Silenced, Cursed }

[Serializable]
public sealed class StatusEffectInstance
{
    [SerializeField] private StatusEffectType type;
    [SerializeField, Min(1)] private int strength;
    [SerializeField, Min(1)] private int remainingTicks;
    [SerializeField] private GamePhase tickOnPhase;
    [SerializeField] private int sourcePlayerID;
    public StatusEffectType Type => type;
    public int Strength => strength;
    public int RemainingTicks => remainingTicks;
    public GamePhase TickOnPhase => tickOnPhase;
    public int SourcePlayerID => sourcePlayerID;

    public StatusEffectInstance(StatusEffectType type, int strength, int ticks, GamePhase phase, int sourceID = -1)
    {
        this.type = type;
        this.strength = Mathf.Max(1, strength);
        remainingTicks = Mathf.Max(1, ticks);
        tickOnPhase = phase;
        sourcePlayerID = sourceID;
    }

    public bool HasSameSource(StatusEffectType value, int sourceID) => type == value && sourcePlayerID == sourceID;
    public void Refresh(int value, int ticks, GamePhase phase)
    {
        // One source refreshes its layer; independent sources retain separate layers.
        strength = Mathf.Max(strength, Mathf.Max(1, value));
        remainingTicks = Mathf.Max(remainingTicks, Mathf.Max(1, ticks));
        tickOnPhase = phase;
    }
    public bool Tick(GamePhase phase)
    {
        if (phase != tickOnPhase) return false;
        remainingTicks = Mathf.Max(0, remainingTicks - 1);
        return remainingTicks == 0;
    }
}
