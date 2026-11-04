using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIIngredientButton : MonoBehaviour
{
    [SerializeField] private IngredientData ingredientData;
    [SerializeField] private Image iconImage;
    [SerializeField] private SimplePotionCauldron cauldron;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);

        if (cauldron == null)
            cauldron = Object.FindFirstObjectByType<SimplePotionCauldron>();

        if (iconImage == null)
        {
            Transform iconChild = transform.Find("Icon");
            if (iconChild != null)
                iconImage = iconChild.GetComponent<Image>();
        }
    }

    private void OnClick()
    {
        if (cauldron == null || !cauldron.CanAcceptIngredient()) return;

        Sprite spriteToUse = null;

        if (iconImage != null && iconImage.sprite != null)
            spriteToUse = iconImage.sprite;
        else if (ingredientData != null && ingredientData.icon != null)
            spriteToUse = ingredientData.icon;

        if (spriteToUse == null)
            Debug.LogError($"[Potion] No sprite assigned to {gameObject.name}!");

        cauldron.SpawnIngredientAbove(ingredientData, spriteToUse);
    }
}