using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class ThuongTopDownCamera : MonoBehaviour
{
    public Transform target;
    public Vector2 boundsMin = new Vector2(-13, -11);
    public Vector2 boundsMax = new Vector2(15, 13);
    public float gameplaySize = 6.5f;
    public float smoothing = 12f;
    public bool overview;
    private Camera view;

    private void Awake() => view = GetComponent<Camera>();
    public void Follow(Transform value)
    {
        target = value;
        if (view == null) view = GetComponent<Camera>();
        Refresh(true);
    }
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) overview = !overview;
    }
    private void LateUpdate() => Refresh(false);
    public void Refresh(bool snap)
    {
        if (view == null) view = GetComponent<Camera>();
        view.orthographic = true;
        view.transform.rotation = Quaternion.identity;
        float aspect = view.aspect;
        float height = overview ? Mathf.Max((boundsMax.y-boundsMin.y)/2 + 1.5f,
            (boundsMax.x-boundsMin.x)/(2 * Mathf.Max(.1f, aspect)) + 1.5f) : gameplaySize;
        view.orthographicSize = height;
        Vector2 desired = overview || target == null ? (boundsMin+boundsMax)/2 : (Vector2)target.position;
        float halfWidth = height * aspect;
        desired.x = ClampAxis(desired.x, boundsMin.x, boundsMax.x, halfWidth);
        desired.y = ClampAxis(desired.y, boundsMin.y, boundsMax.y, height);
        Vector3 position = new Vector3(desired.x, desired.y, -10);
        transform.position = snap ? position : Vector3.Lerp(transform.position, position, 1-Mathf.Exp(-smoothing * Time.unscaledDeltaTime));
    }
    private static float ClampAxis(float value, float min, float max, float extent)
        => max-min <= extent*2 ? (min+max)/2 : Mathf.Clamp(value, min+extent, max-extent);
}
