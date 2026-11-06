using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private RectTransform spectatorPanel;
        private TMP_Text spectatorLabel;
        private GameObject spectatorNightMask;
        private Button spectatorPrevious, spectatorNext, spectatorOverview;
        private int spectatorTargetId = -1;

        private void RefreshSpectatorHud(GameHUD hud)
        {
            if (hud == null) return;
            if (!matchStarted)
            {
                spectatorPanel?.gameObject.SetActive(false);
                spectatorNightMask?.SetActive(false);
                return;
            }
            EnsureSpectatorPanel(hud);
            if (spectatorPanel == null) return;

            bool localAlive = HasLocalRole && (privateFlags & 1) != 0;
            bool hasPendingHunterAction = HasLocalRole && (privateFlags & 16) != 0;
            bool spectating = HasLocalRole && !localAlive && !hasPendingHunterAction && matchResult == null;
            spectatorPanel.gameObject.SetActive(spectating);
            if (!spectating)
            {
                spectatorTargetId = -1;
                spectatorNightMask?.SetActive(false);
                return;
            }

            if (visibleState == GameState.Night)
            {
                spectatorTargetId = -1;
                if (worldCamera != null)
                {
                    worldCamera.overview = false;
                    if (worldCamera.target != null) worldCamera.Follow(null);
                }
                spectatorNightMask?.SetActive(true);
                if (spectatorLabel != null) spectatorLabel.text = "ĐÃ BỊ LOẠI · Góc nhìn bị giới hạn ban đêm";
                spectatorPrevious.interactable = spectatorNext.interactable = false;
                spectatorOverview.interactable = false;
                return;
            }
            spectatorNightMask?.SetActive(false);

            var living = publicPlayers
                .Where(player => player.Alive && player.PlayerId != localPlayerId &&
                    !lobby.Any(member => member.PlayerId == player.PlayerId && member.Offline))
                .OrderBy(player => player.PlayerId)
                .ToArray();
            if (living.Length == 0)
            {
                spectatorTargetId = -1;
                worldCamera?.Follow(null);
                if (spectatorLabel != null) spectatorLabel.text = "ĐÃ BỊ LOẠI · Không còn người sống để theo dõi";
                spectatorPrevious.interactable = spectatorNext.interactable = false;
                spectatorOverview.interactable = false;
                return;
            }

            if (!living.Any(player => player.PlayerId == spectatorTargetId))
                spectatorTargetId = living[0].PlayerId;
            var target = living.First(player => player.PlayerId == spectatorTargetId);
            if (spectatorLabel != null)
                spectatorLabel.text = $"ĐÃ BỊ LOẠI · Đang theo dõi {target.Name}";
            spectatorPrevious.interactable = spectatorNext.interactable = living.Length > 1;
            spectatorOverview.interactable = true;

            if (worldCamera != null && !worldCamera.overview && avatars.TryGetValue(target.PlayerId, out var avatar) &&
                avatar.Root != null && worldCamera.target != avatar.Root.transform)
                worldCamera.Follow(avatar.Root.transform);
        }

        private void EnsureSpectatorPanel(GameHUD hud)
        {
            if (spectatorPanel != null) return;
            var root = new GameObject("Spectator controls", typeof(RectTransform), typeof(Image));
            spectatorPanel = root.GetComponent<RectTransform>();
            spectatorPanel.SetParent(hud.transform, false);
            spectatorPanel.anchorMin = new Vector2(.27f, .84f);
            spectatorPanel.anchorMax = new Vector2(.73f, .975f);
            spectatorPanel.offsetMin = spectatorPanel.offsetMax = Vector2.zero;
            var background = root.GetComponent<Image>();
            background.color = new Color(.055f, .12f, .15f, .94f);
            ThuongMedievalTheme.Active?.StylePanel(background, "Spectator controls");

            var nightMask = new GameObject("Spectator night privacy", typeof(RectTransform), typeof(Image));
            var maskRect = nightMask.GetComponent<RectTransform>();
            maskRect.SetParent(hud.transform, false);
            maskRect.anchorMin = Vector2.zero; maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = maskRect.offsetMax = Vector2.zero;
            var maskImage = nightMask.GetComponent<Image>();
            maskImage.color = new Color(.015f, .025f, .04f, .97f);
            maskImage.raycastTarget = false;
            nightMask.transform.SetAsFirstSibling();
            spectatorNightMask = nightMask;
            spectatorNightMask.SetActive(false);

            var labelObject = new GameObject("Spectator status", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(spectatorPanel, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(.03f, .58f); labelRect.anchorMax = new Vector2(.97f, .98f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            spectatorLabel = labelObject.GetComponent<TMP_Text>();
            spectatorLabel.font = hud.phaseText != null ? hud.phaseText.font : null;
            spectatorLabel.fontSize = 17;
            spectatorLabel.alignment = TextAlignmentOptions.Center;
            spectatorLabel.raycastTarget = false;
            spectatorLabel.enableAutoSizing = true;
            spectatorLabel.fontSizeMin = 12; spectatorLabel.fontSizeMax = 18;
            ThuongMedievalTheme.Active?.StyleText(spectatorLabel);

            spectatorPrevious = MatchButton(hud, "‹ TRƯỚC", spectatorPanel,
                new Vector2(.025f, .06f), new Vector2(.31f, .55f), () => CycleSpectatorTarget(-1));
            spectatorOverview = MatchButton(hud, "TOÀN MAP", spectatorPanel,
                new Vector2(.345f, .06f), new Vector2(.655f, .55f), ShowSpectatorOverview);
            spectatorNext = MatchButton(hud, "SAU ›", spectatorPanel,
                new Vector2(.69f, .06f), new Vector2(.975f, .55f), () => CycleSpectatorTarget(1));
            spectatorPanel.gameObject.SetActive(false);
        }

        private void CycleSpectatorTarget(int direction)
        {
            var living = publicPlayers
                .Where(player => player.Alive && player.PlayerId != localPlayerId &&
                    !lobby.Any(member => member.PlayerId == player.PlayerId && member.Offline))
                .OrderBy(player => player.PlayerId)
                .ToArray();
            if (living.Length == 0) return;
            int current = System.Array.FindIndex(living, player => player.PlayerId == spectatorTargetId);
            int next = current < 0 ? 0 : (current + direction + living.Length) % living.Length;
            spectatorTargetId = living[next].PlayerId;
            if (worldCamera != null && avatars.TryGetValue(spectatorTargetId, out var avatar) && avatar.Root != null)
            {
                worldCamera.overview = false;
                worldCamera.Follow(avatar.Root.transform);
            }
        }

        private void ShowSpectatorOverview()
        {
            if (worldCamera == null) return;
            worldCamera.overview = true;
            worldCamera.Follow(null);
        }
    }
}
