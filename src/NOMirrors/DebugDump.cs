using System.IO;
using BepInEx;
using UnityEngine;

namespace NOMirrors
{
    /// <summary>
    /// Developer aid, off unless <c>BepInEx/nomirrors_debug.flag</c> exists: every few seconds, logs each mirror's frame and the
    /// eye, and saves what each mirror shows (camera mode) or the shared probe's six faces (probe mode) as PNGs in
    /// <c>BepInEx/nomirrors_debug/</c>.
    /// </summary>
    public class DebugDump : MonoBehaviour
    {
        static string Flag => Path.Combine(Paths.BepInExRootPath, "nomirrors_debug.flag");
        static string Dir => Path.Combine(Paths.BepInExRootPath, "nomirrors_debug");
        float next;
        bool layersLogged;

        internal static bool StockCameras;   // debug A/B: mirror cameras without the plain-render settings (applies on the next mode change)
        static float look;
        static int turnedFrame = -1;
        static Quaternion gameRotation;

        void OnEnable() => UnityEngine.Rendering.RenderPipelineManager.beginContextRendering += Turn;
        void OnDisable() => UnityEngine.Rendering.RenderPipelineManager.beginContextRendering -= Turn;

        // turn for this frame's renders only; LateUpdate of the next frame starts from the game's own rotation again
        static void Turn(UnityEngine.Rendering.ScriptableRenderContext ctx, System.Collections.Generic.List<Camera> cams)
        {
            if (look == 0f || turnedFrame == Time.frameCount) return;
            var main = Plugin.ViewCamera();
            if (main == null) return;
            turnedFrame = Time.frameCount;
            gameRotation = main.transform.localRotation;
            main.transform.rotation = Quaternion.AngleAxis(look, main.transform.parent != null ? main.transform.parent.up : Vector3.up) * main.transform.rotation;
            instance.StartCoroutine(Restore(main.transform));
        }

        static System.Collections.IEnumerator Restore(Transform t)
        {
            yield return new WaitForEndOfFrame();
            if (t != null) t.localRotation = gameRotation;
        }

        static DebugDump instance;
        void Awake() => instance = this;
        void Update()
        {
            
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 3f;
            if (!File.Exists(Flag)) return;
            try { Dump(); }
            catch (System.Exception e) { Plugin.Log.LogError(e); next = Time.unscaledTime + 30f; }
        }

        void Dump()
        {
            // live tuning: lines "key=value" in the flag file set config entries (fov, mode, cockpit)
            foreach (var line in File.ReadAllLines(Flag))
            {
                var kv = line.Split('=');
                if (kv.Length != 2) continue;
                string k = kv[0].Trim().ToLowerInvariant(), v = kv[1].Trim();
                if (k == "fov" && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fv)) Plugin.FieldOfView.Value = fv;
                else if (k == "mode" && System.Enum.TryParse<MirrorMode>(v, true, out var mv)) Plugin.Mode.Value = mv;
                else if (k == "cockpit" && bool.TryParse(v, out var bv)) Plugin.ShowCockpit.Value = bv;
                else if (k == "enabled" && bool.TryParse(v, out var ev)) Plugin.Enabled.Value = ev;
                else if (k == "timescale" && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tv)) Time.timeScale = tv;
                else if (k == "stock" && bool.TryParse(v, out var sv)) StockCameras = sv;
                else if (k == "look" && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var yv)) look = yv;
            }
            var mirrors = FindObjectsOfType<Mirror>();
            if (mirrors.Length == 0) return;
            Directory.CreateDirectory(Dir);
            var main = Plugin.ViewCamera();
            var eye = main != null ? main.transform.position : Vector3.zero;
            Plugin.Log.LogInfo($"[debug] view camera {(main != null ? main.name : "none")} at {eye:F3} fwd {(main != null ? main.transform.forward : Vector3.zero):F3} mask {(main != null ? main.cullingMask : 0):X}");
            foreach (var c in Camera.allCameras)
                Plugin.Log.LogInfo($"[debug] camera {c.name} depth {c.depth} mask {c.cullingMask:X} rt {(c.targetTexture != null)} at {c.transform.position:F3} near {c.nearClipPlane:F3}");
            if (!layersLogged)
            {
                layersLogged = true;
                for (int l = 0; l < 32; l++) if (LayerMask.LayerToName(l) != "") Plugin.Log.LogInfo($"[debug] layer {l} = {LayerMask.LayerToName(l)}");
                foreach (var r in FindObjectsOfType<Renderer>())
                    if (r.gameObject.layer == 3)
                        Plugin.Log.LogInfo($"[debug] layer {r.gameObject.layer}: {r.GetType().Name} {r.name} under {(r.transform.parent != null ? r.transform.parent.name : "-")} mat {(r.sharedMaterial != null ? r.sharedMaterial.shader.name : "-")}");
            }
            foreach (var m in mirrors)
            {
                var t = m.transform;
                var r = m.GetComponent<Renderer>();
                Vector3 c = r.bounds.center, n = t.forward;
                if (Vector3.Dot(n, eye - c) < 0f) n = -n;
                var refl = Vector3.Reflect((c - eye).normalized, n);
                var root = t.root;
                Plugin.Log.LogInfo($"[debug] {m.name} {m.Mode}: centre {c:F3} normal {n:F3} reflects {refl:F3} " +
                                   $"(aircraft frame {root.InverseTransformDirection(refl):F3}) visible {r.isVisible} layer {m.gameObject.layer}");
                var cam = m.ReflectionCamera;
                if (cam != null)
                    Plugin.Log.LogInfo($"[debug]   camera at {cam.transform.position:F3} fwd {cam.transform.forward:F3} near {cam.nearClipPlane:F3}");
                if (m.Texture != null) Save(m.Texture, Path.Combine(Dir, m.name + ".png"));
            }
            var probe = ProbeReflections.Texture;
            if (probe != null)
                for (int f = 0; f < 6; f++) SaveFace(probe, f, Path.Combine(Dir, $"probe_{(CubemapFace)f}.png"));
        }

        static void Save(RenderTexture rt, string path)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
        }

        static void SaveFace(Texture cube, int face, string path)
        {
            var tmp = RenderTexture.GetTemporary(cube.width, cube.height, 0, ((RenderTexture)cube).format);
            Graphics.CopyTexture(cube, face, 0, tmp, 0, 0);
            Save(tmp, path);
            RenderTexture.ReleaseTemporary(tmp);
        }
    }
}
