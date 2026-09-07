# Open-hearth furnace

**Status** designed - nothing built. No block, no block entity, no metal def, no layout, no recipe, no
shape, no lang key, no handbook page. A repo-wide search for `openhearth` / `open hearth` in `src/` returns
zero hits.   **Mod** smex (`SteelmakingExpanded`)

**Owns** - the facts this page is canonical for:

* the machine's shape as designed: a shallow reverberatory bath over two regenerative chambers, gas and air
  reversing through them, and the fact that nothing plays over the bath but flame;
* the settled balance rule for the two steelmakers - tune rhythm, not rate - and its corollary that the open
  hearth may be slower per unit and still be mandatory;
* why its steel is low-N, and that the grade is the material identity rather than a simulated ppm;
* why it is the suite's only bulk-scrap sink, and the mechanical reason its scrap ceiling is time where the
  [Bessemer](bessemer.md)'s is temperature;
* bath alloying (D6) and the division of labour with the [ladle](ladle.md);
* the worked heat-balance argument that the open hearth must be modelled as a blown furnace, and the numbers
  showing that this is what saves it from the natural-draught ceiling that blocks B8 and B15;
* its proposed numbers and the existing constants each one is measured against;
* its dependency on producer gas, and exactly what producer gas needs from the pipe system.

**Does not own** - cited only, never restated:
[heat balance](../mechanics/heat-balance.md) - `T_process = T_in − T_loss`, every `Bf*` term, the melt-speed
factor, the Idle/Firing/Melting FSM and its timers · [Bessemer](bessemer.md) - the converter's carbon model,
its blast draw, its scrap gate, its capacity and its four-state tilt ·
[ladle](ladle.md) - mixing by held proportion, the chill model, recarburisation, waste alloy ·
[molten network](../mechanics/molten-network.md) and [molten canal](molten-canal.md) - the canal it taps into ·
[pipe network](../mechanics/pipe-network.md) - R1 single medium, pool volume, pressure, burst ·
[multiblock & fillers](../mechanics/multiblock.md) - the layout DSL and the filler footprint system ·
[recipes & config](../mechanics/recipes-config.md) · [cupola](cupola.md) and
[crucible furnace](crucible-furnace.md) - the other two melting machines ·
[blast furnace (cold)](blast-furnace-cold.md) - the pig source and the ferroalloy furnace ·
[long cell](long-cell.md) - what the steel is cast into · [materials.md](../materials.md) - what open-hearth
steel is and which parts require it · [STATE.md](../../internal/plans/STATE.md) - D3, D6, the blocker list

**Depends on** [heat balance](../mechanics/heat-balance.md) · [multiblock & fillers](../mechanics/multiblock.md) ·
[molten network](../mechanics/molten-network.md) · [pipe network](../mechanics/pipe-network.md) ·
[ladle](ladle.md) · [Bessemer](bessemer.md) · [recipes & config](../mechanics/recipes-config.md)

---

## Role

The steel tier's quality machine, and its only bulk sink for scrap.

Where the [Bessemer](bessemer.md) is a batch the player tends - charge, blow, pour, repeat, minutes at a
time - the open hearth is a single long heat set going and returned to later. A Bessemer blow is over in
about twenty minutes with no correction possible; an open-hearth heat runs eight to twelve hours and can be
held and corrected while it runs, which is why alloy and tool steels were made there.

Its product is open-hearth steel: low-C, low-N, the mod's pressure grade. Boiler plate, pressure parts and
HSS feedstock require it and Bessemer steel is barred ([materials.md](../materials.md)). The nitrogen grade
is the material identity - there is no ppm attribute and no nitrogen simulation, because it does not blow
air through the melt.

### The settled balance rule — rhythm, not rate

Do not balance the open hearth to the Bessemer's throughput. It does not need to win on throughput, because
it wins on a hard gate: Bessemer steel is barred from pressure parts by grade, so the open hearth may be
slower per unit and still be mandatory, and making it faster would delete the reason the Bessemer exists.

The axis to tune is rhythm. Set the per-hour rates roughly comparable (the open hearth may sit a little
below) and put the whole difference into heat size × heat duration.

| | [**Bessemer**](bessemer.md) | **Open hearth** |
|---|---|---|
| Rhythm | fast batch, tended throughout | one long heat, walk away |
| Heat size | one vessel-load | much larger |
| Fuel | none - autothermal | producer gas, continuously |
| Scrap | limited, capped by temperature | bulk - its economic argument |
| Alloying | none | in the bath (D6) |
| Product | high-N: cheap structural | low-N: the required grade |
| Sells | volume | guarantees |

No bootstrap risk: wrought iron also passes the pressure gate ([materials.md](../materials.md)), so the
first LP boiler is riveted wrought plate and never waits on an open hearth. The gate bites at HP, hadfield
and HSS.

The two coexist permanently, on what the product must guarantee rather than on tier: Bessemer and open
hearth ran side by side for decades and neither displaced the other until the electric arc furnace.

| The product needs… | Route |
|---|---|
| to be strong, and there is a lot of it | [Bessemer](bessemer.md) - volume, structural, one grade |
| to hold pressure, hold an edge, or hit a composition | Open hearth - quality and alloys |

The [ladle](ladle.md) is untouched by any of this: it stays the one mixing block (R3). The open hearth
supplies a controllable base, the ladle does the proportioning. One mechanic each, no overlap.

---

## Structure

Nothing is drawn and nothing is laid out. What follows is the design, with the existing part each piece
would be built from.

```
        ┌──────────────────────────────────────┐
        │            CHARGING FLOOR            │   charge doors along the front
   ═════╡  ▒▒▒▒▒▒▒▒ shallow BATH ▒▒▒▒▒▒▒▒  ╞═════   flame plays OVER the bath
   gas  │                                  │  gas
   air  ├──────────────┬───────────────────┤  air     ↕ the pair REVERSES
        │  regenerator │    regenerator    │
        │   chamber A  │     chamber B     │        checker brick, both sides
        └──────────────┴───────────────────┘
                       ▼ tap
```

| Part | Job | Build it from |
|---|---|---|
| hearth / bath | the shallow pool of metal; every interactive cell the player charges through | the reverberatory hearth idiom - `BlockPuddlingHearth.cs:25-30`, `BlockHeatingHearth.cs:43-54` (the clicked cell picks the slot) |
| charge doors | scrap and pig go in here, not down a shaft | `game:cokeovendoor*` is already a layout legend on the cowper (`BlockCowperStoveIntake.cs:56`); the puddling charge door is a modelled block |
| regenerator chambers ×2 | soak flue heat on one half-cycle, give it back to the incoming gas + air on the other | the cowper stove, exactly - `BlockEntityCowperStove.cs:25` already models a checker-brick regenerator: internal temperature climbs from an exhaust network (`:175-186`), bleeds into the medium it reheats (`:215-219`), capped by `CowperMaxTemperature` (`:64`), with heat-sink blocks reading the value out (`:279-290`) |
| gas + air intakes | producer gas one side, air the other | `siex:cowperstove-intake*` and `iiex:pipe-passthrough-*` are already layout legends (`BlockCowperStoveIntake.cs:52-55`) |
| reversing valve | the one control the machine has: swap which chamber fires and which soaks | the damper-on-a-lever idiom - `BlockEntityPuddlingChimneyCap.cs:19-33` (`IsOpen` / `Toggle`, held `idle`/`open` poses) |
| metal tap | one tap into a canal | `iiex:moltenmetaltap*`, unchanged ([molten canal](molten-canal.md)) |
| slag | skimmed or tapped separately | `iiex:slag`, as every other furnace makes |

The regenerative block family already exists: the cowper is the same device applied to air only. The open
hearth applies it to gas and air together, and adds the reversal.

Origin and orientation follow the furnace-core convention: the anchor is a `BlockFurnaceCoreBase.Core` cube
at the bottom centre, `side` variant drives the structure angle
([blast furnace § Orientation](blast-furnace-cold.md#orientation)).

One layout problem is already known. A `MultiblockLayout` is a fixed cell table
([multiblock](../mechanics/multiblock.md)), so "two chambers of n courses each" is not expressible any more
than the [crucible furnace](crucible-furnace.md)'s variable stack is. Either the chambers are fixed-size
(recommended - the open hearth's scaling axis is heat size, not chamber count) or the machine needs the
counted-scan approach that page proposes.

---

## Assets

| Asset | State |
|---|---|
| editable shape | missing - `workbench/shapes/` holds no open-hearth file |
| runtime shape | missing; `mods/siex/assets/siex/shapes/` holds `converter/`, and the cowper/smokestack/engine sets - nothing else |
| textures | missing. The furnace core's south face carries a two-letter type label (`BlockFurnaceCoreBase.cs:34-68`); the cupola's `cf.png` is the precedent, and an open hearth needs its own so a built one reads apart |
| metal def | missing - `mods/siex/assets/siex/config/metals/` contains only `bessemersteel.json` |
| producer-gas medium | missing - `exlib/assets/exlib/config/liquids.json` declares `Air`, `Steam`, `Exhaust`, `Water` and nothing else |
| lang / handbook | no key, no page |

Reusable art: the cowper's regenerator chamber, `game:cokeovendoor*`, the puddling chimney cap, the
refractory-brick course family the smokestack accepts (`BlockSmokeStackIntake.cs:50-53`), and the
`moltenmetaltap` shape.

---

## Construction

No recipe. Proposed, and expensive - this is the machine that says the steel tier is finished:

| Part | Proposed cost | Modelled on |
|---|---|---|
| hearth core | refractory brick ×4 + 2 rod + 1 plate + fire clay | `FurnaceRecipeDefinitions.cs:68-80` (the cupola core) |
| hearth cells | tier-3 refractory brick, in bulk | the vessel's own RCC stages, `BlockConverterBessemer.cs:112-121` |
| regenerator chambers | tier-3 refractory brick + `siex:cowperstove-intake` ×2 | `SiexRecipeConfig.cs:61-62` |
| reversing valve | plate + nails on the chimney-cap chassis | `ExIngredients.cs:36` |
| tap | `iiex:moltenmetaltap` | unchanged |

Cost keys `openhearthcore-grid` and `openhearthreverser-grid` would go in
`SiexRecipeConfig.DefaultCatalogue` (`SiexRecipeConfig.cs:45-70`).

Its real prerequisite is not a recipe but the gas producer, which is also unbuilt (see Open #1).

---

## Operation

```
pig (canal or ingot) + BULK SCRAP + flux  ──▶  BATH  ◀── producer gas + regenerated air
                                                 │
                                     ┌───────────┴──────────┐
                                     ▼                      ▼
                        open-hearth steel (canal)         slag
```

### Inputs → outputs

| In | Out |
|---|---|
| molten pig from a canal, or solid pig / cast iron charged through the doors | molten open-hearth steel down the tap |
| bulk steel scrap - any `Roles.Scrap` item, in stacks, not in bits | molten slag (`iiex:slag`) |
| flux (lime) - the open hearth is basic practice, unlike the acid Bessemer | spent flue gas → the same exhaust network the cowpers feed |
| producer gas, continuously, on a pipe | - |
| regenerated air | - |

### The player's verbs

| Verb | Where | Effect |
|---|---|---|
| RMB with pig / scrap / flux | a charge door | add to the bath. Bulk, by the stack |
| RMB the reversing lever | the chamber front | swap fire and soak. The one control the machine has |
| RMB with a ferroalloy | a charge door, during the heat | bath alloying (D6) - see below |
| RMB the tap with an empty hand | the tap | tap into a canal, once a start is in place |

### It has no blast, and that is the identity

Air goes through the regenerators, not through the melt. That single fact produces every difference from the
Bessemer:

* no nitrogen pickup ⇒ low-N steel ⇒ the pressure grade;
* an external, continuous heat source ⇒ the scrap limit is completely different (below);
* a long, controllable bath ⇒ alloying and correction are possible at all.

### Bath alloying (D6, settled)

Alloying happens in the open-hearth bath, primarily. The [ladle](ladle.md) can do it too - a second option,
not a replacement.

Ferroalloy and carbon trims go in during the heat and the player watches the composition move. The
proportion arithmetic, the off-spec → waste-alloy rule and the chill model belong to the [ladle](ladle.md)
and must not be re-derived here; the open hearth's only addition is that its heat lasts long enough for
corrections to be worth making, and that its flame keeps the bath hot while they are made.

D3 applies: alloys inherit their base's grade as a continuous penalty, not a lockout - hadfield mixed on a
Bessemer base gets a lower max pressure than hadfield mixed on an open-hearth base
([STATE.md](../../internal/plans/STATE.md) D3).

### Scrap: the ceiling is time, not temperature

The [Bessemer](bessemer.md)'s scrap cap is emergent from having no external heat: cold scrap raises `T_loss`
against a fixed autothermal `T_in`, so past ~857 u the bath cannot stay above its refine floor and the heat
stalls. The open hearth has a flame. Cold scrap still raises `T_loss` through the same `chargeLoss` term the
[heat balance](../mechanics/heat-balance.md) already computes, but the flame keeps supplying `T_in`, so the
bath does not stall - it just takes longer to get there. Bessemer scrap costs the heat; open-hearth scrap
costs the clock.

Mechanically this is the existing melt-speed factor doing its job, and no new system is written: more cold
mass ⇒ lower `T_process` ⇒ a smaller superheat margin ⇒ a longer melt interval
([heat balance](../mechanics/heat-balance.md) § Melt speed). It is R5 exactly - the threshold gates speed,
never possibility.

It is the only bulk sink for failed castings, crop ends and worn parts, so it closes the loop R2 wants
without a separate recycling mechanic.

---

## Numbers

All proposed. No config section, no keys, no code. The right-hand column is what exists and what each
proposal is measured against.

### Worked first: the open hearth must be a **blown** furnace, or it cannot melt

If it is built as a natural-draught reverberatory - the obvious reading of "no blast" - it inherits the
ceiling that already blocks the puddling furnace (B8) and the crucible furnace (B15). Against
[heat balance](../mechanics/heat-balance.md)'s own terms, with a pure-fuel firebox reporting
`fuelFrac = 1.0` the way the reheat furnace does (`BlockEntityHeatingFurnace.cs:84`, so `fuelFactor` clamps
to `BfMaxFuelFactor`):

| Model | `fuelFactor` | `airFactor` | preheat | `T_in` | `T_loss` | `T_process` | Melts steel? |
|---|---|---|---|---|---|---|---|
| natural draught, no regenerator | 1.25 | 0.5 | 0 | 1512.5 | 430 | 1082.5 °C | no - 400 short of iron's own 1482 line |
| blown, cold air | 1.25 | 1.0 | 0 | 2075 | 430 | 1645 °C | |
| blown + regenerator at the cowper's 1240 °C cap | 1.25 | 1.0 | 427 | 2502 | 430 | 2072 °C | with margin |

Terms: `BfCombustionBaseTemp` 950, `BfCombustionCokeGain` 900, `BfMaxFuelFactor` 1.25,
`BfNaturalDraughtFactor` 0.5, `BfPreheatCoefficient` 0.35, `BfRadiationLossBase` 120, `BfChargeLossFull` 310
(`IiexConfig.cs:181`, `:184`, `:196`, `:207`, `:218`, `:221`, `:225`); `CowperMaxTemperature` 1240
(`SiexConfig.cs:121`).

A Siemens furnace is blown - the regenerators are how the air gets there. So `RequiresBlast => true` with
the regenerator feeding the intake is both the correct model and the one that works, and this machine does
not need the stack-height draught function the [crucible furnace](crucible-furnace.md) is blocked on.

The regenerator therefore buys speed, not possibility - R5 exactly. Cold-air open hearth: 1645 °C, a 163 °C
margin over iron's melt line. Regenerated: 2072 °C, which the melt-speed factor caps at 2.0×
(`BfMeltSpeedMax`, `IiexConfig.cs:253`). Banking the producer down never stops the furnace; it halves it.

### Proposed keys

| Key | Proposed | Measured against (file:line) | What it does |
|---|---|---|---|
| `OpenHearthCapacity` | 24 000 u = 8 slab pours | `BessemerConverterCapacity` 4800 (`SiexConfig.cs:145`), settled 6000 | "much larger per heat" as a number - 4× the converter |
| `OpenHearthMeltingPoint` | 1500 °C | `BfIronMeltingPoint` 1482 (`IiexConfig.cs:271`); `BessemerRefineTemperature` 1500 (`SiexConfig.cs:194`) | steel's liquidus, same figure the converter refines above |
| `OpenHearthMeltIntervalSec` | 60 s | `BfMeltIntervalSec` 10 (`IiexConfig.cs:286`), `CupolaMeltIntervalSec` 20 (`:333`) | the slow cadence - 6× the blast furnace's |
| `OpenHearthSteelPerMeltCycle` | 400 u | `BfIronPerMeltCycle` 60, settled 200 (`IiexConfig.cs:289`) | ⇒ 6.7 u/s nominal, 13.3 u/s at the melt-speed cap |
| `OpenHearthSlagPerMeltCycle` | 40 u | `CupolaSlagPerMeltCycle` 8 (`IiexConfig.cs:340`) | 10 % - basic practice makes more slag than the acid converter's 6 % |
| `OpenHearthGasPerSecond` | 24 L/s | `CowperIntakeVolume` 24 (`SiexConfig.cs:139`), `BessemerBlastPerSecond` 8 (`:148`) | continuous fuel draw; three times the converter's blast |
| `OpenHearthAirPerSecond` | 24 L/s | same | the regenerated air side |
| `OpenHearthScrapFraction` | no key | - | absent by design. The ceiling is the existing charge-loss/melt-speed arithmetic, not a number |
| `OpenHearthReversalSeconds` | 300 s | `CowperCoolingSpeedExhaust` 0.3 / `CowperCoolingSpeedAir` 0.0012 (`SiexConfig.cs:133`, `:136`) | how long a chamber fires before the lever should be thrown |
| `OpenHearthMaxTemperature` | reuse `CowperMaxTemperature` 1240 | `SiexConfig.cs:121` | regenerator cap; a separate key only if the OH is meant to out-preheat a cowper |

### The rhythm arithmetic these produce

| | [Bessemer](bessemer.md) (shipped, at 6000 u) | Open hearth (proposed) |
|---|---|---|
| fill | ~120 s (canal-limited at 50 u/s) | ~ door-charged, player-paced |
| process | 297 s blow | ~54 min (21 600 u ÷ 6.7 u/s) |
| pour | 123 s steel + 9 s slag | ~7 min at the same tap ceiling |
| cycle | ≈ 9.2 min, hands-on throughout | ≈ 60 min, unattended |
| steel per cycle | 5400 u | 21 600 u |
| nominal u/h | ≈ 35 200 if perfectly tended | ≈ 21 600 |

That lands where the settled rule asks: the open hearth sits a little below on paper, and above in practice,
because nobody tends a Bessemer perfectly for an hour. The difference is entirely heat size × duration, and
neither number is a "quality multiplier".

Do not tune these against each other until the Bessemer's own capacity question is settled -
[bessemer § Open #2](bessemer.md#open) shows 6000 u of pig yields only 5400 u of steel.

### Producer gas — what it needs

The open hearth's fuel is the [gas producer](gas-producer.md) (designed, unbuilt). What it requires of the
existing systems is small and specific:

| Need | Status |
|---|---|
| a `producergas` medium | a new entry in `exlib/assets/exlib/config/liquids.json` (phase `gas`, its own priority), loaded by `ExLiquids.Load` (`ExpandedLib/Fluids/ExLiquids.cs:74`). The shipped file has 4 media |
| a pipe run from producer to hearth | works today - [pipe network](../mechanics/pipe-network.md) |
| R1: one medium per network | a producer-gas main and an air main are two separate networks; they cannot share a pipe. The open hearth therefore needs two intakes on two runs, exactly as the cowper does |
| never stored | by design - no gasholder. Producer gas carries about a tenth of town gas's heating value, was fed hot so its sensible heat reached the regenerators, and is largely CO. Direct supply also makes the producer a live dependency: bank it down and the hearth cools |

This is the steam tier's only genuine role in steelmaking beyond driving the blower: the producer needs
steam injected into its bed, so no boiler, no producer gas, no open hearth.

---

## Drops

Proposed; every rule already exists on another machine.

| Broken | Returns |
|---|---|
| hearth core | itself, `{tier}-{side}` variant - the furnace-core rule, no `GetDrops` override (`BlockFurnaceCoreBase.cs:63`) |
| hearth cells / chamber courses | ordinary bricks |
| reversing valve, intakes, tap | themselves |
| a live bath | undecided. The [Bessemer](bessemer.md#drops) drops a solidified charge at 5 u per bit minus a random mangling loss and voids a liquid one. A 24 000 u bath is 4× a converter-load; voiding it silently is the worst outcome in the design. Recommend the furnace-core extinguish freeze instead (solidify onto hearth cells, `BlockEntityFurnaceCore.cs:986`), which is already the pattern and leaves the metal visible |

---

## Code

Nothing exists. `grep -ri "openhearth\|open hearth" src/` returns no hits at all.

| Piece | Where it would go | Model it on |
|---|---|---|
| `BlockOpenHearthCore` | `mods/siex/src/BlockStructures/OpenHearth/Blocks/` | `BlockCupolaFurnaceCore.cs:20-127` - the shortest complete furnace: `Core(domain, code, path, tiers…)`, then `.Class` / `.EntityClass` / faces / `.MultiblockLayout` |
| the layout | `.MultiblockLayout(s => s.Origin(…).Legend(…).Layer(…))` | `BlockCowperStoveIntake.cs:49-110` - the closest sibling; negative-Y layers are legal and used |
| `BlockEntityOpenHearth` | `…/OpenHearth/BlockEntities/` | `BlockEntityCupolaFurnace.cs:28` (a furnace that is only property overrides) for the tunables shape; `BlockEntityBlastFurnace.cs:31` for the molten pools + taps |
| regenerator chambers | reuse | `BlockEntityCowperStove.cs:25` - soak (`:175-186`), give back (`:215-219`), cap (`:64`), heat-sink readout (`:279-290`) |
| the reversing lever | reuse | `BlockPuddlingChimneyCap` / `BlockEntityPuddlingChimneyCap.cs:19-33` |
| charge doors / hearth cells | reuse | `BlockPuddlingHearth.cs:25-30`, `BlockHeatingHearth.cs:43-54` - the clicked cell picks the slot |
| bulk-scrap acceptance | `MaterialRoleRegistry.IsRole(Roles.Scrap, stack)` | the converter already classifies scrap by role, not by path (`BlockEntityConverterControl.cs:619`) - reuse it and take whole stacks instead of `BessemerScrapUnitValue` bits |
| metal def | `mods/siex/assets/siex/config/metals/openhearthsteel.json` | `bessemersteel.json` is the template. decide `generateItemFamily` and `tools` explicitly - see Gotchas |
| heat | inherited, unmodified | override `MeltingPoint`, `MaxFuelBurnTime`, `MeltStartDelay`, `MeltIntervalSec`, `TuyereIntakeVolume`, `BlastPressureThreshold`, `BlastMixRequiredToFire` (`BlockEntityFurnaceCore.cs:111-128`) and nothing else |
| the tap | already works | `iiex:moltenmetaltap*` ([molten canal](molten-canal.md)) |

### Where a caller hooks in

The open hearth is the fourth instance of the same override pattern the cupola documents
([cupola § Where a caller hooks in](cupola.md#where-a-caller-hooks-in)): charge family, product identity,
tunables, cell geometry, and nothing else. The one genuinely new piece is the pair of regenerators and the
reversal between them.

The second new piece is not a machine at all: `HeatBalance` currently derives `airFactor` from tuyere
supply. An open hearth's air arrives through a regenerator, so either the chamber presents itself as a
tuyere (cheapest, since it already reads a pipe) or `ComputeHeatBalance` gains a second supply source. The
cowper already exposes its temperature to neighbours via heat-sink blocks (`BlockEntityCowperStove.cs:279-290`),
which is the existing precedent for reading a regenerator's heat from outside it.

### Tests

None. When built, the shape of the suite is set by
`mods/siex/tests/Fixtures/SteelPlantScenes.cs:31` (`ConverterRig`) and
`mods/iiex/tests/Scenarios/CupolaScenarioTests.cs` - build the real footprint through
`StructureRig`, never force `StructureComplete`, and drive the machine's own production tick.

---

## Gotchas

1. The natural-draught reading of "no blast" is fatal. It gives `T_process` 1082.5 °C, which is 400 °C short
   of iron's melt line, let alone steel's. This is the same failure as B8 (puddling) and B15 (crucible). See
   Numbers: the open hearth is blown, through its regenerators.

2. "Regenerative" must not become a second heat model. It is the existing `BfPreheatCoefficient` term with a
   different source - the same one hot blast uses. Anything else duplicates
   [heat balance](../mechanics/heat-balance.md).

3. `BfChargeLossFull` makes a fuller bath a colder bath (`BlockEntityFurnaceCore.cs:773-776`). That is
   correct for a burden column and doubly right here - it is the scrap-slows-the-heat mechanic - but it
   means charging scrap during a heat has a real, visible cost. Do not "fix" it.

4. Do not give open-hearth steel a tool family without deciding to. `bessemersteel.json` ships
   `generateItemFamily: true` and `tools: { preset: "good" }`, which is why Bessemer steel currently makes
   pickaxes in contradiction of [materials.md](../materials.md). Open-hearth steel is the pressure grade;
   tools come from shear / crucible / HSS.

5. The exhaust side already has a consumer and a vent. Spent flue gas should join the same exhaust network
   the cowpers soak and the smokestack vents (`SmokestackGasIntakeVolume` 48 L/s, `SiexConfig.cs:255`), not
   a private one. A furnace whose exhaust network is full counts a disruption
   ([heat balance](../mechanics/heat-balance.md)), so an open hearth on an undersized stack will die.

6. R1 forces two pipe runs. Producer gas and air cannot share a network. Every intake count, every pressure
   and every burst check doubles.

7. The scrap ceiling must stay emergent. A hardcoded `OpenHearthScrapFraction` would be the third time the
   suite reached for a constant where the model already produces the behaviour (the Bessemer's cap is
   emergent and documented as such; the crucible's batch size is its build).

---

## Open

1. Its fuel does not exist. The [gas producer](gas-producer.md) is unbuilt, the `producergas` medium is
   unregistered, and the whole "steam tier matters to steelmaking" argument rests on it. The producer should
   be built first, because an open hearth with no gas is a furnace that cannot light.

2. How large is "much larger"? The proposal is 4× the converter (24 000 u). That interacts with three
   unsettled things: the Bessemer's own capacity ([bessemer § Open #2](bessemer.md#open)), the long cell's
   pour sizes ([long cell](long-cell.md)), and the canal's 50 u/s throughput, at which 21 600 u takes
   7 minutes just to tap.

3. Where does bath alloying show? D6 settles that it happens here; nothing settles what the player reads. R7
   says nothing is hidden, so the bath needs a live composition readout - and that readout's format should
   be the [ladle](ladle.md)'s, shared, not a second one.

4. Flux. The open hearth is basic practice and the [Bessemer](bessemer.md) is acid by design, so flux is
   a real input here and nowhere else in the steel tier. Whether it is a burden-family concern
   (`Burden.cs`), a hand-charged item, or simply folded into the slag yield is undecided.

5. Does it take a liquid charge, a solid one, or both? The design rule for the steel furnaces is heat, not
   ore - a liquid charge of molten pig from a ladle, for both converters. Bulk scrap is necessarily solid. A
   machine that accepts both is right and is also two charge paths.

6. HSS feedstock is open-hearth-exclusive ([materials.md](../materials.md)) and elex is out of scope
   ([STATE.md](../../internal/plans/STATE.md) D8). So one of this machine's three stated jobs has no consumer in the
   current release target - worth knowing before sizing it around that job.

7. No `MaxAwayCatchupSteps` decision. A one-hour heat is exactly the machine a player walks away from, and
   the furnace core already replays up to 10 minutes of unloaded game time
   ([heat balance](../mechanics/heat-balance.md)). 10 minutes against a 60-minute heat is the wrong ratio;
   the [Bessemer](bessemer.md) replays none at all. This machine forces the question.
