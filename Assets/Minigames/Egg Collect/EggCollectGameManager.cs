using UnityEngine;

public class EggCollectGameManager : MonoBehaviour
{
    public static EggCollectGameManager Instance { get; private set; }

    [SerializeField] private EggSpawner spawner;
    private int score = 0;
    private bool isGameOver = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddScore()
    {
        if (isGameOver) return;
        score++;
        Debug.Log($"Egg caught! Current Score: {score}");
    }

    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        
        spawner.StopSpawning();
        Time.timeScale = 0f; // Freeze gameplay
        Debug.Log($"Missed an egg! Game Over. Final Score: {score}");
    }
}