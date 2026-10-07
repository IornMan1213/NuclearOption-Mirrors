# NO Mirrors: working mirrors for Nuclear Option

A small BepInEx plugin that makes cockpit mirrors actually reflect. It is meant to be shared: any aircraft mod can add mirrors
without writing code, and other plugins can use it through a tiny API.

**Status: early (0.1.0).** The framework is in place; first in-game tuning is under way with the
[MiG-29 Fulcrum mod](https://github.com/IornMan1213/MiG29-NuclearOption).

## How it works

Each mirror gets its own camera. Every update it sits on the glass and looks along the pilot's line of sight reflected in the
mirror, with the field of view the glass covers from the pilot's eye. It renders into a texture that the glass then shows,
flipped left-right like a real mirror. Mirrors only render while they are on screen, at a capped rate (30 per second by
default), and only on the aircraft you are flying.

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
| `fov`X | field of view multiplier, above 1 for a wider, convex-mirror look | 1 |
| `near`X / `far`X | camera clip distances, metres | 0.05 / 3000 |
| `bright`X | reflection brightness | 0.9 |
| `noflip` | do not mirror the image (if your UVs already run right to left) | |

Example: `NOMirror_left_res384_fps45_fov1.3`.

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
- **General > Object name prefix**: `NOMirror` by default.
- **Quality > Resolution / Updates per second / View distance**: defaults for every mirror; name tokens override them.

## Install

Put `NOMirrors.dll` in `BepInEx/plugins/` (any subfolder). Requires BepInEx 5.

## Building

```bash
dotnet build src/NOMirrors -c Release
```

It builds against your installed game; pass `-p:GameDir="D:\Games\Nuclear Option"` if it is not in the default Steam folder.
No game files are in this repository.

## Roadmap

- True planar reflection (eye reflected through the mirror plane with an oblique near plane) as an option, for large mirrors.
- Skip URP post-processing and shadows on mirror cameras for speed.
- The MiG-29 Fulcrum mod's three canopy mirrors as the first user.

## Licence

MIT, see [LICENSE](LICENSE). Use it in your own mods, including bundled with them.
