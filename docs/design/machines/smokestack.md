# Smoke stack

**Status** live   **Mod** smex

**Owns** — the facts this page is canonical for:

* the stack's 72-cell layout: the census per glyph, the fixed 9 stacked brick courses, the exact set of bricks
  the `B` glyph accepts (and the one vanilla colour it excludes), and the 11-cell flue;
* the vent rate and everything the sink does with what it draws - the medium-read ordering, the plume colour
  rule (including the one medium that vents invisibly), and the draught sound;
* the fact that this block entity registers itself as a gas node by hand because it is a
  `BlockEntityMultiblockStructure`, not a `BlockEntityNetworkNode` - and every consequence of that, including
  the `Orientation` field that nothing ever populates;
* the stack's construction cost and its single grid recipe;
* the stack's relationship to the [hot blast furnace](blast-furnace-hot.md)'s exhaust budget (they are the same
  number), and to iiex's cheap one-block chimney vent (they are different mechanisms);
* the fact that the stacked-brick-course column is the only precedent in the codebase for a variable-height
  chimney - and that it is not, itself, variable.

**Does not own** — cited only, never restated:
[pipe network](../mechanics/pipe-network.md) (the graph substrate, `AddNode`/`RemoveNode`, node registration
rules, one-medium pools, `LitresPerPipe`, capacity, pressure, over-pressure burst, leaks, and the separate
`IPipeVentStrategy` seam) · [hot blast furnace](blast-furnace-hot.md) (the exhaust source, the two outlets,
`IsChoked` and what choking costs the furnace) · [cowper](cowper.md) (the machine upstream of the stack) ·
[heat balance](../mechanics/heat-balance.md) (`ExhaustVolumePerTick`, `ProductionTickMs`, the disruption model) ·
[multiblock](../mechanics/multiblock.md) (the layout DSL, origin-is-negation, structure completion, the build
outline) · [recipes & config](../mechanics/recipes-config.md) ·
[crucible furnace](crucible-furnace.md) (the designed variable stack, and the requirement that natural
draught become a function of stack height)

---

## Role

The exhaust system's safety valve. The [hot blast furnace](blast-furnace-hot.md) makes more exhaust than the
[cowpers](cowper.md) can swallow, and a backed-up exhaust main sets `IsChoked`, which is a disruption, and two
disruptions kill a furnace on the tick ([heat balance](../mechanics/heat-balance.md)). The stack draws gas off
the network every second and destroys it.

It is a pure sink with no state. Everything it holds is a display field: no temperature, no capacity, no cycle,
nothing to tend. The only decision the player makes is whether the stack is on the same network as the surplus.

Its rate is set to exactly match one furnace:

| | L/s |
|---|---|
| one hot blast furnace's exhaust (2 outlets) | 48 |
| one smoke stack | 48 |
| two cowper stoves (2 × 24) | 48 |

A plant with one furnace, two stoves and one stack therefore has twice the sink capacity it needs, which is the
margin that lets a stove sit idle mid-swap without choking the furnace.

### Not to be confused with the chimney vent

iiex ships a cheaper, unrelated mechanism: a vanilla chimney placed on the top connector of an
`IChimneyVentable` pipe fitting draws `ChimneyGasDrawRate` = 16 L/s out of the run
(`exlib …/Blocks/Networks/ChimneyVent.cs`; `IiexConfig.cs:237`). That is a network strategy injected into every
`"pipe"` network (`IronIndustryExpandedModSystem.cs:96`) and it never touches this block. The smoke stack is an
`IPipeNode` consumer; the chimney vent is an `IPipeVentStrategy`. Three stacked courses of the smoke stack cost
more than a vanilla chimney and vent three times as much.

---

## Structure

Anchor: `siex:smokestack-intake-{refractory}-{orientation}`, at the base front of the chimney. Layout authored
with `Origin(-1, 0)` - the negation of the `I` glyph's (col, row), per
[multiblock](../mechanics/multiblock.md).

| Source | Where |
|---|---|
| Definition (twelve cross-sections, y = −1 → y = 10) | `BlockSmokeStackIntake.cs:46-151` |
| Golden (the arbiter) | `test/SteelIndustryExpanded.Tests/goldens/siex/blocktypes/smokestack/intake.json` |
| Round-tripped copy for editing | [layouts.md](../../internal/workbench/layouts.md) § Section 2 |

### Cell census — 72 offsets

| Glyph | Required block | Count | file:line |
|---|---|---|---|
| `#` | `game:refractorybricks-good-tier*` (any tier) | 24 | BlockSmokeStackIntake.cs:48 |
| `I` | `siex:smokestack-intake*` (the anchor) | 1 | :49 |
| `a` | `game:air` - the flue | 11 | :50 |
| `B` | the brick-course alternation (below) | 36 | :51-54 |

Per layer: y−1 9 · y0 9 · y1 9 · y2…y10 5 each = 45.

### The two halves: a refractory base and nine brick courses

| Levels | Footprint | Content |
|---|---|---|
| y = −1 | solid 3 × 3 | 9 refractory bricks - the pad |
| y = 0 | 3 × 3 | the intake `I` at the origin, one flue cell, 7 refractory bricks |
| y = 1 | 3 × 3 | one flue cell, 8 refractory bricks |
| y = 2…10 | hollow plus, corners empty | 4 `B` per level around one flue cell - 9 courses |

The base is a squat refractory block three high; each course above it is a 1 × 1 flue ringed by four bricks.
The corners are `'.'` - not required - so the silhouette above the base is a cross, not a square.

The nine identical courses are the codebase's only worked example of "a chimney is a stack of repeated
courses", and [crucible furnace](crucible-furnace.md) needs exactly that shape with a player-chosen height
driving the natural-draught factor. This stack does not: its height is fixed by the layout at nine courses, no
more and no fewer, and the layout DSL has no way to express "N or more of these". See Open #1.

### What `B` accepts

```
@(claybricks-good-fire|refractorybricks-good-.*|brickcourse-.*-(black|brown|cream|gray|orange|red|tan))
```
(`BlockSmokeStackIntake.cs:51-54`.) Three families:

* `game:claybricks-good-fire` - vanilla fire brick;
* any `game:refractorybricks-good-*` - so the whole stack may be refractory;
* any vanilla `game:brickcourse` in seven of its eight colours. Vanilla's code is
  `brickcourse-{state}-{type}-{color}` with `state ∈ {four, eight}`, `type ∈ {header, headero, soldier,
  soldiero, running, runningo, runningl, runningr}` and `color` including `clinker`
  (`D:/Gaming/Others/Vintagestory/assets/survival/blocktypes/clay/brickcourse.json`).

`clinker` is the one colour excluded, and nothing says why. Every state and every bond pattern is accepted, so
the courses may be laid in any decorative bond: this is the one structure in the suite whose bulk is a cosmetic
choice.

### Orientation

`UpdateStructureRotation` reads the block's plain `orientation` variant through `ExOrientation.AngleFromSide`
(`BlockEntitySmokeStack.cs:145-154`) - the single-letter form `n|s|w|e` shares the side-angle convention. Unlike
the [cowper](cowper.md) there is no `+180`: structure-local `+Z` is the block's own back, so the flue rises
directly behind the intake and the pipe joins its front face.

`AllowedOrientations` and the fallback are derived from the def by the base `BlockPipe`
(`BlockSmokeStackIntake.cs:25-26`) - orientation states `[n, s, w, e]`, fallback `n`. No hand-written table.

### Interactive vs inert cells

| Cell | Interactive? | What the player does there |
|---|---|---|
| intake `I` | yes | Ctrl+Shift+RMB → build outline; HUD shows the litres drawn last tick |
| flue `a` ×11 | no | must stay air; the plume is drawn up this column |
| refractory `#` ×24, courses `B` ×36 | no | breaking any one drops `StructureComplete` and the draw stops dead |

---

## Assets

The smoke stack has no art of its own. Every visible block is vanilla brick except the intake, and the intake
borrows iiex's:

| Asset | Path | State |
|---|---|---|
| intake shape | `iiex:pipes/outlet` at `rotateY` 180/0/270/90 for n/s/w/e (`BlockSmokeStackIntake.cs:158-161`) | reused, not copied - the smoke stack ships no shape file; `assets/siex/shapes/smokestack/` does not exist |
| intake texture | `front1` → `game:block/clay/refractory/{refractory}/front1` (`:162`) | tier-tinted |
| the stack itself | vanilla `game:brickcourse-*` / `claybricks` / `refractorybricks` | the player picks |
| plume | `ExParticles.RisingPlume` over the flue column, coloured by medium (`BlockEntitySmokeStack.cs:204-234`) | live |

No editable source exists.

The intake is `sidesolid: false` (`:163`) where the [cowper](cowper.md)'s intake is `true` - the smoke stack's
anchor is a pipe fitting first and a structure anchor second.

Handbook: `assets/siex/config/handbook/02-hotblast.json` ↔ `docs/siex/handbook/02-hotblast.html`; the block
declares `Handbook("smokestack-intake-*")` (`:42`).

---

## Construction

One grid recipe, no RCC. `src/SteelIndustryExpanded/Recipes/Grid/SmokeStackRecipeDefinitions.cs`; golden
`test/SteelIndustryExpanded.Tests/goldens/siex/recipes/grid/smokestack.json`.

| Output | Pattern | Ingredients | file:line |
|---|---|---|---|
| `siex:smokestack-intake-{tier}-n` | `BHB,_P_,BNB` | 4 × `game:refractorybrick-fired-*` (tier captured as `{tier}`), 2 × nails, 1 × `iiex:pipe-cast-straight*`, hammer | :19-28 |

Authored with `.GridObject(...)` - a lone JSON object rather than an array - because it is the only recipe in
its file (`:19`, and the doc comment at `:8-11`). It is "four bricks deep for the taller column" against the
[cowper](cowper.md) intake's two, which is the only difference between the two recipes.

Whole-stack cost: 1 intake + 24 refractory bricks of any single tier + 36 brick-course blocks. The 36 courses
are the cheapest bulk in the suite: vanilla `brickcourse` is a decorative block, so a stack is essentially free
once the player has clay.

In the cost catalogue as `smokestack-intake-grid` (`SiexRecipeConfig.cs:63`), so `/exmod steel cheap` halves the
intake - but not the 60 bricks, which are structure, not recipe.

---

## Operation

### Inputs → outputs

| In | Out |
|---|---|
| whatever gas the connected pipe network holds - in practice exhaust from the [hot blast furnace](blast-furnace-hot.md) or a [cowper](cowper.md)'s spent-exhaust outlet | nothing. The gas is destroyed |
| — | a coloured plume up the flue + a throttled draught roar |

### The tick

`OnProductionTick` (`BlockEntitySmokeStack.cs:166-202`), once per second while `StructureComplete`:

1. read `SmokestackGasIntakeVolume` from live config (`:171`);
2. read `Medium` before drawing (`:175`) - `TryConsume` can empty the pool and clear its label, so reading
   after would lose the plume colour;
3. `TryConsume` up to that many litres from the network at its own position (`:176`, `:101-106`);
4. `MarkDirty` only when the drawn amount changed by more than 0.001 L (`:178-186`);
5. if nothing was drawn, stop - no particles, no sound (`:188-189`);
6. otherwise spawn the plume and play a throttled fire loop (6 s, 30 % volume, 32 m range, `:193-201`).

`TryConsume` returns what the network could actually give, so a nearly-empty main is emptied rather than driven
negative - pinned by `Blocks/SmokeStack/SmokeStackTests.cs:115-125`.

### The plume

`SpawnSmokeParticles` (`:204-234`) colours by medium through `ExParticles.GasColor(medium, ventAir: false)`
(`exlib …/Helpers/ExParticles.cs:109-112`):

| Medium | Plume |
|---|---|
| `"Exhaust"` | sooty - the normal case |
| `"Steam"`, `"Water"`, anything else | vapour |
| `"Air"` | none - `GasColor` returns `null` and the method returns early |

A stack venting plain air therefore shows nothing, which is what a player who has mis-plumbed their blast main
into the stack sees. The draw still happens.

The column is drawn over the cell in front of the stack - structure-local `(0, *, 1)` rotated by
`_currentAngle` (`:211-216`) - from `Pos.Y + 1` to `Pos.Y + 13`, i.e. up the flue and two blocks past the top
course. Quantity scales with the litres drawn: `15×` to `25×` `_lastConsumedAmount` particles per tick
(`:225-226`), so at the shipped 48 L/s a working stack emits 720–1200 particles a second.

### The player's verbs

| Gesture | Where | Effect |
|---|---|---|
| Ctrl+Shift+RMB | intake | build outline while incomplete |
| look at the intake | — | `Consuming {x} of Gas`, formatted by `ExMeasure.Volume` (`:247-252`) |
| plumb / unplumb the exhaust main | anywhere | the only control |

There is no valve, no throttle and no off switch on the block. Stopping a stack means breaking it or cutting
its pipe.

---

## Numbers

### Owned — `src/SteelIndustryExpanded/SiexConfig.cs`

| Key | Value | file:line | What it does |
|---|---|---|---|
| `SmokestackGasIntakeVolume` | 48 L/s | SiexConfig.cs:255 | Gas drawn off the network per production tick. Read fresh every tick (`BlockEntitySmokeStack.cs:171`), so `/exmod config smex` applies on the next second |

Pre-0.9.0 configs are force-reset by `SiexConfig.Migrations` (`SiexConfig.cs:34-46`), which retunes the key from
4 → 48 L/s: the value matches the furnace's two outlets, so a stale 4 would silently choke every furnace built
before that release.

### Owned, hard-coded — not config, source-only

| Constant | Value | file:line | What it does |
|---|---|---|---|
| dirty epsilon | `0.001f` | BlockEntitySmokeStack.cs:178 | Litres of change before the client is re-synced |
| plume column | local `(0, *, 1)`, `Pos.Y + 1` … `Pos.Y + 13` | :211-216 | Rotated by `_currentAngle` |
| plume density | `15×` … `25×` litres drawn | :225-226 | |
| plume life / gravity / size | `1.5 s`, `−0.1`, `0.5`…`1.5` | :227-229 | |
| draught-sound throttle | `6000` ms, `0.3f` vol, `32f` range | :193-201 | |
| `ventAir` | `false` | :207 | Air vents invisibly |
| connector fallback | `"n"` | :64 | Used when `Orientation` is null - which is always; see Gotchas #2 |
| inert defaults with no network | `20 °C`, `""`, `0` | :109-139 | Pinned by `SmokeStackTests.cs:45-56` |

### Cited — owned elsewhere

| Key / constant | Owner |
|---|---|
| `LitresPerPipe` (`ExpandedLib/ExlibConfig.cs:32`) - a main's per-node capacity | [pipe network](../mechanics/pipe-network.md) |
| `ChimneyGasDrawRate` = 16 L/s (`IiexConfig.cs:237`) - the other venting mechanism | [pipe network](../mechanics/pipe-network.md) |
| `ExhaustVolumePerTick`, `ProductionTickMs`, `IsChoked`, the disruption model | [heat balance](../mechanics/heat-balance.md) |
| `CowperIntakeVolume` = 24 L/s | [cowper](cowper.md) |

---

## Drops

| Broken block | Returns | file:line |
|---|---|---|
| intake | `siex:smokestack-intake-{tier}-n` - the fallback orientation, whatever it was facing; the brick tier is preserved | `exlib …/Blocks/Networks/BlockNetworkNode.cs:675-686` (inherited via `BlockPipePassthrough`) |
| refractory bricks, brick courses | vanilla | — |

`OnPickBlock` returns the same canonical variant (`BlockNetworkNode.cs:688-689`), so middle-clicking a
west-facing stack hands you a north-facing one.

The intake also carries `Behavior("Lockable")` (`BlockSmokeStackIntake.cs:154`) and `MaxStackSize(1)` (`:41`).

---

## Code

| Type / member | file:line | Notes |
|---|---|---|
| `BlockSmokeStackIntake` | `…/SmokeStack/Blocks/BlockSmokeStackIntake.cs:18` | `: BlockPipePassthrough` - an iiex fitting subclass. `Definitions` is `static new` (`:27`) so it replaces, not extends, the passthrough's own defs |
| ↳ `Definitions` | :27-165 | layout `:46-151`; variant groups `:155-157`; shapes `:158-161` |
| `BlockEntitySmokeStack` | `…/BlockEntities/BlockEntitySmokeStack.cs:23` | `BlockEntityMultiblockStructure` + `INetworkNode` + `IPipeNode` - the unusual combination this page exists to document |
| ↳ `Initialize` | :32-41 | `_system.AddNode(accessor, Pos, "pipe")` by hand, server-side, guarded on `GetNetworkAt(Pos) == null` |
| ↳ `OnBlockRemoved` | :43-49 | the matching `RemoveNode`, described in-source as a "safety fallback for chunk-unload edge cases" |
| ↳ `HasConnectorAt` | :63-64 | `face.Code.StartsWith(Orientation ?? "n")` |
| ↳ `OnLeak` / `OnOpenConnectorsChanged` / `OnNetworkUpdate` | :67, :70, :73 | all no-ops - the stack draws through `TryConsume`, it does not leak and caches nothing |
| ↳ `TryProduce` / `TryConsume` | :80-98 / :101-106 | `TryProduce` exists "for completeness" and is unused |
| ↳ `Temperature` / `Medium` / `IsLiquid` / `Pressure` / `Volume` / `MaxVolume` | :109-139 | all live reads of the network, not cached fields |
| ↳ `UpdateStructureRotation` | :145-154 | `AngleFromSide(Variant["orientation"])`, no `+180` |
| ↳ `OnProductionTick` | :166-202 | |
| ↳ `SpawnSmokeParticles` | :204-234 | |
| ↳ `ToTree` / `FromTree` | :259-282 | `_lastConsumedAmount`, `Orientation`, `PossibleOrientations` (the last via `JsonSerializer` + `ExTree.SafeDeserialize`) |
| `ChimneyVent` | `exlib …/Blocks/Networks/ChimneyVent.cs:20` | the other venting mechanism - a network strategy, not a block |

### Why the hand-rolled node registration matters

`BlockEntityNetworkNode` does `AddNode`/`RemoveNode` automatically, but this block entity extends
`BlockEntityMultiblockStructure` instead (it needs the footprint machinery), so it cannot inherit that. The
source says so at `:37-38`. Any multiblock that wants to be a graph node has to do the same, and must also
implement `INetworkNode` itself, since the framework's default `HasConnectorAt` (which delegates to the block)
is not available either.

### Where a caller hooks in

* A second sink - an economiser, a scrubber, a waste-heat boiler - implements `IPipeNode.TryConsume` and is
  placed on the exhaust main. Nothing about the stack is special; the furnace never looks for one.
* A different vent behaviour on a single block belongs in `IPipeVentStrategy`
  ([pipe network](../mechanics/pipe-network.md)), not here.
* A variable-height stack ([crucible furnace](crucible-furnace.md)) cannot reuse this layout - see Open #1.

### Tests

| File | Covers |
|---|---|
| `Blocks/SmokeStack/SmokeStackTests.cs:45-56` | an unbuilt, unwired node reports inert defaults |
| `…:58-71` | `HasConnectorAt` matches the orientation face (with `Orientation` set by hand - see Gotchas #2) |
| `…:77-113` | a built stack draws exactly one intake's worth; a stack with one brick pulled out draws nothing |
| `…:115-125` | the draw is capped at what the network holds - emptied, not driven negative |
| `…:131-148` | node state round-trips through the tree |
| `Scenarios/HotBlastScenarioTests.cs:44-79` | the safety-valve property: 12 ticks of a furnace spilling 38 L each keeps a 6-node main near empty with a stack, and pushes it over capacity without one |

`SmokeStackRig` (`test/SteelIndustryExpanded.Tests/Fixtures/SmokeStackScenes.cs:28-115`) raises the real 72-cell
chimney and lets the stack's own monitor tick complete it - per the megablock-rig rule, `StructureComplete` is
never forced.

---

## Gotchas

1. **The whole stack is load-bearing.** All 60 non-flue blocks are required, including the nine purely
   decorative course levels, so knocking one brick out of the top course stops the machine as surely as
   breaking the intake. `An_incomplete_stack_draws_nothing` (`SmokeStackTests.cs:89-113`) pulls exactly one
   brick to prove it. In a plant, a stray chisel or a mob's explosion 10 blocks up chokes the furnace with no
   error message anywhere near the furnace.

2. **`Orientation` on the block entity is never populated.** Both writers in the framework cast to
   `BlockEntityNetworkNode` (`exlib …/Blocks/Networks/BlockNetworkNode.cs:169-173`, `:820-839`) and this block
   entity is a `BlockEntityMultiblockStructure`, so neither assignment ever runs. The field stays `null`, is
   serialised as `null`, and `HasConnectorAt` falls back to `"n"` for every stack in the world regardless of
   how it was placed (`BlockEntitySmokeStack.cs:64`).

   Harmless today, because graph connectivity is decided by the block (`INetworkConnector` on
   `BlockNetworkNode`, which reads `Variant["orientation"]`), not by the block entity - `PipeNetwork` only ever
   calls the block's version (`exlib …/Networks/PipeNetwork.cs:598-607`). But the block entity's
   `HasConnectorAt` is live public API, both tests exercise it by setting `Orientation` by hand
   (`SmokeStackTests.cs:68`, `:136`), and the first caller that trusts it in-game gets a north-facing answer
   for a west-facing stack.

3. **The medium must be read before the draw.** `TryConsume` can empty the pool and clear its label, so
   `string medium = Medium;` sits one line above the draw (`:175-176`) with a comment saying why. Reordering
   those two lines silently turns every plume into vapour.

4. **A stack on an air line vents invisibly and silently-ish.** `GasColor(medium, ventAir: false)` returns
   `null` for `"Air"` (`exlib …/Helpers/ExParticles.cs:109-112`) and `SpawnSmokeParticles` returns before doing
   anything (`:207-208`) - but the sound still plays, because the throttled roar is outside that method
   (`:193-201`). So a mis-plumbed stack roars with no plume.

5. **`OnBlockRemoved` calls `RemoveNode`, and the comment says it is only a fallback.** "break-time
   `RemoveNode` is handled elsewhere" (`:44`). If that other path is ever removed, the stack becomes the one
   node that de-registers correctly by accident.

6. **`TryProduce` is dead code with a real implementation.** `:80-98` fully implements gas production into the
   stack's own network and is called by nothing (`:79` says so). A future bug that wires it up would make the
   safety valve a source.

7. **`brickcourse-*-clinker` is excluded from the `B` alternation** (`:53`) with no stated reason, while all
   seven other vanilla colours and all sixteen state/type combinations are accepted. A player building in
   clinker brick - a plausible choice for a chimney - gets a structure that will not complete and a build
   outline that highlights blocks they think they already placed.

8. **The intake's shape is `iiex:pipes/outlet`, not a smoke-stack shape.** Cosmetically the anchor is
   indistinguishable from a plain pipe outlet apart from its refractory tint; in a plant with a dozen outlets,
   finding the stack's control block means reading block names.

9. **The stack's rotation and the [cowper](cowper.md)'s differ by 180°.** The stack uses
   `AngleFromSide(orientation)` plain (`:150-152`); the cowper uses `+180` (`BlockEntityCowperStove.cs:77`).
   Two adjacent machines in the same plant, built from the same layout DSL, with opposite frames - copy an
   offset from one into the other and it lands on the far side.

10. **Nothing throttles the particle count.** 720–1200 particles per second per stack (`:225-226`) at the
    shipped 48 L/s, unconditionally, for any client in range. A four-furnace plant is four stacks doing that.

---

## Open

1. **The stack is the precedent for a variable-height chimney, and it is not variable.**
   [crucible furnace](crucible-furnace.md) requires that the natural-draught factor become a function of stack
   height, which needs a layout that accepts "N or more identical courses". This machine has nine identical
   courses hard-written as nine `.Layer(...)` calls (`:110-150`) and the DSL has no repetition or
   open-ended-column construct ([multiblock](../mechanics/multiblock.md)). Whoever builds the crucible furnace
   either extends the DSL or counts courses in C# outside the structure check - and if they do the latter, this
   stack should probably adopt it too, so that a taller chimney vents more.

2. **The vent rate is not a function of anything.** 48 L/s regardless of height, brick, weather or what the
   stack is venting. A stack that drew proportionally to its course count would make Open #1 pay for itself and
   give the nine decorative levels a reason to exist.

3. **There is no back-pressure and no failure mode.** The stack never refuses, never fills and never wears out;
   the only way it stops is structural damage. Whether the exhaust system should have any running cost is
   undecided - today the correct play is "build one stack, forget it exists".

4. **No test asserts the rate as a number in a plant context.** `SmokeStackTests.cs:86` asserts the draw equals
   `SiexValues.SmokestackGasIntakeVolume`, i.e. it checks the code against itself; the scenario test asserts
   only that the main stays "near empty". If the config drifted away from 2 × `ExhaustVolumePerTick`, nothing
   would fail.

5. **The handbook's stack paragraph is right and unusually specific** - "a smoke stack intake as its control
   block, 24 refractory bricks of any tier and 36 of any ordinary brick"
   (`siex:handbook-hotblast-text`) matches the golden exactly. It is the only part of the smex handbook that
   currently does; the rest of that page and all of `01-blastfurnace` do not (see
   [hot blast furnace](blast-furnace-hot.md) Gotchas #8).
