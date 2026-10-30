using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Progreso del viaje hasta la entrega: una barra arriba de la pantalla que la nave recorre
// de izquierda a derecha. Avanza según la velocidad de la nave (ShipMovement.TravelSpeed),
// así que si algo la frena, el viaje tarda más. Al llegar al final lanza onArrived.
// El largo del viaje está en Resources/ShipSystems (sección Viaje).
// La UI la crea Tools > Nave > Añadir barra de progreso del viaje.
public class DeliveryDistance : MonoBehaviour
{
    [Tooltip("Si se deja vacío, busca la nave en la escena")]
    [SerializeField] private ShipMovement ship;

    [Header("Salidas")]
    public UnityEvent onArrived;

    [Header("UI (la crea la herramienta)")]
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private RectTransform progressFill;
    [Tooltip("Icono de la nave que recorre la barra")]
    [SerializeField] private RectTransform shipIcon;

    private float distance;
    private bool hasArrived;
    private CanvasGroup barGroup;

    public float Distance => distance;

    // Radar / navegación averiados (0 a 1): el icono tiembla y la barra se desvanece. Lo pone ShipFailureEffects.
    public float SignalNoise { get; set; }

    // 0 = recién salidos, 1 = pedido entregado
    public float Progress => 1f - distance / ShipState.Config.journeyDistance;

    public GameObject UIRoot => uiRoot;

    private void Awake()
    {
        // Si venimos de la escena de primera persona, sigue el viaje por donde iba
        float journeyDistance = ShipState.Config.journeyDistance;
        distance = ShipState.Distance >= 0f ? ShipState.Distance : journeyDistance;
        hasArrived = distance <= 0f;
        ShipState.TotalDistance = journeyDistance;

        if (ship == null)
        {
            ship = FindFirstObjectByType<ShipMovement>();
        }

        // Para poder desvanecer la barra entera cuando se pierde el rumbo
        if (progressFill != null)
        {
            GameObject bar = progressFill.parent.gameObject;
            barGroup = bar.GetComponent<CanvasGroup>();
            if (barGroup == null)
            {
                barGroup = bar.AddComponent<CanvasGroup>();
            }
        }
    }

    private void Update()
    {
        // Solo avanza mientras la nave se está pilotando (no durante las animaciones de cámara)
        bool isPiloting = ship == null || (ship.isActiveAndEnabled && !ship.IsDestroyed);
        if (!hasArrived && isPiloting)
        {
            float speed = ship != null ? ship.TravelSpeed : 1f;
            distance = Mathf.Max(0f, distance - ShipState.Config.kilometersPerSecond * speed * Time.deltaTime);

            if (distance <= 0f)
            {
                hasArrived = true;
                onArrived.Invoke();
            }
        }

        // ShipState la guarda entre escenas y la usa la curva de dificultad
        ShipState.Distance = distance;

        if (shipIcon != null)
        {
            // Sin rumbo, el icono salta de un lado a otro y la barra se apaga y parpadea
            float shownProgress = Progress + Random.Range(-1f, 1f) * SignalNoise * 0.15f;
            PlaceOnBar(Mathf.Clamp01(shownProgress));
            barGroup.alpha = Random.value < SignalNoise * 0.3f ? 0f : 1f - SignalNoise * 0.7f;
        }
    }

    // El relleno y el icono de la nave se colocan con anclas: 0 = izquierda, 1 = derecha
    private void PlaceOnBar(float progress)
    {
        MinigameUI.SetBar(progressFill, progress);
        shipIcon.anchorMin = new Vector2(progress, 0.5f);
        shipIcon.anchorMax = shipIcon.anchorMin;
        shipIcon.anchoredPosition = Vector2.zero;
    }

    // Solo la usa la herramienta del editor
    public void CreateUI()
    {
        Canvas canvas = MinigameUI.CreateCanvas("ProgressCanvas", transform);
        canvas.sortingOrder = 40;
        uiRoot = canvas.gameObject;

        // Barra arriba al centro
        progressFill = MinigameUI.CreateBar("ProgressBar", canvas.transform, new Color(0.15f, 0.17f, 0.22f, 0.9f), new Color(0.3f, 0.6f, 1f), Vector2.zero, new Vector2(700f, 10f));
        RectTransform bar = (RectTransform)progressFill.parent;
        bar.anchorMin = new Vector2(0.5f, 1f);
        bar.anchorMax = new Vector2(0.5f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.anchoredPosition = new Vector2(0f, -40f);

        // Punto de salida y planeta del pedido (placeholders)
        MinigameUI.CreateImage("Origin", bar, new Color(0.6f, 0.6f, 0.65f), new Vector2(-350f, -5f), new Vector2(14f, 14f));
        MinigameUI.CreateImage("Destination", bar, new Color(0.3f, 0.9f, 0.5f), new Vector2(350f, -5f), new Vector2(26f, 26f));

        // Icono de la nave: cámbialo por un sprite de la nave
        shipIcon = MinigameUI.CreateGroup("ShipIcon", bar, Vector2.zero, new Vector2(40f, 24f));
        Image icon = MinigameUI.CreateImage("Icon", shipIcon, Color.white, Vector2.zero, new Vector2(34f, 16f));
        MinigameUI.CreateImage("Cockpit", icon.transform, new Color(0.3f, 0.8f, 1f), new Vector2(10f, 0f), new Vector2(10f, 8f));
        PlaceOnBar(0f);
    }
}
