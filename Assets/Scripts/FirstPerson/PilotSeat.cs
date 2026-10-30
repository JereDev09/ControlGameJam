using UnityEngine;

// Asiento del piloto: al interactuar vuelves a pilotar la nave (con fundido).
// En la escena de pilotar, PilotingTransition hace que la cámara se aleje de la nave.
public class PilotSeat : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Pilotar la nave";
    [Tooltip("Escena de pilotar (tiene que estar en la lista de escenas de la build)")]
    [SerializeField] private string pilotScene = "MinijuegoPrincipal";

    public string Prompt => prompt;

    public void Interact(GameObject interactor)
    {
        PlayerControl.Set(interactor, false);
        SceneTransition.Load(pilotScene);
    }
}
