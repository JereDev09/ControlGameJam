using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Efectos de las averías en primera persona:
// - Electricidad: las luces parpadean y se apagan (las terminales sin energía las controla Minigame).
// - Presión: la vista se nubla y pierde color, como si faltara el aire.
// - Sobrecarga: cada explosión interna se nota con un temblor y un destello.
// - Si la nave explota estando dentro: temblor fuerte y destello rojo antes de salir a verlo.
// El mapa con interferencias (radar / navegación) lo hace el StabilityHUD.
// Son progresivos: suaves al bajar del umbral de peligro y máximos al llegar a 0%.
// Lo añade Tools > Minijuegos > Añadir efectos de averías (interior).
public class InteriorFailureEffects : MonoBehaviour
{
    [Tooltip("Si se deja vacío, usa la cámara principal")]
    [SerializeField] private Camera playerCamera;

    [Header("Presión (vista borrosa)")]
    [Tooltip("A partir de qué distancia se ve borroso con la presión a 0%")]
    [SerializeField] private float blurStartDistance = 0.5f;
    [SerializeField] private float blurEndDistance = 4f;

    private Light[] lights;
    private float[] baseIntensities;
    private float baseAmbient;
    private Volume blurVolume;
    private VolumeProfile blurProfile;
    private Image flash;
    private bool destructionShown;

    private void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Solo las luces de esta escena (no las de PersistentGameplay)
        lights = System.Array.FindAll(FindObjectsByType<Light>(FindObjectsSortMode.None), sceneLight => sceneLight.gameObject.scene == gameObject.scene);
        baseIntensities = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            baseIntensities[i] = lights[i].intensity;
        }

        baseAmbient = RenderSettings.ambientIntensity;
        CreateBlurVolume();
        CreateFlash();
        FailureDirector.OverloadExploded += OnOverloadExploded;
    }

    private void OnDestroy()
    {
        FailureDirector.OverloadExploded -= OnOverloadExploded;
        RenderSettings.ambientIntensity = baseAmbient;

        if (blurProfile != null)
        {
            Destroy(blurProfile);
        }
    }

    private void Update()
    {
        UpdateLights(ShipState.GetSeverity(ShipZones.Electricity));
        UpdateBlur(ShipState.GetSeverity(ShipZones.Pressure));

        if (ShipState.IsDestroyed && !destructionShown)
        {
            destructionShown = true;
            Shake(0.3f, 1.5f);
            Flash(new Color(1f, 0.15f, 0.1f, 0.7f));
        }
    }

    // Electricidad: las luces parpadean; cuanto peor, más tiempo están apagadas
    private void UpdateLights(float severity)
    {
        float noise = Mathf.PerlinNoise(Time.time * 7f, 0f);
        float power = severity <= 0f || noise > severity * 0.85f ? 1f : 0.05f;

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
            {
                lights[i].intensity = baseIntensities[i] * power;
            }
        }

        RenderSettings.ambientIntensity = baseAmbient * Mathf.Lerp(0.15f, 1f, power);
    }

    // Presión: la vista se nubla con un "latido" lento, como si faltara el aire
    private void UpdateBlur(float severity)
    {
        float breathing = Mathf.Lerp(0.6f, 1f, (Mathf.Sin(Time.time * 1.5f) + 1f) / 2f);
        blurVolume.weight = severity * breathing;
    }

    // Sobrecarga: temblor y destello naranja
    private void OnOverloadExploded()
    {
        Shake(0.12f, 0.5f);
        Flash(new Color(1f, 0.6f, 0.2f, 0.5f));
    }

    private void Shake(float strength, float duration)
    {
        if (playerCamera == null)
        {
            return;
        }

        Transform cameraTransform = playerCamera.transform;
        cameraTransform.DOComplete();
        cameraTransform.DOShakePosition(duration, strength, 25).SetLink(cameraTransform.gameObject);
    }

    private void Flash(Color color)
    {
        flash.DOKill();
        flash.color = color;
        flash.DOFade(0f, 0.8f);
    }

    // Volume creado por código con desenfoque y algo menos de color. Su "weight" decide cuánto se ve.
    private void CreateBlurVolume()
    {
        blurProfile = ScriptableObject.CreateInstance<VolumeProfile>();

        DepthOfField depthOfField = blurProfile.Add<DepthOfField>(true);
        depthOfField.mode.Override(DepthOfFieldMode.Gaussian);
        depthOfField.gaussianStart.Override(blurStartDistance);
        depthOfField.gaussianEnd.Override(blurEndDistance);
        depthOfField.gaussianMaxRadius.Override(1.5f);

        // Solo se cambia la saturación, el resto de ajustes de color de la escena se respetan
        blurProfile.Add<ColorAdjustments>().saturation.Override(-40f);

        GameObject volumeObject = new GameObject("PressureBlur");
        volumeObject.transform.SetParent(transform, false);
        blurVolume = volumeObject.AddComponent<Volume>();
        blurVolume.isGlobal = true;
        blurVolume.priority = 100f;
        blurVolume.weight = 0f;
        blurVolume.sharedProfile = blurProfile;
    }

    // Imagen a pantalla completa para los destellos
    private void CreateFlash()
    {
        Canvas canvas = MinigameUI.CreateCanvas("FlashCanvas", transform);
        canvas.sortingOrder = 60;

        flash = MinigameUI.CreateImage("Flash", canvas.transform, new Color(1f, 1f, 1f, 0f), Vector2.zero, Vector2.zero);
        MinigameUI.Stretch(flash.rectTransform);
        flash.raycastTarget = false;
    }
}
