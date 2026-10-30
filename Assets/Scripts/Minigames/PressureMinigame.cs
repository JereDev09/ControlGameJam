using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Fuga de presión: hay que tocar las válvulas en orden antes de que se acabe el tiempo.
// Solo se enseña el paso actual: un marco del color al que hay que dejar la válvula
// (verde = abrirla, rojo = cerrarla) y su rueda amaga con girar hacia ese lado.
// Tocar la válvula equivocada hace subir la presión (resta tiempo).
public class PressureMinigame : Minigame
{
    private static readonly Color OpenColor = new Color(0.2f, 0.6f, 0.3f);
    private static readonly Color ClosedColor = new Color(0.6f, 0.2f, 0.2f);
    private static readonly Color OpenHighlightColor = new Color(0.4f, 1f, 0.5f);
    private static readonly Color CloseHighlightColor = new Color(1f, 0.35f, 0.3f);
    private static readonly Color SteamColor = new Color(0.8f, 0.9f, 1f);
    private static readonly Color DoneDotColor = new Color(0.3f, 0.9f, 0.4f);
    private static readonly Color PendingDotColor = new Color(0.3f, 0.32f, 0.38f);

    [SerializeField] private int stepCount = 5;
    [SerializeField] private float mistakePenalty = 3f;

    [Header("UI")]
    [Tooltip("Hay tantas válvulas como botones en esta lista")]
    [SerializeField] private Button[] valveButtons;
    [Tooltip("Rueda de cada válvula (gira al abrirla o cerrarla)")]
    [SerializeField] private RectTransform[] valveWheels;
    [SerializeField] private Text[] valveLabels;
    [Tooltip("Marco que se coloca detrás de la válvula del paso actual")]
    [SerializeField] private Image targetHighlight;
    [Tooltip("Punto desactivado que se copia una vez por paso para enseñar el progreso")]
    [SerializeField] private Image stepDotTemplate;
    [SerializeField] private RectTransform pressureFill;

    private bool[] valveOpen;
    private int[] stepValves;     // qué válvula hay que tocar en cada paso
    private bool[] stepOpens;     // true = hay que abrirla, false = cerrarla
    private Image[] stepDots;
    private int currentStep;
    private Image pressureFillImage;
    private Tween highlightPulse;
    private Tween wheelHint;

    private void Reset()
    {
        title = "Controlar fuga de presión";
        timeLimit = 20f;
    }

    public override string SystemName => ShipZones.Pressure;

    protected override void CreateContent()
    {
        const int valveCount = 5;
        Color wheelColor = new Color(0.85f, 0.85f, 0.9f);

        MinigameUI.CreateText("PressureLabel", Window, "PRESIÓN", 22, new Vector2(0f, 220f), new Vector2(300f, 40f));
        pressureFill = MinigameUI.CreateBar("PressureBar", Window, new Color(0.2f, 0.22f, 0.28f), new Color(0.9f, 0.5f, 0.1f), new Vector2(0f, 185f), new Vector2(700f, 30f));

        // El marco va antes que las válvulas para quedar por detrás
        targetHighlight = MinigameUI.CreateImage("TargetHighlight", Window, OpenHighlightColor, new Vector2(0f, 40f), new Vector2(172f, 172f));
        targetHighlight.raycastTarget = false;

        valveButtons = new Button[valveCount];
        valveWheels = new RectTransform[valveCount];
        valveLabels = new Text[valveCount];
        for (int i = 0; i < valveCount; i++)
        {
            Vector2 position = new Vector2((i - (valveCount - 1) / 2f) * 175f, 40f);
            valveButtons[i] = MinigameUI.CreateButton($"Valve{i + 1}", Window, "", ClosedColor, position, new Vector2(140f, 140f));

            RectTransform wheel = MinigameUI.CreateGroup("Wheel", valveButtons[i].transform, new Vector2(0f, 12f), new Vector2(80f, 80f));
            MinigameUI.CreateImage("SpokeHorizontal", wheel, wheelColor, Vector2.zero, new Vector2(80f, 12f));
            MinigameUI.CreateImage("SpokeVertical", wheel, wheelColor, Vector2.zero, new Vector2(12f, 80f));
            MinigameUI.CreateImage("Hub", wheel, wheelColor, Vector2.zero, new Vector2(28f, 28f));
            valveWheels[i] = wheel;

            valveLabels[i] = MinigameUI.CreateText("Label", valveButtons[i].transform, "CERRADA", 18, new Vector2(0f, -52f), new Vector2(140f, 30f));
        }

        // Progreso: un punto por paso, que se colocan solos en fila
        RectTransform progress = MinigameUI.CreateGroup("Progress", Window, new Vector2(0f, -150f), new Vector2(600f, 30f));
        HorizontalLayoutGroup layout = progress.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 14f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        stepDotTemplate = MinigameUI.CreateImage("StepDotTemplate", progress, PendingDotColor, Vector2.zero, new Vector2(22f, 22f));
        stepDotTemplate.gameObject.SetActive(false);

        CreateInstructions("Gira la válvula marcada hasta que tenga el color de su marco");
    }

    protected override void SetupMinigame()
    {
        pressureFillImage = pressureFill.GetComponent<Image>();

        for (int i = 0; i < valveButtons.Length; i++)
        {
            int index = i; // copia para usarla dentro del clic del botón
            valveButtons[i].onClick.AddListener(() => PressValve(index));
        }
    }

    protected override void StartMinigame()
    {
        currentStep = 0;
        valveOpen = new bool[valveButtons.Length];
        for (int i = 0; i < valveOpen.Length; i++)
        {
            valveOpen[i] = Random.value < 0.5f;

            valveButtons[i].transform.DOKill(true);
            valveWheels[i].DOKill();
            valveWheels[i].localRotation = Quaternion.Euler(0f, 0f, GetWheelAngle(i));
            RefreshValve(i);
        }

        CreateSteps();

        stepDots = new Image[stepCount];
        for (int i = 0; i < stepCount; i++)
        {
            stepDots[i] = Spawn(stepDotTemplate);
        }

        MinigameUI.SetBar(pressureFill, 0f);
        ShowCurrentStep();
    }

    // Inventa la lista de pasos simulando cómo van quedando las válvulas
    private void CreateSteps()
    {
        stepValves = new int[stepCount];
        stepOpens = new bool[stepCount];
        bool[] simulated = (bool[])valveOpen.Clone();
        int previousValve = -1;

        for (int step = 0; step < stepCount; step++)
        {
            int valve;
            do
            {
                valve = Random.Range(0, valveOpen.Length);
            }
            while (valve == previousValve);

            previousValve = valve;
            simulated[valve] = !simulated[valve];
            stepValves[step] = valve;
            stepOpens[step] = simulated[valve];
        }
    }

    protected override void UpdateMinigame()
    {
        // La presión sube a medida que se acaba el tiempo
        if (timeLimit > 0f)
        {
            float pressure = 1f - TimeLeft / timeLimit;
            MinigameUI.SetBar(pressureFill, pressure);
            pressureFillImage.color = Color.Lerp(new Color(0.9f, 0.5f, 0.1f), Color.red, pressure);
        }
    }

    // Marca la válvula del paso actual y pinta los puntos de progreso
    private void ShowCurrentStep()
    {
        highlightPulse?.Kill();
        wheelHint?.Kill();

        for (int i = 0; i < stepDots.Length; i++)
        {
            stepDots[i].color = i < currentStep ? DoneDotColor : (i == currentStep ? Color.white : PendingDotColor);
        }

        if (currentStep >= stepCount)
        {
            targetHighlight.gameObject.SetActive(false);
            return;
        }

        int valve = stepValves[currentStep];
        bool mustOpen = stepOpens[currentStep];

        // Marco del color al que hay que dejar la válvula, latiendo
        targetHighlight.gameObject.SetActive(true);
        targetHighlight.rectTransform.anchoredPosition = ((RectTransform)valveButtons[valve].transform).anchoredPosition;
        targetHighlight.color = mustOpen ? OpenHighlightColor : CloseHighlightColor;
        targetHighlight.transform.localScale = Vector3.one;
        highlightPulse = MinigameFX.Pulse(targetHighlight.transform, 1.1f, 0.4f);

        // La rueda amaga con girar hacia donde hay que girarla (abrir = izquierda, cerrar = derecha)
        RectTransform wheel = valveWheels[valve];
        float hintAngle = GetWheelAngle(valve) + (mustOpen ? 25f : -25f);
        wheelHint = wheel.DOLocalRotate(new Vector3(0f, 0f, hintAngle), 0.35f)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(wheel.gameObject);
    }

    private void PressValve(int valve)
    {
        Button button = valveButtons[valve];

        if (currentStep >= stepCount || valve != stepValves[currentStep])
        {
            MinigameFX.Shake((RectTransform)button.transform);
            MinigameFX.Flash(button.image, Color.red);
            RemoveTime(mistakePenalty);
            return;
        }

        wheelHint?.Kill();
        valveOpen[valve] = !valveOpen[valve];
        currentStep++;

        // La rueda da media vuelta, la válvula cambia de color y suelta un poco de "vapor"
        valveWheels[valve].DOLocalRotate(new Vector3(0f, 0f, GetWheelAngle(valve)), 0.4f, RotateMode.FastBeyond360).SetEase(Ease.OutBack);
        MinigameFX.Punch(button.transform);
        MinigameFX.Burst(Window, ((RectTransform)button.transform).anchoredPosition, SteamColor, 10, 90f);
        MinigameFX.Punch(stepDots[currentStep - 1].transform, 0.5f);
        RefreshValve(valve);
        ShowCurrentStep();

        if (currentStep == stepCount)
        {
            Succeed();
        }
    }

    private void RefreshValve(int valve)
    {
        valveButtons[valve].image.DOKill();
        valveButtons[valve].image.DOColor(valveOpen[valve] ? OpenColor : ClosedColor, 0.2f);
        valveLabels[valve].text = valveOpen[valve] ? "ABIERTA" : "CERRADA";
    }

    // Abierta = rueda girada media vuelta, cerrada = sin girar
    private float GetWheelAngle(int valve)
    {
        return valveOpen[valve] ? 180f : 0f;
    }
}
