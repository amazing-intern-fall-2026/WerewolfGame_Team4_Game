using UnityEngine;
using UnityEngine.UI;

namespace Ilumisoft.Minesweeping.UI
{
    public class Timer : MonoBehaviour
    {
        [SerializeField]
        Text text = null;

        float elapsedTime = 0.0f;

        void Update()
        {
            elapsedTime += Time.deltaTime;

            text.text = GetTime();
        }

        public string GetTime()
        {
            string minutes = Mathf.Floor(elapsedTime / 60).ToString("00");
            string seconds = (elapsedTime % 60).ToString("00");

            return minutes + ":" + seconds;
        }
    }
}