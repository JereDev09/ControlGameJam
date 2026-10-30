using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Fuga de presión: abre o cierra las válvulas en el orden indicado antes de que se acabe
// el tiempo. Tocar la válvula equivocada hace subir la presión (resta tiempo).
public class PressureMinigame : Minigame
{
    private static readonly Color OpenColor = new Color(0.2f, 0.6f, 0.3f);
    private static readonly Color ClosedColor = new Color(0.6f, 0.2f, 0.2f);
    private static readonly Color SteamColor = new Color(0.8f, 0.9f, 1f);

    [SerializeField] private int stepCount = 5;
    [SerializeField] private float mistakePenalty = 3f;

    [Header("UI")]
    [Tooltip("Hay tantas válvulas como botones en esta lista")]
    [SerializeField] private Button[] valveButtons;
    [Tooltip("Rueda de cada válvula (gira al abrirla o cerrarla)")]
    [SerializeField] private RectTransform[] valveWheels;
    [SerializeField] private Text[] valveLabels;
    [SerializeField] private Text stepsText;
    [SerializeField] private RectTransform pressureFill;

    private bool[] valveOpen;
    private int[] stepValves;     // qué válvula hay que tocar en cada paso
    private bool[] stepOpens;     // true = hay que abrirla, false = cerrarla
    private int currentStep;
    private Image pressureFillImage;

    private void Reset()
    {
        title = "Controlar fuga de presión";
        timeLimit = 20f;
    }

    protected override void CreateContent()
    {
        const int valveCount = 5;
        Color wheelColor = new Color(0.85f, 0.85f, 0.9f);

        MinigameUI.CreateText("PressureLabel", Window, "PRESIÓN", 22, new Vector2(0f, 220f), new Vector2(300f, 40f));
        pressureFill = MinigameUI.CreateBar("PressureBar", Window, new Color(0.2f, 0.22f, 0.28f), new Color(0.9f, 0.5f, 0.1f), new Vector2(0f, 185f), new Vector2(700f, 30f));

        valveButtons = new Button[valveCount];
        valveWheels = new RectTransform[valveCount];
        valveLabels = new Text[valveCount];
        for (int i = 0; i < valveCount; i++)
        {
            Vector2 position = new Vector2((i - (valveCount - 1) / 2f) * 165f, 40f);
            valveButtons[i] = MinigameUI.CreateButton($"Valve{i + 1}", Window, "", ClosedColor, position, new Vector2(140f, 140f));

            RectTransform wheel = MinigameUI.CreateGroup("Wheel", valveButtons[i].transform, new Vector2(0f, 12f), new Vector2(80f, 80f));
            MinigameUI.CreateImage("SpokeHorizontal", wheel, wheelColor, Vector2.zero, new Vector2(80f, 12f));
            MinigameUI.CreateImage("SpokeVertical", wheel, wheelColor, Vector2.zero, new Vector2(12f, 80f));
            MinigameUI.CreateImage("Hub", wheel, wheelColor, Vector2.zero, new Vector2(28f, 28f));
            valveWheels[i] = wheel;

            valveLabels[i] = MinigameUI.CreateText("Label", valveButtons[i].transform, $"V{i + 1}", 18, new Vector2(0f, -52f), new Vector2(140f, 30f));
        }

        stepsText = MinigameUI.CreateText("StepsText", Window, "> Abrir V1     Cerrar V2     ...", 26, new Vector2(0f, -130f), new Vector2(950f, 80f));
        CreateInstructions("Sigue los pasos en orden. Equivocarte de válvula sube la presión");
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
            valveWheels[i].localRotation = Quaternion.identity;
            RefreshValve(i);
        }

        CreateSteps();
        RefreshSteps();
        MinigameUI.SetBar(pressureFill, 0f);
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

    private void PressValve(int valve)
    {
        Button button = valveButtons[valve];

        if (valve != stepValves[currentStep])
        {
            MinigameFX.Shake((RectTransform)button.transform);
            MinigameFX.Flash(button.image, Color.red);
            RemoveTime(mistakePenalty);
            return;
        }

        valveOpen[valve] = !valveOpen[valve];
        currentStep++;

        // La rueda da media vuelta, la válvula cambia de color y suelta un poco de "vapor"
        valveWheels[valve].DOKill(true);
        valveWheels[valve].DOLocalRotate(new Vector3(0f, 0f, -180f), 0.4f, RotateMode.LocalAxisAdd).SetEase(Ease.OutBack);
        MinigameFX.Punch(button.transform);
        MinigameFX.Burst(Window, ((RectTransform)button.transform).anchoredPosition, SteamColor, 10, 90f);
        RefreshValve(valve);
        RefreshSteps();
        MinigameFX.Punch(stepsText.transform, 0.05f);

        if (currentStep == stepCount)
        {
            Succeed();
        }
    }

    private void RefreshValve(int valve)
    {
        valveButtons[valve].image.DOKill();
        valveButtons[valve].image.DOColor(valveOpen[valve] ? OpenColor : ClosedColor, 0.2f);
        valveLabels[valve].text = $"V{valve + 1} {(valveOpen[valve] ? "ABIERTA" : "CERRADA")}";
    }

    private void RefreshSteps()
    {
        string text = "";
        for (int step = 0; step < stepCount; step++)
        {
            string action = $"{(stepOpens[step] ? "Abrir" : "Cerrar")} V{stepValves[step] + 1}";

            if (step < currentStep)
            {
                action = $"<color=#555555>{action}</color>";       // ya hecho
            }
            else if (step == currentStep)
            {
                action = $"<color=yellow>> {action}</color>";      // el siguiente
            }

            text += action + "     ";
        }

        stepsText.text = text;
    }
}
