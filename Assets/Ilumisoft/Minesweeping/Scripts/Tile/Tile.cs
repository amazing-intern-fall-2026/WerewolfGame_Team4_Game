using UnityEngine;
using UnityEngine.Events;

namespace Ilumisoft.Minesweeping
{
    public class Tile : MonoBehaviour
    {
        public UnityAction<TileState> OnStateChanged { get; set; } = null;

        public TileState State { get; private set; }

        public virtual void Reveal()
        {
            State = TileState.Revealed;
            OnStateChanged?.Invoke(State);
        }
        public void Flag()
        {
            State = TileState.Flagged;
            OnStateChanged?.Invoke(State);
        }

        public void Unflag()
        {
            State = TileState.Hidden;
            OnStateChanged?.Invoke(State);
        }

        public void SwitchFlag()
        {
            if (State != TileState.Revealed)
            {
                if (State == TileState.Flagged)
                {
                    Unflag();
                }
                else
                {
                    Flag();
                }
            }
        }
    }
}