using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BasketPhysicsController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Camera mainCam;
    private float targetX;
    private float minX;
    private float maxX;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCam = Camera.main;
    }

    void Start()
    {
        float halfWidth = 0.8f; // Adjust to half-width of your basket sprite
        Vector3 leftEdge = mainCam.ViewportToWorldPoint(new Vector3(0, 0, 0));
        Vector3 rightEdge = mainCam.ViewportToWorldPoint(new Vector3(1, 0, 0));

        minX = leftEdge.x + halfWidth;
        maxX = rightEdge.x - halfWidth;
        targetX = transform.position.x;
    }

    void Update()
    {
        if (Input.touchCount > 0 || Input.GetMouseButton(0))
        {
            Vector3 inputPos = Input.touchCount > 0 
                ? (Vector3)Input.GetTouch(0).position 
                : Input.mousePosition;

            Vector3 worldPos = mainCam.ScreenToWorldPoint(inputPos);
            targetX = Mathf.Clamp(worldPos.x, minX, maxX);
        }
    }

    void FixedUpdate()
    {
        // MovePosition keeps Kinematic collision reactions smooth and physically accurate
        Vector2 nextPos = new Vector2(targetX, rb.position.y);
        rb.MovePosition(nextPos);
    }
}