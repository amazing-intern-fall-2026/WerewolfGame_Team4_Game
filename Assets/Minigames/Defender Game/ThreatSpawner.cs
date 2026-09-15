using UnityEngine;

public class ThreatSpawner : MonoBehaviour
{
    [SerializeField] private GameObject threatPrefab;
    [SerializeField] private float spawnRadius = 8f;
    [SerializeField] private float spawnInterval = 1.2f;

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            SpawnThreat();
            timer = 0f;
        }
    }

    private void SpawnThreat()
    {
        // Pick a random angle around the circle
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 spawnPos = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle)) * spawnRadius;

        GameObject threat = Instantiate(threatPrefab, spawnPos, Quaternion.identity);

        // Orient threat to face the center (0,0)
        Vector2 dirToCenter = (Vector2.zero - spawnPos).normalized;
        float angle = Mathf.Atan2(dirToCenter.y, dirToCenter.x) * Mathf.Rad2Deg - 90f;
        threat.transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}