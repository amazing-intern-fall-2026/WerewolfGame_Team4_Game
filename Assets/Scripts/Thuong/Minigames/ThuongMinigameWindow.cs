using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Thuong.Minigames
{
    public sealed class ThuongMinigameWindow : MonoBehaviour, IDisposable
    {
        private Scene localScene;
        private PhysicsScene2D physics;
        private Camera renderCamera;
        private RenderTexture texture;
        private GameObject localRoot;
        private ThuongTaskMinigameView view;
        private ThuongMinigameState state;
        private TMP_Text progress, instructions, result;
        private Button retry, flag;
        private RawImage image;
        private Action cancel;
        private Func<double> now;
        private bool disposed, flagMode;
        private double closeAt = double.PositiveInfinity;

        public void Open(ThuongMinigameCatalog catalog, ThuongMinigameEntry entry, ThuongMinigameState initial,
            TMP_FontAsset font, Action<ThuongMinigameInput> send, Action cancelAction, Action retryAction, Func<double> serverTime)
        {
            cancel = cancelAction; now = serverTime;
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1500;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1100, 760); scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            // The full-screen graphic consumes pointer events before the underlying gameplay HUD.
            var shade = Panel("Modal blocker", transform, Vector2.zero, Vector2.one, new Color(0, 0, 0, .82f));
            var panel = Panel("Task panel", shade, new Vector2(.035f, .035f), new Vector2(.965f, .965f), new Color32(35, 31, 26, 255));
            Label("Title", entry.task.taskName.ToUpperInvariant(), panel, new Vector2(.04f, .9f), new Vector2(.96f, .98f), font, 30);
            instructions = Label("Instructions", entry.instructions, panel, new Vector2(.04f, .8f), new Vector2(.96f, .9f), font, 19);
            progress = Label("Progress", "", panel, new Vector2(.04f, .735f), new Vector2(.96f, .8f), font, 20);
            var viewport = Panel("Viewport", panel, new Vector2(.04f, .18f), new Vector2(.96f, .735f), Color.black);
            image = new GameObject("Minigame image", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter), typeof(ThuongMinigamePointer)).GetComponent<RawImage>();
            image.transform.SetParent(viewport, false);
            var fitter = image.GetComponent<AspectRatioFitter>(); fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 16f / 9;
            result = Label("Result", "", panel, new Vector2(.04f, .105f), new Vector2(.96f, .175f), font, 21);
            Button("Đóng / Hủy", panel, new Vector2(.04f, .025f), new Vector2(.29f, .1f), font, () => cancel?.Invoke());
            retry = Button("Thử lại", panel, new Vector2(.71f, .025f), new Vector2(.96f, .1f), font, () => retryAction());
            retry.gameObject.SetActive(false);
            flag = Button("Cắm cờ: TẮT", panel, new Vector2(.35f, .025f), new Vector2(.65f, .1f), font, () =>
            {
                flagMode = !flagMode;
                flag.GetComponentInChildren<TMP_Text>().text = flagMode ? "Cắm cờ: BẬT" : "Cắm cờ: TẮT";
            });
            flag.gameObject.SetActive(entry.kind == ThuongMinigameKind.Trapsweeper);

            localScene = SceneManager.CreateScene("ThuongTask-" + initial.attemptId, new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physics = localScene.GetPhysicsScene2D();
            localRoot = new GameObject("Minigame local root"); localRoot.SetActive(false);
            SceneManager.MoveGameObjectToScene(localRoot, localScene);
            // Spatial isolation also keeps other scene cameras from rendering these local objects.
            localRoot.transform.position = new Vector3(10000, 10000, 0);
            var cameraRoot = new GameObject("Task camera", typeof(Camera));
            cameraRoot.transform.SetParent(localRoot.transform, false); cameraRoot.transform.localPosition = new Vector3(0, 0, -10);
            renderCamera = cameraRoot.GetComponent<Camera>(); renderCamera.orthographic = true; renderCamera.orthographicSize = 4.5f;
            renderCamera.clearFlags = CameraClearFlags.SolidColor; renderCamera.backgroundColor = new Color32(29, 40, 40, 255);
            renderCamera.nearClipPlane = .1f; renderCamera.farClipPlane = 30; renderCamera.allowHDR = false;
            texture = new RenderTexture(960, 540, 24) { name = "Thuong task view", antiAliasing = 1 };
            texture.Create(); renderCamera.targetTexture = texture; image.texture = texture;
            var instance = Instantiate(entry.viewPrefab, localRoot.transform, false);
            view = instance.GetComponent<ThuongTaskMinigameView>();
            view.Initialize(new ThuongMinigameContext
            { camera = renderCamera, root = localRoot.transform, entry = entry, catalog = catalog, font = font, send = send, serverTime = serverTime });
            image.GetComponent<ThuongMinigamePointer>().receive = (position, pressed, secondary) =>
            {
                if (state?.result == ThuongMinigameResult.Playing) view.Pointer(position, pressed, secondary || flagMode);
            };
            if (entry.kind == ThuongMinigameKind.Potion)
            {
                for (int i = 0; i < catalog.ingredients.Length; i++)
                {
                    int index = i; float left = .03f + .323f * i;
                    var ingredient = catalog.ingredients[i]; int score = catalog.IngredientScores()[i];
                    var choice = Button(ingredient.ingredientName + " (" + score.ToString("+0;-0;0") + ")", viewport,
                        new Vector2(left, .04f), new Vector2(left + .3f, .22f), font,
                        () => { if (state?.result == ThuongMinigameResult.Playing) send(new ThuongMinigameInput(ThuongMinigameAction.Ingredient, index)); });
                    if (ingredient.icon != null)
                    {
                        var icon = new GameObject("Ingredient icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                        icon.transform.SetParent(choice.transform, false); icon.sprite = ingredient.icon; icon.preserveAspect = true; icon.raycastTarget = false;
                        Place(icon.rectTransform, new Vector2(.02f, .12f), new Vector2(.18f, .88f));
                        choice.GetComponentInChildren<TMP_Text>().margin = new Vector4(40, 0, 0, 0);
                    }
                }
            }
            view.Begin(initial); localRoot.SetActive(true); Apply(initial);
        }

        public void Apply(ThuongMinigameState next)
        {
            if (disposed) return;
            state = next; view.Apply(next);
            result.text = next.message;
            bool playing = next.result == ThuongMinigameResult.Playing;
            image.raycastTarget = playing;
            retry.gameObject.SetActive(next.result == ThuongMinigameResult.Failed || next.result == ThuongMinigameResult.Expired);
            flag.interactable = playing;
            if (next.kind == ThuongMinigameKind.Potion)
                instructions.text = "Pha " + RecipeName(next.recipe) + ". Chọn đúng 3 nguyên liệu; được chọn lặp. " + RecipeRule(next.recipe);
            if (next.result == ThuongMinigameResult.Succeeded) closeAt = now() + .9;
            if (next.result == ThuongMinigameResult.Cancelled) closeAt = now();
            RefreshProgress();
        }

        private void Update()
        {
            if (disposed || state == null) return;
            RefreshProgress();
            if (now() >= closeAt || Keyboard.current?.escapeKey.wasPressedThisFrame == true) cancel?.Invoke();
        }
        private void FixedUpdate()
        {
            if (!disposed && physics.IsValid()) physics.Simulate(Time.fixedDeltaTime);
        }
        private void RefreshProgress()
        {
            progress.text = $"{state.score}/{state.target}  •  Trượt {state.mistakes}/{state.allowedMistakes}  •  Còn {Math.Max(0, state.expiresAt - now()):0}s";
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (view != null) { view.Cancel(); view.Dispose(); }
            if (renderCamera != null) { renderCamera.enabled = false; renderCamera.targetTexture = null; }
            if (image != null) { image.texture = null; image.GetComponent<ThuongMinigamePointer>().receive = null; }
            if (texture != null) { texture.Release(); Destroy(texture); texture = null; }
            if (localRoot != null) { localRoot.SetActive(false); Destroy(localRoot); }
            if (localScene.IsValid() && localScene.isLoaded) SceneManager.UnloadSceneAsync(localScene);
            cancel = null; now = null;
            gameObject.SetActive(false);
        }
        private void OnDestroy() => Dispose();

        public static string RecipeName(ThuongPotion recipe) => recipe switch
        {
            ThuongPotion.Healing => "thuốc hồi phục", ThuongPotion.StrongHealing => "thuốc hồi phục mạnh",
            ThuongPotion.Poison => "thuốc độc", ThuongPotion.StrongPoison => "thuốc độc mạnh", _ => "hỗn hợp trung tính"
        };
        private static string RecipeRule(ThuongPotion recipe) => recipe switch
        {
            ThuongPotion.Healing => "Tổng điểm từ 1 đến 4.", ThuongPotion.StrongHealing => "Tổng điểm từ 5 trở lên.",
            ThuongPotion.Poison => "Tổng điểm từ -4 đến -1.", ThuongPotion.StrongPoison => "Tổng điểm từ -5 trở xuống.", _ => "Tổng điểm bằng 0."
        };

        private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = root.GetComponent<RectTransform>(); rect.SetParent(parent, false); Place(rect, min, max);
            root.GetComponent<Image>().color = color; return rect;
        }
        private static TMP_Text Label(string name, string text, Transform parent, Vector2 min, Vector2 max, TMP_FontAsset font, int size)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            label.transform.SetParent(parent, false); Place(label.rectTransform, min, max);
            label.font = font; label.fontSize = size; label.text = text; label.alignment = TextAlignmentOptions.Center;
            label.color = new Color32(247, 232, 193, 255); label.raycastTarget = false;
            label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = size;
            return label;
        }
        private static Button Button(string text, Transform parent, Vector2 min, Vector2 max, TMP_FontAsset font, Action clicked)
        {
            var rect = Panel(text, parent, min, max, new Color32(91, 66, 43, 255));
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => clicked());
            Label("Text", text, rect, new Vector2(.03f, .05f), new Vector2(.97f, .95f), font, 20);
            return button;
        }
        private static void Place(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }

    public sealed class ThuongMinigamePointer : MonoBehaviour, IPointerDownHandler, IPointerMoveHandler, IDragHandler
    {
        public Action<Vector2, bool, bool> receive;
        private void Forward(PointerEventData data, bool pressed)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out Vector2 p) || !rect.rect.Contains(p)) return;
            var viewport = new Vector2((p.x - rect.rect.xMin) / rect.rect.width, (p.y - rect.rect.yMin) / rect.rect.height);
            receive?.Invoke(viewport, pressed, data.button == PointerEventData.InputButton.Right);
        }
        public void OnPointerDown(PointerEventData data) => Forward(data, true);
        public void OnPointerMove(PointerEventData data) => Forward(data, false);
        public void OnDrag(PointerEventData data) => Forward(data, false);
    }
}
