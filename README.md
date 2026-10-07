# NO Mirrors: working mirrors for Nuclear Option

A small BepInEx plugin that makes cockpit mirrors actually reflect. It is meant to be shared: any aircraft mod can add mirrors
without writing code, and other plugins can use it through a tiny API.

**Status: early (0.0.2).** Working in game with the [MiG-29 Fulcrum mod](https://github.com/IornMan1213/MiG29-NuclearOption)'s
three canopy mirrors.

## How it works

Two modes (BepInEx config, General > Mode):

- **Cameras** (default). A true planar reflection per mirror: a camera sits at the pilot's eye mirrored through the glass's
  plane and looks back through the glass, with an off-axis frustum whose near plane is exactly the glass. Every texel shows what
  real glass would reflect at that point, so the image maps straight onto the glass, moves with your head, and nothing in front
  of the glass (the mirror's own housing, the airframe ahead) gets in. Mirrors take turns: at most one mirror renders per frame,
  each capped at *Updates per second* (30 by default), and none while off screen, turned away or seen from outside the cockpit.
  Mirror cameras skip post-processing, anti-aliasing and screen copies, and don't see mirror glass or the aircraft's canopy glass.
- **Probe**. One realtime reflection probe at the pilot's eye, shared by every mirror; the glass gets a fully metallic, fully
  smooth material and looks the reflection up per pixel. Cheapest, but soft (a small flat mirror covers only a few texels of the
  cube), and the cube is captured from the eye, not the glass. Probe mode needs the game's URP Lit shader; without it, mirrors
  use cameras.

Real flat mirrors this size only show a narrow slice behind you. *General > Field of view* (or a mirror's `fov` token) above 1
makes them convex: a wider view along the same line. Mirrors only work on the aircraft you are flying.
## Adding mirrors to your aircraft (no code)

1. Model the reflecting surface as its own mesh object (a flat quad is enough):
   - local **Z** is the mirror's normal (either way round); local **X** runs across it, local **Y** up it;
   - UVs cover 0..1 across the reflecting face, as seen from the front.
2. Name the object `NOMirror`, or `NOMirror_<anything>` to tell several apart: `NOMirror_left`, `NOMirror_centre`.
3. Players who have this plugin installed see working mirrors; players without it see your mirror's own material, so give it
   a sensible look (dark glass, say).

Settings can be added to the name as `_`-separated tokens (anything else is ignored):

| Token | Meaning | Default |
|---|---|---|
| `res`N | texture height in pixels | 256 (config) |
| `fps`N | updates per second, 0 = every frame | 30 (config) |
| `fov`X | field of view: above 1 is a convex mirror (wider view); multiplies the player's setting | 1 |
| `far`X | how far the mirror sees, metres | 3000 |
| `bright`X | reflection brightness | 0.9 |
| `noflip` | reverse the image left-right (only if your UVs run opposite to the glass's local X) | |

Example: `NOMirror_left_res384_fps45_fov2`.

## Using it from code

Reference `NOMirrors.dll` and add `[BepInDependency("iornman.nomirrors")]` to your plugin.

```csharp
using NOMirrors;

// one renderer, with defaults (or settings read from its name)
Mirror m = MirrorSystem.Attach(myGlassRenderer);

// or explicit settings
MirrorSystem.Attach(myGlassRenderer, new MirrorSettings { Resolution = 512, UpdateRate = 60, FovScale = 1.2f });

// every renderer under a transform whose name starts with the configured prefix
int n = MirrorSystem.AttachAll(aircraftRoot);

MirrorSystem.Detach(myGlassRenderer);
```

## Player settings (BepInEx Configuration Manager)

- **General > Enabled**: turn mirror rendering off (each mirror keeps its last image).
- **General > Mode**: Cameras or Probe (above).
- **General > Field of view**: 1 = flat mirrors; above 1 widens every mirror like a convex one.
- **General > Reflect the cockpit**: include the cockpit interior (seat, canopy frame) in camera-mode mirrors.
- **General > Object name prefix**: `NOMirror` by default.
- **Quality > Probe resolution / Probe updates per second**: probe mode's cube size and refresh rate.
- **Quality > Resolution / Updates per second / View distance**: defaults for every mirror; name tokens override them.

## Install

Put `NOMirrors.dll` in `BepInEx/plugins/` (any subfolder). Requires BepInEx 5.

## Building

```bash
dotnet build src/NOMirrors -c Release
```

It builds against your installed game; pass `-p:GameDir="D:\Games\Nuclear Option"` if it is not in the default Steam folder.
No game files are in this repository.

## Debugging your mirrors

Create an empty file `BepInEx/nomirrors_debug.flag`. Every 3 seconds the plugin logs each mirror's position, normal and the
direction it reflects (also in the aircraft's frame), and saves what each mirror shows to `BepInEx/nomirrors_debug/*.png`
(probe mode: the six cube faces). Lines such as `fov=2.5`, `mode=Probe` or `cockpit=false` in the flag file change those
settings live. Delete the file when done.

## Roadmap

- Skip URP post-processing and shadows on mirror cameras for speed.
- Curved (true convex) glass in camera mode.
## Licence

MIT, see [LICENSE](LICENSE). Use it in your own mods, including bundled with them.
