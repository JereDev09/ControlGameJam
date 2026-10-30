using UnityEngine;

// Activa o desactiva el control del jugador en primera persona (moverse, mirar e interactuar).
// Al desactivarlo el ratón queda libre para usar la UI.
public static class PlayerControl
{
    public static void Set(GameObject player, bool canControl)
    {
        if (player == null)
        {
            return;
        }

        if (player.TryGetComponent(out FirstPersonController controller))
        {
            controller.enabled = canControl;
        }

        if (player.TryGetComponent(out PlayerInteractor interactor))
        {
            interactor.enabled = canControl;
        }
    }
}
