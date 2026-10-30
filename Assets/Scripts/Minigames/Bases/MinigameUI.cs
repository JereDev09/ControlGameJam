using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Funciones para crear la UI placeholder de los minijuegos por código (cuadrados de colores,
// textos, botones...). Las usa la herramienta del editor para crear la UI la primera vez.
// Las posiciones son en píxeles desde el centro del padre, sobre una pantalla de 1920x1080.
public static class MinigameUI
{
    private static Font font;

    public static Canvas CreateCanvas(string name, Transform parent)
    {
        GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    // Objeto vacío de UI, para agrupar otros
    public static RectTransform CreateGroup(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject groupObject = new GameObject(name, typeof(RectTransform));
        groupObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)groupObject.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static Image CreateImage(string name, Transform parent, Color color, Vector2 position, Vector2 size)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        return image;
    }

    public static Text CreateText(string name, Transform parent, string content, int fontSize, Vector2 position, Vector2 size)
    {
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
        return text;
    }

    public static Button CreateButton(string name, Transform parent, string label, Color color, Vector2 position, Vector2 size)
    {
        Image image = CreateImage(name, parent, color, position, size);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        if (!string.IsNullOrEmpty(label))
        {
            CreateText("Label", image.transform, label, 24, Vector2.zero, size);
        }

        return button;
    }

    public static Slider CreateSlider(string name, Transform parent, Vector2 position, Vector2 size)
    {
        // DefaultControls crea lo mismo que el menú GameObject > UI > Slider
        GameObject sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
        sliderObject.name = name;
        sliderObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)sliderObject.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        // Sin sprites todas las partes salen blancas, así que les damos colores distintos
        Slider slider = sliderObject.GetComponent<Slider>();
        sliderObject.transform.Find("Background").GetComponent<Image>().color = new Color(0.25f, 0.27f, 0.33f);
        slider.fillRect.GetComponent<Image>().color = new Color(0.3f, 0.6f, 1f);
        slider.handleRect.GetComponent<Image>().color = Color.white;
        slider.handleRect.sizeDelta = new Vector2(30f, 10f);
        return slider;
    }

    // Barra que se rellena de izquierda a derecha. Devuelve el relleno: usa SetBar para cambiarlo.
    public static RectTransform CreateBar(string name, Transform parent, Color backgroundColor, Color fillColor, Vector2 position, Vector2 size)
    {
        Image background = CreateImage(name, parent, backgroundColor, position, size);
        Image fill = CreateImage("Fill", background.transform, fillColor, Vector2.zero, Vector2.zero);
        fill.rectTransform.anchorMin = Vector2.zero;
        SetBar(fill.rectTransform, 0f);
        return fill.rectTransform;
    }

    // value va de 0 (vacía) a 1 (llena)
    public static void SetBar(RectTransform fill, float value)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
    }

    // Hace que ocupe todo el padre (por ejemplo, un fondo que tapa toda la pantalla)
    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    // Coloca una imagen como línea recta entre dos puntos. El grosor es su altura actual.
    public static void PlaceLine(RectTransform line, Vector2 from, Vector2 to)
    {
        Vector2 direction = to - from;
        line.pivot = new Vector2(0f, 0.5f);
        line.anchoredPosition = from;
        line.sizeDelta = new Vector2(direction.magnitude, line.sizeDelta.y);
        line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    // Sin EventSystem los botones no reciben clics
    public static GameObject EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem != null)
        {
            return eventSystem.gameObject;
        }

        return new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }
}
