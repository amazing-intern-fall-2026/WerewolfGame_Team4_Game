using UnityEngine;

public class BasketController : MonoBehaviour
{
    private Camera mainCam;
    private float minX;
    private float maxX;

    void Start()
    {
        mainCam = Camera.main;
        
        // Calculate screen boundaries in world space
        float halfBasketWidth = GetComponent<SpriteRenderer>().bounds.extents.x;
        Vector3 leftEdge = mainCam.ViewportToWorldPoint(new Vector3(0, 0, 0));
        Vector3 rightEdge = mainCam.ViewportToWorldPoint(new Vector3(1, 0, 0));

        minX = leftEdge.x + halfBasketWidth;
        maxX = rightEdge.x - halfBasketWidth;
    }

    void Update()
    {
        if (Input.touchCount > 0 || Input.GetMouseButton(0))
        {
            Vector3 touchPos = Input.touchCount > 0 
                ? (Vector3)Input.GetTouch(0).position 
                : Input.mousePosition;

            Vector3 worldPos = mainCam.ScreenToWorldPoint(touchPos);
            float clampedX = Mathf.Clamp(worldPos.x, minX, maxX);
            
            transform.position = new Vector3(clampedX, transform.position.y, 0f);
        }
    }
}