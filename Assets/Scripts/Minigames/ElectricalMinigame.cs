using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Falla eléctrica: arrastra cada cable de la izquierda hasta su pareja de la derecha (el del mismo color).
// Las parejas van por número: LeftWire1 con RightWire1, LeftWire2 con RightWire2...
// Los cables no se tiñen (cada uno usa su sprite); en cada partida se desordena el sitio de los de la derecha.
// Mientras arrastras, el cable se estira siguiendo el ratón. Si lo sueltas en el equivocado, falla.
public class ElectricalMinigame : Minigame
{
    [Tooltip("Color de la línea que se dibuja para cada pareja de cables (en el mismo orden). Ajústalo a tus sprites")]
    [SerializeField] private Color[] lineColors =
    {
        Color.red, new Color(0.2f, 0.4f, 1f), Color.yellow, Color.green, Color.magenta, Color.cyan
    };

    [Header("UI")]
    [Tooltip("Hay tantos cables como botones en estas listas. El cable N de la izquierda va con el N de la derecha")]
    [SerializeField] private Button[] leftWires;
    [SerializeField] private Button[] rightWires;
    [Tooltip("Imagen desactivada que se copia para dibujar cada cable")]
    [SerializeField] private Image lineTemplate;

    private Vector2[] rightPositions;   // sitios originales de los cables de la derecha
    private bool[] leftConnected;
    private bool[] rightConnected;
    private int connectedCount;
    private int draggedWire;      // cable de la izquierda que se está arrastrando (-1 = ninguno)
    private Image draggedLine;
    private Tween draggedPulse;

    private void Reset()
    {
        title = "Restablecer energía";
        timeLimit = 20f;
    }

    public override string SystemName => ShipZones.Electricity;

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
        // Los colores de los botones son solo de relleno, hasta que les pongas sus sprites
        for (int i = 0; i < wireCount; i++)
        {
            float y = 180f - i * (350f / (wireCount - 1));
            leftWires[i] = MinigameUI.CreateButton($"LeftWire{i + 1}", Window, "", GetLineColor(i), new Vector2(-350f, y), wireSize);
            rightWires[i] = MinigameUI.CreateButton($"RightWire{i + 1}", Window, "", GetLineColor(i), new Vector2(350f, y), wireSize);
        }

        CreateInstructions("Arrastra cada cable de la izquierda hasta el del mismo color");
    }

    protected override void SetupMinigame()
    {
        // Se guardan los sitios de los cables de la derecha para desordenarlos en cada partida
        rightPositions = new Vector2[rightWires.Length];
        for (int i = 0; i < rightWires.Length; i++)
        {
            rightPositions[i] = GetPosition(rightWires[i]);
        }
    }

    protected override void StartMinigame()
    {
        int wireCount = leftWires.Length;
        connectedCount = 0;
        draggedWire = -1;
        leftConnected = new bool[wireCount];
        rightConnected = new bool[wireCount];

        // Los cables de la derecha se colocan en un orden distinto cada vez
        int[] order = new int[wireCount];
        for (int i = 0; i < wireCount; i++)
        {
            order[i] = i;
        }
        Shuffle(order);

        for (int i = 0; i < wireCount; i++)
        {
            ResetWire(leftWires[i]);
            ResetWire(rightWires[i]);
            ((RectTransform)rightWires[i].transform).anchoredPosition = rightPositions[order[i]];
        }
    }

    private static void ResetWire(Button wire)
    {
        wire.transform.DOKill(true);
        wire.transform.localScale = Vector3.one;
    }

    private Color GetLineColor(int wire)
    {
        return lineColors.Length > 0 ? lineColors[wire % lineColors.Length] : Color.white;
    }

    protected override void UpdateMinigame()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null)
        {
            return;
        }

        Vector2 screenPosition = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame)
        {
            StartDrag(FindWire(leftWires, screenPosition));
        }

        if (draggedWire < 0)
        {
            return;
        }

        // El cable se estira desde su enchufe hasta el ratón
        MinigameUI.PlaceLine(draggedLine.rectTransform, GetPosition(leftWires[draggedWire]), ScreenToWindow(screenPosition));

        if (pointer.press.wasReleasedThisFrame)
        {
            Drop(FindWire(rightWires, screenPosition));
        }
    }

    private void StartDrag(int wire)
    {
        if (wire < 0 || leftConnected[wire])
        {
            return;
        }

        draggedWire = wire;
        draggedLine = Spawn(lineTemplate);
        draggedLine.color = GetLineColor(wire);
        draggedPulse = MinigameFX.Pulse(leftWires[wire].transform, 1.2f, 0.35f);
    }

    private void Drop(int slot)
    {
        int wire = draggedWire;
        Image line = draggedLine;
        draggedWire = -1;
        draggedPulse.Kill();
        leftWires[wire].transform.localScale = Vector3.one;

        // Soltado fuera de un cable libre: el cable se recoge
        if (slot < 0 || rightConnected[slot])
        {
            RectTransform rect = line.rectTransform;
            rect.DOSizeDelta(new Vector2(0f, rect.sizeDelta.y), 0.15f).SetLink(line.gameObject)
                .OnComplete(() => Destroy(line.gameObject));
            return;
        }

        Button rightWire = rightWires[slot];
        MinigameUI.PlaceLine(line.rectTransform, GetPosition(leftWires[wire]), GetPosition(rightWire));

        // Cada cable de la izquierda solo va con el de la derecha de su mismo número
        if (slot != wire)
        {
            MinigameFX.Shake((RectTransform)rightWire.transform);
            Fail();
            return;
        }

        MinigameFX.Punch(rightWire.transform);
        MinigameFX.Burst(Window, GetPosition(rightWire), line.color);
        leftConnected[wire] = true;
        rightConnected[slot] = true;
        connectedCount++;

        if (connectedCount == leftWires.Length)
        {
            Succeed();
        }
    }

    // Índice del cable que hay bajo el ratón (-1 si no hay ninguno)
    private static int FindWire(Button[] wires, Vector2 screenPosition)
    {
        for (int i = 0; i < wires.Length; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)wires[i].transform, screenPosition))
            {
                return i;
            }
        }

        return -1;
    }

    // Pasa la posición del ratón en pantalla a una posición dentro de la ventana
    private Vector2 ScreenToWindow(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(Window, screenPosition, null, out Vector2 localPosition);
        return localPosition;
    }

    // Posición del botón dentro de la ventana (los cables deben ser hijos directos de Window)
    private static Vector2 GetPosition(Component element)
    {
        return ((RectTransform)element.transform).anchoredPosition;
    }
}
