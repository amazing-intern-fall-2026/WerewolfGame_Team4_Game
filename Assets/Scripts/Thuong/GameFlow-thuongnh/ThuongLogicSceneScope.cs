using UnityEngine;

// Optional logic integrations are owned by this scene and never alter the other teams' sources.
public sealed class ThuongLogicSceneScope : MonoBehaviour
{
    private NetworkGameOverManager legacyResults;
    private bool restoreResults;
    public static bool IsActive => GameRoleManager.Instance != null &&
        (GameRoleManager.Instance.manualLogicPrototype || GameRoleManager.Instance.HasIntegratedLogic ||
         Thuong.Online.ThuongOnlineSession.Active != null);

    public static void Ensure(GameRoleManager owner)
    {
        if (owner == null || Thuong.Online.ThuongOnlineSession.Active != null ||
            !(owner.manualLogicPrototype || owner.HasIntegratedLogic) ||
            owner.GetComponent<ThuongLogicSceneScope>() != null) return;
        owner.gameObject.AddComponent<ThuongLogicSceneScope>();
    }
    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        legacyResults = NetworkGameOverManager.Instance;
        restoreResults = legacyResults.enabled;
        legacyResults.ResetUIState();
        legacyResults.enabled = false;
    }
    private void OnDisable()
    {
        if (legacyResults != null && restoreResults) legacyResults.enabled = true;
        legacyResults = null; restoreResults = false;
    }
}
