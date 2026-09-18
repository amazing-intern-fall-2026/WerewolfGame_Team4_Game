using UnityEngine;

public class LoopingGround : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float groundWidth = 20f; // Width of your tilemap in units

    void Update()
    {
        // Move left continuously
        transform.position += Vector3.left * speed * Time.deltaTime;

        // When completely past the left threshold, wrap around to the right
        if (transform.position.x <= -groundWidth)
        {
            // Reposition behind the active tilemap
            transform.position += new Vector3(groundWidth * 2f, 0, 0);
        }
    }
}