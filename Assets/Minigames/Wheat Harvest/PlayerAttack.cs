using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("Hitbox")]
    [SerializeField] private Collider2D slashCollider;
    [SerializeField] private float slashActiveTime = 0.12f;
    [SerializeField] private float slashCooldown = 0.22f;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject slashVfx;

    private bool canAttack = true;

    private void Start()
    {
        if (slashCollider != null)
            slashCollider.enabled = false;

        if (slashVfx != null)
            slashVfx.SetActive(false);
    }

    private void Update()
    {
        // 1. Mobile screen tap
        bool touchedScreen = Touchscreen.current != null && 
                             Touchscreen.current.primaryTouch.press.wasPressedThisFrame;

        // 2. Editor / Desktop click fallback
        bool mouseClicked = Mouse.current != null && 
                            Mouse.current.leftButton.wasPressedThisFrame;

        if ((touchedScreen || mouseClicked) && canAttack)
        {
            StartCoroutine(PerformSlash());
        }
    }

    private IEnumerator PerformSlash()
    {
        canAttack = false;

        if (slashCollider != null) slashCollider.enabled = true;
        if (slashVfx != null) slashVfx.SetActive(true);

        yield return new WaitForSeconds(slashActiveTime);

        if (slashCollider != null) slashCollider.enabled = false;
        if (slashVfx != null) slashVfx.SetActive(false);

        yield return new WaitForSeconds(slashCooldown - slashActiveTime);
        canAttack = true;
    }
}