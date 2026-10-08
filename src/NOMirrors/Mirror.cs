using UnityEngine;
using UnityEngine.Rendering;

namespace NOMirrors
{
    /// <summary>Which way mirrors get their reflection.</summary>
    public enum MirrorMode
    {
        /// <summary>One shared reflection probe at the pilot's eye; the glass computes its reflection per pixel (cheapest, soft).</summary>
        Probe,
        /// <summary>A planar reflection camera per mirror rendering into a texture (default: sharp and exact, a render per mirror).</summary>
        Cameras,
    }

    /// <summary>
    /// Makes a renderer reflect. The glass is a mesh whose local Z axis is the mirror's normal (either way round), local X runs
    /// across it and local Y up it; its UVs should cover 0..1 across the reflecting face (camera mode uses them).
    /// </summary>
    [DisallowMultipleComponent]
    public class Mirror : MonoBehaviour
    {
        /// <summary>This mirror's settings. After changing them on a live mirror, call <see cref="Refresh"/>.</summary>
        public MirrorSettings Settings;

        /// <summary>The renderer whose glass reflects.</summary>
        public Renderer Glass => glass;
        /// <summary>Camera mode: the texture the glass shows (null in probe mode or before the mirror has started).</summary>
        public RenderTexture Texture => rt;
        /// <summary>How this mirror currently reflects (probe mode falls back to cameras without the URP Lit shader).</summary>
        public MirrorMode Mode => mode;
        /// <summary>Time.unscaledTime of this mirror's last render (camera mode), or -1 if it has not rendered yet.</summary>
        public float LastRenderTime { get; private set; } = -1f;
        /// <summary>True once the mirror has set its glass up (from its first frame).</summary>
        public bool Ready => started;

        /// <summary>Re-applies <see cref="Settings"/> (resolution, field of view and the rest) and the player's current mode.</summary>
        public void Refresh() { if (started) Apply(); }
        /// <summary>Camera mode: render at the next chance instead of waiting for the update rate.</summary>
        public void RenderNow() => nextRender = 0f;

        internal bool SettingsFromName;   // attached by name: picks up the player's config changes
        bool started;

        internal static int ActiveProbeMirrors;

        Renderer glass;
        Material original, mat;
        MirrorMode mode;
        bool counted;

        // camera mode
        Camera cam;
        RenderTexture rt;
        Bounds local;          // the glass mesh's bounds, its own space: the reflecting rectangle
        Vector2 size;          // glass width and height, metres
        float nextRender;
        int flip = -1;         // texture orientation on the glass (bit 0: x, bit 1: y), set from the camera's frame

        // probe mode
        MeshFilter filter;
        Mesh originalMesh, bent;
        float bentFor = -1f;   // the field of view the bent normals were made for

        void Start()
        {
            glass = GetComponent<Renderer>();
            if (glass == null) { Plugin.Log.LogWarning($"{name}: a mirror needs a renderer"); Destroy(this); return; }
            if (Settings == null) Settings = Plugin.Defaults.Clone();
            original = glass.sharedMaterial;

            var mf = filter = GetComponent<MeshFilter>();
            if (mf != null) originalMesh = mf.sharedMesh;
            local = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, new Vector3(0.1f, 0.05f, 0f));
            var ls = transform.lossyScale;
            size = new Vector2(Mathf.Abs(local.size.x * ls.x), Mathf.Abs(local.size.y * ls.y));
            if (size.x < 1e-4f || size.y < 1e-4f) { size = new Vector2(0.1f, 0.05f); local = new Bounds(local.center, new Vector3(0.1f, 0.05f, 0f)); }
            if (mf != null && originalMesh != null && !originalMesh.isReadable && originalMesh.vertexCount != 4)
                Plugin.Log.LogInfo($"{name}: the glass mesh is not readable, so it is drawn as a rectangle over its bounds " +
                                   "(enable Read/Write on the mesh to keep its own shape)");
            started = true;
            Apply();
            MirrorSystem.RaiseAdded(this);
        }

        /// <summary>Sets the glass up for the current mode (called again when the player changes the mode).</summary>
        internal void Apply()
        {
            Teardown();
            mode = Plugin.Mode.Value;
            if (mode == MirrorMode.Probe && ProbeReflections.Lit == null)
            {
                Plugin.Log.LogWarning("URP Lit shader not available: mirrors use cameras");
                mode = MirrorMode.Cameras;
            }
            if (mode == MirrorMode.Probe)
            {
                ProbeReflections.Ensure();
                mat = ProbeReflections.MakeMaterial(Settings);
                glass.reflectionProbeUsage = ReflectionProbeUsage.Simple;
                ActiveProbeMirrors++; counted = true;
                Bend();
            }
            else
            {
                int h = Settings.Resolution, w = Mathf.Clamp(Mathf.RoundToInt(h * size.x / size.y), 16, 4096);
                rt = new RenderTexture(w, h, 24) { name = "NOMirror_" + name, antiAliasing = 1 };
                flip = -1;
                var go = new GameObject("NOMirror_camera");
                go.transform.SetParent(transform, false);
                cam = go.AddComponent<Camera>();
                cam.targetTexture = rt;
                cam.enabled = false;
                mirrorCameras.Add(cam);
                GlassHider.Hook();
                // a plain render: no screen-colour / depth copies (the game's glass shaders read the global ones, and a mirror
                // camera's copy then showed through the canopy as a ghost cockpit), no post-processing, no AA
                if (DebugDump.StockCameras) goto made;   // debug A/B
                var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                data.requiresColorOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
                data.requiresDepthOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
                data.renderPostProcessing = false;
                data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                made:
                cam.nearClipPlane = Settings.Near;
                cam.farClipPlane = Settings.Far;
                mat = MirrorSystem.MakeMaterial(rt, Settings);
            }
            glass.material = mat;
        }

        void LateUpdate()
        {
            if (glass == null) return;
            if (mode != Plugin.Mode.Value && !(Plugin.Mode.Value == MirrorMode.Probe && ProbeReflections.Lit == null)) Apply();
            if (cam == null)           // probe mode: only re-bend the glass if the field of view changed
            {
                if (counted && !Mathf.Approximately(bentFor, Settings.FovScale * Plugin.FieldOfView.Value)) Bend();
                return;
            }

            var main = Plugin.ViewCamera();
            // only from the cockpit: in outside views the mirror cameras would keep each other "visible"
            bool render = Plugin.Enabled.Value && main != null && glass.isVisible && Time.unscaledTime >= nextRender &&
                          (main.transform.position - glass.bounds.center).sqrMagnitude < Settings.Range * Settings.Range;
            // One mirror per frame, rendered here by hand. Left enabled together, the mirror cameras sometimes got each other's
            // pictures (the left mirror showing the right one's view, or a mix: user video, 0.0.2).
            if (!render || lastRenderFrame == Time.frameCount) return;
            if (!Place(main.transform.position)) return;
            lastRenderFrame = Time.frameCount;
            if (Settings.UpdateRate > 0f) nextRender = Time.unscaledTime + 1f / Settings.UpdateRate;
            bool cockpit = Settings.ReflectCockpit ?? Plugin.ShowCockpit.Value;
            cam.cullingMask = (main.cullingMask | (cockpit ? CockpitLayers(main) : 0)) & ~Plugin.ExcludedLayers;
            cam.Render();
            LastRenderTime = Time.unscaledTime;
        }

        static int lastRenderFrame = -1;

        internal static readonly System.Collections.Generic.List<Mirror> All = new System.Collections.Generic.List<Mirror>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>
        /// Planar reflection: the camera sits at the eye mirrored through the glass's plane and looks back through the glass, with an
        /// off-axis frustum whose near plane is exactly the glass rectangle. Each texel then shows what the eye sees reflected at that
        /// point of the glass, so the texture maps straight onto the glass's UVs (no aiming, no field-of-view guess), and moves with
        /// the head. The fov setting pulls the virtual eye toward the glass: a wider, convex-mirror view along the same line.
        /// Returns false when the eye is behind the glass.
        /// </summary>
        bool Place(Vector3 eye)
        {
            Vector3 c = transform.TransformPoint(local.center);
            Vector3 ax = transform.TransformVector(new Vector3(local.extents.x, 0f, 0f));   // half width, world
            Vector3 ay = transform.TransformVector(new Vector3(0f, local.extents.y, 0f));   // half height, world
            Vector3 n = Vector3.Cross(ax, ay).normalized;
            float de = Vector3.Dot(eye - c, n);
            if (de < 0f) { n = -n; de = -de; }                              // the face the pilot sees
            if (de < 1e-3f) return false;
            Vector3 virt = eye - 2f * de * n;                                // the eye mirrored through the glass
            virt = c + (virt - c) / Mathf.Max(Settings.FovScale * Plugin.FieldOfView.Value, 0.1f);

            Vector3 up = Vector3.ProjectOnPlane(ay, n).normalized;
            cam.transform.SetPositionAndRotation(virt, Quaternion.LookRotation(n, up));
            Vector3 right = cam.transform.right, cu = cam.transform.up;
            Vector3 rel = c - virt;
            float d = Vector3.Dot(rel, n);                                   // virtual eye to the glass plane
            float cx = Vector3.Dot(rel, right), cy = Vector3.Dot(rel, cu);
            float hw = Mathf.Abs(Vector3.Dot(ax, right)) + Mathf.Abs(Vector3.Dot(ay, right));
            float hh = Mathf.Abs(Vector3.Dot(ax, cu)) + Mathf.Abs(Vector3.Dot(ay, cu));
            float near = Mathf.Max(d + 0.002f, 0.001f), far = Mathf.Max(Settings.Far, near + 1f);   // just past the glass
            cam.nearClipPlane = near;                                        // clips everything behind the glass
            cam.farClipPlane = far;
            float k = near / d;                                              // the glass rectangle, scaled out to the near plane
            cam.projectionMatrix = Matrix4x4.Frustum((cx - hw) * k, (cx + hw) * k, (cy - hh) * k, (cy + hh) * k, near, far);

            // the texture's x runs along the camera's right; the glass's u along its own x: flip where they disagree
            int f = (Vector3.Dot(right, ax) < 0f ? 1 : 0) | (Vector3.Dot(cu, ay) < 0f ? 2 : 0);
            if (!Settings.FlipX) f ^= 1;
            if (f != flip) { flip = f; FlipUVs((f & 1) != 0, (f & 2) != 0); }
            return true;
        }

        Mesh flipped;

        /// <summary>
        /// Flips the glass's UVs (a copy of its mesh, or a quad over its bounds when the mesh is not readable, as in asset bundles).
        /// Done on the mesh, not with texture scale/offset: the shader the game has for the glass (Sprites/Default) ignores those,
        /// and every mirror showed its picture reversed (user video, 0.0.2).
        /// </summary>
        void FlipUVs(bool fx, bool fy)
        {
            if (filter == null || originalMesh == null) return;
            if (flipped != null) { Destroy(flipped); flipped = null; }
            if (!fx && !fy) { filter.sharedMesh = originalMesh; return; }
            Vector2 F(Vector2 uv) => new Vector2(fx ? 1f - uv.x : uv.x, fy ? 1f - uv.y : uv.y);
            if (originalMesh.isReadable)
            {
                flipped = Instantiate(originalMesh);
                var uvs = originalMesh.uv;
                for (int i = 0; i < uvs.Length; i++) uvs[i] = F(uvs[i]);
                flipped.uv = uvs;
            }
            else
            {
                Vector3 c = local.center, e = local.extents;
                flipped = new Mesh
                {
                    vertices = new[] { c + new Vector3(-e.x, -e.y, 0f), c + new Vector3(-e.x, e.y, 0f), c + new Vector3(e.x, e.y, 0f), c + new Vector3(e.x, -e.y, 0f) },
                    uv = new[] { F(new Vector2(0, 0)), F(new Vector2(0, 1)), F(new Vector2(1, 1)), F(new Vector2(1, 0)) },
                    triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 },   // both faces
                };
                flipped.RecalculateNormals();
            }
            flipped.name = originalMesh.name + "_mirrored";
            filter.sharedMesh = flipped;
        }

        static readonly System.Collections.Generic.HashSet<Camera> mirrorCameras = new System.Collections.Generic.HashSet<Camera>();
        internal static bool IsMirrorCamera(Camera c) => mirrorCameras.Contains(c);

        static int cockpitLayers;
        static float nextLayerScan;

        /// <summary>Layers drawn by the other screen cameras at the eye (the game draws the cockpit interior with its own camera).</summary>
        internal static int CockpitLayers(Camera main)
        {
            if (Time.unscaledTime < nextLayerScan) return cockpitLayers;
            nextLayerScan = Time.unscaledTime + 1f;
            int mask = 0;
            foreach (var c in Camera.allCameras)
                if (c != main && c.targetTexture == null && c.GetComponentInParent<Mirror>() == null &&
                    (c.transform.position - main.transform.position).sqrMagnitude < 0.25f)
                    mask |= c.cullingMask;
            return cockpitLayers = mask;
        }

        internal Camera ReflectionCamera => cam;

        /// <summary>
        /// Probe mode: a flat glass reflects nearly one direction, a few texels of the probe's cube. Wider fields of view bend the
        /// glass's normals outward from its centre, as on a convex mirror, so the reflection fans out across the glass: at the
        /// edges the normal tilts by (k - 1) x offset / (2 x eye distance), which widens the reflected field k times.
        /// </summary>
        void Bend()
        {
            float k = Settings.FovScale * Plugin.FieldOfView.Value;
            bentFor = k;
            if (filter == null || originalMesh == null) return;
            if (k <= 1.001f) { filter.sharedMesh = originalMesh; return; }
            var main = Plugin.ViewCamera();
            float dist = main != null ? Mathf.Clamp(Vector3.Distance(main.transform.position, glass.bounds.center), 0.2f, 3f) : 0.6f;
            float scale = transform.lossyScale.x;
            Vector3[] v, n;
            if (originalMesh.isReadable && originalMesh.normals.Length == originalMesh.vertexCount)
            {
                if (bent == null) bent = Instantiate(originalMesh);
                v = originalMesh.vertices; n = originalMesh.normals;
            }
            else
            {
                // meshes in asset bundles are usually not readable: a quad over the glass's bounds instead (UVs 0..1, as the README asks)
                float side = main != null && transform.InverseTransformPoint(main.transform.position).z < local.center.z ? -1f : 1f;
                Vector3 c = local.center, e = local.extents;
                v = new[] { c + new Vector3(-e.x, -e.y, 0f), c + new Vector3(-e.x, e.y, 0f), c + new Vector3(e.x, e.y, 0f), c + new Vector3(e.x, -e.y, 0f) };
                n = new[] { Vector3.forward * side, Vector3.forward * side, Vector3.forward * side, Vector3.forward * side };
                if (bent == null)
                {
                    bent = new Mesh { vertices = v, uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) } };
                    bent.triangles = side < 0f ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
                }
            }
            bent.name = originalMesh.name + "_convex";
            var outN = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var o = v[i] - local.center; o.z = 0f;
                outN[i] = (n[i].normalized + o * (scale * (k - 1f) / (2f * dist))).normalized;
            }
            bent.normals = outN;
            filter.sharedMesh = bent;
        }

        void Teardown()
        {
            if (filter != null && originalMesh != null) filter.sharedMesh = originalMesh;
            bentFor = -1f;
            if (cam != null) { mirrorCameras.Remove(cam); Destroy(cam.gameObject); cam = null; }
            if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
            if (mat != null) { Destroy(mat); mat = null; }
            if (counted) { ActiveProbeMirrors--; counted = false; }
        }

        void OnDestroy()
        {
            if (started) MirrorSystem.RaiseRemoved(this);
            Teardown();
            if (bent != null) Destroy(bent);
            if (flipped != null) Destroy(flipped);
            if (glass != null && original != null) glass.sharedMaterial = original;
        }
    }
}
