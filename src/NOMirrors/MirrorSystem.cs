using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace NOMirrors
{
    /// <summary>
    /// Public API for other plugins, and the scanner that finds named mirrors on the aircraft the player flies.
    /// No-code use: name a renderer's object <c>NOMirror</c> (or <c>NOMirror_&lt;anything&gt;</c>, with optional setting tokens, see
    /// <see cref="MirrorSettings.Parse"/>) and it reflects whenever its aircraft is the player's.
    /// </summary>
    public static class MirrorSystem
    {
        /// <summary>Bumped when the public API changes in a way that matters to callers. 1: NO Mirrors 0.0.3.</summary>
        public const int ApiVersion = 1;

        /// <summary>The NO Mirrors version, e.g. "0.0.3".</summary>
        public static string Version => Plugin.Version;

        /// <summary>Every live mirror (read only).</summary>
        public static IReadOnlyList<Mirror> Mirrors => Mirror.All;

        /// <summary>Raised when a mirror has set its glass up (its first frame). Use it to adjust mirrors made by name.</summary>
        public static event System.Action<Mirror> MirrorAdded;
        /// <summary>Raised when a mirror is removed (detached, or its object destroyed).</summary>
        public static event System.Action<Mirror> MirrorRemoved;

        internal static void RaiseAdded(Mirror m) { try { MirrorAdded?.Invoke(m); } catch (System.Exception e) { Plugin.Log.LogError(e); } }
        internal static void RaiseRemoved(Mirror m) { try { MirrorRemoved?.Invoke(m); } catch (System.Exception e) { Plugin.Log.LogError(e); } }

        /// <summary>Makes <paramref name="glass"/> a mirror, or updates an existing mirror's settings (applied straight away).
        /// Without <paramref name="settings"/>, they are read from the object's name over the player's defaults. Returns the component.</summary>
        public static Mirror Attach(Renderer glass, MirrorSettings settings = null)
        {
            if (glass == null) throw new System.ArgumentNullException(nameof(glass));
            var m = glass.GetComponent<Mirror>() ?? glass.gameObject.AddComponent<Mirror>();
            m.SettingsFromName = settings == null;
            m.Settings = settings ?? MirrorSettings.Parse(glass.name, Plugin.Defaults);
            m.Refresh();
            return m;
        }

        /// <summary>Stops <paramref name="glass"/> reflecting and gives it back its own material.</summary>
        public static void Detach(Renderer glass)
        {
            var m = glass != null ? glass.GetComponent<Mirror>() : null;
            if (m != null) Object.Destroy(m);
        }

        /// <summary>Attaches every renderer under <paramref name="root"/> (inactive ones too) whose object name starts with the
        /// configured prefix (<c>NOMirror</c>) and is not a mirror yet. Returns how many were attached.</summary>
        public static int AttachAll(Transform root)
        {
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith(Plugin.Prefix.Value) && r.GetComponent<Mirror>() == null) { Attach(r); n++; }
            return n;
        }

        /// <summary>Re-reads the settings of mirrors attached by name (after the player changed the defaults) and re-applies all mirrors.</summary>
        public static void RefreshAll()
        {
            foreach (var m in Mirror.All.ToArray())
            {
                if (m.SettingsFromName) m.Settings = MirrorSettings.Parse(m.name, Plugin.Defaults);
                m.Refresh();
            }
        }

        static readonly string[] Shaders = { "Universal Render Pipeline/Unlit", "Unlit/Texture", "Sprites/Default" };

        internal static Material MakeMaterial(RenderTexture rt, MirrorSettings s)
        {
            Shader sh = null;
            foreach (var name in Shaders) if ((sh = Shader.Find(name)) != null) break;
            var m = new Material(sh) { name = "NOMirror" };
            foreach (var prop in TexProps)
                if (m.HasProperty(prop)) m.SetTexture(prop, rt);
            var tint = new Color(s.Brightness, s.Brightness, s.Brightness, 1f);
            foreach (var prop in new[] { "_BaseColor", "_Color" })
                if (m.HasProperty(prop)) m.SetColor(prop, tint);
            return m;
        }

        static readonly string[] TexProps = { "_BaseMap", "_MainTex" };
    }

    /// <summary>
    /// See-through cockpit renderers of the player's aircraft (canopy glass) are hidden while a mirror camera draws. The game's glass shaders sample the
    /// screen image, which inside a mirror camera is some other render: the mirrors flickered with rectangles of the wrong view
    /// (user video, 0.0.2). The glass still shows normally on screen.
    /// </summary>
    internal static class GlassHider
    {
        static readonly List<Renderer> glass = new List<Renderer>();
        static readonly List<Renderer> hidden = new List<Renderer>();
        static float nextScan;
        static bool hooked;

        internal static void Hook()
        {
            if (hooked) return;
            hooked = true;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += (ctx, cam) => { if (Mirror.IsMirrorCamera(cam)) Hide(); };
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += (ctx, cam) => Restore();
        }

        static void Hide()
        {
            Restore();
            var main = Plugin.ViewCamera();
            if (main == null) return;
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 2f;
                glass.Clear();
                // the player's aircraft (its parts become separate roots in flight): its see-through, non-particle renderers
                var hud = SceneSingleton<CombatHUD>.i;
                var ac = hud != null ? hud.aircraft : null;
                if (ac != null)
                {
                    var roots = new HashSet<Transform> { ac.transform };
                    foreach (var part in Object.FindObjectsOfType<UnitPart>()) if (part.parentUnit == ac) roots.Add(part.transform);
                    var seen = new HashSet<Renderer>();
                    foreach (var root in roots)
                        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        {
                            if (!seen.Add(r) || !(r is MeshRenderer || r is SkinnedMeshRenderer) || r.GetComponent<Mirror>() != null) continue;
                            foreach (var m in r.sharedMaterials)
                                if (m != null && m.renderQueue > 2500) { glass.Add(r); break; }
                        }
                }
            }
            foreach (var r in glass)
                if (r != null && !r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
            // and every mirror's own glass: with the cockpit layer in view, a mirror camera drew its own glass right at its near
            // plane, the previous (flipped) picture in patches (the flicker in the user's videos, "Reflect the cockpit" on)
            foreach (var m in Mirror.All)
            {
                var r = m.Glass;
                if (r != null && !r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
            }
        }

        static void Restore()
        {
            foreach (var r in hidden) if (r != null) r.forceRenderingOff = false;
            hidden.Clear();
        }
    }

    /// <summary>Every second: if the player's aircraft changed, attach its named mirrors.</summary>
    public class MirrorScanner : MonoBehaviour
    {
        Aircraft last;
        float next;

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            try
            {
                var hud = SceneSingleton<CombatHUD>.i;
                var ac = hud != null ? hud.aircraft : null;
                if (ac == null || ac == last) return;
                last = ac;
                // aircraft parts become separate physics roots in flight: look through every part of this aircraft
                int n = 0;
                var seen = new HashSet<Transform>();
                foreach (var part in FindObjectsOfType<UnitPart>())
                    if (part.parentUnit == ac && seen.Add(part.transform)) n += MirrorSystem.AttachAll(part.transform);
                n += MirrorSystem.AttachAll(ac.transform);
                if (n > 0) Plugin.Log.LogInfo($"{ac.name}: {n} mirror(s)");
            }
            catch (System.Exception e) { Plugin.Log.LogError(e); next = Time.unscaledTime + 10f; }
        }
    }
}
