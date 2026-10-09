using System.Collections;
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

    [Header("Spawn Settings")]
[SerializeField] private Transform dropPoint;
[SerializeField] private GameObject fallingIngredientPrefab;
[SerializeField] private GameObject finishedBottlePrefab;
[SerializeField] private Vector3 fallingItemScale = new Vector3(0.4f, 0.4f, 1f);
[SerializeField] private Vector3 potionScale = new Vector3(1.5f, 1.5f, 1f); // <-- ADD THIS LINE

    [Header("Visual Feedback (Optional)")]
    [SerializeField] private SpriteRenderer liquidRenderer;

    private readonly List<IngredientData> pot = new List<IngredientData>();
    private bool isBrewing = false;

    public bool CanAcceptIngredient() => pot.Count < 3 && !isBrewing;

    public void SpawnIngredientAbove(IngredientData data, Sprite customSprite = null)
    {
        if (!CanAcceptIngredient() || fallingIngredientPrefab == null) return;

        Vector3 spawnPos = dropPoint != null ? dropPoint.position : transform.position + Vector3.up * 3f;
        spawnPos.z = 0f; // Keep on 2D plane

        GameObject spawned = Instantiate(fallingIngredientPrefab, spawnPos, Quaternion.identity);
        spawned.transform.localScale = fallingItemScale;

        SpriteRenderer sr = spawned.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = (customSprite != null) ? customSprite : data.icon;
            sr.sortingOrder = 10; // Draw in front
        }

        FallingIngredient fi = spawned.GetComponent<FallingIngredient>();
        if (fi != null)
        {
            fi.data = data;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out FallingIngredient item))
        {
            AddIngredient(item.data);
            Destroy(other.gameObject);
        }
    }

    public void AddIngredient(IngredientData ingredient)
    {
        if (pot.Count >= 3) return;

        pot.Add(ingredient);
        Debug.Log($"Added {ingredient.ingredientName} ({pot.Count}/3)");

        UpdateLiquidColor();

        if (pot.Count == 3)
        {
            StartCoroutine(BrewRoutine());
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

    private IEnumerator BrewRoutine()
    {
        isBrewing = true;
        yield return new WaitForSeconds(0.4f);

        int score = 0;
        foreach (var item in pot)
        {
            if (item.type == IngredientType.Healing) score += item.potency;
            else if (item.type == IngredientType.Poison) score -= item.potency;
        }

        PotionResult result = EvaluateScore(score);
        Debug.Log($"Brewed: {result.potionName} (Score: {score})");

        PopOutFinishedPotion(result);

        pot.Clear();
        isBrewing = false;
    }

    private void PopOutFinishedPotion(PotionResult result)
{
    if (finishedBottlePrefab == null) return;

    Vector3 popPos = new Vector3(transform.position.x, transform.position.y + 0.5f, 0f);
    GameObject bottle = Instantiate(finishedBottlePrefab, popPos, Quaternion.identity);
    bottle.transform.localScale = potionScale;

    // Despawn bottle after 3 seconds once it falls off-screen
    Destroy(bottle, 3f);

    SpriteRenderer sr = bottle.GetComponentInChildren<SpriteRenderer>();
    if (sr != null)
    {
        if (result.potionSprite != null)
        {
            sr.sprite = result.potionSprite;
        }
        else
        {
            Debug.LogError($"[Cauldron] Loại thuốc '{result.potionName}' chưa được gán Potion Sprite trong Inspector!");
        }

        sr.sortingOrder = 15;
    }

    Rigidbody2D rb = bottle.GetComponent<Rigidbody2D>();
    if (rb != null)
    {
        rb.linearVelocity = new Vector2(Random.Range(-1.5f, 1.5f), 4f);
        rb.AddTorque(Random.Range(-25f, 25f));
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