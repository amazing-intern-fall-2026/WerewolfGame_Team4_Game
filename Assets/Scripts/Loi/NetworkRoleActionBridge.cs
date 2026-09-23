using UnityEngine;

public class NetworkRoleActionBridge : MonoBehaviour
{
    private void OnEnable()
    {
        NetworkPlayerAction.OnNetworkActionAccepted += OnActionAccepted;
    }

    private void OnDisable()
    {
        NetworkPlayerAction.OnNetworkActionAccepted -= OnActionAccepted;
    }

    private void OnActionAccepted(NetworkActionEvent actionEvent)
    {
        int requesterID = (int)actionEvent.RequesterPlayerId;
        int targetID = (int)actionEvent.TargetPlayerId;

        Debug.Log(
            "ROLE ACTION BRIDGE | Player "
            + requesterID
            + " → Target "
            + targetID
        );

        RoleManager roleManager = RoleManager.Instance;

        if (roleManager == null)
        {
            roleManager = FindFirstObjectByType<RoleManager>();
        }

        if (roleManager == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: Không tìm thấy RoleManager!"
            );
            return;
        }

        if (roleManager.playerRoles == null ||
            roleManager.playerRoles.Count == 0)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: playerRoles đang rỗng!"
            );

            roleManager.AssignRole();
        }

        if (!roleManager.playerRoles.TryGetValue(
            requesterID,
            out BaseRole role))
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: Không tìm thấy Role của Player "
                + requesterID
            );
            return;
        }

        Debug.Log(
            "ROLE ACTION BRIDGE | Player "
            + requesterID
            + " | Role = "
            + role.roleType
        );

        NightManager nightManager = NightManager.Instance;

        if (nightManager == null)
        {
            nightManager = FindFirstObjectByType<NightManager>();

            if (nightManager != null)
            {
                NightManager.Instance = nightManager;

                Debug.Log(
                    "ROLE ACTION BRIDGE | Đã tìm thấy NightManager"
                );
            }
        }

        if (nightManager == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: Không tìm thấy NightManager!"
            );
            return;
        }

        Debug.Log(
            "ROLE ACTION BRIDGE | UseNightAbility("
            + targetID
            + ")"
        );

        if (!roleManager.UseNightAbility(requesterID, targetID))
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: Action bị từ chối bởi game flow."
            );
        }
    }
}