using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Lo que pasa con las averías en cualquier escena (se crea solo y sobrevive a los cambios de escena):
// - Decide cuándo se avería cada zona según la curva de dificultad: al principio falla una sola
//   cosa de vez en cuando; cerca del destino, varias y más rápido. Con varias averiadas, las
//   siguientes a la primera bajan más despacio, salvo muy cerca del destino.
//   Mientras dura el tutorial (ShipTutorial) no hay averías al azar.
// - Presión: la nave pierde vida poco a poco.
// - Sobrecarga: la imagen se distorsiona y hay explosiones internas que quitan vida.
// - Derrota: si varias zonas llegan a 0% (o la vida a 0), la nave explota.
// Los efectos de cada escena (tirones, chispas, apagones...) están en ShipFailureEffects
// (pilotar) e InteriorFailureEffects (primera persona). Los ajustes, en Resources/ShipSystems.
public class FailureDirector : MonoBehaviour
{
    // Avisa a los efectos de cada escena para que enseñen la explosión (temblor, chispas...)
    public static event System.Action OverloadExploded;

    private static FailureDirector instance;
    private float nextFailureTimer;
    private float nextExplosionTimer;
    private float destroyedTimer;
    private bool gameOverShown;
    private bool returningToMenu;
    private Volume distortionVolume;
    private VolumeProfile distortionProfile;
    private float crtBaseCurvature = -1f;

    public static void Create()
    {
        if (instance != null)
        {
            return;
        }

        GameObject directorObject = new GameObject("FailureDirector");
        DontDestroyOnLoad(directorObject);
        instance = directorObject.AddComponent<FailureDirector>();
    }

    public static void Remove()
    {
        if (instance != null)
        {
            Destroy(instance.gameObject);
            instance = null;
        }
    }

    private void Start()
    {
        nextFailureTimer = ShipState.Config.firstFailureDelay;
        nextExplosionTimer = ShipState.Config.overloadExplosionInterval.x;
        CreateDistortionVolume();
    }

    private void OnDestroy()
    {
        RestoreCRT();

        if (distortionProfile != null)
        {
            Destroy(distortionProfile);
        }
    }

    private void Update()
    {
        // En el menú principal no pasa nada
        if (MainMenu.IsOpen)
        {
            distortionVolume.weight = 0f;
            RestoreCRT();
            return;
        }

        if (ShipState.IsDestroyed)
        {
            distortionVolume.weight = 0f;
            RestoreCRT();
            UpdateDestruction();
            return;
        }

        UpdateFailures();
        UpdatePressure();
        UpdateOverload();
        CheckDefeat();
    }

    // ---------- Averías ----------

    private void UpdateFailures()
    {
        ShipSystems config = ShipState.Config;
        float progress = ShipState.Progress;

        // Las zonas averiadas pierden estabilidad, más rápido cuanto más avanzado está el viaje.
        // Si hay varias, solo la primera baja a velocidad normal (salvo muy cerca del destino).
        // Durante el tutorial las averías las decide ShipTutorial y su estabilidad no baja más
        if (!ShipState.TutorialFinished)
        {
            nextFailureTimer = config.firstFailureDelay;
            return;
        }

        float lossMultiplier = config.lossMultiplier.Evaluate(progress);
        bool noMercy = progress >= config.noMercyProgress;
        ShipState.SystemState firstFailing = FindFirstFailing();
        int failingCount = 0;

        foreach (ShipState.SystemState system in ShipState.Systems)
        {
            if (!system.isFailing)
            {
                continue;
            }

            failingCount++;
            float crowdMultiplier = system == firstFailing || noMercy ? 1f : config.othersLossMultiplier;
            float loss = system.lossPerSecond * lossMultiplier * crowdMultiplier * Time.deltaTime;
            system.stability = Mathf.Max(0f, system.stability - loss);
        }

        if (!ShipState.IsPiloting)
        {
            return;
        }

        // Cada cierto tiempo se avería una zona nueva, si no hay ya demasiadas averiadas
        nextFailureTimer -= Time.deltaTime;
        if (nextFailureTimer > 0f)
        {
            return;
        }

        nextFailureTimer = config.secondsBetweenFailures.Evaluate(progress);
        if (failingCount < Mathf.RoundToInt(config.maxFailuresAtOnce.Evaluate(progress)))
        {
            BreakRandomSystem();
        }
    }

    private static void BreakRandomSystem()
    {
        List<ShipState.SystemState> working = new List<ShipState.SystemState>();
        foreach (ShipState.SystemState system in ShipState.Systems)
        {
            if (!system.isFailing)
            {
                working.Add(system);
            }
        }

        if (working.Count > 0)
        {
            // Al averiarse pierde de golpe un buen trozo de estabilidad, para que se note enseguida
            ShipState.SystemState system = working[Random.Range(0, working.Count)];
            ShipState.Break(system.name);
            ShipState.SetStability(system.name, system.stability - ShipState.Config.failureInitialDrop);
        }
    }

    // La zona que lleva más tiempo averiada
    private static ShipState.SystemState FindFirstFailing()
    {
        ShipState.SystemState first = null;
        foreach (ShipState.SystemState system in ShipState.Systems)
        {
            if (system.isFailing && (first == null || system.failedAt < first.failedAt))
            {
                first = system;
            }
        }

        return first;
    }

    // ---------- Presión: la nave pierde vida poco a poco ----------

    private void UpdatePressure()
    {
        // Durante el tutorial las averías se ven pero no hacen daño
        if (!ShipState.TutorialFinished)
        {
            return;
        }

        float severity = ShipState.GetSeverity(ShipZones.Pressure);
        ShipState.Damage(ShipState.Config.pressureDamagePerSecond * severity * Time.deltaTime);
    }

    // ---------- Sobrecarga: distorsión y explosiones internas ----------

    private void UpdateOverload()
    {
        float severity = ShipState.GetSeverity(ShipZones.Overload);

        // La distorsión tiembla con ruido y es más fuerte cuanto peor está la zona
        float wobble = Mathf.PerlinNoise(Time.time * 2f, 0f);
        distortionVolume.weight = severity * Mathf.Lerp(0.4f, 1f, wobble);
        WobbleCRT(severity, wobble);

        if (severity <= 0f)
        {
            nextExplosionTimer = ShipState.Config.overloadExplosionInterval.x;
            return;
        }

        nextExplosionTimer -= Time.deltaTime;
        if (nextExplosionTimer <= 0f)
        {
            Vector2 interval = ShipState.Config.overloadExplosionInterval;
            nextExplosionTimer = Mathf.Lerp(interval.x, interval.y, severity);
            if (ShipState.TutorialFinished)
            {
                ShipState.Damage(ShipState.Config.overloadExplosionDamage);
            }

            OverloadExploded?.Invoke();
        }
    }

    // Volume global creado por código con aberración cromática y distorsión de lente.
    // Su "weight" (de 0 a 1) decide cuánto se ve.
    private void CreateDistortionVolume()
    {
        distortionProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        distortionProfile.Add<ChromaticAberration>(true).intensity.Override(1f);
        distortionProfile.Add<LensDistortion>(true).intensity.Override(-0.35f);

        GameObject volumeObject = new GameObject("OverloadDistortion");
        volumeObject.transform.SetParent(transform, false);
        distortionVolume = volumeObject.AddComponent<Volume>();
        distortionVolume.isGlobal = true;
        distortionVolume.priority = 100f;
        distortionVolume.weight = 0f;
        distortionVolume.sharedProfile = distortionProfile;
    }

    // Si el efecto CRT está activo, la curvatura de la pantalla ondula
    private void WobbleCRT(float severity, float wobble)
    {
        if (severity <= 0f || !CRT_Controller.isEnable)
        {
            RestoreCRT();
            return;
        }

        if (crtBaseCurvature < 0f)
        {
            crtBaseCurvature = CRT_Controller.GetCurvature();
        }

        CRT_Controller.SetCurvature(crtBaseCurvature + (wobble - 0.5f) * 0.6f * severity);
    }

    // El material del CRT es un asset: hay que dejarle su curvatura original
    private void RestoreCRT()
    {
        if (crtBaseCurvature < 0f)
        {
            return;
        }

        if (CRT_Controller.IsInitialized)
        {
            CRT_Controller.SetCurvature(crtBaseCurvature);
        }

        crtBaseCurvature = -1f;
    }

    // ---------- Derrota ----------

    // Si demasiadas zonas llegan a 0% a la vez, la nave explota
    private void CheckDefeat()
    {
        int zonesAtZero = 0;
        foreach (ShipState.SystemState system in ShipState.Systems)
        {
            if (system.stability <= 0f)
            {
                zonesAtZero++;
            }
        }

        if (zonesAtZero >= ShipState.Config.zonesAtZeroToLose)
        {
            ShipState.Destroy();
        }
    }

    // Al perder: si estás dentro te lleva fuera a ver la explosión, sale "NAVE DESTRUIDA"
    // y después vuelve al menú principal para empezar de nuevo
    private void UpdateDestruction()
    {
        if (returningToMenu || SceneTransition.IsTransitioning)
        {
            return;
        }

        if (!ShipState.IsPiloting)
        {
            SceneTransition.Load(ShipState.Config.pilotingScene, 1.5f);
            return;
        }

        destroyedTimer += Time.deltaTime;
        if (destroyedTimer >= 2f && !gameOverShown)
        {
            gameOverShown = true;
            ShowGameOver();
        }

        if (destroyedTimer >= 5f)
        {
            returningToMenu = true;
            SceneTransition.Load(ShipState.Config.menuScene, 1.5f);
        }
    }

    // Texto grande en el centro. Se crea en la escena actual, así que desaparece al irse al menú.
    private static void ShowGameOver()
    {
        GameObject gameOverObject = new GameObject("GameOver");
        Canvas canvas = MinigameUI.CreateCanvas("GameOverCanvas", gameOverObject.transform);
        canvas.sortingOrder = 80;

        Text title = MinigameUI.CreateText("Title", canvas.transform, "NAVE DESTRUIDA", 96, new Vector2(0f, 40f), new Vector2(1600f, 140f));
        title.color = new Color(1f, 0.25f, 0.2f);
        title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4f, -4f);
        MinigameUI.CreateText("Subtitle", canvas.transform, "El pedido no llegará a su destino...", 32, new Vector2(0f, -50f), new Vector2(1600f, 60f));

        CanvasGroup group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.DOFade(1f, 0.8f);
        title.transform.localScale = Vector3.one * 1.4f;
        title.transform.DOScale(1f, 0.8f).SetEase(Ease.OutCubic);
    }
}
