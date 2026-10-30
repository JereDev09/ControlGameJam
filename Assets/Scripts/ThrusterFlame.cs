using UnityEngine;

// Fuego de un propulsor: escala el pivote en Z con ruido para que parezca que está propulsando.
// Además se alarga al acelerar (Sprint), da un fogonazo al esquivar, brilla con el ruido
// y se apaga si la nave se destruye. Va en cada PivoteFuego (Tools > Nave).
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
    [SerializeField] private float boostLength = 1.6f;
    [SerializeField] private float dodgeLength = 2.2f;
    [SerializeField] private float responseSpeed = 10f;

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
        power = Mathf.Lerp(power, GetTargetPower(), responseSpeed * Time.deltaTime);

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

        if (ship.IsDodging)
        {
            return dodgeLength;
        }

        return ship.IsBoosting ? boostLength : 1f;
    }
}
