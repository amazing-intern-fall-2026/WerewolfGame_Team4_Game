using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(AudioSource))]
public class WoodGameManager : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private GameObject logPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnInterval = 1.5f;

    [Header("Chop Reference")]
    [SerializeField] private Transform targetCenter; // Target center marker (X = 0)

    [Header("UI Feedback")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private GameObject judgmentPrefab; // Prefab with JudgmentText.cs
    [SerializeField] private Transform judgmentSpawnPos;

    [Header("Audio")]
    [SerializeField] private AudioClip chopSound;       // Axe blade / impact sound
    [SerializeField] private AudioClip woodBreakSound;  // Wood splinter / breaking sound
    [SerializeField] private AudioClip missSound;       // Optional: Swing whoosh / miss sound
    [Range(0f, 1f)] [SerializeField] private float soundVolume = 1f;

    [Header("Tolerance Thresholds")]
    [SerializeField] private float perfectThreshold = 0.20f;
    [SerializeField] private float greatThreshold = 0.50f;
    [SerializeField] private float okThreshold = 0.90f;

    private readonly List<Log> activeLogs = new List<Log>();
    private AudioSource audioSource;
    private int score = 0;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        // Ensure background music or loop isn't turned on accidentally
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    private void Update()
    {
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
                Log logComp = newLog.GetComponent<Log>();
                if (logComp != null) activeLogs.Add(logComp);
            }
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void HandleChop()
    {
        activeLogs.RemoveAll(l => l == null);

        Log targetLog = activeLogs.Find(l => l.isInChopZone && !l.isCut);

        if (targetLog != null)
        {
            float centerX = targetCenter != null ? targetCenter.position.x : 0f;
            float distance = Mathf.Abs(targetLog.transform.position.x - centerX);

            // Play the cutting audio clips together
            PlayCutAudio();

            if (distance <= perfectThreshold)
            {
                score += 300;
                SpawnJudgment("PERFECT!", new Color(1f, 0.84f, 0f)); // Gold
            }
            else if (distance <= greatThreshold)
            {
                score += 150;
                SpawnJudgment("GREAT!", new Color(0.2f, 0.9f, 0.3f)); // Green
            }
            else if (distance <= okThreshold)
            {
                score += 50;
                SpawnJudgment("OK", new Color(0.3f, 0.7f, 1f)); // Blue
            }
            else
            {
                score += 10;
                SpawnJudgment("EARLY / LATE", new Color(0.7f, 0.7f, 0.7f)); // Gray
            }

            activeLogs.Remove(targetLog);
            targetLog.CutAtPosition(centerX);
            UpdateUI();
        }
        else
        {
            if (missSound != null)
            {
                audioSource.PlayOneShot(missSound, soundVolume * 0.6f);
            }
            SpawnJudgment("MISS", new Color(0.9f, 0.2f, 0.2f)); // Red
        }
    }

    private void PlayCutAudio()
    {
        // Slight pitch variation makes rapid chops feel less repetitive
        audioSource.pitch = Random.Range(0.95f, 1.05f);

        if (chopSound != null)
        {
            audioSource.PlayOneShot(chopSound, soundVolume);
        }

        if (woodBreakSound != null)
        {
            audioSource.PlayOneShot(woodBreakSound, soundVolume);
        }
    }

    public void MissLog()
    {
        activeLogs.RemoveAll(l => l == null);
        SpawnJudgment("MISS", new Color(0.9f, 0.2f, 0.2f));
    }

    private void SpawnJudgment(string text, Color color)
    {
        if (judgmentPrefab == null) return;

        Vector3 pos = judgmentSpawnPos != null 
            ? judgmentSpawnPos.position 
            : (targetCenter != null ? targetCenter.position + Vector3.up * 1.5f : Vector3.up * 1.5f);

        Transform parentCanvas = FindFirstObjectByType<Canvas>()?.transform;
        GameObject jObj = Instantiate(judgmentPrefab, pos, Quaternion.identity, parentCanvas);
        
        JudgmentText jText = jObj.GetComponent<JudgmentText>();
        if (jText != null)
        {
            jText.Setup(text, color);
        }
    }

    private void UpdateUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }
    }
}