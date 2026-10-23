using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private uint matchEpoch, privateMatchEpoch;
        private GamePhase localAbilityPhase = GamePhase.Night;
        private int localBaseVotePower = 1;
        private readonly List<StatusEffectInstance> localEffects = new();
        private readonly HashSet<int> localAllowedTargets = new();

        private T[] SceneComponents<T>() where T : Component
        {
            // The scene entry runs during Awake, before Unity marks the scene loaded.
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(component => component.gameObject.scene.name == ThuongOnlineBootstrap.SceneName).ToArray();
        }
        private T SceneComponent<T>() where T : Component => SceneComponents<T>().FirstOrDefault();

        private void PrepareOnlineLogic()
        {
            if (game == null) throw new InvalidOperationException("Scene Thương Online thiếu bộ điều phối gameplay.");
            var statusSystem = SceneComponent<StatusEffectSystem>();
            if (statusSystem == null)
            {
                var root = new GameObject("Thuong Online Status Effects"); root.transform.SetParent(game.transform, false);
                statusSystem = root.AddComponent<StatusEffectSystem>();
            }
            var abilitySystem = SceneComponent<AbilitySystem>();
            if (abilitySystem == null)
            {
                var root = new GameObject("Thuong Online Abilities"); root.transform.SetParent(game.transform, false);
                abilitySystem = root.AddComponent<AbilitySystem>();
            }
            statusSystem.deathSystem = SceneComponent<DeathResolver>();
            abilitySystem.phaseController = game; abilitySystem.nightSystem = SceneComponent<NightManager>();
            if (statusSystem.deathSystem == null || abilitySystem.nightSystem == null)
                throw new InvalidOperationException("Scene Thương Online thiếu hệ thống chết hoặc giải quyết đêm.");
            game.statusEffects = statusSystem; game.abilities = abilitySystem;
            abilitySystem.SetMatchRunning(false);
            game.PhaseChanged += HandleOnlineLogicPhaseChanged;
        }
        private void HandleOnlineLogicPhaseChanged(GamePhase previous, GamePhase current) => nextPrivateTime = 0;

        private int[] AllowedTargets(PlayerData actor)
        {
            if (actor == null || players?.players == null) return Array.Empty<int>();
            return players.players.Where(target => target != null &&
                (RoleManager.UsesConfiguredAbility(actor)
                    ? TargetValidator.Validate(actor, target, actor.roleDefinition).IsValid &&
                      game.abilities.Validate(actor, target).FailureReason != ActionFailureReason.RepeatedTarget
                    : TargetValidator.ValidateLegacy(actor, target).IsValid))
                .Select(target => target.playerID).ToArray();
        }
        public bool CanTargetLocalAbility(int playerID) => HasLocalRole && localAllowedTargets.Contains(playerID);
        public string LocalStatusSummary => string.Join(" • ", localEffects.Select(effect =>
            $"{EffectLabel(effect.Type)}{(effect.Type == StatusEffectType.ExtraVote ? " +" + effect.Strength : "")} (còn {effect.RemainingTicks} lượt)"));
        public int LocalVoteWeight => (privateFlags & 32) == 0 ? 0 : localBaseVotePower +
            localEffects.Where(effect => effect.Type == StatusEffectType.ExtraVote).Sum(effect => effect.Strength);
        private static string EffectLabel(StatusEffectType type) => type switch
        {
            StatusEffectType.Protected => "Được bảo vệ", StatusEffectType.CannotVote => "Cấm bỏ phiếu",
            StatusEffectType.ExtraVote => "Thêm phiếu", StatusEffectType.Silenced => "Khóa kỹ năng",
            StatusEffectType.Cursed => "Lời nguyền", _ => type.ToString()
        };

        private void CaptureLocalLogic(PlayerData player, int[] allowedTargets)
        {
            privateMatchEpoch = matchEpoch;
            localAbilityPhase = RoleManager.UsesConfiguredAbility(player) ? player.roleDefinition.usablePhase : GamePhase.Night;
            localBaseVotePower = Mathf.Max(1, player.votePower);
            localEffects.Clear();
            if (player.effects != null)
                foreach (var effect in player.effects)
                    localEffects.Add(new StatusEffectInstance(effect.Type, effect.Strength, effect.RemainingTicks, effect.TickOnPhase));
            localAllowedTargets.Clear(); localAllowedTargets.UnionWith(allowedTargets);
        }
        private void WritePrivateLogic(FastBufferWriter writer, PlayerData player, int[] targets)
        {
            writer.WriteValueSafe(matchEpoch);
            writer.WriteValueSafe((byte)(RoleManager.UsesConfiguredAbility(player) ? player.roleDefinition.usablePhase : GamePhase.Night));
            writer.WriteValueSafe(Mathf.Max(1, player.votePower));
            var effects = player.effects ?? new List<StatusEffectInstance>();
            writer.WriteValueSafe((ushort)effects.Count);
            foreach (var effect in effects)
            {
                writer.WriteValueSafe((byte)effect.Type); writer.WriteValueSafe(effect.Strength);
                writer.WriteValueSafe(effect.RemainingTicks); writer.WriteValueSafe((byte)effect.TickOnPhase);
            }
            writer.WriteValueSafe((ushort)targets.Length);
            foreach (int id in targets) writer.WriteValueSafe(id);
        }
        private bool ReadPrivateLogic(FastBufferReader reader)
        {
            reader.ReadValueSafe(out uint epoch);
            if (epoch < matchEpoch || epoch < privateMatchEpoch) return false;
            privateMatchEpoch = epoch;
            reader.ReadValueSafe(out byte phase); localAbilityPhase = (GamePhase)phase;
            reader.ReadValueSafe(out localBaseVotePower);
            reader.ReadValueSafe(out ushort count);
            if (count > (MaxPlayers + 1) * 5) throw new InvalidOperationException("Too many personal status effects.");
            localEffects.Clear();
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out byte type); reader.ReadValueSafe(out int strength);
                reader.ReadValueSafe(out int ticks); reader.ReadValueSafe(out byte tickPhase);
                if (!Enum.IsDefined(typeof(StatusEffectType), (int)type) || strength < 1 || ticks < 1)
                    throw new InvalidOperationException("Invalid personal status effect.");
                localEffects.Add(new StatusEffectInstance((StatusEffectType)type, strength, ticks, (GamePhase)tickPhase));
            }
            reader.ReadValueSafe(out ushort targetCount);
            if (targetCount > MaxPlayers) throw new InvalidOperationException("Too many ability targets.");
            localAllowedTargets.Clear();
            for (int i = 0; i < targetCount; i++) { reader.ReadValueSafe(out int id); localAllowedTargets.Add(id); }
            return true;
        }

        private void ResetReplicatedRound(uint epoch)
        {
            matchEpoch = epoch;
            matchResult = null;
            localRoleCardOpen = localRoleConfirmed = false;
            localActionFeedback = string.Empty;
            if (privateMatchEpoch != epoch)
            {
                privateStateReceived = false; privateFlags = 0;
                localEffects.Clear(); localAllowedTargets.Clear(); localVotedTarget = -1;
            }
            ResetEndgamePresentation(); ResetVotePresentation();
        }

        public bool CanRestartMatch => network != null && network.IsServer && network.IsListening &&
            connected && matchResult != null && game != null && game.currentState == GameState.GameOver;
        public bool RestartMatchInRoom()
        {
            if (!CanRestartMatch) return false;
            ResetMinigames();
            game.ResetForOnlineLobby();
            matchEpoch++;
            matchStarted = rolesAssigned = roomLocked = false;
            matchResult = null; privateStateReceived = false; privateFlags = 0;
            localRoleCardOpen = localRoleConfirmed = false;
            localEffects.Clear(); localAllowedTargets.Clear(); privateFeedback.Clear();
            localActionFeedback = string.Empty; localVotedTarget = -1;
            roles.playerRoles.Clear(); players.players.Clear(); tasks.ResetForNewMatch();
            VoteManager.Instance?.StartVote();
            foreach (var member in serverPlayers.Values)
            {
                member.Ready = member.RevealRead = false; member.Input = Vector2.zero;
            }
            onlineOutcomeDirty = true;
            ResetVotePresentation(); ResetEndgamePresentation();
            status = "Ván mới đã sẵn sàng. Chủ phòng phân vai lại, mọi người đọc vai và Ready.";
            BroadcastRoster(); BroadcastSnapshot();
            return true;
        }
    }
}
