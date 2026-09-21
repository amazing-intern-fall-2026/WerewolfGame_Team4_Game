using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundScaler : MonoBehaviour
{
    private void Start()
    {
        ScaleBackground();
    }

    private void ScaleBackground()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // Reset scale so calculation is based on pure sprite size
        transform.localScale = Vector3.one;

        // Get sprite dimensions in world units
        float spriteWidth = sr.sprite.bounds.size.x;
        float spriteHeight = sr.sprite.bounds.size.y;

        // Get camera dimensions in world units
        float worldScreenHeight = cam.orthographicSize * 2f;
        float worldScreenWidth = worldScreenHeight / Screen.height * Screen.width;

        // Apply scale to fill screen exactly
        transform.localScale = new Vector3(
            worldScreenWidth / spriteWidth,
            worldScreenHeight / spriteHeight,
            1f
        );
    }
}