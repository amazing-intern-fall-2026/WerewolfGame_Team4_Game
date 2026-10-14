using UnityEngine;
using TMPro;

public class NetworkDeathNotification : MonoBehaviour
{
    [SerializeField] private GameObject deathNotification;
    [SerializeField] private TMP_Text deathText;

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

    private void ShowDeathNotification()
    {
        if (deathNotification != null)
            deathNotification.SetActive(true);

        if (deathText != null)
            deathText.text = "Bạn đã chết";
    }
}