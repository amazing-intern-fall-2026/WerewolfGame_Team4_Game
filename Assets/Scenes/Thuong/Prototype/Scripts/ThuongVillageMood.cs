using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(2100)]
public sealed class ThuongVillageMood : MonoBehaviour
{
    public Color nightTint = new Color(.40f, .48f, .68f, 1);
    public SpriteRenderer[] renderers;
    private Color[] originalColors;
    private float darkness;
    private Tilemap[] terrain;
    private Color[] terrainColors;

    private void Start()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true)
            .Where(r => r.GetComponent<ThuongSpriteLoop>()?.staysBrightAtNight != true).ToArray();
        originalColors = renderers.Select(r => r.color).ToArray();
        terrain = GetComponentsInChildren<Tilemap>();
        terrainColors = terrain.Select(t => t.color).ToArray();
    }
    private void LateUpdate()
    {
        if (originalColors == null) return;
        bool night = GameRoleManager.Instance != null && GameRoleManager.Instance.currentState == GameState.Night;
        darkness = Mathf.MoveTowards(darkness, night ? 1 : 0, Time.unscaledDeltaTime * 2);
        Color tint = Color.Lerp(Color.white, nightTint, darkness);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = originalColors[i] * tint;
        for (int i = 0; i < terrain.Length; i++)
            if (terrain[i] != null) terrain[i].color = terrainColors[i] * tint;
    }
}
