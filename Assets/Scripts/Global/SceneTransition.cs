using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Cambia de escena con un fundido a negro: SceneTransition.Load("NombreEscena").
// Se puede elegir cuánto tarda en fundirse (por ejemplo, lo mismo que una animación de cámara).
// No cierra PersistentGameplay: descarga solo la escena actual y carga la nueva encima.
public class SceneTransition : MonoBehaviour
{
    private const float FadeDuration = 0.5f;

    private static SceneTransition instance;
    private Image fade;

    public static bool IsTransitioning { get; private set; }

    public static void Load(string sceneName, float fadeOutDuration = FadeDuration)
    {
        if (IsTransitioning)
        {
            return;
        }

        if (instance == null)
        {
            CreateInstance();
        }

        instance.StartCoroutine(instance.LoadRoutine(sceneName, fadeOutDuration));
    }

    // Se crea sola la primera vez y sobrevive a los cambios de escena
    private static void CreateInstance()
    {
        GameObject transitionObject = new GameObject("SceneTransition");
        DontDestroyOnLoad(transitionObject);
        instance = transitionObject.AddComponent<SceneTransition>();

        Canvas canvas = MinigameUI.CreateCanvas("FadeCanvas", transitionObject.transform);
        canvas.sortingOrder = 1000; // por encima de todo

        instance.fade = MinigameUI.CreateImage("Fade", canvas.transform, new Color(0f, 0f, 0f, 0f), Vector2.zero, Vector2.zero);
        MinigameUI.Stretch(instance.fade.rectTransform);
        instance.fade.raycastTarget = false;
    }

    private IEnumerator LoadRoutine(string sceneName, float fadeOutDuration)
    {
        IsTransitioning = true;
        fade.raycastTarget = true; // bloquea los clics mientras dura
        yield return fade.DOFade(1f, fadeOutDuration).WaitForCompletion();

        if (SceneManager.sceneCount > 1)
        {
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene());
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            // La escena activa es la que pone el cielo y la iluminación
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
        }
        else
        {
            yield return SceneManager.LoadSceneAsync(sceneName);
        }

        yield return fade.DOFade(0f, FadeDuration).WaitForCompletion();
        fade.raycastTarget = false;
        IsTransitioning = false;
    }

    // Al darle a Play en el editor siempre se empieza sin transición a medias
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        IsTransitioning = false;
    }
}
