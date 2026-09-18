using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WoodGameManager : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private GameObject logPrefab;
    [SerializeField] private Transform spawnPoint; // Off-screen left (e.g., X = -10, Y = 0, Z = 0)
    [SerializeField] private float spawnInterval = 1.5f;

    [Header("Hit Target")]
    [SerializeField] private Transform targetCenter; // ChopZone center (0, 0, 0)
    [SerializeField] private Text scoreText;         // UI Text component

    private readonly List<Log> activeLogs = new List<Log>();
    private int score = 0;

    private void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    private void Update()
    {
        // Works for both mouse click and mobile touch
        if (Input.GetMouseButtonDown(0))
        {
            HandleChop();
        }
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            if (logPrefab != null && spawnPoint != null)
            {
                GameObject newLog = Instantiate(logPrefab, spawnPoint.position, Quaternion.identity);
                Log logComponent = newLog.GetComponent<Log>();

                if (logComponent != null)
                {
                    activeLogs.Add(logComponent);
                }
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void HandleChop()
    {
        // Remove null references from logs that despawned
        activeLogs.RemoveAll(l => l == null);

        // Find the first log currently inside the target zone
        Log targetLog = activeLogs.Find(l => l.isInChopZone && !l.isCut);

        if (targetLog != null)
        {
            float targetX = targetCenter != null ? targetCenter.position.x : 0f;
            float offset = Mathf.Abs(targetLog.transform.position.x - targetX);

            if (offset < 0.35f)
            {
                score += 100; // Perfect chop
            }
            else
            {
                score += 50;  // Good chop
            }

            activeLogs.Remove(targetLog);
            targetLog.Cut();
            UpdateUI();
        }
        else
        {
            Debug.Log("Swing and a miss!");
        }
    }

    public void MissLog()
    {
        activeLogs.RemoveAll(l => l == null);
        Debug.Log("Log escaped!");
    }

    private void UpdateUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }
    }
}