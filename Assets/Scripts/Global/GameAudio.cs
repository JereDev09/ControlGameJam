using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

// Sonido del juego. Todo suena por el mixer de Resources/SoundLibrary (Assets/Sonidos/GameMixer),
// que tiene un grupo para cada tipo de sonido:
// - Musica y Ambiente: un bucle cada uno, que cambia con un fundido (PlayMusic / PlayAmbience).
// - Efectos: sonidos sueltos del juego (PlaySfx), que pueden sonar varios a la vez. Se paran en pausa.
// - Interfaz: botones y menús (PlayUi). Siguen sonando en pausa.
// Y un snapshot para cada sitio (Normal, Menu, Dentro, Pausa): al cambiar de sitio el mixer pasa
// suavemente al suyo. Por ejemplo, en "Dentro" la música está más baja y con un filtro de graves.
// Uso: GameAudio.PlaySfx(GameAudio.Sounds.wireConnected);
// Si un sonido no está puesto, simplemente no suena. Se crea solo y sobrevive a los cambios de escena.
public class GameAudio : MonoBehaviour
{
    // Dónde estás, para elegir el snapshot del mixer (la pausa se nota sola)
    public enum MusicMode
    {
        Normal,   // pilotando
        Menu,
        Inside    // dentro de la nave
    }

    private const int SfxSourceCount = 12;
    private const float FadeDuration = 0.8f;
    private const float SnapshotTransition = 0.6f;

    // Nombres de los grupos y snapshots del mixer (si los renombras allí, cámbialos aquí)
    private const string MusicGroup = "Musica";
    private const string AmbienceGroup = "Ambiente";
    private const string SfxGroup = "Efectos";
    private const string UiGroup = "Interfaz";

    // Un bucle (música o ambiente): su AudioSource y lo que tiene que sonar (aunque aún se esté fundiendo)
    private class LoopChannel
    {
        public AudioSource source;
        public AudioClip clip;
    }

    private static GameAudio instance;

    private SoundLibrary library;
    private LoopChannel music;
    private LoopChannel ambience;
    private AudioSource uiSource;
    private AudioSource[] sfxSources;
    private int nextSfxSource;
    private AudioListener listener;
    private MusicMode musicMode;
    private AudioMixerSnapshot normalSnapshot;
    private AudioMixerSnapshot menuSnapshot;
    private AudioMixerSnapshot insideSnapshot;
    private AudioMixerSnapshot pauseSnapshot;
    private AudioMixerSnapshot currentSnapshot;

    // Todos los sonidos
    public static SoundLibrary Sounds => Instance.library;

    private static GameAudio Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject audioObject = new GameObject("GameAudio");
                DontDestroyOnLoad(audioObject);
                instance = audioObject.AddComponent<GameAudio>();
            }

            return instance;
        }
    }

    // Efecto del juego (grupo Efectos)
    public static void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null)
        {
            return;
        }

        // Se van usando los AudioSource por turnos, así pueden sonar varios efectos a la vez
        GameAudio gameAudio = Instance;
        AudioSource source = gameAudio.sfxSources[gameAudio.nextSfxSource];
        gameAudio.nextSfxSource = (gameAudio.nextSfxSource + 1) % SfxSourceCount;

        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.Play();
    }

    // Sonido de la interfaz (grupo Interfaz): sigue sonando en pausa
    public static void PlayUi(AudioClip clip, float volume = 1f)
    {
        if (clip != null)
        {
            Instance.uiSource.PlayOneShot(clip, volume);
        }
    }

    // null = silencio. Si ya está sonando esa música, sigue sin cortarse (solo cambia el snapshot).
    public static void PlayMusic(AudioClip clip, MusicMode mode = MusicMode.Normal)
    {
        GameAudio gameAudio = Instance;
        gameAudio.musicMode = mode;
        gameAudio.CrossFade(gameAudio.music, clip);
    }

    public static void SetMusicMode(MusicMode mode)
    {
        Instance.musicMode = mode;
    }

    // null = silencio
    public static void PlayAmbience(AudioClip clip)
    {
        GameAudio gameAudio = Instance;
        gameAudio.CrossFade(gameAudio.ambience, clip);
    }

    public static void SetAmbiencePitch(float pitch)
    {
        Instance.ambience.source.pitch = pitch;
    }

    // Para los sonidos pendientes: mientras no estén puestos, se usa otro parecido
    public static AudioClip Either(AudioClip clip, AudioClip fallback)
    {
        return clip != null ? clip : fallback;
    }

    // Bucle propio de un objeto (por ejemplo, la estática del radar), en el grupo Efectos.
    // Devuelve su AudioSource para que el objeto le cambie el volumen y lo pare cuando quiera.
    public static AudioSource CreateLoop(GameObject owner, AudioClip clip)
    {
        AudioSource source = Instance.CreateSource(owner, SfxGroup, true);
        source.clip = clip;
        return source;
    }

    private void Awake()
    {
        library = Resources.Load<SoundLibrary>(SoundLibrary.ResourceName);
        if (library == null)
        {
            Debug.LogWarning("No existe Resources/SoundLibrary. Usa Tools > Sonido > Crear o actualizar biblioteca de sonidos.");
            library = ScriptableObject.CreateInstance<SoundLibrary>();
        }

        music = new LoopChannel { source = CreateSource(gameObject, MusicGroup, true) };
        ambience = new LoopChannel { source = CreateSource(gameObject, AmbienceGroup, true) };
        uiSource = CreateSource(gameObject, UiGroup, false);
        sfxSources = new AudioSource[SfxSourceCount];
        for (int i = 0; i < SfxSourceCount; i++)
        {
            sfxSources[i] = CreateSource(gameObject, SfxGroup, false);
        }

        // En pausa (AudioListener.pause) se para todo menos la música y la interfaz
        music.source.ignoreListenerPause = true;
        uiSource.ignoreListenerPause = true;

        if (library.mixer != null)
        {
            normalSnapshot = library.mixer.FindSnapshot("Normal");
            menuSnapshot = library.mixer.FindSnapshot("Menu");
            insideSnapshot = library.mixer.FindSnapshot("Dentro");
            pauseSnapshot = library.mixer.FindSnapshot("Pausa");
        }

        // El "oído" del juego vive aquí y no en las cámaras: al cambiar de escena hay un momento
        // sin cámara y, sin oído, la música se cortaría. Todos los sonidos son 2D, así que da igual
        // dónde esté. Los AudioListener de las cámaras se apagan.
        listener = gameObject.AddComponent<AudioListener>();
        DisableSceneListeners();
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Sonidos de cosas que pasan en cualquier escena
        ShipState.SystemBroke += OnSystemBroke;
        FailureDirector.OverloadExploded += OnOverloadExploded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ShipState.SystemBroke -= OnSystemBroke;
        FailureDirector.OverloadExploded -= OnOverloadExploded;
    }

    // El mixer pasa suavemente al snapshot del sitio donde estás
    private void Update()
    {
        AudioMixerSnapshot target = GetSnapshot();
        if (target != null && target != currentSnapshot)
        {
            currentSnapshot = target;
            target.TransitionTo(SnapshotTransition);
        }
    }

    private AudioMixerSnapshot GetSnapshot()
    {
        if (PauseMenu.IsPaused)
        {
            return pauseSnapshot;
        }

        switch (musicMode)
        {
            case MusicMode.Menu:
                return menuSnapshot;
            case MusicMode.Inside:
                return insideSnapshot;
            default:
                return normalSnapshot;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DisableSceneListeners();
    }

    private void DisableSceneListeners()
    {
        foreach (AudioListener sceneListener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        {
            if (sceneListener != listener)
            {
                sceneListener.enabled = false;
            }
        }
    }

    private void OnSystemBroke(string systemName)
    {
        PlaySfx(library.failureWarning);
    }

    private void OnOverloadExploded()
    {
        PlaySfx(Either(library.overloadExplosion, library.shipExplosion), 0.4f);
    }

    private AudioSource CreateSource(GameObject owner, string groupName, bool loop)
    {
        AudioSource source = owner.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = FindGroup(groupName);
        source.loop = loop;
        source.playOnAwake = false;
        return source;
    }

    private AudioMixerGroup FindGroup(string groupName)
    {
        if (library.mixer == null)
        {
            return null;
        }

        AudioMixerGroup[] groups = library.mixer.FindMatchingGroups(groupName);
        if (groups.Length == 0)
        {
            Debug.LogWarning($"El mixer no tiene el grupo {groupName}");
            return null;
        }

        return groups[0];
    }

    // Cambia el bucle de un canal con un fundido. Si ya está sonando ese mismo, no hace nada.
    private void CrossFade(LoopChannel channel, AudioClip clip)
    {
        if (channel.clip == clip)
        {
            return;
        }

        channel.clip = clip;
        AudioSource source = channel.source;

        // SetUpdate(true): el fundido funciona aunque el juego esté en pausa
        source.DOKill();
        source.DOFade(0f, source.isPlaying ? FadeDuration : 0f).SetUpdate(true).OnComplete(() =>
        {
            source.clip = clip;
            source.pitch = 1f;
            if (clip == null)
            {
                source.Stop();
                return;
            }

            source.Play();
            source.DOFade(1f, FadeDuration).SetUpdate(true);
        });
    }
}
