using UnityEngine;

public class ThreatSpawner : MonoBehaviour
{
    [Header("Spawning Settings")]
    [SerializeField] private GameObject threatPrefab;
    [SerializeField] private float spawnRadius = 8f;

    [Header("Difficulty Ramp")]
    [SerializeField] private float initialSpawnInterval = 1.5f; // Starting delay between spawns
    [SerializeField] private float minimumSpawnInterval = 0.35f; // Fastest delay allowed
    [SerializeField] private float timeToMaxDifficulty = 60f; // Seconds to reach minimum delay

    private float currentInterval;
    private float timer;
    private float elapsedTime;

    void Start()
    {
        currentInterval = initialSpawnInterval;
    }

    void Update()
    {
        // 1. Track survival time and smoothly reduce interval
        elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsedTime / timeToMaxDifficulty);
        currentInterval = Mathf.Lerp(initialSpawnInterval, minimumSpawnInterval, progress);

        // 2. Spawn countdown timer
        timer += Time.deltaTime;
        if (timer >= currentInterval)
        {
            SpawnThreat();
            timer = 0f;
        }
    }

    private void SpawnThreat()
    {
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 spawnPos = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle)) * spawnRadius;

        GameObject threat = Instantiate(threatPrefab, spawnPos, Quaternion.identity);

        Vector2 dirToCenter = (Vector2.zero - spawnPos).normalized;
        float angle = Mathf.Atan2(dirToCenter.y, dirToCenter.x) * Mathf.Rad2Deg - 90f;
        threat.transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}