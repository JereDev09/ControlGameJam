using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Sobrecarga del sistema: mira qué botones se iluminan y luego pulsa la misma secuencia.
// Un botón equivocado = fallo.
public class OverloadMinigame : Minigame
{
    private static readonly Color[] ButtonColors =
    {
        Color.red, Color.green, new Color(0.2f, 0.4f, 1f), Color.yellow, Color.magenta, Color.cyan
    };

    [SerializeField] private int sequenceLength = 5;
    [SerializeField] private float flashTime = 0.35f;

    [Header("UI")]
    [Tooltip("Hay tantos botones como haya en esta lista (máximo 6, uno por color)")]
    [SerializeField] private Button[] buttons;
    [SerializeField] private Text statusText;

    private int[] sequence;
    private int inputIndex;
    private bool acceptingInput;

    private void Reset()
    {
        title = "Detener sobrecarga";
    }

    public override string SystemName => ShipZones.Overload;

    protected override void CreateContent()
    {
        const int buttonCount = 4;

        buttons = new Button[buttonCount];
        for (int i = 0; i < buttonCount; i++)
        {
            Vector2 position = new Vector2((i - (buttonCount - 1) / 2f) * 190f, 60f);
            buttons[i] = MinigameUI.CreateButton($"Button{i + 1}", Window, "", DimColor(i), position, new Vector2(160f, 160f));
        }

        statusText = MinigameUI.CreateText("StatusText", Window, "Mira la secuencia...", 30, new Vector2(0f, -120f), new Vector2(600f, 50f));
        CreateInstructions("Repite el orden en el que se iluminan los botones");
    }

    protected override void SetupMinigame()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i; // copia para usarla dentro del clic del botón
            buttons[i].onClick.AddListener(() => PressButton(index));
        }
    }

    protected override void StartMinigame()
    {
        inputIndex = 0;
        acceptingInput = false;

        sequence = new int[sequenceLength];
        for (int i = 0; i < sequenceLength; i++)
        {
            sequence[i] = Random.Range(0, buttons.Length);
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].transform.DOKill(true);
            buttons[i].image.DOKill();
            buttons[i].image.color = DimColor(i);
        }

        statusText.text = "Mira la secuencia...";
        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        yield return new WaitForSeconds(0.2f);

        foreach (int button in sequence)
        {
            LightUp(button, flashTime);
            yield return new WaitForSeconds(flashTime * 1.15f);
        }

        acceptingInput = true;
        statusText.text = "¡Tu turno!";
        MinigameFX.Punch(statusText.transform, 0.3f);
    }

    // Enciende el botón de golpe y se va apagando poco a poco
    private void LightUp(int button, float duration)
    {
        Image image = buttons[button].image;
        image.DOKill();
        image.color = GetColor(button);
        image.DOColor(DimColor(button), duration).SetEase(Ease.InQuad);
        MinigameFX.Punch(buttons[button].transform, 0.12f);
    }

    private void PressButton(int button)
    {
        if (!acceptingInput)
        {
            return;
        }

        LightUp(button, 0.3f);

        if (button != sequence[inputIndex])
        {
            Fail();
            return;
        }

        MinigameFX.Burst(Window, ((RectTransform)buttons[button].transform).anchoredPosition, GetColor(button), 8, 100f);
        inputIndex++;
        statusText.text = $"{inputIndex} / {sequenceLength}";
        MinigameFX.Punch(statusText.transform, 0.15f);

        if (inputIndex == sequenceLength)
        {
            Succeed();
        }
    }

    private static Color GetColor(int button)
    {
        return ButtonColors[button % ButtonColors.Length];
    }

    private static Color DimColor(int button)
    {
        return Color.Lerp(Color.black, GetColor(button), 0.3f);
    }
}
