using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Falla eléctrica: conecta cada cable de la izquierda con el del mismo color de la derecha.
// Clic en un cable de la izquierda y luego en su pareja. Si conectas un color equivocado, falla.
public class ElectricalMinigame : Minigame
{
    private static readonly Color[] WireColors =
    {
        Color.red, new Color(0.2f, 0.4f, 1f), Color.yellow, Color.green, Color.magenta, Color.cyan
    };

    [Header("UI")]
    [Tooltip("Hay tantos cables como botones en estas listas (máximo 6, uno por color)")]
    [SerializeField] private Button[] leftWires;
    [SerializeField] private Button[] rightWires;
    [Tooltip("Imagen desactivada que se copia para dibujar cada cable conectado")]
    [SerializeField] private Image lineTemplate;

    private int[] rightColors;    // qué color tiene cada cable de la derecha
    private bool[] leftConnected;
    private bool[] rightConnected;
    private int selectedWire;     // cable de la izquierda seleccionado (-1 = ninguno)
    private int connectedCount;
    private Tween selectedPulse;

    private void Reset()
    {
        title = "Restablecer energía";
        timeLimit = 20f;
    }

    protected override void CreateContent()
    {
        const int wireCount = 4;
        Vector2 wireSize = new Vector2(90f, 50f);

        // Las líneas van en un grupo creado antes que los cables, para que queden por detrás
        RectTransform lines = MinigameUI.CreateGroup("Lines", Window, Vector2.zero, WindowSize);
        lineTemplate = MinigameUI.CreateImage("LineTemplate", lines, Color.white, Vector2.zero, new Vector2(100f, 14f));
        lineTemplate.raycastTarget = false;
        lineTemplate.gameObject.SetActive(false);

        leftWires = new Button[wireCount];
        rightWires = new Button[wireCount];
        for (int i = 0; i < wireCount; i++)
        {
            float y = 180f - i * (350f / (wireCount - 1));
            leftWires[i] = MinigameUI.CreateButton($"LeftWire{i + 1}", Window, "", WireColors[i], new Vector2(-350f, y), wireSize);
            rightWires[i] = MinigameUI.CreateButton($"RightWire{i + 1}", Window, "", WireColors[i], new Vector2(350f, y), wireSize);
        }

        CreateInstructions("Clic en un cable de la izquierda y luego en el del mismo color a la derecha");
    }

    protected override void SetupMinigame()
    {
        for (int i = 0; i < leftWires.Length; i++)
        {
            int index = i; // copia para usarla dentro del clic del botón
            leftWires[i].onClick.AddListener(() => SelectWire(index));
            rightWires[i].onClick.AddListener(() => ConnectTo(index));
        }
    }

    protected override void StartMinigame()
    {
        int wireCount = leftWires.Length;
        selectedWire = -1;
        connectedCount = 0;
        leftConnected = new bool[wireCount];
        rightConnected = new bool[wireCount];

        // A la derecha están los mismos colores, pero desordenados
        rightColors = new int[wireCount];
        for (int i = 0; i < wireCount; i++)
        {
            rightColors[i] = i;
        }
        Shuffle(rightColors);

        for (int i = 0; i < wireCount; i++)
        {
            ResetWire(leftWires[i], WireColors[i]);
            ResetWire(rightWires[i], WireColors[rightColors[i]]);
        }
    }

    private void ResetWire(Button wire, Color color)
    {
        wire.transform.DOKill(true);
        wire.transform.localScale = Vector3.one;
        wire.image.color = color;
    }

    private void SelectWire(int wire)
    {
        if (leftConnected[wire])
        {
            return;
        }

        StopSelectedPulse();
        selectedWire = wire;
        selectedPulse = MinigameFX.Pulse(leftWires[wire].transform, 1.2f, 0.35f);
    }

    private void StopSelectedPulse()
    {
        if (selectedWire < 0)
        {
            return;
        }

        selectedPulse.Kill();
        leftWires[selectedWire].transform.localScale = Vector3.one;
    }

    private void ConnectTo(int slot)
    {
        if (selectedWire < 0 || rightConnected[slot])
        {
            return;
        }

        Button rightWire = rightWires[slot];
        if (rightColors[slot] != selectedWire)
        {
            MinigameFX.Shake((RectTransform)rightWire.transform);
            Fail();
            return;
        }

        Vector2 from = GetPosition(leftWires[selectedWire]);
        Vector2 to = GetPosition(rightWire);
        Color color = WireColors[selectedWire];

        Image line = Spawn(lineTemplate);
        line.color = color;
        MinigameUI.PlaceLine(line.rectTransform, from, to);
        MinigameFX.GrowLine(line.rectTransform);
        MinigameFX.Punch(rightWire.transform);
        MinigameFX.Burst(Window, to, color);

        StopSelectedPulse();
        leftConnected[selectedWire] = true;
        rightConnected[slot] = true;
        selectedWire = -1;
        connectedCount++;

        if (connectedCount == leftWires.Length)
        {
            Succeed();
        }
    }

    // Posición del botón dentro de la ventana (los cables deben ser hijos directos de Window)
    private static Vector2 GetPosition(Component element)
    {
        return ((RectTransform)element.transform).anchoredPosition;
    }
}
