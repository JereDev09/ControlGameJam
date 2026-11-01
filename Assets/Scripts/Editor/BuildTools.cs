using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

// Tools > Build > Construir juego (Windows)
// Hace la build con las escenas de File > Build Profiles (la primera es la que se abre al empezar)
// en la carpeta Builds/Windows del proyecto (git la ignora).
//
// Antes crea unos materiales de referencia en Resources/BuildShaders: algunos scripts crean
// materiales por código (asteroides, disparos) y, si ningún material de la build usara esos shaders,
// Unity los quitaría y en la build saldrían rosas o invisibles.
public static class BuildTools
{
    private const string BuildFolder = "Builds/Windows";
    private const string ShaderMaterialsFolder = "Assets/Resources/BuildShaders";

    [MenuItem("Tools/Build/Construir juego (Windows)")]
    private static void BuildWindows()
    {
        EnsureShaderMaterials();

        List<string> scenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                scenes.Add(scene.path);
            }
        }

        if (scenes.Count == 0)
        {
            EditorUtility.DisplayDialog("Build", "No hay escenas en File > Build Profiles.", "Vale");
            return;
        }

        string exePath = $"{BuildFolder}/{PlayerSettings.productName}.exe";
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });

        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build terminada: {exePath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0} s)");
            EditorUtility.RevealInFinder(exePath);
        }
        else
        {
            EditorUtility.DisplayDialog("Build", $"La build no ha salido bien ({summary.result}). Mira los errores en la consola.", "Vale");
        }
    }

    // Un material por cada shader (y variante) que se usa desde código
    private static void EnsureShaderMaterials()
    {
        MinigameTools.CreateFolder(ShaderMaterialsFolder);
        CreateMaterial("LitTransparente", "Universal Render Pipeline/Lit", true);
        CreateMaterial("UnlitOpaco", "Universal Render Pipeline/Unlit", false);
        CreateMaterial("UnlitTransparente", "Universal Render Pipeline/Unlit", true);
        AssetDatabase.SaveAssets();
    }

    private static void CreateMaterial(string materialName, string shaderName, bool transparent)
    {
        string path = $"{ShaderMaterialsFolder}/{materialName}.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
        {
            return;
        }

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning($"No existe el shader {shaderName}");
            return;
        }

        // Los mismos ajustes que ponen los scripts al hacerlos transparentes
        Material material = new Material(shader);
        if (transparent)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        AssetDatabase.CreateAsset(material, path);
    }
}
