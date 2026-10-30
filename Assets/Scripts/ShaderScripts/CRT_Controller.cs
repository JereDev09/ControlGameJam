using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class CRT_Controller
{
    // Nombres reales de las keywords generadas por CRT_Shader.shadergraph.
    // (ojo: "SCALINE" esta asi en el propio Shader Graph, no es una errata de aqui)
    private const string KeywordDeformation = "_DEFORMATIONACTIVE";
    private const string KeywordScanlines = "_SCALINEACTIVE";
    private const string KeywordChromaticAberration = "_CHROMATICABERRATIONACTIVE";
    private const string KeywordRGB = "_RGB_ACTIVE";

    private static readonly int CurvatureID = Shader.PropertyToID("_Curvature");
    private static readonly int OverscanID = Shader.PropertyToID("_Overscan");
    private static readonly int ScanlineIntensityID = Shader.PropertyToID("_ScanlineIntensity");
    private static readonly int ChromaticAberrationID = Shader.PropertyToID("_ChromaticAberration");
    private static readonly int ScanLineTimeID = Shader.PropertyToID("_ScanLineTime");

    private static FullScreenPassRendererFeature feature;
    private static Material material;

    // =========================================================
    // INICIALIZACIÓN
    // =========================================================

    public static void Initialize(UniversalRendererData rendererData)
    {
        if (rendererData == null)
        {
            Debug.LogError("CRT: RendererData es NULL.");
            return;
        }

        foreach (var rendererFeature in rendererData.rendererFeatures)
        {
            if (rendererFeature is FullScreenPassRendererFeature fullscreen &&
                fullscreen.name == "CRT")
            {
                feature = fullscreen;

                // El material del FullScreen Pass
                material = fullscreen.passMaterial;

                //Debug.Log("CRT: FullScreen Pass encontrado.");
                return;
            }
        }

        Debug.LogError("CRT: No se encontró el FullScreenPassRendererFeature llamado 'CRT'.");
    }

    /// <summary>
    /// True cuando <see cref="Initialize"/> ya encontró el render feature.
    /// </summary>
    public static bool IsInitialized => feature != null;

    // =========================================================
    // ON / OFF
    // =========================================================

    public static void Enable()
    {
        if (feature == null)
        {
            Debug.LogError("CRT: No está inicializado.");
            return;
        }

        feature.SetActive(true);
    }

    public static void Disable()
    {
        if (feature == null)
        {
            Debug.LogError("CRT: No está inicializado.");
            return;
        }

        feature.SetActive(false);
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
            Enable();
        else
            Disable();
    }

    // NOTA: esto se consulta cada frame desde CRTVirtualMouse, así que no puede
    // lanzar NullReference cuando la escena se carga sin SettingController.
    public static bool isEnable => feature != null && feature.isActive;

    /// <summary>
    /// True solo si el pase CRT está activo Y la deformación de pantalla está encendida.
    /// Es la condición que decide si hay que corregir la posición del ratón.
    /// </summary>
    public static bool IsDeformationActive =>
        isEnable && material != null && material.IsKeywordEnabled(KeywordDeformation);

    // =========================================================
    // PARÁMETROS DEL SHADER
    // =========================================================

    public static void SetCurvature(float value)
    {
        if (!CheckMaterial())
            return;

        material.SetFloat(CurvatureID, value);
    }

    public static float GetCurvature()
    {
        if (!isEnable || material == null || !material.HasFloat(CurvatureID))
            return 0f;

        return material.GetFloat(CurvatureID);
    }

    public static void SetOverscan(float value)
    {
        if (!CheckMaterial())
            return;

        material.SetFloat(OverscanID, value);
    }

    public static float GetOverscan()
    {
        // 1 es el valor neutro: overscan 0 colapsaría toda la pantalla en un punto.
        if (!isEnable || material == null || !material.HasFloat(OverscanID))
            return 1f;

        return material.GetFloat(OverscanID);
    }

    public static void SetScanlineIntensity(float value)
    {
        if (!CheckMaterial())
            return;

        material.SetFloat(ScanlineIntensityID, value);
    }

    public static void SetTime(float value)
    {
        if (!CheckMaterial())
            return;
            
        material.SetFloat(ScanLineTimeID, value);
    }

    public static void SetChromaticAberration(float value)
    {
        if (!CheckMaterial())
            return;

        material.SetFloat(ChromaticAberrationID, value);
    }

    // =========================================================
    // KEYWORDS
    // =========================================================

    public static void SetScanlines(bool enabled)
    {
        SetKeyword(KeywordScanlines, enabled);
    }


    public static void SetChromaticAberration(bool enabled)
    {
        SetKeyword(KeywordChromaticAberration, enabled);
    }


    public static void SetCurvatureEnabled(bool enabled)
    {
        SetKeyword(KeywordDeformation, enabled);
    }


    public static void SetRGBMask(bool enabled)
    {
        SetKeyword(KeywordRGB, enabled);
    }

    // =========================================================
    // INTERNAL
    // =========================================================

    private static bool CheckMaterial()
    {
        if (material == null)
        {
            Debug.LogError("CRT: Material es NULL.");
            return false;
        }

        return true;
    }


    private static void SetKeyword(string keyword, bool enabled)
    {
        if (!CheckMaterial())
            return;

        if (enabled)
            material.EnableKeyword(keyword);
        else
            material.DisableKeyword(keyword);
    }
}
