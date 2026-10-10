using UnityEngine;

namespace Ilumisoft.Minesweeping
{
    [RequireComponent(typeof(Tile))]
    public class Bomb : MonoBehaviour
    {
        public static readonly string Tag = "Bomb";

        private void Awake()
        {
            gameObject.tag = Tag;
        }
    }
}