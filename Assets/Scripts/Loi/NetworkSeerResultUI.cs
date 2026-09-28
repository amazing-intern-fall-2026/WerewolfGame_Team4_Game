
using TMPro;
using UnityEngine;

public class NetworkSeerResultUI : MonoBehaviour
{
    [Header("Result Panel")]
    [SerializeField]
    private GameObject resultPanel;

    [Header("Result Text")]
    [SerializeField]
    private TextMeshProUGUI resultText;

    // =========================================
    // Enable
    // =========================================

    private void OnEnable()
    {
        NetworkPlayerAction.OnSeerResultReceived +=
            OnSeerResultReceived;
    }

    // =========================================
    // Disable
    // =========================================

    private void OnDisable()
    {
        NetworkPlayerAction.OnSeerResultReceived -=
            OnSeerResultReceived;
    }

    // =========================================
    // Start
    // =========================================

    private void Start()
    {
        HideResult();
    }

    // =========================================
    // Receive Seer Result
    // =========================================

    private void OnSeerResultReceived(
        int playerID,
        string playerName,
        RoleType role)
    {
        bool isDogSpirit =
            role == RoleType.DogSpirit;

        Debug.Log(
            "SEER UI | "
            + playerName
            + " | Is DogSpirit = "
            + isDogSpirit
        );

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (resultText != null)
        {
            resultText.text =
                "INSPECT RESULT\n\n"
                + playerName
                + "\n\n"
                + (
                    isDogSpirit
                        ? "DOGSPIRIT"
                        : "NOT DOGSPIRIT"
                );
        }
    }

    // =========================================
    // Hide Result
    // =========================================

    public void HideResult()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
    }
}

