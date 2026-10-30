using UnityEngine;
using UnityEngine.InputSystem;

// Lanza un rayo desde el centro de la cámara. Si choca con algo que implementa
// IInteractable, muestra su texto y lo usa al pulsar la acción "Interact".
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactLayers = ~0;

    private InputAction interactAction;
    private IInteractable currentTarget;
    private GUIStyle promptStyle;

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        interactAction = InputSystem.actions.FindAction("Interact");
    }

    private void Update()
    {
        currentTarget = FindTarget();

        if (currentTarget != null && interactAction.WasPressedThisFrame())
        {
            currentTarget.Interact(gameObject);
        }
    }

    private IInteractable FindTarget()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayers, QueryTriggerInteraction.Ignore))
        {
            // InParent: así el script puede estar en el padre aunque el collider esté en un hijo
            return hit.collider.GetComponentInParent<IInteractable>();
        }

        return null;
    }

    private void OnGUI()
    {
        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;

        // Punto de mira
        GUI.Label(new Rect(centerX - 10f, centerY - 10f, 20f, 20f), "+", promptStyle);

        if (currentTarget != null)
        {
            string key = interactAction.GetBindingDisplayString();
            GUI.Label(new Rect(centerX - 200f, centerY + 30f, 400f, 30f), $"[{key}] {currentTarget.Prompt}", promptStyle);
        }
    }
}
