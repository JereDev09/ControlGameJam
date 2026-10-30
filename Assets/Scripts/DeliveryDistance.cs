using UnityEngine;
using UnityEngine.Events;

// Distancia (falsa) que falta para entregar el pedido. Baja poco a poco mientras se pilota
// la nave, más rápido al acelerar, y se para si la nave se destruye. Al llegar a 0 lanza onArrived.
public class DeliveryDistance : MonoBehaviour
{
    [Tooltip("Si se deja vacío, busca la nave en la escena")]
    [SerializeField] private ShipMovement ship;
    [SerializeField] private float startDistance = 25000f;
    [Tooltip("Kilómetros que baja por segundo")]
    [SerializeField] private float kilometersPerSecond = 120f;
    [Tooltip("Cuánto más rápido baja mientras la nave acelera (Sprint)")]
    [SerializeField] private float boostMultiplier = 1.8f;

    [Header("Salidas")]
    public UnityEvent onArrived;

    private GUIStyle distanceStyle;
    private float distance;
    private bool hasArrived;

    public float Distance => distance;

    // 0 = recién salidos, 1 = pedido entregado
    public float Progress => 1f - distance / startDistance;

    private void Awake()
    {
        distance = startDistance;

        if (ship == null)
        {
            ship = FindFirstObjectByType<ShipMovement>();
        }
    }

    private void Update()
    {
        bool isPiloting = ship == null || !ship.IsDestroyed;
        if (hasArrived || !isPiloting)
        {
            return;
        }

        float speed = kilometersPerSecond * (ship != null && ship.IsBoosting ? boostMultiplier : 1f);
        distance = Mathf.Max(0f, distance - speed * Time.deltaTime);

        if (distance <= 0f)
        {
            hasArrived = true;
            onArrived.Invoke();
        }
    }

    private void OnGUI()
    {
        if (distanceStyle == null)
        {
            distanceStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        // Arriba a la derecha, con el mismo estilo que la barra de vida
        Rect boxRect = new Rect(Screen.width - 236f, 16f, 220f, 28f);
        Color previousColor = GUI.color;

        GUI.color = new Color(0.08f, 0.1f, 0.12f, 0.85f);
        GUI.Box(boxRect, GUIContent.none);
        GUI.color = Color.white;
        string text = hasArrived ? "¡PEDIDO ENTREGADO!" : $"DESTINO {Mathf.CeilToInt(distance):N0} km";
        GUI.Label(boxRect, text, distanceStyle);
        GUI.color = previousColor;
    }
}
