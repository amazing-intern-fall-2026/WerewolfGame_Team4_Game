using System.Collections.Generic;
using System.Linq;
using Thuong.Maps;
using UnityEngine;
using UnityEngine.Tilemaps;

// Public day/phase only; never reads a role, faction or client-side gameplay manager.
public sealed class ThuongEventPresentation : MonoBehaviour
{
    private ThuongMapRuntime map;
    private ThuongEventPresentationCatalog catalog;
    private readonly Dictionary<SpriteRenderer, Color> sprites = new();
    private readonly Dictionary<Tilemap, Color> terrain = new();
    private GameObject overlay;
    private AudioSource audioSource;
    private AudioClip ambience;
    private Sprite fogSprite;
    private Texture2D fogTexture;
    private GameEventType current;
    private GamePhase phase;
    private int day;
    public int OverlayCount => overlay != null ? 1 : 0;
    public GameEventType Current => current;

    public void Bind(ThuongMapRuntime runtime, ThuongEventPresentationCatalog visuals)
    {
        Clear(); map = runtime; catalog = visuals; sprites.Clear(); terrain.Clear();
        if (map?.World == null) return;
        foreach (var mood in map.World.GetComponentsInChildren<ThuongVillageMood>(true)) mood.enabled = false;
        foreach (var sprite in map.World.GetComponentsInChildren<SpriteRenderer>(true))
            if (sprite.GetComponentInParent<TaskStation>() == null && sprite.GetComponent<ThuongSpriteLoop>()?.staysBrightAtNight != true)
                sprites[sprite] = sprite.color;
        foreach (var tilemap in map.World.GetComponentsInChildren<Tilemap>(true)) terrain[tilemap] = tilemap.color;
    }

    public void Apply(GameEventType eventType, int eventDay, GamePhase visiblePhase, bool running)
    {
        if (map?.World == null || catalog == null) return;
        if (!running || visiblePhase == GamePhase.GameOver || visiblePhase == GamePhase.Setup || visiblePhase == GamePhase.RoleReveal)
        { Clear(); return; }
        if (day != eventDay || current != eventType) { Clear(); day = eventDay; current = eventType; }
        phase = visiblePhase;
        bool night = phase == GamePhase.Night || phase == GamePhase.ResolveNight;
        var visual = catalog.Find(current);
        bool visible = visual != null && ThuongEventPresentationCatalog.IsVisible(current, phase);
        Color tint = night ? new Color(.4f, .48f, .68f) : Color.white;
        if (visible) tint = night ? visual.nightTint : visual.dayTint;
        foreach (var pair in sprites) if (pair.Key != null)
        { Color color = pair.Value * tint; color.a = pair.Key.color.a; pair.Key.color = color; }
        foreach (var pair in terrain) if (pair.Key != null) pair.Key.color = pair.Value * tint;
        if (!visible) { if (overlay != null) overlay.SetActive(false); if (audioSource != null) audioSource.volume = 0; return; }
        if (overlay == null) BuildOverlay();
        overlay.SetActive(true);
        bool dialogue = phase == GamePhase.Discussion || phase == GamePhase.Voting || phase == GamePhase.ResolveVote || phase == GamePhase.DayResolution;
        if (audioSource != null) audioSource.volume = dialogue ? 0 : night && current == GameEventType.HarvestFestival ? 0 :
            current == GameEventType.TuongNight && !night ? .003f : visual.volume;
    }

    private void BuildOverlay()
    {
        overlay = new GameObject("Event overlay - " + current);
        overlay.transform.SetParent(map.World.transform, false);
        var definition = map.Definition;
        var visual = catalog.Find(current);
        if (current == GameEventType.Fog)
        {
            EnsureFog();
            for (int i = 0; i < 7; i++)
            {
                var strip = Prop("Fog band", fogSprite, new Vector2(definition.boundsMin.x + 3 + i * 3.1f, definition.boundsMin.y + 2 + i * 3.1f));
                strip.transform.localScale = new Vector3(14, 2.8f, 1);
                strip.color = new Color(.64f, .75f, .8f, .13f); strip.sortingOrder = 3500;
            }
        }
        else if (current == GameEventType.ClearSky || current == GameEventType.BloodMoon)
        {
            for (int i = 0; i < 12; i++)
            {
                var glint = Prop("Atmosphere", visual.decoration, definition.lanterns[i % definition.lanterns.Length] + new Vector2((i % 3) * .45f, 1 + i * .12f));
                glint.transform.localScale = Vector3.one * .25f;
                glint.color = current == GameEventType.BloodMoon ? new Color(1, .32f, .14f, .65f) : new Color(1, .9f, .54f, .48f);
                glint.sortingOrder = 3500;
            }
        }
        else
        {
            foreach (var anchor in definition.lanterns)
                Prop("Event lantern", catalog.lantern, anchor + Vector2.up * 1.2f);
            if (current == GameEventType.HarvestFestival)
                for (int i = -2; i <= 2; i++) Prop("Harvest pennant", catalog.banner, definition.plaza + new Vector2(i * 1.2f, 2.6f));
            if (current == GameEventType.TuongNight)
            {
                Prop("Tuong stage", catalog.stage, definition.stage);
                Prop("Tuong mask", catalog.tuongMask, definition.stage + new Vector2(0, 1.3f));
            }
        }
        audioSource = overlay.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; audioSource.loop = true; audioSource.spatialBlend = 0;
        audioSource.volume = 0;
        ambience = AudioClip.Create("Quiet event ambience", 44100 * 4, 1, 44100, false);
        var samples = new float[44100 * 4];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / 44100f;
            float envelope = Mathf.Pow(1 - (t % 1), 3);
            samples[i] = current == GameEventType.Fog ? Mathf.Sin(t * Mathf.PI * 38) * .08f :
                Mathf.Sin(t * Mathf.PI * (current == GameEventType.TuongNight ? 440 : 660)) * envelope * .12f;
        }
        ambience.SetData(samples, 0); audioSource.clip = ambience; if (Application.isPlaying) audioSource.Play();
    }

    private SpriteRenderer Prop(string label, Sprite sprite, Vector2 position)
    {
        var root = new GameObject(label, typeof(SpriteRenderer)); root.transform.SetParent(overlay.transform, false);
        root.transform.position = position;
        var renderer = root.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
        renderer.sharedMaterial = map.World.GetComponentInChildren<SpriteRenderer>()?.sharedMaterial;
        renderer.sortingOrder = ThuongDepthSort.Order(position.y) + 30;
        return renderer;
    }

    private void EnsureFog()
    {
        if (fogSprite != null) return;
        fogTexture = new Texture2D(64, 16, TextureFormat.RGBA32, false);
        var pixels = new Color[64 * 16];
        for (int y = 0; y < 16; y++) for (int x = 0; x < 64; x++)
            pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Sin(Mathf.PI * x / 63) * Mathf.Sin(Mathf.PI * y / 15));
        fogTexture.SetPixels(pixels); fogTexture.Apply();
        fogSprite = Sprite.Create(fogTexture, new Rect(0, 0, 64, 16), new Vector2(.5f, .5f), 64);
    }

    private void Update()
    {
        if (overlay == null || !overlay.activeSelf) return;
        int index = 0;
        foreach (Transform child in overlay.transform)
        {
            if (current == GameEventType.Fog)
                child.localPosition = new Vector3(map.Definition.boundsMin.x + 3 + index * 3.1f + Mathf.Sin(Time.unscaledTime * .12f + index) * 1.6f,
                    map.Definition.boundsMin.y + 2 + index * 3.1f, 0);
            else if (child.name == "Atmosphere")
                child.localScale = Vector3.one * (.23f + Mathf.Sin(Time.unscaledTime * 1.5f + index) * .04f);
            else if (current == GameEventType.GhostMonth && child.name == "Event lantern")
                child.localPosition = map.Definition.lanterns[index] + Vector2.up * (1.2f + Mathf.Sin(Time.unscaledTime + index) * .14f);
            else if (current == GameEventType.TuongNight && child.name == "Tuong mask")
                child.localScale = Vector3.one * (phase == GamePhase.Night ? 1 + Mathf.Sin(Time.unscaledTime * 1.5f) * .025f : 1);
            index++;
        }
    }

    public void Clear()
    {
        if (audioSource != null) audioSource.Stop();
        if (overlay != null) { overlay.SetActive(false); Release(overlay); }
        if (ambience != null) Release(ambience);
        overlay = null; ambience = null; audioSource = null; current = GameEventType.None; day = 0;
        foreach (var pair in sprites) if (pair.Key != null) pair.Key.color = pair.Value;
        foreach (var pair in terrain) if (pair.Key != null) pair.Key.color = pair.Value;
    }
    private static void Release(Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    private void OnDestroy() { Clear(); if (fogSprite != null) Release(fogSprite); if (fogTexture != null) Release(fogTexture); }
}
