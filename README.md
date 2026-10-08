# NO Mirrors: working mirrors for Nuclear Option

A small BepInEx plugin that makes cockpit mirrors actually reflect. It is meant to be shared: any aircraft mod can add mirrors
without writing code, and other plugins can use it through a small API.

**Status: early (0.0.3).** Working in game with the [MiG-29 Fulcrum mod](https://github.com/IornMan1213/MiG29-NuclearOption)'s
three canopy mirrors, the reference user of this framework.

- [For players](#for-players)
- [Adding mirrors to your aircraft (no code)](#adding-mirrors-to-your-aircraft-no-code)
- [Troubleshooting your mirrors](#troubleshooting-your-mirrors)
- [Using it from code](#using-it-from-code)
- [How it works](#how-it-works)
- [Building](#building)

## For players

Put `NOMirrors.dll` in `BepInEx/plugins/` (any subfolder). Requires BepInEx 5. Aircraft that have mirrors for it then show
working mirrors; nothing changes on other aircraft. Settings (BepInEx Configuration Manager, F1):

- **General > Enabled**: turn mirror rendering off (each mirror keeps its last image).
- **General > Mode**: *Cameras* (default, sharp and exact) or *Probe* (cheaper, soft). See [How it works](#how-it-works).
- **General > Field of view**: 1 = flat mirrors (true to life, a narrow view); above 1 widens every mirror like a convex one.
- **General > Reflect the cockpit**: mirrors also show the cockpit interior (seat, canopy frame).
- **Quality > Resolution / Updates per second / View distance**: defaults for every mirror (an aircraft's own settings win).
- **Quality > Probe resolution / Probe updates per second**: probe mode only.

Mirrors cost little: they take turns (at most one renders per frame) and only render while you are in the cockpit looking at
them. On the test PC the frame rate was the same with them on or off.

## Adding mirrors to your aircraft (no code)

Your aircraft does not need to depend on this plugin. Players without it simply see your glass's own material.

### 1. Model the glass

Make the reflecting surface its own object: a flat rectangle (a quad is enough) placed on the face of your mirror housing,
a millimetre or so proud of it.

- Its local axes: **Z** is the mirror's normal (either way round), **X** runs across the glass, **Y** up it.
- UVs cover 0..1 across the face, as seen from the front (u along local X, v along local Y).
- Give it a sensible fallback material for players without the plugin (plain mirror grey or dark glass).
- The housing can sit right behind the glass: the reflection starts at the glass, so the housing never shows in it.
- The glass can be a child of moving parts (a folding hinge, a canopy): the reflection follows it every frame.

**Aiming.** A mirror shows the view reflected about its normal. To make a mirror show direction `view` (from the glass, say
straight back), point its normal halfway between "towards the pilot's eye" and `view`:
`normal = normalize(normalize(eye - glass) + normalize(view))`. Aiming the glass straight at the eye only shows the seat behind the pilot.

**Size and field of view.** A real flat mirror only shows the angle it covers from the eye: a 20 cm glass 60 cm away shows
about 20 degrees. For a useful rear view, make it convex with the `fov` token (2 to 3 works well for small cockpit mirrors).

### 2. Name it

Name the object `NOMirror`, or `NOMirror_<anything>` to tell several apart: `NOMirror_left`, `NOMirror_centre`. The plugin finds
every renderer whose name starts with `NOMirror` on the aircraft the player flies (all of its parts, active or not).

Settings go in the name as `_`-separated tokens; anything else is ignored. Decimals use a dot.

| Token | Meaning | Default |
|---|---|---|
| `res`N | texture height in pixels (16 to 4096; the width follows the glass's shape) | 256 (player setting) |
| `fps`N | updates per second, 0 = every frame | 30 (player setting) |
| `fov`X | field of view: above 1 is a convex mirror (wider view); multiplies the player's setting | 1 |
| `far`X | how far the mirror sees, metres | 3000 (player setting) |
| `bright`X | reflection brightness | 0.9 |
| `range`X | only render while the player's view is within this many metres of the glass | 3 |
| `cockpit` / `nocockpit` | always / never show the cockpit interior in this mirror (default: the player's setting) | |
| `noflip` | reverse the image left-right (only if your UVs run opposite to the glass's local X) | |

Example: `NOMirror_left_res320_fov3`.

### 3. Unity / Blueprinter notes

- **Readable meshes.** Meshes in asset bundles are usually not readable from code. That is fine for rectangles: the plugin then
  draws the glass as a rectangle over the mesh's bounds. For other shapes (a rounded mirror), enable *Read/Write* on the mesh so
  its own outline is kept.
- **Layer.** Put the glass on the layer the rest of your cockpit uses. In cockpit view the game draws the interior with its own
  camera; the plugin finds that camera's layers by itself.
- **Scale.** Any transform scale works; the reflection is worked out in world space.

### 4. Test it

1. Fly the aircraft (or just spawn it) and look at the mirrors.
2. If something looks wrong, create an empty file `BepInEx/nomirrors_debug.flag`. Every 3 seconds the log then shows each
   mirror's position, normal and the direction it reflects (also in the aircraft's frame: +Z forward, +X right, +Y up), and
   `BepInEx/nomirrors_debug/<mirror>.png` shows exactly what each mirror sees.
3. Lines in the flag file change settings live: `fov=2.5`, `mode=Probe`, `cockpit=false`, `enabled=false`.
4. Delete the flag file when done.

A complete example: the MiG-29 mod builds its mirrors in `blender/cockpit_build.py` (`mirror()`: housing, aim, glass frame) and
`unity/MiG29Tools/MiG29Cockpit.cs` (`MirrorGlass()`: the quad, its UVs and the `NOMirror_<id>_res320_fov3` name).

## Troubleshooting your mirrors

| What you see | Likely cause | Fix |
|---|---|---|
| Plain glass, no reflection | the object name does not start with `NOMirror`, or the player's prefix setting differs | rename the object; check the log for `<aircraft>: N mirror(s)` |
| Only sky, only ground, or the wrong side | the glass's normal is aimed wrong | aim it with the rule above; the debug log prints the reflected direction in the aircraft's frame |
| Picture mirrored the wrong way | UVs run opposite to the glass's local X | add `noflip` |
| Picture upside down | UVs' v runs opposite to the glass's local Y | rotate the glass object 180 degrees about its Z, or fix the UVs |
| Tiny, zoomed-in view | a flat mirror is true to life | add `fov2` or `fov3` |
| Reflection stops when the view moves away | the pilot's eye is more than `range` metres from the glass | add `range5` (or more) |
| A rectangle instead of your rounded glass | the mesh is not readable | enable Read/Write on the mesh |

## Using it from code

Reference `NOMirrors.dll` (and keep it out of your build output). Either depend on it:

```csharp
[BepInDependency("iornman.nomirrors")]
```

or make it optional, so your plugin still loads without it:

```csharp
[BepInDependency("iornman.nomirrors", BepInDependency.DependencyFlags.SoftDependency)]
...
if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("iornman.nomirrors")) MirrorHooks.Install();   // touch NOMirrors types only inside MirrorHooks
```

The API:

```csharp
using NOMirrors;

// make a renderer reflect: settings from its name over the player's defaults, or your own
Mirror m = MirrorSystem.Attach(glassRenderer);
MirrorSystem.Attach(glassRenderer, new MirrorSettings { Resolution = 512, UpdateRate = 60, FovScale = 2f, ReflectCockpit = true });

// every renderer under a transform whose name starts with NOMirror
int n = MirrorSystem.AttachAll(aircraftRoot);

// change a live mirror
m.Settings.FovScale = 3f;
m.Refresh();          // re-applies settings (new resolution, mode, ...)
m.RenderNow();        // render at the next chance instead of waiting for the update rate

// read it
RenderTexture tex = m.Texture;   // camera mode: what the glass shows
MirrorMode mode = m.Mode;        // Cameras or Probe
float t = m.LastRenderTime;      // Time.unscaledTime of the last render

// every live mirror, and when they come and go (mirrors made by name included)
foreach (var mirror in MirrorSystem.Mirrors) { }
MirrorSystem.MirrorAdded += mirror => Logger.LogInfo($"{mirror.name}: {mirror.Settings}");
MirrorSystem.MirrorRemoved += mirror => { };

// stop it reflecting (its own material comes back)
MirrorSystem.Detach(glassRenderer);

// MirrorSystem.ApiVersion (1 since 0.0.3) and MirrorSystem.Version, if you need to check what you run against
```

A complete example plugin is in [examples/MirrorExample](examples/MirrorExample).

## How it works

- **Cameras** (default). A true planar reflection per mirror: a camera sits at the pilot's eye mirrored through the glass's
  plane and looks back through the glass, with an off-axis frustum whose near plane is exactly the glass. Every texel shows what
  real glass would reflect at that point, so the image maps straight onto the glass, moves with your head, and nothing in front
  of the glass (the mirror's own housing, the airframe ahead) gets in. A convex `fov` pulls the virtual eye towards the glass.
  Mirrors take turns: at most one mirror renders per frame, each capped at *Updates per second*, and none while off screen,
  turned away or seen from outside the cockpit. Mirror cameras skip post-processing, anti-aliasing and screen copies, and don't
  see mirror glass or the aircraft's canopy glass.
- **Probe**. One realtime reflection probe at the pilot's eye, shared by every mirror; the glass gets a fully metallic, fully
  smooth material and looks the reflection up per pixel. Cheapest, but soft (a small flat mirror covers only a few texels of the
  cube), and the cube is captured from the eye, not the glass. Convex mirrors bend the glass's normals. Probe mode needs the
  game's URP Lit shader; without it, mirrors use cameras.

Mirrors only work on the aircraft you are flying (or on renderers a plugin attaches itself).

## Building

```bash
dotnet build src/NOMirrors -c Release
```

It builds against your installed game; pass `-p:GameDir="D:\Games\Nuclear Option"` if it is not in the default Steam folder.
No game files are in this repository. `NOMirrors.xml` (the API's documentation, for IntelliSense) is built next to the DLL.

## Roadmap

- Curved (true convex) glass in camera mode.
- Mirrors on other aircraft (wingmen) when seen from close by.

## Licence

MIT, see [LICENSE](LICENSE). Use it in your own mods, including bundled with them.
