using UnityEngine;
using UnityEngine.UI;

namespace Ilumisoft.Minesweeping.UI
{
    public class TimeSpentText : MonoBehaviour
    {
        [SerializeField]
        Text text = null;

        Timer timer;

        private void Awake()
        {
            timer = FindAnyObjectByType<Timer>();
        }

        private void OnEnable()
        {
            if (timer != null && text != null)
            {
                text.text = "TIME: "+timer.GetTime();
            }
        }
    }
}