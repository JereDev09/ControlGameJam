using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Efectos de feedback hechos con DOTween, para usarlos desde cualquier minijuego.
public static class MinigameFX
{
    // Pequeño "salto" de tamaño, para clics y aciertos
    public static void Punch(Transform target, float strength = 0.15f)
    {
        // Relativo a su escala, así también funciona con objetos volteados (escala -1)
        target.DOKill(true);
        target.DOPunchScale(target.localScale * strength, 0.25f, 8, 0.8f).SetLink(target.gameObject);
    }

    // Sacudida, para errores
    public static void Shake(RectTransform target, float strength = 15f)
    {
        target.DOKill(true);
        target.DOShakeAnchorPos(0.35f, strength, 25).SetLink(target.gameObject);
    }

    // Cambia de color un momento y vuelve al color que tenía
    public static void Flash(Graphic graphic, Color flashColor, float duration = 0.3f)
    {
        graphic.DOKill(true);
        Color originalColor = graphic.color;
        graphic.color = flashColor;
        graphic.DOColor(originalColor, duration).SetLink(graphic.gameObject);
    }

    // Late sin parar (relativo a su escala). Guarda el Tween que devuelve para pararlo con Kill()
    public static Tween Pulse(Transform target, float scale = 1.15f, float duration = 0.4f)
    {
        return target.DOScale(target.localScale * scale, duration).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(target.gameObject);
    }

    // Hace crecer una línea desde su inicio hasta su largo actual
    public static void GrowLine(RectTransform line, float duration = 0.25f)
    {
        Vector2 finalSize = line.sizeDelta;
        line.sizeDelta = new Vector2(0f, finalSize.y);
        line.DOSizeDelta(finalSize, duration).SetEase(Ease.OutCubic).SetLink(line.gameObject);
    }

    // "Partículas" de UI: cuadraditos que salen disparados desde un punto y se desvanecen
    public static void Burst(Transform parent, Vector2 position, Color color, int count = 12, float distance = 120f)
    {
        for (int i = 0; i < count; i++)
        {
            Image particle = MinigameUI.CreateImage("Particle", parent, color, position, Vector2.one * Random.Range(8f, 16f));
            particle.raycastTarget = false;

            Vector2 target = position + Random.insideUnitCircle.normalized * Random.Range(distance * 0.4f, distance);
            float duration = Random.Range(0.4f, 0.7f);

            particle.rectTransform.DOAnchorPos(target, duration).SetEase(Ease.OutCubic).SetLink(particle.gameObject);
            particle.rectTransform.DOLocalRotate(new Vector3(0f, 0f, Random.Range(-360f, 360f)), duration, RotateMode.FastBeyond360).SetLink(particle.gameObject);
            particle.DOFade(0f, duration).SetEase(Ease.InQuad).SetLink(particle.gameObject)
                .OnComplete(() => Object.Destroy(particle.gameObject));
        }
    }

    // Varias explosiones de colores, para cuando se completa un minijuego
    public static void Confetti(Transform parent)
    {
        Color[] colors = { Color.green, Color.yellow, Color.cyan, Color.magenta, Color.white };
        foreach (Color color in colors)
        {
            Vector2 position = new Vector2(Random.Range(-300f, 300f), Random.Range(-150f, 150f));
            Burst(parent, position, color, 15, 250f);
        }
    }

    // Texto que sube y se desvanece (por ejemplo "-3s")
    public static void FloatingText(Transform parent, Vector2 position, string content, Color color)
    {
        Text text = MinigameUI.CreateText("FloatingText", parent, content, 30, position, new Vector2(300f, 50f));
        text.color = color;
        text.rectTransform.DOAnchorPosY(position.y + 80f, 0.8f).SetEase(Ease.OutCubic).SetLink(text.gameObject);
        text.DOFade(0f, 0.8f).SetEase(Ease.InQuad).SetLink(text.gameObject)
            .OnComplete(() => Object.Destroy(text.gameObject));
    }
}
