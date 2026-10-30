using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Filas de estabilidad (nombre, barra y porcentaje), una por cada zona de la nave.
// Las usan el panel en pantalla (StabilityHUD) y la terminal (StabilityTerminal).
public class StabilityRows
{
    private class Row
    {
        public string systemName;
        public RectTransform rect;
        public RectTransform fill;
        public Image fillImage;
        public Text percent;
        public float lastStability;
    }

    private readonly List<Row> rows = new List<Row>();

    // La plantilla es una fila desactivada con hijos llamados Name, Bar (con Fill dentro) y Percent
    public StabilityRows(RectTransform template)
    {
        foreach (ShipSystems.SystemInfo system in ShipState.Config.systems)
        {
            RectTransform rowRect = Object.Instantiate(template, template.parent);
            rowRect.gameObject.SetActive(true);
            rowRect.Find("Name").GetComponent<Text>().text = system.name;

            RectTransform fill = (RectTransform)rowRect.Find("Bar/Fill");
            rows.Add(new Row
            {
                systemName = system.name,
                rect = rowRect,
                fill = fill,
                fillImage = fill.GetComponent<Image>(),
                percent = rowRect.Find("Percent").GetComponent<Text>(),
                lastStability = ShipState.GetStability(system.name)
            });
        }
    }

    public void Refresh()
    {
        foreach (Row row in rows)
        {
            float stability = ShipState.GetStability(row.systemName);
            MinigameUI.SetBar(row.fill, stability / 100f);
            row.fillImage.color = GetStabilityColor(stability);
            row.percent.text = $"{Mathf.CeilToInt(stability)}%";

            // Si acaba de subir (se ha reparado), la fila da un saltito
            if (stability > row.lastStability + 1f)
            {
                MinigameFX.Punch(row.rect, 0.1f);
            }

            row.lastStability = stability;
        }
    }

    // Rojo por debajo del 50%, amarillo en el medio y verde arriba
    public static Color GetStabilityColor(float stability)
    {
        return stability < 50f
            ? Color.Lerp(Color.red, Color.yellow, stability / 50f)
            : Color.Lerp(Color.yellow, Color.green, (stability - 50f) / 50f);
    }

    // Crea la fila plantilla, desactivada (solo la usan las herramientas del editor)
    public static RectTransform CreateTemplate(Transform parent, float width, float height, int fontSize)
    {
        float nameWidth = width * 0.34f;
        float percentWidth = width * 0.16f;
        float barWidth = width - nameWidth - percentWidth - 20f;
        float left = -width / 2f;

        RectTransform template = MinigameUI.CreateGroup("RowTemplate", parent, Vector2.zero, new Vector2(width, height));
        MinigameUI.CreateText("Name", template, "Motor", fontSize, new Vector2(left + nameWidth / 2f, 0f), new Vector2(nameWidth, height))
            .alignment = TextAnchor.MiddleLeft;
        MinigameUI.CreateBar("Bar", template, new Color(0.2f, 0.22f, 0.28f), Color.green,
            new Vector2(left + nameWidth + 10f + barWidth / 2f, 0f), new Vector2(barWidth, height * 0.6f));
        MinigameUI.CreateText("Percent", template, "100%", fontSize, new Vector2(-left - percentWidth / 2f, 0f), new Vector2(percentWidth, height))
            .alignment = TextAnchor.MiddleRight;

        template.gameObject.SetActive(false);
        return template;
    }
}
