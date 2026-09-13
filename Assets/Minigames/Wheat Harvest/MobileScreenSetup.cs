using UnityEngine;

public class MobileScreenSetup : MonoBehaviour
{
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform spawnPointTransform;
    [SerializeField] private float groundY = -2.5f;

    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
        AlignPositions();
    }

    public void AlignPositions()
    {
        if (cam == null) return;

        // Viewport coordinates: (0,0) is bottom-left, (1,1) is top-right
        // Player sits 20% into the screen from the left
        if (playerTransform != null)
        {
            Vector3 playerPos = cam.ViewportToWorldPoint(new Vector3(0.2f, 0, cam.nearClipPlane));
            playerTransform.position = new Vector3(playerPos.x, groundY, 0f);
        }

        // Spawner sits just outside the right edge (110% width)
        if (spawnPointTransform != null)
        {
            Vector3 spawnPos = cam.ViewportToWorldPoint(new Vector3(1.1f, 0, cam.nearClipPlane));
            spawnPointTransform.position = new Vector3(spawnPos.x, groundY, 0f);
        }
    }
}