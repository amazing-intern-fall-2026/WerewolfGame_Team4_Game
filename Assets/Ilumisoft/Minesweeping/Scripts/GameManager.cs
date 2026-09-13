using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ilumisoft.Minesweeping
{
    public class GameManager : MonoBehaviour, ITileClickListener
    {
        [SerializeField]
        GameObject levelCompleteUI = null;

        [SerializeField]
        GameObject gameOverUI = null;

        [SerializeField]
        Tile normalTilePrefab = null;

        [SerializeField]
        Tile bombTilePrefab = null;

        [SerializeField]
        TileGrid grid = null;

        [SerializeField]
        int bombCount = 5;

        GameObject tileContainer = null;

        List<Tile> tiles = new List<Tile>();

        private void Awake()
        {
            tileContainer = new GameObject("Tile Container");
        }

        void Start()
        {
            CreateBoard();
        }

        /// <summary>
        /// 
        /// </summary>
        void CreateBoard()
        {
            AddBombsToGrid();
            AddNormalTilesToGrid();
            AssignBombNumbers();
        }

        /// <summary>
        /// Adds the bomb tiles to the grid
        /// </summary>
        private void AddBombsToGrid()
        {
            // Make sure the number of bombs is not larger than the grid size
            bombCount = Mathf.Min(bombCount, grid.Width * grid.Height);

            for (int i = 0; i < bombCount; i++)
            {
                // Calculate a random position
                int x = Random.Range(0, grid.Width);
                int y = Random.Range(0, grid.Height);

                // Add a bomb if no tile has been assigned yet to the position
                if (grid.TryGetTile(x, y, out _) == false)
                {
                    AddTileToGrid(x, y, bombTilePrefab);
                }
                // Otherwise our random position is already in use and we need to get a new one
                else
                {
                    i--;
                }
            }
        }

        /// <summary>
        /// Fills all empty grid cells with normal tiles
        /// </summary>
        private void AddNormalTilesToGrid()
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    // If no (bomb) tile is already set, add a normal tile
                    if (grid.TryGetTile(x, y, out _) == false)
                    {
                        AddTileToGrid(x, y, normalTilePrefab);
                    }
                }
            }
        }

        /// <summary>
        /// Assigns to each tile the number of surrounding bombs
        /// </summary>
        private void AssignBombNumbers()
        {
            foreach (var tile in tiles)
            {
                if (tile.TryGetComponent<TileNumber>(out var tileNumber))
                {
                    tileNumber.SetNumberOfBombs(grid.GetNumberOfSurroundingBombs(tile));
                }
            }
        }

        /// <summary>
        /// Creates an instance of the given tile prefab and adds it to the given grid position
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="prefab"></param>
        void AddTileToGrid(int x, int y, Tile prefab)
        {
            var position = grid.GetWorldPosition(x, y);

            var tile = Instantiate(prefab, position, Quaternion.identity);
            tile.transform.SetParent(tileContainer.transform);
            grid.SetTile(x, y, tile);

            tiles.Add(tile);
        }

        public void OnTileClick(Tile tile)
        {
            if (tile.State == TileState.Hidden)
            {
                TileRevealer tileRevealer = new TileRevealer(grid);

                tileRevealer.Reveal(tile);

                if (tile.CompareTag(Bomb.Tag))
                {
                    GameOver(won: false);
                }
                else if(HasRevealedAllSafeTiles())
                {
                    GameOver(won: true);
                }
            }
        }

        bool HasRevealedAllSafeTiles()
        {
            foreach (var tile in tiles)
            {
                if (tile.CompareTag(Bomb.Tag) == false && tile.State != TileState.Revealed)
                {
                    return false;
                }
            }

            return true;
        }

        void GameOver(bool won)
        {
            StopAllCoroutines();
            StartCoroutine(GameOverCoroutine(won));
        }

        IEnumerator GameOverCoroutine(bool won)
        {
            GameObject uiElement = won ? levelCompleteUI : gameOverUI;

            yield return new WaitForSecondsRealtime(1.0f);

            uiElement.SetActive(true);
        }
    }
}