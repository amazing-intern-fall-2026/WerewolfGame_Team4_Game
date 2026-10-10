using System.Collections.Generic;
using UnityEngine;

namespace Ilumisoft.Minesweeping
{
    public static class TileGridExtensions
    {
        static readonly List<Vector2Int> neighborOffsets = new List<Vector2Int>()
        {
            new Vector2Int(0,1),
            new Vector2Int(1,1),
            new Vector2Int(1,0),
            new Vector2Int(1,-1),
            new Vector2Int(0,-1),
            new Vector2Int(-1,-1),
            new Vector2Int(-1,0),
            new Vector2Int(-1,1),
        };

        public static List<Tile> GetNeighbors(this TileGrid grid, Tile tile)
        {
            if(grid.TryGetGridPosition(tile, out var gridPos))
            {
                return GetNeighbors(grid, gridPos.x, gridPos.y);
            }

            return new List<Tile>();
        }

        public static List<Tile> GetNeighbors(this TileGrid grid, int x, int y)
        {
            var result = new List<Tile>();

            foreach (var offset in neighborOffsets)
            {
                if (grid.TryGetTile(new Vector2Int(x,y) + offset, out var neighbor))
                {
                    result.Add(neighbor);
                }
            }

            return result;
        }

        public static int GetNumberOfSurroundingBombs(this TileGrid grid, Tile tile)
        {
            int result = 0;

            var neighbors = GetNeighbors(grid, tile);

            foreach (var neighbor in neighbors)
            {
                if (neighbor.CompareTag(Bomb.Tag))
                {
                    result++;
                }
            }

            return result;
        }
    }
}