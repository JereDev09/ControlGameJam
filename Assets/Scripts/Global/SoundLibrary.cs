using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

// Todos los sonidos del juego y el mixer por el que suenan. Vive en Assets/Resources/SoundLibrary.
// Los volúmenes se tocan en el mixer (Assets/Sonidos/GameMixer).
// Tools > Sonido > Crear o actualizar biblioteca de sonidos la rellena sola con los sonidos de
// Assets/Sonidos/SonidosGameJam (por el nombre de cada carpeta). Los que estén vacíos no suenan:
// cuando tengas uno de los pendientes, ponlo aquí y ya sonará donde toca.
public class SoundLibrary : ScriptableObject
{
    public const string ResourceName = "SoundLibrary";

    [Tooltip("Mixer con los volúmenes de cada grupo (Musica, Ambiente, Efectos, Interfaz) y un snapshot " +
             "por cada sitio (Normal, Menu, Dentro, Pausa). Ábrelo con Window > Audio > Audio Mixer")]
    public AudioMixer mixer;

    [Header("Música y ambiente (en bucle)")]
    [Tooltip("Música del juego: suena todo el rato (en el menú, pilotando y dentro de la nave) sin cortarse")]
    [FormerlySerializedAs("pilotingMusic")]
    public AudioClip gameMusic;
    [Tooltip("Ambiente dentro de la nave (primera persona)")]
    public AudioClip interiorAmbience;
    [Tooltip("Motor de la nave mientras se pilota (cambia de tono con la velocidad)")]
    public AudioClip shipEngineLoop;

    [Header("Nave")]
    public AudioClip asteroidImpact;
    public AudioClip shipExplosion;
    [Tooltip("Cuando se avería una zona de la nave")]
    public AudioClip failureWarning;
    public AudioClip enemyDetected;
    [Tooltip("Disparos (los de la nave y, más graves, los de los enemigos)")]
    public AudioClip shot;

    [Header("Jugador")]
    public AudioClip footstep;

    [Header("Minijuegos")]
    public AudioClip minigameCompleted;
    public AudioClip minigameFailed;
    [Tooltip("Cables: al conectar una pareja")]
    public AudioClip wireConnected;
    [Tooltip("Sobrecarga (memoria): cada botón que se ilumina o se pulsa (con un tono distinto por botón)")]
    public AudioClip memoryClick;
    [Tooltip("Presión: al girar una válvula")]
    public AudioClip valveClick;
    [Tooltip("Recuperar piezas: al recoger una (más agudo cuanto más avanzas)")]
    public AudioClip partCollected;
    [Tooltip("Radar: estática de fondo (baja al sintonizar)")]
    public AudioClip radioStatic;

    [Header("Interfaz")]
    public AudioClip menuButton;

    [Header("Pendientes (ponlos cuando los tengas y ya sonarán)")]
    [Tooltip("Explosiones internas de la sobrecarga. Si está vacío, suena la explosión de la nave más bajo")]
    public AudioClip overloadExplosion;
    [Tooltip("Cada segundo de la cuenta atrás de explosión")]
    public AudioClip meltdownAlarm;
    [Tooltip("Al entrar en la nave y al volver a pilotar")]
    public AudioClip transitionWhoosh;
    [Tooltip("Cuando aparece un mensaje del tutorial. Si está vacío, suena el botón del menú")]
    public AudioClip notification;
    [Tooltip("Circuitos: al girar una pieza. Si está vacío, suena el click de memoria")]
    public AudioClip circuitRotate;
    [Tooltip("Navegación: cada tramo de la ruta. Si está vacío, suena el click de memoria")]
    public AudioClip navigationStep;
    [Tooltip("Radar: al encontrar la frecuencia")]
    public AudioClip radarTuned;
}
