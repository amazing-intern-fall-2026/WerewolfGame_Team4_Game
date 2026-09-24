using UnityEngine;

public class CatchFruitGameManager : MonoBehaviour
{
    public static CatchFruitGameManager Instance { get; private set; }

    [SerializeField] private FruitSpawner spawner;
    private int score = 0;
    private bool isGameOver = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddScore(int points)
    {
        if (isGameOver) return;
        score += points;
        Debug.Log($"Caught! Total Score: {score}");
    }

    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (spawner != null) spawner.StopSpawning();
        Time.timeScale = 0f;
        Debug.Log($"Fruit hit the floor! Game Over. Final Score: {score}");
    }
}