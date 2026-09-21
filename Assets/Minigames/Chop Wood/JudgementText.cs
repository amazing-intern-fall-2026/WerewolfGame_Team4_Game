using UnityEngine;
using TMPro;

public class JudgmentText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI tmpText;
    [SerializeField] private float floatSpeed = 1.2f;
    [SerializeField] private float fadeDuration = 0.6f;

    private Color initialColor;
    private float timer = 0f;

    private void Awake()
    {
        if (tmpText == null) tmpText = GetComponent<TextMeshProUGUI>();
    }

    public void Setup(string text, Color color)
    {
        tmpText.text = text;
        tmpText.color = color;
        initialColor = color;
        timer = 0f;
    }

    private void Update()
    {
        transform.position += Vector3.up * (floatSpeed * Time.deltaTime);

        timer += Time.deltaTime;
        float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
        tmpText.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);

        if (timer >= fadeDuration)
        {
            Destroy(gameObject);
        }
    }
}