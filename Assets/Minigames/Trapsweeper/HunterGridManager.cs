using System.Collections.Generic;
using UnityEngine;

public class HunterGridManager : MonoBehaviour
{
    [SerializeField] private int width = 9;
    [SerializeField] private int height = 9;
    [SerializeField] private int trapCount = 10;
    [SerializeField] private HunterTrapCell cellPrefab;
    [SerializeField] private Transform gridParent;

    private HunterTrapCell[,] board;
    private bool firstClick = true;

    void Start()
    {
        GenerateEmptyBoard();
    }

    private void GenerateEmptyBoard()
    {
        board = new HunterTrapCell[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var cell = Instantiate(cellPrefab, gridParent);
                cell.Init(x, y, this);
                board[x, y] = cell;
            }
        }
    }

    public void OnCellClicked(int startX, int startY)
    {
        // Guarantee first tap never springs a trap
        if (firstClick)
        {
            DistributeTraps(startX, startY);
            CalculateAdjacentClues();
            firstClick = false;
        }

        HunterTrapCell cell = board[startX, startY];
        if (cell.isFlagged || cell.isRevealed) return;

        if (cell.isTrap)
        {
            cell.Reveal(triggered: true);
            GameOver();
            return;
        }

        FloodFillReveal(startX, startY);
    }

    private void DistributeTraps(int safeX, int safeY)
    {
        int placed = 0;
        while (placed < trapCount)
        {
            int rx = Random.Range(0, width);
            int ry = Random.Range(0, height);

            // Avoid the initial clicked cell
            if ((rx == safeX && ry == safeY) || board[rx, ry].isTrap)
                continue;

            board[rx, ry].isTrap = true;
            placed++;
        }
    }

    private void CalculateAdjacentClues()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (board[x, y].isTrap) continue;

                int count = 0;
                foreach (var neighbor in GetNeighbors(x, y))
                {
                    if (neighbor.isTrap) count++;
                }
                board[x, y].adjacentTraps = count;
            }
        }
    }

    private void FloodFillReveal(int x, int y)
    {
        HunterTrapCell cell = board[x, y];
        if (cell.isRevealed || cell.isFlagged) return;

        cell.Reveal();

        // If no traps are adjacent, safely auto-clear surrounding brush
        if (cell.adjacentTraps == 0)
        {
            foreach (var neighbor in GetNeighbors(x, y))
            {
                if (!neighbor.isTrap)
                {
                    FloodFillReveal(neighbor.x, neighbor.y);
                }
            }
        }
    }

    private List<HunterTrapCell> GetNeighbors(int cx, int cy)
    {
        List<HunterTrapCell> neighbors = new();
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = cx + dx;
                int ny = cy + dy;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    neighbors.Add(board[nx, ny]);
                }
            }
        }
        return neighbors;
    }

    private void GameOver()
    {
        // Trigger screen shake, sound, reveal remaining traps
        Debug.Log("Trap triggered! Game Over.");
    }
}