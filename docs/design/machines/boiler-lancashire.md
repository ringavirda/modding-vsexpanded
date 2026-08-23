# Lancashire Boiler
**Status** live   **Mod** siex

**Owns**
- The Lancashire variant's stat table - capacity, boil window, steam rate, choke pressure, blast radius -
  and every `LancashireBoiler*` key.
- The Lancashire's structure: the 35-cell filler footprint, the seven geometry offsets, the masonry bill
  its retired layout used to demand, and which cells the player touches.
- Its construction: the grid frame recipe and the four RCC stages with their exact totals (a test pins
  them), including the steel-vs-iron split inside those stages.
- Its assets: shape, animations, textures, `waterRendererBox`, and the state of its handbook page.
- The arithmetic that follows from its numbers: the 9.6 atm hand-prime ceiling, the boil-down time, the
  temperature at its ceiling, and how many Cornish engines one Lancashire feeds.
- The fact that the masonry its retired layout demanded hard-codes iiex cast-tier fittings, so an HP
  plant's boiler could never have been walled in with HP-tier parts.

**Does not own** - cited only, never restated:
- The shared boiler model - one tank shared between water and steam, `InternalPressure`, the
  `Idle → Heating → Boiling` FSM and its graces, feedwater intake and the pressurised-feed flash,
  `PushSteam`'s connected-vessel equalisation, the steam ceiling cap, man-hatch venting, internal condensation, the
  choke rule, the burst sequence, `GetBlockInfo`, the drop rules, and every shared `Boiler*` / `Steam*` key -
  [Cornish boiler](boiler-cornish.md), canonical for all of it.
- The pipe pool, `LitresPerPipe`, pressure formulas, burst-by-tier, joints, leaks, chimney venting, the
  network tick order - [pipe network](../mechanics/pipe-network.md).
- The rolled tier the Lancashire's steam main has to be made of, its 12 atm rating, its welded-joins-only
  rule and its missing recipe (B5) - [rolled pipe](rolled-pipe.md).
- The iiex fittings the retired layout named (passthrough, passthrough-bend, outlet) and B6 in
  full - [cast pipes & fittings](cast-pipes.md).
- Fillers, the footprint DSL, `Origin`-is-the-negation, per-cell collision, the declarative filler port -
  [multiblock](../mechanics/multiblock.md).
- Code-first defs, the RCC `Construction` builder, the `brokenDropsRatio` chain, the recipe-cost catalogue -
  [recipes & config](../mechanics/recipes-config.md).
- The Cornish engine this boiler exists to feed - its bands, its throttle, its power
  ([Cornish engine](engine-cornish.md)).
- The injector and the settled feedwater redesign - [pumps](pumps.md).

---

## Role

The Lancashire is the Cornish boiler pushed up a band: same class, same FSM, and a vessel that chokes at
12 atm instead of 5. The two have parted company on size and on fire - the Cornish was rebalanced onto a
larger footprint with a bed inside its own shape, and this one has neither yet.

| | Cornish boiler | Lancashire |
|---|---|---|
| Capacity | 1600 L | 1200 L |
| Boil window | 300 – 1000 L | 200 – 800 L |
| Steam | 64 L/s | 48 L/s |
| Choke | 5.0 atm | 12.0 atm |
| Blast radius | 3 | 4 |
| Pickaxe | tier 3 (bronze) | tier 4 (iron) |
| Footprint | 3 × 6 × 3, 40 fillers | 3 × 6 × 2, 35 fillers |
| Fire | an internal `BEBehaviorFirebox` bed, 16 units, lit through its own main hatch | a free-placed `game:coalpile` at `(0,0,-1)`, required by nothing |
| Fuel matters | rate and duration read off the coal's own `combustibleProps` | not at all: `IsBurning` and non-empty |

⛔ The Lancashire is the smaller vessel now and still the higher tier. Its 48 L/s against the Cornish's
64 buys pressure, not throughput, which is the right shape only if the Cornish engine's draw justifies it
- and nothing has re-checked that since the rebalance.

A boiler that chokes at 12 sits four atmospheres above the 8 atm break point of the only engine that can use
it, and the device that is supposed to sit between them cannot be fitted
([cast pipes](cast-pipes.md) § B6, [rolled pipe](rolled-pipe.md)).

---

## Structure

A megablock and nothing else, like the Cornish ([multiblock](../mechanics/multiblock.md) owns the filler
system; the machinery is [Cornish boiler](boiler-cornish.md) § Structure's).

| | |
|---|---|
| Class | `BlockBoilerLancashire : BlockBoiler : BlockFilledMegastructure` (`BlockBoilerLancashire.cs:17`) |
| Block entity | `BlockEntityBoilerLancashire : BlockEntityBoiler` (`BlockEntityBoilerLancashire.cs:12`) - 28 lines, six stat overrides, nothing else |
| Filler footprint | 35 cells - `Origin(-1, 0)`, layer 0 `+ O +` then 5 × `+ + +`, layer 1 6 × `# + #` with the middle of row 5 a `Port('S', UP, "pipe")` (`:95-122`). 22 attach-allowing, 13 plain (golden `goldens/siex/blocktypes/boiler/lancashire.json`) |
| Reserved body | 3 × 2 × 6 (X −1..1, Y 0..1, Z 0..5) minus the principal |
| Principal | `(0,0,0)`, the `'O'` glyph of the footprint DSL |
| Orientation | `side` variant, from `BoilerShell` (`BlockBoiler.cs:124-140`) |
| `StructureAngle` | `AngleFromSide(side) + 180` (`BlockBoiler.cs:44`, `:49-53`) |
| Shape spin | `rotateYByType` `*-n: 0` - the leaf passes 0, not `BodySpinOffset`, so it differs from `StructureAngle` by 180° on purpose ([Cornish boiler](boiler-cornish.md) Gotcha 2) |
| Resistance / stack | 45 / 1, from `BoilerShell` (`BlockBoiler.cs:131-132`) |
| Mining tier | 4 (`BlockBoilerLancashire.cs:47`) - pinned by `HpMegablockDropTierTests.Lancashire_boiler_needs_an_iron_tier_pickaxe` |
| Rendered footprint | [layouts.md](../../internal/workbench/layouts.md) § siex - round-tripped from the golden |

### Geometry offsets — `BlockBoilerLancashire.cs:50-94`, resolved through `BlockBoiler.cs:135-174`

All seven rotate by `StructureAngle`; a missing attribute resolves to the principal
(`BlockBoiler.cs:142-145`).

| Offset | Cell | What sits there |
|---|---|---|
| `fuelOffset` | `(0,0,-1)` | the coal pile the player sets there; nothing declares or requires the cell |
| `manHatchOffset` | `(0,1,1)` | filler carrying the access hatch; fill / drain / hold-toggle answer only here |
| `steamConnectorOffset` | `(0,1,4)` | the footprint's `Port(UP, "pipe")` cell; the steam pipe goes at `(0,2,4)`, above it |
| `explosionCenterOffset` | `(0,1,3)` | blast centre — inside the vessel |
| `lightSampleOffset` | `(0,1,2)` | body cell the animated mesh is lit from |
| `exhaustOutletOffset` | `(0,1,6)` | `iiex:pipe-outlet-fire-u`, a player-built block and a graph node in its own right — outside the filler footprint (which stops at Z = 5) |
| `waterRendererBox` | voxels `(-14,2,2)-(30,30,94)` | the in-vessel water surface |

No `mainHatchOffset` and no `feedwaterFace`: this leaf declares neither, so the main hatch resolves to the
principal and is never reached (the branch is gated on the vessel having a bed, and this one has none),
and the feedwater face falls back to DOWN.

The steam port `(0,1,4)`, the blast centre `(0,1,3)` and the light sample `(0,1,2)` are three different
cells, so moving any one of them leaves the other two where they are. This is the same separation the
Cornish now carries ([Cornish boiler](boiler-cornish.md) Gotcha 12).

### The player-built firebox — dropped, pending this boiler's own conversion

⛔ **The masonry surround is not a requirement.** `BlockEntityBoiler` derives from
`BlockEntityProductionMachine`, not from a multiblock, so no boiler verifies a layout around itself; the
only gate on running one is finishing its construction stages. The Lancashire therefore declares no
`MultiblockLayout` at all, rather than a bill of masonry, flue passthroughs and an outlet neck that reads
as enforced and enforces nothing. Verification does not come back here — the base class that performed
it is gone by design.

What the Lancashire still needs to run: a burning `game:coalpile` in its `fuelOffset` cell `(0,0,-1)`,
and a steam pipe above the port cell its footprint declares at `(0,1,4)`. The surround is decoration
until this boiler is given a firebox inside its own shape, the way the Cornish was.

Its frames are already aligned and must stay that way. The art is drawn along local **-z** (casing,
flues and base extension all run z -80..+16 voxels; the +z figures a naive read gives come from two
elements measured before their own rotation is applied) while the footprint is authored along **+z**
(`Origin(-1, 0)`, `O` in the first row). `StructureAngle`'s `+180` is exactly what reconciles them, so
this leaf passes a shape spin of **0** where the Cornish passes 180 - the two differ on purpose, and
making them alike would move the Lancashire's mesh six cells off its fillers. `BoilerFootprintGuards`
asserts it in both suites, at all four orientations.

The bill below is what the retired layout used to demand, kept as the record of what the art depicts:

| Block | Count | Where |
|---|---|---|
| `game:claybricks-good-fire` | 39 | 27 on layer −1, 9 on layer 0, 3 on layer 1 |
| `game:cokeovendoor*` | 1 | `(0,0,-2)` — the stoking door |
| `iiex:pipe-passthrough-fire-*` | 2 | `(0,-1,-2)`, `(0,-1,-1)` — the feed line crossing the firebox wall |
| `iiex:pipe-passthroughbend-fire-u*` | 1 | `(0,-1,0)` — turns the feed up into the boiler's DOWN port |
| `iiex:pipe-outlet-fire-u` | 1 | `(0,1,6)` — the exhaust neck; cap it with a vanilla chimney |
| `@(air\|coalpile)` | 1 | `(0,0,-1)` |
| `game:air*` | 1 | `(0,0,6)` — must stay clear |
| `exlib:structurefiller` | 35 | placed by the megablock, not the player |

Four of those five fittings were iiex cast-tier blocks, named as literal legends, neither wildcarded across
domains nor tier-parameterised - so while the layout stood, the high-pressure boiler could only be walled in
with low-pressure-tier plumbing. There is still no `siex:` passthrough, bend or outlet to substitute; hpex
ships segments only ([rolled pipe](rolled-pipe.md)). The three are also flanged, so a rolled steam main
cannot reach any of them either, which is the half of this that survived the layout; see
[Gotchas](#gotchas) 1.

### The three ports

Same mechanism as the Cornish ([Cornish boiler](boiler-cornish.md) § The three ports), and this vessel
takes the fallback on two of the three.

| Port | Cell | Direction |
|---|---|---|
| Feedwater in | the principal's DOWN face - no `feedwaterFace` declared, so `FeedwaterWorldFace` falls back to DOWN (`BlockBoiler.cs:64-68`) | pipe → boiler |
| Steam out | `(0,2,4)`, above the footprint's own `Port('S', UP, "pipe")` cell | boiler → pipe |
| Exhaust out | `(0,1,6)`, a player-built outlet block. That cell declares no port, so `ExhaustWorldFace` answers null and the boiler reads the network *at* the cell rather than across a face (`BlockEntityBoiler.ExhaustNetwork`, `:458-465`) | boiler → pipe |

---

## Assets

| Asset | Path | State |
|---|---|---|
| Editable shape | `assets/editable/shapes/machines/steam/machine-pipe-megablock-boiler-lancashire.json` | present; the runtime copy is converted from it. Not redrawn for the hatch rename or an internal firebox |
| Runtime shape | `assets/siex/shapes/boiler/lancashire.json` | one top-level `Root`, children `Base` · `BaseExtension` · `Casing` · `Flues` - exactly the four RCC stage element sets. The Cornish's `Root` is unwrapped; this one is not |
| Animations | same file | `idle` (30 f, `Hold`) · `lidopen` (30 f, `Hold`) - both poses, not motion. The shared base plays `manhatchopen`, so this leaf overrides `ManHatchAnimation` back to `lidopen` (`BlockEntityBoilerLancashire.cs:27`); without that override the pose loop would stop `idle` and start a clip that does not exist, and the vessel would vanish |
| Textures | `fire1`, `iron3`, `steel32`, `iron5`, `steel3`, `steel42` | declared in the shape. Still carries `iron3` and `iron5`, so the steel boiler is part iron sheet |
| Water surface | `BoilerWaterRenderer` + `waterRendererBox` `(-14,2,2)–(30,30,94)` | `BlockBoilerLancashire.cs:85-92` — 94/16 ≈ 5.9 cells along the 6-cell body |
| Handbook | `assets/siex/config/handbook/05-highpressure.json` ↔ `docs/siex/handbook/05-highpressure.html` | present, shared with the Cornish engine, and wrong on three of the four build figures — see [Gotchas](#gotchas) 4 |
| Mod icon | `src/SteelIndustryExpanded/modicon.png` | a copy of iiex's, placeholder (`docs/siex/ASSETS-TODO.md`) |

The RCC behaviour suppresses the default mesh, so the vessel is only visible through the animator holding
`idle` or `lidopen`. The seeding guard and the `BoilerAnimatableRenderer` light-sample swap are
[Cornish boiler](boiler-cornish.md) § Assets'.

---

## Construction

Two steps, both required.

### 1. The frame block — grid recipe (`MachineRecipeDefinitions.cs:26-34`)

```
B H B          P = game:metalplate-steel ×1 each   → 2
P R P          B = game:burnedbrick-fire ×2 each   → 4
               R = game:rod-steel ×2               → 2
               H = hammer (tool)
→ siex:boilerlancashire-n
```

Size is declared `3 × 2` (`:29`). Totals: 2 steel plate · 4 fire brick · 2 steel rod. Emitted once - the
boiler recipe is not looped over gear codes (`:16-21`). Golden `goldens/siex/recipes/grid/machines.json`.
No dead ingredient: every letter in the pattern is bound, which is what the Cornish's frame recipe was
brought into line with.

### 2. The RCC stages — `BlockBoilerLancashire.cs:123-144`

Right-click the placed block with the materials in the hotbar; each stage adds one element subtree.

| # | Adds | `metalplate-steel` | `iiex:rivet` | `rod-steel` | `game:burnedbrick-fire` |
|---|---|---|---|---|---|
| 1 | `Root/Base` | — | — | — | — |
| 2 | `Root/BaseExtension` | 10 | 16 | — | 12 |
| 3 | `Root/Flues` | 8 | 16 | 4 | — |
| 4 | `Root/Casing` | 16 | 16 | 6 | 48 |
| | **total** | **34** | **48** | **10** | **60** |

Rivets, not nails, and they are **iiex's**: a rivet is a rivet whatever the shell is made of, and siex
mints none of its own (`BlockBoilerLancashire.cs:28-29`). `PressureVesselGate` asserts that no boiler
stage accepts nails ([fasteners](../items/fasteners.md)).

Pinned exactly by `HpMegablockDropTierTests.Lancashire_boiler_full_construction_cost_is_pinned`
(`HpMegablockDropTierTests.cs:94-103`), because the 80 % salvage is taken from it.

The material gate is inconsistent inside the stage table. Plates and rods are the bare codes
`metalplate-steel` / `rod-steel` - steel only, no wildcard, no `storeWildCard` (`:145`, `:151`, `:156`,
`:161`). The nails go through `RequireMetalNails`, which emits `metalnailsandstrips-*` with
`allowedVariants: ["iron","steel"]` and `storeWildCard: "metal"` (golden). So the boiler is steel-gated on
44 of its 68 metal pieces and iron-satisfiable on the other 24. The salvage stores the wildcard variant only
for the nails.

Catalogued twice for the cost system: `boilerlancashire-grid` and `boilerlancashire-rcc`
(`SiexRecipeConfig.cs:50`, `:54`), level switched by `/exmod recipes hpex <level>`
([recipes & config](../mechanics/recipes-config.md)).

---

## Operation

Every mechanism is [Cornish boiler](boiler-cornish.md)'s; what follows is what the six numbers change.

```
       coal pile (0,0,-1)                      exhaust outlet (0,1,6) ──► chimney
              │                                    ▲  16 L/s (FIXED, same as Cornish)
              ▼                                    │  refused above 0.8 atm ⇒ CHOKE
   ┌───────────────────────────────────────────────┴──┐
   │  ONE 1200 L VESSEL                               │
   │   water ──(180 s heat-up)──► boiling: 3 L/s water│──► steam port (0,2,4)
   │                                   ×16 = 48 L/s   │
   │   InternalPressure = steam / (1200 − water)      │
   └──────────────▲───────────────────────────────────┘
                  │ ≤10 L/s up to 600 L, from the DOWN network
            feedwater pipe (0,-1,0)
```

### The numbers that move, and the ones that do not

| quantity | Lancashire | source |
|---|---|---|
| Water burnt while boiling | 3 L/s | `SteamPerSecond / SteamExpansionFactor` = 48 / 16 |
| Auto-intake ceiling | 600 L | `Capacity × BoilerWaterIntakeFillFraction (0.5)` (`BlockEntityBoiler.cs:59-60`) |
| Manual-fill / condensation ceiling | 800 L | `MaxBoilWater` |
| Danger-zone plume threshold | 10.8 atm | `0.9 × MaxOutputPressure` (`BlockEntityBoiler.cs:127`) |
| Hard steam cap | `12 × (1200 − water)` | `CapSteamToCeiling` |
| Steam temperature at the ceiling | ≈ 189.9 °C | `100 × (12+1)^0.25`, formula owned by [Cornish boiler](boiler-cornish.md) |
| Heat-up | 180 s | unchanged — iiex's `BoilerHeatUpSeconds` |
| Exhaust | 16 L/s @ 0.6 × T | unchanged — `BoilerExhaustPerSecond` is fixed for every variant |
| Man-hatch vent / leak / condense rates | 200 / 16 / 200 L/s | unchanged — all iiex constants |
| Burst grace | 30 s | unchanged |

Exhaust production, the choke test and every relief path are per-second constants shared with the 64 L/s
Cornish (Gotcha 6), so the 16 L/s unpiped-outlet leak is a third of what the Lancashire makes.

### Hand-primed, the Lancashire can never reach 12 atm — and therefore can never burst

Boiling stops the moment water falls below `MinBoilWater` (`BlockEntityBoiler.cs:369-371`, the
`enoughWater` gate at `:357-405`), and manual pouring tops out at `MaxBoilWater`. So the entire steam a
hand-primed boiler can make is:

```
boiled  = MaxBoilWater − MinBoilWater = 800 − 200 = 600 L water
steam   = 600 × 16                                = 9600 L
head    = Capacity − MinBoilWater = 1200 − 200    = 1000 L
p_max   = 9600 / 1000                             = 9.6 atm
```

9.6 atm is past every Cornish engine engage pressure and past its 8 atm break point, but below the 12 atm
choke, so the burst arming condition (`InternalPressure >= MaxOutputPressure`) is never met. Solving the same
equation for a prime that would reach 12 gives a negative starting water level: no charge of any size gets
there.

The Lancashire is therefore safe until water is plumbed to it and dangerous the moment it is. With a feed
holding water at the 600 L intake ceiling the free space is fixed at 600 L, so 12 atm needs 7200 L of steam -
150 s of boiling - and the burst follows 30 s later. Nothing warns that connecting the feedwater line is what
arms it; the only signal is the danger-zone plume from 10.8 atm.

The intake draws whatever the line offers regardless of `InternalPressure`
([Cornish boiler](boiler-cornish.md) § Open), so any water source does this. The settled fix - gate intake on
internal pressure and make the injector the HP feed - is designed and unbuilt ([pumps](pumps.md)).

### Burst

Armed by `Boiling && burning && InternalPressure >= 12 atm` for 30 s (`BlockEntityBoiler.cs:436-454`);
opening the man hatch resets it unconditionally. The sequence - 40 % salvage, `RemoveStructure`,
`ShatterFragileBlocks` below resistance 20, `CreateExplosion(EntityBlast, r, r+2)` - is
[Cornish boiler](boiler-cornish.md) § Burst's. Only the radius is this page's: `r = 4`, so
shatter radius 4 and entity-damage radius 6, against the Cornish's 3 and 5.

At radius 4 the blast reaches past the boiler's own firebox in every direction (the envelope is only 3 wide)
and into a Cornish engine parked beside it. Fire brick is above the resistance-20 threshold and survives;
pipes, ports and coal piles do not.

### Feeding a Cornish engine

| Lancashire output | engines it can hold at full draw | headroom |
|---|---|---|
| 48 L/s | 1 × Cornish engine @ High (32 L/s) | 16 L/s |
| | 3 × Cornish @ Normal (3 × 16) | 0 L/s — exactly saturated |
| | 6 × Cornish @ Low (6 × 8) | 0 L/s |
| | 1 × Watt (30 L/s) + 1 × Cornish @ Low | 10 L/s |

Nothing in the code states any of this; it is the ratio of `LancashireBoilerSteamPerSecond` to the engine's
`RunSteamRate` ([Cornish engine](engine-cornish.md)). A starved line does not stall an engine, it scales its
power down by `frac`.

---

## Numbers

### hpex config — `SiexConfig.cs`, file `ModConfig/ex_values.json`, section `hpex`

| key | value | file:line | what it does |
|---|---|---|---|
| `LancashireBoilerCapacity` | `1200 L` | `SiexConfig.cs:46` | one tank, shared between water and steam |
| `LancashireBoilerMinBoilWater` | `200 L` | `:49` | operating floor; also the bucket-drain floor |
| `LancashireBoilerMaxBoilWater` | `800 L` | `:52` | manual-fill / condensation ceiling |
| `LancashireBoilerSteamPerSecond` | `48 L/s` | `:56` | ⇒ 3 L/s of water consumed |
| `LancashireBoilerMaxOutputPressure` | `12.0 atm` | `:60` | choke ceiling, hard cap and burst trigger |
| `LancashireBoilerExplosionRadius` | `4` | `:63` | shatter radius; blast damage radius is `r + 2` = 6 |
| `RccBrokenDropsRatio` | `0.8` | `:124` | salvage from mining it intact; wired at `SteelIndustryExpandedModSystem.cs:35-38` |
| `RecipeLevel` | `"normal"` | `:131` | `/exmod recipes hpex <level>` |

`SiexConfig.Migrations` is empty (`:37`), so none of these is force-reset on upgrade; retuned values survive.

### Bound through the base — the six abstract stats

| override | reads | file:line |
|---|---|---|
| `Capacity` | `LancashireBoilerCapacity` | `BlockEntityBoilerLancashire.cs:14` |
| `MinBoilWater` | `LancashireBoilerMinBoilWater` | `:15-16` |
| `MaxBoilWater` | `LancashireBoilerMaxBoilWater` | `:17-18` |
| `SteamPerSecond` | `LancashireBoilerSteamPerSecond` | `:19-20` |
| `MaxOutputPressure` | `LancashireBoilerMaxOutputPressure` | `:21-22` |
| `ExplosionRadius` | `LancashireBoilerExplosionRadius` | `:23-24` |

Everything else the boiler needs is `IiexConfig`'s, tabulated by [Cornish boiler](boiler-cornish.md)
§ Numbers (`SiexConfig.cs:14-18`).

### Derived — owned here

| quantity | formula | value |
|---|---|---|
| water burnt while boiling | `48 / 16` | 3 L/s |
| `MaxWaterIntakeFill` | `1200 × 0.5` | 600 L |
| danger zone | `0.9 × 12` | 10.8 atm |
| boil-down time, full prime | `600 / 3` | 200 s |
| hand-prime pressure ceiling | `(800−200)×16 / (1200−200)` | 9.6 atm |
| steam to reach the choke on a live feed | `12 × (1200 − 600)` | 7200 L ⇒ 150 s |
| temperature at the choke | `100 × 13^0.25` | ≈ 189.9 °C |
| firebox masonry | glyph count | 39 `claybricks-good-fire` |
| full RCC cost | stage sum | 34 plate · 24 nails · 10 rod · 60 brick |

### Cited, owned elsewhere — the ladder this boiler sits on

| quantity | value | owner |
|---|---|---|
| rolled (hpex) pipe burst | 12 atm | [rolled pipe](rolled-pipe.md) |
| cast (iiex) pipe burst | 5.0 atm | [cast pipes](cast-pipes.md) |
| plated (iiex) pipe burst | 2.5 atm | [pipe network](../mechanics/pipe-network.md) |
| Cornish engine engage, low / normal / high | 5 / 6 / 7 atm | [Cornish engine](engine-cornish.md) |
| Cornish engine break | 8 atm | [Cornish engine](engine-cornish.md) |
| iiex pressure-valve gate ceiling | 5.0 atm | [cast pipes](cast-pipes.md) § B6 |
| `SteamExpansionFactor` / `BoilerHeatUpSeconds` / `BoilerExhaustPerSecond` / `BoilerOverpressureSeconds` | 16 / 180 s / 16 L/s / 30 s | [Cornish boiler](boiler-cornish.md) |
| `LitresPerPipe` | 30 L | [pipe network](../mechanics/pipe-network.md) |

```
5      6      7   | 8        9.6          12
       engage      break    hand-prime    Lancashire choke
                            ceiling       == rolled pipe burst
```

The boiler's ceiling equals the only pipe tier that can carry it; [rolled pipe](rolled-pipe.md) explains why.

### Hard-coded — not config

Everything hard-coded on this machine is inherited and listed by
[Cornish boiler](boiler-cornish.md) § Hard-coded (tick intervals, the `dt` clamp, the 3000 ms completion
monitor, the exhaust temperature factor, the transfer epsilon, the plume counts, the blast call shape, the
shatter chance). The two that are this block's own:

| value | file:line | what it does |
|---|---|---|
| mining tier `4` | `BlockBoilerLancashire.cs:35` | iron pickaxe, one tier above the Cornish boiler |
| `waterRendererBox (-14,2,2)-(30,30,94)` | `BlockBoilerLancashire.cs:45-53` | the rendered water volume; the base's coded fallback `(-16,0,0)-(16,16,48)` would be less than half the vessel |

---

## Drops

The Lancashire never drops itself. `BoilerShell` calls `NoDrops()` (`BlockBoiler.cs:65`, golden
`"drops": []`) and `BlockBoiler.GetDrops` returns `[]` unconditionally (`BlockBoiler.cs:153-158`).
Pinned by `HpMegablockDropTierTests.Lancashire_boiler_does_not_drop_itself_as_a_block`
(`HpMegablockDropTierTests.cs:37-49`).

| Path | Returns |
|---|---|
| Mined intact | 80 % of the RCC materials - ~27 plate, ~19 nails, 8 rod, 48 brick - scattered by the RCC behaviour (`RccBrokenDropsRatio`, `SiexConfig.cs:124`, registered at `SteelIndustryExpandedModSystem.cs:35-38`) |
| Burst | 40 % (`BoilerExplosionDropRatio`, iiex), pulled through `ExRightClickConstructable.GetConstructionDrops` |
| Fillers | removed, never dropped |
| Firebox masonry | ordinary block drops; player-placed, the boiler never touches it |
| Water / steam held | lost |

`brokenDropsRatio` is not in the def - pinned absent by
`HpMegablockDropTierTests.Hp_machines_no_longer_carry_a_json_drop_ratio` (`:113-121`) so the number can only
come from config. The 80 % default is pinned by `Hp_machine_salvage_ratio_defaults_to_80_percent` (`:105-111`).

The salvage ratio is looked up by the broken block's `Code.Domain`, so hpex must register its own copy even
though iiex already registered an identical default (`SteelIndustryExpandedModSystem.cs:32-38`).

---

## Code

| Piece | file:line |
|---|---|
| `BlockBoilerLancashire : BlockBoiler, IFillerHost, IBoilerGeometry, IExBlockDefProvider` | `BlockStructures/Boiler/Blocks/BlockBoilerLancashire.cs:17` |
| `Definitions(domain)` → the single def | `:31-32` |
| `Lancashire(domain)` — the whole blocktype | `:34-144` |
| geometry attributes | `:50-94` |
| `FillerOffsets` (the 35-cell footprint) | `:95-122` |
| `Construction` (four stages) | `:123-144` |
| `BlockEntityBoilerLancashire : BlockEntityBoiler` | `BlockStructures/Boiler/BlockEntities/BlockEntityBoilerLancashire.cs:12` |
| the six stat overrides, plus the `ManHatchAnimation` override | `:13-27` |
| `SiexConfig` § Lancashire boiler | `SiexConfig.cs:39-64` |
| grid recipe | `Recipes/Grid/MachineRecipeDefinitions.cs:26-34` |
| cost-catalogue keys | `SiexRecipeConfig.cs:50`, `:54` |
| salvage-ratio registration | `SteelIndustryExpandedModSystem.cs:32-38` |
| save migration off `iiex:` / `ppex:` | `BlockMigrations/HpMachineDomainMigration.cs:34-46` — see [rolled pipe](rolled-pipe.md) § Gotchas 1, which owns that class |
| everything that runs | `IronIndustryExpanded/BlockStructures/Boiler/BlockBoiler.cs`, `BlockEntityBoiler.cs` — [Cornish boiler](boiler-cornish.md) § Code |

### Where a caller hooks in

- Another boiler variant: implement the six abstract stats on `BlockEntityBoiler` (`:46-68`) and call
  `BoilerShell`. This leaf adds nothing but numbers, a footprint, a layout and stages - 25 lines of BE and
  164 of block def.
- Retuning the HP band: `LancashireBoilerMaxOutputPressure` is read live through `SiexValues`, so
  `/exmod config hpex` changes the choke, the hard cap and the burst trigger together - they are one number
  used three times.
- Making the vessel longer: five things must move together - the footprint layer bounds, the layout row
  count, `steamConnectorOffset`, `exhaustOutletOffset` and `waterRendererBox.z2`. Nothing checks the
  relationship.

### Tests — `test/SteelIndustryExpanded.Tests/`

| file | pins |
|---|---|
| `Definitions/HpMegablockDropTierTests.cs` | no self-drop · mining tier 4 · the full four-stage construction cost · salvage default 0.8 · no JSON `brokenDropsRatio` |
| `Definitions/SiexDefinitionGoldenTests.cs` | the def reproduces `goldens/siex/blocktypes/boiler/lancashire.json` byte-for-byte; the golden set exactly covers the defs; every shape reference resolves |

There is no behavioural test for this boiler at all. No FSM test, no burst test, no scene: the entire
iiex boiler suite (`test/IronIndustryExpanded.Tests/Blocks/Boiler/`) runs against the Cornish stat table, so
nothing exercises a 1200 L / 12 atm vessel. The 9.6 atm ceiling above is unasserted anywhere.

---

## Gotchas

1. The art depicts a firebox nothing asks for, and the fittings it depicts are iiex's. The retired layout
   named `iiex:pipe-passthrough-fire-*`, `iiex:pipe-passthroughbend-fire-u*` and `iiex:pipe-outlet-fire-u`
   as literal legends, so an HP vessel could only be walled in with LP-tier plumbing; with no layout in the
   leaf, nothing demands them, and a player who builds what the model draws spends 39 fire bricks, a door
   and four fittings on decoration. What outlives the deletion is the gap that made it awkward: hpex ships
   pipe segments only ([rolled pipe](rolled-pipe.md)), and its segments are rolled while those three
   fittings are flanged, so a rolled steam main still cannot reach an iiex fitting anywhere. That is B6's
   problem now rather than this boiler's.

2. Connecting feedwater is what arms the boiler, and nothing says so. Hand-primed it tops out at 9.6 atm and
   cannot burst; piped, it reaches 12 atm in 150 s of boiling. The intake does not consider
   `InternalPressure`, so even a 1 atm manual-pump line does it. See [Operation](#operation).

3. `MinBoilWater` (200 L) is also the bucket-drain floor. `TryManualDrain` refuses below it
   ([Cornish boiler](boiler-cornish.md)), so 200 L of water is permanently unrecoverable - a third less than
   the Cornish's stranded 300 L, and a sixth of this vessel's 1200 L against the just-under-a-fifth the
   Cornish strands of its 1600 L. The larger boiler is the one that gives more back.

4. The handbook is wrong on three of the four build figures (`docs/siex/handbook/05-highpressure.html:14-15`,
   `assets/siex/lang/en.json` `handbook-highpressure-text`):

   | handbook | actual | source |
   |---|---|---|
   | "36 steel plates" | 34 | `HpMegablockDropTierTests.cs:99` |
   | "24 nails-and-strips" | 24 yes | `:100` |
   | "12 rods" | 10 | `:101` |
   | "64 fire bricks" | 60 | `:102` |
   | "48 L/s and 12 atm, 1200 L vessel, needs 200 L to begin boiling" | all yes | `SiexConfig.cs:46-60` |

   The HTML ↔ lang copy is guarded by `HandbookParityTests`, but nothing compares either against the code, so
   these four numbers can drift freely ([handbook sync pipeline](../mechanics/recipes-config.md) is the
   pipeline; the drift is this page's).

5. The handbook and the README both instruct the player to fit a pressure valve between this boiler and the
   Cornish engine. They cannot (`src/SteelIndustryExpanded/README.md:20-23`,
   `docs/siex/handbook/05-highpressure.html:31-34`, `docs/siex/moddb.html:52-58`) - B6, owned in full by
   [cast pipes](cast-pipes.md) § B6, and silent in play because of B18 ([rolled pipe](rolled-pipe.md)).

6. The bigger vessel did not get a bigger flue or a bigger lid. `BoilerExhaustPerSecond` (16 L/s),
   `BoilerLidVentRate` (200 L/s), `BoilerSteamLeakRate` (16 L/s) and `BoilerShutdownCondenseRate` (200 L/s)
   are shared constants ([Cornish boiler](boiler-cornish.md) § Numbers), so every relief path on a Lancashire
   is proportionally two-thirds as strong as on a Cornish.

7. `CondenseInternal`'s anti-burst guard is unreachable here too - it refuses at 16 atm, the cap holds the
   vessel at 12. Owned by [Cornish boiler](boiler-cornish.md) Gotcha 1, which names the Lancashire.

8. The name "Cornish" spans two mods and two tiers. `iiex:boilercornish` is the low-pressure entry boiler;
   `siex:enginecornish` is the high-pressure engine. A Cornish boiler cannot drive a Cornish engine (5 atm
   choke vs 6 atm engage at normal throttle) - see [Cornish boiler](boiler-cornish.md) Gotcha 3.

9. The blast radius grew but the blast-resistance threshold did not. `BoilerBlastResistanceThreshold` is 20
   and every machine in the suite is resistance 45, so a radius-4 Lancashire burst still cannot chain into a
   neighbouring boiler or engine - only into pipes, ports, coal piles and terrain. That is by design
   ([Cornish boiler](boiler-cornish.md)), but at radius 4 the blast reliably takes out the steam main itself,
   so a burst also disconnects the plant.

10. Nothing in hpex consumes rolled pipe, including this boiler. Its grid recipe asks for steel plate, rod
    and fire brick; its stages ask for the same; its structure asks for iiex fittings. The rolled tier is a
    peer of the machines, not a prerequisite for them ([rolled pipe](rolled-pipe.md)).

---

## Open

- No behavioural test. The whole boiler suite exercises the Cornish's numbers. A Lancashire rig would pin the
  9.6 atm hand-prime ceiling, the 150 s live-feed climb and the radius-4 burst.
- No editable shape. `assets/siex/shapes/boiler/lancashire.json` is the only copy; the model cannot be
  re-edited from source (`docs/siex/ASSETS-TODO.md` lists the listing art but not this).
- The rolled tier still reaches no fitting at all, and that needs a decision (Gotcha 1). It is no longer
  this boiler's lock-in - there is no layout to name blocks in - but hpex ships segments only, so either it
  ships passthrough / passthrough-bend / outlet in its own domain (which also fixes B6, since both the joint
  family and the pressure-valve ceiling read `Code.Domain`), or a rolled run can never terminate in anything
  but an iiex flanged part.
- Feedwater is the settled-but-unbuilt half of this machine. [pumps](pumps.md) fixes the injector as the one
  device that covers the whole band; the block is iiex's and does not exist. Until then the boiler is either
  inert at 9.6 atm or armed by any water line at all.
- The stage table's steel gate is half-applied (plates and rods steel-only, nails iron-or-steel). Decide
  whether the Lancashire is a steel build or a wildcard one and make all four ingredients agree - the
  handbook already calls it a steel build.
- The handbook needs a numbers pass (Gotcha 4); it is the only handbook page hpex ships, shared with the
  engine.
