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
            iconImage = transform.Find("Icon")?.GetComponent<Image>();
    }

    private void OnClick()
    {
        if (cauldron != null && cauldron.CanAcceptIngredient())
        {
            Sprite spriteToUse = iconImage != null ? iconImage.sprite : ingredientData.icon;
            cauldron.SpawnIngredientAbove(ingredientData, spriteToUse);
        }
    }
}