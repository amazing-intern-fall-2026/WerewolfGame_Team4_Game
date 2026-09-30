using UnityEngine;
using TMPro;

public class FloatingScore : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.8f;
    [SerializeField] private float lifetime = 0.8f;

    // TMP_Text là class cha của cả TextMeshPro (World) và TextMeshProUGUI (Canvas UI)
    private TMP_Text tmpText;
    private Color chosenColor;
    private float timer = 0f;

    void Awake()
    {
        EnsureTextComponent();
        AssignRandomVibrantColor();
    }

    private void EnsureTextComponent()
    {
        if (tmpText == null)
        {
            // Tìm component trên chính nó hoặc trên object con
            tmpText = GetComponentInChildren<TMP_Text>();
        }
    }

    public void Setup(int points)
    {
        EnsureTextComponent();

        if (tmpText != null)
        {
            tmpText.text = $"+{points}";
        }
        else
        {
            Debug.LogError($"[FloatingScore] Không tìm thấy component TextMeshPro nào trên {gameObject.name}!");
        }
    }

    private void AssignRandomVibrantColor()
    {
        EnsureTextComponent();

        if (tmpText == null) return;

        // Tạo màu ngẫu nhiên rực rỡ
        chosenColor = Color.HSVToRGB(Random.value, Random.Range(0.8f, 1f), Random.Range(0.9f, 1f));
        chosenColor.a = 1f;
        tmpText.color = chosenColor;
    }

    void Update()
    {
        // Bay lên
        transform.position += Vector3.up * (moveSpeed * Time.deltaTime);

        // Mờ dần theo thời gian
        timer += Time.deltaTime;
        if (tmpText != null)
        {
            float alpha = Mathf.Lerp(1f, 0f, timer / lifetime);
            tmpText.color = new Color(chosenColor.r, chosenColor.g, chosenColor.b, alpha);
        }

        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}