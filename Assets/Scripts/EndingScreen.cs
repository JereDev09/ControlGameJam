using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Escena con una imagen que se acerca despacio y, pasado un rato, vuelve al menú principal.
// Se usa para el final (al entregar el pedido) y para la derrota (al explotar la nave).
// Las escenas las crean Tools > Menú > Crear escena final / de derrota. Pon tu imagen en EndingCanvas > Image.
public class EndingScreen : MonoBehaviour
{
    [Tooltip("Segundos que se ve la imagen antes de volver al menú")]
    [SerializeField] private float duration = 8f;
    [Tooltip("Escena del menú principal (tiene que estar en la lista de escenas de la build)")]
    [SerializeField] private string menuScene = "MenuPrincipal";
    [Tooltip("Música de esta escena. Vacío = sigue la que estuviera sonando (al perder, la explosión ya la ha cortado)")]
    [SerializeField] private AudioClip music;

    [Header("UI (la crea la herramienta)")]
    [SerializeField] private Image image;
    [SerializeField] private CanvasGroup texts;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (music != null)
        {
            GameAudio.PlayMusic(music);
        }

        GameAudio.SetMusicMode(GameAudio.MusicMode.Normal);
        GameAudio.PlayAmbience(null);

        // La imagen se acerca muy despacio y los textos aparecen un poco después
        image.transform.localScale = Vector3.one;
        image.transform.DOScale(1.08f, duration).SetEase(Ease.Linear).SetLink(image.gameObject);
        texts.alpha = 0f;
        texts.DOFade(1f, 1.5f).SetDelay(1f).SetLink(texts.gameObject);

        DOVirtual.DelayedCall(duration, () => SceneTransition.Load(menuScene, 1.5f)).SetLink(gameObject);
    }

    // Solo la usa la herramienta del editor
    public void CreateUI(string titleText, string subtitleText)
    {
        Canvas canvas = MinigameUI.CreateCanvas("EndingCanvas", transform);

        // Imagen a pantalla completa (de momento un color de relleno)
        image = MinigameUI.CreateImage("Image", canvas.transform, new Color(0.1f, 0.15f, 0.25f), Vector2.zero, Vector2.zero);
        MinigameUI.Stretch(image.rectTransform);
        image.preserveAspect = true;
        image.raycastTarget = false;

        RectTransform textGroup = MinigameUI.CreateGroup("Texts", canvas.transform, Vector2.zero, Vector2.zero);
        MinigameUI.Stretch(textGroup);
        texts = textGroup.gameObject.AddComponent<CanvasGroup>();

        Text title = MinigameUI.CreateText("Title", textGroup, titleText, 90, new Vector2(0f, 380f), new Vector2(1600f, 140f));
        title.fontStyle = FontStyle.Bold;
        title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4f, -4f);
        MinigameUI.CreateText("Subtitle", textGroup, subtitleText, 40, new Vector2(0f, -420f), new Vector2(1600f, 70f))
            .gameObject.AddComponent<Shadow>();
    }
}
