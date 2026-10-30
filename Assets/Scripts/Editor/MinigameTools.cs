using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Botones del menú Tools para preparar los minijuegos y la nave.
public static class MinigameTools
{
    private const string PrefabFolder = "Assets/Prefabs/Minigames";
    private const string ParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

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

        CreatePrefabFolder();
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

            string path = AssetDatabase.GenerateUniqueAssetPath($"{PrefabFolder}/{minigame.name}.prefab");
            PrefabUtility.SaveAsPrefabAssetAndConnect(minigame.gameObject, path, InteractionMode.UserAction);
            converted++;
        }

        MinigameUI.EnsureEventSystem();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Minijuegos",
            $"Convertidos: {converted}\nYa eran prefabs: {skipped}\n\n" +
            $"Los prefabs están en {PrefabFolder}. Para editar una ventana, abre el prefab y activa su MinigameCanvas.\n\n" +
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

    // Añade el efecto de propulsión a todos los objetos de la escena que se llamen PivoteFuego...
    [MenuItem("Tools/Nave/Añadir efecto a los fuegos (PivoteFuego...)")]
    private static void SetupThrusterFlames()
    {
        int added = 0;
        foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.name.StartsWith("PivoteFuego") && candidate.GetComponent<ThrusterFlame>() == null)
            {
                Undo.AddComponent<ThrusterFlame>(candidate.gameObject);
                added++;
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Nave",
            added > 0 ? $"Añadido ThrusterFlame a {added} fuegos. No olvides guardar la escena." : "No encontré fuegos nuevos (objetos llamados PivoteFuego... sin ThrusterFlame).",
            "Vale");
    }

    // Añade el contador de distancia al pedido en el mismo objeto que la nave
    [MenuItem("Tools/Nave/Añadir contador de distancia")]
    private static void SetupDeliveryDistance()
    {
        ShipMovement ship = Object.FindFirstObjectByType<ShipMovement>();
        if (ship == null)
        {
            EditorUtility.DisplayDialog("Nave", "No hay ninguna nave (ShipMovement) en la escena abierta.", "Vale");
            return;
        }

        if (ship.GetComponent<DeliveryDistance>() != null)
        {
            EditorUtility.DisplayDialog("Nave", $"{ship.name} ya tiene el contador de distancia.", "Vale");
            return;
        }

        Undo.AddComponent<DeliveryDistance>(ship.gameObject);
        EditorSceneManager.MarkSceneDirty(ship.gameObject.scene);
        EditorUtility.DisplayDialog("Nave", $"Añadido DeliveryDistance a {ship.name}. No olvides guardar la escena.", "Vale");
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

    private static void CreatePrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Minigames");
        }
    }

    // Crea las partículas de éxito y fallo encima del objeto (si no las tiene ya)
    private static void AddParticles(Minigame minigame)
    {
        SerializedObject serializedMinigame = new SerializedObject(minigame);
        SerializedProperty success = serializedMinigame.FindProperty("successParticles");
        SerializedProperty fail = serializedMinigame.FindProperty("failParticles");
        Vector3 top = GetTopOf(minigame.gameObject);

        if (success.objectReferenceValue == null)
        {
            success.objectReferenceValue = CreateBurstParticles("SuccessParticles", minigame.transform, top,
                new Color(0.3f, 1f, 0.5f), new Color(0.4f, 0.9f, 1f), -0.2f);
        }

        if (fail.objectReferenceValue == null)
        {
            fail.objectReferenceValue = CreateBurstParticles("FailParticles", minigame.transform, top,
                new Color(1f, 0.6f, 0.1f), new Color(1f, 0.15f, 0.1f), 1f);
        }

        serializedMinigame.ApplyModifiedPropertiesWithoutUndo();
    }

    // Una explosión de chispas que se apagan. gravity < 0 hace que suban, > 0 que caigan.
    private static ParticleSystem CreateBurstParticles(string name, Transform parent, Vector3 position, Color colorA, Color colorB, float gravity)
    {
        GameObject particlesObject = new GameObject(name);
        particlesObject.transform.SetParent(parent, false);
        particlesObject.transform.position = position;

        ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        Gradient fadeOut = new Gradient();
        fadeOut.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = fadeOut;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        Material material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
        if (material == null)
        {
            Debug.LogWarning($"No encontré el material de partículas de URP ({ParticleMaterialPath}). Asígnale uno a {name}.", particlesObject);
        }

        particlesObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        return particles;
    }

    // Punto más alto del objeto (según su Renderer), para que las partículas salgan de arriba
    private static Vector3 GetTopOf(GameObject target)
    {
        Renderer renderer = target.GetComponentInChildren<Renderer>();
        if (renderer == null)
        {
            return target.transform.position + Vector3.up * 0.5f;
        }

        Bounds bounds = renderer.bounds;
        return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
    }
}
