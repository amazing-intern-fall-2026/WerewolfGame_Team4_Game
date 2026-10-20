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
        [SerializeField] private ThuongOnlineWinSettings winSettings = new();
        private WinConditionManager scopedWinManager;
        private ThuongMatchResult matchResult;
        private int matchMaxDay = 7;
        private NetworkGameOverManager suspendedGameOverManager;
        private bool restoreGameOverManager;

        public string LocalWinObjective
        {
            get
            {
                if (privateRole == RoleType.Madman) return "Bị vote loại trong ngày 1 để thắng riêng. Bị giết bằng cách khác không đạt điều kiện này.";
                if (privateRole == RoleType.FoxSpirit) return "Thắng riêng khi bạn còn sống, đã mê hoặc ít nhất 2 người và toàn bộ người đã mê hoặc đều còn sống.";
                if (privateRole == RoleType.Killer) return $"Thắng riêng khi còn sống, hết Ma Sói / Bạch Khuyển thức tỉnh đối địch và còn tối đa {winSettings.specialWinVillageLimit} dân.";
                if (privateRole == RoleType.WhiteHound || privateRole == RoleType.WhiteWolf)
                    return $"Trước thức tỉnh: thắng cùng Dân Làng. Sau thức tỉnh: thắng riêng khi hết Ma Sói / Sát nhân đối địch và còn tối đa {winSettings.specialWinVillageLimit} dân.";
                string team = privateFaction == FactionType.Monster
                    ? "Thắng khi hết ngày quy định mà phe vẫn còn sống và nhiệm vụ chưa đủ 100%." +
                      (winSettings.eliminationWin ? " Hoặc loại hết những người ngoài phe." : "") +
                      (winSettings.eliminationWin && lobby.Count >= Mathf.Max(3, winSettings.minimumPlayersForParity)
                          ? " Hoặc đạt thế cân bằng quân số khi không còn phe tấn công đối địch." : "")
                    : privateFaction == FactionType.Villager
                        ? "Thắng khi tổng nhiệm vụ đạt 100%." + (winSettings.eliminationWin ? " Hoặc loại hết Ma Sói, Sát nhân và Bạch Khuyển thức tỉnh." : "")
                        : "Thực hiện điều kiện thắng riêng của vai trò.";
                return team + $" Ván giới hạn {matchMaxDay} ngày. Đồng đội đã bị loại vẫn thắng cùng phe.\n" +
                    "Nếu là một trong hai người yêu nhau cuối cùng còn sống, cặp đôi thắng riêng.";
            }
        }

        private void WriteWinSettings(FastBufferWriter writer)
        {
            matchMaxDay = scopedWinManager != null ? scopedWinManager.MaxDay : matchMaxDay;
            writer.WriteValueSafe(winSettings.eliminationWin);
            writer.WriteValueSafe(winSettings.minimumPlayersForParity);
            writer.WriteValueSafe(winSettings.specialWinVillageLimit);
            writer.WriteValueSafe(matchMaxDay);
        }

        private void ReadWinSettings(FastBufferReader reader)
        {
            reader.ReadValueSafe(out winSettings.eliminationWin);
            reader.ReadValueSafe(out winSettings.minimumPlayersForParity);
            reader.ReadValueSafe(out winSettings.specialWinVillageLimit);
            reader.ReadValueSafe(out matchMaxDay);
        }

        private void PrepareOnlineWinRules()
        {
            // NetworkManager moves this runtime root to DontDestroyOnLoad.
            // Scope by the gameplay manager's scene, not the runtime root's scene.
            if (game == null || game.gameObject.scene.name != ThuongOnlineBootstrap.SceneName) return;
            winSettings ??= new ThuongOnlineWinSettings();
            winSettings.minimumPlayersForParity = Mathf.Max(3, winSettings.minimumPlayersForParity);
            winSettings.specialWinVillageLimit = Mathf.Max(0, winSettings.specialWinVillageLimit);
            scopedWinManager = FindObjectsByType<WinConditionManager>(FindObjectsInactive.Include)
                .FirstOrDefault(manager => manager.gameObject.scene == game.gameObject.scene);
            scopedWinManager?.InstallMatchEvaluator(game, EvaluateOnlineWin);
            // main's legacy manager auto-installs globally and evaluates its own
            // rules. Suspend it before Start while this scene owns the match.
            suspendedGameOverManager = NetworkGameOverManager.Instance;
            restoreGameOverManager = suspendedGameOverManager.enabled;
            suspendedGameOverManager.enabled = false;
            suspendedGameOverManager.ResetUIState();
        }

        private void RestoreOtherGameOverManager()
        {
            if (suspendedGameOverManager != null && restoreGameOverManager)
                suspendedGameOverManager.enabled = true;
            suspendedGameOverManager = null;
            restoreGameOverManager = false;
        }

        private void LateUpdate()
        {
            // Submitted night attacks are still pending. A charm must not win
            // before an attack that kills its owner has been resolved.
            if (game != null && game.currentState == GameState.Night) return;
            // Standalone abilities, faction changes, tasks and death chains have
            // completed their synchronous work by this point. Phase resolutions
            // also call this evaluator explicitly before advancing the day.
            EvaluateOnlineWin(false);
        }

        private void EvaluateOnlineWin(bool dayFinished)
        {
            if (network == null || !network.IsServer || !matchStarted || matchResult != null ||
                game == null || game.currentState == GameState.GameOver || players == null ||
                scopedWinManager == null || !scopedWinManager.HasMatchEvaluator(game)) return;
            var state = new List<ThuongWinPlayer>();
            foreach (var player in players.players)
            {
                if (player == null) continue;
                var member = serverPlayers.Values.FirstOrDefault(item => item.PlayerId == player.playerID);
                bool foxQualified = roles != null && roles.playerRoles.TryGetValue(player.playerID, out BaseRole role) &&
                    role is FoxSpiritRole fox && fox.HasWon;
                state.Add(new ThuongWinPlayer(player.playerID, player.DisplayName, member?.CharacterIndex ?? 0,
                    player.roleType, player.faction, player.isAlive, player.wasVotedOut,
                    player.loverID, player.isWhiteHoundAwakened, foxQualified));
            }
            matchResult = ThuongMatchOutcome.Evaluate(state, winSettings, game.currentDay, game.currentPhase,
                tasks != null ? tasks.progress : 0, scopedWinManager.MaxDay, dayFinished);
            if (matchResult == null) return;

            ResetMinigames();
            foreach (var member in serverPlayers.Values) member.Input = Vector2.zero;
            game.FinishEvaluatedMatch(matchResult.WinnerId);
            status = "Ván chơi đã kết thúc. Xem kết quả thắng / thua của bạn.";
            localActionFeedback = string.Empty;
            privateFeedback.Clear();
            SendAllPrivateStates();
            BroadcastSnapshot();
        }

        private void BalanceOnlineOpeningRoles()
        {
            if (!winSettings.eliminationWin) return;
            // Preserve the attacking Dog Spirit; avoid a 2-wolf / 2-villager
            // opening in a four-player room when parity wins are enabled.
            int cap = Mathf.Max(1, (players.players.Count - 1) / 2);
            var wolves = players.players.Where(player => player.faction == FactionType.Monster && !player.isWhiteHoundAwakened)
                .OrderBy(player => player.roleType == RoleType.DogSpirit).ThenBy(player => player.playerID).ToArray();
            for (int i = 0; i < wolves.Length - cap; i++)
            {
                PlayerData player = wolves[i];
                BaseRole replacement = RoleCatalog.Create(RoleType.Villager, player);
                player.roleType = RoleType.Villager;
                player.faction = FactionType.Villager;
                player.roleDefinition = roles.roleDefinitions != null ? roles.roleDefinitions.Find(RoleType.Villager) : null;
                roles.playerRoles[player.playerID] = replacement;
                replacement.OnGameStart();
                player.NotifyChanged();
            }
        }

        private void WriteMatchResult(FastBufferWriter writer)
        {
            bool finished = matchResult != null && game != null && game.currentState == GameState.GameOver;
            writer.WriteValueSafe(finished);
            if (!finished) return;
            writer.WriteValueSafe((byte)matchResult.Winner);
            writer.WriteValueSafe((byte)matchResult.Reason);
            writer.WriteValueSafe(matchResult.Day);
            writer.WriteValueSafe((byte)matchResult.Phase);
            writer.WriteValueSafe((ushort)matchResult.Players.Count);
            foreach (var player in matchResult.Players)
            {
                writer.WriteValueSafe(player.Id);
                writer.WriteValueSafe(Fixed64(player.Name));
                writer.WriteValueSafe(player.CharacterIndex);
                writer.WriteValueSafe((byte)player.Role);
                writer.WriteValueSafe((byte)player.Faction);
                writer.WriteValueSafe(player.Alive);
                writer.WriteValueSafe(player.Awakened);
                writer.WriteValueSafe(player.Won);
            }
        }

        private void ReadMatchResult(FastBufferReader reader)
        {
            reader.ReadValueSafe(out bool finished);
            if (!finished) return;
            reader.ReadValueSafe(out byte winner);
            reader.ReadValueSafe(out byte reason);
            reader.ReadValueSafe(out int day);
            reader.ReadValueSafe(out byte phase);
            reader.ReadValueSafe(out ushort count);
            if (visibleState != GameState.GameOver || count > MaxPlayers)
                throw new InvalidOperationException("Invalid Thuong match result.");
            var finalPlayers = new List<ThuongFinalPlayer>(count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out int id);
                reader.ReadValueSafe(out FixedString64Bytes name);
                reader.ReadValueSafe(out int character);
                reader.ReadValueSafe(out byte role);
                reader.ReadValueSafe(out byte faction);
                reader.ReadValueSafe(out bool alive);
                reader.ReadValueSafe(out bool awakened);
                reader.ReadValueSafe(out bool won);
                finalPlayers.Add(new ThuongFinalPlayer(id, name.ToString(), character, (RoleType)role,
                    (FactionType)faction, alive, awakened, won));
            }
            // A match has one immutable result, even if another snapshot repeats it.
            matchResult ??= new ThuongMatchResult((ThuongWinner)winner, (ThuongWinReason)reason, day, (GamePhase)phase, finalPlayers);
        }
    }
}
