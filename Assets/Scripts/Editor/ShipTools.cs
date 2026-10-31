using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Botones del menú Tools > Nave y Tools > Menú.
public static class ShipTools
{
    private const string ShipScenePath = "Assets/Scenes/MinijuegoPrincipal.unity";
    private const string MenuScenePath = "Assets/Scenes/MenuPrincipal.unity";
    private const string EndingScenePath = "Assets/Scenes/Final.unity";
    private const string GameOverScenePath = "Assets/Scenes/Derrota.unity";

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

    // Vincula las líneas de velocidad (objetos que se llamen "Lineas de velocidad...") con la velocidad de la nave
    [MenuItem("Tools/Nave/Vincular líneas de velocidad")]
    private static void SetupSpeedLines()
    {
        int added = 0;
        foreach (ParticleSystem candidate in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.name.StartsWith("Lineas de velocidad") && candidate.GetComponent<SpeedLines>() == null)
            {
                Undo.AddComponent<SpeedLines>(candidate.gameObject);
                added++;
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Nave",
            added > 0 ? $"Añadido SpeedLines a {added} sistemas de partículas. No olvides guardar la escena." : "No encontré partículas nuevas llamadas \"Lineas de velocidad\" sin SpeedLines.",
            "Vale");
    }

    // Añade a la nave el progreso del viaje (DeliveryDistance) y su barra con el icono de la nave
    [MenuItem("Tools/Nave/Añadir barra de progreso del viaje")]
    private static void SetupDeliveryProgress()
    {
        ShipMovement ship = FindShip();
        if (ship == null)
        {
            return;
        }

        DeliveryDistance distance = ship.GetComponent<DeliveryDistance>();
        if (distance == null)
        {
            distance = Undo.AddComponent<DeliveryDistance>(ship.gameObject);
        }

        if (distance.UIRoot != null)
        {
            EditorUtility.DisplayDialog("Nave", $"{ship.name} ya tiene la barra de progreso (ProgressCanvas).", "Vale");
            return;
        }

        distance.CreateUI();
        EditorUtility.SetDirty(distance);
        EditorSceneManager.MarkSceneDirty(ship.gameObject.scene);
        EditorUtility.DisplayDialog("Nave", $"Barra de progreso añadida a {ship.name} (ProgressCanvas). No olvides guardar la escena.", "Vale");
    }

    // Crea las partículas de explosión (fuego, humo y chispas) como hijas de la nave
    // y se las asigna, para que exploten cuando se quede sin vida
    [MenuItem("Tools/Nave/Crear explosión de la nave")]
    private static void CreateShipExplosion()
    {
        ShipMovement ship = FindShip();
        if (ship == null)
        {
            return;
        }

        SerializedObject serializedShip = new SerializedObject(ship);
        SerializedProperty explosion = serializedShip.FindProperty("explosionParticles");
        if (explosion.objectReferenceValue != null)
        {
            EditorUtility.DisplayDialog("Nave", $"{ship.name} ya tiene explosión.", "Vale");
            return;
        }

        // Los tamaños y velocidades dependen del tamaño de la nave, sea cual sea su escala
        Bounds bounds = ParticleFactory.GetBounds(ship.gameObject);
        float shipSize = Mathf.Max(bounds.size.magnitude, 0.1f);
        Vector3 center = bounds.center;

        ParticleSystem fire = ParticleFactory.CreateBurst("Explosion", ship.transform, center, 30,
            new Vector2(0.4f, 0.8f), new Vector2(0.5f, 1.5f) * shipSize, new Vector2(0.2f, 0.45f) * shipSize,
            new Color(1f, 0.9f, 0.3f), new Color(1f, 0.4f, 0.05f), 0f, shipSize * 0.1f, 1.5f);

        // El humo y las chispas son hijos del fuego, así se lanzan los tres a la vez
        ParticleFactory.CreateBurst("Smoke", fire.transform, center, 20,
            new Vector2(1.5f, 2.5f), new Vector2(0.2f, 0.6f) * shipSize, new Vector2(0.3f, 0.6f) * shipSize,
            new Color(0.25f, 0.25f, 0.25f, 0.8f), new Color(0.4f, 0.4f, 0.4f, 0.6f), -0.05f, shipSize * 0.15f, 2f);
        ParticleFactory.CreateBurst("Sparks", fire.transform, center, 40,
            new Vector2(0.5f, 1.2f), new Vector2(1.5f, 3f) * shipSize, new Vector2(0.02f, 0.05f) * shipSize,
            new Color(1f, 0.9f, 0.4f), new Color(1f, 0.5f, 0.1f), 0.5f, shipSize * 0.05f, 0f);

        Undo.RegisterCreatedObjectUndo(fire.gameObject, "Crear explosión de la nave");
        explosion.objectReferenceValue = fire;
        serializedShip.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(ship.gameObject.scene);
        EditorUtility.DisplayDialog("Nave", "Explosión creada como hija de la nave (Explosion). No olvides guardar la escena.", "Vale");
    }

    // Añade a la nave el paso a primera persona: al empezar la cámara se aleja de la nave
    // y al pulsar Interact (E) se acerca y cambia de escena
    [MenuItem("Tools/Nave/Añadir cambio a primera persona")]
    private static void SetupPilotingTransition()
    {
        ShipMovement ship = FindShip();
        if (ship == null)
        {
            return;
        }

        if (Object.FindFirstObjectByType<PilotingTransition>() != null)
        {
            EditorUtility.DisplayDialog("Nave", "Esta escena ya tiene PilotingTransition.", "Vale");
            return;
        }

        Undo.AddComponent<PilotingTransition>(ship.gameObject);
        EditorSceneManager.MarkSceneDirty(ship.gameObject.scene);
        EditorUtility.DisplayDialog("Nave", $"Añadido PilotingTransition a {ship.name}. No olvides guardar la escena.", "Vale");
    }

    // Para la escena de primera persona: un cubo que al usarlo te devuelve a pilotar
    [MenuItem("Tools/Nave/Crear asiento de piloto")]
    private static void CreatePilotSeat()
    {
        GameObject seat = MinigameTools.CreatePlaceholderCube("PilotSeat");
        seat.AddComponent<PilotSeat>();
        EditorSceneManager.MarkSceneDirty(seat.scene);
        EditorUtility.DisplayDialog("Nave", "Asiento creado (seleccionado en la escena). Colócalo donde quieras y cámbiale el modelo si quieres.\n\nNo olvides guardar la escena.", "Vale");
    }

    // Añade a la nave los efectos de las averías (tirones, controles que fallan, apagones...)
    // y crea las chispas y el humo que salen de ella
    [MenuItem("Tools/Nave/Añadir efectos de averías")]
    private static void SetupShipFailureEffects()
    {
        ShipMovement ship = FindShip();
        if (ship == null)
        {
            return;
        }

        if (ship.GetComponent<ShipFailureEffects>() != null)
        {
            EditorUtility.DisplayDialog("Nave", $"{ship.name} ya tiene ShipFailureEffects.", "Vale");
            return;
        }

        ShipFailureEffects effects = Undo.AddComponent<ShipFailureEffects>(ship.gameObject);

        // Tamaños y velocidades según el tamaño de la nave, sea cual sea su escala
        Bounds bounds = ParticleFactory.GetBounds(ship.gameObject);
        float shipSize = Mathf.Max(bounds.size.magnitude, 0.1f);

        ParticleSystem sparks = ParticleFactory.CreateLoop("FailureSparks", ship.transform, bounds.center,
            new Vector2(0.3f, 0.7f), new Vector2(1f, 3f) * shipSize, new Vector2(0.02f, 0.05f) * shipSize,
            new Color(1f, 0.9f, 0.4f), new Color(0.4f, 0.8f, 1f), 0.5f, shipSize * 0.2f, 0f);
        ParticleSystem smoke = ParticleFactory.CreateLoop("FailureSmoke", ship.transform, bounds.center,
            new Vector2(1f, 2f), new Vector2(0.1f, 0.3f) * shipSize, new Vector2(0.2f, 0.4f) * shipSize,
            new Color(0.2f, 0.2f, 0.2f, 0.6f), new Color(0.4f, 0.4f, 0.4f, 0.5f), -0.02f, shipSize * 0.15f, 2f);

        SerializedObject serializedEffects = new SerializedObject(effects);
        serializedEffects.FindProperty("sparks").objectReferenceValue = sparks;
        serializedEffects.FindProperty("smoke").objectReferenceValue = smoke;
        serializedEffects.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(ship.gameObject.scene);
        EditorUtility.DisplayDialog("Nave", $"Añadidos ShipFailureEffects, chispas y humo a {ship.name}. No olvides guardar la escena.", "Vale");
    }

    // Crea el menú de pausa en la escena abierta. Úsalo en PersistentGameplay, que siempre está
    // cargada, para que funcione en todas las escenas de juego
    [MenuItem("Tools/Menú/Crear menú de pausa")]
    private static void CreatePauseMenu()
    {
        if (Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include) != null)
        {
            EditorUtility.DisplayDialog("Menú", "Ya hay un menú de pausa en las escenas abiertas.", "Vale");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "PersistentGameplay" &&
            !EditorUtility.DisplayDialog("Menú",
                $"La escena abierta es {scene.name}. Lo normal es ponerlo en PersistentGameplay para que funcione en todas. ¿Crearlo aquí igualmente?",
                "Crear aquí", "Cancelar"))
        {
            return;
        }

        GameObject pauseObject = new GameObject("PauseMenu");
        pauseObject.AddComponent<PauseMenu>().CreateUI();
        Undo.RegisterCreatedObjectUndo(pauseObject, "Crear menú de pausa");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("Menú", $"Menú de pausa creado en {scene.name}. No olvides guardar la escena.", "Vale");
    }

    // Crea la escena del menú principal copiando la de la nave (cielo, partículas, cámara y nave),
    // quitándole lo que es de jugar y añadiéndole la UI del menú
    [MenuItem("Tools/Menú/Crear menú principal")]
    private static void CreateMainMenu()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) != null)
        {
            EditorUtility.DisplayDialog("Menú", $"Ya existe {MenuScenePath}. Bórrala si quieres crearla de nuevo.", "Vale");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (!AssetDatabase.CopyAsset(ShipScenePath, MenuScenePath))
        {
            EditorUtility.DisplayDialog("Menú", $"No pude copiar {ShipScenePath}.", "Vale");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

        foreach (AsteroidSpawner spawner in Object.FindObjectsByType<AsteroidSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(PrefabUtility.IsPartOfPrefabInstance(spawner) ? (Object)spawner : spawner.gameObject);
        }

        foreach (DeliveryDistance distance in Object.FindObjectsByType<DeliveryDistance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (distance.UIRoot != null)
            {
                Object.DestroyImmediate(distance.UIRoot);
            }

            Object.DestroyImmediate(distance);
        }

        foreach (PilotingTransition transition in Object.FindObjectsByType<PilotingTransition>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(transition);
        }

        // La nave se queda de adorno, pero sin control ni barra de vida
        Transform ship = null;
        foreach (ShipMovement movement in Object.FindObjectsByType<ShipMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            ship = movement.transform;
            Object.DestroyImmediate(movement);
        }

        new GameObject("MainMenu").AddComponent<MainMenu>().CreateUI(ship);
        MinigameUI.EnsureEventSystem();
        EditorSceneManager.SaveScene(scene);
        AddSceneFirstInBuild(MenuScenePath);

        EditorUtility.DisplayDialog("Menú",
            $"Menú creado en {MenuScenePath} y puesto el primero en la lista de escenas de la build.\n\n" +
            "Cambia el nombre del juego y los créditos en MainMenu > MenuCanvas. La escena que carga Jugar se elige en el componente MainMenu.", "Vale");
    }

    // Escena del final, al entregar el pedido (una imagen que vuelve al menú pasado un rato)
    [MenuItem("Tools/Menú/Crear escena final")]
    private static void CreateEndingScene()
    {
        CreateImageScene(EndingScenePath, "¡PEDIDO ENTREGADO!", "Gracias por jugar");
    }

    // Escena de la derrota, después de ver explotar la nave (una imagen que vuelve al menú pasado un rato)
    [MenuItem("Tools/Menú/Crear escena de derrota")]
    private static void CreateGameOverScene()
    {
        CreateImageScene(GameOverScenePath, "NAVE DESTRUIDA", "El pedido no llegará a su destino...");
    }

    // Crea una escena con una cámara y un EndingScreen (imagen + textos) y la añade a la build
    private static void CreateImageScene(string path, string title, string subtitle)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
        {
            EditorUtility.DisplayDialog("Menú", $"Ya existe {path}. Bórrala si quieres crearla de nuevo.", "Vale");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Cámara con fondo negro (sin cámara la pantalla no se limpia)
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;

        new GameObject("EndingScreen").AddComponent<EndingScreen>().CreateUI(title, subtitle);

        EditorSceneManager.SaveScene(scene, path);
        AddSceneLastInBuild(path);

        EditorUtility.DisplayDialog("Menú",
            $"Escena creada en {path} y añadida a la lista de escenas de la build.\n\n" +
            "Pon tu imagen en EndingScreen > EndingCanvas > Image (campo Source Image). " +
            "Los segundos que dura se cambian en el componente EndingScreen.", "Vale");
    }

    private static void AddSceneLastInBuild(string path)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(buildScene => buildScene.path == path))
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    private static void AddSceneFirstInBuild(string path)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(buildScene => buildScene.path == path);
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static ShipMovement FindShip()
    {
        ShipMovement ship = Object.FindFirstObjectByType<ShipMovement>();
        if (ship == null)
        {
            EditorUtility.DisplayDialog("Nave", "No hay ninguna nave (ShipMovement) en la escena abierta.", "Vale");
        }

        return ship;
    }
}
