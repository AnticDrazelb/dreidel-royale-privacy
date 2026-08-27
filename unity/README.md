# Dreidel Royale — Unity

A port of the Gelt Edition WebGL build (`Dreidel_Royale_v1_0_public_70.html`) to Unity,
aimed at fidelity: the geometry constants, animation curves, rules, unlock gates, palette
and sound design are quoted from the original rather than re-invented.

## Opening it

1. Open `unity/DreidelRoyale` with Unity **2021.3 LTS** or newer (built-in render pipeline).
2. Open `Assets/Scenes/Main.unity` and press Play.

The scene holds exactly one object, `Bootstrap`. Everything else — camera, lights, table,
dreidel, audio graph and UI — is constructed at runtime, because that is how the web build
works: every mesh, texture and sound is generated in code, and nothing ships as an asset.
The upside is that there is nothing to re-import and nothing that can drift out of sync with
the code describing it.

## How the port is laid out

| Path | What lives there |
| --- | --- |
| `Core/Consts.cs` | the four faces, their yaw angles, the chant, seat hues |
| `Core/Rules.cs` | rising / sudden death / classic, ante escalation, face resolution |
| `Core/Unlocks.cs` | every dreidel and table gate, with its own progress wording |
| `Core/EnvDefs.cs` | the six tables: fog, floor, lights, sky, weather, prop kit |
| `Core/CpuBrain.cs` | difficulty bands and the table-talk pools |
| `Core/GameController.cs` | the turn loop, the spin, the bots, the end of a game |
| `Visual/Geo.cs` | procedural meshes, including the body's swept rounded extrusion |
| `Visual/Canvas2D.cs` | a CPU raster surface with the slice of the canvas 2D API the art uses |
| `Visual/Tex.cs` | every texture, painted with the same passes as the originals |
| `Visual/DreidelRig.cs` | the dreidel: body, plaques, tip, handle, and the per-skin extras |
| `Visual/DreidelView.cs` | the animation state machine and the scene's per-frame life |
| `Visual/GeltSystem.cs` | the pot as physical objects — scatter, cleave, pay-in, fly-out |
| `Audio/Synth.cs` | a live synthesis graph mixed on the audio thread |
| `Audio/MusicEngine.cs` | the generative D freygish soundtrack |
| `UI/` | the screens, the HUD, and the screen-space effect layers |

## What carried over exactly

- **The dreidel.** Body edge 1.6, tip height 1.15, the rounded extrusion's bevel of 0.12 and
  corner radius 0.16, plaques at 0.812 sized 1.42, the handle and knob, the floating glyphs
  0.09 proud of each face. All fifteen skins, including the Menorah's four arm pairs, the
  Diamond's brilliant cut standing in for the knob, and the Oil Miracle's sloshing fill with
  its own depth gradient.
- **The spin.** `1080 + power*2160` degrees plus up to 720 of variance, a `2.2 + power*3.2`
  second sweep on the same easing, the launch spring, the precession walk's rosette, the 22%
  fake-out where the top nearly catches itself, and the irregular wooden clatter whose final
  knock — not a timer — fires the slam's consequences.
- **The rules.** Antes that rise every 5 rounds (3 in Sudden Death) and cap at 5; short stacks
  go all-in and only 0 gelt eliminates; the pot refills whenever it empties; classic rules keep
  the authentic single-coin *shtel*.
- **The sound.** The same voices, the same frequencies, the same envelopes — including the
  scrape whose pitch rides the dreidel's RPM and dies through the topple.

## What is not in this port

Called out plainly so nobody goes looking:

- **Online multiplayer.** The web build is peer-to-peer over PeerJS — host/join, room codes,
  quick match, heartbeats, reconnection and host migration. None of that has a drop-in Unity
  equivalent; it needs a transport decision (Netcode for GameObjects, Mirror, Photon) that
  should be made deliberately rather than inherited. Single Player, Pass & Play and Decision
  Dreidel are complete.
- **AR.** WebXR hit-testing and the camera-plus-gyroscope fallback are browser APIs. Unity's
  equivalent is AR Foundation, which is a different integration rather than a translation.
- **Sharing and the store bridge.** The Web Share invite and result cards, and the Play
  Billing hookup behind "Unlock Full Collection", are platform plumbing. The Full Collection
  entitlement is stored and honoured; the button grants it locally so the premium dreidels can
  be seen and played.
- **The WebGL survival machinery.** Context-loss recovery, renderer rebuilds and the graphics
  tier governor exist to keep a browser canvas alive. Unity has no analogue for them.

## One deliberate difference

three.js and Unity build identical rotation matrices, but their camera bases are mirrored: a
three.js camera looking down −Z has screen-right at world +X, and a Unity camera looking down
−Z has screen-right at world −X. Every position, angle and axis here is quoted verbatim from
the source, so the whole scene renders as its own mirror image — invisible on a four-fold
symmetric dreidel, a circular table and symmetric candles, but fatal for Hebrew glyphs. The
plaque quads flip their U coordinate to correct it (`Geo.PlaqueQuad`). That is one documented
compensation in one place, rather than a negated Z or yaw at forty call sites and no way to
read this code against the original.

## Type-checking outside the editor

`tools/stubs/` holds a minimal Unity API surface so the port can be compiled without the
editor — useful in CI, and how this port was checked as it was written:

```sh
mcs -target:library -out:/tmp/out.dll tools/stubs/*.cs $(find DreidelRoyale/Assets/Scripts -name '*.cs')
```

The stubs are a compile-time convenience only. They are not a runtime shim, and nothing in
`Assets/` references them.
