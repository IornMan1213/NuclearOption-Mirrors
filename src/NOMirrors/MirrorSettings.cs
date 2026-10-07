using System;
using System.Globalization;

namespace NOMirrors
{
    /// <summary>Per-mirror settings. Defaults come from the plugin config; a mirror's object name can override them
    /// (see <see cref="Parse"/>), and code that calls <see cref="MirrorSystem.Attach"/> can pass its own.</summary>
    public class MirrorSettings
    {
        /// <summary>Render texture height in pixels (the width follows the mirror's shape).</summary>
        public int Resolution = 256;
        /// <summary>Reflection updates per second (0 = every frame).</summary>
        public float UpdateRate = 30f;
        /// <summary>Multiplies the field of view the mirror's size gives from the pilot's eye (more than 1 = wider, convex look).</summary>
        public float FovScale = 1f;
        /// <summary>Near and far clip of the reflection camera, metres.</summary>
        public float Near = 0.05f, Far = 3000f;
        /// <summary>Flip the image left-right on the glass. A mirror shows the world reversed; flip this off if the glass's UVs
        /// already run right-to-left as seen from the front.</summary>
        public bool FlipX = true;
        /// <summary>Brightness of the reflection (mirrors lose a little light).</summary>
        public float Brightness = 0.9f;

        public MirrorSettings Clone() => (MirrorSettings)MemberwiseClone();

        /// <summary>Reads overrides from an object name such as <c>NOMirror_left_res512_fps60_fov1.3_noflip</c>.
        /// Tokens: <c>res</c>N, <c>fps</c>N, <c>fov</c>X, <c>near</c>X, <c>far</c>X, <c>bright</c>X, <c>noflip</c>. Others are ignored
        /// (use them to name your mirrors).</summary>
        public static MirrorSettings Parse(string name, MirrorSettings defaults)
        {
            var s = defaults.Clone();
            foreach (var raw in name.Split('_'))
            {
                var t = raw.ToLowerInvariant();
                if (t == "noflip") { s.FlipX = false; continue; }
                if (Num(t, "res", out var v)) s.Resolution = Math.Max(16, (int)v);
                else if (Num(t, "fps", out v)) s.UpdateRate = Math.Max(0f, v);
                else if (Num(t, "fov", out v)) s.FovScale = Math.Max(0.1f, v);
                else if (Num(t, "near", out v)) s.Near = Math.Max(0.001f, v);
                else if (Num(t, "far", out v)) s.Far = Math.Max(1f, v);
                else if (Num(t, "bright", out v)) s.Brightness = Math.Max(0f, v);
            }
            return s;
        }

        static bool Num(string token, string prefix, out float value)
        {
            value = 0f;
            return token.StartsWith(prefix) && token.Length > prefix.Length &&
                   float.TryParse(token.Substring(prefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
