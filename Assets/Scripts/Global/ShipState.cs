using System.Collections.Generic;
using UnityEngine;

// Estado de la nave que se mantiene al cambiar de escena (pilotar <-> primera persona):
// la estabilidad de cada zona, el progreso del viaje y la vida.
// Cuándo se avería cada zona y qué pasa entonces lo decide FailureDirector (y al principio, ShipTutorial).
// Se reinicia al empezar una partida nueva (botón Jugar del menú).
public static class ShipState
{
    public class SystemState
    {
        public string name;
        public float stability = 100f;
        public float lossPerSecond;
        public bool isFailing;    // averiada: pierde estabilidad hasta que se repare
        public float failedAt;    // momento en que se averió (para saber cuál fue la primera)
    }

    private static readonly List<SystemState> systems = new List<SystemState>();
    private static ShipSystems config;
    private static float health = -1f;

    // -1 = todavía no se ha guardado nada (se usan los valores iniciales)
    public static float Distance { get; set; } = -1f;
    public static float TotalDistance { get; set; } = -1f;

    public static float MaxHealth { get; set; } = 100f;
    public static float Health => health < 0f ? MaxHealth : health;
    public static bool IsDestroyed { get; private set; }

    // Progreso del viaje: 0 = salida, 1 = destino
    public static float Progress => TotalDistance > 0f && Distance >= 0f ? 1f - Distance / TotalDistance : 0f;

    // Sin electricidad, las demás terminales no funcionan
    public static bool HasPower => GetSeverity(ShipZones.Electricity) <= 0f;

    // Tutorial: al principio no se puede entrar en la nave y no hay averías al azar (lo controla ShipTutorial)
    public static bool CanEnterShip { get; set; } = true;
    public static bool TutorialFinished { get; set; } = true;

    // Si estamos en la escena de pilotar (lo pone PilotingTransition)
    public static bool IsPiloting { get; set; }

    // Si ya ha empezado una partida (las zonas están cargadas)
    public static bool IsLoaded => config != null;

    // Ajustes de las zonas, el mapa, la dificultad y los efectos (el asset Resources/ShipSystems)
    public static ShipSystems Config
    {
        get
        {
            EnsureLoaded();
            return config;
        }
    }

    public static List<SystemState> Systems
    {
        get
        {
            EnsureLoaded();
            return systems;
        }
    }

    public static void Reset()
    {
        systems.Clear();
        config = null;
        health = -1f;
        IsDestroyed = false;
        Distance = -1f;
        TotalDistance = -1f;
        FailureDirector.Remove();
        ShipTutorial.Remove();
    }

    public static bool HasZone(string systemName)
    {
        return Find(systemName) != null;
    }

    public static bool IsFailing(string systemName)
    {
        SystemState system = Find(systemName);
        return system != null && system.isFailing;
    }

    // Avería una zona: empieza a perder estabilidad hasta que se repare
    public static void Break(string systemName)
    {
        SystemState system = Find(systemName);
        if (system != null && !system.isFailing)
        {
            system.isFailing = true;
            system.failedAt = Time.time;
        }
    }

    // De 0 a 100
    public static float GetStability(string systemName)
    {
        SystemState system = Find(systemName);
        return system != null ? system.stability : 100f;
    }

    // Al dejarla al 100% (minijuego completado) la zona deja de estar averiada
    public static void SetStability(string systemName, float stability)
    {
        SystemState system = Find(systemName);
        if (system == null)
        {
            return;
        }

        system.stability = Mathf.Clamp(stability, 0f, 100f);
        if (system.stability >= 100f)
        {
            system.isFailing = false;
        }
    }

    // Lo grave que es la avería de una zona, para sus efectos:
    // 0 = por encima del umbral de peligro (sin efectos), 1 = a 0% (efectos al máximo)
    public static float GetSeverity(string systemName)
    {
        float threshold = Config.dangerThreshold;
        return Mathf.Clamp01((threshold - GetStability(systemName)) / threshold);
    }

    public static void Damage(float amount)
    {
        if (IsDestroyed || amount <= 0f)
        {
            return;
        }

        health = Mathf.Max(0f, Health - amount);
        if (health <= 0f)
        {
            Destroy();
        }
    }

    // La nave explota (la escena de pilotar enseña la explosión)
    public static void Destroy()
    {
        if (IsDestroyed)
        {
            return;
        }

        IsDestroyed = true;
        health = 0f;
        Debug.LogWarning("¡La nave ha sido destruida!");
    }

    private static SystemState Find(string systemName)
    {
        foreach (SystemState system in Systems)
        {
            if (system.name == systemName)
            {
                return system;
            }
        }

        return null;
    }

    // Empieza la partida si no había empezado: carga las zonas (todas al 100%)
    // y pone en marcha FailureDirector y el tutorial. Se llama sola al usar ShipState.
    public static void EnsureLoaded()
    {
        if (config != null)
        {
            return;
        }

        config = Resources.Load<ShipSystems>(ShipSystems.ResourceName);
        if (config == null)
        {
            Debug.LogWarning("No existe Resources/ShipSystems. Guarda la escena de primera persona para crearlo.");
            config = ScriptableObject.CreateInstance<ShipSystems>();
        }

        foreach (ShipSystems.SystemInfo info in config.systems)
        {
            systems.Add(new SystemState
            {
                name = info.name,
                // Cada zona pierde estabilidad a un ritmo algo distinto
                lossPerSecond = info.lossPerSecond * Random.Range(0.7f, 1.3f)
            });
        }

        FailureDirector.Create();

        if (config.tutorialEnabled)
        {
            ShipTutorial.Create();
        }
        else
        {
            CanEnterShip = true;
            TutorialFinished = true;
        }
    }

    // Al darle a Play en el editor siempre se empieza de cero
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Reset();
    }
}
