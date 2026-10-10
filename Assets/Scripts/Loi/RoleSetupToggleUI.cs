using UnityEngine;

public class RoleSetupToggleUI : MonoBehaviour
{
    [SerializeField]
    private GameObject roleSetupPanel;

    private bool isOpen = true;

    private void Start()
    {
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