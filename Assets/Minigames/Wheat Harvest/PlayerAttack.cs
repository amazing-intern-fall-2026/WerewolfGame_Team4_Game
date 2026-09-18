using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class PlayerAttack : MonoBehaviour
{
    [Header("Hitbox")]
    [SerializeField] private Collider2D slashCollider;
    [SerializeField] private float slashActiveTime = 0.12f;
    [SerializeField] private float slashCooldown = 0.22f;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject slashVfx;

    [Header("Audio")]
    [SerializeField] private AudioClip slashSfx;
    [SerializeField] [Range(0f, 0.3f)] private float pitchVariation = 0.08f;

    [Header("Screen Positioning")]
    [Range(0.05f, 0.4f)]
    [SerializeField] private float screenLeftMarginPercent = 0.15f;

    private AudioSource audioSource;
    private bool canAttack = true;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D Audio
    }

    private void Start()
    {
        PositionPlayerToScreen();

        if (slashCollider != null) slashCollider.enabled = false;
        if (slashVfx != null) slashVfx.SetActive(false);
    }

    private void PositionPlayerToScreen()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 targetWorldPos = cam.ViewportToWorldPoint(new Vector3(screenLeftMarginPercent, 0f, 0f));
        transform.position = new Vector3(targetWorldPos.x, transform.position.y, transform.position.z);
    }

    private void Update()
    {
        if (WheatHarvestGameManager.Instance != null && WheatHarvestGameManager.Instance.IsGameOver) return;

        bool touchedScreen = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        bool mouseClicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        if ((touchedScreen || mouseClicked || spacePressed) && canAttack)
        {
            StartCoroutine(PerformSlash());
        }
    }

    private IEnumerator PerformSlash()
    {
        canAttack = false;

        if (audioSource != null && slashSfx != null)
        {
            audioSource.pitch = Random.Range(1f - pitchVariation, 1f + pitchVariation);
            audioSource.PlayOneShot(slashSfx);
        }

        if (slashCollider != null) slashCollider.enabled = true;
        if (slashVfx != null) slashVfx.SetActive(true);

        yield return new WaitForSeconds(slashActiveTime);

        if (slashCollider != null) slashCollider.enabled = false;
        if (slashVfx != null) slashVfx.SetActive(false);

        float remainingCooldown = Mathf.Max(0.01f, slashCooldown - slashActiveTime);
        yield return new WaitForSeconds(remainingCooldown);

        canAttack = true;
    }
}