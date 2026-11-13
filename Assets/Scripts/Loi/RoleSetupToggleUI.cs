using UnityEngine;

public class RoleSetupToggleUI : MonoBehaviour
{
    [SerializeField]
    private GameObject roleSetupPanel;

    private bool isOpen = true;

    private void Start()
    {
        if (LobbyManager.Instance != null && LobbyManager.Instance.UsesThuongGameplay)
        {
            isOpen = false;
            if (roleSetupPanel != null) roleSetupPanel.SetActive(false);
            gameObject.SetActive(false);
            return;
        }
        if (roleSetupPanel == null)
        {
            Debug.LogError(
                "ROLE TOGGLE | Chưa gán RoleSetupPanel."
            );

            return;
        }

        roleSetupPanel.SetActive(isOpen);
    }

    public void ToggleRoleSetup()
    {
        if (roleSetupPanel == null)
            return;

        isOpen = !isOpen;

        roleSetupPanel.SetActive(isOpen);
    }
}
