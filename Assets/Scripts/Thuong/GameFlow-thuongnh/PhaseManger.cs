using UnityEngine;
public class PhaseManger : MonoBehaviour
{
    public static PhaseManger Instance;
    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void StartRoleReveal() { GameRoleManager.Instance?.BeginGame(); }
}
