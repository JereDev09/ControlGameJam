using UnityEditor;
using UnityEngine;

// Crea sistemas de partículas de una sola explosión (burst) para las herramientas del editor.
// Después se pueden retocar a mano en el Inspector como cualquier otro.
public static class ParticleFactory
{
    private const string MaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    // lifetime, speed y size son rangos (mínimo, máximo).
    // gravity < 0 hace que suban y > 0 que caigan. endSize: tamaño al final de su vida (0 = desaparecen encogiendo).
    public static ParticleSystem CreateBurst(string name, Transform parent, Vector3 position, int count,
        Vector2 lifetime, Vector2 speed, Vector2 size, Color colorA, Color colorB, float gravity, float radius, float endSize)
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;

        // Se desvanecen al final de su vida
        Gradient fadeOut = new Gradient();
        fadeOut.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = fadeOut;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, endSize));

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Debug.LogWarning($"No encontré el material de partículas de URP ({MaterialPath}). Asígnale uno a {name}.", particlesObject);
        }

        particlesObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        return particles;
    }

    // Igual que CreateBurst, pero emite sin parar (chispas o humo continuos).
    // Empieza sin emitir nada: el script que lo usa sube su emisión (rateOverTime) cuando hace falta.
    public static ParticleSystem CreateLoop(string name, Transform parent, Vector3 position,
        Vector2 lifetime, Vector2 speed, Vector2 size, Color colorA, Color colorB, float gravity, float radius, float endSize)
    {
        ParticleSystem particles = CreateBurst(name, parent, position, 0, lifetime, speed, size, colorA, colorB, gravity, radius, endSize);

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.SetBursts(new ParticleSystem.Burst[0]);
        return particles;
    }

    // Punto central y tamaño de un objeto según sus Renderers (sin contar partículas)
    public static Bounds GetBounds(GameObject target)
    {
        Bounds bounds = new Bounds(target.transform.position, Vector3.zero);
        bool hasBounds = false;

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
        {
            if (renderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (hasBounds)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            else
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
        }

        return bounds;
    }
}
