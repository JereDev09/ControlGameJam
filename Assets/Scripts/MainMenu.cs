using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Menú principal: título, Jugar y Créditos, con la escena de la nave de fondo.
// La escena y su UI las crea Tools > Menú > Crear menú principal; luego se edita como UI normal.
public class MainMenu : MonoBehaviour
{
    [Tooltip("Escena que se carga al pulsar Jugar (tiene que estar en la lista de escenas de la build)")]
    [SerializeField] private string gameScene = "MinijuegoPrincipal";
    [Tooltip("La nave del fondo, para que flote un poco")]
    [SerializeField] private Transform ship;

    [Header("UI (la crea la herramienta)")]
    [SerializeField] private RectTransform title;
    [SerializeField] private CanvasGroup mainPanel;
    [SerializeField] private Button playButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private CanvasGroup creditsPanel;
    [SerializeField] private Button backButton;
    [Tooltip("Imagen negra que tapa la pantalla al entrar y al salir")]
    [SerializeField] private Image fade;

    // Mientras el menú está abierto no hay averías ni se puede pausar
    public static bool IsOpen { get; private set; }

    private void Start()
    {
        IsOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        playButton.onClick.AddListener(Play);
        creditsButton.onClick.AddListener(() => ShowCredits(true));
        backButton.onClick.AddListener(() => ShowCredits(false));
        creditsPanel.gameObject.SetActive(false);

        PlayIntro();
        FloatShip();
    }

    private void OnDestroy()
    {
        IsOpen = false;
    }

    // El negro se aclara, el título cae desde arriba y aparecen los botones
    private void PlayIntro()
    {
        fade.color = Color.black;
        fade.DOFade(0f, 1f).SetLink(gameObject);

        Vector2 titlePosition = title.anchoredPosition;
        title.anchoredPosition = titlePosition + Vector2.up * 300f;
        title.DOAnchorPos(titlePosition, 0.9f).SetEase(Ease.OutBack).SetDelay(0.3f).SetLink(gameObject);
        title.DOScale(1.04f, 1.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(1.2f).SetLink(title.gameObject);

        mainPanel.alpha = 0f;
        mainPanel.DOFade(1f, 0.6f).SetDelay(1f).SetLink(gameObject);
    }

    // La nave sube y baja despacio (la altura depende de su tamaño)
    private void FloatShip()
    {
        if (ship == null)
        {
            return;
        }

        Renderer shipRenderer = ship.GetComponentInChildren<Renderer>();
        float height = shipRenderer != null ? shipRenderer.bounds.size.y * 0.15f : 0.1f;
        ship.DOMoveY(ship.position.y + height, 2f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(ship.gameObject);
    }

    private void Play()
    {
        mainPanel.interactable = false;
        MinigameFX.Punch(playButton.transform);
        ShipState.Reset(); // partida nueva
        SceneTransition.Load(gameScene, 0.8f, 1f);
    }

    private void ShowCredits(bool show)
    {
        CanvasGroup hide = show ? mainPanel : creditsPanel;
        CanvasGroup appear = show ? creditsPanel : mainPanel;

        hide.interactable = false;
        hide.DOFade(0f, 0.25f).SetLink(gameObject).OnComplete(() => hide.gameObject.SetActive(false));

        appear.gameObject.SetActive(true);
        appear.interactable = true;
        appear.alpha = 0f;
        appear.DOFade(1f, 0.25f).SetLink(gameObject);
    }

    // Solo la usa la herramienta del editor
    public void CreateUI(Transform backgroundShip)
    {
        ship = backgroundShip;
        Canvas canvas = MinigameUI.CreateCanvas("MenuCanvas", transform);
        Color buttonColor = new Color(0.12f, 0.2f, 0.35f, 0.9f);

        Text titleText = MinigameUI.CreateText("Title", canvas.transform, "NOMBRE DEL JUEGO", 96, new Vector2(0f, 250f), new Vector2(1600f, 160f));
        titleText.fontStyle = FontStyle.Bold;
        titleText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4f, -4f);
        title = titleText.rectTransform;

        RectTransform main = MinigameUI.CreateGroup("MainPanel", canvas.transform, new Vector2(0f, -100f), new Vector2(400f, 300f));
        mainPanel = main.gameObject.AddComponent<CanvasGroup>();
        playButton = MinigameUI.CreateButton("PlayButton", main, "JUGAR", buttonColor, new Vector2(0f, 60f), new Vector2(360f, 80f));
        creditsButton = MinigameUI.CreateButton("CreditsButton", main, "CRÉDITOS", buttonColor, new Vector2(0f, -50f), new Vector2(360f, 80f));

        Image credits = MinigameUI.CreateImage("CreditsPanel", canvas.transform, new Color(0.05f, 0.07f, 0.1f, 0.92f), new Vector2(0f, -60f), new Vector2(900f, 600f));
        creditsPanel = credits.gameObject.AddComponent<CanvasGroup>();
        MinigameUI.CreateText("CreditsTitle", credits.transform, "CRÉDITOS", 48, new Vector2(0f, 240f), new Vector2(800f, 70f));
        MinigameUI.CreateText("CreditsText", credits.transform,
            "Programación\nNombre\n\nArte\nNombre\n\nDiseño\nNombre", 30, new Vector2(0f, 10f), new Vector2(800f, 360f));
        backButton = MinigameUI.CreateButton("BackButton", credits.transform, "VOLVER", buttonColor, new Vector2(0f, -240f), new Vector2(300f, 70f));
        credits.gameObject.SetActive(false);

        fade = MinigameUI.CreateImage("Fade", canvas.transform, new Color(0f, 0f, 0f, 0f), Vector2.zero, Vector2.zero);
        MinigameUI.Stretch(fade.rectTransform);
        fade.raycastTarget = false;
    }
}
