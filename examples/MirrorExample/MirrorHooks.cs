using NOMirrors;
using UnityEngine;

namespace NOMirrorsExample
{
    /// <summary>Everything that uses NO Mirrors types (see ExamplePlugin for why it is kept separate).</summary>
    static class MirrorHooks
    {
        internal static void Install(GameObject host)
        {
            ExamplePlugin.Log.LogInfo($"NO Mirrors {MirrorSystem.Version} (API {MirrorSystem.ApiVersion})");
            MirrorSystem.MirrorAdded += m => ExamplePlugin.Log.LogInfo($"mirror {m.name} ({m.Mode}): {m.Settings}");
            MirrorSystem.MirrorRemoved += m => ExamplePlugin.Log.LogInfo($"mirror {m.name} removed");
            host.AddComponent<Runner>();
        }

        class Runner : MonoBehaviour
        {
            Aircraft last;
            bool convex;

            void Update()
            {
                if (ExamplePlugin.ConvexKey.Value.IsDown())
                {
                    convex = !convex;
                    foreach (var m in MirrorSystem.Mirrors)
                    {
                        m.Settings.FovScale = convex ? 2.5f : 1f;
                        m.Refresh();
                    }
                    ExamplePlugin.Log.LogInfo(convex ? "mirrors convex" : "mirrors flat");
                }

                // once per aircraft: make the configured renderer a mirror
                var hud = SceneSingleton<CombatHUD>.i;
                var ac = hud != null ? hud.aircraft : null;
                if (ac == null || ac == last || ExamplePlugin.GlassName.Value == "") return;
                last = ac;
                foreach (var r in ac.GetComponentsInChildren<Renderer>(true))
                    if (r.name == ExamplePlugin.GlassName.Value)
                        MirrorSystem.Attach(r, new MirrorSettings { Resolution = 384, UpdateRate = 30, FovScale = 2f, ReflectCockpit = true });
            }
        }
    }
}
