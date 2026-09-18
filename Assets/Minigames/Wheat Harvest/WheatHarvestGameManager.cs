using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class WheatHarvestGameManager : MonoBehaviour
{
    public static WheatHarvestGameManager Instance { get; private set; }

    [Header("Game Pace")]
    [SerializeField] private float baseSpeed = 6f;
    [SerializeField] private float speedRamp = 0.05f;

    [Header("Spawning")]
    [SerializeField] private GameObject wheatPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float minInterval = 1.2f;
    [SerializeField] private float maxInterval = 2.4f;

    public float GameSpeed { get; private set; }
    public int Score { get; private set; }
    public bool IsGameOver { get; private set; }

    private float timer = 0f;
    private float nextSpawnTime = 1f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        GameSpeed = baseSpeed > 0 ? baseSpeed : 6f;
    }

    private void Start()
    {
        AlignSpawnPointToScreen();
        SetNextSpawnTime();
    }

    private void Update()
    {
        if (IsGameOver)
        {
            CheckRestartInput();
            return;
        }

        GameSpeed += speedRamp * Time.deltaTime;

        timer += Time.deltaTime;
        if (timer >= nextSpawnTime)
        {
            SpawnWheat();
            timer = 0f;
            SetNextSpawnTime();
        }
    }

    private void AlignSpawnPointToScreen()
    {
        Camera cam = Camera.main;
        if (cam == null || spawnPoint == null) return;

        // Position spawn point just beyond the right edge of the screen
        float screenRightEdge = cam.ViewportToWorldPoint(new Vector3(1f, 0f, 0f)).x;
        spawnPoint.position = new Vector3(screenRightEdge + 2f, spawnPoint.position.y, 0f);
    }

    private void CheckRestartInput()
    {
        bool touched = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool space = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        if (touched || clicked || space)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void SetNextSpawnTime()
    {
        float ratio = (baseSpeed > 0) ? (GameSpeed / baseSpeed) : 1f;
        nextSpawnTime = Random.Range(minInterval, maxInterval) / Mathf.Max(ratio, 0.5f);
    }

    private void SpawnWheat()
    {
        if (wheatPrefab == null || spawnPoint == null) return;
        Instantiate(wheatPrefab, spawnPoint.position, Quaternion.identity);
    }

    public void AddScore(int amount)
    {
        if (IsGameOver) return;
        Score += amount;
        Debug.Log($"Score: {Score}");
    }

    public void TriggerGameOver()
    {
        IsGameOver = true;
        GameSpeed = 0f;
        Debug.Log($"GAME OVER! Final Score: {Score}");
    }
}