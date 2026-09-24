using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FruitSpawner : MonoBehaviour
{
    [Header("Fruit Configuration")]
    [Tooltip("Add all your fruit prefabs here (Apple, Banana, Watermelon, etc.)")]
    [SerializeField] private List<GameObject> fruitPrefabs = new List<GameObject>();

    [Header("Spawn Settings")]
    [SerializeField] private float initialSpawnInterval = 1.5f;
    [SerializeField] private float minimumSpawnInterval = 0.4f;
    [SerializeField] private float difficultyRampRate = 0.02f; // Seconds reduced per spawn

    private float currentInterval;
    private float minX;
    private float maxX;
    private float spawnY;
    private bool isSpawning = true;

    void Start()
    {
        Camera cam = Camera.main;
        Vector3 left = cam.ViewportToWorldPoint(new Vector3(0.08f, 1.05f, 0));
        Vector3 right = cam.ViewportToWorldPoint(new Vector3(0.92f, 1.05f, 0));

        minX = left.x;
        maxX = right.x;
        spawnY = left.y;
        currentInterval = initialSpawnInterval;

        if (fruitPrefabs.Count > 0)
        {
            StartCoroutine(SpawnLoop());
        }
        else
        {
            Debug.LogError("No fruit prefabs assigned to FruitSpawner!");
        }
    }

    private IEnumerator SpawnLoop()
    {
        while (isSpawning)
        {
            SpawnRandomFruit();

            yield return new WaitForSeconds(currentInterval);

            // Dynamically speed up interval over time
            currentInterval = Mathf.Max(minimumSpawnInterval, currentInterval - difficultyRampRate);
        }
    }

    private void SpawnRandomFruit()
    {
        int randomIndex = Random.Range(0, fruitPrefabs.Count);
        GameObject selectedFruit = fruitPrefabs[randomIndex];

        float randX = Random.Range(minX, maxX);
        Vector3 spawnPosition = new Vector3(randX, spawnY, 0f);

        GameObject fruitInstance = Instantiate(selectedFruit, spawnPosition, Quaternion.identity);

        // Add a slight random spin to make the drop and rim bounces look organic
        Rigidbody2D fruitRb = fruitInstance.GetComponent<Rigidbody2D>();
        if (fruitRb != null)
        {
            fruitRb.angularVelocity = Random.Range(-120f, 120f);
        }
    }

    public void SetSpawnInterval(float newInterval) => currentInterval = newInterval;
    public void StopSpawning() => isSpawning = false;
}