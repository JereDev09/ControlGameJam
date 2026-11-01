using UnityEngine;
using UnityEngine.Rendering.Universal;

// Vive en PersistentGameplay (siempre cargada). Se encarga del efecto CRT:
// solo se ve mientras pilotas la nave (el minijuego principal); en el menú, en primera persona
// y en las escenas del final no.
public class GameManager : MonoBehaviour
{
    [SerializeField] private UniversalRendererData CRTRender;

    private bool crtWasActive;

    private void Awake()
    {
        CRT_Controller.Initialize(CRTRender);
        crtWasActive = CRT_Controller.isEnable;
    }

    private void Update()
    {
        if (CRT_Controller.IsInitialized && CRT_Controller.isEnable != ShipState.IsPiloting)
        {
            CRT_Controller.SetEnabled(ShipState.IsPiloting);
        }
    }

    // El render feature es un asset del proyecto: al salir se deja como estaba
    private void OnDestroy()
    {
        if (CRT_Controller.IsInitialized)
        {
            CRT_Controller.SetEnabled(crtWasActive);
        }
    }
}
