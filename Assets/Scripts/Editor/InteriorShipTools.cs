using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Nave > Añadir colisiones al interior de la nave
// Pone colisiones al modelo del interior (Assets/Models/InteriorShip) que haya en la escena abierta:
// - MeshCollider en el casco (suelos, paredes y techo), las barandillas y los objetos grandes.
// - Rampas invisibles encima de las escaleras: sus escalones miden hasta 1,25 m y el jugador no
//   podría subirlos; con la rampa sube andando. Van en el objeto hijo "Colisiones".
// Las terminales no llevan colisión: los minijuegos están encima y taparían el rayo para interactuar.
// Al final coloca al jugador de pie sobre el suelo. Se puede pulsar las veces que haga falta.
public static class InteriorShipTools
{
    private const string ModelPath = "Assets/Models/InteriorShip/InteriorShip.fbx";
    private const string CollidersName = "Colisiones";
    private const float RampThickness = 2f; // en unidades del modelo (como las rampas)

    // Piezas del modelo que llevan colisión (por nombre)
    private static readonly string[] SolidParts = { "Interior_Ship", "Border", "Crate", "Motor" };

    // Rampas de las escaleras, en coordenadas del modelo (las del FBX): del pie de la escalera a lo alto,
    // y su ancho. Si cambias el modelo, ajusta estos números.
    private struct Ramp
    {
        public string name;
        public Vector3 bottom;
        public Vector3 top;
        public float width;
    }

    private static readonly Ramp[] Ramps =
    {
        new Ramp { name = "Rampa escalera principal", bottom = new Vector3(-127.1f, 59.4f, -46.6f), top = new Vector3(-127.1f, 82.6f, -16.2f), width = 25.2f },
        new Ramp { name = "Rampa escalera lateral 1", bottom = new Vector3(-140.5f, 59.4f, -77.6f), top = new Vector3(-150f, 65.4f, -77.6f), width = 17.5f },
        new Ramp { name = "Rampa escalera lateral 2", bottom = new Vector3(-113.7f, 59.4f, -77.6f), top = new Vector3(-104.2f, 65.4f, -77.6f), width = 17.5f }
    };

    [MenuItem("Tools/Nave/Añadir colisiones al interior de la nave")]
    private static void AddInteriorColliders()
    {
        List<GameObject> ships = FindShipModels();
        if (ships.Count == 0)
        {
            EditorUtility.DisplayDialog("Interior de la nave", $"No hay ningún {ModelPath} en la escena abierta.", "Vale");
            return;
        }

        // Unidades del modelo -> unidades de Unity (el FBX está en centímetros)
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        float unit = importer.globalScale * (importer.useFileScale ? importer.fileScale : 1f);

        List<string> solids = new List<string>();
        foreach (GameObject ship in ships)
        {
            RemoveEmptyMeshColliders(ship);
            AddMeshColliders(ship, solids);
            CreateRamps(ship.transform, unit);
        }

        string playerResult = PlacePlayerOnFloor();
        EditorSceneManager.MarkSceneDirty(ships[0].scene);

        EditorUtility.DisplayDialog("Interior de la nave",
            $"Con colisión: {string.Join(", ", solids)}.\n" +
            $"Rampas en las escaleras: {Ramps.Length} (dentro de \"{CollidersName}\").\n\n{playerResult}\n\n" +
            "Guarda la escena para conservar los cambios.", "Vale");
    }

    // Las instancias del modelo que hay en la escena
    private static List<GameObject> FindShipModels()
    {
        HashSet<GameObject> ships = new HashSet<GameObject>();
        foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (filter.sharedMesh != null && AssetDatabase.GetAssetPath(filter.sharedMesh) == ModelPath)
            {
                GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(filter.gameObject);
                ships.Add(root != null ? root : filter.transform.root.gameObject);
            }
        }

        return new List<GameObject>(ships);
    }

    // Un MeshCollider sin malla no choca con nada (por ejemplo, uno puesto a mano en la raíz)
    private static void RemoveEmptyMeshColliders(GameObject ship)
    {
        foreach (MeshCollider meshCollider in ship.GetComponents<MeshCollider>())
        {
            if (meshCollider.sharedMesh == null)
            {
                Undo.DestroyObjectImmediate(meshCollider);
            }
        }
    }

    private static void AddMeshColliders(GameObject ship, List<string> solids)
    {
        foreach (MeshFilter filter in ship.GetComponentsInChildren<MeshFilter>(true))
        {
            if (System.Array.IndexOf(SolidParts, filter.name) < 0)
            {
                continue;
            }

            MeshCollider meshCollider = filter.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = Undo.AddComponent<MeshCollider>(filter.gameObject);
            }

            meshCollider.sharedMesh = filter.sharedMesh;
            solids.Add(filter.name);
        }
    }

    // Cajas finas e inclinadas: su cara de arriba va del pie de la escalera a lo alto
    private static void CreateRamps(Transform ship, float unit)
    {
        Transform parent = ship.Find(CollidersName);
        if (parent == null)
        {
            GameObject parentObject = new GameObject(CollidersName);
            Undo.RegisterCreatedObjectUndo(parentObject, "Colisiones del interior");
            parent = parentObject.transform;
            parent.SetParent(ship, false);
        }

        foreach (Ramp ramp in Ramps)
        {
            Transform rampTransform = parent.Find(ramp.name);
            if (rampTransform == null)
            {
                GameObject rampObject = new GameObject(ramp.name, typeof(BoxCollider));
                Undo.RegisterCreatedObjectUndo(rampObject, "Rampa");
                rampTransform = rampObject.transform;
                rampTransform.SetParent(parent, false);
            }

            Vector3 bottom = ModelToLocal(ramp.bottom, unit);
            Vector3 top = ModelToLocal(ramp.top, unit);
            Vector3 slope = top - bottom;
            Quaternion rotation = Quaternion.LookRotation(slope, Vector3.up);
            float thickness = RampThickness * unit;

            rampTransform.localRotation = rotation;
            rampTransform.localPosition = (bottom + top) / 2f - rotation * Vector3.up * (thickness / 2f);
            rampTransform.localScale = Vector3.one;
            rampTransform.GetComponent<BoxCollider>().size = new Vector3(ramp.width * unit, thickness, slope.magnitude);
        }
    }

    // Unity le da la vuelta al eje X de los FBX (pasa de mano derecha a mano izquierda)
    private static Vector3 ModelToLocal(Vector3 modelPoint, float unit)
    {
        return new Vector3(-modelPoint.x, modelPoint.y, modelPoint.z) * unit;
    }

    // Pone al jugador justo encima del suelo que tiene debajo (si empieza metido en él, lo atravesaría)
    private static string PlacePlayerOnFloor()
    {
        FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>();
        if (player == null)
        {
            return "No hay jugador (FirstPersonController) en la escena.";
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        Physics.SyncTransforms();
        RaycastHit[] hits = Physics.RaycastAll(player.transform.position + Vector3.up * 0.5f, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == controller)
            {
                continue;
            }

            // Distancia del centro del jugador a sus pies
            float scaleY = player.transform.lossyScale.y;
            float feetOffset = (controller.center.y - controller.height / 2f) * scaleY;
            Undo.RecordObject(player.transform, "Colocar jugador");
            Vector3 position = player.transform.position;
            position.y = hit.point.y - feetOffset + controller.skinWidth;
            player.transform.position = position;
            return $"Jugador colocado sobre el suelo ({hit.collider.name}).";
        }

        return "No encontré suelo debajo del jugador: colócalo a mano encima del suelo de la nave.";
    }
}
