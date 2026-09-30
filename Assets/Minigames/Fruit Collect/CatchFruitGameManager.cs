using UnityEngine;
using TMPro; // Required for TextMeshPro

public class CatchFruitGameManager : MonoBehaviour
{
    public static CatchFruitGameManager Instance { get; private set; }

    [Header("UI Reference")]
    [SerializeField] private TMP_Text scoreText;

    [Header("Dependencies")]
    [SerializeField] private FruitSpawner spawner;

    private int score = 0;
    private bool isGameOver = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        UpdateScoreUI();
    }

    public void AddScore(int points)
    {
        if (isGameOver) return;

        score += points;
        UpdateScoreUI();
    }

    private void UpdateScoreUI()
{
    if (scoreText != null)
    {
        // Includes the "SCORE: " prefix before the number
        scoreText.text = $"SCORE: {score}";
    }
}

    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (spawner != null)
        {
            spawner.StopSpawning();
        }

        Time.timeScale = 0f;
        Debug.Log($"Game Over! Final Score: {score}");
    }
}