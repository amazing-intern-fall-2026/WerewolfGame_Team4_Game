using UnityEngine;

public class Fruit : MonoBehaviour
{
    [SerializeField] private int pointValue = 1;
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
            CatchFruitGameManager.Instance.AddScore(pointValue);
            Destroy(gameObject);
        }
        else if (hitObject.CompareTag("Ground"))
        {
            isHandled = true;
            CatchFruitGameManager.Instance.GameOver();
            Destroy(gameObject);
        }
    }
}