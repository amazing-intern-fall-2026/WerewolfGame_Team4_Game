using System.Collections;
using UnityEngine;

public class EggSpawner : MonoBehaviour
{
    [SerializeField] private GameObject eggPrefab;
    [SerializeField] private float spawnInterval = 1.2f;
    
    private float minX;
    private float maxX;
    private float spawnY;
    private bool isSpawning = true;

    void Start()
    {
        Camera cam = Camera.main;
        Vector3 left = cam.ViewportToWorldPoint(new Vector3(0.05f, 1.05f, 0));
        Vector3 right = cam.ViewportToWorldPoint(new Vector3(0.95f, 1.05f, 0));

        minX = left.x;
        maxX = right.x;
        spawnY = left.y;

        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        while (isSpawning)
        {
            float randX = Random.Range(minX, maxX);
            Instantiate(eggPrefab, new Vector3(randX, spawnY, 0), Quaternion.identity);
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    public void StopSpawning() => isSpawning = false;
}