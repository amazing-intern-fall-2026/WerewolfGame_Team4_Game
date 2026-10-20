using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HunterTilemapManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap coverTilemap;
    [SerializeField] private Tilemap markerTilemap;

    [Header("Tile Assets")]
    [SerializeField] private TileBase coverTile;
    [SerializeField] private TileBase clearedTile;
    [SerializeField] private TileBase flagTile;
    [SerializeField] private TileBase trapTile;
    [SerializeField] private TileBase[] numberTiles; // 8 tiles (1 to 8)

    [Header("Board Config")]
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 10;
    [SerializeField] private int totalTraps = 15;

    private struct CellData
    {
        public bool isTrap;
        public bool isRevealed;
        public bool isFlagged;
        public int adjacentTraps;
    }

    private CellData[,] grid;
    private bool firstTap = true;
    private bool isGameOver = false;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();

        InitializeBoard();
        CenterEverything();
    }

    public void InitializeBoard()
    {
        grid = new CellData[width, height];
        firstTap = true;
        isGameOver = false;

        // Batch-fill the entire board in 1 call instead of hundreds of SetTile calls
        BoundsInt bounds = new BoundsInt(0, 0, 0, width, height, 1);
        int totalCells = width * height;

        TileBase[] coverBlock = new TileBase[totalCells];
        TileBase[] groundBlock = new TileBase[totalCells];
        TileBase[] emptyBlock = new TileBase[totalCells];

        for (int i = 0; i < totalCells; i++)
        {
            coverBlock[i] = coverTile;
            groundBlock[i] = clearedTile;
            emptyBlock[i] = null;
        }

        groundTilemap.SetTilesBlock(bounds, groundBlock);
        coverTilemap.SetTilesBlock(bounds, coverBlock);
        markerTilemap.SetTilesBlock(bounds, emptyBlock);
    }

    private void CenterEverything()
    {
        if (groundTilemap != null && groundTilemap.layoutGrid != null)
        {
            Transform gridTransform = groundTilemap.layoutGrid.transform;
            gridTransform.position = new Vector3(-width * 0.5f, -height * 0.5f, 0f);
        }

        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 0f, -10f);

            float padding = 1.5f;
            float verticalSize = (height * 0.5f) + padding;
            float horizontalSize = ((width * 0.5f) + padding) / cam.aspect;
            cam.orthographicSize = Mathf.Max(verticalSize, horizontalSize);
        }
    }

    public void HandleTap(Vector3Int cellPos)
    {
        if (isGameOver || !IsInBounds(cellPos.x, cellPos.y)) return;

        int x = cellPos.x;
        int y = cellPos.y;

        if (grid[x, y].isFlagged || grid[x, y].isRevealed) return;

        if (firstTap)
        {
            PlantTraps(x, y);
            CalculateClues();
            firstTap = false;
        }

        if (grid[x, y].isTrap)
        {
            TriggerGameOver(x, y);
            return;
        }

        RevealCell(x, y);
        CheckWinCondition();
    }

    public void HandleToggleFlag(Vector3Int cellPos)
    {
        if (isGameOver || !IsInBounds(cellPos.x, cellPos.y)) return;

        int x = cellPos.x;
        int y = cellPos.y;

        if (grid[x, y].isRevealed) return;

        grid[x, y].isFlagged = !grid[x, y].isFlagged;
        markerTilemap.SetTile(cellPos, grid[x, y].isFlagged ? flagTile : null);
    }

    private void PlantTraps(int safeX, int safeY)
    {
        int placed = 0;
        int maxPossibleTraps = Mathf.Min(totalTraps, (width * height) - 9);

        while (placed < maxPossibleTraps)
        {
            int rx = Random.Range(0, width);
            int ry = Random.Range(0, height);

            if ((Mathf.Abs(rx - safeX) <= 1 && Mathf.Abs(ry - safeY) <= 1) || grid[rx, ry].isTrap)
                continue;

            grid[rx, ry].isTrap = true;
            placed++;
        }
    }

    private void CalculateClues()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y].isTrap) continue;

                int count = 0;
                foreach (var (nx, ny) in GetNeighbors(x, y))
                {
                    if (grid[nx, ny].isTrap) count++;
                }
                grid[x, y].adjacentTraps = count;
            }
        }
    }

    private void RevealCell(int x, int y)
    {
        if (!IsInBounds(x, y) || grid[x, y].isRevealed || grid[x, y].isFlagged) return;

        grid[x, y].isRevealed = true;
        Vector3Int pos = new Vector3Int(x, y, 0);

        coverTilemap.SetTile(pos, null);

        int clues = grid[x, y].adjacentTraps;

        if (clues > 0)
        {
            if (numberTiles != null && clues <= numberTiles.Length && numberTiles[clues - 1] != null)
            {
                groundTilemap.SetTile(pos, numberTiles[clues - 1]);
            }
        }
        else
        {
            groundTilemap.SetTile(pos, clearedTile);
            foreach (var (nx, ny) in GetNeighbors(x, y))
            {
                if (!grid[nx, ny].isTrap)
                {
                    RevealCell(nx, ny);
                }
            }
        }
    }

    private void TriggerGameOver(int hitX, int hitY)
    {
        isGameOver = true;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y].isTrap)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    coverTilemap.SetTile(pos, null);
                    groundTilemap.SetTile(pos, trapTile);
                }
            }
        }
        Debug.Log($"*SNAP!* Stepped on a trap at ({hitX}, {hitY})! Game Over.");
    }

    private void CheckWinCondition()
    {
        int unrevealedSafeCells = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!grid[x, y].isTrap && !grid[x, y].isRevealed)
                    unrevealedSafeCells++;
            }
        }

        if (unrevealedSafeCells == 0)
        {
            isGameOver = true;
            Debug.Log("All traps found! You Win!");
        }
    }

    private IEnumerable<(int, int)> GetNeighbors(int cx, int cy)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = cx + dx;
                int ny = cy + dy;
                if (IsInBounds(nx, ny))
                    yield return (nx, ny);
            }
        }
    }

    private bool IsInBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;
}