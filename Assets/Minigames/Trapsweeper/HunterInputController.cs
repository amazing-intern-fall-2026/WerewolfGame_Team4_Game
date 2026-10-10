using UnityEngine;

public class HunterInputController : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private Grid grid;
    [SerializeField] private HunterTilemapManager manager;

    [Header("Touch Settings")]
    [SerializeField] private float longPressThreshold = 0.28f;
    [SerializeField] private float dragTolerance = 15f; // pixels

    private float touchStartTime;
    private Vector2 touchStartPos;
    private bool isHolding = false;
    private bool hasTriggeredLongPress = false;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        HandleTouchInput();
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            touchStartTime = Time.time;
            touchStartPos = touch.position;
            isHolding = true;
            hasTriggeredLongPress = false;
        }
        else if (touch.phase == TouchPhase.Moved)
        {
            // Cancel touch if user is scrolling/panning
            if (Vector2.Distance(touch.position, touchStartPos) > dragTolerance)
            {
                isHolding = false;
            }
        }
        else if (touch.phase == TouchPhase.Stationary && isHolding && !hasTriggeredLongPress)
        {
            if (Time.time - touchStartTime >= longPressThreshold)
            {
                hasTriggeredLongPress = true;
                Vector3Int cell = ScreenToCell(touch.position);
                manager.HandleToggleFlag(cell);
            }
        }
        else if (touch.phase == TouchPhase.Ended)
        {
            if (isHolding && !hasTriggeredLongPress)
            {
                Vector3Int cell = ScreenToCell(touch.position);
                manager.HandleTap(cell);
            }
            isHolding = false;
        }
    }

    private Vector3Int ScreenToCell(Vector2 screenPos)
    {
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        return grid.WorldToCell(worldPos);
    }
}