using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class NetworkVoteResultNotification : MonoBehaviour
{
    [Header("UI")]
    [SerializeField]
    private GameObject resultPanel;

    [SerializeField]
    private TMP_Text resultTitle;

    [SerializeField]
    private TMP_Text resultText;

    [SerializeField]
    private Button okButton;

    [SerializeField]
    private float autoDismissSeconds = 5f;

    private void Start()
    {
        if (resultPanel != null)
            resultPanel.SetActive(false);

        if (okButton != null)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(HideResult);
        }
    }

    public void ShowResult(string title, string message)
    {
        if (resultPanel != null)
            resultPanel.SetActive(true);

        if (resultTitle != null)
            resultTitle.text = title;

        if (resultText != null)
            resultText.text = message;

        Debug.Log(
            "VOTE RESULT UI | SHOW | "
            + title
            + " | "
            + message
        );

        CancelInvoke(nameof(HideResult));
        if (autoDismissSeconds > 0)
        {
            Invoke(nameof(HideResult), autoDismissSeconds);
        }
    }

    public void HideResult()
    {
        CancelInvoke(nameof(HideResult));
        if (resultPanel != null)
            resultPanel.SetActive(false);
    }
}