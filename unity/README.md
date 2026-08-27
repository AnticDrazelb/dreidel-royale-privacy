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
| `AR/` | session, placement, gestures, and the world-unit corrections |
| `Net/` | the wire protocol, the transport interface, LAN and Relay |

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

## AR

Requires AR Foundation, which is in `Packages/manifest.json` — ARCore on Android, ARKit on
iOS. Open the pause menu and tap **Play on your table**.

The world group is what moves. Every object, animation and game rule stays exactly as it is
and the diorama simply shrinks onto a surface the phone has found. Four things do not survive
a naive scale-down, and are corrected on every scale change:

| | why |
| --- | --- |
| shadow distance | a world-space camera range; the default cascade reaches far past anything that casts at 1/18th scale |
| point-light range | Unity does not scale a light's range with its transform, so a shrunk world turns every candle into a floodlight |
| near plane | 10cm is fine at desk scale, not when you lean in |
| particle size | absolute world units, so dust would render as 24cm clouds |

Tap to set the board down, drag to turn it, pinch to resize. By default the dreidel casts its
shadow straight onto your table; the brass gelt board is one tap away in the menu. The walk
drifts toward the table's front edge so the last seconds carry real is-it-going-over tension,
and the pot's outbound leg goes through the world rather than across the phone glass — real
coins leap off the stacks and arc toward you, and the HUD only takes delivery.

Because the phone is the camera and nothing may move it, the crane shot is replaced by the
**present beat**: after the clatter settles the dreidel rises, turns its winning face toward
the lens, holds, and settles back exactly where it fell. Cosmetic only — the result is decided
by the landing yaw, never read back off the geometry.

Screen shake is suppressed in AR, where it would tear the board off the table.

## Multiplayer

Host authority, ported message-for-message from the web build: the host owns the state,
resolves every spin once, and broadcasts an already-decided landing. That is why two phones
never disagree about which letter came up, and why a guest's phone can be slow without
changing the outcome.

The protocol is transport-agnostic — LAN sockets and a relay differ in how bytes reach the
other phone, not in what the table does with them — so `INetTransport` is all the game layer
ever sees.

**What ships: local Wi-Fi.** TCP with a length-prefixed frame, and UDP broadcast to turn a
four-letter room code into an address. No account, no service, no key, no quota, nothing that
can expire or be rate-limited. This is also the case the game is actually for — the original's
own join screen says *best played on Wi-Fi*, because a dreidel game is a room full of people.
On a network that blocks broadcast (guest Wi-Fi, AP isolation), a guest can type the host's IP
instead of the code; the lobby shows that address rather than leaving anyone to hunt for it.

Everything that makes a party game survive a real room came across whole: seats are **held**
when someone drops rather than deleted, and rebound by a token minted at first join — a name
alone is guessable, so a stranger can't walk into a dropped player's chair. Guests heartbeat
every two seconds and the host force-closes a link that has gone silent for twelve, because a
mobile radio can leave a socket looking open for the best part of a minute while the turn sits
on a zombie. If the host vanishes, the table waits, then elects a new one — staggered by join
order so two claims don't race — and the old host may return only as an observer.

**Internet play** is Unity Relay, implemented in `Net/RelayTransport.cs` behind
`DREIDEL_RELAY`. It is off by default because it needs packages this project does not ship and
a free Unity project of your own. To turn it on:

1. Add `com.unity.services.core`, `com.unity.services.authentication`,
   `com.unity.services.relay` and `com.unity.transport` in Package Manager.
2. Link a Unity project under **Edit → Project Settings → Services**.
3. Add `DREIDEL_RELAY` to **Player → Scripting Define Symbols**, and hand `NetManager` a
   `RelayTransport` in place of `LanTransport`.

There is no zero-setup option for internet play: every route across the internet needs a
relay, and every free relay needs an account with somebody. LAN is the one that needs nothing,
which is why it is the one that ships enabled.

## What is not in this port

Called out plainly so nobody goes looking:

- **Quick Match.** The web build advertises open tables through a set of well-known broker
  slots. That is a PeerJS-specific trick with no LAN equivalent; discovery by room code covers
  the same ground on a local network.
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
