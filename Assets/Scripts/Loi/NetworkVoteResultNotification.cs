using UnityEngine;
using TMPro;

public class NetworkVoteResultNotification : MonoBehaviour
{
    [Header("UI")]
    [SerializeField]
    private GameObject resultPanel;

    [SerializeField]
    private TMP_Text resultTitle;

    [SerializeField]
    private TMP_Text resultText;

    private void Start()
    {
        if (resultPanel != null)
            resultPanel.SetActive(false);
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
    }

    public void HideResult()
    {
        if (resultPanel != null)
            resultPanel.SetActive(false);
    }
}