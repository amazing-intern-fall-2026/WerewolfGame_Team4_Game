using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Thuong.Emotes;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private const float RealtimeInteractionRange = 1.5f;
        private const float InteractionHoldSeconds = .3f;

        private enum LocalInteractionKind : byte { ReportCorpse, CompleteTask, UseAbility }

        private readonly struct LocalInteractionOption
        {
            public readonly LocalInteractionKind Kind;
            public readonly int Id;
            public readonly string Label;
            public LocalInteractionOption(LocalInteractionKind kind, int id, string label)
            { Kind = kind; Id = id; Label = label; }
        }

        private sealed class OnlineCorpse
        {
            public int Id;
            public int VictimId;
            public Vector2 Position;
        }

        private readonly List<OnlineCorpse> onlineCorpses = new();
        private readonly Dictionary<int, GameObject> corpseMarkers = new();
        private int nextCorpseId = 1;
        private int realtimeNight = -1;
        private bool sharedRealtimeAttackUsed;
        private Sprite corpseSprite;
        private readonly List<LocalInteractionOption> interactionOptions = new();
        private LocalInteractionOption selectedInteraction;
        private bool hasSelectedInteraction, interactionKeyPending, interactionMenuOpen, interactionHoldConsumed;
        private float interactionKeyDownAt;
        private int interactionMenuSelection;
        private uint interactionPressEpoch;
        private int interactionPressDay;
        private GamePhase interactionPressPhase;
        private int selectedInteractionDay;
        private GamePhase selectedInteractionPhase;
        private bool InteractionMenuOpen => interactionMenuOpen;

        private bool LocalPlayerAlive => HasLocalRole && (privateFlags & 1) != 0;
        public ThuongMatchMode ActiveMatchMode => roomRules.mode;

        private void ObserveRealtimeDeaths()
        {
            DeathResolver deathResolver = SceneComponent<DeathResolver>();
            if (deathResolver != null) deathResolver.DeathResolved += HandleRealtimeDeath;
        }

        private void StopObservingRealtimeDeaths()
        {
            DeathResolver deathResolver = SceneComponent<DeathResolver>();
            if (deathResolver != null) deathResolver.DeathResolved -= HandleRealtimeDeath;
        }

        private void HandleRealtimeDeath(DeathResult result)
        {
            if (!network.IsServer || roomRules.mode != ThuongMatchMode.RealtimeProximity ||
                result.Outcome != DeathOutcome.Killed || result.Request.Cause is DeathCause.Vote or DeathCause.Disconnect ||
                result.Request.Target == null) return;
            ThuongOnlineServerPlayer victim = serverPlayers.Values.FirstOrDefault(item =>
                item.PlayerId == result.Request.Target.playerID);
            if (victim == null) return;
            onlineCorpses.Add(new OnlineCorpse
            {
                Id = nextCorpseId++,
                VictimId = victim.PlayerId,
                Position = victim.Position
            });
            SyncCorpseMarkers(onlineCorpses.Where(corpse => CanObserveVillagePosition(localPlayerId, corpse.Position)).ToArray());
            nextSnapshotTime = 0;
        }

        private void ProcessContextInteractionInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (interactionKeyPending && (interactionPressEpoch != matchEpoch || interactionPressDay != visibleDay || interactionPressPhase != visiblePhase))
            {
                ResetInteractionInput();
                if (keyboard == null || !keyboard.eKey.isPressed) return;
            }
            if (hasSelectedInteraction && (selectedInteractionDay != visibleDay || selectedInteractionPhase != visiblePhase))
                hasSelectedInteraction = false;
            if (keyboard == null || !Application.isFocused || !matchStarted || !LocalPlayerAlive ||
                NotebookIsOpen || TutorialIsOpen || IsPlayingTask || PlayerEmoteWheelUI.IsOpenFor(this))
            { ResetInteractionInput(); return; }

            if (roomRules.mode != ThuongMatchMode.RealtimeProximity)
            {
                if (keyboard.eKey.wasPressedThisFrame) RequestNearestTask();
                return;
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                interactionKeyPending = true;
                interactionKeyDownAt = Time.unscaledTime;
                interactionPressEpoch = matchEpoch; interactionPressDay = visibleDay; interactionPressPhase = visiblePhase;
                interactionMenuOpen = false;
                interactionHoldConsumed = false;
            }

            if (interactionKeyPending && keyboard.eKey.isPressed && !interactionMenuOpen &&
                Time.unscaledTime - interactionKeyDownAt >= InteractionHoldSeconds)
            {
                interactionHoldConsumed = true;
                interactionOptions.Clear(); interactionOptions.AddRange(BuildInteractionOptions());
                if (interactionOptions.Count > 1)
                {
                    interactionMenuOpen = true;
                    interactionMenuSelection = 0;
                    hasSelectedInteraction = false;
                }
                else status = interactionOptions.Count == 1
                    ? "Chỉ có một tương tác gần. Thả E rồi nhấn nhanh để thực hiện."
                    : "Không có tương tác nào trong tầm.";
            }

            if (interactionMenuOpen)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame ||
                    keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                    interactionMenuSelection = (interactionMenuSelection - 1 + interactionOptions.Count) % interactionOptions.Count;
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame ||
                    keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                    interactionMenuSelection = (interactionMenuSelection + 1) % interactionOptions.Count;
            }

            if (!keyboard.eKey.wasReleasedThisFrame || !interactionKeyPending) return;
            if (interactionMenuOpen && interactionOptions.Count > 0)
            {
                selectedInteraction = interactionOptions[interactionMenuSelection];
                hasSelectedInteraction = true;
                selectedInteractionDay = visibleDay; selectedInteractionPhase = visiblePhase;
                status = $"Đã chọn {selectedInteraction.Label}. Nhấn nhanh E để thực hiện.";
            }
            else if (!interactionHoldConsumed)
            {
                List<LocalInteractionOption> current = BuildInteractionOptions();
                if (hasSelectedInteraction)
                {
                    bool stillAvailable = current.Any(option => option.Kind == selectedInteraction.Kind && option.Id == selectedInteraction.Id);
                    if (stillAvailable) ExecuteLocalInteraction(selectedInteraction);
                    else status = "Tương tác đã rời tầm hoặc không còn hợp lệ. Hãy chọn lại.";
                }
                else if (current.Count > 0) ExecuteLocalInteraction(current[0]);
                else status = "Không có tương tác nào trong tầm.";
                hasSelectedInteraction = false;
            }
            interactionKeyPending = false;
            interactionMenuOpen = false;
            interactionOptions.Clear();
        }

        private List<LocalInteractionOption> BuildInteractionOptions()
        {
            var options = new List<LocalInteractionOption>();
            if (!avatars.TryGetValue(localPlayerId, out ThuongOnlineAvatar avatar)) return options;
            Vector2 position = avatar.Root.transform.position;
            bool freeRoam = visibleState == GameState.Day || visibleState == GameState.Night && visiblePhase == GamePhase.Night;
            if (!freeRoam) return options;

            foreach (OnlineCorpse corpse in onlineCorpses.OrderBy(item => Vector2.Distance(position, item.Position)))
                if (Vector2.Distance(position, corpse.Position) <= RealtimeInteractionRange && SelectedMap != null &&
                    Thuong.Maps.ThuongMapCollision.HasLine(SelectedMap, position, corpse.Position))
                    options.Add(new LocalInteractionOption(LocalInteractionKind.ReportCorpse, corpse.Id, "Báo xác"));

            if (visibleState == GameState.Day)
            {
                int stationId = NearestAvailableTask(position, out float distance);
                if (stationId >= 0 && distance <= stations[stationId].interactionRadius + .25f)
                    options.Add(new LocalInteractionOption(LocalInteractionKind.CompleteTask, stationId,
                        "Nhiệm vụ: " + stations[stationId].task.taskName));
            }
            else if (privateRole is RoleType.DogSpirit or RoleType.VillageGuardian or RoleType.Seer)
            {
                int targetId = NearestAlivePlayer(position, out _);
                if (targetId >= 0)
                {
                    string action = privateRole switch
                    { RoleType.DogSpirit => "Tấn công", RoleType.VillageGuardian => "Bảo vệ", _ => "Soi" };
                    string name = publicPlayers.First(player => player.PlayerId == targetId).Name;
                    options.Add(new LocalInteractionOption(LocalInteractionKind.UseAbility, targetId, $"{action}: {name}"));
                }
            }
            if (visibleState == GameState.Night)
            {
                int abilityOption = options.FindIndex(option => option.Kind == LocalInteractionKind.UseAbility);
                if (abilityOption > 0)
                {
                    LocalInteractionOption ability = options[abilityOption];
                    options.RemoveAt(abilityOption); options.Insert(0, ability);
                }
            }
            return options;
        }

        private void ExecuteLocalInteraction(LocalInteractionOption option)
        {
            switch (option.Kind)
            {
                case LocalInteractionKind.ReportCorpse:
                    SendAction(ThuongOnlineAction.ReportCorpse, option.Id);
                    break;
                case LocalInteractionKind.CompleteTask:
                    if (option.Id >= 0 && option.Id < stations.Length) RequestTaskMinigame(option.Id);
                    break;
                case LocalInteractionKind.UseAbility:
                    SendAction(ThuongOnlineAction.RealtimeAbility, option.Id);
                    break;
            }
        }

        private void ResetInteractionInput()
        {
            interactionKeyPending = interactionMenuOpen = interactionHoldConsumed = hasSelectedInteraction = false;
            interactionOptions.Clear();
        }

        private int NearestAlivePlayer(Vector2 position, out float distance)
        {
            int selected = -1;
            distance = float.MaxValue;
            foreach (ThuongOnlinePublicPlayer candidate in publicPlayers)
            {
                if (!candidate.Alive || candidate.PlayerId == localPlayerId) continue;
                float candidateDistance = Vector2.Distance(position, candidate.Position);
                if (candidateDistance >= distance || candidateDistance > RealtimeInteractionRange) continue;
                if (SelectedMap == null || !Thuong.Maps.ThuongMapCollision.HasLine(SelectedMap, position, candidate.Position)) continue;
                selected = candidate.PlayerId;
                distance = candidateDistance;
            }
            return selected;
        }

        private bool TryRealtimeAbility(ThuongOnlineServerPlayer member, PlayerData actor, int targetId, out string feedback)
        {
            feedback = "Kỹ năng không khả dụng.";
            if (minigameAuthority.IsPlaying(member.ClientId))
            { feedback = "Đóng nhiệm vụ trước khi dùng kỹ năng."; return false; }
            if (roomRules.mode != ThuongMatchMode.RealtimeProximity || game.currentState != GameState.Night || game.currentPhase != GamePhase.Night ||
                !actor.isAlive || actor.HasEffect(StatusEffectType.Silenced))
            { feedback = "Chỉ người sống được dùng E vào ban đêm; kỹ năng có thể bị khóa."; return false; }
            if (!TryValidateInteraction(member, targetId, out PlayerData target, out feedback)) return false;

            int night = NightManager.Instance != null ? NightManager.Instance.CurrentNightNumber : game.currentDay;
            if (night != realtimeNight)
            {
                realtimeNight = night;
                sharedRealtimeAttackUsed = false;
            }

            switch (actor.roleType)
            {
                case RoleType.DogSpirit:
                    if (sharedRealtimeAttackUsed) { feedback = "Bầy đã dùng lượt tấn công đêm này."; return false; }
                    sharedRealtimeAttackUsed = true;
                    actor.hasUseNightAction = true;
                    var death = DeathResolver.Instance.TryKill(new DeathRequest(target, DeathCause.Monster, actor,
                        sourceRole: RoleType.DogSpirit));
                    feedback = death.Outcome == DeathOutcome.Killed ? $"Đã hạ {target.DisplayName}." : death.Message;
                    actor.NotifyChanged();
                    return death.Outcome is DeathOutcome.Killed or DeathOutcome.Prevented;

                case RoleType.VillageGuardian:
                    if (actor.hasUseNightAction) { feedback = "Bạn đã dùng kỹ năng trong đêm này."; return false; }
                    if (!roles.playerRoles.TryGetValue(actor.playerID, out BaseRole baseRole) || baseRole is not GuardianRole guardian)
                    { feedback = "Không tìm thấy vai trò Bảo vệ."; return false; }
                    if (guardian.lastTarget == target.playerID)
                    { feedback = "Không thể bảo vệ cùng một người hai đêm liên tiếp."; return false; }
                    target.status ??= new PlayerStatus();
                    target.status.isProtected = true;
                    guardian.lastTarget = target.playerID;
                    actor.hasUseNightAction = true;
                    target.NotifyChanged(); actor.NotifyChanged();
                    feedback = $"Đã bảo vệ {target.DisplayName} cho đến hết đêm.";
                    return true;

                case RoleType.Seer:
                    if (actor.hasUseNightAction) { feedback = "Bạn đã dùng kỹ năng trong đêm này."; return false; }
                    actor.hasUseNightAction = true;
                    actor.NotifyChanged();
                    bool isDog = target.roleType == RoleType.DogSpirit;
                    feedback = isDog ? $"{target.DisplayName} thuộc phe Khuyển tinh." : $"{target.DisplayName} không phải Khuyển tinh.";
                    return true;

                default:
                    feedback = "Vai trò này chưa có tương tác E trong mode mới.";
                    return false;
            }
        }

        private bool TryValidateInteraction(ThuongOnlineServerPlayer actorMember, int targetId,
            out PlayerData target, out string feedback)
        {
            target = null;
            if (!serverPlayers.Values.Any(item => item.PlayerId == targetId && !item.Offline))
            { feedback = "Mục tiêu không còn trong ván."; return false; }
            target = players?.GetplayerByID(targetId);
            if (target == null || !target.isAlive || target.playerID == actorMember.PlayerId)
            { feedback = "Mục tiêu không hợp lệ."; return false; }
            ThuongOnlineServerPlayer targetMember = serverPlayers.Values.First(item => item.PlayerId == targetId);
            float distance = Vector2.Distance(actorMember.Position, targetMember.Position);
            if (distance > RealtimeInteractionRange)
            { feedback = "Mục tiêu đã ra khỏi tầm tương tác."; return false; }
            if (SelectedMap == null || !Thuong.Maps.ThuongMapCollision.HasLine(SelectedMap, actorMember.Position, targetMember.Position))
            { feedback = "Có vật cản giữa bạn và mục tiêu."; return false; }
            feedback = string.Empty;
            return true;
        }

        private bool TryReportCorpse(ThuongOnlineServerPlayer member, PlayerData actor, int corpseId, out string feedback)
        {
            feedback = "Báo xác không khả dụng.";
            if (roomRules.mode != ThuongMatchMode.RealtimeProximity || !actor.isAlive ||
                game.currentState is not (GameState.Day or GameState.Night) ||
                game.currentPhase is not (GamePhase.DayStart or GamePhase.Event or GamePhase.Task or GamePhase.Night))
            { feedback = "Chỉ người sống có thể báo xác trong lúc tự do di chuyển."; return false; }
            if (minigameAuthority.IsPlaying(member.ClientId))
            { feedback = "Đóng nhiệm vụ trước khi báo xác."; return false; }
            OnlineCorpse corpse = onlineCorpses.FirstOrDefault(item => item.Id == corpseId);
            if (corpse == null) { feedback = "Xác này đã được báo hoặc không còn tồn tại."; return false; }
            float distance = Vector2.Distance(member.Position, corpse.Position);
            if (distance > RealtimeInteractionRange)
            { feedback = "Bạn đang ở ngoài tầm báo xác."; return false; }
            if (SelectedMap == null || !Thuong.Maps.ThuongMapCollision.HasLine(SelectedMap, member.Position, corpse.Position))
            { feedback = "Có vật cản giữa bạn và xác."; return false; }

            if (!game.StartReportedMeeting())
            { feedback = "Cuộc họp không thể mở ở giai đoạn hiện tại."; return false; }
            onlineCorpses.Remove(corpse);
            RemoveCorpseMarker(corpse.Id);
            feedback = "Đã báo xác. Cuộc họp bắt đầu.";
            nextSnapshotTime = 0;
            return true;
        }

        private void WriteCorpseSnapshot(FastBufferWriter writer, int observerId)
        {
            OnlineCorpse[] visible = onlineCorpses.Where(corpse => CanObserveVillagePosition(observerId, corpse.Position))
                .Take(MaxPlayers).ToArray();
            int count = visible.Length;
            writer.WriteValueSafe((byte)count);
            for (int i = 0; i < count; i++)
            {
                OnlineCorpse corpse = visible[i];
                writer.WriteValueSafe(corpse.Id); writer.WriteValueSafe(corpse.VictimId);
                writer.WriteValueSafe(corpse.Position.x); writer.WriteValueSafe(corpse.Position.y);
            }
        }

        private bool ReadCorpseSnapshot(FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte count);
            if (count > MaxPlayers) return false;
            var received = new List<OnlineCorpse>(count);
            bool valid = true;
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out int id); reader.ReadValueSafe(out int victimId);
                reader.ReadValueSafe(out float x); reader.ReadValueSafe(out float y);
                if (id <= 0 || !float.IsFinite(x) || !float.IsFinite(y))
                { valid = false; continue; }
                received.Add(new OnlineCorpse { Id = id, VictimId = victimId, Position = new Vector2(x, y) });
            }
            if (!valid) return false;
            SyncCorpseMarkers(received);
            onlineCorpses.Clear(); onlineCorpses.AddRange(received);
            return true;
        }

        private void SyncCorpseMarkers(IReadOnlyList<OnlineCorpse> received)
        {
            var active = new HashSet<int>(received.Select(item => item.Id));
            foreach (int id in corpseMarkers.Keys.Where(id => !active.Contains(id)).ToArray()) RemoveCorpseMarker(id);
            foreach (OnlineCorpse corpse in received)
            {
                if (!corpseMarkers.TryGetValue(corpse.Id, out GameObject marker))
                {
                    marker = new GameObject("Reported Death Marker " + corpse.Id, typeof(SpriteRenderer));
                    marker.transform.SetParent(transform, false);
                    var renderer = marker.GetComponent<SpriteRenderer>();
                    renderer.sprite = GetCorpseSprite(); renderer.sortingOrder = 100;
                    corpseMarkers.Add(corpse.Id, marker);
                }
                marker.transform.position = corpse.Position + Vector2.up * .3f;
            }
        }

        private Sprite GetCorpseSprite()
        {
            if (corpseSprite != null) return corpseSprite;
            const int size = 48;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - (size - 1) * .5f, dy = y - (size - 1) * .5f;
                bool ring = dx * dx + dy * dy < 21 * 21;
                bool cross = Mathf.Abs(dx - dy) < 3 || Mathf.Abs(dx + dy) < 3;
                pixels[y * size + x] = !ring ? new Color32(0, 0, 0, 0)
                    : cross ? new Color32(255, 237, 214, 255) : new Color32(146, 32, 27, 245);
            }
            texture.SetPixels32(pixels); texture.Apply();
            corpseSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 48);
            return corpseSprite;
        }

        private void RemoveCorpseMarker(int id)
        {
            if (!corpseMarkers.Remove(id, out GameObject marker)) return;
            if (marker != null) Destroy(marker);
        }

        private void ResetCorpseState()
        {
            onlineCorpses.Clear();
            ResetInteractionInput();
            foreach (int id in corpseMarkers.Keys.ToArray()) RemoveCorpseMarker(id);
            if (corpseSprite != null)
            {
                Texture2D texture = corpseSprite.texture;
                Destroy(corpseSprite);
                if (texture != null) Destroy(texture);
                corpseSprite = null;
            }
            nextCorpseId = 1; realtimeNight = -1; sharedRealtimeAttackUsed = false;
        }
    }
}
