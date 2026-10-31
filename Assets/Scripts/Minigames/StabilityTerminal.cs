using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Terminal para ver la estabilidad de la nave: al interactuar abre una ventana con todas las zonas.
// Se cierra con la X o volviendo a pulsar Interact (E).
// La crea Tools > Minijuegos > Crear terminal de estabilidad; luego se edita como UI normal.
public class StabilityTerminal : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Ver estado de la nave";

    [Header("UI (la crea la herramienta)")]
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private RectTransform window;
    [SerializeField] private Button closeButton;
    [Tooltip("Fila desactivada que se copia para cada minijuego. Sus hijos deben llamarse Name, Bar (con Fill dentro) y Percent")]
    [SerializeField] private RectTransform rowTemplate;

    private StabilityRows rows;
    private InputAction interactAction;
    private GameObject player;
    private int openedFrame;
    private int closedFrame;
    private bool isOpen;

    // La terminal que hay abierta ahora mismo (para que Escape la cierre antes de pausar)
    private static StabilityTerminal openTerminal;

    // Sin electricidad la terminal no funciona
    public string Prompt => ShipState.HasPower ? prompt : $"{prompt} (sin energía)";

    private void Awake()
    {
        interactAction = InputSystem.actions.FindAction("Interact");
        closeButton.onClick.AddListener(Close);
        uiRoot.SetActive(false);
    }

    private void Start()
    {
        rows = new StabilityRows(rowTemplate);
    }

    public void Interact(GameObject interactor)
    {
        // closedFrame: que la misma E que la cierra no la vuelva a abrir
        if (isOpen || Time.frameCount == closedFrame)
        {
            return;
        }

        if (!ShipState.HasPower)
        {
            transform.DOComplete();
            transform.DOShakePosition(0.3f, 0.05f, 20).SetLink(gameObject);
            return;
        }

        player = interactor;
        PlayerControl.Set(player, false);
        MinigameUI.EnsureEventSystem();

        isOpen = true;
        openTerminal = this;
        openedFrame = Time.frameCount;
        rows.Refresh();
        uiRoot.SetActive(true);

        window.DOKill(true);
        window.localScale = Vector3.one * 0.85f;
        window.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject);
    }

    private void Update()
    {
        if (!isOpen || PauseMenu.IsPaused)
        {
            return;
        }

        rows.Refresh();

        // openedFrame: que la misma E que la abre no la cierre
        if (interactAction.WasPressedThisFrame() && Time.frameCount != openedFrame)
        {
            Close();
        }
    }

    // Lo usa el menú de pausa: si la terminal está abierta, Escape la cierra en vez de pausar
    public static bool CloseOpenTerminal()
    {
        if (openTerminal == null)
        {
            return false;
        }

        openTerminal.Close();
        return true;
    }

    private void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        openTerminal = null;
        closedFrame = Time.frameCount;
        PlayerControl.Set(player, true);

        window.DOKill(true);
        window.DOScale(0.85f, 0.15f).SetEase(Ease.InBack).SetLink(gameObject).OnComplete(() => uiRoot.SetActive(false));
    }

    // Solo la usa la herramienta del editor
    public void CreateUI()
    {
        Canvas canvas = MinigameUI.CreateCanvas("TerminalCanvas", transform);
        uiRoot = canvas.gameObject;

        Image backdrop = MinigameUI.CreateImage("Backdrop", canvas.transform, new Color(0f, 0f, 0f, 0.6f), Vector2.zero, Vector2.zero);
        MinigameUI.Stretch(backdrop.rectTransform);

        window = MinigameUI.CreateImage("Window", canvas.transform, new Color(0.08f, 0.1f, 0.13f, 0.97f), Vector2.zero, new Vector2(700f, 580f)).rectTransform;
        MinigameUI.CreateText("Title", window, "ESTADO DE LA NAVE", 36, new Vector2(0f, 245f), new Vector2(560f, 50f));
        closeButton = MinigameUI.CreateButton("CloseButton", window, "X", new Color(0.6f, 0.2f, 0.2f), new Vector2(310f, 245f), new Vector2(50f, 50f));

        // Las filas se colocan solas una debajo de otra
        RectTransform list = MinigameUI.CreateGroup("List", window, new Vector2(0f, -10f), new Vector2(620f, 420f));
        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 10f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        rowTemplate = StabilityRows.CreateTemplate(list, 600f, 40f, 26);

        MinigameUI.CreateText("Instructions", window, "Pulsa E o la X para cerrar", 20, new Vector2(0f, -260f), new Vector2(600f, 30f))
            .color = new Color(0.75f, 0.78f, 0.85f);

        uiRoot.SetActive(false);
    }
}
