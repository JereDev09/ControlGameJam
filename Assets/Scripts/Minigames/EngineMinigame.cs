using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Falla del motor: la temperatura sube y baja sola. Mueve la barra de refrigeración para
// mantener la aguja en la zona verde el tiempo suficiente. Si llega a un extremo, falla.
public class EngineMinigame : Minigame
{
    private static readonly Color SafeColor = new Color(0.2f, 0.55f, 0.3f);
    private static readonly Color SafeActiveColor = new Color(0.35f, 1f, 0.5f);

    [SerializeField] private float timeToStabilize = 6f;
    [SerializeField, Range(0f, 1f)] private float safeZoneMin = 0.4f;
    [SerializeField, Range(0f, 1f)] private float safeZoneMax = 0.6f;
    [SerializeField] private float temperatureSpeed = 0.3f;
    [SerializeField] private float heatChangeInterval = 1.5f;

    [Header("UI")]
    [SerializeField] private RectTransform temperatureBar;
    [SerializeField] private Image safeZone;
    [SerializeField] private Image needle;
    [SerializeField] private Slider coolingSlider;
    [SerializeField] private RectTransform progressFill;

    private Vector2 barPosition;
    private float temperature;
    private float heat;
    private float heatTimer;
    private float timeInZone;
    private bool wasInZone;

    // Valores por defecto al añadir el script en el Inspector
    private void Reset()
    {
        title = "Reparar motor";
    }

    protected override void CreateContent()
    {
        MinigameUI.CreateText("TemperatureLabel", Window, "TEMPERATURA", 26, new Vector2(0f, 185f), new Vector2(400f, 40f));
        temperatureBar = MinigameUI.CreateImage("TemperatureBar", Window, new Color(0.2f, 0.22f, 0.28f), new Vector2(0f, 130f), new Vector2(700f, 50f)).rectTransform;
        safeZone = MinigameUI.CreateImage("SafeZone", temperatureBar, SafeColor, Vector2.zero, Vector2.zero);
        needle = MinigameUI.CreateImage("Needle", temperatureBar, Color.white, Vector2.zero, new Vector2(8f, 80f));
        MinigameUI.CreateText("ColdLabel", Window, "FRÍO", 22, new Vector2(-410f, 130f), new Vector2(100f, 40f));
        MinigameUI.CreateText("HotLabel", Window, "CALIENTE", 22, new Vector2(420f, 130f), new Vector2(130f, 40f));

        MinigameUI.CreateText("CoolingLabel", Window, "REFRIGERACIÓN", 26, new Vector2(0f, 30f), new Vector2(400f, 40f));
        coolingSlider = MinigameUI.CreateSlider("CoolingSlider", Window, new Vector2(0f, -20f), new Vector2(600f, 40f));
        coolingSlider.value = 0.5f;

        MinigameUI.CreateText("ProgressLabel", Window, "ESTABILIZANDO", 22, new Vector2(0f, -110f), new Vector2(400f, 40f));
        progressFill = MinigameUI.CreateBar("ProgressBar", Window, new Color(0.2f, 0.22f, 0.28f), new Color(0.2f, 0.8f, 0.4f), new Vector2(0f, -150f), new Vector2(500f, 25f));

        CreateInstructions("Mueve la refrigeración para mantener la aguja en la zona verde");
        PlaceSafeZone();
        PlaceNeedle(0.5f);
    }

    protected override void SetupMinigame()
    {
        barPosition = temperatureBar.anchoredPosition;
    }

    protected override void StartMinigame()
    {
        temperature = 0.5f;
        heatTimer = 0f;
        timeInZone = 0f;
        wasInZone = false;

        coolingSlider.value = 0.5f;
        temperatureBar.anchoredPosition = barPosition;
        safeZone.color = SafeColor;
        MinigameUI.SetBar(progressFill, 0f);
        PlaceSafeZone();
        PlaceNeedle(temperature);
    }

    protected override void UpdateMinigame()
    {
        // Cada cierto tiempo el motor cambia lo mucho que calienta
        heatTimer -= Time.deltaTime;
        if (heatTimer <= 0f)
        {
            heat = Random.value;
            heatTimer = heatChangeInterval;
        }

        // Si el motor calienta más de lo que enfrías, la temperatura sube; si no, baja
        temperature += (heat - coolingSlider.value) * temperatureSpeed * Time.deltaTime;
        PlaceNeedle(temperature);

        if (temperature <= 0f || temperature >= 1f)
        {
            Fail();
            return;
        }

        bool inZone = temperature >= safeZoneMin && temperature <= safeZoneMax;

        // Cuanto más cerca de un extremo, más tiembla la barra y más roja se pone la aguja
        float danger = Mathf.Clamp01((Mathf.Abs(temperature - 0.5f) - 0.3f) / 0.2f);
        temperatureBar.anchoredPosition = barPosition + Random.insideUnitCircle * danger * 6f;
        needle.color = inZone ? SafeActiveColor : Color.Lerp(Color.white, Color.red, danger);

        // Al entrar o salir de la zona verde, la zona se ilumina o se apaga
        if (inZone != wasInZone)
        {
            safeZone.DOKill();
            safeZone.DOColor(inZone ? SafeActiveColor : SafeColor, 0.2f);
            if (inZone)
            {
                MinigameFX.Punch(safeZone.transform, 0.1f);
            }

            wasInZone = inZone;
        }

        if (inZone)
        {
            timeInZone += Time.deltaTime;
            MinigameUI.SetBar(progressFill, timeInZone / timeToStabilize);

            if (timeInZone >= timeToStabilize)
            {
                Succeed();
            }
        }
    }

    // La zona verde se coloca con anclas según safeZoneMin y safeZoneMax (0 = izquierda, 1 = derecha)
    private void PlaceSafeZone()
    {
        RectTransform zone = safeZone.rectTransform;
        zone.anchorMin = new Vector2(safeZoneMin, 0f);
        zone.anchorMax = new Vector2(safeZoneMax, 1f);
        zone.sizeDelta = Vector2.zero;
        zone.anchoredPosition = Vector2.zero;
    }

    private void PlaceNeedle(float value)
    {
        RectTransform rect = needle.rectTransform;
        rect.anchorMin = new Vector2(Mathf.Clamp01(value), 0.5f);
        rect.anchorMax = rect.anchorMin;
        rect.anchoredPosition = Vector2.zero;
    }
}
