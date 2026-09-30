using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FruitSpawner : MonoBehaviour
{
    [Header("Fruit Configuration")]
    [Tooltip("Danh sách các Prefab trái cây rơi")]
    [SerializeField] private List<GameObject> fruitPrefabs = new List<GameObject>();

    [Header("Spawn Settings")]
    [SerializeField] private float initialSpawnInterval = 1.5f;
    [SerializeField] private float minimumSpawnInterval = 0.4f;
    [SerializeField] private float difficultyRampRate = 0.02f;

    private float currentInterval;
    private float minX;
    private float maxX;
    private float spawnY;
    private bool isSpawning = true;
    private Coroutine spawnCoroutine;

    void Start()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[FruitSpawner] Main Camera not found!");
            return;
        }

        Vector3 left = cam.ViewportToWorldPoint(new Vector3(0.08f, 1.08f, 0f));
        Vector3 right = cam.ViewportToWorldPoint(new Vector3(0.92f, 1.08f, 0f));

        minX = left.x;
        maxX = right.x;
        spawnY = left.y;
        currentInterval = initialSpawnInterval;

        if (fruitPrefabs.Count > 0)
        {
            spawnCoroutine = StartCoroutine(SpawnLoop());
        }
        else
        {
            Debug.LogError("[FruitSpawner] No fruit prefabs assigned!");
        }
    }

    private IEnumerator SpawnLoop()
    {
        while (isSpawning)
        {
            SpawnRandomFruit();
            yield return new WaitForSeconds(currentInterval);
            currentInterval = Mathf.Max(minimumSpawnInterval, currentInterval - difficultyRampRate);
        }
    }

    private void SpawnRandomFruit()
    {
        if (fruitPrefabs.Count == 0) return;

        int randomIndex = Random.Range(0, fruitPrefabs.Count);
        GameObject selectedFruit = fruitPrefabs[randomIndex];
        if (selectedFruit == null) return;

        float randX = Random.Range(minX, maxX);
        Vector3 spawnPosition = new Vector3(randX, spawnY, 0f);

        GameObject fruitInstance = Instantiate(selectedFruit, spawnPosition, Quaternion.identity);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayDropSound();
        }

        Rigidbody2D fruitRb = fruitInstance.GetComponent<Rigidbody2D>();
        if (fruitRb != null)
        {
            fruitRb.angularVelocity = Random.Range(-150f, 150f);
        }
    }

    public void SetSpawnInterval(float newInterval) => currentInterval = newInterval;

    public void StopSpawning()
    {
        isSpawning = false;
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }
}