using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HunterTilemapManager : MonoBehaviour
{
    [Header("Tilemaps")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap coverTilemap;
    [SerializeField] private Tilemap markerTilemap;

    [Header("Tiles")]
    [SerializeField] private TileBase coverTile;
    [SerializeField] private TileBase clearedTile;
    [SerializeField] private TileBase flagTile;
    [SerializeField] private TileBase trapTile;
    [SerializeField] private TileBase[] numberTiles; // Indexes 0 to 7 represent numbers 1 to 8

    [Header("Board Settings")]
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 12;
    [SerializeField] private int totalTraps = 15;

    // Internal cell state tracking
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
        InitializeBoard();
    }

    public void InitializeBoard()
    {
        grid = new CellData[width, height];
        firstTap = true;
        isGameOver = false;

        groundTilemap.ClearAllTiles();
        coverTilemap.ClearAllTiles();
        markerTilemap.ClearAllTiles();

        // Populate visual cover and default ground underneath
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                coverTilemap.SetTile(pos, coverTile);
                groundTilemap.SetTile(pos, clearedTile);
            }
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

        if (grid[x, y].isFlagged)
        {
            markerTilemap.SetTile(cellPos, flagTile);
            Handheld.Vibrate(); // Subtle tactile bump on mobile
        }
        else
        {
            markerTilemap.SetTile(cellPos, null);
        }
    }

    private void PlantTraps(int safeX, int safeY)
    {
        int placed = 0;
        while (placed < totalTraps)
        {
            int rx = Random.Range(0, width);
            int ry = Random.Range(0, height);

            // Avoid spawning on or directly adjacent to the first click for comfortable starts
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

        // Remove the brush cover
        coverTilemap.SetTile(pos, null);

        // Update ground display with etched notches/numbers
        int clues = grid[x, y].adjacentTraps;
        if (clues > 0 && clues <= numberTiles.Length)
        {
            groundTilemap.SetTile(pos, numberTiles[clues - 1]);
        }
        else
        {
            groundTilemap.SetTile(pos, clearedTile);
            // Flood fill open safety zones
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

        // Reveal all forgotten traps
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

        Debug.Log($"*CLACK!* Hunter stepped right into a bear trap at ({hitX}, {hitY})!");
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
            Debug.Log("Tracked down all forgotten traps safely!");
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