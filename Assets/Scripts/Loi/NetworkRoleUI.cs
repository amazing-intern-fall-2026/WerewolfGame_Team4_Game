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
        NetworkRoleSync.OnLocalWhiteHoundAwakenedChanged += OnWhiteHoundAwakenedChanged;
    }

    private void OnDisable()
    {
        NetworkRoleSync.OnLocalRoleReceived -= OnRoleReceived;
        NetworkRoleSync.OnLocalWhiteHoundAwakenedChanged -= OnWhiteHoundAwakenedChanged;
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

        bool awakened = (role == RoleType.WhiteHound || role == RoleType.WhiteWolf) &&
                        NetworkRoleSync.LocalInstance != null &&
                        NetworkRoleSync.LocalInstance.IsWhiteHoundAwakened;

        roleText.text = awakened
            ? "ROLE\nWHITEHOUND (DOGSPIRIT)"
            : "ROLE\n" + role.ToString();

        Debug.Log(
            "ROLE UI | Hiển thị Role = "
            + role
            + " | Awakened = "
            + awakened
        );

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }

        hideCoroutine = StartCoroutine(
            HideRoleAfterDelay()
        );
    }

    private void OnWhiteHoundAwakenedChanged(bool awakened)
    {
        if (!awakened || roleText == null)
            return;

        if (NetworkRoleSync.LocalInstance != null &&
            (NetworkRoleSync.LocalInstance.LocalRole == RoleType.WhiteHound ||
             NetworkRoleSync.LocalInstance.LocalRole == RoleType.WhiteWolf))
        {
            OnRoleReceived(NetworkRoleSync.LocalInstance.LocalRole);
        }
    }

    private IEnumerator HideRoleAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);

        gameObject.SetActive(false);
    }
}