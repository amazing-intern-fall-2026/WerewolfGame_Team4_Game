using UnityEngine;

public class Log : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float destroyX = 10f;

    [Header("Physics on Split")]
    [SerializeField] private float flingForceX = 3f;
    [SerializeField] private float flingForceY = 4f;
    [SerializeField] private float torqueForce = 180f;

    [HideInInspector] public bool isInChopZone = false;
    [HideInInspector] public bool isCut = false;

    private SpriteRenderer sr;
    private float spawnTime;
    private const float MinLifeTime = 0.5f;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        spawnTime = Time.time;
    }

    private void Update()
    {
        transform.Translate(Vector2.right * speed * Time.deltaTime);

        if (Time.time - spawnTime > MinLifeTime && transform.position.x > destroyX && !isCut)
        {
            #if UNITY_2023_1_OR_NEWER
            FindFirstObjectByType<WoodGameManager>()?.MissLog();
            #else
            FindObjectOfType<WoodGameManager>()?.MissLog();
            #endif

            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("ChopZone")) isInChopZone = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("ChopZone")) isInChopZone = false;
    }

    /// <summary>
    /// Slices the log at the worldX coordinate into two separate flying pieces.
    /// </summary>
    public void CutAtPosition(float cutWorldX)
    {
        isCut = true;

        if (sr != null && sr.sprite != null)
        {
            Sprite original = sr.sprite;
            Rect origRect = original.rect;
            float ppu = original.pixelsPerUnit;
            float spriteWorldWidth = origRect.width / ppu;

            // Calculate local cut ratio [0..1] relative to log bounds
            float minX = transform.position.x - (spriteWorldWidth * 0.5f);
            float cutRatio = Mathf.Clamp01((cutWorldX - minX) / spriteWorldWidth);

            // Avoid sliver pieces below 5%
            cutRatio = Mathf.Clamp(cutRatio, 0.05f, 0.95f);

            float cutPixelX = origRect.width * cutRatio;

            // Left half
            Rect leftRect = new Rect(origRect.x, origRect.y, cutPixelX, origRect.height);
            Vector3 leftWorldPos = new Vector3(minX + (cutPixelX / ppu * 0.5f), transform.position.y, transform.position.z);
            CreateHalfPiece(original.texture, leftRect, ppu, leftWorldPos, new Vector2(-flingForceX, flingForceY), torqueForce);

            // Right half
            Rect rightRect = new Rect(origRect.x + cutPixelX, origRect.y, origRect.width - cutPixelX, origRect.height);
            Vector3 rightWorldPos = new Vector3(minX + (cutPixelX / ppu) + ((origRect.width - cutPixelX) / ppu * 0.5f), transform.position.y, transform.position.z);
            CreateHalfPiece(original.texture, rightRect, ppu, rightWorldPos, new Vector2(flingForceX, flingForceY), -torqueForce);
        }

        Destroy(gameObject);
    }

    private void CreateHalfPiece(Texture2D texture, Rect rect, float ppu, Vector3 position, Vector2 force, float torque)
    {
        GameObject piece = new GameObject("LogPiece");
        piece.transform.position = position;
        piece.transform.localScale = transform.localScale;

        SpriteRenderer pieceSr = piece.AddComponent<SpriteRenderer>();
        pieceSr.sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), ppu);
        pieceSr.sortingLayerID = sr.sortingLayerID;
        pieceSr.sortingOrder = sr.sortingOrder;

        Rigidbody2D rb = piece.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 3f;
        rb.linearVelocity = force;
        rb.angularVelocity = torque;

        Destroy(piece, 2.5f);
    }
}