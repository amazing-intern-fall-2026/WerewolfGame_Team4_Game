using UnityEngine;

public class Egg : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Basket"))
        {
            EggCollectGameManager.Instance.AddScore();
            Destroy(gameObject); // Or return to pool
        }
        else if (collision.CompareTag("Ground"))
        {
            EggCollectGameManager.Instance.GameOver();
            Destroy(gameObject);
        }
    }
}