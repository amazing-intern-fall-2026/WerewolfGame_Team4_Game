using UnityEngine;

public class Log : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float destroyX = 10f; // Edge of the screen on the right

    [HideInInspector] public bool isInChopZone = false;
    [HideInInspector] public bool isCut = false;

    // Grace period prevents instant despawning on frame 1 if spawned near destroyX
    private float spawnTime;
    private const float MinLifeTimeBeforeDespawn = 0.5f;

    private void Start()
    {
        spawnTime = Time.time;
    }

    private void Update()
    {
        // Move from left to right
        transform.Translate(Vector2.right * speed * Time.deltaTime);

        // Despawn if it crosses past destroyX after the grace period
        if (Time.time - spawnTime > MinLifeTimeBeforeDespawn && transform.position.x > destroyX && !isCut)
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
        if (other.CompareTag("ChopZone"))
        {
            isInChopZone = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("ChopZone"))
        {
            isInChopZone = false;
        }
    }

    public void Cut()
    {
        isCut = true;
        Destroy(gameObject);
    }
}