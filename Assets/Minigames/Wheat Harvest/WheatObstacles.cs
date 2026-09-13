using UnityEngine;

public class WheatObstacle : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float fallbackSpeed = 6f;

    [Header("Visuals")]
    [SerializeField] private ParticleSystem cutParticles;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private bool isCut = false;
    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        if (isCut) return;

        float speed = fallbackSpeed;
        if (GameManager.Instance != null && GameManager.Instance.GameSpeed > 0)
        {
            speed = GameManager.Instance.GameSpeed;
        }

        transform.Translate(Vector3.left * speed * Time.deltaTime, Space.World);

        if (transform.position.x < -15f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCut) return;

        if (other.CompareTag("Scythe"))
        {
            CutDown();
        }
        else if (other.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
                GameManager.Instance.TriggerGameOver();
        }
    }

    private void CutDown()
    {
        isCut = true;

        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(10);

        if (col != null) col.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (cutParticles != null) cutParticles.Play();

        float delay = cutParticles != null ? cutParticles.main.duration : 0.05f;
        Destroy(gameObject, delay);
    }
}