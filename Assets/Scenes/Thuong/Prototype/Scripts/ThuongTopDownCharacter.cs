using Assets.Scripts.Thuong;
using TMPro;
using UnityEngine;

// Skin follows Player ID, never the secret role/faction.
[DefaultExecutionOrder(1100)]
public sealed class ThuongTopDownCharacter : MonoBehaviour
{
    public ThuongMedievalCast cast;
    public SpriteRenderer spriteRenderer;
    public Animator animator;
    public LineRenderer selectionRing;
    private PlayerMovement movement;
    private bool wasAlive = true;

    private void Awake() => movement = GetComponentInParent<PlayerMovement>();
    private void Start() => SetPlayerID(movement != null ? movement.playerID : 0);

    public void SetPlayerID(int id)
    {
        if (cast == null || cast.Count == 0) return;
        int skin = Mathf.Abs(id) % cast.Count;
        spriteRenderer.sprite = cast.idleSprites[skin];
        spriteRenderer.color = Color.white;
        animator.runtimeAnimatorController = cast.controllers[skin];
        animator.SetFloat("LastInputX", 0);
        animator.SetFloat("LastInputY", -1);
        animator.SetBool("isWalking", false);
        animator.SetBool("isDead", false);
        wasAlive = true;
    }

    public void SetSelected(bool selected)
    {
        if (selectionRing != null) selectionRing.enabled = selected;
        var label = transform.parent.GetComponentInChildren<PlayerNameTag>();
        if (label != null)
        {
            var text = label.GetComponent<TMP_Text>();
            if (text != null) text.color = selected ? new Color32(255, 218, 119, 255) : new Color32(249, 241, 214, 255);
        }
    }

    private void LateUpdate()
    {
        if (movement == null || animator == null || animator.runtimeAnimatorController == null) return;
        bool alive = PlayerManager.Instance == null || movement.IsAlive;
        if (alive != wasAlive)
        {
            animator.SetBool("isDead", !alive);
            if (!alive) animator.SetBool("isWalking", false);
            wasAlive = alive;
        }
        spriteRenderer.sortingOrder = ThuongDepthSort.Order(transform.parent.position.y);
        if (selectionRing != null) selectionRing.sortingOrder = spriteRenderer.sortingOrder - 1;
        var tag = transform.parent.GetComponentInChildren<PlayerNameTag>();
        if (tag != null && tag.TryGetComponent(out Renderer textRenderer)) textRenderer.sortingOrder = 5000;
    }
}
