using UnityEngine;

public class Threat : MonoBehaviour
{
    [SerializeField] private float speed = 4f;

    void Update()
    {
        // Move steadily toward the center
        transform.position = Vector2.MoveTowards(transform.position, Vector2.zero, speed * Time.deltaTime);
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
}