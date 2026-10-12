using UnityEngine;

public sealed class ThuongSpriteLoop : MonoBehaviour
{
    public Sprite[] frames;
    public float framesPerSecond = 8;
    public bool staysBrightAtNight;
    private SpriteRenderer spriteRenderer;
    private void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();
    private void Update()
    {
        if (spriteRenderer != null && frames != null && frames.Length > 0)
            spriteRenderer.sprite = frames[Mathf.FloorToInt(Time.time * framesPerSecond) % frames.Length];
    }
}
