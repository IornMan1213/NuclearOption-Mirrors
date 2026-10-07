using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace NOMirrors
{
    /// <summary>Working mirrors for Nuclear Option aircraft. See README.md for how to add mirrors to a craft.</summary>
    [BepInPlugin(Guid, "NO Mirrors", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "iornman.nomirrors";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> Prefix;
        internal static MirrorSettings Defaults = new MirrorSettings();
        internal static int ExcludedLayers = 1 << 5;   // UI

        void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true, "Render mirrors (off: they keep their last image)");
            Prefix = Config.Bind("General", "Object name prefix", "NOMirror", "Renderers whose object name starts with this become mirrors");
            var res = Config.Bind("Quality", "Resolution", 256, new ConfigDescription("Reflection texture height in pixels",
                new AcceptableValueRange<int>(32, 2048)));
            var fps = Config.Bind("Quality", "Updates per second", 30f, new ConfigDescription("How often each mirror re-renders (0 = every frame)",
                new AcceptableValueRange<float>(0f, 144f)));
            var far = Config.Bind("Quality", "View distance", 3000f, "How far the mirrors see, metres");
            void Apply() { Defaults.Resolution = res.Value; Defaults.UpdateRate = fps.Value; Defaults.Far = far.Value; }
            Apply();
            res.SettingChanged += (_, __) => Apply(); fps.SettingChanged += (_, __) => Apply(); far.SettingChanged += (_, __) => Apply();

            var go = new GameObject("NOMirrorsScanner");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<MirrorScanner>();
            Log.LogInfo($"NO Mirrors {Version} ready");
        }
    }
}
