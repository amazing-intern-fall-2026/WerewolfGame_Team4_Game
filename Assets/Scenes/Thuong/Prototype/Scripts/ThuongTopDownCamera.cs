using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    private Canvas viewportFrame;
    private readonly RectTransform[] frameEdges = new RectTransform[4];

    private void Awake() => view = GetComponent<Camera>();
    private void OnEnable() => RefreshViewportFrame();
    private void OnDisable()
    {
        if (viewportFrame != null) viewportFrame.gameObject.SetActive(false);
    }

    // The world camera only clears its viewport. Paint the remaining pixels every
    // frame, otherwise closing a full-screen lobby/modal leaves its old image there.
    private void RefreshViewportFrame()
    {
        if (view == null) view = GetComponent<Camera>();
        if (viewportFrame == null)
        {
            var root = new GameObject("Viewport frame", typeof(Canvas));
            root.transform.SetParent(transform, false);
            viewportFrame = root.GetComponent<Canvas>();
            viewportFrame.renderMode = RenderMode.ScreenSpaceOverlay;
            viewportFrame.sortingOrder = -1000;
            for (int i = 0; i < frameEdges.Length; i++)
            {
                var edge = new GameObject("Viewport edge " + i, typeof(RectTransform), typeof(Image));
                frameEdges[i] = edge.GetComponent<RectTransform>();
                frameEdges[i].SetParent(root.transform, false);
                var image = edge.GetComponent<Image>();
                image.color = new Color32(26, 33, 25, 255);
                image.raycastTarget = false;
            }
        }
        viewportFrame.gameObject.SetActive(true);
        viewportFrame.targetDisplay = view.targetDisplay;
        Rect area = view.rect;
        PlaceFrameEdge(0, Vector2.zero, new Vector2(area.xMin, 1));
        PlaceFrameEdge(1, new Vector2(area.xMax, 0), Vector2.one);
        PlaceFrameEdge(2, new Vector2(area.xMin, 0), new Vector2(area.xMax, area.yMin));
        PlaceFrameEdge(3, new Vector2(area.xMin, area.yMax), new Vector2(area.xMax, 1));
    }

    private void PlaceFrameEdge(int index, Vector2 min, Vector2 max)
    {
        RectTransform edge = frameEdges[index];
        edge.anchorMin = min; edge.anchorMax = max;
        edge.offsetMin = edge.offsetMax = Vector2.zero;
    }
    public void Follow(Transform value)
    {
        target = value;
        if (view == null) view = GetComponent<Camera>();
        Refresh(true);
    }
    private void Update()
    {
        if (NetworkChatUI.Instance?.IsInputFocused != true && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) overview = !overview;
    }
    private void LateUpdate() => Refresh(false);
    public void Refresh(bool snap)
    {
        if (view == null) view = GetComponent<Camera>();
        RefreshViewportFrame();
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
