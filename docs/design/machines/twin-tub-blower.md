# Twin-Tub Blower
**Status** live (simulation complete; recipe landed 2026-08-04)   **Mod** iiex

**Owns**
- The blower's output rate and pressure ceiling, and the speed-response curve that scales both.
- The gate arithmetic: where the ceiling sits relative to charge demand and plated-pipe burst. That ordering,
  not a rule anywhere in code, is what separates the iron tier from the steam tier.
- The blower as a pipe node that generates (not a machine reaching into a neighbouring network), and its
  cold-blast (ambient-temperature) output.
- Its 1 × 2 × 3 footprint, its MP port cell, and its hand-composed `IFillerHost` (it cannot derive
  `BlockFilledMegastructure`).

**Does not own** - cited only, never restated:
- The pipe graph, pools, one-medium rule, capacity, burst-by-tier, joints, valves, leaks, the tick order -
  [pipe network](../mechanics/pipe-network.md).
- `RequiredBlastPressureFor` / `TuyereDrawFor`, the coke→demand mapping, and every `Bf*BlastPressure*`
  constant - [heat balance](../mechanics/heat-balance.md), § Blast demand.
- The MP network the axle rides, `BEBehaviorMPFillerPort`, speed/angle semantics -
  [mp-energy](../mechanics/mp-energy.md).
- Fillers, footprints, the filler-can-never-be-a-graph-node rule - [multiblock](../mechanics/multiblock.md).
- Code-first defs, recipes, cost catalogue - [recipes-config](../mechanics/recipes-config.md).
- Hot blast and preheating - [cowper](cowper.md). Steam-age blowers - [engine-watt](engine-watt.md).

---

## Role

The iron tier's only air source. Every other blower in the suite is a steam sub-machine; this one needs
nothing but an axle, so an iron-age furnace can be blown with a vanilla waterwheel or windmill long before a
boiler exists.

It is also the tier gate, and it gates without a single branch on tier anywhere in the code. Blast demand is
a property of the charge column's carbon fraction, not of the furnace
([heat balance](../mechanics/heat-balance.md)): the fuel bands are the permeable skeleton of the column, so a
carbon-rich column blows easily but eats fuel and drinks air, while a carbon-lean one packs dense and needs
far more pressure. The blower's ceiling sits above what a rich charge asks, below what a lean one asks, and
below what plated pipe can hold.

Consequence: bellows run an iron furnace all day on a fuel-hungry charge and can never run the fuel-efficient
charge. That one needs pressure only a steam blower raises, carried in pipe only the steam tier can build.
Air goes in at ambient, so this is cold blast by construction; preheating needs a cowper, which needs steam.

---

## Structure

A megablock and a pipe node at the same time. It cannot derive `BlockFilledMegastructure` (that base is a
plain `Block` and the blower must be a `BlockPipe`), so it implements `IFillerHost` and drives the
`StructureFillers` statics from its own placement triad (`BlockTwinTubMPBlower.cs:19-25`, `111-151`). Without
that, `fillerOffsets` would be inert JSON: no fillers, and no MP port cell to be driven through.

| | |
|---|---|
| Footprint | 1 × 2 × 3 (X × Y × Z) in the north frame - 6 cells, 5 fillers |
| Layout | `Origin(0, 1)`, slice `"M##"` / `"0##"` (`BlockTwinTubMPBlower.cs:67-82`) |
| Principal | `(0,0,0)` - the `'0'` glyph, bottom-front |
| MP port cell | `'M'` at `(0,+1,0)` - hosts `exlib.BEBehaviorMPFillerPort` facing (rotation-relative) west, `allowAttach: true` (`BlockTwinTubMPBlower.cs:36-37`, `69`) |
| Bellows housing | `(0,1,1)`, `(0,1,2)`, `(0,0,1)`, `(0,0,2)` - plain fillers |
| Orientation | pipe-fitting convention: `type` + `orientation` (`n`/`e`/`s`/`w`) variant pair (`:58-59`) |
| `StructureAngle` | n 0 · e 90 · s 180 · w 270 (`BlockTwinTubMPBlower.cs:99-106`) |

Rows in the slice run −Y (top row is y = 1); columns run +Z (z = 0..2). The block entity rotates its port
lookup by the same `StructureAngle` (`BlockEntityTwinTubMPBlower.cs:57-58`, `155-167`), so footprint and port
can never disagree.

### The network connector

The principal is the only graph node: the blower's footprint cells declare no membership, and a cell that
declares none is not a node ([multiblock](../mechanics/multiblock.md), § A filler cell is a graph node when
it declares one). So the blast main must butt
against the principal cell, on the single face named by the `orientation` variant: `GetConnectorFaces` maps
each character of `Orientation` through `SideToFace` (`BlockNetworkNode.cs:784-793`,
`BlockNetworkModSystem.cs:458-468`), and the blower's orientation string is one character. A `-n` blower
presents exactly one connector, on its NORTH face.

Caution: the source comment at `BlockTwinTubMPBlower.cs:78-79` says the housing's "far end (y=0,z=2) is what
the blast main butts against". That cell is a filler and cannot connect, and it is on the opposite side from
the `-n` connector face. See [Gotchas](#gotchas).

Joint family is the shared flanged joint registered for the iiex domain
(`IronIndustryExpandedModSystem.cs:75`), so the blower couples to plated, cast and rolled segments alike; the
joint rule is per-tier and owned by [pipe network](../mechanics/pipe-network.md).

The blower also carries the shared `MultiblockStructure` behaviour (`:51`) for uniformity. It is not a cell of
any furnace layout, so the layout-ownership resolver finds no anchor and the projection gesture does nothing
(the documented "no resolvable anchor → does nothing" contract).

---

## Assets

| Asset | Path | State |
|---|---|---|
| Editable shape | — | missing. No source file under `workbench/shapes/` (the one that looks close, `machine-pipe-block-engine-airblower.json`, is smex's engine blower) |
| Runtime shape | `mods/iiex/assets/iiex/shapes/furnaces/twintubmpblower.json` | present - elements `Base`, `BaseExtention`, `BaseBeam`, `AxleGear`, `Tubs`, `PipeConn` |
| Animations | same file | `cycle` (60 f, EaseOut) · `idle` (30 f, EaseOut) - authored, wired to nothing |
| Textures | `iron3`, `iron2`, `iron`, `wood-generic` | declared in the shape |

Caution: the bellows never move. The block entity has no animator: no `Animatable` entity behaviour in the
def (`BlockTwinTubMPBlower.cs:44-84`), no `ConstructedAnimator`, no `MPAnim.AdvanceFrame`, no `IRenderer`.
The shape ships a full `cycle` clip and an `AxleGear` element positioned to be driven, and neither is ever
posed. The axle phase-lock pattern to copy is `MPAnim.AdvanceFrame` (`mods/exlib/src/Helpers/MPAnim.cs`),
which iiex's engine drives from its render loop (`BlockEntityEngine.cs`). The only feedback a running blower
gives is the HUD line (`iiex:blower-info-blowing`) and the pipe readout above it.

---

## Construction

A grid recipe (landed 2026-08-04, the "Twin Tub Blower" recipe in `FurnaceRecipeDefinitions.cs:87-103`):
pattern `LPL,PNP,_H_` - leather (`game:leather-normal-plain`; the recipe's own comment records why the
concrete code matters, a bare `leather` is never a registered item), planks, nails and a hammer, out to
`iiex:furnace-twintubblower-n`. Nothing in the recipe comes from iiex: the tier's only air source must be
buildable before anything the tier produces, and it is driven by a plain vanilla wood axle. The tuyere
recipe (B1) is fixed too, so the iron-tier air path - blower, main, tuyere - is buildable end to end.

In the recipe-cost catalogue as `twintubblower-grid` (`IiexRecipeConfig.cs:92`).

---

## Operation

```
axle (waterwheel / windmill / later a steam engine)
      │  couples on the MP port cell (0,+1,0), west face + its opposite
      ▼
  speed s  →  fraction f = clamp((s − 0.5) / 1.0)
      │
      ▼
  45 × f  L/s of "Air" at ambient temperature, once per second,
  injected into the blower's OWN pipe network, clamped at 2.2 atm
```

### The tick - `OnBlowTick`, `BlockEntityTwinTubMPBlower.cs:94-103`

Once per second, server-side (`:65-66`):

1. `PortSpeed()` reads the hosted `BEBehaviorMPFillerPort` at the rotated port cell; `0` if no axle is
   coupled or it is not turning (`:155-167`).
2. If the speed changed, cache it and `MarkDirty()` - the client cannot see the port's live state, so the
   HUD reads the synced `_lastSpeed` (`:53-56`, `:97-101`).
3. `ProduceAir(speed, dt)`.

### `ProduceAir` - `BlockEntityTwinTubMPBlower.cs:111-132`

```csharp
float fraction = SpeedFraction(speed);
if (fraction <= 0f || dt <= 0f) return 0f;
if (NetworkSystem?.GetNetworkAt(Pos) is not PipeNetwork net) return 0f;
float before = net.State?.Volume ?? 0f;
net.TryProduceGas(TwinTubBlowerOutputPerSecond * fraction * dt,
                  AmbientTemperature, "Air", accessor,
                  maxOutputPressure: TwinTubBlowerMaxPressure);
return max(0, (net.State?.Volume ?? 0) - before);
```

It produces into its own network - the same way the fluid intake does for water - rather than reaching into
a neighbour. `TryProduceGas` reports only whether it accepted anything and clamps at the ceiling, so the
litres that actually landed are the change in the pool.

`AmbientTemperature` is the live climate temperature at the block, falling back to 20 °C where no climate is
available (`:135-136`). That is the whole of "cold blast"; there is no flag.

`SpeedFraction` is `public static` (`:143-152`) so the balance can be asserted without standing up a
mechanical network.

### HUD - `GetBlockInfo`, `:184-199`

The pipe readout first (medium, throughput, pressure - owned by
[pipe network](../mechanics/pipe-network.md)), then either `iiex:blower-info-idle`
("Bellows idle - no axle turning") or `iiex:blower-info-blowing` with the live flow rate formatted through
`ExMeasure.FlowRate` (`ExMeasure.cs:81`) and the percentage of rated output.

---

## Numbers

### Config - `mods/iiex/src/IiexConfig.cs`, `ModConfig/ex_values.json`, domain `iiex`

| key | value | file:line | what it does |
|---|---|---|---|
| `TwinTubBlowerOutputPerSecond` | `45 L/s` | `IiexConfig.cs:808` | air delivered at full axle speed |
| `TwinTubBlowerMaxPressure` | `2.2 atm` | `IiexConfig.cs:816` | pressure ceiling this producer will raise its run to - the gate number |
| `TwinTubBlowerMinSpeed` | `0.5` | `IiexConfig.cs:819` | at/below this the bellows deliver nothing |
| `TwinTubBlowerMaxSpeed` | `1.5` | `IiexConfig.cs:822` | at/above this the bellows deliver full output |

### HARD-CODED - not config

| value | file:line | what it does |
|---|---|---|
| tick period `1000 ms` | `BlockEntityTwinTubMPBlower.cs:66` | one blow per second; chosen to match the network's own per-second tick |
| gas type string `"Air"` | `BlockEntityTwinTubMPBlower.cs:128` | the medium label; a literal, not a taxonomy lookup |
| ambient fallback `20 °C` | `BlockEntityTwinTubMPBlower.cs:136` | when no climate is available |
| `MpPortCell = (0,1,0)` | `BlockEntityTwinTubMPBlower.cs:49` | which footprint cell the BE polls for axle speed |
| port face `"west"` | `BlockTwinTubMPBlower.cs:37` | plus its opposite, coupled by the port behaviour at `Initialize` |
| footprint `M## / 0##`, `Origin(0,1)` | `BlockTwinTubMPBlower.cs:71-81` | the 1 × 2 × 3 volume |
| `StructureAngle` table (n0/e90/s180/w270) | `BlockTwinTubMPBlower.cs:99-106` | must match the per-orientation `rotateY` at `:62-65` |

### Cited, owned elsewhere - the four numbers that make the gate

This page owns one row of this table (the ceiling); every other value is quoted with its owner so the
ordering can be checked in one place.

| quantity | value | owner | file:line |
|---|---|---|---|
| rich charge (30 % carbon) demands | 1.25 atm | [heat balance](../mechanics/heat-balance.md) | derived from `IiexConfig.cs:178`, `:185` |
| standard charge (20 % carbon) demands | 2.0 atm | [heat balance](../mechanics/heat-balance.md) | `IiexConfig.cs:178` (`BfBlastPressureAtReference`) |
| blower ceiling | 2.2 atm | this page | `IiexConfig.cs:816` |
| plated pipe burst | 2.5 atm | [pipe network](../mechanics/pipe-network.md) | `IiexConfig.cs:214` |
| lean charge (10 % carbon) demands | 2.75 atm | [heat balance](../mechanics/heat-balance.md) | derived, same two keys |

```
1.25  <  2.0  <  2.2  <  2.5  <  2.75
rich    std   BLOWER  burst   lean
```

Both halves of the gate hold, and they hold independently:

- the blower cannot raise a lean charge's pressure (2.2 < 2.75), and
- even a stronger iron-tier blower could not, because the plated main itself bursts first (2.5 < 2.75).

`TryProduceGas` takes `min(maxOutputPressure, MinBurstPressure(...))` (`PipeNetwork.cs:116-125`), so the
blower's own clamp is redundant with the pipe's and correct in either order.

The real gate line is not 20 % carbon. Demanded pressure is
`BfBlastPressureAtReference + (BfReferenceFuelFrac − f) × BfBlastPressureCokeSensitivity`, clamped to
`[BfBlastPressureMin, BfBlastPressureMax]` (`BlockEntityFurnaceCore.RequiredBlastPressureFor`; keys at
`IiexConfig.cs:178`, `:185`, `:188`, `:191`, `:264`), where f is the charge column's carbon fraction - fuel
units weighted by `CarbonPerUnit` (coke 1.0, charcoal 0.5) over total charge units
(`BlockEntityShaftFurnace.Accumulate`). Solving `2.0 + (0.20 − f) × 7.5 = 2.2` gives f ≈ 0.173: the bellows
run any charge at or above ~17.3 % carbon. In courses, that is coke at ≥ ~17.3 % of the column by volume, or
charcoal at roughly twice that share - a column 30 % charcoal by volume reads ≈ 17.6 % carbon and sits just
above the line.

### The air-volume gate

| quantity | value | from |
|---|---|---|
| reference tuyere draw | `14 L/s` | `IiexConfig.cs:582` ([heat balance](../mechanics/heat-balance.md)) |
| rich-charge draw factor | `clamp(0.30/0.20, 0.4, 1.8) = 1.5` | `IiexConfig.cs:264`, `:194`, `:197` |
| per-tuyere draw, rich charge | `21 L/s` | derived |
| two-tuyere furnace, rich charge | 42 L/s | derived |
| blower output | 45 L/s | `IiexConfig.cs:808` - this page |

45 ≥ 42, so one blower runs one blast furnace on its thirstiest charge, with 7 % headroom. A carbon-rich
charge is the highest air demand the iron tier ever has to meet (a leaner one burns less coke, so draws less
air). Asserted by
`TwinTubBlowerTests.Full_output_covers_a_two_tuyere_furnace_on_its_thirstiest_burden`.

### Speed response

`SpeedFraction(s)` = `0` at `s ≤ 0.5`, `1` at `s ≥ 1.5`, linear between (`:143-152`).

| axle speed | fraction | output |
|---|---|---|
| 0.0 | 0 | 0 L/s |
| 0.5 | 0 | 0 L/s |
| 1.0 | 0.5 | 22.5 L/s |
| 1.5 | 1.0 | 45 L/s |
| 4.0 | 1.0 (capped) | 45 L/s |

A vanilla waterwheel runs at roughly speed 1, i.e. half rated output, 22.5 L/s, which is below the 42 L/s a
rich-charged two-tuyere furnace draws. Reaching rated output takes gearing or a second wheel.

---

## Drops

Plain block drops - the def sets neither `NoDrops()` nor a `GetDrops` override
(`BlockTwinTubMPBlower.cs:44-84`), so breaking returns one `iiex:twintubmpblower` item (`MaxStackSize` is
unset; the pipe `Common` surface is not applied to this def).

`OnBlockBroken` clears the reserved filler volume before calling base, so no invisible solid cells are left
behind (`BlockTwinTubMPBlower.cs:141-151`). It holds no inventory, so nothing else is lost.

Placement is refused unless the whole 1 × 2 × 3 volume is clear (`failureCode = "notenoughspace"`,
`:123-128`); without that guard the fillers would fail to spawn and the blower would stand with no MP port
cell.

---

## Code

| Piece | file:line |
|---|---|
| `BlockTwinTubMPBlower : BlockPipe, IExBlockDefProvider, IFillerHost` | `BlockStructures/Furnaces/Blocks/BlockTwinTubMPBlower.cs:28` |
| `Definitions` (code-first blocktype + footprint) | `BlockTwinTubMPBlower.cs:44-84` |
| `StructureAngle` | `BlockTwinTubMPBlower.cs:99-106` |
| `CanPlaceBlock` / `OnBlockPlaced` / `OnBlockBroken` - the hand-rolled filler triad | `BlockTwinTubMPBlower.cs:111-151` |
| `BlockEntityTwinTubMPBlower : BlockEntityPipe` | `BlockStructures/Furnaces/BlockEntities/BlockEntityTwinTubMPBlower.cs:42` |
| `OnBlowTick` | `BlockEntityTwinTubMPBlower.cs:94-103` |
| `ProduceAir` (public - the balance entry point) | `BlockEntityTwinTubMPBlower.cs:111-132` |
| `AmbientTemperature` | `BlockEntityTwinTubMPBlower.cs:135-136` |
| `SpeedFraction` (public static) | `BlockEntityTwinTubMPBlower.cs:143-152` |
| `PortSpeed` | `BlockEntityTwinTubMPBlower.cs:155-167` |
| `GetBlockInfo` | `BlockEntityTwinTubMPBlower.cs:184-199` |
| `PipeNetwork.TryProduceGas` (the ceiling clamp) | `ExpandedLib/Networks/PipeNetwork.cs:96`, `:116-125` |
| burst / joint registration for the iiex domain | `IronIndustryExpandedModSystem.cs:76-77` |

### Where a caller hooks in

- Driving it without a world: call `ProduceAir(speed, dt)` directly; it is public for exactly that
  (`:107-110`).
- Changing the response curve: `SpeedFraction` is the single place; it reads live config, so a retune applies
  without a reload (asserted by `A_retuned_speed_band_moves_the_response_with_it`).
- Adding a second air source: produce into the network with `maxOutputPressure` set to that device's ceiling.
  Producers do not coordinate - the highest ceiling present wins, so a steam blower on the same main lifts
  the whole run past 2.2 atm and the bellows simply stop accepting (their clamp is below the pool pressure).
- Consuming the air: the tuyere draws through `TryConsumeGas` and tests pressure - see
  [heat balance](../mechanics/heat-balance.md) and blocker B11 below.

### Tests

`mods/iiex/tests/Blocks/Furnaces/TwinTubBlowerTests.cs` - four regions:

| region | what it pins |
|---|---|
| Tier balance | the four-number ordering, both halves of the gate, the 45 ≥ 42 air budget |
| Charge-derived demand | a leaner (carbon-poor) charge demands more pressure and draws less air; an unstamped charge reads as the standard mix |
| Speed response | the five-point linear curve, and that a retuned band moves with it |
| Production / Footprint | produces into its own net, never exceeds the ceiling, 5 filler cells with the port at `(0,1,0)` facing west |

> The ascending pipe-tier ordering (plated < cast < rolled) is asserted in the iiex suite - iiex cannot see
> iiex's config.

---

## Gotchas

1. The bellows are static art. `cycle` and `idle` clips exist in the shape and nothing plays them (no
   `Animatable`, no animator). A running blower is visually indistinguishable from an idle one.

2. Stale comment about where the main connects. `BlockTwinTubMPBlower.cs:78-79` says the housing's far end at
   `(y=0, z=2)` "is what the blast main butts against". It is a filler cell that declares no membership, so it
   is no node, and the actual connector is the principal's single orientation-named face - which for `-n`
   points the opposite way from the housing. Anyone plumbing from that comment will build a main that never
   joins.

3. `ProduceAir` re-implements `ProduceGasMeasured`. The before/after volume diff at
   `BlockEntityTwinTubMPBlower.cs:124-131` is exactly what `PipeNetwork.ProduceGasMeasured`
   (`PipeNetwork.cs:176-192`) exists to do, and that method is documented as "the canonical call for producers
   that need the accepted volume". Duplicated logic that can drift.

4. The tick-order assumption is not enforced. `Initialize` comments that "the network's own tick is also per
   second, so the air is produced and then distributed in the same beat" (`:63-65`). Nothing orders the two
   listeners; if the network ticks first, the air produced this second is distributed next second. Combined
   with B11 (air consumed before the pressure test) this is worth checking before trusting any starvation
   observation.

5. A leaking run silently caps the blower at 1 atm. `TryProduceGas` clamps `ceilingPressure` to 1 whenever
   `State.IsLeaking` and `bypassLeakCap` is false (`PipeNetwork.cs:122-124`); the blower never passes
   `bypassLeakCap`. One open connector anywhere on the main therefore drops the whole line below every
   charge's demand, with the blower still reporting "blowing 45 L/s (100 % of rated output)" - the HUD shows
   the rated figure, never the accepted one (`:193-197`).

6. The HUD reports intent, not delivery. `iiex:blower-info-blowing` formats
   `TwinTubBlowerOutputPerSecond × fraction` (`:195`), which is what was offered. `ProduceAir` computes what
   was accepted and throws it away (`:131`). A blower against the ceiling reads as fully working.

7. `"Air"` is a bare string literal (`:128`), matched against the medium taxonomy by name. It is not pulled
   from `ExLiquids` / the medium catalogue.

8. The port face is `west`, but west + east both couple. `BEBehaviorMPFillerPort.Initialize` also connects
   the opposite face so a row of ports merges into one line. Documenting the blower as "west-driven"
   (`BlockTwinTubMPBlower.cs:33-34`, `BlockEntityTwinTubMPBlower.cs:46-48`) undersells it - an axle on either
   side of the rotated port cell drives the bellows.

9. `StructureAngle` and the shape's `rotateY` table are two independent statements of the same fact (`:62-65`
   vs `:99-106`). They are asserted equal by `The_structure_angle_follows_the_orientation_variant`, but
   nothing enforces it at compile time.

10. The blower carries `MultiblockStructure` for uniformity and it does nothing. Documented at `:52-55`; do
    not "fix" the missing anchor.

11. `RegisterBurst` is keyed by domain, not by block (`IronIndustryExpandedModSystem.cs:76`). The blower is an
    `iiex` pipe block, so it presents plated-pipe burst as a node of the run too - putting a blower in a
    cast-pipe run lowers that run's weakest-link burst to 2.5.

---

## Open

- Wire the animation. The clips and the `AxleGear` element already exist; `MPAnim.AdvanceFrame`
  (`mods/exlib/src/Helpers/MPAnim.cs`, driven as iiex's engine does) is the pattern.
- No editable shape. The runtime shape is the only copy.
- Report accepted, not rated, output. `ProduceAir` already returns the accepted litres; the HUD discards them.
  Fixing this makes the ceiling, the leak cap and a saturated main legible instead of invisible, which is
  what R7 ("nothing is hidden") asks for.
- The 17.3 % gate line is undocumented in-game. Nothing tells a player laying fuel courses whether the
  column's carbon fraction lands above or below what the bellows can push; the furnace HUD or the handbook
  should surface the demanded pressure.
- One blower per furnace is the only supported topology. Nothing stops two blowers on one main, but the
  balance (45 vs 42) is sized for exactly one, and stacking them raises volume without raising the 2.2 atm
  ceiling, so a second blower cannot unlock a lean charge. That is probably correct; it is not asserted
  anywhere.
- B11 interaction unresolved: air is consumed before the pressure test, so an under-pressure main drains dry
  while counting zero supply. The blower is the supply side of that bug and its fix will change what
  "45 L/s is enough" means.
- No handbook page. The block ships with only `blockdesc-twintubmpblower*`
  (`mods/iiex/assets/iiex/lang/en.json:185`).
