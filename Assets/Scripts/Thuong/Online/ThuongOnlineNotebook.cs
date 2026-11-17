using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private const int MaxNotebookEntries = 50;
        private const int MaxNotebookCharacters = 240;
        private readonly List<string> notebookEntries = new();
        private RectTransform notebookPanel;
        private TMP_Text notebookHistory;
        private TMP_InputField notebookInput;
        private ScrollRect notebookScroll;
        private bool notebookOpen;
        private uint notebookEpoch;
        private System.Guid notebookRoomToken;

        private void BindNotebookToRound(uint epoch)
        {
            if (notebookEpoch != epoch || notebookRoomToken != reconnectToken) ResetNotebook();
            notebookEpoch = epoch;
            notebookRoomToken = reconnectToken;
        }

        private void SuspendNotebook()
        {
            // Keep both saved entries and an unfinished draft for this match.
            notebookOpen = false;
            if (notebookInput != null) notebookInput.DeactivateInputField();
            if (notebookPanel != null) notebookPanel.gameObject.SetActive(false);
        }

        private bool NotebookIsOpen => notebookOpen && notebookPanel != null && notebookPanel.gameObject.activeSelf;

        private void EnsureNotebookUi(GameHUD hud)
        {
            if (notebookPanel != null || hud == null) return;
            MatchButton(hud, "SỔ TAY", hud.transform,
                new Vector2(.87f, .014f), new Vector2(.98f, .071f), ToggleNotebook);

            var root = new GameObject("Personal notebook", typeof(RectTransform), typeof(Image));
            notebookPanel = root.GetComponent<RectTransform>();
            notebookPanel.SetParent(hud.transform, false);
            notebookPanel.anchorMin = new Vector2(.62f, .15f);
            notebookPanel.anchorMax = new Vector2(.98f, .76f);
            notebookPanel.offsetMin = notebookPanel.offsetMax = Vector2.zero;
            var notebookCanvas = root.AddComponent<Canvas>();
            notebookCanvas.overrideSorting = true; notebookCanvas.sortingOrder = 1900;
            root.AddComponent<GraphicRaycaster>();
            var background = root.GetComponent<Image>();
            background.color = new Color(.055f, .12f, .15f, .98f);
            ThuongMedievalTheme.Active?.StylePanel(background, "Personal notebook");

            var title = CreateNotebookText("Title", notebookPanel, new Vector2(.05f, .88f), new Vector2(.95f, .98f));
            title.text = "SỔ GHI NHỚ RIÊNG";
            title.fontSize = 21;
            title.alignment = TextAlignmentOptions.Center;

            var viewport = LobbyPanel("Notebook viewport", notebookPanel, new Vector2(.06f, .34f), new Vector2(.90f, .85f), false);
            viewport.GetComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            notebookHistory = CreateNotebookText("Notes", viewport, new Vector2(0, 1), Vector2.one);
            notebookHistory.rectTransform.pivot = new Vector2(.5f, 1);
            notebookHistory.fontSize = 16;
            notebookHistory.enableAutoSizing = false;
            notebookHistory.alignment = TextAlignmentOptions.TopLeft;
            notebookHistory.textWrappingMode = TextWrappingModes.Normal;
            notebookHistory.overflowMode = TextOverflowModes.Overflow;
            notebookHistory.richText = false;
            notebookHistory.margin = new Vector4(4, 4, 8, 4);
            notebookHistory.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            notebookScroll = viewport.gameObject.AddComponent<ScrollRect>();
            notebookScroll.viewport = viewport; notebookScroll.content = notebookHistory.rectTransform;
            notebookScroll.horizontal = false; notebookScroll.scrollSensitivity = 35;
            notebookScroll.movementType = ScrollRect.MovementType.Clamped;
            var track = LobbyPanel("Notebook scrollbar", notebookPanel, new Vector2(.92f, .34f), new Vector2(.95f, .85f), false);
            track.GetComponent<Image>().color = new Color32(96, 66, 42, 255);
            var handle = LobbyPanel("Handle", track, Vector2.zero, Vector2.one, false);
            handle.GetComponent<Image>().color = new Color32(233, 195, 125, 255);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            notebookScroll.verticalScrollbar = scrollbar;
            notebookScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            BuildNotebookInput(hud);
            MatchButton(hud, "GHI", notebookPanel,
                new Vector2(.67f, .04f), new Vector2(.94f, .13f), SaveNotebookEntry);
            MatchButton(hud, "ĐÓNG", notebookPanel,
                new Vector2(.06f, .04f), new Vector2(.33f, .13f), ToggleNotebook);
            notebookPanel.gameObject.SetActive(false);
            RefreshNotebookHistory();
        }

        private TMP_Text CreateNotebookText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = child.GetComponent<TMP_Text>();
            text.font = sceneHud != null && sceneHud.phaseText != null ? sceneHud.phaseText.font : null;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12; text.fontSizeMax = 21;
            ThuongMedievalTheme.Active?.StyleText(text);
            return text;
        }

        private void BuildNotebookInput(GameHUD hud)
        {
            var root = new GameObject("Note entry", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(notebookPanel, false);
            rect.anchorMin = new Vector2(.06f, .17f); rect.anchorMax = new Vector2(.94f, .30f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(.9f, .92f, .86f, 1f);
            notebookInput = root.GetComponent<TMP_InputField>();
            notebookInput.characterLimit = MaxNotebookCharacters;
            notebookInput.lineType = TMP_InputField.LineType.MultiLineNewline;
            notebookInput.contentType = TMP_InputField.ContentType.Standard;
            notebookInput.onSubmit.AddListener(_ => SaveNotebookEntry());

            var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.SetParent(root.transform, false);
            viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(8, 5); viewportRect.offsetMax = new Vector2(-8, -5);
            notebookInput.textViewport = viewportRect;

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(viewport.transform, false);
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            notebookInput.textComponent = textObject.GetComponent<TMP_Text>();
            notebookInput.textComponent.font = hud.phaseText != null ? hud.phaseText.font : null;
            notebookInput.textComponent.fontSize = 15;
            notebookInput.textComponent.color = new Color(.07f, .1f, .11f, 1f);
            notebookInput.textComponent.enableAutoSizing = true;
            notebookInput.textComponent.fontSizeMin = 12; notebookInput.textComponent.fontSizeMax = 17;
            notebookInput.textComponent.richText = false;

            var placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            var placeholderRect = placeholderObject.GetComponent<RectTransform>();
            placeholderRect.SetParent(viewport.transform, false);
            placeholderRect.anchorMin = Vector2.zero; placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = placeholderRect.offsetMax = Vector2.zero;
            var placeholder = placeholderObject.GetComponent<TMP_Text>();
            placeholder.font = hud.phaseText != null ? hud.phaseText.font : null;
            placeholder.fontSize = 14;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(.32f, .36f, .36f, 1f);
            placeholder.text = "Ghi chú chỉ lưu trên máy bạn…";
            notebookInput.placeholder = placeholder;
        }

        private void ToggleNotebook()
        {
            BindNotebookToRound(matchEpoch);
            notebookOpen = !notebookOpen;
            if (notebookPanel != null) notebookPanel.gameObject.SetActive(notebookOpen);
            if (notebookOpen) RefreshNotebookHistory();
            else if (notebookInput != null && notebookInput.isFocused) notebookInput.DeactivateInputField();
        }

        private void SaveNotebookEntry()
        {
            if (!NotebookIsOpen || notebookInput == null) return;
            BindNotebookToRound(matchEpoch);
            string value = (notebookInput.text ?? string.Empty).Trim();
            if (value.Length == 0) return;
            if (value.Length > MaxNotebookCharacters) value = value.Substring(0, MaxNotebookCharacters);
            notebookEntries.Add($"Ngày {visibleDay} · {value}");
            if (notebookEntries.Count > MaxNotebookEntries) notebookEntries.RemoveAt(0);
            notebookInput.text = string.Empty;
            notebookInput.ActivateInputField();
            RefreshNotebookHistory();
        }

        private void RefreshNotebookHistory()
        {
            if (notebookHistory == null) return;
            if (notebookEntries.Count == 0)
            {
                notebookHistory.text = "Ghi lại suy luận hoặc thông tin bạn tự quan sát được. Ghi chú này không gửi cho host hay người chơi khác.";
            }
            else
            {
                var text = new StringBuilder();
                foreach (string entry in notebookEntries) text.AppendLine("• " + entry);
                notebookHistory.text = text.ToString();
            }
            if (NotebookIsOpen && notebookScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(notebookHistory.rectTransform);
                notebookScroll.StopMovement();
                notebookScroll.verticalNormalizedPosition = 0;
            }
        }

        private void RefreshNotebookHud()
        {
            if (notebookPanel == null) return;
            if (!matchStarted)
            {
                notebookOpen = false;
                notebookPanel.gameObject.SetActive(false);
                return;
            }
            if (notebookPanel.gameObject.activeSelf != notebookOpen)
                notebookPanel.gameObject.SetActive(notebookOpen);
        }

        private void ResetNotebook()
        {
            notebookEpoch = 0;
            notebookRoomToken = System.Guid.Empty;
            notebookEntries.Clear();
            notebookOpen = false;
            if (notebookPanel != null) notebookPanel.gameObject.SetActive(false);
            if (notebookInput != null) notebookInput.text = string.Empty;
            RefreshNotebookHistory();
        }
    }
}
