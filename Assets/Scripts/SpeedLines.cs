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

    private ParticleSystem lines;
    private float baseSimulationSpeed;
    private float currentSpeed = 1f;

    private void Awake()
    {
        lines = GetComponent<ParticleSystem>();
        baseSimulationSpeed = lines.main.simulationSpeed;

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

        // La velocidad de simulación mueve las líneas más rápido o más lento (y salen más o menos)
        ParticleSystem.MainModule main = lines.main;
        main.simulationSpeed = baseSimulationSpeed * Mathf.Max(currentSpeed, minSimulationSpeed);

        ParticleSystem.EmissionModule emission = lines.emission;
        emission.enabled = currentSpeed > 0.05f;
    }
}
