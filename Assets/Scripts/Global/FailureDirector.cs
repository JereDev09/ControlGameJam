using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Lo que pasa con las averías en cualquier escena (se crea solo y sobrevive a los cambios de escena):
// - Decide cuándo se avería cada zona según la curva de dificultad: al principio falla una sola
//   cosa de vez en cuando; cerca del destino, varias y más rápido. Con varias averiadas, las
//   siguientes a la primera bajan más despacio, salvo muy cerca del destino.
//   Mientras dura el tutorial (ShipTutorial) no hay averías al azar.
// - Presión: la nave pierde vida poco a poco.
// - Sobrecarga: la imagen se distorsiona y hay explosiones internas que quitan vida.
// - Derrota: si varias zonas están críticas a la vez (tras una cuenta atrás) o la vida llega a 0,
//   la nave explota: si estás dentro te saca a verlo y luego pasa a la escena de la derrota.
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
    private bool leavingAfterDestruction;
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
        // En el menú principal y al llegar al destino ya no pasa nada
        if (MainMenu.IsOpen || ShipState.HasArrived)
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
        // Durante el tutorial las averías las decide ShipTutorial: bajan deprisa (pero poco a poco)
        // hasta el nivel del tutorial y ahí se quedan
        if (!ShipState.TutorialFinished)
        {
            nextFailureTimer = config.firstFailureDelay;
            foreach (ShipState.SystemState system in ShipState.Systems)
            {
                if (system.isFailing && system.stability > config.tutorialBreakStability)
                {
                    float drop = config.tutorialDropSpeed * Time.deltaTime;
                    system.stability = Mathf.Max(config.tutorialBreakStability, system.stability - drop);
                }
            }

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
            ShipState.Break(working[Random.Range(0, working.Count)].name);
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

    // Si varias zonas están en estado crítico a la vez, empieza una cuenta atrás (el HUD la enseña).
    // Si reparas alguna a tiempo se cancela; si no, la nave explota.
    private void CheckDefeat()
    {
        ShipSystems config = ShipState.Config;
        int criticalZones = 0;
        foreach (ShipState.SystemState system in ShipState.Systems)
        {
            if (system.stability <= config.criticalStability)
            {
                criticalZones++;
            }
        }

        // Durante el tutorial no se puede perder así
        if (!ShipState.TutorialFinished || criticalZones < config.criticalZonesToLose)
        {
            ShipState.MeltdownTimeLeft = -1f;
            return;
        }

        if (ShipState.MeltdownTimeLeft < 0f)
        {
            ShipState.MeltdownTimeLeft = config.meltdownSeconds;
        }

        ShipState.MeltdownTimeLeft -= Time.deltaTime;
        if (ShipState.MeltdownTimeLeft <= 0f)
        {
            ShipState.MeltdownTimeLeft = -1f;
            ShipState.Destroy();
        }
    }

    // Al perder: si estás dentro te lleva fuera a ver la explosión y después pasa a la escena
    // de la derrota (que vuelve sola al menú principal)
    private void UpdateDestruction()
    {
        if (leavingAfterDestruction || SceneTransition.IsTransitioning)
        {
            return;
        }

        if (!ShipState.IsPiloting)
        {
            SceneTransition.Load(ShipState.Config.pilotingScene, 1.5f);
            return;
        }

        // Unos segundos para ver la explosión
        destroyedTimer += Time.deltaTime;
        if (destroyedTimer >= 3.5f)
        {
            leavingAfterDestruction = true;
            SceneTransition.Load(GetSceneOrMenu(ShipState.Config.gameOverScene), 1.5f);
        }
    }

    // Si la escena no está en la lista de la build (por ejemplo, aún no se ha creado), se va al menú
    public static string GetSceneOrMenu(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            return sceneName;
        }

        Debug.LogWarning($"La escena '{sceneName}' no está en la lista de escenas de la build. Se vuelve al menú.");
        return ShipState.Config.menuScene;
    }
}
