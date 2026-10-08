using System;
using NOMirrors;

// Checks MirrorSettings.Parse (the name tokens modders rely on) without the game. dotnet run -c Release
static class Program
{
    static int failures;

    static void Check(string what, bool ok)
    {
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {what}");
        if (!ok) failures++;
    }

    static int Main()
    {
        var d = new MirrorSettings();

        var s = MirrorSettings.Parse("NOMirror_left_res384_fps45_fov2.5_far1500_bright0.8_range5", d);
        Check("res", s.Resolution == 384);
        Check("fps", s.UpdateRate == 45f);
        Check("fov decimal", Math.Abs(s.FovScale - 2.5f) < 1e-6);
        Check("far", s.Far == 1500f);
        Check("bright", Math.Abs(s.Brightness - 0.8f) < 1e-6);
        Check("range", s.Range == 5f);
        Check("defaults untouched", d.Resolution == 256 && d.FovScale == 1f);

        Check("noflip", !MirrorSettings.Parse("NOMirror_x_noflip", d).FlipX);
        Check("cockpit", MirrorSettings.Parse("NOMirror_cockpit", d).ReflectCockpit == true);
        Check("nocockpit", MirrorSettings.Parse("NOMirror_nocockpit", d).ReflectCockpit == false);
        Check("cockpit follows player by default", MirrorSettings.Parse("NOMirror", d).ReflectCockpit == null);

        Check("res clamped low", MirrorSettings.Parse("NOMirror_res2", d).Resolution == 16);
        Check("res clamped high", MirrorSettings.Parse("NOMirror_res99999", d).Resolution == 4096);
        Check("fov floor", MirrorSettings.Parse("NOMirror_fov0", d).FovScale == 0.1f);
        Check("upper case tokens", MirrorSettings.Parse("NOMirror_RES128_FOV3", d).Resolution == 128);
        Check("names are not tokens", MirrorSettings.Parse("NOMirror_resolute_farleft", d).Resolution == 256);
        Check("bad numbers ignored", MirrorSettings.Parse("NOMirror_fovabc", d).FovScale == 1f);
        Check("null defaults", MirrorSettings.Parse("NOMirror_res64", null).Resolution == 64);

        // a dot decimal must parse the same whatever the PC's language
        var prev = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        Check("fov2.5 under a comma-decimal locale", Math.Abs(MirrorSettings.Parse("NOMirror_fov2.5", d).FovScale - 2.5f) < 1e-6);
        System.Threading.Thread.CurrentThread.CurrentCulture = prev;

        Console.WriteLine(failures == 0 ? "all passed" : $"{failures} failed");
        return failures == 0 ? 0 : 1;
    }
}
