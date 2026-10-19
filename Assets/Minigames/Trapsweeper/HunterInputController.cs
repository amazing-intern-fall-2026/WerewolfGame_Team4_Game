using UnityEngine;

public class HunterInputController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private Grid grid;
    [SerializeField] private HunterTilemapManager manager;

    [Header("Touch & Pan Settings")]
    [SerializeField] private float longPressTime = 0.3f;
    [SerializeField] private float dragThresholdPixels = 15f;

    [Header("Zoom Settings")]
    [SerializeField] private float minZoom = 2f;
    [SerializeField] private float maxZoom = 15f;
    [SerializeField] private float pinchZoomSpeed = 0.015f;
    [SerializeField] private float scrollZoomSpeed = 2f;

    private float pressTimer;
    private Vector2 startScreenPos;
    private Vector3 dragOriginWorld;
    private bool isPointerDown;
    private bool isDragging;
    private bool longPressTriggered;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
    }

    void Update()
    {
        // 1. Handle 2-Finger Pinch Zoom on Mobile
        if (Input.touchCount == 2)
        {
            isPointerDown = false;
            isDragging = false;
            HandlePinchZoom();
            return;
        }

        // 2. Handle 1-Finger Touch (Mobile & Unity Device Simulator)
        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began) OnPointerDown(t.position);
            else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) OnPointerHold(t.position);
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) OnPointerUp(t.position);
        }
        // 3. Fallback for Standard Game View (Mouse)
        else
        {
            if (Input.GetMouseButtonDown(0)) OnPointerDown(Input.mousePosition);
            else if (Input.GetMouseButton(0)) OnPointerHold(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0)) OnPointerUp(Input.mousePosition);

            // Right-click to flag in Game View
            if (Input.GetMouseButtonDown(1))
            {
                Vector3Int cell = ScreenToCell(Input.mousePosition);
                manager.HandleToggleFlag(cell);
            }
        }

        // 4. Mouse Wheel Zoom (works in Game View & some Simulators)
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - scroll * scrollZoomSpeed * 0.2f, minZoom, maxZoom);
        }
    }

    private void OnPointerDown(Vector2 screenPos)
    {
        startScreenPos = screenPos;
        dragOriginWorld = ScreenToWorld(screenPos);
        pressTimer = 0f;
        isPointerDown = true;
        isDragging = false;
        longPressTriggered = false;
    }

    private void OnPointerHold(Vector2 screenPos)
    {
        if (!isPointerDown) return;

        // Check if player moved finger far enough to count as dragging the camera
        if (!isDragging && Vector2.Distance(screenPos, startScreenPos) > dragThresholdPixels)
        {
            isDragging = true;
        }

        if (isDragging)
        {
            // Move the camera by the world delta
            Vector3 currentWorld = ScreenToWorld(screenPos);
            Vector3 delta = dragOriginWorld - currentWorld;
            cam.transform.position += new Vector3(delta.x, delta.y, 0f);
            
            // Update origin after moving camera
            dragOriginWorld = ScreenToWorld(screenPos);
        }
        else if (!longPressTriggered)
        {
            // Finger is still -> count up timer for long press flag
            pressTimer += Time.deltaTime;
            if (pressTimer >= longPressTime)
            {
                longPressTriggered = true;
                Vector3Int cell = ScreenToCell(screenPos);
                manager.HandleToggleFlag(cell);
            }
        }
    }

    private void OnPointerUp(Vector2 screenPos)
    {
        if (!isPointerDown) return;

        // If we released quickly without dragging or long-pressing -> Reveal Tile!
        if (!isDragging && !longPressTriggered)
        {
            Vector3Int cell = ScreenToCell(screenPos);
            manager.HandleTap(cell);
        }

        isPointerDown = false;
        isDragging = false;
    }

    private void HandlePinchZoom()
    {
        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);

        Vector2 t0Prev = t0.position - t0.deltaPosition;
        Vector2 t1Prev = t1.position - t1.deltaPosition;

        float prevDist = Vector2.Distance(t0Prev, t1Prev);
        float currDist = Vector2.Distance(t0.position, t1.position);

        float diff = currDist - prevDist;
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - diff * pinchZoomSpeed, minZoom, maxZoom);
    }

    private Vector3 ScreenToWorld(Vector2 screenPos)
    {
        return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
    }

    private Vector3Int ScreenToCell(Vector2 screenPos)
    {
        return grid.WorldToCell(ScreenToWorld(screenPos));
    }
}