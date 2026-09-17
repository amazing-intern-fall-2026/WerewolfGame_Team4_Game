using UnityEngine;

public class Threat : MonoBehaviour
{
    [SerializeField] private float speed = 4f;
    [SerializeField] private Transform target;
    [SerializeField] private float angleOffset = -90f;


    void Start()
    {
        RotateTowardsTarget();
    }

    void Update()
    {
        // Move steadily toward the center
        transform.position = Vector2.MoveTowards(transform.position, Vector2.zero, speed * Time.deltaTime);
        Vector3 targetPos = target != null ? target.position : Vector3.zero;
        
        // Move toward the target
        transform.position = Vector2.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

        // If the player can move, keep calling this in Update().
        // If the player is static at (0,0), you only need to call it once in Start().
        RotateTowardsTarget();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Shield"))
        {
            // Successfully blocked
            // Trigger deflect VFX/SFX, add score
            Destroy(gameObject);
        }
        else if (collision.CompareTag("Player"))
        {
            // Player hit
            // Trigger damage/game over logic
            Destroy(gameObject);
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