using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace Ilumisoft.Minesweeping
{
    public static class MessageSystem
    {
        public static void Send<T>(UnityAction<T> action)
        {
            var listeners = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<T>();

            foreach (T listener in listeners)
            {
                action?.Invoke(listener);
            }
        }
    }
}