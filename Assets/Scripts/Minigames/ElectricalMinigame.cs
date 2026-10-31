using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Falla eléctrica: arrastra cada cable de la izquierda hasta el del mismo color de la derecha.
// Mientras arrastras, el cable se estira siguiendo el ratón. Si lo sueltas en un color equivocado, falla.
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
    [Tooltip("Imagen desactivada que se copia para dibujar cada cable")]
    [SerializeField] private Image lineTemplate;

    private int[] rightColors;    // qué color tiene cada cable de la derecha
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
        for (int i = 0; i < wireCount; i++)
        {
            float y = 180f - i * (350f / (wireCount - 1));
            leftWires[i] = MinigameUI.CreateButton($"LeftWire{i + 1}", Window, "", WireColors[i], new Vector2(-350f, y), wireSize);
            rightWires[i] = MinigameUI.CreateButton($"RightWire{i + 1}", Window, "", WireColors[i], new Vector2(350f, y), wireSize);
        }

        CreateInstructions("Arrastra cada cable de la izquierda hasta el del mismo color");
    }

    protected override void StartMinigame()
    {
        int wireCount = leftWires.Length;
        connectedCount = 0;
        draggedWire = -1;
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
        draggedLine.color = WireColors[wire];
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

        if (rightColors[slot] != wire)
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
            RectTransform rect = (RectTransform)wires[i].transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, null, out Vector2 localPosition) &&
                ExpandHitArea(rect.rect, 22f, 15f).Contains(localPosition))
            {
                return i;
            }
        }

        return -1;
    }

    private static Rect ExpandHitArea(Rect rect, float horizontalPadding, float verticalPadding)
    {
        rect.xMin -= horizontalPadding;
        rect.xMax += horizontalPadding;
        rect.yMin -= verticalPadding;
        rect.yMax += verticalPadding;
        return rect;
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
