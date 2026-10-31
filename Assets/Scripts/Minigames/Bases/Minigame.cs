using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Base de todos los minijuegos. Va en un objeto con Collider: al interactuar se abre su
// ventana (un Canvas hijo) y, al terminar, se lanza onSuccess u onFail.
//
// La UI la crea la primera vez Tools > Minijuegos > Convertir minijuegos de la escena en prefabs.
// A partir de ahí es UI normal: muévela, cámbiale colores, ponle sprites... en el prefab.
//
// Para crear un minijuego nuevo:
//   1. Hereda de Minigame.
//   2. CreateContent(): crea la UI placeholder (solo la usa la herramienta).
//   3. SetupMinigame(): conecta los botones (una vez, al empezar la escena).
//   4. StartMinigame(): reinicia el estado (cada vez que se abre).
//   5. UpdateMinigame() (en vez de Update) y los clics de los botones: la lógica.
//   6. Llama a Succeed() si se completa o a Fail() si se falla.
public abstract class Minigame : MonoBehaviour, IInteractable
{
    protected static readonly Vector2 WindowSize = new Vector2(1000f, 650f);

    [SerializeField] protected string title = "Minijuego";
    [Tooltip("Segundos para completarlo. 0 = sin límite")]
    [SerializeField] protected float timeLimit;
    [SerializeField] private float resultDuration = 1.5f;

    [Header("Salidas")]
    public UnityEvent onSuccess;
    public UnityEvent onFail;

    // La nave se va debilitando: cada minijuego es una zona de la nave. Cuando FailureDirector la
    // avería, pierde estabilidad (también mientras pilotas) hasta que completas el minijuego.
    // Para no usar un minijuego, desactiva su objeto o desmarca la casilla de este componente.
    // Estos datos y la posición para el mapa se copian a Resources/ShipSystems al guardar la escena.
    [Header("Estabilidad")]
    [Tooltip("Cuánta estabilidad pierde por segundo (de 100) mientras está averiada. Lo multiplica la curva de dificultad")]
    [SerializeField] private float stabilityLossPerSecond = 0.5f;
    [Tooltip("Cuánta estabilidad pierde si fallas el minijuego")]
    [SerializeField] private float failPenalty = 10f;

    [Header("UI base (la crea la herramienta)")]
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private Image backdrop;
    [SerializeField] private RectTransform window;
    [SerializeField] private CanvasGroup windowGroup;
    [SerializeField] private Text timerText;
    [SerializeField] private Button closeButton;
    [SerializeField] private CanvasGroup resultPanel;
    [SerializeField] private Text resultText;

    [Header("Partículas en el mundo")]
    [SerializeField] private ParticleSystem successParticles;
    [SerializeField] private ParticleSystem failParticles;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private GameObject player;
    private Vector2 windowPosition;
    private float backdropAlpha;
    private Color timerColor;
    private float timeLeft;
    private int lastShownSecond;
    private bool isOpen;
    private bool isPlaying;

    // El minijuego que hay abierto ahora mismo (para que Escape lo cierre antes de pausar)
    private static Minigame openMinigame;

    public string Prompt => HasPower ? title : $"{title} (sin energía)";
    public bool IsOpen => isOpen;

    // Sin electricidad las demás terminales no funcionan (la de electricidad sí, para poder repararla)
    private bool HasPower => SystemName == ShipZones.Electricity || ShipState.HasPower;
    public GameObject UIRoot => uiRoot;

    // De 0 a 100. La guarda ShipState para que siga bajando aunque cambies de escena.
    public float Stability => ShipState.GetStability(SystemName);
    public float StabilityLossPerSecond => stabilityLossPerSecond;

    // Nombre corto de la zona de la nave, para el panel de estabilidad
    public virtual string SystemName => title;

    protected RectTransform Window => window;
    protected float TimeLeft => timeLeft;

    // Crea la UI placeholder de este minijuego dentro de Window (solo la usa la herramienta)
    protected abstract void CreateContent();

    // Se llama una vez al empezar la escena: conecta aquí los botones
    protected virtual void SetupMinigame()
    {
    }

    // Se llama cada vez que se abre el minijuego: reinicia aquí su estado
    protected abstract void StartMinigame();

    // Se llama cada frame mientras se está jugando
    protected virtual void UpdateMinigame()
    {
    }

    private void Awake()
    {
        if (uiRoot == null)
        {
            Debug.LogError($"{name} no tiene UI. Usa Tools > Minijuegos > Convertir minijuegos de la escena en prefabs", this);
            return;
        }

        windowPosition = window.anchoredPosition;
        backdropAlpha = backdrop.color.a;
        timerColor = timerText.color;
        uiRoot.SetActive(false);
        closeButton.onClick.AddListener(Cancel);
        SetupMinigame();
    }

    public void Interact(GameObject interactor)
    {
        if (isOpen || uiRoot == null)
        {
            return;
        }

        // Sin energía no se enciende: solo una pequeña sacudida
        if (!HasPower)
        {
            transform.DOComplete();
            transform.DOShakePosition(0.3f, 0.05f, 20).SetLink(gameObject);
            return;
        }

        player = interactor;
        PlayerControl.Set(player, false);
        MinigameUI.EnsureEventSystem();

        ClearSpawned();
        resultPanel.gameObject.SetActive(false);
        timerText.text = "";
        timerText.color = timerColor;
        timeLeft = timeLimit;
        lastShownSecond = -1;
        isOpen = true;
        isPlaying = true;
        openMinigame = this;

        PlayOpenAnimation();
        StartMinigame();
    }

    private void Update()
    {
        if (!isPlaying || PauseMenu.IsPaused)
        {
            return;
        }

        if (timeLimit > 0f)
        {
            timeLeft -= Time.deltaTime;
            UpdateTimer();

            if (timeLeft <= 0f)
            {
                Fail();
                return;
            }
        }

        UpdateMinigame();
    }

    private void UpdateTimer()
    {
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
        timerText.text = $"Tiempo: {seconds}";

        // En los últimos 5 segundos el tiempo se pone rojo y late cada segundo
        if (seconds != lastShownSecond && seconds <= 5)
        {
            timerText.color = Color.red;
            MinigameFX.Punch(timerText.transform, 0.3f);
        }

        lastShownSecond = seconds;
    }

    protected void Succeed()
    {
        Finish(true);
    }

    protected void Fail()
    {
        Finish(false);
    }

    protected void RemoveTime(float seconds)
    {
        timeLeft -= seconds;
        MinigameFX.Punch(timerText.transform, 0.4f);
        MinigameFX.FloatingText(window, timerText.rectTransform.anchoredPosition + Vector2.down * 40f, $"-{seconds:0}s", Color.red);
    }

    // Crea una copia de una plantilla (que está desactivada en la UI). Se borra sola al volver a abrir.
    protected T Spawn<T>(T template) where T : Component
    {
        T copy = Instantiate(template, template.transform.parent);
        copy.gameObject.SetActive(true);
        spawned.Add(copy.gameObject);
        return copy;
    }

    protected static void Shuffle(int[] values)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private void Finish(bool success)
    {
        if (!isPlaying)
        {
            return;
        }

        isPlaying = false;
        StopAllCoroutines();
        StartCoroutine(ShowResultAndClose(success));
    }

    private IEnumerator ShowResultAndClose(bool success)
    {
        // Panel que tapa la ventana (y bloquea los clics) mientras se ve el resultado
        resultPanel.gameObject.SetActive(true);
        resultPanel.alpha = 0f;
        resultPanel.DOFade(1f, 0.2f).SetLink(resultPanel.gameObject);

        resultText.text = success ? "¡COMPLETADO!" : "¡FALLO!";
        resultText.color = success ? Color.green : Color.red;
        resultText.transform.localScale = Vector3.zero;
        resultText.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetLink(resultText.gameObject);

        if (success)
        {
            MinigameFX.Confetti(window);
        }
        else
        {
            MinigameFX.Shake(window, 25f);
        }

        Debug.Log($"{title}: {(success ? "completado" : "fallado")}", this);

        float visibleDuration = success ? Mathf.Min(resultDuration, 0.6f) : resultDuration;
        yield return new WaitForSeconds(visibleDuration);

        CloseWindow();
        PlayWorldFeedback(success);

        if (success)
        {
            ShipState.SetStability(SystemName, 100f);
            onSuccess.Invoke();
        }
        else
        {
            ShipState.SetStability(SystemName, Stability - failPenalty);
            onFail.Invoke();
        }
    }

    // Lo usa el menú de pausa: si hay un minijuego abierto, Escape lo cierra en vez de pausar.
    // Devuelve true si había uno (aunque esté enseñando el resultado y no se pueda cerrar aún).
    public static bool CloseOpenMinigame()
    {
        if (openMinigame == null)
        {
            return false;
        }

        openMinigame.Cancel();
        return true;
    }

    // Botón X (o Escape): cierra sin completar ni fallar
    private void Cancel()
    {
        if (!isPlaying)
        {
            return;
        }

        StopAllCoroutines();
        CloseWindow();
    }

    private void PlayOpenAnimation()
    {
        KillWindowTweens();
        uiRoot.SetActive(true);
        windowGroup.interactable = true;
        windowGroup.alpha = 0f;
        window.anchoredPosition = windowPosition;
        window.localScale = Vector3.one * 0.85f;
        backdrop.color = new Color(backdrop.color.r, backdrop.color.g, backdrop.color.b, 0f);

        backdrop.DOFade(backdropAlpha, 0.25f).SetLink(gameObject);
        windowGroup.DOFade(1f, 0.25f).SetLink(gameObject);
        window.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetLink(gameObject);
    }

    private void CloseWindow()
    {
        isOpen = false;
        isPlaying = false;
        if (openMinigame == this)
        {
            openMinigame = null;
        }

        windowGroup.interactable = false;
        PlayerControl.Set(player, true);

        KillWindowTweens();
        backdrop.DOFade(0f, 0.2f).SetLink(gameObject);
        windowGroup.DOFade(0f, 0.2f).SetLink(gameObject);
        window.DOScale(0.85f, 0.2f).SetEase(Ease.InBack).SetLink(gameObject).OnComplete(() => uiRoot.SetActive(false));
    }

    private void KillWindowTweens()
    {
        backdrop.DOKill(true);
        windowGroup.DOKill(true);
        window.DOKill(true);
    }

    // Partículas y un pequeño salto del objeto 3D, que se ven al cerrar la ventana
    private void PlayWorldFeedback(bool success)
    {
        ParticleSystem particles = success ? successParticles : failParticles;
        if (particles != null)
        {
            particles.Play();
        }

        transform.DOKill(true);
        transform.DOPunchScale(Vector3.one * 0.1f, 0.4f, 6).SetLink(gameObject);
    }

    private void ClearSpawned()
    {
        foreach (GameObject copy in spawned)
        {
            if (copy != null)
            {
                Destroy(copy);
            }
        }

        spawned.Clear();
    }

    private void OnDisable()
    {
        if (!isOpen)
        {
            return;
        }

        StopAllCoroutines();
        isOpen = false;
        isPlaying = false;
        if (openMinigame == this)
        {
            openMinigame = null;
        }

        uiRoot.SetActive(false);
        PlayerControl.Set(player, true);
    }

    // Solo la usa la herramienta del editor: crea el Canvas con la ventana base
    // y luego llama a CreateContent() para el contenido de cada minijuego
    public GameObject CreateUI()
    {
        Canvas canvas = MinigameUI.CreateCanvas("MinigameCanvas", transform);
        uiRoot = canvas.gameObject;

        backdrop = MinigameUI.CreateImage("Backdrop", canvas.transform, new Color(0f, 0f, 0f, 0.6f), Vector2.zero, Vector2.zero);
        MinigameUI.Stretch(backdrop.rectTransform);

        Image windowImage = MinigameUI.CreateImage("Window", canvas.transform, new Color(0.08f, 0.1f, 0.13f, 0.97f), Vector2.zero, WindowSize);
        window = windowImage.rectTransform;
        windowGroup = windowImage.gameObject.AddComponent<CanvasGroup>();

        MinigameUI.CreateText("Title", window, title, 36, new Vector2(0f, 285f), new Vector2(600f, 50f));
        timerText = MinigameUI.CreateText("Timer", window, timeLimit > 0f ? $"Tiempo: {timeLimit:0}" : "", 26, new Vector2(-380f, 285f), new Vector2(200f, 50f));
        closeButton = MinigameUI.CreateButton("CloseButton", window, "X", new Color(0.6f, 0.2f, 0.2f), new Vector2(460f, 285f), new Vector2(50f, 50f));

        CreateContent();

        Image result = MinigameUI.CreateImage("ResultPanel", window, new Color(0f, 0f, 0f, 0.8f), Vector2.zero, WindowSize);
        resultPanel = result.gameObject.AddComponent<CanvasGroup>();
        resultText = MinigameUI.CreateText("ResultText", result.transform, "¡COMPLETADO!", 72, Vector2.zero, new Vector2(900f, 150f));
        result.gameObject.SetActive(false);

        uiRoot.SetActive(false);
        return uiRoot;
    }

    // Texto de ayuda en la parte de abajo de la ventana (para usar en CreateContent)
    protected void CreateInstructions(string text)
    {
        MinigameUI.CreateText("Instructions", window, text, 22, new Vector2(0f, -275f), new Vector2(900f, 40f)).color = new Color(0.75f, 0.78f, 0.85f);
    }
}
