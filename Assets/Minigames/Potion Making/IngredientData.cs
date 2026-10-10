using UnityEngine;

public enum IngredientType
{
    Healing,
    Poison,
    Neutral
}

[CreateAssetMenu(fileName = "New Ingredient", menuName = "Potion/Simple Ingredient")]
public class IngredientData : ScriptableObject
{
    public string ingredientName;
    public Sprite icon;
    public IngredientType type;
    [Range(1, 3)] public int potency = 1;
}

[System.Serializable]
public struct PotionResult
{
    public string potionName;
    public string description;
    public Color potionColor;
}