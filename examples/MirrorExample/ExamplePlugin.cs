using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace NOMirrorsExample
{
    /// <summary>
    /// Example of using NO Mirrors from code. NO Mirrors is optional here: without it this plugin still loads and does nothing.
    /// - Logs every mirror as it comes and goes (mirrors made by name included).
    /// - "Glass object" setting: a renderer of that exact name on the player's aircraft is made a mirror with explicit settings,
    ///   for aircraft whose authors did not name their glass NOMirror.
    /// - "Convex" hotkey: switches every mirror between flat and convex while flying.
    /// </summary>
    [BepInPlugin("example.nomirrors.usage", "NO Mirrors example", "1.0.0")]
    [BepInDependency("iornman.nomirrors", BepInDependency.DependencyFlags.SoftDependency)]
    public class ExamplePlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<string> GlassName;
        internal static ConfigEntry<KeyboardShortcut> ConvexKey;
        internal static BepInEx.Logging.ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            GlassName = Config.Bind("Example", "Glass object", "", "Exact object name of a renderer on your aircraft to turn into a mirror");
            ConvexKey = Config.Bind("Example", "Convex", new KeyboardShortcut(KeyCode.F7), "Switch all mirrors between flat and convex");
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("iornman.nomirrors")) { Logger.LogInfo("NO Mirrors not installed: nothing to do"); return; }
            // NOMirrors types are only touched inside MirrorHooks, so this class loads even when NOMirrors.dll is missing
            MirrorHooks.Install(gameObject);
        }
    }
}
