# Changelog

## 0.0.2
- Camera mode is now a true planar reflection: the camera sits at the eye mirrored through the glass, with an off-axis frustum
  whose near plane is the glass itself. Mirrors show exactly what real glass would, follow the head, and clip nothing in front of
  the glass. Fixes mirrors pointing the wrong way and showing only sky (0.0.1).
- The texture's orientation on the glass is worked out from the glass's own axes (no more guessing a flip).
- New *Field of view* setting (and the `fov` token now means the same thing): above 1 makes mirrors convex, for a wider view.
- New *Reflect the cockpit* setting: mirrors also show the cockpit interior that the game draws with its own camera.
- New Probe mode: one shared reflection cube at the eye, looked up per pixel on the glass (cheap, soft). Convex fields bend the
  glass's normals. Camera mode stays the default.
- Fixed mirrors flickering with patches of the wrong (flipped) picture when *Reflect the cockpit* was on: a mirror camera could
  draw its own glass at its near plane. Mirror glass and the aircraft's see-through parts are now hidden from mirror cameras, and
  mirrors render one per frame, by hand (cheaper too). Mirror cameras skip post-processing, AA and screen copies.
- Developer aid: create `BepInEx/nomirrors_debug.flag` to log each mirror's frame and save what it shows as PNGs.

## 0.0.1
- First version: mirror component (camera on the glass, reflected view, field of view from the pilot's eye), name-based no-code
  setup with setting tokens, `MirrorSystem` API, player config.
