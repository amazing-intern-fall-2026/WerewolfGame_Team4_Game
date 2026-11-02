using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class WellMinigame : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform pointer;
    [SerializeField] private RectTransform barContainer;      // BarBackground
    [SerializeField] private RectTransform yellowZone; 
    [SerializeField] private RectTransform greenZone;  
    [SerializeField] private TextMeshProUGUI scoreText;      

    [Header("Bucket World Reference")]
    [SerializeField] private SpriteRenderer bucketSpriteRenderer; // Drag "TX Village Props - Bucket" here

    [Header("Bucket Animation Settings")]
    [SerializeField] private float floatDistance = 1.0f;       // Height the bucket floats up
    [SerializeField] private float floatDuration = 0.5f;        // Time spent floating up
    [SerializeField] private float fadeOutDuration = 0.5f;      // Time spent fading out at top
    [SerializeField] private float fadeInDuration = 0.3f;       // Time spent appearing at start pos

    [Header("Game Settings")]
    [SerializeField] private float initialPointerSpeed = 1.2f;
    [SerializeField] private float speedIncreasePerTap = 0.3f; 
    [SerializeField] private int requiredPointsToComplete = 4; // Green = 2 pts, Yellow = 1 pt

    // Internal State Variables
    private float currentProgress = 0f;
    private bool movingForward = true;
    private bool isPlaying = true;

    private float currentPointerSpeed;
    private int currentPoints = 0;
    private int totalBucketsCollected = 0;

    private Vector2 currentYellowRange;
    private Vector2 currentGreenRange;

    void Start()
    {
        currentPointerSpeed = initialPointerSpeed;
        
        // Ensure bucket starts fully visible at the well
        if (bucketSpriteRenderer != null)
        {
            SetBucketAlpha(1f);
        }

        RandomizeZones(); 
        UpdateScoreUI();
    }

    void Update()
    {
        if (!isPlaying) return;

        // Pointer Ping-Pong movement
        float step = currentPointerSpeed * Time.deltaTime;
        currentProgress = movingForward ? currentProgress + step : currentProgress - step;

        if (currentProgress >= 1f)
        {
            currentProgress = 1f;
            movingForward = false;
        }
        else if (currentProgress <= 0f)
        {
            currentProgress = 0f;
            movingForward = true;
        }

        // Update UI position of the pointer along the bar
        float barWidth = barContainer.rect.width;
        float newX = (currentProgress - 0.5f) * barWidth;
        pointer.anchoredPosition = new Vector2(newX, pointer.anchoredPosition.y);

        // Detect screen tap/click
        if (Input.GetMouseButtonDown(0))
        {
            EvaluateTap();
        }
    }

    private void RandomizeZones()
    {
        float barWidth = barContainer.rect.width;

        // 1. Randomize Yellow Zone (Width: 30% - 50% of bar)
        float yellowWidth = barWidth * Random.Range(0.3f, 0.5f);
        yellowZone.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, yellowWidth);

        float maxXOffset = (barWidth - yellowWidth) * 0.4f;
        float randomYellowX = Random.Range(-maxXOffset, maxXOffset);
        yellowZone.anchoredPosition = new Vector2(randomYellowX, yellowZone.anchoredPosition.y);

        float yellowMinLocal = randomYellowX - (yellowWidth * 0.5f);
        float yellowMaxLocal = randomYellowX + (yellowWidth * 0.5f);
        currentYellowRange = new Vector2(
            (yellowMinLocal + (barWidth * 0.5f)) / barWidth,
            (yellowMaxLocal + (barWidth * 0.5f)) / barWidth
        );

        // 2. Randomize Green Zone (Small hitbox INSIDE Yellow Zone)
        float greenWidth = yellowWidth * Random.Range(0.15f, 0.25f);
        greenZone.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, greenWidth);

        float maxGreenOffset = (yellowWidth - greenWidth) * 0.4f;
        float randomGreenLocalX = Random.Range(-maxGreenOffset, maxGreenOffset);
        greenZone.anchoredPosition = new Vector2(randomGreenLocalX, 0);

        // 3. Convert positions to normalized ranges (0 to 1) for hit detection
        Vector3[] greenCorners = new Vector3[4];
        greenZone.GetWorldCorners(greenCorners);
        
        Vector3[] barCorners = new Vector3[4];
        barContainer.GetWorldCorners(barCorners);

        float greenWorldMinX = greenCorners[0].x;
        float greenWorldMaxX = greenCorners[3].x;
        float barWorldMinX = barCorners[0].x;
        float barWorldMaxX = barCorners[3].x;
        float barTotalWidth = barWorldMaxX - barWorldMinX;

        currentGreenRange = new Vector2(
            (greenWorldMinX - barWorldMinX) / barTotalWidth,
            (greenWorldMaxX - barWorldMinX) / barTotalWidth
        );
    }

    private void EvaluateTap()
    {
        isPlaying = false; // Pause pointer movement during evaluation

        if (currentProgress >= currentGreenRange.x && currentProgress <= currentGreenRange.y)
        {
            Debug.Log("Green! Perfect Pull! (+2 Points)");
            RegisterPull(2); // Green awards 2 points (Wins faster)
        }
        else if (currentProgress >= currentYellowRange.x && currentProgress <= currentYellowRange.y)
        {
            Debug.Log("Yellow! Good Pull! (+1 Point)");
            RegisterPull(1); // Yellow awards 1 point (Takes longer)
        }
        else
        {
            Debug.Log("Red! Missed!");
            // Penalty on miss
            currentPoints = Mathf.Max(0, currentPoints - 1);
            currentPointerSpeed = Mathf.Max(initialPointerSpeed, currentPointerSpeed - 0.2f);
            
            RandomizeZones(); 
            isPlaying = true; 
        }
    }

    private void RegisterPull(int pointsGained)
    {
        currentPoints += pointsGained;
        currentPointerSpeed += speedIncreasePerTap; // Increase speed per success

        if (currentPoints >= requiredPointsToComplete)
        {
            totalBucketsCollected++;
            UpdateScoreUI();
            StartCoroutine(BucketAnimationRoutine());
        }
        else
        {
            RandomizeZones(); 
            isPlaying = true; 
        }
    }

    private IEnumerator BucketAnimationRoutine()
    {
        if (bucketSpriteRenderer == null) yield break;

        Transform bucketTransform = bucketSpriteRenderer.transform;
        Vector3 startPosition = bucketTransform.position; 
        Vector3 floatTargetPosition = startPosition + new Vector3(0, floatDistance, 0);

        // STEP 1: FLOAT - Bucket moves upward while remaining fully visible
        float elapsed = 0f;
        while (elapsed < floatDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / floatDuration;
            bucketTransform.position = Vector3.Lerp(startPosition, floatTargetPosition, progress);
            yield return null;
        }
        bucketTransform.position = floatTargetPosition;

        // STEP 2: FADE OUT - Bucket stays floating and fades away
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeOutDuration;
            SetBucketAlpha(Mathf.Lerp(1f, 0f, progress));
            yield return null;
        }
        SetBucketAlpha(0f);

        // STEP 3: REAPPEAR AT START - Teleport back to well mouth and fade in
        bucketTransform.position = startPosition;
        elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeInDuration;
            SetBucketAlpha(Mathf.Lerp(0f, 1f, progress));
            yield return null;
        }
        SetBucketAlpha(1f);

        // Reset round state
        currentPoints = 0;
        currentPointerSpeed = initialPointerSpeed;
        RandomizeZones(); 
        isPlaying = true;
    }

    private void SetBucketAlpha(float alpha)
    {
        if (bucketSpriteRenderer != null)
        {
            Color color = bucketSpriteRenderer.color;
            color.a = alpha;
            bucketSpriteRenderer.color = color;
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Buckets: " + totalBucketsCollected;
        }
    }
}