using UnityEngine;
using UnityEngine.Rendering;

namespace NOMirrors
{
    /// <summary>
    /// "Probe" mode: one realtime reflection probe at the pilot's eye, shared by every mirror. The glass gets a fully metallic,
    /// fully smooth URP Lit material, so the GPU computes each pixel's reflection live from the real view direction (it moves
    /// with the head, no flipping or per-mirror maths). The probe refreshes at a capped rate (Quality > Probe updates per second),
    /// the same cost however many mirrors there are.
    /// </summary>
    public class ProbeReflections : MonoBehaviour
    {
        static ProbeReflections instance;
        ReflectionProbe probe;
        float nextRender;
        static Shader lit;

        /// <summary>The URP Lit shader, or null if the game build does not include it (mirrors then fall back to cameras).</summary>
        public static Shader Lit => lit != null ? lit : (lit = Shader.Find("Universal Render Pipeline/Lit"));

        /// <summary>The probe's cube (debug dumps), or null.</summary>
        internal static RenderTexture Texture => instance != null && instance.probe != null ? instance.probe.realtimeTexture : null;

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("NOMirrorProbe");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ProbeReflections>();
        }

        void Awake()
        {
            probe = gameObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            // Not time-sliced: under the game's URP a sliced render never reported finished, so the probe kept its first capture
            // (the spawn hangar; user report, 0.0.2). Whole refreshes at a capped rate instead.
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.importance = 1000;              // wins over the scene's probes inside its box
            probe.size = new Vector3(4f, 3f, 6f); // the cockpit: only renderers in here use it
            probe.boxProjection = false;          // the reflected world is far away: plain direction lookup is right
            probe.hdr = true;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
        }

        void LateUpdate()
        {
            var main = Plugin.ViewCamera();
            if (main == null || !Plugin.Enabled.Value || Mirror.ActiveProbeMirrors == 0) return;
            transform.position = main.transform.position;
            probe.resolution = Plugin.ProbeResolution.Value;
            probe.farClipPlane = Plugin.Defaults.Far;
            probe.nearClipPlane = Mathf.Max(main.nearClipPlane, 0.3f);   // as the game: its main camera starts 1 m out, past the airframe round the eye
            probe.cullingMask = main.cullingMask & ~Plugin.ExcludedLayers;
            if (Time.unscaledTime < nextRender) return;
            float rate = Plugin.ProbeRate.Value;
            nextRender = rate > 0f ? Time.unscaledTime + 1f / rate : 0f;
            probe.RenderProbe();
        }

        internal static Material MakeMaterial(MirrorSettings s)
        {
            var m = new Material(Lit) { name = "NOMirror_probe" };
            var tint = new Color(s.Brightness, s.Brightness, s.Brightness, 1f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 1f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 1f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1f);
            m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return m;
        }
    }
}
