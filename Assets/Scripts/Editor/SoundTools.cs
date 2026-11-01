using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools > Sonido: crea Resources/SoundLibrary y le pone los sonidos de Assets/Sonidos/SonidosGameJam
// según el nombre de cada carpeta. Los que ya estén puestos no se tocan.
public static class SoundTools
{
    private const string LibraryPath = "Assets/Resources/SoundLibrary.asset";
    private const string SoundsFolder = "Assets/Sonidos/SonidosGameJam";
    private const string MixerPath = "Assets/Sonidos/GameMixer.mixer";

    // Campo de SoundLibrary -> carpeta donde está su sonido
    private static readonly Dictionary<string, string> Folders = new Dictionary<string, string>
    {
        { "gameMusic", "SoundTrack para tercera persona" },
        { "interiorAmbience", "Sonido ambiental dentro de la nave" },
        { "shipEngineLoop", "Sonido de la nave en movimiento" },
        { "asteroidImpact", "Sonido del asteroide cuando impacta" },
        { "shipExplosion", "Sonido de cuando explota la nave" },
        { "failureWarning", "Sonido cuando hay una falla en la nave" },
        { "enemyDetected", "Enemigo detectado" },
        { "shot", "Sonido de disparo" },
        { "footstep", "Sonido de un paso, del jugador" },
        { "minigameCompleted", "Sonido para ¡COMPLETED!" },
        { "minigameFailed", "Sonido de fallo" },
        { "wireConnected", "Sonido para juego de cables (conectado)" },
        { "memoryClick", "Sonido para juego de memoria (click)" },
        { "valveClick", "Sonido para minijuego de control de presion (click)" },
        { "partCollected", "Sonido para minijuego de obtener piezas (obtener, mas pitch mientras mas progresas)" },
        { "radioStatic", "Sonido para el minijuego de frecuencia (fondo)" },
        { "menuButton", "Sonido para botones del menu" }
    };

    // Los bucles largos se cargan poco a poco (streaming) para no ocupar mucha memoria
    private static readonly string[] StreamedFields = { "gameMusic", "interiorAmbience", "shipEngineLoop", "radioStatic" };

    [MenuItem("Tools/Sonido/Crear o actualizar biblioteca de sonidos")]
    private static void UpdateLibrary()
    {
        SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
        if (library == null)
        {
            MinigameTools.CreateFolder("Assets/Resources");
            library = ScriptableObject.CreateInstance<SoundLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        SerializedObject serializedLibrary = new SerializedObject(library);
        List<string> missingFolders = new List<string>();
        int assigned = 0;

        // El mixer (Assets/Sonidos/GameMixer)
        SerializedProperty mixerProperty = serializedLibrary.FindProperty("mixer");
        if (mixerProperty.objectReferenceValue == null)
        {
            mixerProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(MixerPath);
        }

        foreach (KeyValuePair<string, string> entry in Folders)
        {
            SerializedProperty property = serializedLibrary.FindProperty(entry.Key);
            if (property.objectReferenceValue != null)
            {
                continue;
            }

            AudioClip clip = FindClip($"{SoundsFolder}/{entry.Value}");
            if (clip == null)
            {
                missingFolders.Add(entry.Value);
                continue;
            }

            property.objectReferenceValue = clip;
            assigned++;

            if (System.Array.IndexOf(StreamedFields, entry.Key) >= 0)
            {
                SetStreaming(clip);
            }
        }

        serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Selection.activeObject = library;

        string message = $"Sonidos puestos: {assigned}.\n\n";
        if (missingFolders.Count > 0)
        {
            message += "No encontré sonido en estas carpetas:\n- " + string.Join("\n- ", missingFolders) + "\n\n";
        }

        message += "Los de la sección \"Pendientes\" se ponen a mano en Resources/SoundLibrary cuando los tengas.";
        EditorUtility.DisplayDialog("Sonido", message, "Vale");
    }

    // El primer sonido que haya en la carpeta
    private static AudioClip FindClip(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return null;
        }

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
        return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
    }

    private static void SetStreaming(AudioClip clip)
    {
        AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        if (settings.loadType == AudioClipLoadType.Streaming)
        {
            return;
        }

        settings.loadType = AudioClipLoadType.Streaming;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
    }
}
