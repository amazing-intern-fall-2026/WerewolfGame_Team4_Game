using UnityEngine;
using UnityEngine.UI;
using TMPro; // Optional: Remove if using standard UI Text

public class WellGameController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform pointer;
    [SerializeField] private RectTransform barContainer;
    [SerializeField] private RectTransform ropeImage; // UI Image representing the rope
    [SerializeField] private TextMeshProUGUI scoreText; // Displays successful pulls

    [Header("Zone Ranges (0 to 1)")]
    [SerializeField] private Vector2 yellowRange = new Vector2(0.35f, 0.65f);
    [SerializeField] private Vector2 greenRange = new Vector2(0.48f, 0.52f);

    [Header("Settings")]
    [SerializeField] private float basePointerSpeed = 1.5f;

    // State Variables
    private float currentProgress = 0f; // Pointer position in the bar (0 to 1)
    private bool movingForward = true;
    private bool isPlaying = true;
    
    private float currentBucketProgress = 0f; // 0 = at bottom, 1 = fully pulled up
    private int successfulPulls = 0;          // Total buckets successfully pulled

    void Update()
    {
        if (!isPlaying) return;

        // Tension makes it harder: pointer speeds up as the bucket gets closer to the top!
        float tensionMultiplier = 1f + (currentBucketProgress * 0.9f); 
        float currentSpeed = basePointerSpeed * tensionMultiplier;

        // Move the pointer back and forth (PingPong)
        float step = currentSpeed * Time.deltaTime;
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

        // Update pointer visual position along the bar
        float barWidth = barContainer.rect.width;
        float newX = (currentProgress - 0.5f) * barWidth;
        pointer.anchoredPosition = new Vector2(newX, pointer.anchoredPosition.y);

        // Detect tap/click
        if (Input.GetMouseButtonDown(0))
        {
            EvaluateTap();
        }
    }

    private void EvaluateTap()
    {
        isPlaying = false; // Pause pointer temporarily

        float heightGain = 0f;

        // Evaluate hit zone
        if (currentProgress >= greenRange.x && currentProgress <= greenRange.y)
        {
            Debug.Log("Green! Perfect Pull!");
            heightGain = 0.30f; // Big progress up
        }
        else if (currentProgress >= yellowRange.x && currentProgress <= yellowRange.y)
        {
            Debug.Log("Yellow! Decent Pull.");
            heightGain = 0.15f; // Moderate progress up
        }
        else
        {
            Debug.Log("Red! Missed / Rope slipped.");
            heightGain = -0.10f; // Penalty drop down
        }

        // Update overall bucket progress
        currentBucketProgress = Mathf.Clamp01(currentBucketProgress + heightGain);
        UpdateRopeTensionAnimation();

        // Check if bucket reached the top
        if (currentBucketProgress >= 1f)
        {
            successfulPulls++;
            UpdateScoreUI();
            Debug.Log("Bucket successfully collected! Total: " + successfulPulls);
            currentBucketProgress = 0f; // Reset for the next bucket
        }

        isPlaying = true; // Resume minigame
    }

    private void UpdateRopeTensionAnimation()
    {
        if (ropeImage != null)
        {
            // 1. Stretch the rope vertically to simulate strain
            float scaleY = 1f + (currentBucketProgress * 0.4f);
            ropeImage.localScale = new Vector3(1f, scaleY, 1f);

            // 2. Add a slight wobble/shake effect when the bucket gets heavy (> 60% progress)
            if (currentBucketProgress > 0.6f)
            {
                float shakeIntensity = currentBucketProgress * 4f;
                float randomShakeX = Random.Range(-shakeIntensity, shakeIntensity);
                ropeImage.anchoredPosition = new Vector2(ropeImage.anchoredPosition.x + randomShakeX, ropeImage.anchoredPosition.y);
            }
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Buckets: " + successfulPulls;
        }
    }
}