using UnityEngine;

// Ejemplo de objeto interactuable: cambia a un color aleatorio cada vez que lo usas.
// Cópialo como plantilla para tus propios objetos (puertas, botones, notas...).
public class ColorChangeInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Cambiar color";

    public string Prompt => prompt;

    public void Interact(GameObject interactor)
    {
        GetComponent<Renderer>().material.color = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.8f, 1f);
        Debug.Log($"{interactor.name} ha interactuado con {name}", this);
    }
}
