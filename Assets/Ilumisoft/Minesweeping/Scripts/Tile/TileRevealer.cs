namespace Ilumisoft.Minesweeping
{
    public class TileRevealer
    {
        TileGrid grid;

        public TileRevealer(TileGrid grid)
        {
            this.grid = grid;
        }

        public void Reveal(Tile tile)
        {
            if (TryReveal(tile))
            {
                if (CanAutoRevealNeighbors(tile))
                {
                    RevealNeighbors(tile);
                }
            }
        }

        bool TryReveal(Tile tile)
        {
            if (tile.State == TileState.Hidden)
            {
                tile.Reveal();

                return true;
            }

            return false;
        }

        void RevealNeighbors(Tile tile)
        {
            var neighbors = grid.GetNeighbors(tile);

            foreach (var neighbor in neighbors)
            {
                Reveal(neighbor);
            }
        }

        bool CanAutoRevealNeighbors(Tile tile)
        {
            return IsNoBomb(tile) && IsNotSurroundedByAnyBomb(tile);
        }

        bool IsNoBomb(Tile tile)
        {
            return tile.CompareTag(Bomb.Tag) == false;
        }

        bool IsNotSurroundedByAnyBomb(Tile tile)
        {
            return grid.GetNumberOfSurroundingBombs(tile) == 0;
        }
    }
}