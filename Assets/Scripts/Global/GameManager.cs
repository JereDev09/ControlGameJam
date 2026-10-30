using UnityEngine;
using UnityEngine.Rendering.Universal;

public class GameManager : MonoBehaviour
{
    [SerializeField] private UniversalRendererData CRTRender;

    private void Awake()
    {
        CRT_Controller.Initialize(CRTRender);
    }
}
