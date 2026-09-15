using UnityEngine;

public class ShieldController : MonoBehaviour
{
    [SerializeField] private Camera mainCam;
    [SerializeField] private float rotationSpeed = 25f;

    void Start()
    {
        if (mainCam == null) mainCam = Camera.main;
    }

    void Update()
    {
        // Works for both touch and mouse drag
        if (Input.GetMouseButton(0))
        {
            Vector3 touchWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 direction = (touchWorldPos - transform.position).normalized;

            // Calculate target angle in degrees
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

            // Smooth rotation toward target
            Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}