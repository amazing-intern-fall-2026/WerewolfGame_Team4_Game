using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DraggableIngredient : MonoBehaviour
{
    public IngredientData ingredientData;

    private Vector3 originalPosition;
    private Camera mainCamera;
    private bool isDragging;
    private Vector3 dragOffset;

    private void Awake()
    {
        mainCamera = Camera.main;
        originalPosition = transform.position;
    }

    private void OnMouseDown()
    {
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        dragOffset = transform.position - mouseWorldPos;
        isDragging = true;
    }

    private void OnMouseDrag()
    {
        if (!isDragging) return;
        Vector3 targetPos = GetMouseWorldPosition() + dragOffset;
        targetPos.z = 0f;
        transform.position = targetPos;
    }

    private void OnMouseUp()
    {
        isDragging = false;
        CheckDropZone();
    }

    private void CheckDropZone()
    {
        // Disable own collider briefly so raycast hits the cauldron underneath
        Collider2D myCollider = GetComponent<Collider2D>();
        myCollider.enabled = false;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.zero);
        myCollider.enabled = true;

        if (hit.collider != null && hit.collider.TryGetComponent(out SimplePotionCauldron cauldron))
        {
            if (cauldron.CanAcceptIngredient())
            {
                cauldron.AddIngredient(ingredientData);
                gameObject.SetActive(false); // Consumed into the pot
                return;
            }
        }

        // Snap back if dropped anywhere else or if cauldron is full
        transform.position = originalPosition;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -mainCamera.transform.position.z;
        return mainCamera.ScreenToWorldPoint(mousePos);
    }
}