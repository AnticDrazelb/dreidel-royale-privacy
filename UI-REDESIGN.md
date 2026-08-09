# Dreidel Royale — UI v2.0 ("Candlelight")

A full redesign of every surface **around** the game. The levels are untouched: the
`ENVS` table definitions, the dreidel skins, the WebGL diorama, the physics, the
networking, the audio and the AR pipeline are all the code that shipped. What changed
is the chrome — the design system, the navigation model, every screen, the HUD, the
menus, and the Android behaviours the shell never had.

File: **`dreidel-royale.html`** (single file, no build step, same two CDN dependencies).

---

## 1. A real token layer

Everything in the app now resolves to one set of custom properties in `:root` — no hex
is written twice.

| Group | What's in it |
|---|---|
| Colour | A 9-stop gelt-gold ramp, a 9-stop midnight-neutral ramp, four accents (flame, ember, indigo, mint, rose) |
| Surface | Five tonal elevations plus three translucent scrims for panels that float over the live canvas |
| Content | `--on-bg`, `--on-bg-muted`, `--on-bg-faint`, `--on-gold` — semantic, not literal |
| Shape | `--r-xs` 8 → `--r-2xl` 34 → `--r-full` |
| Space | 4dp base scale, `--s-1` … `--s-9` |
| Elevation | `--e-1` … `--e-5`, plus `--e-gold` for the primary action |
| Motion | The Material 3 easing set (`emphasized`, `emphasized-in/out`, `standard`, a spring) and four duration steps |
| Layout | `--content-w`, `--tap` (48dp floor), and the four `env(safe-area-inset-*)` values as tokens |

The old variable names (`--gold`, `--sub`, `--card-glass`, …) are kept as aliases onto
the new ramp, so the handful of inline styles inside the game script still resolve.

## 2. Navigation

The old build was a pile of absolutely-positioned screens with no back affordance at
all — on Android, the system Back gesture killed the app from any depth.

- Every secondary screen now has a **sticky top app bar**: back icon, title, optional
  actions. It picks up a surface and a hairline the moment content scrolls under it.
- Primary actions live in a **docked bottom action bar**, inside the safe area, always
  in the thumb zone.
- **The system Back gesture works.** `ui-shell` keeps exactly one spare history entry
  while you are anywhere but the landing screen; each Back press pops it, runs the app's
  own back action, and re-arms. Back closes a sheet, then steps a wizard back a page,
  then leaves the screen — and the moment it lands on the landing screen it stops
  re-arming, so the *next* Back leaves the app, exactly as Android users expect. Bounded
  at 3 history entries no matter how long you play.
- In play, Back opens the pause menu. On overlays that must not be escaped mid-flow
  (countdown, winner, reconnect) it is swallowed.

## 3. Screens

- **Landing** — a gradient-clipped wordmark over the live 3D table, one full-width
  primary action, then mode *cards* with icons and captions instead of a column of
  identical pills. Records and How-to-Play move to app-bar icon buttons. The resume
  card is a tonal row, not a button that looks like every other button.
- **Setup wizards** (Single Player, Pass & Play, Decision Dreidel) — a two-step flow
  with the step indicator in the app bar and a docked action bar whose contents swap
  with the step. Rules + ante collapse into an accordion whose header shows the live
  selection ("Rising · ante 1").
- **Lobby** — the room code becomes a hero panel with a live connection dot; share
  moves to an app-bar action.
- **Quick Match** — a radar of expanding rings around a gelt coin instead of a spinner.
- **Records** — a 4-up stat grid with a proper empty state.
- **Winner** — stronger scrim (the HUD no longer ghosts through), gradient headline,
  4-up stat grid, actions stacked by priority.

## 4. Components

- **Buttons**: five Material roles — filled, tonal, outlined, text, icon — all clearing
  48dp, all with a state layer on press and a **real ripple** seeded at the touch point
  by one delegated listener.
- **Segmented buttons**: the `.cpu-opt` squares became a connected track with equal
  segments and a filled selection. Opponents, difficulty, game style, ante and graphics
  all use it, so five controls that used to look like five different widgets now read
  as one.
- **Choice cards**: tables and dreidels get a selection ring plus a check badge, a
  desaturated (not blacked-out) swatch when locked, and the unlock progress bar pinned
  to the card foot so a row of bars actually lines up.
- **Text fields**: filled, 58dp, uppercase-tracked, with an external label and a 2px
  focus ring. The room code field is a display element in its own right and
  self-formats to four letters.
- **Switches**: Material 3 — the thumb grows and re-centres when it flips.
- **Snackbars**: a card with an accent bar, not a grey pill.

## 5. The HUD

- A floating app bar: **status pill on its own line**, menu button and pot flanking
  beneath it. Sharing one row (the old layout's shape) leaves ~180px for a line like
  "You — Hold to Spin", which clipped on every phone; a row of its own costs 44px and
  never truncates.
- Player rail with a soft masked bottom edge, so a clipped row reads as "scroll",
  not as damage.
- Pot module with the coin-pip stack retained; result card re-shaped and re-lit.
- The spin coin keeps its procedurally minted face and press physics untouched, on a
  slightly larger ring. `pathLength` is preserved, so the charge maths is unchanged.
- The whole HUD insets by the environment frame band **and** the system insets, on all
  four edges.

## 6. Menus as bottom sheets

Pause and How-to-Play are bottom sheets, not centre modals: thumb-reachable, dismissed
by scrim tap, by Back, or by dragging the grip down past 96px. Settings are grouped
list rows with icons; the destructive action is styled as destructive.

## 7. Android specifics

- Edge-to-edge with `env(safe-area-inset-*)` honoured on every edge of every surface.
- `theme-color` follows the surface actually on screen (including the flat-mode
  levels, which turn the status bar sky blue).
- 48dp minimum on every touchable; `overscroll-behavior: contain` on every scroller.
- Soft-keyboard handling: Enter does the obvious thing on each field, focus is dropped
  on screen change, and `visualViewport` resize keeps the focused field in view.
- A 24px inline SVG icon set replaces emoji and CSS-drawn glyphs in the chrome.
- `prefers-reduced-motion` disables ripples, throbs, shakes and every transition.
- Still **no `backdrop-filter` anywhere**: every panel floats over a canvas that
  redraws each frame, and blur forces a per-frame re-snapshot on a layer the compositor
  cannot cache. That was the original build's own finding and it still holds.

## 8. Level-themed chrome

The two level-driven chrome themes are preserved and rebuilt against the new
components:

- **Block mode** (Blocky Biome + Grass Block) — voxel glass panels, `Press Start 2P`,
  square corners, hard inset bevels, a grass-block spin button, a plank-and-dirt pause
  sheet.
- **Flat mode** (Backyard + Blue Pup) — storybook 2D: solid colours, one flat offset
  shadow, cream cards on a sky-blue sheet.

## 9. Changes inside the game script

Deliberately minimal, and all presentational:

- Six `style.display = 'inline-block' | 'block' | 'flex'` assignments became `''`, so
  the stylesheet owns layout instead of being overridden at runtime.
- Empty states and the player-remove button moved from inline styles to design-system
  classes (`.list-empty`, `.records-empty`, `.row-remove`).
- The segmented pickers stopped emitting inline `width` / `padding` / `font-family`.

No game logic, rules, RNG, networking, storage keys or unlock criteria were touched.

---

### Verified on

Rendered and exercised in Chromium (Pixel 8 UA, touch, 2× DPR) at 412×915, 360×640 and
844×390 landscape: every screen, both level chrome themes, the full Back-navigation
matrix, sheet drag/scrim dismissal, and a played CPU game. Zero console or page errors.
