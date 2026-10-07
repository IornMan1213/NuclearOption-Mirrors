using UnityEngine;

namespace NOMirrors
{
    /// <summary>
    /// Makes a renderer reflect. The glass is a mesh whose local Z axis is the mirror's normal (either way round), local X runs
    /// across it and local Y up it; its UVs should cover 0..1 across the reflecting face. A camera at the glass looks along the
    /// pilot's view reflected in the mirror plane, with the field of view the glass covers from the pilot's eye, renders into a
    /// texture and the glass shows it.
    /// </summary>
    [DisallowMultipleComponent]
    public class Mirror : MonoBehaviour
    {
        public MirrorSettings Settings;

        Renderer glass;
        Camera cam;
        RenderTexture rt;
        Material mat;
        Vector2 size;          // glass width and height, metres
        float nextRender;

        void Start()
        {
            glass = GetComponent<Renderer>();
            if (glass == null) { Plugin.Log.LogWarning($"{name}: a mirror needs a renderer"); Destroy(this); return; }
            if (Settings == null) Settings = Plugin.Defaults.Clone();

            var mf = GetComponent<MeshFilter>();
            var b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.1f);
            var ls = transform.lossyScale;
            size = new Vector2(Mathf.Abs(b.size.x * ls.x), Mathf.Abs(b.size.y * ls.y));
            if (size.x < 1e-4f || size.y < 1e-4f) size = new Vector2(0.1f, 0.05f);

            int h = Settings.Resolution, w = Mathf.Clamp(Mathf.RoundToInt(h * size.x / size.y), 16, 4096);
            rt = new RenderTexture(w, h, 24) { name = "NOMirror_" + name, antiAliasing = 1 };

            var go = new GameObject("NOMirror_camera");
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.enabled = false;
            cam.nearClipPlane = Settings.Near;
            cam.farClipPlane = Settings.Far;

            mat = MirrorSystem.MakeMaterial(rt, Settings);
            glass.material = mat;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            var main = Camera.main;
            bool render = Plugin.Enabled.Value && main != null && glass.isVisible && Time.unscaledTime >= nextRender;
            if (!render) { cam.enabled = false; return; }
            if (Settings.UpdateRate > 0f) nextRender = Time.unscaledTime + 1f / Settings.UpdateRate;

            Vector3 p = glass.bounds.center, eye = main.transform.position;
            Vector3 n = transform.forward;
            if (Vector3.Dot(n, eye - p) < 0f) n = -n;                       // the face the pilot sees
            Vector3 view = p - eye;
            float dist = Mathf.Max(view.magnitude, 0.05f);
            Vector3 dir = Vector3.Reflect(view / dist, n);
            Vector3 up = Vector3.ProjectOnPlane(transform.up, dir);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.up;
            cam.transform.SetPositionAndRotation(p + n * 0.01f, Quaternion.LookRotation(dir, up));

            // the glass seen from the eye: its height sets the vertical field of view, its shape the aspect
            float fov = 2f * Mathf.Atan(size.y * 0.5f / dist) * Mathf.Rad2Deg * Settings.FovScale;
            cam.fieldOfView = Mathf.Clamp(fov, 1f, 170f);
            cam.aspect = size.x / size.y;
            cam.cullingMask = main.cullingMask & ~Plugin.ExcludedLayers;
            cam.depth = main.depth - 1f;
            cam.enabled = true;
        }

        void OnDestroy()
        {
            if (cam != null) Destroy(cam.gameObject);
            if (rt != null) { rt.Release(); Destroy(rt); }
            if (mat != null) Destroy(mat);
        }
    }
}
