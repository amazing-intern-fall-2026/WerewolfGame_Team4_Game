using UnityEngine;

[ExecuteAlways]
public sealed class ThuongDepthSort : MonoBehaviour
{
    public float footOffset;
    private SpriteRenderer spriteRenderer;
    public static int Order(float footY) => 2000 - Mathf.RoundToInt(footY * 48);
    private void OnEnable() { spriteRenderer = GetComponent<SpriteRenderer>(); Refresh(); }
    private void LateUpdate() => Refresh();
    private void Refresh()
    {
        if (spriteRenderer != null) spriteRenderer.sortingOrder = Order(transform.position.y + footOffset);
    }
}
