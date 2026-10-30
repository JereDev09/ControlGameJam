using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Botones del menú Tools > Minijuegos.
public static class MinigameTools
{
    private const string MinigamePrefabFolder = "Assets/Prefabs/Minigames";
    private const string UIPrefabFolder = "Assets/Prefabs/UI";

    // Para cada minijuego de la escena abierta: le crea la UI y las partículas (si no las tiene)
    // y lo guarda como prefab en Assets/Prefabs/Minigames
    [MenuItem("Tools/Minijuegos/Convertir minijuegos de la escena en prefabs")]
    private static void ConvertSceneMinigames()
    {
        Minigame[] minigames = Object.FindObjectsByType<Minigame>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (minigames.Length == 0)
        {
            EditorUtility.DisplayDialog("Minijuegos", "No hay minijuegos en la escena abierta.", "Vale");
            return;
        }

        CreateFolder(MinigamePrefabFolder);
        int converted = 0;
        int skipped = 0;

        foreach (Minigame minigame in minigames)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(minigame))
            {
                Debug.Log($"{minigame.name} ya es un prefab, lo salto. Si quieres rehacer su UI usa Tools > Minijuegos > Regenerar UI.", minigame);
                skipped++;
                continue;
            }

            if (minigame.UIRoot == null)
            {
                minigame.CreateUI();
            }

            AddParticles(minigame);

            string path = AssetDatabase.GenerateUniqueAssetPath($"{MinigamePrefabFolder}/{minigame.name}.prefab");
            PrefabUtility.SaveAsPrefabAssetAndConnect(minigame.gameObject, path, InteractionMode.UserAction);
            converted++;
        }

        MinigameUI.EnsureEventSystem();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Minijuegos",
            $"Convertidos: {converted}\nYa eran prefabs: {skipped}\n\n" +
            $"Los prefabs están en {MinigamePrefabFolder}. Para editar una ventana, abre el prefab y activa su MinigameCanvas.\n\n" +
            "No olvides guardar la escena.", "Vale");
    }

    // Borra la UI del minijuego seleccionado y la vuelve a crear desde cero
    // (por ejemplo si cambias el tamaño de una cuadrícula). Si es un prefab, cambia el prefab.
    [MenuItem("Tools/Minijuegos/Regenerar UI del minijuego seleccionado")]
    private static void RegenerateSelected()
    {
        GameObject selected = Selection.activeGameObject;
        Minigame minigame = selected != null ? selected.GetComponentInParent<Minigame>(true) : null;
        if (minigame == null)
        {
            EditorUtility.DisplayDialog("Minijuegos", "Selecciona un objeto con un minijuego (en la escena o un prefab).", "Vale");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog("Regenerar UI",
            $"Se borrará la UI de {minigame.name}, con todos los cambios que le hayas hecho, y se creará de nuevo.\n\nNo se puede deshacer.",
            "Regenerar", "Cancelar");
        if (!confirmed)
        {
            return;
        }

        string prefabPath = AssetDatabase.GetAssetPath(minigame);
        if (string.IsNullOrEmpty(prefabPath))
        {
            prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(minigame);
        }

        if (string.IsNullOrEmpty(prefabPath))
        {
            RebuildUI(minigame);
            EditorSceneManager.MarkSceneDirty(minigame.gameObject.scene);
            return;
        }

        // Es un prefab: se abre su contenido, se cambia y se guarda, así se actualizan todas sus copias
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        RebuildUI(prefabRoot.GetComponentInChildren<Minigame>(true));
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
        Debug.Log($"UI regenerada en {prefabPath}");
    }

    // Crea el prefab del HUD de estabilidad (alerta, mapa y panel) o regenera su UI si ya existe,
    // y lo añade a la escena abierta si no lo tiene. Úsalo en la escena de pilotar y en la de primera persona.
    [MenuItem("Tools/Minijuegos/Crear o actualizar HUD de estabilidad")]
    private static void CreateOrUpdateStabilityHUD()
    {
        string path = $"{UIPrefabFolder}/StabilityHUD.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            CreateFolder(UIPrefabFolder);
            GameObject hudObject = new GameObject("StabilityHUD");
            hudObject.AddComponent<StabilityHUD>().CreateUI(GetCircleSprite());
            PrefabUtility.SaveAsPrefabAsset(hudObject, path);
            Object.DestroyImmediate(hudObject);
        }
        else if (EditorUtility.DisplayDialog("HUD de estabilidad",
                     "El prefab StabilityHUD ya existe. ¿Regenerar su UI para añadir lo nuevo? Se pierden los cambios que le hayas hecho a su UI.",
                     "Regenerar", "No, solo añadirlo a la escena"))
        {
            // Se borra la UI vieja dentro del prefab y se crea de nuevo
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            for (int i = prefabRoot.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(prefabRoot.transform.GetChild(i).gameObject);
            }

            prefabRoot.GetComponent<StabilityHUD>().CreateUI(GetCircleSprite());
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        if (Object.FindFirstObjectByType<StabilityHUD>(FindObjectsInactive.Include) == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            Undo.RegisterCreatedObjectUndo(instance, "Añadir HUD de estabilidad");
            EditorSceneManager.MarkSceneDirty(instance.scene);
        }

        EditorUtility.DisplayDialog("HUD de estabilidad",
            $"Listo. El HUD está en {path} y en la escena abierta.\n\n" +
            "Haz lo mismo en la otra escena (pilotar / primera persona) y guarda las dos.", "Vale");
    }

    // Para la escena de primera persona: luces que parpadean, vista borrosa, temblores...
    [MenuItem("Tools/Minijuegos/Añadir efectos de averías (interior)")]
    private static void CreateInteriorFailureEffects()
    {
        if (Object.FindFirstObjectByType<InteriorFailureEffects>() != null)
        {
            EditorUtility.DisplayDialog("Minijuegos", "Esta escena ya tiene InteriorFailureEffects.", "Vale");
            return;
        }

        GameObject effectsObject = new GameObject("InteriorFailureEffects");
        effectsObject.AddComponent<InteriorFailureEffects>();
        Undo.RegisterCreatedObjectUndo(effectsObject, "Añadir efectos de averías");
        EditorSceneManager.MarkSceneDirty(effectsObject.scene);
        EditorUtility.DisplayDialog("Minijuegos", "Añadido InteriorFailureEffects a la escena. No olvides guardar la escena.", "Vale");
    }

    // Crea un cubo con la terminal de estabilidad (y su ventana) donde mira la vista de escena,
    // y lo guarda como prefab. Puedes cambiarle el modelo después.
    [MenuItem("Tools/Minijuegos/Crear terminal de estabilidad")]
    private static void CreateStabilityTerminal()
    {
        GameObject terminalObject = CreatePlaceholderCube("StabilityTerminal");
        terminalObject.AddComponent<StabilityTerminal>().CreateUI();

        CreateFolder(UIPrefabFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{UIPrefabFolder}/StabilityTerminal.prefab");
        PrefabUtility.SaveAsPrefabAssetAndConnect(terminalObject, path, InteractionMode.UserAction);
        MinigameUI.EnsureEventSystem();
        EditorSceneManager.MarkSceneDirty(terminalObject.scene);

        EditorUtility.DisplayDialog("Minijuegos",
            $"Terminal creada (seleccionada en la escena) y guardada en {path}. Colócala donde quieras.\n\n" +
            "No olvides guardar la escena.", "Vale");
    }

    // Círculo blanco que trae Unity (el mismo del tirador de los Slider)
    private static Sprite GetCircleSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
    }

    // Cubo de marcador de posición en el centro de la vista de escena
    public static GameObject CreatePlaceholderCube(string name)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;

        SceneView view = SceneView.lastActiveSceneView;
        cube.transform.position = view != null ? view.pivot : Vector3.zero;

        Undo.RegisterCreatedObjectUndo(cube, $"Crear {name}");
        Selection.activeGameObject = cube;
        return cube;
    }

    private static void RebuildUI(Minigame minigame)
    {
        if (minigame.UIRoot != null)
        {
            Object.DestroyImmediate(minigame.UIRoot);
        }

        minigame.CreateUI();
        AddParticles(minigame);
        EditorUtility.SetDirty(minigame);
    }

    // Crea las partículas de éxito y fallo encima del objeto (si no las tiene ya)
    private static void AddParticles(Minigame minigame)
    {
        SerializedObject serializedMinigame = new SerializedObject(minigame);
        SerializedProperty success = serializedMinigame.FindProperty("successParticles");
        SerializedProperty fail = serializedMinigame.FindProperty("failParticles");

        Bounds bounds = ParticleFactory.GetBounds(minigame.gameObject);
        Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

        if (success.objectReferenceValue == null)
        {
            success.objectReferenceValue = ParticleFactory.CreateBurst("SuccessParticles", minigame.transform, top, 40,
                new Vector2(0.5f, 1f), new Vector2(2f, 5f), new Vector2(0.05f, 0.15f),
                new Color(0.3f, 1f, 0.5f), new Color(0.4f, 0.9f, 1f), -0.2f, 0.2f, 0f);
        }

        if (fail.objectReferenceValue == null)
        {
            fail.objectReferenceValue = ParticleFactory.CreateBurst("FailParticles", minigame.transform, top, 40,
                new Vector2(0.5f, 1f), new Vector2(2f, 5f), new Vector2(0.05f, 0.15f),
                new Color(1f, 0.6f, 0.1f), new Color(1f, 0.15f, 0.1f), 1f, 0.2f, 0f);
        }

        serializedMinigame.ApplyModifiedPropertiesWithoutUndo();
    }

    // Crea una carpeta (y las de encima) si no existe, por ejemplo "Assets/Prefabs/UI"
    public static void CreateFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        CreateFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
