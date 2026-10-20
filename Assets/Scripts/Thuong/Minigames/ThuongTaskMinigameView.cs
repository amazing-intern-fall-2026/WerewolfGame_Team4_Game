using System;
using TMPro;
using UnityEngine;

namespace Thuong.Minigames
{
    // Task adapters share their presentation lifecycle. Rules and scoring live exclusively in Authority.
    // Demo controllers (and their global singletons/timeScale/reload behavior) are never instantiated.
    public sealed class ThuongTaskMinigameView : MonoBehaviour, IThuongTaskMinigame
    {
        public Material spriteMaterial;
        private ThuongMinigameContext context;
        private ThuongMinigameCatalog catalog;
        private ThuongMinigameState state;
        private TMP_FontAsset font;
        private SpriteRenderer subject, tool, marker;
        private SpriteRenderer[] cells;
        private TMP_Text[] clues;
        private Sprite fallback;
        private Texture2D fallbackTexture;
        private Vector2 pointer = new(.5f, .25f);
        private bool hasPointer, running, disposed;
        private double nextControl;

        public void Initialize(ThuongMinigameContext value)
        {
            context = value; catalog = value.catalog; font = value.font;
            fallbackTexture = new Texture2D(1, 1); fallbackTexture.SetPixel(0, 0, Color.white); fallbackTexture.Apply();
            fallback = Sprite.Create(fallbackTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            if (context.entry.background != null) AddSprite("Background", context.entry.background, Vector2.zero, new Vector2(16, 9), -10, false);
            subject = AddSprite("Subject", context.entry.subject, Vector2.zero, new Vector2(1.6f, 1.6f), 2);
            tool = AddSprite("Tool", context.entry.tool, new Vector2(0, -2.5f), new Vector2(2, 1), 3);
            tool.enabled = context.entry.tool != null;
            marker = AddSprite("Target", null, Vector2.zero, new Vector2(.12f, 6), 1); marker.color = new Color(1, .85f, .2f, .6f);
            if (context.entry.kind == ThuongMinigameKind.Defender)
                AddSprite("Protected villager", context.entry.obstacle, Vector2.zero, new Vector2(1, 1.4f), 1);
            if (context.entry.kind == ThuongMinigameKind.Trapsweeper)
            {
                subject.enabled = tool.enabled = marker.enabled = false;
                int width = context.entry.rules.boardWidth;
                cells = new SpriteRenderer[width * width]; clues = new TMP_Text[cells.Length];
                for (int i = 0; i < cells.Length; i++)
                {
                    Vector2 p = CellPosition(i, width);
                    cells[i] = AddSprite("Cell " + i, catalog.trapCover, p, Vector2.one, 2);
                    var label = new GameObject("Clue " + i, typeof(TextMeshPro)).GetComponent<TextMeshPro>();
                    label.transform.SetParent(transform, false); label.transform.localPosition = new Vector3(p.x, p.y, -1);
                    label.font = font; label.fontSize = 5; label.alignment = TextAlignmentOptions.Center;
                    label.rectTransform.sizeDelta = Vector2.one; label.GetComponent<MeshRenderer>().sortingOrder = 5;
                    clues[i] = label;
                }
            }
        }
        public void Begin(ThuongMinigameState initial) { state = initial; running = true; Apply(initial); }
        public void Apply(ThuongMinigameState next)
        {
            state = next; running = next.result == ThuongMinigameResult.Playing;
            if (cells == null) return;
            for (int i = 0; i < cells.Length && i < next.cells.Length; i++)
            {
                int cell = next.cells[i];
                cells[i].sprite = cell == -2 ? catalog.trapCover : cell == -3 ? catalog.trapFlag : catalog.trapCleared;
                if (cells[i].sprite != null)
                {
                    Vector3 size = cells[i].sprite.bounds.size;
                    cells[i].transform.localScale = new Vector3(1 / Mathf.Max(.01f, size.x), 1 / Mathf.Max(.01f, size.y), 1);
                }
                cells[i].color = cell == -9 ? Color.red : Color.white;
                clues[i].text = cell > 0 ? cell.ToString() : cell == -9 ? "X" : "";
                clues[i].color = cell == 1 ? new Color32(66, 106, 234, 255) : cell == 2 ? new Color32(30, 142, 63, 255) : new Color32(190, 44, 34, 255);
            }
        }
        public void Pointer(Vector2 viewport, bool pressed, bool secondary)
        {
            if (!running || disposed) return;
            pointer = viewport; hasPointer = true;
            if (!pressed) return;
            switch (state.kind)
            {
                case ThuongMinigameKind.ChopWood:
                    context.send(new ThuongMinigameInput(ThuongMinigameAction.Chop, state.round)); break;
                case ThuongMinigameKind.Harvest:
                    context.send(new ThuongMinigameInput(ThuongMinigameAction.Harvest, state.round)); break;
                case ThuongMinigameKind.Trapsweeper:
                    Vector3 p = context.camera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, 10)) - transform.position;
                    int w = state.boardWidth;
                    int x = Mathf.FloorToInt(p.x / 1.1f + w / 2f), y = Mathf.FloorToInt(p.y / 1.1f + w / 2f);
                    if (x >= 0 && y >= 0 && x < w && y < w)
                        context.send(new ThuongMinigameInput(secondary ? ThuongMinigameAction.Flag : ThuongMinigameAction.Reveal, y * w + x));
                    break;
            }
        }
        private void Update()
        {
            if (state == null || disposed) return;
            double now = context.serverTime();
            float dt = (float)(now - state.roundAt);
            switch (state.kind)
            {
                case ThuongMinigameKind.ChopWood:
                case ThuongMinigameKind.Harvest:
                    subject.transform.localPosition = new Vector3(Mathf.Clamp(dt * 4.2f, -8, 8), 0, 0);
                    subject.sprite = state.obstacle ? context.entry.obstacle ?? fallback : context.entry.subject ?? fallback;
                    subject.color = state.obstacle ? new Color(1, .35f, .3f) : Color.white;
                    marker.color = Mathf.Abs(dt) <= state.timingWindow ? new Color(.25f, 1, .35f, .7f) : new Color(1, .85f, .2f, .5f);
                    tool.transform.localPosition = new Vector3(0, -2.4f, 0);
                    break;
                case ThuongMinigameKind.Eggs:
                    marker.enabled = false;
                    subject.transform.localPosition = new Vector3((state.lane - .5f) * 12, Mathf.Clamp(-2.5f - dt * 5, -3.5f, 4), 0);
                    tool.transform.localPosition = new Vector3((state.control - .5f) * 12, -2.5f, 0);
                    if (running && hasPointer && now >= nextControl)
                    { nextControl = now + .12; context.send(new ThuongMinigameInput(ThuongMinigameAction.MoveBasket, 0, Mathf.Clamp01((pointer.x - .125f) / .75f))); }
                    break;
                case ThuongMinigameKind.Defender:
                    marker.enabled = false;
                    float angle = state.lane * Mathf.Deg2Rad;
                    subject.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * Mathf.Clamp(1.4f - dt * 3, .5f, 5);
                    subject.transform.localRotation = Quaternion.Euler(0, 0, state.lane + 90);
                    float shield = state.control * Mathf.Deg2Rad;
                    tool.transform.localPosition = new Vector3(Mathf.Cos(shield), Mathf.Sin(shield), 0) * 1.4f;
                    tool.transform.localRotation = Quaternion.Euler(0, 0, state.control - 90);
                    if (running && hasPointer && now >= nextControl)
                    {
                        nextControl = now + .12;
                        float degrees = (Mathf.Atan2((pointer.y - .5f) * 9, (pointer.x - .5f) * 16) * Mathf.Rad2Deg + 360) % 360;
                        context.send(new ThuongMinigameInput(ThuongMinigameAction.AimShield, 0, degrees));
                    }
                    break;
                case ThuongMinigameKind.Potion:
                    marker.enabled = false; tool.enabled = false;
                    subject.color = state.score > 0 ? Color.Lerp(Color.white, state.recipe == ThuongPotion.Poison || state.recipe == ThuongPotion.StrongPoison ? Color.magenta : Color.green, state.score / 4f) : Color.white;
                    break;
            }
        }
        private static Vector2 CellPosition(int index, int width) => new((index % width - (width - 1) / 2f) * 1.1f, (index / width - (width - 1) / 2f) * 1.1f);
        private SpriteRenderer AddSprite(string name, Sprite sprite, Vector2 position, Vector2 size, int order, bool preserveAspect = true)
        {
            var child = new GameObject(name, typeof(SpriteRenderer)); child.transform.SetParent(transform, false); child.transform.localPosition = position;
            var renderer = child.GetComponent<SpriteRenderer>(); renderer.sprite = sprite != null ? sprite : fallback;
            if (spriteMaterial != null) renderer.sharedMaterial = spriteMaterial;
            var bounds = renderer.sprite.bounds.size;
            float scaleX = size.x / Mathf.Max(.01f, bounds.x), scaleY = size.y / Mathf.Max(.01f, bounds.y);
            if (sprite != null && preserveAspect) scaleX = scaleY = Mathf.Min(scaleX, scaleY);
            child.transform.localScale = new Vector3(scaleX, scaleY, 1);
            renderer.sortingOrder = order; return renderer;
        }
        public void Cancel() { running = false; StopAllCoroutines(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; running = false;
            if (fallback != null) Destroy(fallback);
            if (fallbackTexture != null) Destroy(fallbackTexture);
            context = null; state = null;
        }
        private void OnDestroy() => Dispose();
    }
}
