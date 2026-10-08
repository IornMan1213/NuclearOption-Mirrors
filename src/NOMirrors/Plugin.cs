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
        public const string Version = "0.0.3";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> Prefix;
        internal static ConfigEntry<MirrorMode> Mode;
        internal static ConfigEntry<int> ProbeResolution;
        internal static ConfigEntry<float> ProbeRate;
        internal static ConfigEntry<float> FieldOfView;
        internal static ConfigEntry<bool> ShowCockpit;
        internal static MirrorSettings Defaults = new MirrorSettings();
        internal static int ExcludedLayers = 1 << 5;   // UI

        /// <summary>The camera the player sees through: the game's main camera (CameraStateManager), else Unity's.</summary>
        internal static Camera ViewCamera()
        {
            var csm = SceneSingleton<CameraStateManager>.i;
            return csm != null && csm.mainCamera != null ? csm.mainCamera : Camera.main;
        }

        void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true, "Render mirrors (off: they keep their last image)");
            Prefix = Config.Bind("General", "Object name prefix", "NOMirror", "Renderers whose object name starts with this become mirrors");
            Mode = Config.Bind("General", "Mode", MirrorMode.Cameras,
                "Cameras: a true planar reflection per mirror (sharp and exact). Probe: one shared reflection cube at the eye, " +
                "looked up per pixel on the glass (cheapest, but soft, and it sees the airframe round the eye)");
            ProbeResolution = Config.Bind("Quality", "Probe resolution", 512, new ConfigDescription(
                "Probe mode: size of each face of the shared reflection cube (higher = sharper mirrors)", new AcceptableValueList<int>(128, 256, 512, 1024)));
            ProbeRate = Config.Bind("Quality", "Probe updates per second", 15f, new ConfigDescription(
                "Probe mode: how often the shared reflection refreshes (each refresh renders six cube faces; 0 = every frame)",
                new AcceptableValueRange<float>(0f, 60f)));
            FieldOfView = Config.Bind("General", "Field of view", 1f, new ConfigDescription(
                "Camera mode: 1 = flat mirrors (true to life, a narrow view); above 1 widens every mirror like a convex one", new AcceptableValueRange<float>(0.5f, 4f)));
            ShowCockpit = Config.Bind("General", "Reflect the cockpit", true,
                "Camera mode: mirrors also show the cockpit interior (seat, canopy frame), which the game draws with its own camera");
            var res = Config.Bind("Quality", "Resolution", 256, new ConfigDescription("Camera mode: reflection texture height in pixels",
                new AcceptableValueRange<int>(32, 2048)));
            var fps = Config.Bind("Quality", "Updates per second", 30f, new ConfigDescription("How often each mirror re-renders (0 = every frame)",
                new AcceptableValueRange<float>(0f, 144f)));
            var far = Config.Bind("Quality", "View distance", 3000f, "How far the mirrors see, metres");
            void Apply() { Defaults.Resolution = res.Value; Defaults.UpdateRate = fps.Value; Defaults.Far = far.Value; }
            Apply();
            void Changed(object _, System.EventArgs __) { Apply(); MirrorSystem.RefreshAll(); }   // live mirrors pick the new defaults up
            res.SettingChanged += Changed; fps.SettingChanged += Changed; far.SettingChanged += Changed;

            var go = new GameObject("NOMirrorsScanner");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<MirrorScanner>();
            go.AddComponent<DebugDump>();
            Log.LogInfo($"NO Mirrors {Version} ready");
        }
    }
}
