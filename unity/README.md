# Dreidel Royale — Unity

A port of the Gelt Edition WebGL build (`Dreidel_Royale_v1_0_public_70.html`) to Unity,
aimed at fidelity: the geometry constants, animation curves, rules, unlock gates, palette
and sound design are quoted from the original rather than re-invented.

## Opening it

1. Open `unity/DreidelRoyale` with Unity **2021.3 LTS** or newer (built-in render pipeline).
2. Open `Assets/Scenes/Main.unity` and press Play.
3. For **Online** play only: link a project under *Edit → Project Settings → Services* and
   press **Create/Link**. It is free, it takes a minute, and Unity Relay reads the project id
   out of the build — without one, Online fails at the first step. *Same Wi-Fi* play,
   single-player and AR all work with no account at all.

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
| `Net/` | the wire protocol, the transport interface, Relay and LAN, chat |

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

Both routes ship enabled. The name screen carries an **Online / Same Wi-Fi** picker, remembered
between runs, and that choice is the only line in the game that knows which transport it is —
`NetUI.MakeTransport()`. Everything downstream reads `INetTransport`.

**Online** is Unity Relay (`Net/RelayTransport.cs`). The host asks for an allocation and gets a
join code back; guests dial that code and the relay carries the packets. Nobody opens a port,
neither phone learns the other's address, and it works over mobile data — which is what PeerJS
gave the web build. Traffic goes down a fragmenting, reliable, sequenced pipeline rather than
UTP's default unreliable one: a full table's `STATE_UPDATE` exceeds one datagram, and a dropped
turn change would desync the table.

It needs a **free Unity project linked** under *Edit → Project Settings → Services* — Relay
reads the project id out of the build. `Dreidel Royale → Validate build settings` checks for
one, and without it the game says so in a line a player can act on rather than failing silently.
There is no zero-setup route across the internet: every route needs a relay, and every free
relay needs an account with somebody.

**Same Wi-Fi** is TCP with a length-prefixed frame, and UDP broadcast to turn a four-letter room
code into an address. No account, no service, no key, no quota, nothing that can expire or be
rate-limited — and it is the case the game is actually for, since the original's own join screen
says *best played on Wi-Fi*, because a dreidel game is a room full of people. On a network that
blocks broadcast (guest Wi-Fi, AP isolation), a guest can type the host's IP instead of the
code; the lobby shows that address rather than leaving anyone to hunt for it. Typing an address
picks this route whatever the switch says — it cannot mean anything else.

**Quick Match is Wi-Fi only.** It asks the local network who is open, and only a local table can
answer that, so the picker is hidden on that path.

Everything that makes a party game survive a real room came across whole: seats are **held**
when someone drops rather than deleted, and rebound by a token minted at first join — a name
alone is guessable, so a stranger can't walk into a dropped player's chair. Guests heartbeat
every two seconds and the host force-closes a link that has gone silent for twelve, because a
mobile radio can leave a socket looking open for the best part of a minute while the turn sits
on a zombie. If the host vanishes, the table waits, then elects a new one — staggered by join
order so two claims don't race — and the old host may return only as an observer.

**The chair cannot move online.** A relay join code belongs to the host's own allocation, so a
new host would open a room under a code nobody else has any way of learning — and the link that
would have carried it is the one that just died. Host migration is therefore gated to Wi-Fi
tables, where the code is ours to re-mint. Reconnecting to the *same* online host works fine:
the join code stays valid as long as the allocation is alive.

`Net/RelayTransport.cs` is a plain class, not a MonoBehaviour: `NetManager` already calls
`Poll()` once a frame, and every asynchronous step — services init, anonymous sign-in,
allocation, join code, bind — is advanced from there as a small state machine under one 25s
clock. That keeps it interchangeable with `LanTransport` in `TransportFactory`, and keeps all
Unity API contact on the main thread without a lock.

## Store plumbing

The Full Collection is a real entitlement, so the button behind it does not grant one.
`Core/Iap.cs` is the seam: it asks the native side, and when there is no billing bridge it
says *"Purchases are available in the store version of the game"* rather than handing the
unlock out. Wire it up by supplying either half —

- **Android:** a class `com.dreidelroyale.Billing` with a static `instance()` and instance
  methods `buyFullCollection()` / `restorePurchases()`.
- **iOS:** the three functions in `Plugins/iOS/DreidelIap.mm`, and flip
  `_DreidelIapAvailable()` to return 1.

Either side reports success with `UnitySendMessage("Bootstrap", "OnPurchaseComplete", "")`
(or `"OnPurchaseRestored"`). Until then the entitlement is only reachable through the debug
code the web build also carried, which is gated on `Debug.isDebugBuild` — in a release build
typing it does nothing, so the purchase cannot be typed past.

The rating nudge (`Core/Rating.cs`) fires after the third win, once ever, 2.6s into the
celebration. iOS is wired for real through `SKStoreReviewController`; Android looks for a
`com.dreidelroyale.Review` wrapper around Play's In-App Review and stays silent without one.

Invite deep links are handled: a `?join=CODE` URL routes straight into the join flow with
the code prefilled, and is refused with an explanation if a game is already in progress.

## What is not in this port

Called out plainly so nobody goes looking:

- **The WebGL survival machinery.** Context-loss recovery, renderer rebuilds and the frame
  rate governor exist to keep a browser canvas alive. Unity has no analogue for them. The
  graphics *tier* they fed — Auto / High / Medium / Potato, in the pause menu — is ported:
  it moves render resolution, shadows and the ambient particle layer, and Auto is sticky per
  device. Frame rate is deliberately left alone, for the reason the original gives: fidelity
  should follow what the GPU can draw, pacing what the device can sustain.
- **The multi-tab guard.** Two browser tabs of the same game could collide over a peer id.
  An app has one instance.

## One deliberate difference

three.js and Unity build identical rotation matrices, but their camera bases are mirrored: a
three.js camera looking down −Z has screen-right at world +X, and a Unity camera looking down
−Z has screen-right at world −X. Every position, angle and axis here is quoted verbatim from
the source, so the whole scene renders as its own mirror image — invisible on a four-fold
symmetric dreidel, a circular table and symmetric candles, but fatal for Hebrew glyphs. The
plaque quads flip their U coordinate to correct it (`Geo.PlaqueQuad`). That is one documented
compensation in one place, rather than a negated Z or yaw at forty call sites and no way to
read this code against the original.

## Building for Android and iOS

Run **Dreidel Royale → Configure for Android and iOS** once from the menu bar. It sets the
things whose failure mode is silence, and **Validate build settings** re-checks them
afterwards (it also runs automatically at the end of Configure).

What it sets, and why each one bites:

| Setting | If it's wrong |
| --- | --- |
| Android graphics API pinned to **OpenGLES3** | ARCore will not start under Vulkan, and Unity's auto list picks Vulkan first on a modern phone. AR reports "unsupported" on a capable device, with nothing in the log. |
| Android **minSdk 24** | Below it, ARCore is unavailable. |
| **IL2CPP + ARM64** | Play requires a 64-bit binary. |
| iOS **camera usage description** | iOS does not warn when it's missing — it terminates the app the moment AR touches the camera. |
| **Linear** colour space | The gems, emissives and tone mapping are authored linear; gamma flattens the whole set. |
| **ARCore / ARKit loaders** ticked in XR Plug-in Management | The step everyone forgets. Builds and runs perfectly, just never finds a plane. |

Two permissions are merged into the generated projects at build time rather than checked in,
because a hand-written `AndroidManifest.xml` replaces Unity's (taking the launcher activity
with it) and the AR packages contribute manifest entries of their own:

- **Android** — `INTERNET`, `ACCESS_WIFI_STATE`, `CHANGE_WIFI_MULTICAST_STATE`. The last one
  is what allows the multicast lock; without it the Wi-Fi driver drops the discovery broadcast
  before the app sees it, and a host on the same network is simply never found.
- **iOS** — `NSCameraUsageDescription` and `NSLocalNetworkUsageDescription`. Without the
  second, iOS 14+ never shows the local-network prompt and discovery finds nothing, with no
  error to explain it.

### Why iPhone discovery does not use broadcast

iOS 14 requires the `com.apple.developer.networking.multicast` entitlement for raw broadcast
and multicast, and Apple grants it only on request. Rather than make shipping wait on a form,
discovery falls back to walking the local /24 with ordinary outbound TCP: a few hundred
connects with a short timeout, sixty-four at a time, settling in a couple of seconds. That
needs nothing beyond the local-network prompt the plist key already covers.

The host answers each probe with a greeting carrying its room code, which is also what lets a
direct-IP connection fail honestly when the code is wrong rather than seating someone at a
table they did not mean to join. On Android and desktop the broadcast path still runs first,
because it answers in milliseconds; the scan is only the fallback.

## Type-checking outside the editor

`tools/stubs/` holds a minimal Unity API surface so the port can be compiled without the
editor — useful in CI, and how this port was checked as it was written:

```sh
mcs -target:library -out:/tmp/out.dll tools/stubs/*.cs \
    $(find DreidelRoyale/Assets/Scripts -name '*.cs') DreidelRoyale/Assets/Editor/*.cs
```

The stubs are a compile-time convenience only. They are not a runtime shim, and nothing in
`Assets/` references them.
