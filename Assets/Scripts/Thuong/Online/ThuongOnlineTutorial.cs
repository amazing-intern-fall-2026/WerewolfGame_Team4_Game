using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private sealed class TutorialView
        {
            public GameObject Root;
            public TMP_Text Title, Body, Page;
            public Button Previous, Next;
        }

        private TutorialView lobbyTutorial, matchTutorial;
        private int tutorialPage;

        private bool TutorialIsOpen => PracticeIsOpen || lobbyTutorial?.Root.activeSelf == true || matchTutorial?.Root.activeSelf == true;

        private void OpenLobbyTutorial() => OpenTutorial(true);
        private void OpenMatchTutorial() => OpenTutorial(false);

        private void OpenTutorial(bool inLobby)
        {
            TutorialView view = inLobby ? lobbyTutorial : matchTutorial;
            if (view == null)
            {
                Transform parent = inLobby ? lobbyCanvas?.transform : sceneHud?.transform;
                if (parent == null) return;
                view = BuildTutorialView(parent, inLobby);
                if (inLobby) lobbyTutorial = view; else matchTutorial = view;
            }
            if (inLobby && matchTutorial != null) matchTutorial.Root.SetActive(false);
            if (!inLobby && lobbyTutorial != null) lobbyTutorial.Root.SetActive(false);
            tutorialPage = 0;
            view.Root.SetActive(true);
            view.Root.transform.SetAsLastSibling();
            RefreshTutorialPage(view);
        }

        private TutorialView BuildTutorialView(Transform parent, bool inLobby)
        {
            var shade = new GameObject(inLobby ? "Lobby tutorial" : "Match tutorial",
                typeof(RectTransform), typeof(Image));
            var shadeRect = shade.GetComponent<RectTransform>();
            shadeRect.SetParent(parent, false);
            shadeRect.anchorMin = Vector2.zero; shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(.015f, .025f, .035f, .88f);

            var panel = new GameObject("Tutorial card", typeof(RectTransform), typeof(Image));
            var panelRect = panel.GetComponent<RectTransform>(); panelRect.SetParent(shade.transform, false);
            panelRect.anchorMin = new Vector2(.14f, .12f); panelRect.anchorMax = new Vector2(.86f, .88f);
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            ThuongMedievalTheme.Active?.StylePanel(panel.GetComponent<Image>(), "Beginner tutorial");

            var view = new TutorialView { Root = shade };
            view.Title = TutorialText("Title", panelRect, new Vector2(.05f, .85f), new Vector2(.58f, .97f), 28, TextAlignmentOptions.Center);
            LobbyButton("Practice village", panelRect, "THỰC HÀNH LÀNG", new Vector2(.60f, .87f), new Vector2(.95f, .97f), OpenVillagePractice);
            view.Body = TutorialText("Lesson", panelRect, new Vector2(.08f, .22f), new Vector2(.92f, .82f), 22, TextAlignmentOptions.TopLeft);
            view.Body.textWrappingMode = TextWrappingModes.Normal;
            view.Body.richText = false;
            view.Body.enableAutoSizing = true; view.Body.fontSizeMin = 16; view.Body.fontSizeMax = 23;
            view.Page = TutorialText("Page count", panelRect, new Vector2(.38f, .12f), new Vector2(.62f, .20f), 16, TextAlignmentOptions.Center);

            UnityEngine.Events.UnityAction previous = () => ChangeTutorialPage(-1);
            UnityEngine.Events.UnityAction next = () => ChangeTutorialPage(1);
            if (inLobby)
            {
                view.Previous = LobbyButton("Tutorial previous", panelRect, "‹ TRƯỚC", new Vector2(.05f, .055f), new Vector2(.24f, .15f), previous);
                view.Next = LobbyButton("Tutorial next", panelRect, "TIẾP ›", new Vector2(.76f, .055f), new Vector2(.95f, .15f), next);
                LobbyButton("Tutorial skip", panelRect, "BỎ QUA", new Vector2(.39f, .055f), new Vector2(.61f, .15f), CloseTutorial);
            }
            else
            {
                view.Previous = MatchButton(sceneHud, "‹ TRƯỚC", panelRect, new Vector2(.05f, .055f), new Vector2(.24f, .15f), previous);
                view.Next = MatchButton(sceneHud, "TIẾP ›", panelRect, new Vector2(.76f, .055f), new Vector2(.95f, .15f), next);
                MatchButton(sceneHud, "BỎ QUA", panelRect, new Vector2(.39f, .055f), new Vector2(.61f, .15f), CloseTutorial);
            }
            shade.SetActive(false);
            return view;
        }

        private TMP_Text TutorialText(string name, Transform parent, Vector2 min, Vector2 max,
            int size, TextAlignmentOptions alignment)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            var rect = child.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = child.GetComponent<TMP_Text>();
            text.font = sceneHud != null && sceneHud.phaseText != null ? sceneHud.phaseText.font : lobbyHintLabel?.font;
            text.fontSize = size; text.alignment = alignment; text.raycastTarget = false;
            text.enableAutoSizing = true; text.fontSizeMin = 14; text.fontSizeMax = size;
            ThuongMedievalTheme.Active?.StyleText(text);
            return text;
        }

        private string[] TutorialLessons()
        {
            string modeLesson = ActiveMatchMode == ThuongMatchMode.RealtimeProximity
                ? "MODE TƯƠNG TÁC GẦN\nBan đêm, Khuyển tinh, Bảo vệ và Tiên tri dùng E khi mục tiêu ở gần và không bị tường chắn. Kỹ năng có hiệu lực ngay sau khi host xác nhận. Ban ngày, E mở nhiệm vụ gần; nếu có xác trong tầm, E sẽ ưu tiên báo xác và mở họp."
                : "MODE CỔ ĐIỂN\nBan đêm, mở thẻ ROLE / KỸ NĂNG để chọn hành động. Kỹ năng được host xác nhận và xử lý theo luật cuối đêm. Ban ngày, dùng E tại trạm nhiệm vụ.";
            return new[]
            {
                "ĐIỀU KHIỂN\nWASD hoặc phím mũi tên để đi. Nhấn nhanh E để dùng tương tác hiện trên HUD. Nếu có nhiều lựa chọn, giữ E rồi dùng phím hướng để chọn; thả ra chỉ lưu lựa chọn, nhấn nhanh E lần nữa để thực hiện. Mở Bản đồ để xem lối đi và nhiệm vụ.",
                "NGÀY, ĐÊM VÀ NHIỆM VỤ\nBan ngày, đi làm nhiệm vụ và quan sát người chơi. Đến đêm, các vai trò có kỹ năng hành động theo mode của phòng. Đồng hồ trên HUD cho biết thời gian còn lại của phase.",
                "THẢO LUẬN VÀ BỎ PHIẾU\nKhi họp bắt đầu, trao đổi điều bạn tự quan sát được rồi bỏ phiếu. Bạn có thể chọn một người hoặc KHÔNG LOẠI AI. Nếu phiếu cao nhất hòa, không ai bị loại.",
                modeLesson,
                "BỊ LOẠI VÀ QUYỀN RIÊNG TƯ\nSau khi các quyền đặc biệt cuối cùng được xử lý, bạn có thể theo dõi người còn sống hoặc xem toàn bản đồ. Ban đêm, góc nhìn bị che để không tiết lộ hành động bí mật. Sổ tay chỉ lưu trên thiết bị của bạn."
            };
        }

        private void ChangeTutorialPage(int direction)
        {
            int count = TutorialLessons().Length;
            tutorialPage = Mathf.Clamp(tutorialPage + direction, 0, count - 1);
            RefreshTutorialPage(lobbyTutorial?.Root.activeSelf == true ? lobbyTutorial : matchTutorial);
        }

        private void RefreshTutorialPage(TutorialView view)
        {
            if (view == null) return;
            string[] lessons = TutorialLessons();
            tutorialPage = Mathf.Clamp(tutorialPage, 0, lessons.Length - 1);
            view.Title.text = "HƯỚNG DẪN NGƯỜI MỚI";
            view.Body.text = lessons[tutorialPage];
            view.Page.text = $"{tutorialPage + 1} / {lessons.Length}";
            view.Previous.interactable = tutorialPage > 0;
            view.Next.interactable = tutorialPage < lessons.Length - 1;
        }

        private void CloseTutorial()
        {
            CloseVillagePractice();
            lobbyTutorial?.Root.SetActive(false);
            matchTutorial?.Root.SetActive(false);
        }

        private void UpdateTutorialInput()
        {
            if (PracticeIsOpen) { UpdateVillagePractice(); return; }
            if (!TutorialIsOpen) return;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                Gamepad.current?.buttonEast.wasPressedThisFrame == true)
            { CloseTutorial(); return; }
            if (Keyboard.current?.leftArrowKey.wasPressedThisFrame == true ||
                Gamepad.current?.dpad.left.wasPressedThisFrame == true) ChangeTutorialPage(-1);
            if (Keyboard.current?.rightArrowKey.wasPressedThisFrame == true ||
                Keyboard.current?.enterKey.wasPressedThisFrame == true ||
                Gamepad.current?.dpad.right.wasPressedThisFrame == true ||
                Gamepad.current?.buttonSouth.wasPressedThisFrame == true) ChangeTutorialPage(1);
        }

        private void RefreshTutorialVisibility()
        {
            if (!connected || ShowRoleCard)
            {
                lobbyTutorial?.Root.SetActive(false);
                if (!matchStarted) matchTutorial?.Root.SetActive(false);
            }
            if (matchStarted) lobbyTutorial?.Root.SetActive(false);
            if (!matchStarted) matchTutorial?.Root.SetActive(false);
        }
    }
}
