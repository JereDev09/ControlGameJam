using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Reparar circuitos: haz clic en las piezas para girarlas hasta que la energía llegue
// desde la entrada (izquierda) hasta la salida (derecha). Falla si se acaba el tiempo.
public class CircuitMinigame : Minigame
{
    private const float CellSize = 100f;
    private static readonly Color TileColor = new Color(0.18f, 0.2f, 0.25f);
    private static readonly Color WireColor = new Color(0.5f, 0.5f, 0.55f);
    private static readonly Color PowerColor = Color.yellow;

    // Lados de una pieza: 0 = arriba, 1 = derecha, 2 = abajo, 3 = izquierda.
    // Cada pieza guarda sus lados abiertos como bits: arriba = 1, derecha = 2, abajo = 4, izquierda = 8
    private static readonly Vector2Int[] SideOffsets =
    {
        new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0)
    };
    private static readonly string[] SideNames = { "Up", "Right", "Down", "Left" };
    private const int StraightPiece = 0b0101;  // arriba + abajo
    private const int CornerPiece = 0b0011;    // arriba + derecha

    [Header("UI")]
    [Tooltip("Lo pone la herramienta. Si cambias el tamaño, regenera la UI")]
    [SerializeField] private int columns = 5;
    [SerializeField] private int rows = 4;
    [Tooltip("Casillas, fila a fila empezando por abajo a la izquierda")]
    [SerializeField] private Button[] tiles;
    [Tooltip("Dibujo de cada pieza. Sus hijos, en orden: centro, arriba, derecha, abajo, izquierda")]
    [SerializeField] private RectTransform[] pieceVisuals;
    [SerializeField] private Image entranceMarker;
    [SerializeField] private Image exitMarker;

    private int[,] pieces;
    private bool[,] wasPowered;
    private int startRow;
    private int endRow;

    private void Reset()
    {
        title = "Reparar circuitos";
        timeLimit = 40f;
    }

    public override string SystemName => ShipZones.Circuits;

    protected override void CreateContent()
    {
        columns = 5;
        rows = 4;
        tiles = new Button[columns * rows];
        pieceVisuals = new RectTransform[columns * rows];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                int index = y * columns + x;
                Vector2 position = new Vector2((x - (columns - 1) / 2f) * CellSize, (y - (rows - 1) / 2f) * CellSize);
                tiles[index] = MinigameUI.CreateButton($"Tile{x}_{y}", Window, "", TileColor, position, new Vector2(CellSize - 6f, CellSize - 6f));
                pieceVisuals[index] = CreatePieceVisual(tiles[index].transform);
            }
        }

        entranceMarker = MinigameUI.CreateImage("Entrance", Window, PowerColor, new Vector2(-325f, 0f), new Vector2(50f, 24f));
        exitMarker = MinigameUI.CreateImage("Exit", Window, Color.white, new Vector2(325f, 0f), new Vector2(50f, 24f));
        CreateInstructions("Clic en las piezas para girarlas y llevar la energía hasta la salida");
    }

    // Dibuja una pieza: un cuadrado en el centro y una barra hacia cada lado.
    // En el juego se encienden solo las barras de los lados abiertos.
    private RectTransform CreatePieceVisual(Transform tile)
    {
        RectTransform visual = MinigameUI.CreateGroup("Piece", tile, Vector2.zero, new Vector2(CellSize, CellSize));
        MinigameUI.CreateImage("Center", visual, WireColor, Vector2.zero, new Vector2(24f, 24f));

        for (int side = 0; side < 4; side++)
        {
            Vector2 direction = SideOffsets[side];
            Vector2 size = direction.x != 0f ? new Vector2(CellSize / 2f, 24f) : new Vector2(24f, CellSize / 2f);
            MinigameUI.CreateImage(SideNames[side], visual, WireColor, direction * CellSize / 4f, size);
        }

        return visual;
    }

    protected override void SetupMinigame()
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            int index = i; // copia para usarla dentro del clic del botón
            tiles[i].onClick.AddListener(() => RotatePiece(index));
        }
    }

    protected override void StartMinigame()
    {
        pieces = new int[columns, rows];
        wasPowered = new bool[columns, rows];
        CreateSolutionPath();

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                // Las casillas fuera del camino reciben una pieza al azar
                if (pieces[x, y] == 0)
                {
                    pieces[x, y] = Random.value < 0.5f ? StraightPiece : CornerPiece;
                }

                // Desordenar: girar cada pieza un número de veces al azar
                int turns = Random.Range(0, 4);
                for (int i = 0; i < turns; i++)
                {
                    pieces[x, y] = RotateClockwise(pieces[x, y]);
                }
            }
        }

        // Si por casualidad ya estaba resuelto, giramos la primera pieza
        while (IsSolved())
        {
            pieces[0, startRow] = RotateClockwise(pieces[0, startRow]);
        }

        // Enciende las barras de cada pieza según sus lados abiertos
        for (int i = 0; i < tiles.Length; i++)
        {
            RectTransform visual = pieceVisuals[i];
            visual.DOKill();
            visual.localRotation = Quaternion.identity;
            tiles[i].transform.DOKill(true);

            int piece = pieces[i % columns, i / columns];
            for (int side = 0; side < 4; side++)
            {
                visual.GetChild(side + 1).gameObject.SetActive(HasSide(piece, side));
            }
        }

        // Coloca la entrada y la salida al lado de su casilla
        Vector2 sideOffset = new Vector2(CellSize / 2f + 25f, 0f);
        entranceMarker.rectTransform.anchoredPosition = GetTilePosition(0, startRow) - sideOffset;
        exitMarker.rectTransform.anchoredPosition = GetTilePosition(columns - 1, endRow) + sideOffset;
        exitMarker.DOKill();
        exitMarker.color = Color.white;

        RefreshPower(false);
    }

    // Crea un camino que siempre tiene solución: avanza columna a columna
    // y dentro de cada columna sube o baja al azar
    private void CreateSolutionPath()
    {
        int row = Random.Range(0, rows);
        startRow = row;
        OpenSide(0, row, 3); // entrada por la izquierda

        for (int column = 0; column < columns; column++)
        {
            int targetRow = Random.Range(0, rows);
            while (row != targetRow)
            {
                int side = targetRow > row ? 0 : 2; // arriba o abajo
                OpenSide(column, row, side);
                row += targetRow > row ? 1 : -1;
                OpenSide(column, row, Opposite(side));
            }

            OpenSide(column, row, 1); // salir por la derecha
            if (column + 1 < columns)
            {
                OpenSide(column + 1, row, 3);
            }
        }

        endRow = row;
    }

    private void RotatePiece(int index)
    {
        int x = index % columns;
        int y = index / columns;
        pieces[x, y] = RotateClockwise(pieces[x, y]);

        // Termina el giro anterior (si lo había) y gira 90º más
        RectTransform visual = pieceVisuals[index];
        visual.DOKill(true);
        visual.DOLocalRotate(new Vector3(0f, 0f, -90f), 0.15f, RotateMode.LocalAxisAdd).SetEase(Ease.OutBack);

        RefreshPower(true);

        if (IsSolved())
        {
            exitMarker.DOColor(PowerColor, 0.2f);
            MinigameFX.Burst(Window, exitMarker.rectTransform.anchoredPosition, PowerColor, 20);
            Succeed();
        }
    }

    private bool IsSolved()
    {
        bool[,] powered = FindPoweredCells();
        return powered[columns - 1, endRow] && HasSide(pieces[columns - 1, endRow], 1);
    }

    // Colorea las piezas con energía. Las que se acaban de encender dan un saltito.
    private void RefreshPower(bool animate)
    {
        bool[,] powered = FindPoweredCells();
        for (int i = 0; i < tiles.Length; i++)
        {
            int x = i % columns;
            int y = i / columns;

            foreach (Image part in pieceVisuals[i].GetComponentsInChildren<Image>())
            {
                part.color = powered[x, y] ? PowerColor : WireColor;
            }

            if (animate && powered[x, y] && !wasPowered[x, y])
            {
                MinigameFX.Punch(tiles[i].transform, 0.12f);
            }
        }

        wasPowered = powered;
    }

    // Sigue las piezas conectadas desde la entrada, como si fluyera la energía
    private bool[,] FindPoweredCells()
    {
        bool[,] powered = new bool[columns, rows];
        if (!HasSide(pieces[0, startRow], 3))
        {
            return powered;
        }

        Queue<Vector2Int> pending = new Queue<Vector2Int>();
        powered[0, startRow] = true;
        pending.Enqueue(new Vector2Int(0, startRow));

        while (pending.Count > 0)
        {
            Vector2Int cell = pending.Dequeue();
            for (int side = 0; side < 4; side++)
            {
                Vector2Int next = cell + SideOffsets[side];
                bool insideGrid = next.x >= 0 && next.x < columns && next.y >= 0 && next.y < rows;
                if (!insideGrid || powered[next.x, next.y])
                {
                    continue;
                }

                // Dos piezas están conectadas si las dos tienen abierto el lado por el que se tocan
                if (HasSide(pieces[cell.x, cell.y], side) && HasSide(pieces[next.x, next.y], Opposite(side)))
                {
                    powered[next.x, next.y] = true;
                    pending.Enqueue(next);
                }
            }
        }

        return powered;
    }

    private Vector2 GetTilePosition(int x, int y)
    {
        return ((RectTransform)tiles[y * columns + x].transform).anchoredPosition;
    }

    private void OpenSide(int x, int y, int side)
    {
        pieces[x, y] |= 1 << side;
    }

    private static bool HasSide(int piece, int side)
    {
        return (piece & (1 << side)) != 0;
    }

    private static int Opposite(int side)
    {
        return (side + 2) % 4;
    }

    // Girar 90º en sentido horario: arriba pasa a derecha, derecha a abajo, abajo a izquierda...
    private static int RotateClockwise(int piece)
    {
        return ((piece << 1) | (piece >> 3)) & 0b1111;
    }
}
