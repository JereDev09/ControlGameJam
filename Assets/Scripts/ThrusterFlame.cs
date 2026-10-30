using UnityEngine;

// Fuego de un propulsor: escala el pivote en Z con ruido para que parezca que está propulsando.
// Su largo sigue a la velocidad de la nave (ShipMovement.TravelSpeed): más largo al acelerar,
// más corto si la nave va frenada y apagado si se destruye. Cuando el motor averiado da un
// tirón, se apaga de golpe y chisporrotea. Va en cada PivoteFuego (Tools > Nave).
public class ThrusterFlame : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Tooltip("Si se deja vacío, busca la nave en los padres")]
    [SerializeField] private ShipMovement ship;

    [Header("Ruido")]
    [SerializeField] private float noiseSpeed = 15f;
    [Tooltip("Cuánto cambia el largo con el ruido (0.3 = ±30%)")]
    [SerializeField, Range(0f, 1f)] private float lengthNoise = 0.3f;
    [Tooltip("Cuánto cambia el grosor con el ruido")]
    [SerializeField, Range(0f, 0.5f)] private float widthNoise = 0.08f;

    [Header("Reacción a la nave")]
    [SerializeField] private float responseSpeed = 10f;
    [Tooltip("Lo rápido que se apaga el fuego cuando el motor da un tirón")]
    [SerializeField] private float stallResponseSpeed = 30f;

    [Header("Brillo (emisión del material)")]
    [Tooltip("Si se deja vacío, usa el Renderer de los hijos")]
    [SerializeField] private Renderer flameRenderer;
    [SerializeField, Range(0f, 1f)] private float glowNoise = 0.35f;

    private Vector3 baseScale;
    private float noiseSeed;
    private float power = 1f;
    private MaterialPropertyBlock propertyBlock;
    private Color baseEmission;

    private void Awake()
    {
        baseScale = transform.localScale;
        noiseSeed = Random.value * 100f; // cada fuego con su propio ruido, para que no vayan sincronizados

        if (ship == null)
        {
            ship = GetComponentInParent<ShipMovement>();
        }

        if (flameRenderer == null)
        {
            flameRenderer = GetComponentInChildren<Renderer>();
        }

        if (flameRenderer != null && flameRenderer.sharedMaterial.HasProperty(EmissionColorId))
        {
            propertyBlock = new MaterialPropertyBlock();
            baseEmission = flameRenderer.sharedMaterial.GetColor(EmissionColorId);
        }
    }

    private void Update()
    {
        // Suaviza los cambios de potencia para que el fuego no salte de golpe
        // (salvo cuando el motor se cala: entonces se apaga casi al instante)
        bool stalled = ship != null && ship.EngineStalled;
        power = Mathf.Lerp(power, GetTargetPower(), (stalled ? stallResponseSpeed : responseSpeed) * Time.deltaTime);

        // PerlinNoise da un valor suave entre 0 y 1 que cambia con el tiempo; lo pasamos a -1..1
        float lengthWave = Mathf.PerlinNoise(Time.time * noiseSpeed, noiseSeed) * 2f - 1f;
        float widthWave = Mathf.PerlinNoise(noiseSeed, Time.time * noiseSpeed) * 2f - 1f;

        float length = power * (1f + lengthWave * lengthNoise);
        float width = Mathf.Min(power, 1f) * (1f + widthWave * widthNoise);
        transform.localScale = new Vector3(baseScale.x * width, baseScale.y * width, baseScale.z * length);

        // El brillo sigue al mismo ruido, así el fuego "parpadea"
        if (propertyBlock != null)
        {
            flameRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, baseEmission * power * (1f + lengthWave * glowNoise));
            flameRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private float GetTargetPower()
    {
        if (ship == null)
        {
            return 1f;
        }

        if (ship.IsDestroyed)
        {
            return 0f;
        }

        // Motor calado: el fuego se apaga y a ratos chisporrotea, como intentando arrancar
        if (ship.EngineStalled)
        {
            return Random.value < 0.08f ? 0.6f : 0f;
        }

        return ship.TravelSpeed;
    }
}
