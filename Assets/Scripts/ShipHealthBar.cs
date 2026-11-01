using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ShipHealthBar : MonoBehaviour
{
    private Image healthFill;
    private Text healthText;
    private RectTransform panel;
    private Sprite shipSprite;
    private int maxHealth;
    private float damageFlashRemaining;
    private Vector2 panelPosition;

    public void Initialize(int maximumHealth)
    {
        maxHealth = Mathf.Max(1, maximumHealth);
        Texture2D shipTexture = Resources.Load<Texture2D>("NavLifeBar");
        if (shipTexture == null)
        {
            Debug.LogError("No se encontró Assets/Resources/NavLifeBar.png para mostrar la vida de la nave.", this);
            return;
        }

        shipSprite = Sprite.Create(
            shipTexture,
            new Rect(0f, 0f, shipTexture.width, shipTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        Canvas canvas = MinigameUI.CreateCanvas("ShipHealthCanvas", transform);
        canvas.sortingOrder = 45;
        panel = MinigameUI.CreateGroup("ShipHealthPanel", canvas.transform, Vector2.zero, new Vector2(220f, 205f));
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        panel.anchoredPosition = new Vector2(-28f, 20f);
        panelPosition = panel.anchoredPosition;

        Image shipOutline = MinigameUI.CreateImage(
            "ShipOutline",
            panel,
            new Color(0.35f, 0.42f, 0.5f, 0.5f),
            new Vector2(0f, 38f),
            new Vector2(185f, 165f));
        shipOutline.sprite = shipSprite;
        shipOutline.preserveAspect = true;

        healthFill = MinigameUI.CreateImage(
            "ShipHealthFill",
            panel,
            Color.white,
            new Vector2(0f, 38f),
            new Vector2(185f, 165f));
        healthFill.sprite = shipSprite;
        healthFill.preserveAspect = true;
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Vertical;
        healthFill.fillOrigin = (int)Image.OriginVertical.Bottom;

        healthText = MinigameUI.CreateText(
            "ShipHealthText",
            panel,
            "",
            22,
            new Vector2(0f, -80f),
            new Vector2(220f, 30f));
        healthText.fontStyle = FontStyle.Bold;
        healthText.color = Color.white;

        Refresh(NeedsRepair());
    }

    private void Update()
    {
        if (healthFill != null)
        {
            damageFlashRemaining = Mathf.Max(0f, damageFlashRemaining - Time.deltaTime);
            bool needsRepair = NeedsRepair();
            if (needsRepair)
            {
                panel.anchoredPosition = panelPosition + Vector2.right * (Mathf.Sin(Time.time * 14f) * 7f);
            }
            else
            {
                panel.anchoredPosition = panelPosition;
            }

            Refresh(needsRepair);
        }
    }

    public void PlayDamageFeedback()
    {
        if (healthFill == null)
        {
            return;
        }

        damageFlashRemaining = 0.25f;
        panel.DOKill();
        panel.DOPunchScale(new Vector3(0.08f, 0.08f, 0f), 0.25f, 8).SetLink(panel.gameObject);
    }

    private void Refresh(bool needsRepair)
    {
        float healthRatio = Mathf.Clamp01(ShipState.Health / maxHealth);
        healthFill.fillAmount = healthRatio;
        healthFill.color = Color.Lerp(GetHealthColor(needsRepair), Color.white, damageFlashRemaining / 0.25f);
        healthText.text = $"CASCO {Mathf.CeilToInt(ShipState.Health)}/{maxHealth}";
    }

    private static bool NeedsRepair()
    {
        float dangerThreshold = ShipState.Config.dangerThreshold;
        foreach (ShipSystems.SystemInfo system in ShipState.Config.systems)
        {
            if (ShipState.GetStability(system.name) < dangerThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private static Color GetHealthColor(bool needsRepair)
    {
        return needsRepair
            ? new Color(1f, 0.12f, 0.08f)
            : new Color(0.2f, 0.9f, 0.48f);
    }

    private void OnDestroy()
    {
        if (shipSprite != null)
        {
            Destroy(shipSprite);
        }
    }
}
