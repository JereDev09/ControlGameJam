using UnityEngine;
using UnityEngine.UI;

// Reparar el radar: mueve la perilla hasta que la señal deje de ser ruido y se vea limpia,
// y mantenla ahí unos segundos. Falla si se acaba el tiempo.
public class RadarMinigame : Minigame
{
    private const int SignalBarCount = 30;
    private static readonly Color TunedFlashColor = new Color(0.4f, 1f, 0.5f);

    [Tooltip("Cuánto te puedes alejar de la frecuencia correcta (0 a 1)")]
    [SerializeField] private float tolerance = 0.03f;
    [SerializeField] private float holdTime = 2f;

    [Header("UI")]
    [SerializeField] private Image screen;
    [Tooltip("Barras de la pantalla, ancladas abajo para que crezcan hacia arriba")]
    [SerializeField] private Image[] signalBars;
    [Tooltip("Dibujo de la perilla: gira según el valor del slider")]
    [SerializeField] private RectTransform knob;
    [SerializeField] private Slider frequencySlider;
    [SerializeField] private Text frequencyText;
    [SerializeField] private RectTransform holdFill;

    private float targetFrequency;
    private float holdTimer;
    private bool wasTuned;

    private void Reset()
    {
        title = "Reparar radar";
        timeLimit = 25f;
    }

    public override string SystemName => ShipZones.Radar;

    protected override void CreateContent()
    {
        screen = MinigameUI.CreateImage("Screen", Window, new Color(0.05f, 0.15f, 0.08f), new Vector2(0f, 120f), new Vector2(720f, 220f));
        signalBars = new Image[SignalBarCount];
        float barWidth = 720f / SignalBarCount;
        for (int i = 0; i < SignalBarCount; i++)
        {
            Image bar = MinigameUI.CreateImage($"SignalBar{i + 1}", screen.transform, Color.green, Vector2.zero, new Vector2(barWidth - 4f, 30f));
            bar.rectTransform.anchorMin = new Vector2((i + 0.5f) / SignalBarCount, 0f);
            bar.rectTransform.anchorMax = bar.rectTransform.anchorMin;
            bar.rectTransform.pivot = new Vector2(0.5f, 0f);
            bar.rectTransform.anchoredPosition = Vector2.zero;
            signalBars[i] = bar;
        }

        frequencyText = MinigameUI.CreateText("FrequencyText", Window, "Frecuencia: 88.0 MHz", 28, new Vector2(0f, -30f), new Vector2(400f, 40f));
        knob = MinigameUI.CreateImage("Knob", Window, new Color(0.35f, 0.38f, 0.45f), new Vector2(-400f, -85f), new Vector2(70f, 70f)).rectTransform;
        MinigameUI.CreateImage("KnobMarker", knob, Color.white, new Vector2(0f, 22f), new Vector2(8f, 26f));
        frequencySlider = MinigameUI.CreateSlider("FrequencySlider", Window, new Vector2(40f, -85f), new Vector2(620f, 40f));

        MinigameUI.CreateText("HoldLabel", Window, "SINTONIZADO", 22, new Vector2(0f, -155f), new Vector2(400f, 40f));
        holdFill = MinigameUI.CreateBar("HoldBar", Window, new Color(0.2f, 0.22f, 0.28f), new Color(0.2f, 0.8f, 0.4f), new Vector2(0f, -195f), new Vector2(500f, 25f));

        CreateInstructions("Mueve la perilla hasta que la señal se vea limpia y mantenla ahí");
    }

    protected override void StartMinigame()
    {
        targetFrequency = Random.Range(0.1f, 0.9f);
        holdTimer = 0f;
        wasTuned = false;
        frequencySlider.value = 0f;
        MinigameUI.SetBar(holdFill, 0f);
    }

    protected override void UpdateMinigame()
    {
        float distance = Mathf.Abs(frequencySlider.value - targetFrequency);

        // 1 = señal perfecta, 0 = solo ruido
        float signalQuality = Mathf.Clamp01(1f - distance / 0.25f);
        float screenHeight = screen.rectTransform.rect.height;

        for (int i = 0; i < signalBars.Length; i++)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f + i * 0.6f);
            float noise = Random.value;
            float height = Mathf.Lerp(noise, wave, signalQuality);

            RectTransform bar = signalBars[i].rectTransform;
            bar.sizeDelta = new Vector2(bar.sizeDelta.x, height * screenHeight);
            signalBars[i].color = Color.Lerp(Color.red, Color.green, signalQuality);
        }

        knob.localRotation = Quaternion.Euler(0f, 0f, 135f - frequencySlider.value * 270f);
        frequencyText.text = $"Frecuencia: {Mathf.Lerp(88f, 108f, frequencySlider.value):0.0} MHz";

        // Al encontrar la frecuencia, la pantalla da un destello
        bool tuned = distance <= tolerance;
        if (tuned && !wasTuned)
        {
            MinigameFX.Flash(screen, TunedFlashColor);
            MinigameFX.Punch(frequencyText.transform, 0.2f);
        }

        wasTuned = tuned;
        holdTimer = tuned ? holdTimer + Time.deltaTime : 0f;
        MinigameUI.SetBar(holdFill, holdTimer / holdTime);

        if (holdTimer >= holdTime)
        {
            Succeed();
        }
    }
}
