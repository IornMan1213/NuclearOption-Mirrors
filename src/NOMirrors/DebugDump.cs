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
            }
            var mirrors = FindObjectsOfType<Mirror>();
            if (mirrors.Length == 0) return;
            Directory.CreateDirectory(Dir);
            var main = Plugin.ViewCamera();
            var eye = main != null ? main.transform.position : Vector3.zero;
            Plugin.Log.LogInfo($"[debug] view camera {(main != null ? main.name : "none")} at {eye:F3} fwd {(main != null ? main.transform.forward : Vector3.zero):F3} mask {(main != null ? main.cullingMask : 0):X}");
            foreach (var c in Camera.allCameras)
                Plugin.Log.LogInfo($"[debug] camera {c.name} depth {c.depth} mask {c.cullingMask:X} rt {(c.targetTexture != null)} at {c.transform.position:F3} near {c.nearClipPlane:F3}");
            foreach (var m in mirrors)
            {
                var t = m.transform;
                var r = m.GetComponent<Renderer>();
                Vector3 c = r.bounds.center, n = t.forward;
                if (Vector3.Dot(n, eye - c) < 0f) n = -n;
                var refl = Vector3.Reflect((c - eye).normalized, n);
                var root = t.root;
                Plugin.Log.LogInfo($"[debug] {m.name} {m.ActiveMode}: centre {c:F3} normal {n:F3} reflects {refl:F3} " +
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
