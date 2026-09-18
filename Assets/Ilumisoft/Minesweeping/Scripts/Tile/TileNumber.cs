using UnityEngine;
using UnityEngine.UI;

namespace Ilumisoft.Minesweeping
{
    [RequireComponent(typeof(Tile))]
    public class TileNumber : MonoBehaviour
    {
        [SerializeField]
        Text text = null;

        public void SetNumberOfBombs(int count)
        {
            if (text != null)
            {
                this.text.text = count == 0 ? "" : count.ToString();
            }
        }
    }
}