using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class ResponsiveGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int columns = 3;
    [SerializeField] private float cellAspectRatio = 1.0f; // 1.0 for square items

    private GridLayoutGroup grid;
    private RectTransform rectTransform;

    void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    void Start() => UpdateCellSize();
    void OnRectTransformDimensionsChange() => UpdateCellSize();

    public void UpdateCellSize()
    {
        if (grid == null || rectTransform == null) return;

        // Calculate available width inside the panel padding and spacing
        float usableWidth = rectTransform.rect.width 
                            - grid.padding.left 
                            - grid.padding.right 
                            - (grid.spacing.x * (columns - 1));

        float cellWidth = Mathf.Max(0, usableWidth / columns);
        float cellHeight = cellWidth / cellAspectRatio;

        grid.cellSize = new Vector2(cellWidth, cellHeight);
    }
}