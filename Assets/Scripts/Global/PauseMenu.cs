using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Menú de pausa (tecla Escape): para el juego y deja continuar, volver al menú principal o salir.
// Va en la escena PersistentGameplay, que siempre está cargada, así funciona en todas las escenas
// de juego. En el menú principal, en el final, al perder y durante los cambios de escena no se abre.
// La UI la crea Tools > Menú > Crear menú de pausa; luego se edita como UI normal.
public class PauseMenu : MonoBehaviour
{
    [Tooltip("Escena del menú principal (tiene que estar en la lista de escenas de la build)")]
    [SerializeField] private string menuScene = "MenuPrincipal";

    [Header("UI (la crea la herramienta)")]
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private RectTransform window;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private Button quitButton;

    private InputAction pauseAction;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;

    // Los scripts que leen controles lo miran para no hacer nada mientras está en pausa
    public static bool IsPaused { get; private set; }

    private void Awake()
    {
        pauseAction = InputSystem.actions.FindAction("UI/Cancel");
        resumeButton.onClick.AddListener(Resume);
        menuButton.onClick.AddListener(GoToMenu);
        quitButton.onClick.AddListener(Quit);
        uiRoot.SetActive(false);
    }

    private void Update()
    {
        if (!pauseAction.WasPressedThisFrame())
        {
            return;
        }

        if (IsPaused)
        {
            Resume();
            return;
        }

        // Si hay un minijuego o la terminal abiertos, Escape primero los cierra (sin pausar)
        if (Minigame.CloseOpenMinigame() || StabilityTerminal.CloseOpenTerminal())
        {
            return;
        }

        if (!MainMenu.IsOpen && !ShipState.HasArrived && !ShipState.IsDestroyed && !SceneTransition.IsTransitioning)
        {
            Pause();
        }
    }

    private void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;

        // Se libera el ratón para usar los botones (y se recuerda cómo estaba)
        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        MinigameUI.EnsureEventSystem();
        uiRoot.SetActive(true);

        // SetUpdate(true): la animación funciona aunque el juego esté parado
        window.DOKill(true);
        window.localScale = Vector3.one * 0.85f;
        window.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
        uiRoot.SetActive(false);
    }

    private void GoToMenu()
    {
        Resume();
        SceneTransition.Load(menuScene);
    }

    private void Quit()
    {
        // Application.Quit no hace nada dentro del editor, así que ahí se para el Play
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Al darle a Play en el editor siempre se empieza sin pausa
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        IsPaused = false;
    }

    // Solo la usa la herramienta del editor
    public void CreateUI()
    {
        Canvas canvas = MinigameUI.CreateCanvas("PauseCanvas", transform);
        canvas.sortingOrder = 200; // por encima de todo menos del fundido entre escenas
        uiRoot = canvas.gameObject;

        Image backdrop = MinigameUI.CreateImage("Backdrop", canvas.transform, new Color(0f, 0f, 0f, 0.7f), Vector2.zero, Vector2.zero);
        MinigameUI.Stretch(backdrop.rectTransform);

        Color buttonColor = new Color(0.12f, 0.2f, 0.35f, 0.95f);
        window = MinigameUI.CreateImage("Window", canvas.transform, new Color(0.08f, 0.1f, 0.13f, 0.97f), Vector2.zero, new Vector2(500f, 440f)).rectTransform;
        MinigameUI.CreateText("Title", window, "PAUSA", 52, new Vector2(0f, 160f), new Vector2(400f, 70f));
        resumeButton = MinigameUI.CreateButton("ResumeButton", window, "CONTINUAR", buttonColor, new Vector2(0f, 50f), new Vector2(360f, 70f));
        menuButton = MinigameUI.CreateButton("MenuButton", window, "MENÚ PRINCIPAL", buttonColor, new Vector2(0f, -40f), new Vector2(360f, 70f));
        quitButton = MinigameUI.CreateButton("QuitButton", window, "SALIR DEL JUEGO", new Color(0.4f, 0.12f, 0.12f, 0.95f), new Vector2(0f, -130f), new Vector2(360f, 70f));

        uiRoot.SetActive(false);
    }
}
