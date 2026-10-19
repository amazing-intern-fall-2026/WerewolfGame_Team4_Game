using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HunterTrapCell : MonoBehaviour
{
    public int x;
    public int y;
    public bool isTrap;
    public bool isRevealed;
    public bool isFlagged;
    public int adjacentTraps;

    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI clueText;
    [SerializeField] private GameObject flagIcon;
    [SerializeField] private GameObject trapGraphic;

    private HunterGridManager manager;

    public void Init(int posX, int posY, HunterGridManager gridManager)
    {
        x = posX;
        y = posY;
        manager = gridManager;
        isTrap = false;
        isRevealed = false;
        isFlagged = false;
        clueText.text = "";
        flagIcon.SetActive(false);
        trapGraphic.SetActive(false);
    }

    public void Reveal(bool triggered = false)
    {
        isRevealed = true;
        flagIcon.SetActive(false);

        if (isTrap)
        {
            trapGraphic.SetActive(true);
            background.color = Color.red; // Visual snap feedback
            return;
        }

        background.color = Color.gray; // Cleared ground
        if (adjacentTraps > 0)
        {
            clueText.text = adjacentTraps.ToString();
        }
    }

    public void ToggleFlag()
    {
        if (isRevealed) return;
        isFlagged = !isFlagged;
        flagIcon.SetActive(isFlagged);
    }
}