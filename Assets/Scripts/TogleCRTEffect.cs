using UnityEngine;

public class TogleCRTEffect : MonoBehaviour
{
    [SerializeField] private bool CRT_ActiveByDefault;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CRT_Controller.SetEnabled(CRT_ActiveByDefault);
    }
}
