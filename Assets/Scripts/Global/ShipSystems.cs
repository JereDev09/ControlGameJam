using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Ajustes de las zonas de la nave. Vive en Assets/Resources/ShipSystems.
// - Zonas y mapa: se actualizan solos al guardar la escena de primera persona
//   (o con Tools > Minijuegos > Actualizar zonas y mapa). No hace falta tocarlos.
// - Dificultad, efectos y derrota: edítalos a mano en el Inspector.
public class ShipSystems : ScriptableObject
{
    public const string ResourceName = "ShipSystems";

    [System.Serializable]
    public class SystemInfo
    {
        public string name;
        public float lossPerSecond;
        public Vector3 position;
    }

    [Header("Zonas (automático)")]
    public List<SystemInfo> systems = new List<SystemInfo>();

    [Header("Mapa")]
    [Tooltip("Si está marcado, la zona del mapa se calcula sola para que quepan todos los minijuegos")]
    public bool autoMapArea = true;
    [Tooltip("Esquina del mundo (X, Z) que corresponde a la esquina de abajo a la izquierda del fondo del mapa")]
    public Vector2 mapMin = new Vector2(-10f, -10f);
    [Tooltip("Esquina del mundo (X, Z) que corresponde a la esquina de arriba a la derecha del fondo del mapa")]
    public Vector2 mapMax = new Vector2(10f, 10f);

    [Header("Viaje")]
    [Tooltip("Kilómetros hasta el destino")]
    public float journeyDistance = 54000f;
    [Tooltip("Kilómetros que avanza la nave por segundo a velocidad normal (solo mientras se pilota)")]
    public float kilometersPerSecond = 120f;

    // En las curvas, el eje X es el progreso del viaje: 0 = salida, 1 = destino
    [Header("Dificultad (curvas: X = progreso del viaje, de 0 a 1)")]
    [Tooltip("Segundos de calma al terminar el tutorial, antes de la primera avería normal")]
    public float firstFailureDelay = 6f;
    [Tooltip("Cuántas zonas pueden estar averiadas a la vez")]
    public AnimationCurve maxFailuresAtOnce = AnimationCurve.Linear(0f, 2f, 1f, 4f);
    [Tooltip("Segundos entre una avería y la siguiente")]
    public AnimationCurve secondsBetweenFailures = AnimationCurve.Linear(0f, 15f, 1f, 7f);
    [Tooltip("Multiplica lo rápido que pierden estabilidad las zonas averiadas")]
    public AnimationCurve lossMultiplier = AnimationCurve.Linear(0f, 6f, 1f, 10f);
    [Tooltip("Con varias zonas averiadas, todas menos la primera pierden estabilidad multiplicada por esto")]
    [Range(0f, 1f)] public float othersLossMultiplier = 0.5f;
    [Tooltip("A partir de este progreso del viaje ya no hay piedad: todas las averías bajan a velocidad normal")]
    [Range(0f, 1f)] public float noMercyProgress = 0.9f;

    [Header("Tutorial (al empezar una partida)")]
    public bool tutorialEnabled = true;
    [Tooltip("Segundos pilotando antes de la primera avería (la electricidad). Hasta entonces no se puede entrar en la nave")]
    public float tutorialFirstFailureDelay = 14f;
    [Tooltip("Segundos pilotando entre una avería del tutorial y la siguiente")]
    public float tutorialStepDelay = 16f;
    [Tooltip("Estabilidad hasta la que baja cada avería del tutorial, para que se note su efecto. " +
             "Ahí se queda: durante el tutorial no baja más ni hace daño a la nave")]
    [Range(0f, 100f)] public float tutorialBreakStability = 10f;
    [Tooltip("Lo rápido (% por segundo) que baja la avería del tutorial hasta ese nivel")]
    public float tutorialDropSpeed = 20f;
    [Tooltip("Cuántas zonas al azar se averían, una a una, después de la electricidad")]
    public int tutorialExtraFailures = 1;
    [Tooltip("Mientras pilotas con una avería. {zone} = nombre de la zona, {key} = tecla para entrar en la nave")]
    public string tutorialEnterMessage = "¡Avería en {zone}! Pulsa {key} para entrar en la nave y repararla";
    [Tooltip("Dentro de la nave, con la avería sin reparar. {zone} = nombre de la zona")]
    public string tutorialRepairMessage = "Busca la zona de {zone} (en rojo en el mapa) y repárala";
    [Tooltip("Dentro de la nave, después de repararla")]
    public string tutorialBackMessage = "¡Reparado! Vuelve al asiento del piloto para seguir el viaje";

    [Header("Efectos de las averías")]
    [Tooltip("Por debajo de esta estabilidad empiezan los efectos (suaves) y salta la alerta. A 0% son máximos")]
    [Range(1f, 100f)] public float dangerThreshold = 30f;
    [Tooltip("Presión: vida que pierde la nave por segundo con la presión a 0%")]
    public float pressureDamagePerSecond = 2f;
    [Tooltip("Sobrecarga: vida que quita cada explosión interna")]
    public float overloadExplosionDamage = 8f;
    [Tooltip("Sobrecarga: segundos entre explosiones (con la avería suave y con la avería al máximo)")]
    public Vector2 overloadExplosionInterval = new Vector2(12f, 4f);

    [Header("Derrota")]
    [Tooltip("Por debajo de esta estabilidad una zona cuenta como crítica")]
    [Range(0f, 100f)] public float criticalStability = 10f;
    [Tooltip("Si este número de zonas está en estado crítico a la vez, empieza la cuenta atrás para explotar")]
    [FormerlySerializedAs("zonesAtZeroToLose")] public int criticalZonesToLose = 3;
    [Tooltip("Segundos de aviso antes de explotar. Si reparas alguna zona a tiempo, se cancela")]
    public float meltdownSeconds = 6f;
    [Tooltip("Escena donde se ve explotar la nave si estás dentro cuando ocurre")]
    public string pilotingScene = "MinijuegoPrincipal";
    [Tooltip("Escena con la imagen de la derrota, después de ver explotar la nave")]
    public string gameOverScene = "Derrota";
    [Tooltip("Escena del menú principal (se usa si falta alguna de las otras)")]
    public string menuScene = "MenuPrincipal";

    [Header("Final")]
    [Tooltip("Escena que se carga al llegar al destino (con la imagen del final)")]
    public string endingScene = "Final";
}
