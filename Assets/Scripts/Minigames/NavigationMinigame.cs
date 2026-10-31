using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Sistema de navegación: traza la ruta uniendo puntos vecinos desde la nave (verde) hasta
// el destino (azul) sin tocar asteroides (rojo). El combustible da justo para la ruta más corta.
public class NavigationMinigame : Minigame
{
    private static readonly Color EmptyColor = new Color(0.35f, 0.38f, 0.45f);
    private static readonly Color AsteroidColor = new Color(0.8f, 0.2f, 0.2f);
    private static readonly Color DestinationColor = new Color(0.2f, 0.5f, 1f);
    private static readonly Color ShipColor = Color.green;
    private static readonly Color VisitedColor = new Color(0.2f, 0.45f, 0.2f);

    [SerializeField, Range(0f, 0.8f)] private float asteroidChance = 0.4f;

    [Header("UI")]
    [Tooltip("Lo pone la herramienta. Si cambias el tamaño, regenera la UI")]
    [SerializeField] private int gridSize = 5;
    [Tooltip("Puntos del mapa, fila a fila empezando por abajo a la izquierda")]
    [SerializeField] private Button[] points;
    [SerializeField] private Text fuelText;
    [Tooltip("Imagen desactivada que se copia para dibujar cada tramo de la ruta")]
    [SerializeField] private Image lineTemplate;

    private bool[,] isAsteroid;
    private Vector2Int current;
    private Vector2Int destination;
    private int fuel;
    private Color fuelColor;
    private Tween destinationPulse;
    private bool isDraggingRoute;

    private void Reset()
    {
        title = "Calcular ruta";
        timeLimit = 25f;
    }

    public override string SystemName => ShipZones.Navigation;

    protected override void CreateContent()
    {
        const float spacing = 90f;
        gridSize = 5;

        // Las líneas van en un grupo creado antes que los puntos, para que queden por detrás
        RectTransform lines = MinigameUI.CreateGroup("Lines", Window, Vector2.zero, WindowSize);
        lineTemplate = MinigameUI.CreateImage("LineTemplate", lines, ShipColor, Vector2.zero, new Vector2(100f, 8f));
        lineTemplate.raycastTarget = false;
        lineTemplate.gameObject.SetActive(false);

        points = new Button[gridSize * gridSize];
        float offset = (gridSize - 1) / 2f;
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                Vector2 position = new Vector2((x - offset) * spacing, (y - offset) * spacing + 10f);
                points[y * gridSize + x] = MinigameUI.CreateButton($"Point{x}_{y}", Window, "", EmptyColor, position, new Vector2(46f, 46f));
            }
        }

        fuelText = MinigameUI.CreateText("FuelText", Window, "Combustible: 8", 26, new Vector2(-370f, 60f), new Vector2(220f, 40f));
        MinigameUI.CreateText("Legend", Window, "Verde: nave\nAzul: destino\nRojo: asteroide", 22, new Vector2(370f, 60f), new Vector2(220f, 100f));
        CreateInstructions("Haz clic o desliza por puntos vecinos hasta el destino");
    }

    protected override void SetupMinigame()
    {
        fuelColor = fuelText.color;

        for (int i = 0; i < points.Length; i++)
        {
            Vector2Int point = new Vector2Int(i % gridSize, i / gridSize);
            points[i].onClick.AddListener(() => MoveTo(point));
        }
    }

    protected override void StartMinigame()
    {
        isDraggingRoute = false;
        isAsteroid = new bool[gridSize, gridSize];
        current = Vector2Int.zero;
        destination = new Vector2Int(gridSize - 1, gridSize - 1);
        fuel = (gridSize - 1) * 2;
        fuelText.text = $"Combustible: {fuel}";
        fuelText.color = fuelColor;

        bool[,] isSafeRoute = CreateSafeRoute();

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                // Asteroides al azar, pero nunca encima de la ruta segura
                isAsteroid[x, y] = !isSafeRoute[x, y] && Random.value < asteroidChance;

                Vector2Int point = new Vector2Int(x, y);
                Button button = GetPoint(point);
                button.transform.DOKill();
                button.transform.localRotation = Quaternion.identity;
                button.transform.localScale = Vector3.one;
                button.image.color = GetPointColor(point);

                // Los asteroides giran despacio
                if (isAsteroid[x, y])
                {
                    button.transform.DOLocalRotate(new Vector3(0f, 0f, 360f), Random.Range(2f, 4f), RotateMode.FastBeyond360)
                        .SetLoops(-1).SetEase(Ease.Linear).SetLink(button.gameObject);
                }
            }
        }

        // El destino late para que se vea bien
        destinationPulse = MinigameFX.Pulse(GetPoint(destination).transform, 1.25f, 0.4f);
    }

    protected override void UpdateMinigame()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null)
        {
            return;
        }

        if (pointer.press.wasPressedThisFrame)
        {
            isDraggingRoute = true;
        }

        if (!isDraggingRoute)
        {
            return;
        }

        int hoveredPoint = FindPoint(pointer.position.ReadValue());
        if (hoveredPoint >= 0)
        {
            Vector2Int point = new Vector2Int(hoveredPoint % gridSize, hoveredPoint / gridSize);
            Vector2Int difference = point - current;
            if (point != current && Mathf.Abs(difference.x) + Mathf.Abs(difference.y) == 1)
            {
                MoveTo(point);
            }
        }

        if (pointer.press.wasReleasedThisFrame)
        {
            isDraggingRoute = false;
        }
    }

    private int FindPoint(Vector2 screenPosition)
    {
        for (int i = 0; i < points.Length; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)points[i].transform, screenPosition))
            {
                return i;
            }
        }

        return -1;
    }

    // Inventa una ruta que siempre funciona: desde el inicio, ir a la derecha o arriba al azar
    private bool[,] CreateSafeRoute()
    {
        bool[,] isSafeRoute = new bool[gridSize, gridSize];
        Vector2Int step = current;
        isSafeRoute[step.x, step.y] = true;

        while (step != destination)
        {
            bool goRight = step.y == destination.y || (step.x < destination.x && Random.value < 0.5f);
            if (goRight)
            {
                step.x++;
            }
            else
            {
                step.y++;
            }

            isSafeRoute[step.x, step.y] = true;
        }

        return isSafeRoute;
    }

    private void MoveTo(Vector2Int point)
    {
        if (point == current)
        {
            return;
        }

        Button target = GetPoint(point);

        // Solo se puede ir a un punto vecino (arriba, abajo, izquierda o derecha)
        Vector2Int difference = point - current;
        if (Mathf.Abs(difference.x) + Mathf.Abs(difference.y) != 1)
        {
            MinigameFX.Shake((RectTransform)target.transform, 5f);
            return;
        }

        if (isAsteroid[point.x, point.y])
        {
            MinigameFX.Burst(Window, GetPosition(point), AsteroidColor, 16);
            Fail();
            return;
        }

        Image line = Spawn(lineTemplate);
        MinigameUI.PlaceLine(line.rectTransform, GetPosition(current), GetPosition(point));
        MinigameFX.GrowLine(line.rectTransform, 0.15f);

        GetPoint(current).image.color = VisitedColor;
        current = point;
        target.image.color = ShipColor;
        MinigameFX.Punch(target.transform, 0.3f);

        fuel--;
        fuelText.text = $"Combustible: {fuel}";
        MinigameFX.Punch(fuelText.transform, 0.15f);
        if (fuel <= 2)
        {
            fuelText.color = Color.red;
        }

        if (current == destination)
        {
            destinationPulse.Kill();
            MinigameFX.Burst(Window, GetPosition(point), DestinationColor, 20);
            Succeed();
        }
        else if (fuel == 0)
        {
            Fail();
        }
    }

    private Color GetPointColor(Vector2Int point)
    {
        if (point == current)
        {
            return ShipColor;
        }

        if (point == destination)
        {
            return DestinationColor;
        }

        return isAsteroid[point.x, point.y] ? AsteroidColor : EmptyColor;
    }

    private Button GetPoint(Vector2Int point)
    {
        return points[point.y * gridSize + point.x];
    }

    // Posición del punto dentro de la ventana (los puntos deben ser hijos directos de Window)
    private Vector2 GetPosition(Vector2Int point)
    {
        return ((RectTransform)GetPoint(point).transform).anchoredPosition;
    }
}
