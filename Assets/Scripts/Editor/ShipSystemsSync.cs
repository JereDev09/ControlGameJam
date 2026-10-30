using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Mantiene al día Resources/ShipSystems (las zonas de la nave y sus posiciones para el mapa):
// se actualiza solo cada vez que guardas una escena que tenga minijuegos.
// Así, si mueves o desactivas un minijuego, el mapa y la estabilidad lo tienen en cuenta.
[InitializeOnLoad]
public static class ShipSystemsSync
{
    private const string AssetPath = "Assets/Resources/ShipSystems.asset";
    private const float MapMargin = 3f;

    static ShipSystemsSync()
    {
        EditorSceneManager.sceneSaved += scene => Sync(scene, false);
    }

    [MenuItem("Tools/Minijuegos/Actualizar zonas y mapa")]
    private static void SyncActiveScene()
    {
        Sync(SceneManager.GetActiveScene(), true);
    }

    private static void Sync(Scene scene, bool showDialog)
    {
        List<Minigame> minigames = new List<Minigame>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            minigames.AddRange(root.GetComponentsInChildren<Minigame>(true));
        }

        // Escenas sin minijuegos (la de pilotar, el menú...) no tocan nada
        if (minigames.Count == 0)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Minijuegos", "Esta escena no tiene minijuegos.", "Vale");
            }

            return;
        }

        ShipSystems asset = AssetDatabase.LoadAssetAtPath<ShipSystems>(AssetPath);
        if (asset == null)
        {
            MinigameTools.CreateFolder("Assets/Resources");
            asset = ScriptableObject.CreateInstance<ShipSystems>();
            AssetDatabase.CreateAsset(asset, AssetPath);
        }

        asset.systems.Clear();
        Bounds area = new Bounds();
        foreach (Minigame minigame in minigames)
        {
            // Los minijuegos desactivados (objeto o componente) no cuentan
            if (!minigame.enabled || !minigame.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 position = minigame.transform.position;
            asset.systems.Add(new ShipSystems.SystemInfo
            {
                name = minigame.SystemName,
                lossPerSecond = minigame.StabilityLossPerSecond,
                position = position
            });

            if (asset.systems.Count == 1)
            {
                area = new Bounds(position, Vector3.zero);
            }
            else
            {
                area.Encapsulate(position);
            }
        }

        if (asset.autoMapArea)
        {
            // Zona cuadrada (como el mapa) que abarca todos los minijuegos, con un margen
            float halfSize = Mathf.Max(area.extents.x, area.extents.z) + MapMargin;
            asset.mapMin = new Vector2(area.center.x - halfSize, area.center.z - halfSize);
            asset.mapMax = new Vector2(area.center.x + halfSize, area.center.z + halfSize);
        }

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        if (showDialog)
        {
            EditorUtility.DisplayDialog("Minijuegos", $"Actualizadas {asset.systems.Count} zonas en {AssetPath}.", "Vale");
        }
    }
}
