using System.Collections;
using TMPro;
using UnityEngine;

public class NetworkRoleUI : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI roleText;

    [SerializeField]
    private float displayDuration = 3f;

    private Coroutine hideCoroutine;

    private void OnEnable()
    {
        NetworkRoleSync.OnLocalRoleReceived += OnRoleReceived;
    }

    private void OnDisable()
    {
        NetworkRoleSync.OnLocalRoleReceived -= OnRoleReceived;
    }

    private void Start()
    {
        if (roleText != null)
        {
            roleText.text = "";
        }

        if (NetworkRoleSync.LocalInstance != null)
        {
            OnRoleReceived(
                NetworkRoleSync.LocalInstance.LocalRole
            );
        }
    }

    private void OnRoleReceived(RoleType role)
    {
        if (roleText == null)
            return;

        gameObject.SetActive(true);

        roleText.text =
            "ROLE\n" + role.ToString();

        Debug.Log(
            "ROLE UI | Hiển thị Role = "
            + role
        );

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }

        hideCoroutine = StartCoroutine(
            HideRoleAfterDelay()
        );
    }

    private IEnumerator HideRoleAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);

        gameObject.SetActive(false);
    }
}