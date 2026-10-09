using UnityEngine;

// Presentation only: swapping art never replaces PlayerID, physics, input or match state.
public sealed class ThuongCharacterView : MonoBehaviour
{
    public ThuongCharacterAppearance appearance;
    public SpriteRenderer spriteRenderer;
    public Animator animator;
    private void Awake() => ApplyAppearance();
    public void ApplyAppearance()
    {
        if (appearance == null) return;
        if (spriteRenderer != null) { spriteRenderer.sprite = appearance.sprite; spriteRenderer.color = appearance.tint; }
        if (animator != null) animator.runtimeAnimatorController = appearance.animatorController;
        transform.localScale = appearance.visualScale;
    }
}
