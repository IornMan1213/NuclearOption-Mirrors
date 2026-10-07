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
        /// <summary>Field of view: 1 = a flat mirror; above 1 = convex (a wider view). Multiplies the player's General > Field of view.</summary>
        public float FovScale = 1f;
        /// <summary>Far clip of the reflection camera, metres (the near clip is the glass itself; Near is kept for compatibility).</summary>
        public float Near = 0.05f, Far = 3000f;
        /// <summary>The image's orientation on the glass is worked out from the glass's local axes; set false (token <c>noflip</c>)
        /// only if the glass's UVs run opposite to its local X.</summary>
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
