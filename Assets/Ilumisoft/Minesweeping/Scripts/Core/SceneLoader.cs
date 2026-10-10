using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Ilumisoft.Minesweeping
{
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField]
        PlayableDirector playableDirector = null;

        public void LoadScene(string name)
        {
            StopAllCoroutines();
            StartCoroutine(LoadSceneCoroutine(name));
        }

        IEnumerator LoadSceneCoroutine(string name)
        {
            if (playableDirector != null)
            {
                playableDirector.Stop();
                playableDirector.time = 0;
                playableDirector.Evaluate();
                playableDirector.Play();

                yield return new WaitForSecondsRealtime((float)playableDirector.duration);
            }

            SceneManager.LoadScene(name);
        }
    }
}