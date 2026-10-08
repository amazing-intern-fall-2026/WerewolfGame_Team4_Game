using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class IngredientButton : MonoBehaviour
{
    [SerializeField] private IngredientData ingredientData;
    [SerializeField] private SimplePotionCauldron cauldron;

    private void Start()
    {
        if (cauldron == null)
        {
            cauldron = Object.FindFirstObjectByType<SimplePotionCauldron>();
        }
    }

    private void OnMouseDown()
    {
        if (cauldron != null && cauldron.CanAcceptIngredient())
        {
            cauldron.AddIngredient(ingredientData);
        }
    }
}