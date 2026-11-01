using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Recuperar piezas: mueve el robot (WASD / stick) para recoger las piezas verdes que flotan.
// Si tocas una roja o se acaba el tiempo, falla.
public class RecoverPartsMinigame : Minigame
{
    [SerializeField] private int correctPieceCount = 3;
    [SerializeField] private int wrongPieceCount = 4;
    [SerializeField] private float robotSpeed = 350f;
    [SerializeField] private float pieceSpeed = 120f;

    [Header("UI")]
    [SerializeField] private RectTransform area;
    [SerializeField] private RectTransform robot;
    [Tooltip("Imágenes desactivadas que se copian para crear las piezas")]
    [SerializeField] private Image correctPieceTemplate;
    [SerializeField] private Image wrongPieceTemplate;
    [SerializeField] private Text counterText;

    private class FloatingPiece
    {
        public Image image;
        public Vector2 velocity;
        public bool isCorrect;
    }

    private readonly List<FloatingPiece> pieces = new List<FloatingPiece>();
    private InputAction moveAction;
    private int collected;

    private void Reset()
    {
        title = "Recuperar piezas";
        timeLimit = 30f;
    }

    public override string SystemName => ShipZones.Parts;

    protected override void CreateContent()
    {
        area = MinigameUI.CreateImage("Area", Window, new Color(0.03f, 0.03f, 0.08f), new Vector2(0f, 20f), new Vector2(850f, 420f)).rectTransform;

        correctPieceTemplate = MinigameUI.CreateImage("CorrectPieceTemplate", area, Color.green, Vector2.zero, new Vector2(36f, 36f));
        correctPieceTemplate.gameObject.SetActive(false);
        wrongPieceTemplate = MinigameUI.CreateImage("WrongPieceTemplate", area, Color.red, Vector2.zero, new Vector2(36f, 36f));
        wrongPieceTemplate.gameObject.SetActive(false);

        robot = MinigameUI.CreateImage("Robot", area, Color.white, Vector2.zero, new Vector2(50f, 50f)).rectTransform;
        MinigameUI.CreateImage("Visor", robot, new Color(0.1f, 0.2f, 0.4f), new Vector2(0f, 8f), new Vector2(34f, 10f));

        counterText = MinigameUI.CreateText("CounterText", Window, "Piezas: 0 / 3", 26, new Vector2(0f, -225f), new Vector2(400f, 40f));
        CreateInstructions("Mueve el robot (blanco) con WASD y recoge las piezas verdes. ¡No toques las rojas!");
    }

    protected override void SetupMinigame()
    {
        moveAction = InputSystem.actions.FindAction("Move");
    }

    protected override void StartMinigame()
    {
        pieces.Clear();
        collected = 0;
        UpdateCounter();

        for (int i = 0; i < correctPieceCount; i++)
        {
            SpawnPiece(true);
        }

        for (int i = 0; i < wrongPieceCount; i++)
        {
            SpawnPiece(false);
        }

        robot.DOKill(true);
        robot.anchoredPosition = Vector2.zero;
        robot.localRotation = Quaternion.identity;
        robot.SetAsLastSibling(); // el robot se dibuja por encima de las piezas
    }

    private void SpawnPiece(bool isCorrect)
    {
        Image piece = Spawn(isCorrect ? correctPieceTemplate : wrongPieceTemplate);
        Vector2 limit = GetLimit(piece.rectTransform);

        // Posición al azar, pero no encima del robot (que empieza en el centro)
        Vector2 position;
        do
        {
            position = new Vector2(Random.Range(-limit.x, limit.x), Random.Range(-limit.y, limit.y));
        }
        while (position.magnitude < 120f);

        piece.rectTransform.anchoredPosition = position;

        // Aparecen con un "pop" y giran sin parar para que parezca que flotan
        piece.transform.localScale = Vector3.zero;
        piece.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetDelay(Random.Range(0f, 0.3f)).SetLink(piece.gameObject);
        piece.transform.DOLocalRotate(new Vector3(0f, 0f, 360f), Random.Range(2f, 5f), RotateMode.FastBeyond360)
            .SetLoops(-1).SetEase(Ease.Linear).SetLink(piece.gameObject);

        pieces.Add(new FloatingPiece
        {
            image = piece,
            velocity = Random.insideUnitCircle.normalized * pieceSpeed,
            isCorrect = isCorrect
        });
    }

    protected override void UpdateMinigame()
    {
        MoveRobot();

        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            FloatingPiece piece = pieces[i];
            RectTransform pieceRect = piece.image.rectTransform;
            MovePiece(piece);

            // ¿El robot toca esta pieza?
            float touchDistance = (robot.sizeDelta.x + pieceRect.sizeDelta.x) / 2f;
            if (Vector2.Distance(pieceRect.anchoredPosition, robot.anchoredPosition) > touchDistance)
            {
                continue;
            }

            if (!piece.isCorrect)
            {
                MinigameFX.Burst(area, pieceRect.anchoredPosition, Color.red, 16);
                MinigameFX.Shake(area, 20f);
                Fail();
                return;
            }

            Collect(piece);
            pieces.RemoveAt(i);

            if (collected == correctPieceCount)
            {
                Succeed();
                return;
            }
        }
    }

    private void Collect(FloatingPiece piece)
    {
        collected++;
        UpdateCounter();
        // Cada pieza suena más aguda que la anterior
        GameAudio.PlaySfx(GameAudio.Sounds.partCollected, 1f, 1f + (collected - 1) * 0.15f);
        MinigameFX.Punch(counterText.transform, 0.3f);
        MinigameFX.Punch(robot, 0.25f);
        MinigameFX.Burst(area, piece.image.rectTransform.anchoredPosition, Color.green);

        // La pieza crece, se desvanece y se borra
        GameObject pieceObject = piece.image.gameObject;
        piece.image.transform.DOKill();
        piece.image.transform.DOScale(1.8f, 0.25f).SetLink(pieceObject);
        piece.image.DOFade(0f, 0.25f).SetLink(pieceObject).OnComplete(() => Destroy(pieceObject));
    }

    private void MoveRobot()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector2 limit = GetLimit(robot);
        Vector2 position = robot.anchoredPosition + input * robotSpeed * Time.deltaTime;
        robot.anchoredPosition = new Vector2(
            Mathf.Clamp(position.x, -limit.x, limit.x),
            Mathf.Clamp(position.y, -limit.y, limit.y));

        // Se inclina un poco hacia donde se mueve
        Quaternion tilt = Quaternion.Euler(0f, 0f, -input.x * 20f);
        robot.localRotation = Quaternion.Lerp(robot.localRotation, tilt, 10f * Time.deltaTime);
    }

    // Las piezas avanzan en línea recta y rebotan en los bordes del área
    private void MovePiece(FloatingPiece piece)
    {
        RectTransform rect = piece.image.rectTransform;
        Vector2 limit = GetLimit(rect);
        Vector2 position = rect.anchoredPosition + piece.velocity * Time.deltaTime;

        if (Mathf.Abs(position.x) > limit.x)
        {
            piece.velocity.x = -piece.velocity.x;
            position.x = Mathf.Clamp(position.x, -limit.x, limit.x);
        }

        if (Mathf.Abs(position.y) > limit.y)
        {
            piece.velocity.y = -piece.velocity.y;
            position.y = Mathf.Clamp(position.y, -limit.y, limit.y);
        }

        rect.anchoredPosition = position;
    }

    // Hasta dónde se puede mover un elemento sin salirse del área
    private Vector2 GetLimit(RectTransform element)
    {
        return (area.rect.size - element.sizeDelta) / 2f;
    }

    private void UpdateCounter()
    {
        counterText.text = $"Piezas: {collected} / {correctPieceCount}";
    }
}
