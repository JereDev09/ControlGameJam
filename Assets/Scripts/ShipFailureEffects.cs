using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Efectos de las averías mientras se pilota (va en la nave, junto a ShipMovement):
// - Motor: tirones; la nave pierde casi toda la velocidad a golpes y los fuegos se apagan.
// - Circuitos: los controles dejan de responder a ratos y saltan chispas de la nave.
// - Piezas: los asteroides hacen hasta el doble de daño y la nave echa humo.
// - Electricidad: la pantalla se oscurece levemente a ratos.
// - Radar / Navegación: la barra del viaje tiembla y se desvanece (rumbo perdido).
// - Sobrecarga: cada explosión interna se nota con chispas y un temblor.
// - Y al chocar con un asteroide: destello rojo en la pantalla y chispas.
// Son progresivos: suaves al bajar del umbral de peligro y máximos al llegar a 0%.
// Lo añade Tools > Nave > Añadir efectos de averías (que también crea las chispas y el humo).
public class ShipFailureEffects : MonoBehaviour
{
    [Header("Referencias (si se dejan vacías se buscan solas)")]
    [SerializeField] private ShipMovement ship;
    [SerializeField] private DeliveryDistance delivery;
    [SerializeField] private Camera shipCamera;

    [Header("Partículas (las crea la herramienta)")]
    [SerializeField] private ParticleSystem sparks;
    [SerializeField] private ParticleSystem smoke;

    [Header("Ajustes")]
    [Tooltip("Velocidad de la nave durante un tirón del motor (1 = normal)")]
    [SerializeField, Range(0f, 1f)] private float stallSpeed = 0.15f;
    [SerializeField] private float shakeStrength = 0.3f;
    [SerializeField, Range(0f, 0.3f)] private float blackoutMaxAlpha = 0.16f;

    private float baseSpeedMultiplier;
    private float stallTimer;
    private float stallRemaining;
    private float jamTimer;
    private float jamRemaining;
    private float blackoutRemaining;
    private float blackoutAlpha;
    private Image blackout;
    private Image damageFlash;
    private Tween shake;

    private void Start()
    {
        if (ship == null)
        {
            ship = FindFirstObjectByType<ShipMovement>();
        }

        if (delivery == null)
        {
            delivery = FindFirstObjectByType<DeliveryDistance>();
        }

        if (shipCamera == null)
        {
            shipCamera = Camera.main;
        }

        baseSpeedMultiplier = ship.SpeedMultiplier;
        CreateBlackout();
        FailureDirector.OverloadExploded += OnOverloadExploded;
        ship.Damaged += OnShipDamaged;
    }

    private void OnDestroy()
    {
        FailureDirector.OverloadExploded -= OnOverloadExploded;
        if (ship != null)
        {
            ship.Damaged -= OnShipDamaged;
        }
    }

    // Al chocar: destello rojo en la pantalla y chispas saltando de la nave
    private void OnShipDamaged()
    {
        damageFlash.DOKill();
        damageFlash.color = new Color(1f, 0.1f, 0.05f, 0.45f);
        damageFlash.DOFade(0f, 0.5f).SetLink(damageFlash.gameObject);

        if (sparks != null)
        {
            sparks.Emit(30);
        }
    }

    private void Update()
    {
        if (ship.IsDestroyed)
        {
            ship.ControlsJammed = false;
            ship.EngineStalled = false;
            blackoutAlpha = 0f;
            blackout.enabled = false;
            blackout.color = new Color(0f, 0f, 0f, 0f);
            SetEmission(sparks, 0f);
            SetEmission(smoke, 0f);
            return;
        }

        UpdateEngine(ShipState.GetSeverity(ShipZones.Engine));
        UpdateCircuits(ShipState.GetSeverity(ShipZones.Circuits));
        UpdateParts(ShipState.GetSeverity(ShipZones.Parts));
        UpdateElectricity(ShipState.GetSeverity(ShipZones.Electricity));

        // Radar y navegación van juntos: si falla cualquiera de los dos se pierde el rumbo
        float navigation = Mathf.Max(ShipState.GetSeverity(ShipZones.Radar), ShipState.GetSeverity(ShipZones.Navigation));
        if (delivery != null)
        {
            delivery.SignalNoise = navigation;
        }
    }

    // Motor: cada cierto tiempo se cala un momento: la nave pierde casi toda la velocidad,
    // los fuegos se apagan (ThrusterFlame) y suelta una bocanada de humo y chispas
    private void UpdateEngine(float severity)
    {
        stallTimer -= Time.deltaTime;
        stallRemaining -= Time.deltaTime;

        if (severity > 0f && stallTimer <= 0f)
        {
            // Cuanto peor está el motor, más seguidos y más largos son los tirones
            stallTimer = Random.Range(2f, 5f) * (1.3f - severity);
            stallRemaining = Mathf.Lerp(0.3f, 0.9f, severity);
            Shake(0.4f);

            if (smoke != null)
            {
                smoke.Emit(12);
            }

            if (sparks != null)
            {
                sparks.Emit(15);
            }
        }

        bool stalled = stallRemaining > 0f;
        ship.EngineStalled = stalled;
        ship.SpeedMultiplier = baseSpeedMultiplier * (stalled ? stallSpeed : 1f);
    }

    // Circuitos: los controles dejan de responder a ratos, con chispas
    private void UpdateCircuits(float severity)
    {
        jamTimer -= Time.deltaTime;
        jamRemaining -= Time.deltaTime;

        if (severity > 0f && jamTimer <= 0f)
        {
            jamTimer = Random.Range(2f, 5f) * (1.3f - severity);
            jamRemaining = Mathf.Lerp(0.3f, 1f, severity);
            if (sparks != null)
            {
                sparks.Emit(25);
            }
        }

        ship.ControlsJammed = jamRemaining > 0f;
        SetEmission(sparks, severity * 15f); // además, alguna chispa suelta todo el rato
    }

    // Piezas: los golpes hacen más daño y la nave echa humo
    private void UpdateParts(float severity)
    {
        ship.DamageMultiplier = 1f + severity;
        SetEmission(smoke, severity * 12f);
    }

    // Electricidad: la imagen se oscurece brevemente y de forma sutil cuando hay una caída.
    private void UpdateElectricity(float severity)
    {
        blackoutRemaining -= Time.deltaTime;

        if (severity > 0f && blackoutRemaining <= 0f && Random.value < severity * 0.5f * Time.deltaTime)
        {
            blackoutRemaining = Random.Range(0.1f, 0.2f) * (0.5f + severity);
        }

        float targetAlpha = blackoutRemaining > 0f ? blackoutMaxAlpha * severity : 0f;
        blackoutAlpha = Mathf.MoveTowards(blackoutAlpha, targetAlpha, Time.deltaTime * 2f);
        blackout.color = new Color(0f, 0f, 0f, blackoutAlpha);
        blackout.enabled = blackoutAlpha > 0.005f;
    }

    // Sobrecarga: cada explosión interna se nota con chispas y un temblor fuerte
    private void OnOverloadExploded()
    {
        if (sparks != null)
        {
            sparks.Emit(50);
        }

        Shake(1.2f);
    }

    private void Shake(float strength)
    {
        // Solo mientras se pilota, para no estropear las animaciones de cámara de entrar y salir
        if (shipCamera == null || !ship.enabled)
        {
            return;
        }

        shake?.Complete();
        shake = shipCamera.transform.DOShakePosition(0.3f, shakeStrength * strength, 25).SetLink(shipCamera.gameObject);
    }

    private static void SetEmission(ParticleSystem particles, float rate)
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTimeMultiplier = rate;
    }

    // Oscurecimiento sutil bajo el HUD para que el aviso siga siendo legible.
    private void CreateBlackout()
    {
        Canvas canvas = MinigameUI.CreateCanvas("BlackoutCanvas", transform);
        canvas.sortingOrder = 49;

        blackout = MinigameUI.CreateImage("Blackout", canvas.transform, new Color(0f, 0f, 0f, 0f), Vector2.zero, Vector2.zero);
        MinigameUI.Stretch(blackout.rectTransform);
        blackout.raycastTarget = false;
        blackout.enabled = false;

        // Destello rojo al chocar (empieza transparente)
        damageFlash = MinigameUI.CreateImage("DamageFlash", canvas.transform, new Color(1f, 0.1f, 0.05f, 0f), Vector2.zero, Vector2.zero);
        MinigameUI.Stretch(damageFlash.rectTransform);
        damageFlash.raycastTarget = false;
    }
}
