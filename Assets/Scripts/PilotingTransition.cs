using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

// Entrada y salida de la escena de pilotar:
// - Al empezar, la cámara parte pegada a la nave y se aleja (como si te sentaras a pilotar).
// - Al pulsar Interact (E), la cámara se acerca a la nave y pasas al modo primera persona
//   (durante el principio del tutorial todavía no se puede: ShipState.CanEnterShip).
public class PilotingTransition : MonoBehaviour
{
    [Tooltip("Escena de primera persona (tiene que estar en la lista de escenas de la build)")]
    [SerializeField] private string interiorScene = "TestZonePlayer";
    [Tooltip("Cuánto se acerca la cámara a la nave (0 = nada, 1 = hasta la nave)")]
    [SerializeField, Range(0f, 1f)] private float closeUp = 0.75f;
    [Tooltip("Segundos que tarda la cámara en acercarse a la nave al entrar")]
    [SerializeField] private float zoomDuration = 1.2f;
    [Tooltip("Segundos que tarda la cámara en alejarse de la nave al volver a pilotar")]
    [SerializeField] private float pullBackDuration = 1.8f;

    [Header("Referencias (si se dejan vacías se buscan solas)")]
    [SerializeField] private ShipMovement ship;
    [SerializeField] private AsteroidSpawner asteroidSpawner;
    [SerializeField] private Camera shipCamera;

    private InputAction interactAction;
    private Vector3 cameraPosition;
    private bool isPiloting;

    private void Start()
    {
        if (ship == null)
        {
            ship = FindFirstObjectByType<ShipMovement>();
        }

        if (asteroidSpawner == null)
        {
            asteroidSpawner = FindFirstObjectByType<AsteroidSpawner>();
        }

        if (shipCamera == null)
        {
            shipCamera = Camera.main;
        }

        interactAction = InputSystem.actions.FindAction("Interact");
        ShipState.EnsureLoaded(); // empieza la partida (con el tutorial) si no había empezado
        ShipState.IsPiloting = true;

        // Mientras se pilota no hace falta ver el ratón
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // La cámara empieza cerca de la nave y se aleja suavemente hasta su sitio
        // (dura más que el fundido de entrada, así se ve casi todo el movimiento)
        cameraPosition = shipCamera.transform.position;
        shipCamera.transform.position = GetCloseUpPosition();
        SetPiloting(false);
        shipCamera.transform.DOMove(cameraPosition, pullBackDuration).SetEase(Ease.InOutSine).SetLink(gameObject)
            .OnComplete(() => SetPiloting(true));
    }

    private void OnDestroy()
    {
        ShipState.IsPiloting = false;
    }

    private void Update()
    {
        if (isPiloting && ShipState.CanEnterShip && !ship.IsDestroyed && !PauseMenu.IsPaused && interactAction.WasPressedThisFrame())
        {
            GoInside();
        }
    }

    // La cámara se acerca suavemente a la nave; a mitad de camino la pantalla empieza a fundirse
    // a negro y, justo al llegar, se pasa a primera persona
    private void GoInside()
    {
        SetPiloting(false);
        shipCamera.transform.DOMove(GetCloseUpPosition(), zoomDuration).SetEase(Ease.InOutSine).SetLink(gameObject);
        SceneTransition.Load(interiorScene, zoomDuration * 0.6f, 0.8f, zoomDuration * 0.4f);
    }

    // Mientras la cámara se mueve, la nave no se controla y no salen asteroides
    private void SetPiloting(bool piloting)
    {
        isPiloting = piloting;
        ship.enabled = piloting;

        if (asteroidSpawner != null)
        {
            asteroidSpawner.enabled = piloting;
        }
    }

    private Vector3 GetCloseUpPosition()
    {
        return Vector3.Lerp(cameraPosition, ship.transform.position, closeUp);
    }
}
