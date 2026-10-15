using System.Collections.Generic;
using UnityEngine;

namespace NOMirrors
{
    /// <summary>
    /// Camera mode, shared (the default since 0.0.4): one render of the view behind the pilot serves every mirror. Each mirror render
    /// was a whole extra scene render, about 6 ms each whatever its size or view distance (measured, MiG-29 cockpit); three mirrors
    /// kept one rendering on most frames. The rear-view mirrors all look back from about the same point (the pilot's eye mirrored
    /// in each glass is within a metre of it), so a single camera at the eye, aimed at the middle of what the mirrors reflect, sees
    /// all of it. Each mirror then shows its own part: every corner of the glass gets the texture coordinates of the direction it
    /// reflects (eye to corner, reflected in the glass, widened by the mirror's field of view). Faraway things are exact; things
    /// close behind (tail, wings) are off by the eye-to-virtual-eye parallax, a fraction of a degree.
    /// </summary>
    internal static class SharedView
    {
        static Camera cam;
        static RenderTexture rt;
        static readonly List<Mirror> users = new List<Mirror>();
        static readonly List<Mirror> active = new List<Mirror>();
        static int tickFrame = -1;
        static float next;
        static readonly Plane[] planes = new Plane[6];

        // the camera's tangent-space window this render covered (x right, y up, at distance 1 along its forward)
        static float x0, x1, y0, y1;

        internal static RenderTexture Texture => rt;

        internal static void Register(Mirror m) { if (!users.Contains(m)) users.Add(m); }
        internal static void Unregister(Mirror m) { users.Remove(m); if (users.Count == 0) Release(); }

        static void Release()
        {
            if (cam != null) { Mirror.ForgetCamera(cam); Object.Destroy(cam.gameObject); cam = null; }
            if (rt != null) { rt.Release(); Object.Destroy(rt); rt = null; }
        }

        static void EnsureCamera()
        {
            if (cam != null) return;
            var go = new GameObject("NOMirror_shared_camera");
            Object.DontDestroyOnLoad(go);
            cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.allowHDR = false; cam.allowMSAA = false;
            Mirror.RememberCamera(cam);
            GlassHider.Hook();
            if (DebugDump.StockCameras) return;
            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.requiresColorOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
            data.requiresDepthOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
        }

        /// <summary>Called by every shared mirror each frame; does the work once per frame.</summary>
        internal static void Tick(Camera main)
        {
            if (tickFrame == Time.frameCount) return;
            tickFrame = Time.frameCount;
            if (!Plugin.Enabled.Value || main == null || users.Count == 0) return;

            // mirrors in range and in the player's view (the near plane moved to the eye: the cockpit is drawn by its own camera)
            GeometryUtility.CalculateFrustumPlanes(main, planes);
            planes[4] = new Plane(main.transform.forward, main.transform.position);
            var eye = main.transform.position;
            active.Clear();
            foreach (var m in users)
            {
                if (m == null || m.Glass == null || !m.isActiveAndEnabled) continue;
                if ((eye - m.Glass.bounds.center).sqrMagnitude > m.Settings.Range * m.Settings.Range) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, m.Glass.bounds)) continue;
                active.Add(m);
            }
            if (active.Count == 0 || Time.unscaledTime < next) return;
            float rate = 0f;
            foreach (var m in active) rate = Mathf.Max(rate, m.Settings.UpdateRate);
            next = rate > 0f ? Time.unscaledTime + 1f / rate : 0f;

            // aim at the middle of what the mirrors reflect, then fit an off-axis window round every corner's reflected direction
            // from the mirrors' virtual eye (the pilot's eye mirrored in each glass, averaged): a mirror shows the cockpit from in front
            // of the glass looking back, and from the pilot's own eye the seat's headrest filled every mirror
            Vector3 aim = Vector3.zero, virt = Vector3.zero;
            foreach (var m in active) { aim += m.CentreReflection(eye); virt += m.VirtualEye(eye); }
            if (aim.sqrMagnitude < 1e-6f) return;
            virt /= active.Count;
            EnsureCamera();
            var fwd = aim.normalized;
            var up = Vector3.ProjectOnPlane(main.transform.up, fwd).normalized;
            cam.transform.SetPositionAndRotation(virt, Quaternion.LookRotation(fwd, up));
            var right = cam.transform.right;
            x0 = y0 = float.MaxValue; x1 = y1 = float.MinValue;
            foreach (var m in active)
                foreach (var d in m.CornerReflections(eye))
                {
                    float z = Vector3.Dot(d, fwd);
                    if (z < 0.15f) continue;                       // past ~80 deg off the aim: leave it out of the window
                    float x = Vector3.Dot(d, right) / z, y = Vector3.Dot(d, up) / z;
                    x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                }
            if (x1 <= x0 || y1 <= y0) return;
            float px = (x1 - x0) * 0.02f, py = (y1 - y0) * 0.02f;
            x0 -= px; x1 += px; y0 -= py; y1 += py;

            // texture: the mirrors' resolution as its height, the window's shape for its width
            int h = 0; foreach (var m in active) h = Mathf.Max(h, m.Settings.Resolution);
            int w = Mathf.Clamp(Mathf.RoundToInt(h * (x1 - x0) / (y1 - y0)), 16, 4096);
            if (rt == null || Mathf.Abs(rt.width - w) > w / 8 || rt.height != h)
            {
                if (rt != null) { rt.Release(); Object.Destroy(rt); }
                rt = new RenderTexture(w, h, 24) { name = "NOMirror_shared", antiAliasing = 1 };
                foreach (var m in users) if (m != null) m.SetSharedTexture(rt);
            }
            cam.targetTexture = rt;
            // near plane just short of the nearest glass: clips what lies between the virtual eye and the mirrors (panel, nose)
            float near = float.MaxValue, far = 0f;
            foreach (var m in active) near = Mathf.Min(near, Vector3.Dot(m.Glass.bounds.center - virt, fwd));
            near = Mathf.Max(near * 0.9f, 0.05f);
            foreach (var m in active) far = Mathf.Max(far, m.Settings.Far);
            cam.nearClipPlane = near; cam.farClipPlane = far;
            cam.projectionMatrix = Matrix4x4.Frustum(x0 * near, x1 * near, y0 * near, y1 * near, near, far);
            bool cockpit = false;
            foreach (var m in active) cockpit |= m.Settings.ReflectCockpit ?? Plugin.ShowCockpit.Value;
            cam.cullingMask = (main.cullingMask | (cockpit ? Mirror.CockpitLayers(main) : 0)) & ~Plugin.ExcludedLayers;

            float lodBias = QualitySettings.lodBias;
            QualitySettings.lodBias = lodBias * Plugin.DetailScale.Value;
            try { cam.Render(); }
            finally { QualitySettings.lodBias = lodBias; }

            foreach (var m in active) m.MapShared(eye, fwd, right, up, x0, x1, y0, y1);
        }
    }
}
