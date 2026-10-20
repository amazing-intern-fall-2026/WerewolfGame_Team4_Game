using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkDeathNotification : MonoBehaviour
{
    public static NetworkDeathNotification Instance { get; private set; }

    [SerializeField] private GameObject deathNotification;
    [SerializeField] private TMP_Text deathText;
    [SerializeField] private float autoHideDuration = 4f;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (deathNotification != null)
            deathNotification.SetActive(false);
    }

    private void OnEnable()
    {
        NetworkPlayerStateSync.OnLocalPlayerDeath += ShowDeathNotification;
    }

    private void OnDisable()
    {
        NetworkPlayerStateSync.OnLocalPlayerDeath -= ShowDeathNotification;
    }

    public void ShowDeathNotification()
    {
        if (deathNotification != null)
        {
            deathNotification.SetActive(true);

            // Đảm bảo có nút Đóng
            EnsureCloseButton();

            // Cho phép click vào bất kỳ đâu trên nền thông báo để đóng
            var bgButton = deathNotification.GetComponent<Button>();
            if (bgButton == null)
            {
                bgButton = deathNotification.AddComponent<Button>();
                bgButton.transition = Selectable.Transition.None;
            }
            bgButton.onClick.RemoveListener(HideDeathNotification);
            bgButton.onClick.AddListener(HideDeathNotification);
        }

        if (deathText != null)
            deathText.text = "Bạn đã chết";

        // Tự động đóng sau autoHideDuration giây
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }

        if (autoHideDuration > 0f)
        {
            hideCoroutine = StartCoroutine(AutoHideRoutine(autoHideDuration));
        }
    }

    public void HideDeathNotification()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        if (deathNotification != null)
        {
            deathNotification.SetActive(false);
        }

        Debug.Log("DEATH NOTIFICATION: Đã đóng thông báo Bạn đã chết.");
    }

    private IEnumerator AutoHideRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideDeathNotification();
    }

    private void EnsureCloseButton()
    {
        if (deathNotification == null)
            return;

        Transform existing = deathNotification.transform.Find("CloseDeathNotificationBtn");
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return;
        }

        GameObject btnGO = new GameObject("CloseDeathNotificationBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(deathNotification.transform, false);

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -55f);
        rt.sizeDelta = new Vector2(160f, 44f);

        Image img = btnGO.GetComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.22f, 0.95f);

        Outline outline = btnGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.35f, 0.35f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(HideDeathNotification);

        GameObject textGO = new GameObject("BtnText", typeof(RectTransform));
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "ĐÓNG (X)";
        tmp.fontSize = 20;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
    }
}