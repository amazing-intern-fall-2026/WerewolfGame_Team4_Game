using UnityEngine;
using UnityEngine.UI;

public class WellMinigame : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform pointer;
    [SerializeField] private RectTransform barContainer;
    
    [Header("Zone Normalized Ranges (0 to 1)")]
    [Tooltip("Min and Max normalized X position for the Yellow zone")]
    [SerializeField] private Vector2 yellowRange = new Vector2(0.35f, 0.65f);
    [Tooltip("Min and Max normalized X position for the Green zone (must be inside Yellow)")]
    [SerializeField] private Vector2 greenRange = new Vector2(0.48f, 0.52f);

    [Header("Settings")]
    [SerializeField] private float pointerSpeed = 1.5f;

    private float currentProgress = 0f;
    private bool movingForward = true;
    private bool isPlaying = true;

    void Update()
    {
        if (!isPlaying) return;

        // Move the pointer back and forth (PingPong effect)
        float step = pointerSpeed * Time.deltaTime;
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

        // Update pointer position visually along the bar width
        float barWidth = barContainer.rect.width;
        float newX = (currentProgress - 0.5f) * barWidth;
        pointer.anchoredPosition = new Vector2(newX, pointer.anchoredPosition.y);

        // Detect player tap/click anywhere on the screen
        if (Input.GetMouseButtonDown(0))
        {
            EvaluateTap();
        }
    }

    private void EvaluateTap()
    {
        isPlaying = false; // Stop pointer movement

        // Check which zone the pointer landed in
        if (currentProgress >= greenRange.x && currentProgress <= greenRange.y)
        {
            OnPerfectPull();
        }
        else if (currentProgress >= yellowRange.x && currentProgress <= yellowRange.y)
        {
            OnGoodPull();
        }
        else
        {
            OnBadPull();
        }
    }

    private void OnPerfectPull()
    {
        Debug.Log("GREEN! Perfect Pull! Bucket pulls up fast.");
        // TODO: Add high upward movement to the bucket, play success VFX/SFX
        ResetGame();
    }

    private void OnGoodPull()
    {
        Debug.Log("YELLOW! Decent Pull. Bucket pulls up normally.");
        // TODO: Add moderate upward movement to the bucket
        ResetGame();
    }

    private void OnBadPull()
    {
        Debug.Log("RED! Missed! Bucket slips back down.");
        // TODO: Add slip-down penalty, play splash SFX
        ResetGame();
    }

    public void ResetGame()
    {
        // Call this to restart the minigame round or close it after a delay
        isPlaying = true;
    }
}