using UnityEngine;
public class PhaseManager : MonoBehaviour
{
    public static PhaseManager Instance;
    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void StartRoleReveal() { GameRoleManager.Instance?.BeginGame(); }
}
