using UnityEngine;

[ExecuteAlways] // Updates position in Scene View automatically
public class LockToScreenRight : MonoBehaviour
{
    [Header("Positioning Settings")]
    [Tooltip("Distance in world units from the right edge of the screen")]
    public float rightPadding = 1.5f;

    [Tooltip("Vertical screen position: 0 = Bottom, 0.5 = Center, 1 = Top")]
    [Range(0f, 1f)]
    public float verticalScreenRatio = 0.25f;

    void Start()
    {
        UpdatePosition();
    }

    void Update()
    {
        // Keeps position accurate in Editor mode or if screen resizes
        if (!Application.isPlaying) 
        {
            UpdatePosition();
        }
    }

    public void UpdatePosition()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        float distanceZ = Mathf.Abs(mainCam.transform.position.z - transform.position.z);
        Vector3 rightEdgeWorld = mainCam.ViewportToWorldPoint(new Vector3(1f, verticalScreenRatio, distanceZ));

        transform.position = new Vector3(rightEdgeWorld.x - rightPadding, rightEdgeWorld.y, transform.position.z);
    }
}