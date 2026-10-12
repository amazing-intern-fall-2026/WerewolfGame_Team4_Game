using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct AbilityResolution
{
    public PlayerData Actor { get; }
    public PlayerData Target { get; }
    public bool Succeeded { get; }
    public string Message { get; }
    public AbilityResolution(PlayerData actor, PlayerData target, bool succeeded, string message)
    { Actor = actor; Target = target; Succeeded = succeeded; Message = message; }
}

public sealed class AbilitySystem : MonoBehaviour
{
    public static AbilitySystem Instance;
    public GameRoleManager phaseController;
    public NightManager nightSystem;
    public bool MatchIsRunning { get; private set; }
    private readonly Dictionary<int, int> uses = new();
    private readonly Dictionary<int, (int night, int target)> lastNightTargets = new();
    public event Action<AbilityResolution> AbilityResolved;
    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void SetMatchRunning(bool running)
    {
        MatchIsRunning = running;
        if (!running) { uses.Clear(); lastNightTargets.Clear(); }
    }
    public void BeginPhase(GamePhase phase) { uses.Clear(); }
    public ActionValidationResult ValidateActor(PlayerData actor)
    {
        var result = Validate(actor, null);
        return result.FailureReason == ActionFailureReason.TargetMissing ? ActionValidationResult.Success() : result;
    }
    public ActionValidationResult Validate(PlayerData actor, PlayerData target)
    {
        if (phaseController == null || nightSystem == null)
            return ActionValidationResult.Fail(ActionFailureReason.MatchNotRunning, "Chưa nối hệ thống điều phối kỹ năng.");
        // UI and submission must both read the registered instances, including their current faction/life state.
        if (actor != null && PlayerManager.Instance?.GetplayerByID(actor.playerID) != actor)
            return ActionValidationResult.Fail(ActionFailureReason.ActorMissing, "Người dùng không thuộc trận hiện tại.");
        if (target != null && PlayerManager.Instance?.GetplayerByID(target.playerID) != target)
            return ActionValidationResult.Fail(ActionFailureReason.TargetMissing, "Mục tiêu không thuộc trận hiện tại.");
        uses.TryGetValue(actor?.playerID ?? -1, out int used);
        var result = AbilityValidator.Validate(MatchIsRunning, phaseController.currentPhase, actor, target, used);
        if (result.IsValid && actor.roleDefinition.forbidConsecutiveNightTarget &&
            lastNightTargets.TryGetValue(actor.playerID, out var previous) &&
            nightSystem.CurrentNightNumber == previous.night + 1 && target.playerID == previous.target)
            return ActionValidationResult.Fail(ActionFailureReason.RepeatedTarget, "Không thể bảo vệ cùng một người trong hai đêm liên tiếp.");
        return result;
    }
    public ActionValidationResult TryUseAbility(PlayerData actor, PlayerData target)
    {
        var result = Validate(actor, target);
        if (!result.IsValid) return result;
        uses.TryGetValue(actor.playerID, out int used);
        uses[actor.playerID] = used + 1;
        if (actor.roleDefinition.usablePhase == GamePhase.Night)
            lastNightTargets[actor.playerID] = (nightSystem.CurrentNightNumber, target.playerID);
        actor.hasUseNightAction = true;
        nightSystem.QueueAbility(actor, target, actor.roleDefinition);
        actor.NotifyChanged();
        return ActionValidationResult.Success("Hành động đã ghi nhận; xử lý ở NightResolution.");
    }
    public void Publish(AbilityResolution result) => AbilityResolved?.Invoke(result);
}
