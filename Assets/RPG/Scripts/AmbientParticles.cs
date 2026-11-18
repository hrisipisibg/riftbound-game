using UnityEngine;

namespace Riftbound
{
    public class AmbientParticles : MonoBehaviour
    {
        PlayerController player;
        ParticleSystem ps;
        ParticleSystem.MainModule main;
        string lastZone = "";

        void Start()
        {
            var go = new GameObject("ZoneAmbientParticles");
            go.transform.SetParent(transform, false);
            ps = go.AddComponent<ParticleSystem>();
            main = ps.main;
            main.loop = true;
            main.maxParticles = 45;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;

            var emission = ps.emission;
            emission.rateOverTime = 9f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18f, 6f, 18f);

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Particles/Standard Unlit")
                      ?? Shader.Find("Sprites/Default");
            var pr = go.GetComponent<ParticleSystemRenderer>();
            if (shader)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", Color.white);
                pr.sharedMaterial = mat;
            }
        }

        void LateUpdate()
        {
            if (!ps) return;
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player || FrontendMenu.IsOpen)
            {
                if (ps.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                return;
            }
            if (!ps.isPlaying) ps.Play();

            ps.transform.position = player.transform.position + Vector3.up * 3.2f;

            string zone = ZoneManager.GetZoneName(player.transform.position);
            if (zone != lastZone)
            {
                lastZone = zone;
                ApplyZoneTheme(zone);
            }
        }

        void ApplyZoneTheme(string zone)
        {
            string z = (zone ?? "").ToLowerInvariant();
            if (z.Contains("frost") || z.Contains("snow") || z.Contains("peak") || z.Contains("glacier"))
            {
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.95f, 1f, 0.7f), new Color(0.65f, 0.85f, 1f, 0.45f));
                main.gravityModifier = 0.08f;
            }
            else if (z.Contains("ember") || z.Contains("ash") || z.Contains("cinder") || z.Contains("volcan") || z.Contains("reaver"))
            {
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f, 0.8f), new Color(1f, 0.25f, 0.08f, 0.5f));
                main.gravityModifier = -0.08f;
            }
            else if (z.Contains("green") || z.Contains("hollow") || z.Contains("wood") || z.Contains("grove") || z.Contains("meadow"))
            {
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 1f, 0.45f, 0.7f), new Color(1f, 0.92f, 0.45f, 0.45f));
                main.gravityModifier = -0.02f;
            }
            else
            {
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.7f, 0.45f), new Color(0.85f, 0.9f, 1f, 0.3f));
                main.gravityModifier = -0.01f;
            }
        }
    }
}
