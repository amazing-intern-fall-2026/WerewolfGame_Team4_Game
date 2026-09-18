using UnityEngine;

public class Threat : MonoBehaviour
{
    [Header("Movement & Target")]
    [SerializeField] private float speed = 4f;
    [SerializeField] private Transform target;
    [SerializeField] private float angleOffset = -90f;

    [Header("Audio")]
    [SerializeField] private AudioClip[] blockSounds; // Array of sound clips
    [Range(0f, 1f)]
    [SerializeField] private float blockVolume = 1f;

    void Start()
    {
        RotateTowardsTarget();
    }

    void Update()
    {
        Vector3 targetPos = target != null ? target.position : Vector3.zero;

        // Move toward target position
        transform.position = Vector2.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

        // Keep pointing towards the target
        RotateTowardsTarget();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Shield"))
        {
            PlayRandomBlockSound();

            // Successfully blocked
            Destroy(gameObject);
        }
        else if (collision.CompareTag("Player"))
        {
            // Player hit
            // Trigger damage/game over logic
            Destroy(gameObject);
        }
    }

    private void PlayRandomBlockSound()
    {
        if (blockSounds != null && blockSounds.Length > 0)
        {
            // Pick a random clip from the array
            int randomIndex = Random.Range(0, blockSounds.Length);
            AudioClip clipToPlay = blockSounds[randomIndex];

            if (clipToPlay != null)
            {
                AudioSource.PlayClipAtPoint(clipToPlay, transform.position, blockVolume);
            }
        }
    }

    private void RotateTowardsTarget()
    {
        Vector3 targetPos = target != null ? target.position : Vector3.zero;

        // 1. Get the direction vector from the arrow to the player
        Vector2 direction = (targetPos - transform.position).normalized;

        // 2. Convert the vector to degrees
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 3. Apply the angle to the Z-axis with the sprite offset
        transform.rotation = Quaternion.Euler(0f, 0f, angle + angleOffset);
    }
}