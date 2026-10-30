using UnityEditor;
using UnityEngine;

// Ventana para probar las averías mientras juegas: Tools > Minijuegos > Depurar estabilidad.
// Deja cambiar la estabilidad de cada zona, averiarla o repararla, saltar el tutorial
// y ver la vida y el progreso del viaje.
public class StabilityDebugWindow : EditorWindow
{
    private Vector2 scroll;

    [MenuItem("Tools/Minijuegos/Depurar estabilidad")]
    private static void Open()
    {
        GetWindow<StabilityDebugWindow>("Estabilidad");
    }

    // Se repinta unas 10 veces por segundo para ver los valores en directo
    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Dale a Play para cambiar la estabilidad de las zonas.", MessageType.Info);
            return;
        }

        if (!ShipState.IsLoaded)
        {
            EditorGUILayout.HelpBox("La partida todavía no ha empezado (no hay zonas cargadas).", MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawShip();
        EditorGUILayout.Space();
        DrawZones();
        EditorGUILayout.EndScrollView();
    }

    private static void DrawShip()
    {
        EditorGUILayout.LabelField("Nave", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Vida", $"{ShipState.Health:0} / {ShipState.MaxHealth:0}{(ShipState.IsDestroyed ? "  (DESTRUIDA)" : "")}");
        EditorGUILayout.LabelField("Progreso del viaje", $"{ShipState.Progress * 100f:0}%");
        EditorGUILayout.LabelField("Tutorial", ShipState.TutorialFinished ? "Terminado" : "En curso");

        if (!ShipState.TutorialFinished && GUILayout.Button("Saltar tutorial"))
        {
            ShipTutorial.Skip();
        }
    }

    private static void DrawZones()
    {
        EditorGUILayout.LabelField("Zonas", EditorStyles.boldLabel);

        foreach (ShipState.SystemState system in ShipState.Systems)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            float stability = EditorGUILayout.Slider(system.name, system.stability, 0f, 100f);
            if (EditorGUI.EndChangeCheck())
            {
                ShipState.SetStability(system.name, stability);
            }

            EditorGUI.BeginChangeCheck();
            bool failing = GUILayout.Toggle(system.isFailing, "Averiada", GUILayout.Width(75f));
            if (EditorGUI.EndChangeCheck())
            {
                if (failing)
                {
                    ShipState.Break(system.name);
                }
                else
                {
                    system.isFailing = false;
                }
            }

            if (GUILayout.Button("Reparar", GUILayout.Width(65f)))
            {
                ShipState.SetStability(system.name, 100f);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Todas al 20%"))
        {
            foreach (ShipState.SystemState system in ShipState.Systems)
            {
                ShipState.SetStability(system.name, 20f);
            }
        }

        if (GUILayout.Button("Reparar todas"))
        {
            foreach (ShipState.SystemState system in ShipState.Systems)
            {
                ShipState.SetStability(system.name, 100f);
            }
        }

        EditorGUILayout.EndHorizontal();
    }
}
