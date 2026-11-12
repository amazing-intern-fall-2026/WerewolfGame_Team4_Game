using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private Canvas practiceCanvas;
        private ThuongVillagePractice villagePractice;
        private RectTransform practiceAvatar;
        private TMP_Text practiceTitle, practiceLesson, practiceFeedback;
        private Button practiceNext, practiceInteract;
        private bool PracticeIsOpen => practiceCanvas != null && practiceCanvas.gameObject.activeSelf;

        private void OpenVillagePractice()
        {
            CloseTutorial(); CloseVillagePanel(); SuspendNotebook();
            NetworkChatUI.Instance?.SetChatOpen(false);
            if (connected && network != null) SendMovementInput(Vector2.zero);
            ResetInteractionInput();
            if (practiceCanvas == null) BuildVillagePractice();
            practiceCanvas.gameObject.SetActive(true);
            villagePractice = new ThuongVillagePractice(); villagePractice.Begin(0, Time.unscaledTimeAsDouble);
            RefreshVillagePractice();
        }

        private void CloseVillagePractice()
        {
            if (practiceCanvas != null) practiceCanvas.gameObject.SetActive(false);
            villagePractice = null;
        }

        private void BuildVillagePractice()
        {
            var root = new GameObject("Village practice", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false); practiceCanvas = root.GetComponent<Canvas>();
            practiceCanvas.renderMode = RenderMode.ScreenSpaceOverlay; practiceCanvas.sortingOrder = 2000;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var shade = LobbyPanel("Practice shade", root.transform, Vector2.zero, Vector2.one, false);
            shade.GetComponent<Image>().color = new Color(0, 0, 0, .94f);
            var card = LobbyPanel("Practice card", shade, new Vector2(.08f, .08f), new Vector2(.92f, .92f));
            practiceTitle = LobbyText("Practice title", card, "", new Vector2(.04f, .89f), new Vector2(.96f, .98f), 28, TextAlignmentOptions.Center);
            var map = LobbyPanel("Practice map", card, new Vector2(.04f, .22f), new Vector2(.56f, .86f), false);
            map.GetComponent<Image>().color = new Color(.05f, .18f, .16f, 1);
            PracticeMarker(map, "CHUÔNG", ThuongVillagePractice.Bell, new Color(1, .76f, .22f));
            PracticeMarker(map, "A", ThuongVillagePractice.PadA, Color.cyan);
            PracticeMarker(map, "B · BẠN MÔ PHỎNG", ThuongVillagePractice.PadB, Color.cyan);
            PracticeMarker(map, "KHU MẤT ĐIỆN / SỬA", ThuongVillagePractice.Repair, new Color(.9f, .25f, .18f));
            practiceAvatar = PracticeMarker(map, "BẠN", Vector2.zero, Color.white);
            practiceLesson = LobbyText("Practice lesson", card, "", new Vector2(.60f, .60f), new Vector2(.96f, .86f), 22);
            practiceLesson.textWrappingMode = TextWrappingModes.Normal; practiceLesson.enableAutoSizing = true; practiceLesson.fontSizeMin = 16;
            practiceFeedback = LobbyText("Practice feedback", card, "", new Vector2(.60f, .37f), new Vector2(.96f, .59f), 19);
            practiceFeedback.richText = false; practiceFeedback.textWrappingMode = TextWrappingModes.Normal;
            practiceFeedback.enableAutoSizing = true; practiceFeedback.fontSizeMin = 14;
            practiceInteract = LobbyButton("Practice interact", card, "E · TƯƠNG TÁC", new Vector2(.60f, .27f), new Vector2(.96f, .36f), PracticeInteract);
            practiceNext = LobbyButton("Practice next", card, "BÀI TIẾP THEO", new Vector2(.60f, .16f), new Vector2(.96f, .25f), NextPracticeStage);
            Vector2[] directions = { Vector2.left, Vector2.up, Vector2.down, Vector2.right };
            string[] labels = { "←", "↑", "↓", "→" };
            for (int i = 0; i < 4; i++)
            {
                Vector2 direction = directions[i]; float left = .04f + .13f * i;
                LobbyButton("Practice move " + i, card, labels[i], new Vector2(left, .105f), new Vector2(left + .12f, .20f), () => villagePractice?.Move(direction));
            }
            var hint = LobbyText("Practice isolation", card, "Bài tập dùng luật mặc định, không tính thưởng vào ván. Nếu mở trong trận, ván hiện tại vẫn tiếp tục.", new Vector2(.04f, .035f), new Vector2(.57f, .10f), 15);
            hint.textWrappingMode = TextWrappingModes.Normal;
            LobbyButton("Practice retry", card, "THỬ LẠI", new Vector2(.60f, .035f), new Vector2(.77f, .13f),
                () => villagePractice?.Begin(villagePractice.Stage, Time.unscaledTimeAsDouble));
            LobbyButton("Practice close", card, "ĐÓNG", new Vector2(.79f, .035f), new Vector2(.96f, .13f), CloseVillagePractice);
        }

        private RectTransform PracticeMarker(RectTransform map, string label, Vector2 position, Color color)
        {
            Vector2 center = (position + Vector2.one * 5) / 10;
            var marker = LobbyPanel(label, map, center - Vector2.one * .035f, center + Vector2.one * .035f, false);
            marker.GetComponent<Image>().color = color;
            var text = LobbyText(label + " label", marker, label, new Vector2(-1.5f, 1.1f), new Vector2(2.5f, 2.3f), 16, TextAlignmentOptions.Center);
            text.richText = false; text.raycastTarget = false; text.enableAutoSizing = true; text.fontSizeMin = 10;
            return marker;
        }

        private void PracticeInteract() => villagePractice?.Interact(Time.unscaledTimeAsDouble);
        private void NextPracticeStage()
        {
            if (villagePractice == null || !villagePractice.Succeeded) return;
            if (villagePractice.Stage == 3) { CloseVillagePractice(); return; }
            villagePractice.Begin(villagePractice.Stage + 1, Time.unscaledTimeAsDouble);
        }

        private void UpdateVillagePractice()
        {
            if (!PracticeIsOpen || villagePractice == null) return;
            if (!connected || ShowRoleCard || matchResult != null || matchStarted && !LocalPlayerAlive) { CloseVillagePractice(); return; }
            var keyboard = Keyboard.current;
            if (keyboard?.escapeKey.wasPressedThisFrame == true) { CloseVillagePractice(); return; }
            Vector2 movement = Vector2.zero;
            if (keyboard != null)
            {
                movement.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                movement.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
                villagePractice.Move(Vector2.ClampMagnitude(movement, 1) * (3 * Time.unscaledDeltaTime));
                if (keyboard.eKey.wasPressedThisFrame) PracticeInteract();
            }
            villagePractice.Tick(Time.unscaledTimeAsDouble); RefreshVillagePractice();
        }

        private void RefreshVillagePractice()
        {
            int stage = villagePractice.Stage;
            string[] lessons =
            {
                "CHUÔNG LÀNG\nĐi tới ô vàng rồi nhấn E hoặc TƯƠNG TÁC để gọi họp.",
                "NHIỆM VỤ ĐÔI\nĐi tới bảng A và nhấn E. Bạn mô phỏng dùng bảng B; hãy đứng yên ba giây.",
                "SỬA PHÁ HOẠI\nĐi tới điểm đỏ rồi nhấn E. Đứng yên ba giây để khắc phục mất điện.",
                "VẬT PHẨM\nTới gần khu mất điện màu đỏ, nhấn E hoặc DÙNG ĐÈN để dùng đèn lồng trong túi."
            };
            practiceTitle.text = $"THỰC HÀNH LÀNG · BÀI {stage + 1}/4";
            practiceLesson.text = lessons[stage];
            practiceFeedback.text = villagePractice.Feedback + (villagePractice.Progress(Time.unscaledTimeAsDouble) > 0 ? $"\nTiến độ: {villagePractice.Progress(Time.unscaledTimeAsDouble):P0}" : "");
            practiceInteract.interactable = !villagePractice.Succeeded;
            practiceInteract.GetComponentInChildren<TMP_Text>().text = stage == 3 ? "E · DÙNG ĐÈN" : "E · TƯƠNG TÁC";
            practiceNext.interactable = villagePractice.Succeeded;
            practiceNext.GetComponentInChildren<TMP_Text>().text = stage == 3 ? "HOÀN THÀNH" : "BÀI TIẾP THEO";
            Vector2 center = (villagePractice.Position + Vector2.one * 5) / 10;
            practiceAvatar.anchorMin = center - Vector2.one * .035f; practiceAvatar.anchorMax = center + Vector2.one * .035f;
        }
    }
}
