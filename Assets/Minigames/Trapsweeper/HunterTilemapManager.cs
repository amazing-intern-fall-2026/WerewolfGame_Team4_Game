using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HunterTilemapManager : MonoBehaviour
{
    public enum TrapType { None = 0, BearTrap = 1, SpikePit = 2 }
    public enum FlagState { None = 0, BearFlag = 1, SpikeFlag = 2 }

    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap coverTilemap;
    [SerializeField] private Tilemap markerTilemap;

    [Header("Tile Assets")]
    [SerializeField] private TileBase coverTile;
    [SerializeField] private TileBase clearedTile;
    [SerializeField] private TileBase flagTile;
    [SerializeField] private TileBase spikeFlagTile; // Optional: leave empty to auto-tint standard flagTile red!
    [SerializeField] private TileBase bearTrapTile;
    [SerializeField] private TileBase spikePitTile;  // Assign your new Spike Pit sprite tile here
    [SerializeField] private TileBase[] numberTiles; // Assign tiles 1 through 8 (or up to 16)

    [Header("Board Config")]
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 10;
    [SerializeField] private int bearTrapCount = 12;
    [SerializeField] private int spikePitCount = 3;

    [Header("Spike Pit Visuals")]
    [SerializeField] private Color spikeClueTint = new Color(1f, 0.55f, 0.2f, 1f); // Orange warning tint on numbers

    private struct CellData
    {
        public TrapType trapType;
        public bool isRevealed;
        public FlagState flagState;
        public int adjacentTrapSum;
        public bool touchesSpikePit;
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

        // Reset any tile colors from previous rounds
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                groundTilemap.SetTileFlags(pos, TileFlags.None);
                groundTilemap.SetColor(pos, Color.white);
                markerTilemap.SetTileFlags(pos, TileFlags.None);
                markerTilemap.SetColor(pos, Color.white);
            }
        }
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

        if (grid[x, y].flagState != FlagState.None || grid[x, y].isRevealed) return;

        if (firstTap)
        {
            PlantAllTraps(x, y);
            CalculateClues();
            firstTap = false;
        }

        if (grid[x, y].trapType != TrapType.None)
        {
            TriggerGameOver(x, y, grid[x, y].trapType);
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

        // Cycle: None -> BearFlag (1) -> SpikeFlag (2) -> None
        FlagState nextState = (FlagState)(((int)grid[x, y].flagState + 1) % 3);
        grid[x, y].flagState = nextState;

        markerTilemap.SetTileFlags(cellPos, TileFlags.None);

        switch (nextState)
        {
            case FlagState.None:
                markerTilemap.SetTile(cellPos, null);
                markerTilemap.SetColor(cellPos, Color.white);
                break;

            case FlagState.BearFlag:
                markerTilemap.SetTile(cellPos, flagTile);
                markerTilemap.SetColor(cellPos, Color.white);
                break;

            case FlagState.SpikeFlag:
                // Uses spikeFlagTile if assigned, otherwise reuses flagTile tinted bright red
                if (spikeFlagTile != null)
                {
                    markerTilemap.SetTile(cellPos, spikeFlagTile);
                    markerTilemap.SetColor(cellPos, Color.white);
                }
                else
                {
                    markerTilemap.SetTile(cellPos, flagTile);
                    markerTilemap.SetColor(cellPos, new Color(1f, 0.3f, 0.3f, 1f));
                }
                break;
        }
    }

    private void PlantAllTraps(int safeX, int safeY)
    {
        int availableCells = (width * height) - 9;
        int targetSpikes = Mathf.Min(spikePitCount, availableCells);
        int targetBears = Mathf.Min(bearTrapCount, availableCells - targetSpikes);

        PlantTrapType(safeX, safeY, targetSpikes, TrapType.SpikePit);
        PlantTrapType(safeX, safeY, targetBears, TrapType.BearTrap);
    }

    private void PlantTrapType(int safeX, int safeY, int countToPlace, TrapType type)
    {
        int placed = 0;
        while (placed < countToPlace)
        {
            int rx = Random.Range(0, width);
            int ry = Random.Range(0, height);

            // Keep 3x3 starting area trap-free
            if ((Mathf.Abs(rx - safeX) <= 1 && Mathf.Abs(ry - safeY) <= 1) || grid[rx, ry].trapType != TrapType.None)
                continue;

            grid[rx, ry].trapType = type;
            placed++;
        }
    }

    private void CalculateClues()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y].trapType != TrapType.None) continue;

                int sum = 0;
                bool nearSpike = false;

                foreach (var (nx, ny) in GetNeighbors(x, y))
                {
                    if (grid[nx, ny].trapType == TrapType.BearTrap)
                    {
                        sum += 1;
                    }
                    else if (grid[nx, ny].trapType == TrapType.SpikePit)
                    {
                        sum += 2; // Spike Pit counts as 2!
                        nearSpike = true;
                    }
                }

                grid[x, y].adjacentTrapSum = sum;
                grid[x, y].touchesSpikePit = nearSpike;
            }
        }
    }

    private void RevealCell(int x, int y)
    {
        if (!IsInBounds(x, y) || grid[x, y].isRevealed || grid[x, y].flagState != FlagState.None) return;

        grid[x, y].isRevealed = true;
        Vector3Int pos = new Vector3Int(x, y, 0);

        coverTilemap.SetTile(pos, null);

        int clues = grid[x, y].adjacentTrapSum;

        if (clues > 0)
        {
            if (numberTiles != null && numberTiles.Length > 0)
            {
                // Clamp index in case sum exceeds 8 (e.g., surrounded by multiple Spike Pits)
                int tileIndex = Mathf.Clamp(clues - 1, 0, numberTiles.Length - 1);
                if (numberTiles[tileIndex] != null)
                {
                    groundTilemap.SetTile(pos, numberTiles[tileIndex]);
                }
            }

            // Apply warning orange tint if touching at least one Spike Pit
            if (grid[x, y].touchesSpikePit)
            {
                groundTilemap.SetTileFlags(pos, TileFlags.None);
                groundTilemap.SetColor(pos, spikeClueTint);
            }
        }
        else
        {
            groundTilemap.SetTile(pos, clearedTile);
            foreach (var (nx, ny) in GetNeighbors(x, y))
            {
                if (grid[nx, ny].trapType == TrapType.None)
                {
                    RevealCell(nx, ny);
                }
            }
        }
    }

    private void TriggerGameOver(int hitX, int hitY, TrapType hitType)
    {
        isGameOver = true;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y].trapType != TrapType.None)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    coverTilemap.SetTile(pos, null);

                    if (grid[x, y].trapType == TrapType.SpikePit)
                    {
                        // Use spikePitTile if assigned, otherwise fallback to bearTrapTile tinted red
                        if (spikePitTile != null)
                        {
                            groundTilemap.SetTile(pos, spikePitTile);
                        }
                        else
                        {
                            groundTilemap.SetTile(pos, bearTrapTile);
                            groundTilemap.SetTileFlags(pos, TileFlags.None);
                            groundTilemap.SetColor(pos, Color.red);
                        }
                    }
                    else
                    {
                        groundTilemap.SetTile(pos, bearTrapTile);
                    }
                }
            }
        }

        if (hitType == TrapType.SpikePit)
            Debug.Log($"*CRASH!* Fell into a massive Spike Pit at ({hitX}, {hitY})! Game Over.");
        else
            Debug.Log($"*SNAP!* Stepped on a Bear Trap at ({hitX}, {hitY})! Game Over.");
    }

    private void CheckWinCondition()
    {
        int unrevealedSafeCells = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y].trapType == TrapType.None && !grid[x, y].isRevealed)
                    unrevealedSafeCells++;
            }
        }

        if (unrevealedSafeCells == 0)
        {
            isGameOver = true;
            Debug.Log("All Bear Traps and Spike Pits avoided! You Win!");
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