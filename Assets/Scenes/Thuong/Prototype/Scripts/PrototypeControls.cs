using Assets.Scripts.Thuong;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Input bridge confined to the offline prototype.
[RequireComponent(typeof(PlayerMovement))]
public class PrototypeControls : MonoBehaviour
{
    private InputAction move;
    private void Awake()
    {
        move = new InputAction("PrototypeMove", InputActionType.Value);
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        move.AddBinding("<Gamepad>/leftStick");
        var movement = GetComponent<PlayerMovement>();
        move.performed += movement.Move;
        move.canceled += movement.Move;
    }
    private void OnEnable() { move?.Enable(); }
    private void OnDisable() { move?.Disable(); }
    private void OnDestroy() { move?.Dispose(); }
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                SceneManager.GetActiveScene().path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
#endif
        }
    }
}
