# Lancashire Boiler
**Status** live   **Mod** hpex

**Owns**
- The Lancashire variant's stat table - capacity, boil window, steam rate, choke pressure, blast radius -
  and every `LancashireBoiler*` key.
- The Lancashire's structure: the 3 × 3 × 10 verified envelope, the 35-cell filler footprint, the six
  geometry offsets, the firebox bill, and which cells the player touches.
- Its construction: the grid frame recipe and the four RCC stages with their exact totals (a test pins
  them), including the steel-vs-iron split inside those stages.
- Its assets: shape, animations, textures, `waterRendererBox`, and the state of its handbook page.
- The arithmetic that follows from its numbers: the 9.6 atm hand-prime ceiling, the boil-down time, the
  temperature at its ceiling, and how many Cornish engines one Lancashire feeds.
- The fact that its own required structure hard-codes lpex cast-tier fittings, so an HP plant's boiler
  cannot be walled in with HP-tier parts.

**Does not own** - cited only, never restated:
- The shared boiler model - one tank shared between water and steam, `InternalPressure`, the
  `Idle → Heating → Boiling` FSM and its graces, feedwater intake and the pressurised-feed flash,
  `PushSteam`'s connected-vessel equalisation, the steam ceiling cap, lid venting, internal condensation, the
  choke rule, the burst sequence, `GetBlockInfo`, the drop rules, and every shared `Boiler*` / `Steam*` key -
  [Cornish boiler](boiler-cornish.md), canonical for all of it.
- The pipe pool, `LitresPerPipe`, pressure formulas, burst-by-tier, joints, leaks, chimney venting, the
  network tick order - [pipe network](../mechanics/pipe-network.md).
- The rolled tier the Lancashire's steam main has to be made of, its 12 atm rating, its welded-joins-only
  rule and its missing recipe (B5) - [rolled pipe](rolled-pipe.md).
- The lpex fittings this boiler's layout demands (passthrough, passthrough-bend, outlet) and B6 in
  full - [cast pipes & fittings](cast-pipes.md).
- Fillers, the ASCII layout DSL, `Origin`-is-the-negation, projection, per-cell collision -
  [multiblock](../mechanics/multiblock.md).
- Code-first defs, the RCC `Construction` builder, the `brokenDropsRatio` chain, the recipe-cost catalogue -
  [recipes & config](../mechanics/recipes-config.md).
- The Cornish engine this boiler exists to feed - its bands, its throttle, its power
  ([Cornish engine](engine-cornish.md)).
- The injector and the settled feedwater redesign - [pumps](pumps.md).

---

## Role

The Lancashire is the Cornish boiler scaled up and pushed up a band: same class, same FSM, same firebox
pattern, with a vessel half as large again that boils 50 % faster and chokes at 12 atm instead of 5.

| | Cornish boiler | Lancashire |
|---|---|---|
| Capacity | 800 L | 1200 L |
| Boil window | 150 – 500 L | 200 – 800 L |
| Steam | 32 L/s | 48 L/s |
| Choke | 5.0 atm | 12.0 atm |
| Blast radius | 3 | 4 |
| Pickaxe | tier 3 (bronze) | tier 4 (iron) |
| Structure | 3 × 3 × 8, 23 fillers, 33 bricks | 3 × 3 × 10, 35 fillers, 39 bricks |

A boiler that chokes at 12 sits four atmospheres above the 8 atm break point of the only engine that can use
it, and the device that is supposed to sit between them cannot be fitted
([cast pipes](cast-pipes.md) § B6, [rolled pipe](rolled-pipe.md)).

---

## Structure

A megablock and a multiblock at once, like the Cornish ([multiblock](../mechanics/multiblock.md) owns both
systems; the machinery is [Cornish boiler](boiler-cornish.md) § Structure's).

| | |
|---|---|
| Class | `BlockBoilerLancashire : BlockBoiler : BlockFilledMegastructure` (`BlockBoilerLancashire.cs:15`) |
| Block entity | `BlockEntityBoilerLancashire : BlockEntityBoiler` (`BlockEntityBoilerLancashire.cs:12`) - 25 lines, six stat overrides, nothing else |
| Verified envelope | 3 × 3 × 10 - X −1..1, Y −1..1, Z −2..7 = 90 cells (`BlockBoilerLancashire.cs:85-141`) |
| Filler footprint | 35 cells - `Origin(-1, 0)`, layer 0 `+ O +` then 5 × `+ + +`, layer 1 6 × `# + #` (`:56-82`). 23 attach-allowing, 12 plain (golden `goldens/hpex/blocktypes/boiler/lancashire.json`) |
| Reserved body | 3 × 2 × 6 (X −1..1, Y 0..1, Z 0..5) minus the principal |
| Principal | `(0,0,0)`, the `'L'` glyph |
| Orientation | `side` from `abstract/horizontalorientation` (`BlockBoiler.cs:70`) |
| `StructureAngle` | `AngleFromSide(side) + 180` (`BlockBoiler.cs:39-43`) |
| Shape spin | `rotateYByType` `*-north: 0` - no `+180`, so it differs from `StructureAngle` by 180° on purpose ([Cornish boiler](boiler-cornish.md) Gotcha 2) |
| Resistance / stack | 45 / 1, from `BoilerShell` (`BlockBoiler.cs:63-64`) |
| Mining tier | 4 (`BlockBoilerLancashire.cs:35`) - pinned by `HpMegablockDropTierTests.Lancashire_boiler_needs_an_iron_tier_pickaxe` |
| Rendered layout | [layouts.md](../../internal/workbench/layouts.md) § Section 2, "Lancashire boiler (hpex)" - round-tripped from the golden |

### Geometry offsets — `BlockBoilerLancashire.cs:36-55`, resolved through `BlockBoiler.cs:79-114`

All six rotate by `StructureAngle`; a missing attribute resolves to the principal (`BlockBoiler.cs:76-78`).

| Offset | Cell | What sits there |
|---|---|---|
| `fuelOffset` | `(0,0,-1)` | the coal pile — `@(air\|coalpile)` in the layout |
| `lidOffset` | `(0,1,1)` | filler carrying the access lid; fill / drain / hold-toggle answer only here |
| `steamConnectorOffset` | `(0,1,4)` | filler turned into an upward `"pipe"` port; the steam pipe goes at `(0,2,4)`, above it |
| `explosionCenterOffset` | `(0,1,3)` | blast centre — inside the vessel |
| `lightSampleOffset` | `(0,1,3)` | body cell the animated mesh is lit from |
| `exhaustOutletOffset` | `(0,1,6)` | `lpex:pipe-outlet-fire-u`, a player-built block and a graph node in its own right — outside the filler footprint (which stops at Z = 5) |

The steam port and the blast/light cells are different cells here (`(0,1,4)` vs `(0,1,3)`), so the Cornish's
three-concerns-on-one-cell coincidence ([Cornish boiler](boiler-cornish.md) Gotcha 12) does not apply.

### The player-built firebox — `BlockBoilerLancashire.cs:85-141`

Derived by counting the layout glyphs:

| Block | Count | Where |
|---|---|---|
| `game:claybricks-good-fire` | 39 | 27 on layer −1, 9 on layer 0, 3 on layer 1 |
| `game:cokeovendoor*` | 1 | `(0,0,-2)` — the stoking door |
| `lpex:pipe-passthrough-fire-*` | 2 | `(0,-1,-2)`, `(0,-1,-1)` — the feed line crossing the firebox wall |
| `lpex:pipe-passthroughbend-fire-u*` | 1 | `(0,-1,0)` — turns the feed up into the boiler's DOWN port |
| `lpex:pipe-outlet-fire-u` | 1 | `(0,1,6)` — the exhaust neck; cap it with a vanilla chimney |
| `@(air\|coalpile)` | 1 | `(0,0,-1)` |
| `game:air*` | 1 | `(0,0,6)` — must stay clear |
| `exlib:structurefiller` | 35 | placed by the megablock, not the player |

Four of those five fittings are lpex cast-tier blocks, named as literal legends (`:89`, `:90`, `:95`),
neither wildcarded across domains nor tier-parameterised, so the high-pressure boiler can only be walled in
with low-pressure-tier plumbing. There is no `hpex:` passthrough, bend or outlet to substitute - hpex ships
segments only ([rolled pipe](rolled-pipe.md)). The three are also flanged, so a rolled steam main cannot
reach any of them either; see [Gotchas](#gotchas) 1.

### The three ports

Identical mechanism to the Cornish ([Cornish boiler](boiler-cornish.md) § The three ports); only cells differ.

| Port | Cell | Direction |
|---|---|---|
| Feedwater in | the boiler's own cell, DOWN face (`BlockBoiler.cs:48`) | pipe → boiler |
| Steam out | `(0,2,4)`, above the port filler marked by `MarkSteamPort` (`BlockBoiler.cs:131-145`) | boiler → pipe |
| Exhaust out | `(0,1,6)`, the outlet block itself | boiler → pipe |

---

## Assets

| Asset | Path | State |
|---|---|---|
| Editable shape | — | missing. Nothing under `assets/editable/shapes/` matches the Lancashire; the runtime shape is the only copy |
| Runtime shape | `assets/hpex/shapes/boiler/lancashire.json` | root children `Base` · `BaseExtension` · `Casing` · `Flues` - exactly the four RCC stage element sets |
| Animations | same file | `idle` (30 f, `Hold`) · `lidopen` (30 f, `Hold`) - both poses, not motion |
| Textures | `fire1`, `iron3`, `steel32`, `iron5`, `steel3`, `steel42` | declared in the shape. Still carries `iron3` and `iron5`, so the steel boiler is part iron sheet |
| Water surface | `BoilerWaterRenderer` + `waterRendererBox` `(-14,2,2)–(30,30,94)` | `BlockBoilerLancashire.cs:45-53` — 94/16 ≈ 5.9 cells along the 6-cell body, matching the Cornish's 62-for-4 inset |
| Handbook | `assets/hpex/config/handbook/00-highpressure.json` ↔ `docs/hpex/handbook/00-highpressure.html` | present, shared with the Cornish engine, and wrong on three of the four build figures — see [Gotchas](#gotchas) 4 |
| Mod icon | `src/HighPressureExpanded/modicon.png` | a copy of lpex's, placeholder (`docs/hpex/ASSETS-TODO.md`) |

The RCC behaviour suppresses the default mesh, so the vessel is only visible through the animator holding
`idle` or `lidopen`. The seeding guard and the `BoilerAnimatableRenderer` light-sample swap are
[Cornish boiler](boiler-cornish.md) § Assets'.

---

## Construction

Two steps, both required.

### 1. The frame block — grid recipe (`MachineRecipeDefinitions.cs:29-37`)

```
B H B          P = game:metalplate-steel ×1 each   → 2
P R P          B = game:burnedbrick-fire ×2 each   → 4
               R = game:rod-steel ×2               → 2
               H = hammer (tool)
→ hpex:boilerlancashire-north
```

Size is declared `3 × 2` (`:33`). Totals: 2 steel plate · 4 fire brick · 2 steel rod. Emitted once - the
boiler recipe is not looped over gear codes (`:19-26`). Golden `goldens/hpex/recipes/grid/machines.json`.
No dead ingredient: every letter in the pattern is bound.

### 2. The RCC stages — `BlockBoilerLancashire.cs:142-163`

Right-click the placed block with the materials in the hotbar; each stage adds one element subtree.

| # | Adds | `metalplate-steel` | `metalnailsandstrips-*` | `rod-steel` | `game:burnedbrick-fire` |
|---|---|---|---|---|---|
| 1 | `Root/Base` | — | — | — | — |
| 2 | `Root/BaseExtension` | 10 | 8 | — | 12 |
| 3 | `Root/Flues` | 8 | 8 | 4 | — |
| 4 | `Root/Casing` | 16 | 8 | 6 | 48 |
| | **total** | **34** | **24** | **10** | **60** |

Pinned exactly by `HpMegablockDropTierTests.Lancashire_boiler_full_construction_cost_is_pinned`
(`HpMegablockDropTierTests.cs:94-103`), because the 80 % salvage is taken from it.

The material gate is inconsistent inside the stage table. Plates and rods are the bare codes
`metalplate-steel` / `rod-steel` - steel only, no wildcard, no `storeWildCard` (`:145`, `:151`, `:156`,
`:161`). The nails go through `RequireMetalNails`, which emits `metalnailsandstrips-*` with
`allowedVariants: ["iron","steel"]` and `storeWildCard: "metal"` (golden). So the boiler is steel-gated on
44 of its 68 metal pieces and iron-satisfiable on the other 24. The salvage stores the wildcard variant only
for the nails.

Catalogued twice for the cost system: `boilerlancashire-grid` and `boilerlancashire-rcc`
(`HpexRecipeConfig.cs:50`, `:54`), level switched by `/exmod recipes hpex <level>`
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
| Heat-up | 180 s | unchanged — lpex's `BoilerHeatUpSeconds` |
| Exhaust | 16 L/s @ 0.6 × T | unchanged — `BoilerExhaustPerSecond` is fixed for every variant |
| Lid vent / leak / condense rates | 200 / 16 / 200 L/s | unchanged — all lpex constants |
| Burst grace | 30 s | unchanged |

Exhaust production, the choke test and every relief path are per-second constants shared with the 32 L/s
Cornish (Gotcha 6), so the 16 L/s unpiped-outlet leak is a third of what the Lancashire makes.

### Hand-primed, the Lancashire can never reach 12 atm — and therefore can never burst

Boiling stops the moment water falls below `MinBoilWater` (`BlockEntityBoiler.cs:352-354`, the
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

Armed by `Boiling && burning && InternalPressure >= 12 atm` for 30 s (`BlockEntityBoiler.cs:419-437`);
opening the lid resets it unconditionally. The sequence - 40 % salvage, `RemoveStructure`,
`ShatterFragileBlocks` below resistance 20, `CreateExplosion(EntityBlast, r, r+2)` - is
[Cornish boiler](boiler-cornish.md) § Burst's. Only the radius is this page's: `r = 4`, so
shatter radius 4 and entity-damage radius 6, against the Cornish's 3 and 5.

At radius 4 the blast reaches past the boiler's own firebox in every direction (the envelope is only 3 wide)
and into a Cornish engine parked beside it. Fire brick is above the resistance-20 threshold and survives;
pipes, ports and coal piles do not.

### Feeding a Cornish engine

| Lancashire output | engines it can hold at full draw | headroom |
|---|---|---|
| 48 L/s | 1 × Cornish @ High (32 L/s) | 16 L/s |
| | 3 × Cornish @ Normal (3 × 16) | 0 L/s — exactly saturated |
| | 6 × Cornish @ Low (6 × 8) | 0 L/s |
| | 1 × Watt (30 L/s) + 1 × Cornish @ Low | 10 L/s |

Nothing in the code states any of this; it is the ratio of `LancashireBoilerSteamPerSecond` to the engine's
`RunSteamRate` ([Cornish engine](engine-cornish.md)). A starved line does not stall an engine, it scales its
power down by `frac`.

---

## Numbers

### hpex config — `HpexConfig.cs`, file `ModConfig/ex_values.json`, section `hpex`

| key | value | file:line | what it does |
|---|---|---|---|
| `LancashireBoilerCapacity` | `1200 L` | `HpexConfig.cs:46` | one tank, shared between water and steam |
| `LancashireBoilerMinBoilWater` | `200 L` | `:49` | operating floor; also the bucket-drain floor |
| `LancashireBoilerMaxBoilWater` | `800 L` | `:52` | manual-fill / condensation ceiling |
| `LancashireBoilerSteamPerSecond` | `48 L/s` | `:56` | ⇒ 3 L/s of water consumed |
| `LancashireBoilerMaxOutputPressure` | `12.0 atm` | `:60` | choke ceiling, hard cap and burst trigger |
| `LancashireBoilerExplosionRadius` | `4` | `:63` | shatter radius; blast damage radius is `r + 2` = 6 |
| `RccBrokenDropsRatio` | `0.8` | `:124` | salvage from mining it intact; wired at `HighPressureExpandedModSystem.cs:35-38` |
| `RecipeLevel` | `"normal"` | `:131` | `/exmod recipes hpex <level>` |

`HpexConfig.Migrations` is empty (`:37`), so none of these is force-reset on upgrade; retuned values survive.

### Bound through the base — the six abstract stats

| override | reads | file:line |
|---|---|---|
| `Capacity` | `LancashireBoilerCapacity` | `BlockEntityBoilerLancashire.cs:14` |
| `MinBoilWater` | `LancashireBoilerMinBoilWater` | `:15-16` |
| `MaxBoilWater` | `LancashireBoilerMaxBoilWater` | `:17-18` |
| `SteamPerSecond` | `LancashireBoilerSteamPerSecond` | `:19-20` |
| `MaxOutputPressure` | `LancashireBoilerMaxOutputPressure` | `:21-22` |
| `ExplosionRadius` | `LancashireBoilerExplosionRadius` | `:23-24` |

Everything else the boiler needs is `LpexConfig`'s, tabulated by [Cornish boiler](boiler-cornish.md)
§ Numbers (`HpexConfig.cs:14-18`).

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
| cast (lpex) pipe burst | 5.0 atm | [cast pipes](cast-pipes.md) |
| plated (iwex) pipe burst | 2.5 atm | [pipe network](../mechanics/pipe-network.md) |
| Cornish engine engage, low / normal / high | 5 / 6 / 7 atm | [Cornish engine](engine-cornish.md) |
| Cornish engine break | 8 atm | [Cornish engine](engine-cornish.md) |
| lpex pressure-valve gate ceiling | 5.0 atm | [cast pipes](cast-pipes.md) § B6 |
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
| Mined intact | 80 % of the RCC materials - ~27 plate, ~19 nails, 8 rod, 48 brick - scattered by the RCC behaviour (`RccBrokenDropsRatio`, `HpexConfig.cs:124`, registered at `HighPressureExpandedModSystem.cs:35-38`) |
| Burst | 40 % (`BoilerExplosionDropRatio`, lpex), pulled through `ExRightClickConstructable.GetConstructionDrops` |
| Fillers | removed, never dropped |
| Firebox masonry | ordinary block drops; player-placed, the boiler never touches it |
| Water / steam held | lost |

`brokenDropsRatio` is not in the def - pinned absent by
`HpMegablockDropTierTests.Hp_machines_no_longer_carry_a_json_drop_ratio` (`:113-121`) so the number can only
come from config. The 80 % default is pinned by `Hp_machine_salvage_ratio_defaults_to_80_percent` (`:105-111`).

The salvage ratio is looked up by the broken block's `Code.Domain`, so hpex must register its own copy even
though lpex already registered an identical default (`HighPressureExpandedModSystem.cs:32-38`).

---

## Code

| Piece | file:line |
|---|---|
| `BlockBoilerLancashire : BlockBoiler, IFillerHost, IBoilerGeometry, IExBlockDefProvider` | `BlockStructures/Boiler/Blocks/BlockBoilerLancashire.cs:15` |
| `Definitions(domain)` → the single def | `:24-25` |
| `Lancashire(domain)` — the whole blocktype | `:27-163` |
| geometry attributes | `:36-55` |
| `FillerOffsets` (the 35-cell footprint) | `:56-82` |
| `MultiblockLayout` (the 90-cell verified envelope) | `:85-141` |
| `Construction` (four stages) | `:142-163` |
| `BlockEntityBoilerLancashire : BlockEntityBoiler` | `BlockStructures/Boiler/BlockEntities/BlockEntityBoilerLancashire.cs:12` |
| the six stat overrides | `:14-24` |
| `HpexConfig` § Lancashire boiler | `HpexConfig.cs:39-64` |
| grid recipe | `Recipes/Grid/MachineRecipeDefinitions.cs:29-37` |
| cost-catalogue keys | `HpexRecipeConfig.cs:50`, `:54` |
| salvage-ratio registration | `HighPressureExpandedModSystem.cs:32-38` |
| save migration off `lpex:` / `ppex:` | `BlockMigrations/HpexExtractionMigration.cs:34-46` — see [rolled pipe](rolled-pipe.md) § Gotchas 1, which owns that class |
| everything that runs | `LowPressureExpanded/BlockStructures/Boiler/BlockBoiler.cs`, `BlockEntityBoiler.cs` — [Cornish boiler](boiler-cornish.md) § Code |

### Where a caller hooks in

- Another boiler variant: implement the six abstract stats on `BlockEntityBoiler` (`:46-68`) and call
  `BoilerShell`. This leaf adds nothing but numbers, a footprint, a layout and stages - 25 lines of BE and
  164 of block def.
- Retuning the HP band: `LancashireBoilerMaxOutputPressure` is read live through `HpexValues`, so
  `/exmod config hpex` changes the choke, the hard cap and the burst trigger together - they are one number
  used three times.
- Making the vessel longer: five things must move together - the footprint layer bounds, the layout row
  count, `steamConnectorOffset`, `exhaustOutletOffset` and `waterRendererBox.z2`. Nothing checks the
  relationship.

### Tests — `test/HighPressureExpanded.Tests/`

| file | pins |
|---|---|
| `Definitions/HpMegablockDropTierTests.cs` | no self-drop · mining tier 4 · the full four-stage construction cost · salvage default 0.8 · no JSON `brokenDropsRatio` |
| `Definitions/HpexDefinitionGoldenTests.cs` | the def reproduces `goldens/hpex/blocktypes/boiler/lancashire.json` byte-for-byte; the golden set exactly covers the defs; every shape reference resolves |

There is no behavioural test for this boiler at all. No FSM test, no burst test, no scene: the entire
lpex boiler suite (`test/LowPressureExpanded.Tests/Blocks/Boiler/`) runs against the Cornish stat table, so
nothing exercises a 1200 L / 12 atm vessel. The 9.6 atm ceiling above is unasserted anywhere.

---

## Gotchas

1. The HP boiler's own structure is made of LP parts, and its steam main cannot be. The layout hard-codes
   `lpex:pipe-passthrough-fire-*`, `lpex:pipe-passthroughbend-fire-u*` and `lpex:pipe-outlet-fire-u`
   (`BlockBoilerLancashire.cs:89-95`). Those are `BlockPipe` subclasses in the flanged family, fine for the
   feedwater and exhaust lines (flanged too) but never part of the rolled steam run, and there is no hpex
   equivalent to swap in. The comment at `:83-84` frames this as a domain-naming choice, not a tier lock-in.

2. Connecting feedwater is what arms the boiler, and nothing says so. Hand-primed it tops out at 9.6 atm and
   cannot burst; piped, it reaches 12 atm in 150 s of boiling. The intake does not consider
   `InternalPressure`, so even a 1 atm manual-pump line does it. See [Operation](#operation).

3. `MinBoilWater` (200 L) is also the bucket-drain floor. `TryManualDrain` refuses below it
   ([Cornish boiler](boiler-cornish.md)), so 200 L of water is permanently unrecoverable - a third more than
   the Cornish's stranded 150 L.

4. The handbook is wrong on three of the four build figures (`docs/hpex/handbook/00-highpressure.html:12-15`,
   `assets/hpex/lang/en.json` `handbook-highpressure-text`):

   | handbook | actual | source |
   |---|---|---|
   | "36 steel plates" | 34 | `HpMegablockDropTierTests.cs:99` |
   | "24 nails-and-strips" | 24 yes | `:100` |
   | "12 rods" | 10 | `:101` |
   | "64 fire bricks" | 60 | `:102` |
   | "its firebox uses 39 fire-brick blocks" | 39 yes | glyph count |
   | "48 L/s and 12 atm, 1200 L vessel, needs 200 L to begin boiling" | all yes | `HpexConfig.cs:46-60` |

   The HTML ↔ lang copy is guarded by `HandbookParityTests`, but nothing compares either against the code, so
   these four numbers can drift freely ([handbook sync pipeline](../mechanics/recipes-config.md) is the
   pipeline; the drift is this page's).

5. The handbook and the README both instruct the player to fit a pressure valve between this boiler and the
   Cornish engine. They cannot (`src/HighPressureExpanded/README.md:20-23`,
   `docs/hpex/handbook/00-highpressure.html:27-30`, `docs/hpex/moddb.html:52-58`) - B6, owned in full by
   [cast pipes](cast-pipes.md) § B6, and silent in play because of B18 ([rolled pipe](rolled-pipe.md)).

6. The bigger vessel did not get a bigger flue or a bigger lid. `BoilerExhaustPerSecond` (16 L/s),
   `BoilerLidVentRate` (200 L/s), `BoilerSteamLeakRate` (16 L/s) and `BoilerShutdownCondenseRate` (200 L/s)
   are shared constants ([Cornish boiler](boiler-cornish.md) § Numbers), so every relief path on a Lancashire
   is proportionally two-thirds as strong as on a Cornish.

7. `CondenseInternal`'s anti-burst guard is unreachable here too - it refuses at 16 atm, the cap holds the
   vessel at 12. Owned by [Cornish boiler](boiler-cornish.md) Gotcha 1, which names the Lancashire.

8. The name "Cornish" spans two mods and two tiers. `lpex:boilercornish` is the low-pressure entry boiler;
   `hpex:enginecornish` is the high-pressure engine. A Cornish boiler cannot drive a Cornish engine (5 atm
   choke vs 6 atm engage at normal throttle) - see [Cornish boiler](boiler-cornish.md) Gotcha 3.

9. The blast radius grew but the blast-resistance threshold did not. `BoilerBlastResistanceThreshold` is 20
   and every machine in the suite is resistance 45, so a radius-4 Lancashire burst still cannot chain into a
   neighbouring boiler or engine - only into pipes, ports, coal piles and terrain. That is by design
   ([Cornish boiler](boiler-cornish.md)), but at radius 4 the blast reliably takes out the steam main itself,
   so a burst also disconnects the plant.

10. Nothing in hpex consumes rolled pipe, including this boiler. Its grid recipe asks for steel plate, rod
    and fire brick; its stages ask for the same; its structure asks for lpex fittings. The rolled tier is a
    peer of the machines, not a prerequisite for them ([rolled pipe](rolled-pipe.md)).

---

## Open

- No behavioural test. The whole boiler suite exercises the Cornish's numbers. A Lancashire rig would pin the
  9.6 atm hand-prime ceiling, the 150 s live-feed climb and the radius-4 burst.
- No editable shape. `assets/hpex/shapes/boiler/lancashire.json` is the only copy; the model cannot be
  re-edited from source (`docs/hpex/ASSETS-TODO.md` lists the listing art but not this).
- The lpex fitting lock-in needs a decision, not just a note (Gotcha 1). Either hpex ships passthrough /
  passthrough-bend / outlet in its own domain (which also fixes B6, since both the joint family and the
  pressure-valve ceiling read `Code.Domain`), or the legends become domain-wildcards and the tier distinction
  stops meaning anything at the firebox.
- Feedwater is the settled-but-unbuilt half of this machine. [pumps](pumps.md) fixes the injector as the one
  device that covers the whole band; the block is lpex's and does not exist. Until then the boiler is either
  inert at 9.6 atm or armed by any water line at all.
- The stage table's steel gate is half-applied (plates and rods steel-only, nails iron-or-steel). Decide
  whether the Lancashire is a steel build or a wildcard one and make all four ingredients agree - the
  handbook already calls it a steel build.
- The handbook needs a numbers pass (Gotcha 4); it is the only handbook page hpex ships, shared with the
  engine.
