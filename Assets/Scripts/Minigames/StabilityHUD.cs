using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// HUD de la nave, para las dos escenas (pilotar y primera persona):
// - Alerta: si alguna zona baja del umbral, viñeta tenue y un aviso breve arriba.
// - Minimapa redondo (arriba a la derecha, solo en primera persona): gira con tu vista; las zonas
//   lejanas se quedan pegadas al borde apuntando hacia ellas, y las que fallan laten en rojo.
// - Panel con la estabilidad de cada zona (opcional, solo en primera persona; también está la terminal).
// La UI la crea Tools > Minijuegos > Crear o actualizar HUD de estabilidad.
public class StabilityHUD : MonoBehaviour
{
    [Tooltip("Mostrar el panel con todas las zonas (solo en primera persona). Si no, se mira en la terminal")]
    [SerializeField] private bool showPanel;
    [Tooltip("Mostrar el mapa (solo en primera persona)")]
    [SerializeField] private bool showMap = true;
    [Tooltip("Metros del mundo que se ven desde el centro del mapa hasta el borde (menos = más zoom)")]
    [SerializeField] private float mapVisibleRadius = 8f;

    [Header("Viñeta sutil de la alerta")]
    [SerializeField] private Color vignetteColor = new Color(0.75f, 0.38f, 0.12f);
    [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.2f;

    [Header("UI (la crea la herramienta)")]
    [Tooltip("Fila desactivada que se copia para cada zona. Sus hijos deben llamarse Name, Bar (con Fill dentro) y Percent")]
    [SerializeField] private RectTransform rowTemplate;
    [SerializeField] private GameObject alertRoot;
    [SerializeField] private CanvasGroup alertMessage;
    [SerializeField] private Text alertText;
    [SerializeField] private GameObject map;
    [Tooltip("Zona del mapa donde se colocan los puntos (recorta lo que se sale)")]
    [SerializeField] private RectTransform mapArea;
    [Tooltip("Fondo que gira con el jugador. Representa la zona de mapMin a mapMax de Resources/ShipSystems: ponle un sprite con el plano de la nave")]
    [SerializeField] private RectTransform mapBackground;
    [Tooltip("Punto desactivado que se copia para cada zona. Su hijo Label enseña el nombre cuando falla")]
    [SerializeField] private RectTransform markerTemplate;
    [SerializeField] private RectTransform playerMarker;

    private const float MapEdgeMargin = 12f;

    private class Marker
    {
        public string systemName;
        public Vector3 worldPosition;
        public RectTransform rect;
        public Image image;
        public GameObject label;
        public Tween pulse;
    }

    private readonly List<Marker> markers = new List<Marker>();
    private StabilityRows rows;
    private GameObject panel;
    private Transform player;
    private Volume alertVolume;
    private VolumeProfile alertProfile;
    private bool isAlertShown;

    private void Start()
    {
        rows = new StabilityRows(rowTemplate);
        panel = rowTemplate.parent.gameObject; // el panel es el padre de las filas
        alertText.color = new Color(1f, 0.72f, 0.45f);
        alertText.fontSize = 30;
        alertRoot.SetActive(false);
        CreateAlertVolume();
        CreateMarkers();

        // Si hay jugador estamos en primera persona; si no, pilotando (ahí solo sale la alerta)
        FirstPersonController firstPerson = FindFirstObjectByType<FirstPersonController>();
        player = firstPerson != null ? firstPerson.transform : null;
    }

    private void Update()
    {
        bool isFirstPerson = player != null;
        bool panelVisible = showPanel && isFirstPerson;
        bool mapVisible = showMap && isFirstPerson;
        panel.SetActive(panelVisible);
        map.SetActive(mapVisible);

        if (panelVisible)
        {
            rows.Refresh();
        }

        if (mapVisible)
        {
            UpdateMap();
        }

        UpdateAlert();
    }

    private void OnDestroy()
    {
        // El perfil de la viñeta se creó por código, así que hay que borrarlo a mano
        if (alertProfile != null)
        {
            Destroy(alertProfile);
        }
    }

    // ---------- Alerta ----------

    // La alerta sale cuando alguna zona baja del umbral de peligro (Resources/ShipSystems)
    private void UpdateAlert()
    {
        List<string> compromisedSystems = new List<string>();
        int compromisedCount = 0;
        foreach (ShipSystems.SystemInfo system in ShipState.Config.systems)
        {
            float stability = ShipState.GetStability(system.name);
            if (stability < ShipState.Config.dangerThreshold)
            {
                compromisedCount++;
                if (compromisedSystems.Count < 2)
                {
                    compromisedSystems.Add(system.name);
                }
            }
        }

        bool showAlert = compromisedCount > 0;
        SetAlert(showAlert);

        if (!showAlert)
        {
            return;
        }

        float electricitySeverity = ShipState.GetSeverity(ShipZones.Electricity);
        if (electricitySeverity > 0f)
        {
            alertText.text = ShipState.GetStability(ShipZones.Electricity) <= 0f
                ? "SIN ENERGÍA · REPARA ELECTRICIDAD"
                : "FALLA ELÉCTRICA · REPÁRALA";
            return;
        }

        string systems = string.Join(", ", compromisedSystems);
        if (compromisedCount > compromisedSystems.Count)
        {
            systems += $" +{compromisedCount - compromisedSystems.Count}";
        }

        alertText.text = $"AVERÍA: {systems}";
    }

    // Radar y navegación van juntos: si falla cualquiera de los dos se pierde el rumbo
    private static float GetNavigationSeverity()
    {
        return Mathf.Max(ShipState.GetSeverity(ShipZones.Radar), ShipState.GetSeverity(ShipZones.Navigation));
    }

    private void SetAlert(bool show)
    {
        if (show == isAlertShown)
        {
            return;
        }

        isAlertShown = show;
        alertMessage.DOKill();
        alertVolume.DOKill();
        alertRoot.SetActive(show);

        if (show)
        {
            // Una señal tenue y estable evita tapar la vista o distraer al jugador.
            alertVolume.weight = 0f;
            DOTween.To(() => alertVolume.weight, value => alertVolume.weight = value, 0.25f, 0.4f)
                .SetEase(Ease.OutSine).SetTarget(alertVolume);
            alertMessage.alpha = 1f;
        }
        else
        {
            DOTween.To(() => alertVolume.weight, value => alertVolume.weight = value, 0f, 0.25f).SetTarget(alertVolume);
        }
    }

    // Volume global creado por código que solo tiene una viñeta ámbar sutil.
    // Su "weight" (de 0 a 1) decide cuánto se ve.
    private void CreateAlertVolume()
    {
        alertProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        Vignette vignette = alertProfile.Add<Vignette>(true);
        vignette.color.Override(Color.Lerp(vignetteColor, new Color(1f, 0.58f, 0.24f), 0.75f));
        vignette.intensity.Override(Mathf.Min(vignetteIntensity, 0.2f));
        vignette.smoothness.Override(0.5f);

        GameObject volumeObject = new GameObject("AlertVignette");
        volumeObject.transform.SetParent(transform, false);
        alertVolume = volumeObject.AddComponent<Volume>();
        alertVolume.isGlobal = true;
        alertVolume.priority = 100f; // por encima de los otros Volumes de la escena
        alertVolume.weight = 0f;
        alertVolume.sharedProfile = alertProfile;
    }

    // ---------- Mapa ----------

    private void CreateMarkers()
    {
        foreach (ShipSystems.SystemInfo system in ShipState.Config.systems)
        {
            RectTransform rect = Instantiate(markerTemplate, markerTemplate.parent);
            rect.gameObject.SetActive(true);

            GameObject label = rect.Find("Label").gameObject;
            label.GetComponent<Text>().text = system.name;
            label.SetActive(false);

            markers.Add(new Marker
            {
                systemName = system.name,
                worldPosition = system.position,
                rect = rect,
                image = rect.GetComponent<Image>(),
                label = label
            });
        }

        // El jugador siempre en el centro, mirando hacia arriba, y por encima de los puntos
        playerMarker.anchoredPosition = Vector2.zero;
        playerMarker.localRotation = Quaternion.identity;
        playerMarker.SetAsLastSibling();
    }

    // Minimapa: el jugador está en el centro y el mapa gira con su vista.
    // Las zonas que quedan fuera se pegan al borde, en la dirección en la que están.
    private void UpdateMap()
    {
        UpdateMapBackground();

        // Sin rumbo (radar / navegación averiados), los puntos tiemblan y parpadean
        float noise = GetNavigationSeverity();

        foreach (Marker marker in markers)
        {
            float stability = ShipState.GetStability(marker.systemName);
            bool isFailing = stability < ShipState.Config.dangerThreshold;

            Vector2 interference = Random.insideUnitCircle * noise * 25f;
            marker.rect.anchoredPosition = ClampToEdge(WorldToMinimap(marker.worldPosition) + interference);
            marker.image.enabled = Random.value > noise * 0.5f;
            marker.image.color = isFailing ? Color.red : StabilityRows.GetStabilityColor(stability);

            // Las zonas que fallan laten y enseñan su nombre
            if (isFailing && marker.pulse == null)
            {
                marker.pulse = MinigameFX.Pulse(marker.rect, 1.6f, 0.3f);
                marker.label.SetActive(true);
            }
            else if (!isFailing && marker.pulse != null)
            {
                marker.pulse.Kill();
                marker.pulse = null;
                marker.rect.localScale = Vector3.one;
                marker.label.SetActive(false);
            }
        }
    }

    // El fondo representa la zona del mundo de mapMin a mapMax (en Resources/ShipSystems),
    // así que se mueve y gira igual que los puntos
    private void UpdateMapBackground()
    {
        if (mapBackground == null)
        {
            return;
        }

        ShipSystems config = ShipState.Config;
        Vector2 center = (config.mapMin + config.mapMax) / 2f;
        mapBackground.anchoredPosition = WorldToMinimap(new Vector3(center.x, 0f, center.y));
        mapBackground.sizeDelta = (config.mapMax - config.mapMin) * GetMapScale();
        mapBackground.localRotation = Quaternion.Euler(0f, 0f, player.eulerAngles.y);
    }

    // Pasa una posición del mundo a una posición en el mapa: el jugador en el centro
    // y lo que tiene delante hacia arriba
    private Vector2 WorldToMinimap(Vector3 worldPosition)
    {
        Vector2 offset = new Vector2(worldPosition.x - player.position.x, worldPosition.z - player.position.z);
        float angle = player.eulerAngles.y * Mathf.Deg2Rad;
        Vector2 rotated = new Vector2(
            offset.x * Mathf.Cos(angle) - offset.y * Mathf.Sin(angle),
            offset.x * Mathf.Sin(angle) + offset.y * Mathf.Cos(angle));
        return rotated * GetMapScale();
    }

    // Píxeles del mapa por cada metro del mundo
    private float GetMapScale()
    {
        return mapArea.rect.width / 2f / mapVisibleRadius;
    }

    // Si un punto queda fuera del círculo, lo pega al borde en la misma dirección
    private Vector2 ClampToEdge(Vector2 position)
    {
        float radius = mapArea.rect.width / 2f - MapEdgeMargin;
        return position.magnitude > radius ? position.normalized * radius : position;
    }

    // ---------- Crear la UI (solo la usa la herramienta del editor) ----------

    // circle: sprite redondo para el mapa y sus puntos
    public void CreateUI(Sprite circle)
    {
        Canvas canvas = MinigameUI.CreateCanvas("HUDCanvas", transform);
        canvas.sortingOrder = 50; // por debajo de las ventanas de los minijuegos

        CreatePanel(canvas.transform);
        CreateMap(canvas.transform, circle);
        CreateAlert(canvas.transform);
    }

    // Panel arriba a la izquierda. Las filas se colocan solas una debajo de otra.
    private void CreatePanel(Transform canvas)
    {
        Image panelImage = MinigameUI.CreateImage("Panel", canvas, new Color(0.05f, 0.07f, 0.1f, 0.8f), Vector2.zero, new Vector2(380f, 0f));
        AnchorToCorner(panelImage.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -20f));

        VerticalLayoutGroup layout = panelImage.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 12);
        layout.spacing = 6f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        panelImage.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        MinigameUI.CreateText("Header", panelImage.transform, "ESTABILIDAD DE LA NAVE", 20, Vector2.zero, new Vector2(356f, 28f))
            .alignment = TextAnchor.MiddleLeft;
        rowTemplate = StabilityRows.CreateTemplate(panelImage.transform, 356f, 26f, 18);
    }

    // Minimapa redondo arriba a la derecha
    private void CreateMap(Transform canvas, Sprite circle)
    {
        RectTransform mapRect = MinigameUI.CreateGroup("Map", canvas, Vector2.zero, new Vector2(300f, 330f));
        AnchorToCorner(mapRect, new Vector2(1f, 1f), new Vector2(-20f, -20f));
        map = mapRect.gameObject;

        MinigameUI.CreateText("Title", mapRect, "MAPA DE LA NAVE", 18, new Vector2(0f, 150f), new Vector2(280f, 28f));
        Vector2 center = new Vector2(0f, -12f);

        Image frame = MinigameUI.CreateImage("Frame", mapRect, new Color(0.55f, 0.6f, 0.7f, 0.9f), center, new Vector2(284f, 284f));
        frame.sprite = circle;

        // Círculo que recorta el fondo cuando gira (el Mask usa la forma del sprite)
        Image area = MinigameUI.CreateImage("MapArea", mapRect, new Color(0.03f, 0.04f, 0.06f), center, new Vector2(270f, 270f));
        area.sprite = circle;
        area.gameObject.AddComponent<Mask>();
        mapArea = area.rectTransform;

        // Fondo que gira con el jugador (la zona de la nave). Aquí puedes poner un sprite con el plano.
        mapBackground = MinigameUI.CreateImage("MapBackground", mapArea, new Color(0.14f, 0.18f, 0.24f), Vector2.zero, new Vector2(270f, 270f)).rectTransform;

        // Los puntos van fuera de la máscara, para que los que están pegados al borde se vean enteros
        RectTransform markers = MinigameUI.CreateGroup("Markers", mapRect, center, new Vector2(270f, 270f));
        Image marker = MinigameUI.CreateImage("MarkerTemplate", markers, Color.green, Vector2.zero, new Vector2(16f, 16f));
        marker.sprite = circle;
        markerTemplate = marker.rectTransform;
        Text label = MinigameUI.CreateText("Label", markerTemplate, "Motor", 14, new Vector2(0f, 18f), new Vector2(120f, 20f));
        label.color = new Color(1f, 0.4f, 0.35f);
        label.gameObject.AddComponent<Shadow>();
        markerTemplate.gameObject.SetActive(false);

        // Cuadrado con una "nariz" que indica hacia dónde mira el jugador (siempre hacia arriba)
        playerMarker = MinigameUI.CreateImage("PlayerMarker", markers, Color.cyan, Vector2.zero, new Vector2(14f, 14f)).rectTransform;
        MinigameUI.CreateImage("Direction", playerMarker, Color.cyan, new Vector2(0f, 11f), new Vector2(4f, 10f));
    }

    // Mensaje breve arriba al centro (la viñeta sutil se crea por código al jugar)
    private void CreateAlert(Transform canvas)
    {
        RectTransform alert = MinigameUI.CreateGroup("Alert", canvas, Vector2.zero, Vector2.zero);
        MinigameUI.Stretch(alert);
        alertRoot = alert.gameObject;

        // 1000 de ancho para que no pise el panel (izquierda) ni el mapa (derecha),
        // y algo bajado para dejar sitio a la barra del viaje cuando se pilota
        alertText = MinigameUI.CreateText("Message", alert, "AVERÍA DETECTADA", 30, Vector2.zero, new Vector2(1000f, 64f));
        alertText.color = new Color(1f, 0.72f, 0.45f);
        AnchorToCorner(alertText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f));
        alertText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        alertMessage = alertText.gameObject.AddComponent<CanvasGroup>();
        alertMessage.blocksRaycasts = false;

        alertRoot.SetActive(false);
    }

    // Ancla un elemento a una esquina o borde de la pantalla, por ejemplo (0, 1) = arriba a la izquierda
    private static void AnchorToCorner(RectTransform rect, Vector2 corner, Vector2 offset)
    {
        rect.anchorMin = corner;
        rect.anchorMax = corner;
        rect.pivot = corner;
        rect.anchoredPosition = offset;
    }
}
