using UnityEngine;

// Hace que las líneas de velocidad (un ParticleSystem) vayan según la velocidad de la nave:
// más rápidas al acelerar, más lentas si va frenada y sin líneas nuevas si se para o explota.
// Va en el objeto "Lineas de velocidad" (Tools > Nave).
[RequireComponent(typeof(ParticleSystem))]
public class SpeedLines : MonoBehaviour
{
    [Tooltip("Si se deja vacío, busca la nave en la escena")]
    [SerializeField] private ShipMovement ship;
    [Tooltip("Velocidad mínima de las líneas que ya están en pantalla, para que no se queden congeladas")]
    [SerializeField, Range(0f, 1f)] private float minSimulationSpeed = 0.2f;
    [SerializeField] private float responseSpeed = 4f;
    [SerializeField, Min(1f)] private float boostEmissionMultiplier = 1.7f;
    [SerializeField, Range(0f, 1f)] private float opacity = 0.3f;

    private ParticleSystem lines;
    private Renderer lineRenderer;
    private MaterialPropertyBlock colorBlock;
    private float baseSimulationSpeed;
    private float baseEmissionRate;
    private float currentSpeed = 1f;
    private float currentBoostBlend;

    private void Awake()
    {
        lines = GetComponent<ParticleSystem>();
        lineRenderer = GetComponent<ParticleSystemRenderer>();
        lineRenderer.rendererPriority = -1;
        colorBlock = new MaterialPropertyBlock();
        lineRenderer.GetPropertyBlock(colorBlock);
        Color tint = new Color(1f, 1f, 1f, opacity);
        colorBlock.SetColor("_BaseColor", tint);
        colorBlock.SetColor("_Color", tint);
        lineRenderer.SetPropertyBlock(colorBlock);
        baseSimulationSpeed = lines.main.simulationSpeed;
        baseEmissionRate = lines.emission.rateOverTimeMultiplier;

        if (ship == null)
        {
            ship = FindFirstObjectByType<ShipMovement>();
        }
    }

    private void Update()
    {
        // Suaviza los cambios para que las líneas no aceleren de golpe
        float targetSpeed = ship != null ? ship.TravelSpeed : 1f;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, responseSpeed * Time.deltaTime);
        float targetBoostBlend = ship != null && ship.IsBoosting ? 1f : 0f;
        currentBoostBlend = Mathf.Lerp(currentBoostBlend, targetBoostBlend, responseSpeed * Time.deltaTime);

        // La velocidad de simulación mueve las líneas más rápido o más lento (y salen más o menos)
        ParticleSystem.MainModule main = lines.main;
        main.simulationSpeed = baseSimulationSpeed * Mathf.Max(currentSpeed, minSimulationSpeed);

        ParticleSystem.EmissionModule emission = lines.emission;
        emission.enabled = currentSpeed > 0.05f;
        emission.rateOverTimeMultiplier = baseEmissionRate * Mathf.Lerp(1f, boostEmissionMultiplier, currentBoostBlend);
    }
}
