using System;
using System.Globalization;

namespace NOMirrors
{
    /// <summary>Per-mirror settings. Defaults come from the plugin config; a mirror's object name can override them
    /// (see <see cref="Parse"/>), and code that calls <see cref="MirrorSystem.Attach"/> can pass its own.
    /// After changing a live mirror's settings, call <see cref="Mirror.Refresh"/>.</summary>
    public class MirrorSettings
    {
        /// <summary>Render texture height in pixels (the width follows the mirror's shape). Token <c>res</c>N.</summary>
        public int Resolution = 256;
        /// <summary>Reflection updates per second (0 = every frame). Mirrors take turns, at most one renders per frame. Token <c>fps</c>N.</summary>
        public float UpdateRate = 30f;
        /// <summary>Field of view: 1 = a flat mirror; above 1 = convex (a wider view). Multiplies the player's General > Field of view.
        /// Token <c>fov</c>X.</summary>
        public float FovScale = 1f;
        /// <summary>Unused since 0.0.2: the near clip is the glass itself. Kept so existing code still compiles.</summary>
        public float Near = 0.05f;
        /// <summary>How far the mirror sees, metres. Token <c>far</c>X.</summary>
        public float Far = 3000f;
        /// <summary>The image's orientation on the glass is worked out from the glass's local axes; set false (token <c>noflip</c>)
        /// only if the glass's UVs run opposite to its local X.</summary>
        public bool FlipX = true;
        /// <summary>Brightness of the reflection (mirrors lose a little light). Token <c>bright</c>X.</summary>
        public float Brightness = 0.9f;
        /// <summary>Show the cockpit interior in this mirror: null follows the player's General > Reflect the cockpit.
        /// Tokens <c>cockpit</c> / <c>nocockpit</c>.</summary>
        public bool? ReflectCockpit;
        /// <summary>The mirror only renders while the player's view is within this distance of the glass, metres (in the cockpit, not
        /// from outside views). Raise it for mirrors far from the pilot's seat. Token <c>range</c>X.</summary>
        public float Range = 3f;

        /// <summary>A copy of these settings.</summary>
        public MirrorSettings Clone() => (MirrorSettings)MemberwiseClone();

        /// <summary>Reads overrides from an object name such as <c>NOMirror_left_res512_fps60_fov1.3_noflip</c>.
        /// Tokens: <c>res</c>N, <c>fps</c>N, <c>fov</c>X, <c>far</c>X, <c>bright</c>X, <c>range</c>X, <c>noflip</c>, <c>cockpit</c>,
        /// <c>nocockpit</c>. Others are ignored (use them to name your mirrors). Decimals use a dot: <c>fov2.5</c>.</summary>
        public static MirrorSettings Parse(string name, MirrorSettings defaults)
        {
            var s = (defaults ?? new MirrorSettings()).Clone();
            foreach (var raw in name.Split('_'))
            {
                var t = raw.ToLowerInvariant();
                if (t == "noflip") { s.FlipX = false; continue; }
                if (t == "cockpit") { s.ReflectCockpit = true; continue; }
                if (t == "nocockpit") { s.ReflectCockpit = false; continue; }
                if (Num(t, "res", out var v)) s.Resolution = Math.Max(16, Math.Min(4096, (int)v));
                else if (Num(t, "fps", out v)) s.UpdateRate = Math.Max(0f, v);
                else if (Num(t, "fov", out v)) s.FovScale = Math.Max(0.1f, v);
                else if (Num(t, "near", out v)) s.Near = Math.Max(0.001f, v);
                else if (Num(t, "far", out v)) s.Far = Math.Max(1f, v);
                else if (Num(t, "bright", out v)) s.Brightness = Math.Max(0f, v);
                else if (Num(t, "range", out v)) s.Range = Math.Max(0.1f, v);
            }
            return s;
        }

        static bool Num(string token, string prefix, out float value)
        {
            value = 0f;
            return token.StartsWith(prefix) && token.Length > prefix.Length &&
                   float.TryParse(token.Substring(prefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <inheritdoc/>
        public override string ToString() =>
            FormattableString.Invariant($"res {Resolution}, fps {UpdateRate}, fov {FovScale}, far {Far}, bright {Brightness}, range {Range}, flipX {FlipX}, cockpit {(ReflectCockpit.HasValue ? ReflectCockpit.ToString() : "player")}");
    }
}
