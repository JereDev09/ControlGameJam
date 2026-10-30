using UnityEngine;

// Cualquier objeto con un script que implemente esta interfaz (y un Collider)
// se puede usar mirándolo y pulsando la acción "Interact" (E por defecto).
public interface IInteractable
{
    // Texto que aparece en pantalla al mirar el objeto, por ejemplo "Abrir puerta"
    string Prompt { get; }

    // Se llama al interactuar. "interactor" es el jugador que lo ha usado.
    void Interact(GameObject interactor);
}
