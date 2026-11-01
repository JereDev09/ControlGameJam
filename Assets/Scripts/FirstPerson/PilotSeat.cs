using DG.Tweening;
using UnityEngine;

// Asiento del piloto: al interactuar la cámara baja como si te sentaras y vuelves a pilotar (con fundido).
// En la escena de pilotar, PilotingTransition hace que la cámara se aleje de la nave.
public class PilotSeat : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Pilotar la nave";
    [Tooltip("Escena de pilotar (tiene que estar en la lista de escenas de la build)")]
    [SerializeField] private string pilotScene = "MinijuegoPrincipal";
    [Tooltip("Segundos que tarda en sentarse (y en fundirse a negro)")]
    [SerializeField] private float sitDuration = 0.45f;

    public string Prompt => prompt;

    public void Interact(GameObject interactor)
    {
        PlayerControl.Set(interactor, false);
        GameAudio.PlaySfx(GameAudio.Sounds.transitionWhoosh);

        // La cámara baja un poco y mira algo hacia abajo, como al sentarse
        Camera playerCamera = interactor.GetComponentInChildren<Camera>();
        if (playerCamera != null)
        {
            Transform cameraTransform = playerCamera.transform;
            cameraTransform.DOLocalMoveY(cameraTransform.localPosition.y - 0.4f, sitDuration).SetEase(Ease.InOutSine).SetLink(cameraTransform.gameObject);
            cameraTransform.DOLocalRotate(new Vector3(15f, 0f, 0f), sitDuration).SetEase(Ease.InOutSine).SetLink(cameraTransform.gameObject);
        }

        SceneTransition.Load(pilotScene, sitDuration, 0.5f);
    }
}
