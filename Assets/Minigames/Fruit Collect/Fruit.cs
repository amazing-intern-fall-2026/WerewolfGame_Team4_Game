using UnityEngine;

public class Fruit : MonoBehaviour
{
    [SerializeField] private int pointValue = 1;
    [SerializeField] private GameObject floatingScorePrefab; // Assign the prefab here
    private bool isHandled = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        ProcessHit(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ProcessHit(collision.gameObject);
    }

    private void ProcessHit(GameObject hitObject)
    {
        if (isHandled) return;

        if (hitObject.CompareTag("CatchZone"))
        {
            isHandled = true;

            // 1. Play SFX
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayCatchSound();
            }

            // 2. Spawn Floating "+1"
            if (floatingScorePrefab != null)
            {
                GameObject popup = Instantiate(floatingScorePrefab, transform.position, Quaternion.identity);
                FloatingScore scoreScript = popup.GetComponent<FloatingScore>();
                if (scoreScript != null)
                {
                    scoreScript.Setup(pointValue);
                }
            }

            // 3. Register score and destroy
            CatchFruitGameManager.Instance.AddScore(pointValue);
            Destroy(gameObject);
        }
        else if (hitObject.CompareTag("Ground"))
        {
            isHandled = true;
            
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayGameOverSound();
            }

            CatchFruitGameManager.Instance.GameOver();
            Destroy(gameObject);
        }
    }
}