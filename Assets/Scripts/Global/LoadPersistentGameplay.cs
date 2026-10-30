using UnityEngine;
using UnityEngine.SceneManagement;
public class LoadPersistentGameplay
{
    private const string SceneName = "PersistentGameplay";

    // Se ejecuta una vez al arrancar el juego, sin necesidad de instanciar nada.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        if (!Application.CanStreamedLevelBeLoaded(SceneName))
        {
            Debug.LogError(
                $"No encuentro la escena '{SceneName}' en la lista de escenas de la build. " +
               "Sin ella no hay opciones, ni CRT, ni cursor, ni tecla de pausa.");
            return;
        }

        SceneManager.LoadScene(SceneName, LoadSceneMode.Additive);
    }
}
