using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SimplePotionCauldron : MonoBehaviour
{
    [Header("Outputs")]
    [SerializeField] private PotionResult minorHeal = new PotionResult { potionName = "Minor Healing Potion", potionColor = Color.green };
    [SerializeField] private PotionResult strongHeal = new PotionResult { potionName = "Greater Healing Potion", potionColor = Color.cyan };
    [SerializeField] private PotionResult minorPoison = new PotionResult { potionName = "Weak Poison", potionColor = new Color(0.6f, 0.1f, 0.8f) };
    [SerializeField] private PotionResult lethalPoison = new PotionResult { potionName = "Lethal Poison", potionColor = Color.magenta };
    [SerializeField] private PotionResult ruinedSludge = new PotionResult { potionName = "Ruined Sludge", potionColor = Color.gray };

    [Header("Visual Feedback (Optional)")]
    [SerializeField] private SpriteRenderer liquidRenderer;

    private readonly List<IngredientData> pot = new List<IngredientData>();

    public bool CanAcceptIngredient() => pot.Count < 3;

    public void AddIngredient(IngredientData ingredient)
    {
        if (!CanAcceptIngredient()) return;

        pot.Add(ingredient);
        Debug.Log($"Dropped {ingredient.ingredientName} ({pot.Count}/3)");

        UpdateLiquidColor();

        if (pot.Count == 3)
        {
            Brew();
        }
    }

    private void UpdateLiquidColor()
    {
        if (liquidRenderer == null) return;

        int currentScore = 0;
        foreach (var item in pot)
        {
            currentScore += (item.type == IngredientType.Healing) ? item.potency : -item.potency;
        }

        if (currentScore > 0)
            liquidRenderer.color = Color.Lerp(Color.white, Color.green, 0.6f);
        else if (currentScore < 0)
            liquidRenderer.color = Color.Lerp(Color.white, new Color(0.6f, 0.1f, 0.8f), 0.6f);
        else
            liquidRenderer.color = Color.gray;
    }

    private void Brew()
    {
        int score = 0;
        foreach (var item in pot)
        {
            if (item.type == IngredientType.Healing) score += item.potency;
            else if (item.type == IngredientType.Poison) score -= item.potency;
        }

        PotionResult result = EvaluateScore(score);
        Debug.Log($"Brew Complete: {result.potionName} (Final Score: {score})");

        if (liquidRenderer != null)
        {
            liquidRenderer.color = result.potionColor;
        }
    }

    private PotionResult EvaluateScore(int score)
    {
        if (score >= 5) return strongHeal;
        if (score > 0) return minorHeal;
        if (score <= -5) return lethalPoison;
        if (score < 0) return minorPoison;
        return ruinedSludge;
    }
}