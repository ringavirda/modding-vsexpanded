# Heat balance

**Status** live   **Mod** iwex (the model) · exlib (the shared `T_process` law and the HUD formatter)

**Owns** the furnace heat model and everything that reads it directly:
* the `T_process = T_in − T_loss` law as furnaces evaluate it, and every term in it - coke factor, air
  factor, preheat gain, radiation loss, charge-mass loss, ambient loss;
* the melt-speed factor and its clamp;
* `RequiredBlastPressureFor` and `TuyereDrawFor` - the burden → blast-demand mapping;
* the `Idle` / `Firing` / `Melting` labels and both ways a furnace arrives at one - the firebox branch's
  stored machine with its timers, disruption count and two extinguish thresholds, and the shaft branch's
  per-tick derivation, which has none of them;
* every config key and hard-coded constant in the Numbers tables below, including the firebox cadence
  (`Firebox{MaxFuelBurnTime / MeltStartDelay / MeltIntervalSec / HeatRatePerSecond / CoolRatePerSecond}`)
  and the melt points.

**Does not own** - cited only, never restated: burden composition and its flux bands
([burden](../items/burden.md)), blower output and pressure ceiling ([blower](../machines/twin-tub-blower.md)), pipe
capacity and burst ([pipe network](pipe-network.md)), blast preheat source
([cowper](../machines/cowper.md)), per-furnace yields, pools, taps and layouts
([blast furnace](../machines/blast-furnace-cold.md), [cupola](../machines/cupola.md),
[puddling furnace](../machines/puddling-furnace.md), [reheat furnace](../machines/reheat-furnace.md)),
and the extinguish residue payout ([ironmaking](../processes/ironmaking.md)).

**Depends on** [burden](../items/burden.md) · [pipe network](pipe-network.md) ·
[multiblock](multiblock.md) · [recipes & config](recipes-config.md)

---

## Role

Every fired machine in the suite - cold and hot blast furnace, cupola, puddling furnace, reheat furnace, and
(through the same exlib helper) the Bessemer converter - settles at a temperature instead of being given one.
There is no maximum temperature anywhere in the model: a furnace chases wherever the heat it makes and the
heat it loses balance. The two ceilings the furnace core once carried, 1420 °C natural and 1740 °C boosted,
are both emergent now; `test/IronworkingExpanded.Tests/Blocks/Furnaces/HeatBalanceTests.cs:76-78` records the
migration.

The cold and the hot blast furnace are therefore the same C# class (`BlockEntityBlastFurnaceCold.cs:12` is a
21-line subclass that only removes two gas-outlet cells). Hot blast is not a branch; it is a preheat term that
a [cowper](../machines/cowper.md) on the blast line contributes, and cold blast is that term being zero. A
coke-rich burden clearing iron's melt line while a coke-lean one stalls is an arithmetic consequence, not a
rule anyone wrote.

---

## How it works

### The law

`ExpandedLib/Process/HeatBalance.cs:55-80`:

```
T_process = max(ambient, T_in − T_loss)
```

The caller supplies `T_in` and `T_loss` in its own domain and the helper settles the floor and packages the
contributors for the HUD. Two callers exist: the furnace core (`BlockEntityFurnaceCore.cs:785`) and the
Bessemer converter
(`SteelmakingExpanded/BlockStructures/Converter/BlockEntities/BlockEntityConverterControl.cs:705`).

### Heat in

`BlockEntityFurnaceCore.cs:750-771`:

```
fuelFrac   = charge.HasContent ? charge.FuelFrac : BfDefaultFuelFrac
fuelFactor = clamp(1 + BfCokeSensitivity · (fuelFrac − BfReferenceFuelFrac) / BfReferenceFuelFrac,
                   BfMinFuelFactor, BfMaxFuelFactor)
airFactor  = BfNaturalDraughtFactor + (1 − BfNaturalDraughtFactor) · clamp(blastSupplyFrac, 0, 1)
preheat    = BfPreheatCoefficient · max(0, blastTemp − ambient)

T_in       = BfCombustionBaseTemp + BfCombustionCokeGain · fuelFactor · airFactor + preheat
```

At shipped values: `T_in = 950 + 900 · fuelFactor · airFactor + 0.35 · (blastTemp − ambient)`.

* `fuelFrac` on a shaft is the carbon-weighted fuel fraction of what stands in the columns: each fuel unit
  counts its `CarbonPerUnit` (coke 1.0, charcoal 0.5 - see § The raceway rate model), so the number is a
  carbon fraction, not a volume fraction of coke. The player sets it by how they lay their courses; it is
  the coke dial.
* `fuelFactor` is 1.0 at the reference 20 % carbon, caps at 1.25 for any charge ≥ 34.29 %, and floors at
  0.35 (unreachable at the shipped sensitivity - a fuel-free charge only falls to 0.65,
  `HeatBalanceTests.cs:151-171`).
* `airFactor` is 0.5 with no blast at all and 1.0 at full supply. It is a multiplier on the coke gain, so
  losing the blower costs 450 °C at the reference burden, not a proportional slice of the whole.
* `preheat` is added outside the product, so it is the only thing hot blast changes
  (`HeatBalanceTests.cs:124-138` asserts `FuelFactor` and `AirFactor` are identical cold and hot).

### Heat out

`BlockEntityFurnaceCore.ComputeHeatBalance`:

```
chargeLoss  = BfChargeLossFull · clamp(mixCount / max(1, ChargeCapacityUnits), 0, 1)
ambientLoss = BfAmbientLossPerDegree · max(0, BfAmbientReferenceTemp − ambient)

T_loss      = BfRadiationLossBase + chargeLoss + ambientLoss
```

At shipped values, a full hearth on a 20 °C day: `T_loss = 120 + 310 + 0 = 430`.

The charge-mass term inverts the naive intuition: topping a marginal furnace up makes it cooler. A thin charge
runs hotter but exhausts sooner, which is why the ledger is shown in the HUD at all
(`BlockEntityFurnaceCore.cs:1349-1354`). The ambient term is one-sided - a hot day gives nothing back
(`HeatBalanceTests.cs:212-231`).

`ambient` is sampled once per tick from the climate at the core's position, falling back to
`BfAmbientFallbackTemp` when the climate is unavailable - unloaded chunk, or the headless test accessor, which
returns null outright (`BlockEntityFurnaceCore.cs:412-419`).

### Worked results — full hearth, 20 °C ambient, full blast supply

`T_loss` is 430 in every row. Melt line for iron is `BfIronMeltingPoint` = 1482 °C.

| Charge | fuelFactor | airFactor | preheat | T_in | T_process | melts iron? |
|---|---|---|---|---|---|---|
| 10 % coke, cold blast | 0.825 | 1.0 | 0 | 1692.5 | 1262.5 | no |
| 20 % coke (the reference), cold blast | 1.000 | 1.0 | 0 | 1850.0 | 1420.0 | no - below break-even |
| 30 % coke, cold blast | 1.175 | 1.0 | 0 | 2007.5 | 1577.5 | yes, +95.5 |
| ≥ 34.29 % coke (factor capped), cold | 1.250 | 1.0 | 0 | 2075.0 | 1645.0 | yes, +163 |
| 20 % coke, 950 °C blast | 1.000 | 1.0 | 325.5 | 2175.5 | 1745.5 | yes, +263.5 |
| 10 % coke, 950 °C blast | 0.825 | 1.0 | 325.5 | 2018.0 | 1588.0 | yes, +106 |
| 20 % coke, blowers off | 1.000 | 0.5 | 0 | 1400.0 | 970.0 | no |

Every row is pinned by `HeatBalanceTests.cs:79-104`.

Break-even on cold blast at a full charge is 23.94 % carbon. Solve `950 + 900·fuelFactor − 430 ≥ 1482` →
`fuelFactor ≥ 1.0689` → `fuelFrac ≥ 0.2394`. The carbon fraction is the ratio of the two loads in every
course, so cold-blast ironmaking demands a richer charge than the 20 % reference - and the blast pressure and
draw demands move with it (below).

The other lever is the charge itself. At a half-full shaft `chargeLoss` is 155 and `T_loss` is 275, so a 20 %
charge settles at 1575 and melts fine - a thin charge runs hotter and exhausts sooner.

The cupola melts at 1200 °C (`CupolaCastIronMeltingPoint`), which a plain 20 % charge clears by 220 °C on cold
blast. That asymmetry is the tier gate: remelting scrap is an iron-age job, reducing ore is not.

### Melt speed

`BlockEntityFurnaceCore.MeltSpeedFactor` (`:1930`):

```
factor = max(0.01, clamp(1 + BfMeltMarginGain · (T_internal − meltPoint) / max(1, BfMeltMarginReference),
                         BfMeltSpeedMin, BfMeltSpeedMax))
```

The interval below is the firebox branch's. The melt cycle fires every `FireboxMeltIntervalSec / factor`
seconds there; the shaft sets `MeltsPerTick` instead, so the raceway renders whatever descended past it this
second and the cadence is the descent. The factor itself is shared - superheat buys speed on both - but a
blast furnace has no cycle length to wait out. At shipped values `factor = clamp(1 + 0.75·ΔT/200, 0.5, 2.0)`:

| ΔT above melt point | factor | blast-furnace interval (10 s ÷ factor) |
|---|---|---|
| 0 | 1.000 | 10.00 s |
| +95.5 (30 % coke, cold) | 1.358 | 7.36 s |
| +263.5 (20 % coke, 950 °C blast) | 1.988 | 5.03 s |
| ≥ +266.67 | 2.000 (cap) | 5.00 s |
| −133.33 or below | 0.500 (floor) | unreachable, see Gotchas #7 |

This is why a hot furnace out-produces a cold one: there is no second per-furnace yield constant. Setting
`BfMeltMarginGain` to 0 restores a flat rate.

### The raceway rate model — shaft branch

The shaft furnaces are counter-current machines: all combustion happens at the raceway in front of the
tuyeres, the hot gas rises and warms the descending charge, and a band's temperature is carried on the band -
it was warmed by coke that burned beneath it while it descended, never by spending its own. There is no oxygen
above the raceway, so nothing burns there.

One number throttles everything the furnace does in a second: carbon burned at the raceway.
`BlockEntityShaftFurnace.CirculateGas` burns up to
`BfRacewayCarbonPerTuyerePerSecond × tuyeres × ChargeUnitScale` charge units of fuel per second out of the
columns' raceway slices. Fuel is carbon, not bands: a unit's carbon is its `fuel` material-role value over
`BfFuelCarbonReference`
(`BlockEntityFurnaceCore.CarbonPerUnit` - coke 2/2 = 1.0, charcoal 1/2 = 0.5), so a charcoal course runs
cooler and shorter than a coke course of the same height. The spend is rotated across the columns rather than
divided between them: fuel burns in whole units, and an even split would truncate to nothing per tick and then
burn every column at once.

| Follows from the carbon burned | How |
|---|---|
| flame temperature | `_internalTemp` is assigned the raceway flame directly - the shaft's thermal inertia is the column warming through, not the flame |
| rising gas | carbon × `BfRacewayGasPerCokeUnit`, passed up the column; each charge unit absorbs `BfShaftGasTransferFrac ÷ ChargeUnitScale` of what passes it (`ChargeColumn.RiseGasThrough`) |
| descent | a consequence, never a rate: the column falls into the space burnt fuel and melted burden leave. A `units/second` descent constant would make the hang inexpressible |
| melt | burden rendered per tick = carbon burned × `BfBurdenPerCarbonUnit` × the melt-speed factor (`SmeltCycle`). There is no independent melt rate, and no second throttle beside the blast may be added |
| campaign length | a lit shaft runs until its carbon is gone. The shaft has no fuel clock, and none may be re-added as a second answer |

`ChargeUnitScale` is the one seam between the two unit currencies - a blast-furnace block holds 32 items, a
cupola block 3 000 metal units - and exactly two constants scale: the carbon rate with it, the gas-transfer
fraction inversely. Everything else is scale-free.

The melt condition is per band: burden melts iff the temperature it carried down the shaft clears the melting
point. The heat from the coke burning with it is already on the band - the rising gas put it there and the gas
is that coke's - so it must never be added as a second term; the double count would be observable only as a
furnace running richer than calibrated. Coke leaves a column by burning and by nothing else; the melt removes
burden only, mid-span, and both consumers work the same raceway span side by side.

The chill. A cold band stops its column rather than being skipped over - everything above rests on it - and
the column hangs (`IsHung` / `HungColumnCount`) while a better-fuelled neighbour keeps descending.
`AtMeltingTemperature` reads the first burden band at the raceway, the same band the melt walk stops at. There
is no "N columns hung" halt threshold and none may be added: a fully hung shaft renders nothing by arithmetic
and derives `Firing`, the honest label for a furnace burning its charge and making no iron. A hung column
never brings fresh carbon down, so a chilled furnace goes out within minutes on its own. Nothing about a hang
is stored. Coke burns whenever the furnace is lit, melting or not - sitting hot and rendering nothing is a
real way to waste a campaign.

`MeltSpeedFactor` multiplying the coke rate is the Neilson mechanic stated precisely: a hotter furnace makes
more iron from the same carbon, so a preheated furnace wants a leaner charge rather than merely tolerating
one.

Omitted: solution loss (the Boudouard reaction). Carbon is consumed above the raceway in a real furnace, but
endothermically - modelling it as burning would get the sign backwards on the one mechanism this model exists
to produce.

### Blast demand — derived from the burden, not from the furnace

Neither the pressure a furnace needs nor the air it draws is a property of the machine. Both come out of the
burden's coke fraction, and they move in opposite directions (`BlockEntityFurnaceCore.cs:130-147` carries the
rationale: coke is the permeable skeleton of the column, so a lean burden packs denser and resists the blast,
while a rich burden burns more oxidant).

`RequiredBlastPressureFor` (`:155-166`):
```
atm = clamp(BlastPressureThreshold + (BfReferenceFuelFrac − fuelFrac) · BfBlastPressureCokeSensitivity,
            BfBlastPressureMin, BfBlastPressureMax)
```

`TuyereDrawFor` (`:173-185`):
```
L/s = TuyereIntakeVolume · clamp(fuelFrac / BfReferenceFuelFrac,
                                 BfTuyereDrawMinFactor, BfTuyereDrawMaxFactor)
```

Blast furnace (2 tuyeres, 14 L/s base, threshold 2.0 atm):

| Burden coke | required pressure | draw per tuyere | draw total |
|---|---|---|---|
| 10 % | 2.75 atm | 7.0 L/s | 14.0 L/s |
| 20 % | 2.00 atm | 14.0 L/s | 28.0 L/s |
| 30 % | 1.25 atm | 21.0 L/s | 42.0 L/s |
| ≥ 30.67 % | 1.20 atm (floor) | — | — |
| ≥ 36 % | 1.20 atm | 25.2 L/s (cap) | 50.4 L/s |

The tier gate falls out of where those land against the [blower](../machines/twin-tub-blower.md)'s ceiling
(`IwexConfig.cs:777`, `:785`) and plated pipe's burst rating (`IwexConfig.cs:206`): the 30 % burden is
blowable by the iron tier and the 10 % one is not, by pressure and by plumbing at once. The 20 % standard
burden is comfortably inside both - nothing about the pressure model blocks it. What blocks it is heat.

`blastSupplyFrac = blastSupplied / (tuyereCount · perTuyereDraw)`, and it is 0 when a furnace declares no
tuyeres (`:532-533`), so a natural-draught furnace sits permanently at `airFactor` = 0.5.

### The state machine — there are two, and only one of them stores anything

`BlockEntityFurnaceCore.OnProductionTick`, one tick per second, server-side only. Navigate this section by
symbol, not by line number.

Each tick, in order: re-read the tunables (`CacheAttributes`) → vent exhaust and set `IsChoked` → one charge
walk → draw air and read blast → evaluate starvation → the state seam → count disruptions → apply the process
temperature and run the phase branch.

The seam is `DerivesState`, and everything between it and the `if (State != Idle)` block below is the
stored-state machine that only the firebox branch runs:

| | shaft (`BlockEntityShaftFurnace`: cold + hot blast furnace, cupola) | firebox (`BlockEntityFireboxFurnace`: puddling + heating hearth, reheat furnace) |
|---|---|---|
| how state is reached | derived every tick from the charge - `DeriveState(chargeHandle)` | stored, changed by transitions |
| ignition | positional and pneumatic - a complete raceway course of carbon, and air at pressure. No quantity threshold at all | quantity: the bed is lit when its cells are loaded (`ChargeCapacityUnits`) |
| timers | none. `MaxFuelBurnTime` / `MeltStartDelay` / `MeltIntervalSec` are `sealed … => 0` | all five |
| going out | `RacewayHoldsCarbon` fails → `Idle` on that tick; `Shutdown()` runs the residue and sounds only | disruption grace expires → `Extinguish()` |
| temperature | assigned directly - `_internalTemp` is the raceway flame temperature | first-order chase (§ Temperature tracking) |

The shaft's derivation, in full (`BlockEntityShaftFurnace.DeriveState`) - four lines of decision and no state
at all:

1. `IsChoked` ∨ ¬`RacewayHoldsCarbon` → Idle;
2. ¬`StructureComplete` ∧ already `Idle` → Idle - a breached furnace goes on burning but can never be
   re-lit, which is the only thing keeping breach and choke distinguishable. Without it, knocking a wall out
   would be a strictly better way to run one;
3. `ConversionBlocked` (charge the furnace does not recognise) → Firing, however hot it is. The label stays
   honest: a furnace that will render nothing is burning, not melting;
4. otherwise `AtMeltingTemperature` → Melting, else Firing.

`AtMeltingTemperature` reads the first burden band at the raceway, not any hot band anywhere. The melt walk
stops at the first band it cannot melt (the column above rests on it), so a scan for any hot burden reported
`Melting` - label, sound and HUD - over a furnace rendering nothing. That is the chill (§
[layered-charge](../layered-charge.md)), and both reads go through `RacewayBurden`.

The firebox's stored transitions:

| Transition | Trigger | Effect |
|---|---|---|
| Idle → Firing | `StructureComplete` ∧ `_cachedIsFull` ∧ ¬`IsChoked` ∧ `TryIgniteCharge` | snaps `_internalTemp` to `IgnitionTemp` = 900, zeroes `_fuelBurnSeconds`, plays the ignition whoosh |
| Firing → Melting | `T ≥ MeltingPoint` sustained `FireboxMeltStartDelay` ∧ ¬`ConversionBlocked` | zeroes `_meltSeconds` and `_fuelBurnSeconds` |
| Firing → Idle | `_fuelBurnSeconds ≥ FireboxMaxFuelBurnTime` | `Extinguish()` |
| Melting → Firing | `T < MeltingPoint` sustained `BelowMeltingReset` = 30 s | zeroes all three soak/melt/burn timers |
| any → Idle | disruption count held past its threshold, `OnStructureLost`, `OnBlockRemoved` | `Extinguish()` |

`_secondsAboveMelting` is a hard reset, not a decay: one tick below the melt point zeroes the whole soak.
While `ConversionBlocked` the soak timer keeps accruing but the transition is refused, so the furnace converts
the instant the offending pile is dug out.

Disruptions - firebox only (the whole block is behind `!DerivesState`); each adds 1 on the tick it is true:

| Disruption | Test |
|---|---|
| charge thinned out | `mixCount < DisruptionMixFloor` (144) |
| exhaust backfed into a tuyere | any tuyere pipe whose `Medium == "Exhaust"` |
| exhaust network full | `IsChoked` |
| product pool full | `LiquidCapacityReached` |
| air starvation | `RequiresBlast` ∧ `blastSupplyFrac < BfStarvationSupplyFrac` |

One disruption gives `ExtinguishThresholdDefault` = 30 s of grace. Two or more gives
`ExtinguishThresholdSevere` = 0, and `_extinguishSeconds += dt` happens before the comparison, so the furnace
dies on the same tick. A tick with zero disruptions resets the counter outright - there is no memory of an
earlier stall.

`Extinguish()` and `Shutdown()` are two things, and the split is what makes a derived branch possible:
`Shutdown()` does everything going out does - the sound, the reset to a literal `20f`, clearing `_airStarved`,
the residue, and zeroing all five counters - while `Extinguish()` is `Shutdown()` plus deciding the state is
`Idle`. A shaft whose derivation already returned `Idle` calls `Shutdown()`; it must not set a state it does
not own.

### Temperature tracking

Firebox branch only. `ApplyProcessTemperature` is a first-order chase toward `T_process`:

```
T += clamp(T_process − T, −FireboxCoolRatePerSecond·dt, +FireboxHeatRatePerSecond·dt)
```

The shaft branch overrides it and assigns directly, because its thermal inertia is carried on the charge
segments: `_internalTemp` is the raceway flame temperature, which follows the coke arriving in front of the
tuyeres with no lag of its own, and the slow part of the furnace is the column warming through. Two inertias
in series would be one too many, and the second one is invisible: the retired `MeltStartDelay` soak could
expire after a small furnace's campaign had already ended.

There is no absolute clamp on `_internalTemp` on either branch - `T_process` is already floored at ambient,
and a ceiling here silently capped the advertised hot-blast temperature.

### Away catch-up and live retuning

`MaxAwayCatchupSteps` = 600 (`:48`) with a 1 s sub-tick, so a furnace replays up to 10 minutes of the game
time it spent unloaded; each replayed `dt` still passes the 2× clamp
(`ExpandedLib/Blocks/Machines/BEBehaviorProductionMachine.cs:77`, `:146`). `CacheAttributes()` runs at the
top of every tick (`:430-433`), so `/exmod config iwex <key> <value>` takes effect on the next second - no
reload.

---

## Numbers

`IwexValues.X` is a generated accessor over `IwexConfig.X`; the file:line below is the config declaration.

### Heat balance — `src/IronworkingExpanded/IwexConfig.cs`

| Key | Value | file:line | What it does |
|---|---|---|---|
| `BfCombustionBaseTemp` | 950 °C | IwexConfig.cs:250 | Floor of `T_in` - what a lit charge holds before any coke credit or draught |
| `BfCombustionCokeGain` | 900 °C | IwexConfig.cs:253 | Coke's contribution at the reference ratio and full blast |
| `BfReferenceFuelFrac` | 0.20 | IwexConfig.cs:256 | Coke fraction the gain is calibrated at; also the pivot for pressure and draw |
| `BfCokeSensitivity` | 0.35 | IwexConfig.cs:259 | Slope of `fuelFactor` against coke ratio |
| `BfMinFuelFactor` | 0.35 | IwexConfig.cs:262 | Floor on `fuelFactor` - slack at shipped values |
| `BfMaxFuelFactor` | 1.25 | IwexConfig.cs:265 | Ceiling - binds at ≥ 34.29 % coke |
| `BfDefaultFuelFrac` | 0.20 | IwexConfig.cs:269 | Coke assumed for unstamped charge. Must equal `BfReferenceFuelFrac` |
| `BfDefaultFluxFrac` | 0.05 | IwexConfig.cs:273 | Flux assumed for unstamped charge, so it grades as standard not as a flux shortfall |
| `BfNaturalDraughtFactor` | 0.5 | IwexConfig.cs:276 | `airFactor` with no blast at all |
| `BfStarvationSupplyFrac` | 0.1 | IwexConfig.cs:283 | Supply fraction below which a lit blown furnace counts a disruption. 0 disables |
| `BfPreheatCoefficient` | 0.35 | IwexConfig.cs:287 | °C of `T_in` per °C the blast is above ambient - the whole hot-blast mechanic |
| `BfRadiationLossBase` | 120 °C | IwexConfig.cs:290 | Baseline stack radiation |
| `BfChargeLossFull` | 310 °C | IwexConfig.cs:300 | Cold-charge loss at a furnace loaded to `ChargeCapacityUnits` - see the note below |
| `BfAmbientReferenceTemp` | 20 °C | IwexConfig.cs:303 | Ambient the loss term is calibrated at; only colder costs |
| `BfAmbientLossPerDegree` | 1.0 | IwexConfig.cs:306 | °C lost per °C below the reference |
| `BfAmbientFallbackTemp` | 20 °C | IwexConfig.cs:309 | Ambient assumed when the climate lookup returns null |
| `FireboxHeatRatePerSecond` | 4 °C/s | IwexConfig.cs:312 | Climb rate toward `T_process`. Firebox only - renamed from `BfHeatRatePerSecond`; the shaft assigns directly |
| `FireboxCoolRatePerSecond` | 4 °C/s | IwexConfig.cs:315 | Fall rate toward `T_process`. Firebox only - renamed from `BfCoolRatePerSecond` |
| `BfMeltMarginReference` | 200 °C | IwexConfig.cs:467 | Superheat worth one full `BfMeltMarginGain` step |
| `BfMeltMarginGain` | 0.75 | IwexConfig.cs:471 | Melt-speed gained per reference step. 0 = flat rate |
| `BfMeltSpeedMin` | 0.5 | IwexConfig.cs:474 | Slowest melt multiple - unreachable via the melt cycle |
| `BfMeltSpeedMax` | 2.0 | IwexConfig.cs:477 | Fastest melt multiple |

> `BfChargeLossFull`'s denominator is `ChargeCapacityUnits` - derived from geometry and `sealed` on the
> shaft, so no leaf can re-introduce a hand-picked total. Caution: a number that is both the numerator's
> reference and the denominator can only be pinned from outside - `HeatBalanceTests` ties the literal to
> the geometry, and `FullHearth` must not be "tidied" back into a config read. A saturating clamp hides
> its own denominator, because every calibration row that passes the numerator at full charge pins the
> clamp at 1 whatever the denominator is. (A cold blast furnace holds 1 248 units - 39 chargeable
> cells × 32.)

### Raceway rate — `src/IronworkingExpanded/IwexConfig.cs`, shaft branch only

| Key | Value | file:line | What it does |
|---|---|---|---|
| `BfRacewayCarbonPerTuyerePerSecond` | 0.175 u/s | IwexConfig.cs:343 | Carbon burned per tuyere per second at full blast, in blast-furnace charge units |
| `BfBurdenPerCarbonUnit` | 4 | IwexConfig.cs:373 | Burden melted per unit of carbon burned - the coke rate, inverted. 4 is the 20 % reference read as a ratio, so a reference charge is eaten in the proportion it was laid |
| `BfRacewayGasPerCokeUnit` | 20 | IwexConfig.cs:393 | Gas risen per unit of carbon burned |
| `BfShaftGasTransferFrac` | 0.02 | IwexConfig.cs:409 | Fraction of the passing gas each charge unit absorbs; scales inversely with the unit size |
| `BfFuelCarbonReference` | 2 | IwexConfig.cs:435 | Fuel-role value that counts as one full carbon unit - coke's. Charcoal (1) burns as half; a fuel granted the role with no value defaults to 1.0, deliberately the under-performing direction |

### Blast demand — `src/IronworkingExpanded/IwexConfig.cs`

| Key | Value | file:line | What it does |
|---|---|---|---|
| `BfBlastPressureAtReference` | 2.0 atm | IwexConfig.cs:170 | Pressure a reference burden demands (the blast furnace's `BlastPressureThreshold`) |
| `BfBlastPressureCokeSensitivity` | 7.5 | IwexConfig.cs:177 | atm added per unit of coke shortfall against the reference |
| `BfBlastPressureMin` | 1.2 atm | IwexConfig.cs:180 | Floor - binds at ≥ 30.67 % coke |
| `BfBlastPressureMax` | 6 atm | IwexConfig.cs:183 | Ceiling - dead at shipped values, the raw maximum is 3.5 |
| `BfTuyereDrawMinFactor` | 0.4 | IwexConfig.cs:186 | Floor on the draw factor - binds at ≤ 8 % coke |
| `BfTuyereDrawMaxFactor` | 1.8 | IwexConfig.cs:189 | Ceiling - binds at ≥ 36 % coke |
| `TuyereIntakeVolume` | 14 L/s | IwexConfig.cs:573 | Blast furnace, per tuyere, at the reference coke fraction |
| `CupolaTuyereIntakeVolume` | 12 L/s | IwexConfig.cs:627 | Cupola, single tuyere |

### Cadence and melt points

The cadence keys belong to the firebox branch. The shaft seals all three at `0` - it has no fuel clock and no
soak, because the raceway rate model is its only throttle and the descent is its cadence. There are no
fire-threshold keys and no cupola cadence keys: a shaft ignites positionally, and the cupola's slower pace is
geometry (`ChargeUnitScale` stretching the same carbon rate over ~3 000 metal units a block), not a constant.

| Key | Value | file:line | What it does |
|---|---|---|---|
| `BfIronMeltingPoint` | 1482 °C | IwexConfig.cs:507 | Iron's melt line - the shaft's `AtMeltingTemperature` and the firebox's `Firing` → `Melting` |
| `FireboxMaxFuelBurnTime` | 1200 s | IwexConfig.cs:516 | Firing time before burnout. Does not run during `Melting` |
| `FireboxMeltStartDelay` | 300 s | IwexConfig.cs:519 | Soak above the melt point before `Melting` |
| `FireboxMeltIntervalSec` | 10 s | IwexConfig.cs:522 | Nominal melt-cycle period, divided by the melt-speed factor |
| `CupolaCastIronMeltingPoint` | 1200 °C | IwexConfig.cs:568 | Cupola threshold - cast iron's remelt point |
| `RollingTempC` | 900 °C | IwexConfig.cs:822 | Reheat furnace's threshold (a reheat target, not a melt point) - owned by [rolling](../processes/rolling.md) |

Config-migration rule, learned here (2026-08-06): key deletions need no reset row - there is no field to bind,
`nameof` on a deleted field will not compile, and an orphan key in an existing `ex_values.json` is ignored on
load. Key renames do need one - without it, a player who had retuned the old key silently gets the shipped
default under the new name while their value sits unread. The `0.3.0` row in `IwexConfig.Migrations` resets
the five `Bf*` → `Firebox*` renames for exactly that reason.

### Hard-coded — not config, editable only in source

All in `src/IronworkingExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs` unless noted. Every one of
these is a `virtual` member, so a subclass can override it - but no `/exmod config` key exists.

| Constant | Value | file:line | What it does |
|---|---|---|---|
| `ChargeCapacityUnits` | *derived* | :255 | `abstract`, not virtual - everything the furnace can hold, and the charge-loss denominator. `sealed` on the shaft; both branches derive it from geometry |
| `IgnitionTemp` | 900 °C | :258 | Temperature the hearth snaps to on ignition. Firebox only - the shaft has no ignition event |
| `DisruptionMixFloor` | 144 | :261 | Charge count below which a lit furnace counts a disruption. Firebox only |
| `RequiresBlast` | true | :270 | Whether air starvation applies; false on the natural-draught furnaces |
| `ExhaustVolumePerTick` | 24 L | :273 | Exhaust pushed through each gas outlet per tick |
| `ExhaustTempFactor` | 0.8 | :276 | Fraction of the internal temperature the exhaust carries |
| `ExtinguishThresholdDefault` | 30 s | :291 | Grace with exactly one disruption. Firebox only |
| `ExtinguishThresholdSevere` | 0 s | :294 | Grace with two or more - i.e. instant. Firebox only |
| `BelowMeltingReset` | 30 s | :297 | Cold-soak before `Melting` reverts to `Firing`. Firebox only - a derived branch just reads `Firing` again next tick, with nothing to reset |
| `DerivesState` | false | :399 | The branch seam. `true` on the shaft; everything guarded by `!DerivesState` is the stored machine |
| `MaxAwayCatchupSteps` | 600 | :67 | 1 s sub-ticks replayed on load → 10 min of away game time |
| `CompletionTickMs` | 3000 | :147 | Structure-completion re-check interval |
| shutdown reset temperature | `20f` | :1979 | Literal, not `_ambientTemp`, in `Shutdown()` |
| `_internalTemp` initial | `20f` | :109 | Literal |
| `_ambientTemp` initial | `20f` | :140 | Literal, overwritten on the first `CacheAttributes` |
| melt-speed absolute floor | `0.01f` | :1932 | Guards a retuned `BfMeltSpeedMin` of 0 from dividing by zero |
| `HeatBalance.IsHotBlast` epsilon | `0.5f` | `ExpandedLib/Process/HeatBalance.cs:46` | Preheat gain above which the HUD says "hot blast" |
| `ProductionTickMs` | 1000 | `ExpandedLib/Blocks/Machines/BlockEntityProductionMachine.cs:56` | Furnaces use the default; they do not override it |
| `MaxCatchupTickMultiple` | 2 | `…/BEBehaviorProductionMachine.cs:77` | Upper bound on a single `dt` |

### Cited, owned elsewhere

The charge-loss denominator is not a config key at all - it is `ChargeCapacityUnits`, above.
`TwinTubBlowerOutputPerSecond` / `TwinTubBlowerMaxPressure` and `PlatedPipeBurstPressure` are what the derived
demand is measured against - see [blower](../machines/twin-tub-blower.md) and [pipe network](pipe-network.md).
`BfIronPerOreUnit` and `BfSlagPerOreUnit` - what a melted burden unit renders - are [blast
furnace](../machines/blast-furnace-cold.md)'s.

---

## Code

| Type / member | file:line | Notes |
|---|---|---|
| `BlockEntityFurnaceCore` (abstract) | `…/Furnaces/BlockEntityFurnaceCore.cs:61` | The whole model. Extends `BlockEntityMultiblockStructure` |
| `ComputeHeatBalance` | :1867 | `protected`, not virtual - no furnace can override the law |
| `MeltSpeedFactor` | :1930 | `protected` |
| `RequiredBlastPressureFor` | :196 | `public` - callable from a blower/HUD to show what a burden will demand |
| `TuyereDrawFor` | :214 | `public`, same |
| `OnProductionTick` | :1517 | The `DerivesState` seam is at :1660; the stored machine is everything from there to the phase branch |
| `DerivesState` / `DeriveState` | :399 / :407 | The branch seam. `DeriveState` must be a pure read - the tick compares it with last tick's label, so a derivation with a side effect fires on every comparison |
| `TransitionToMelting` | :1947 | `private`; firebox path only |
| `Extinguish` / `Shutdown` | :1964 / :1975 | `Extinguish` = `Shutdown` plus setting `Idle`. A derived branch that already computed `Idle` calls `Shutdown` - it must not set a state it does not own |
| `CacheAttributes` | :1461 | `virtual` - a subclass calls base and adds its own; runs every tick |
| `ReadAmbientTemperature` | :1478 | Climate sample; null-safe |
| `ExtinguishResidue` | :2109 | `virtual`; `SolidifyBottomLayer` :2122, `BurnOutCharge` :2233 (`abstract` - each branch owns its own) |
| `ReadHeatBalance` / `WriteHeatBalance` | :2277 / :2299 | The whole balance rides the save tree - `GetBlockInfo` is client-side and the client never walks the charge or reads pipes |
| `AppendHeatBalanceInfo` | :2741 | Ledger + burden grade + melt-rate line |
| `BlockEntityShaftFurnace` (abstract) | `…/BlockEntities/BlockEntityShaftFurnace.cs:38` | The derived branch: raceway combustion, `DeriveState` :1031, `RacewayHoldsCarbon` :1062, the chill (`IsHung` / `HungColumnCount`) |
| `BlockEntityFireboxFurnace` (abstract) | `…/BlockEntityFireboxFurnace.cs:33` | The stored branch: the `Firebox*` cadence and a `sealed` capacity of `FireboxCellCount × FireboxMixPerCell` |
| `HeatBalance` record / `.Compute` | `ExpandedLib/Process/HeatBalance.cs:30` / `:55` | The shared `T_process` law |
| `HeatBalanceHud.AppendLedger` | `ExpandedLib/Process/HeatBalanceHud.cs:48` | Five shared lines; lang keys passed in so exlib stays content-free |
| `HeatBalanceLedgerKeys` | `…/HeatBalanceHud.cs:21` | The furnace's set is at `BlockEntityFurnaceCore.cs:2772` |

### Where a caller hooks in

Pick a branch first. A new furnace subclasses `BlockEntityShaftFurnace` (counter-current, derived state, no
timers) or `BlockEntityFireboxFurnace` (a fuel bed, stored state, the `Firebox*` cadence), never
`BlockEntityFurnaceCore` directly. The abstract members it must answer are `ChargeCapacityUnits` (:255),
`MaxFuelBurnTime` (:155), `MeltStartDelay` (:158), `MeltIntervalSec` (:161), `BurnOutCharge` (:2233) - and on
the shaft branch the first four are already `sealed`, so a leaf cannot re-introduce a hand-picked total. A
furnace runs on natural draught by setting `RequiresBlast = false` and drawing neither a tuyere nor an outlet
glyph, so `CellRole.Tuyere` and `CellRole.GasOutlet` answer empty. It does not touch the heat balance.

| Subclass | file:line | Branch | What it changes |
|---|---|---|---|
| `BlockEntityBlastFurnaceCold` | `…/BlockEntityBlastFurnaceCold.cs:19` | shaft | An empty class - `{ }`; the shared shaft behaviour lives on the base |
| `BlockEntityCupolaFurnace` | `…/BlockEntityCupolaFurnace.cs:29` | shaft | Cast-iron tunables, one tuyere, single-column shaft, ~3 000 u/block charge scale |
| `BlockEntityBlastFurnaceHot` | `SteelmakingExpanded/…` | shaft | Preheated blast; the gas outlets the cold furnace lacks |
| `BlockEntityPuddlingFurnace` | `…/BlockEntityPuddlingFurnace.cs:31` | firebox | Side firebox, no blast, no outlets |
| `BlockEntityHeatingFurnace` | `…/BlockEntityHeatingFurnace.cs:27` | firebox | Two-cell firebox, no blast, `MeltingPoint` = `RollingTempC` |
| `BlockEntityConverterControl` | `SteelmakingExpanded/…/BlockEntityConverterControl.cs` | — | Not a furnace - the other consumer of `HeatBalance.Compute` |

### Tests

`test/IronworkingExpanded.Tests/Blocks/Furnaces/HeatBalanceTests.cs` - the calibration anchor table
(`:79-104`), the melt-line consequences (`:105-123`), preheat isolation (`:124-138`), both clamps
(`:140-171`), the empty/overfull hearth (`:173-192`), unstamped charge (`:193-211`), and the one-sided ambient
term (`:212-231`). Also `FurnaceHudDistributionTests.cs` for which component block shows which slice.

---

## Gotchas

1. **Cold blast needs a richer-than-reference charge.** A 20 % carbon charge at a full shaft settles at
   1420 °C against the 1482 °C melt line; break-even is 23.94 %. That is the coke dial working, not a
   defect: the player lays richer fuel courses - and pays the pressure and draw those demand - or builds
   toward hot blast. The cupola (1200 °C) clears the line on a plain charge, which is the tier gate.

2. **A fuller hearth is a colder hearth.** `chargeLoss` scales with the charge count, so topping up a
   marginal furnace pushes it further from melting. Intended; visible in the HUD for exactly that reason
   (`:1349-1354`).

3. **Firebox branch: a melting furnace never burns out.** `_fuelBurnSeconds` is incremented only inside
   `if (State == FurnaceState.Firing)` (`:652-654`), and `TransitionToMelting` zeroes it (`:826`). Once a
   furnace reaches `Melting` and stays there, `MaxFuelBurnTime` is never evaluated again.

4. **Two disruptions kill instantly, and the HUD never says so.** `ExtinguishThresholdSevere` = 0 and
   `_extinguishSeconds += dt` precedes the comparison (`:589-601`). The countdown line always subtracts
   `ExtinguishThresholdDefault` (`:1326-1336`), so in the severe case it is computed and then never rendered
   because the furnace is already `Idle`.

5. **Air is consumed before it is validated.** `tuyere.TryConsume(perTuyereDraw)` runs unconditionally at
   `:508`; the `Medium == "Air"` and `Pressure >= requiredPressure` tests are at `:515`. A furnace on an
   under-pressure or wrong-medium main therefore drains it dry while counting zero supply - it starves and
   empties the line.

6. **`blastTemp` is a max, not an average** (`:517`). One preheated tuyere makes the whole blast read hot,
   however cold the others are.

7. **`BfMeltSpeedMin` = 0.5 is unreachable through the melt cycle** - that branch only executes when
   `_internalTemp >= _ironMeltingPoint` (`:683`, `:701-716`), so the factor is always ≥ 1.0 there. It is
   reachable in the HUD's melt-rate line (`:1376-1382`), which runs whenever `State == Melting`, including
   during the 30 s below-melt grace. So the number a player sees can drop below 100 % while the actual
   cadence does not.

8. **`BfBlastPressureMax` = 6 atm is dead.** At the shipped sensitivity the raw expression maxes at
   `2.0 + 0.20·7.5 = 3.5` for a coke-free burden. Only the floor ever binds.

9. **Stale source comment.** `MeltSpeedFactor`'s doc-comment says "This is where the design docs'
    cold ~30 u/s, hot ~45 u/s comes from". The shipped `BfIronPerMeltCycle` = 60 over
    `FireboxMeltIntervalSec` = 10 is 6 u/s nominal and 12 u/s at the cap - the ratio the comment
    describes (1.0 : 1.5) still holds, but the absolute figures are roughly 5× stale. The interval is
    also firebox-only: on the shaft the cadence is the descent, so this arithmetic does not describe a
    blast furnace at all.

10. **`Shutdown` forgets ambient.** It writes a literal `20f` (`:1979`) rather than `_ambientTemp` - the one
    place in the model that ignores the climate it otherwise samples every tick.

11. **`BfDefaultFuelFrac` must track `BfReferenceFuelFrac`.** The invariant is stated in the source
    (`IwexConfig.cs:198-200`) and asserted indirectly by `HeatBalanceTests.cs:193-211`, but nothing in the
    config system enforces it. Change one and every legacy/unstamped charge silently re-grades.

12. **The reheat furnace pins `fuelFactor` at its ceiling.** It reports `mix = new BurdenMix(0f, 0f, count)`
    (`BlockEntityHeatingFurnace.cs:84`), so `FuelFrac` is 1.0 by construction
    (`Items/Burden.cs:21`) and `fuelFactor` clamps to 1.25 on every tick. That is honest ("a firebox is all
    coke") but it means `BfCokeSensitivity` has no effect on any reverberatory furnace with a plain-fuel
    firebox.

13. **`_lastHeatBalance` is never cleared on extinguish** and rides the save tree (`:1166-1183`). Harmless
    today because the ledger only prints while `State != Idle`, but a stale balance is persisted.

14. **`ScanForOutlets()` is called from inside the loop over the list it reassigns** (`:451`, `:527`). Safe in
    C# - `foreach` holds the original list reference - but it re-resolves once per missing node per tick.

15. **Everything the HUD prints must be serialized.** `GetBlockInfo` runs client-side and the client never
    walks the charge or reads pipes, so `_cachedMixCount`, `_airStarved`, `_chargeMix` and the entire
    `HeatBalance` ride the tree (`:1118-1203`). A new HUD figure that is not written there prints zero.

---

## Open

1. **The puddling furnace lights but cannot melt.** It inherits `MeltingPoint` = `BfIronMeltingPoint` =
   1482 (`BlockEntityPuddlingFurnace.cs`) while, with no tuyeres, its `airFactor` is pinned to 0.5 -
   capping `T_in` at `950 + 900·1.25·0.5 = 1512.5` before any loss term, so `T_process` cannot reach
   1482; it needs its own process temperature. It also inherits the default `DisruptionMixFloor` of 144
   against a firebox holding cells × 12, so a lit hearth is simultaneously full enough to light and too
   empty to stay lit, and snuffs on the disruption grace. The puddling cycle itself is unbuilt.

2. **Firebox branch: should `Melting` consume fuel?** Today it does not (Gotchas #3). Either the fuel
   budget should run in both phases, or `FireboxMaxFuelBurnTime` should be documented as a warm-up budget
   rather than a run length.

3. **There is no preheat source in iwex.** The only thing that raises `blastTemp` is the smex
   [cowper](../machines/cowper.md), which is two tiers downstream - so at the iron tier every furnace is
   cold-blast by construction, and the preheat term is dead weight until steel. Whether iwex should get a
   modest recuperator is undecided.

4. **The reheat furnace holds heat but does not transfer it.** `SmeltCycle` is empty
   (`BlockEntityHeatingFurnace.cs:102-107`); soaking heat into stock on the hearth rows is the unbuilt half.

5. **No config keys for the FSM's disruption behaviour.** Ignition temperature, disruption floor, both
   extinguish thresholds, the below-melting reset and the exhaust factors are all hard-coded virtuals. Every
   other lever in the model is live-tunable; these are the exception.

6. **Per R7 "nothing is hidden", the ledger is complete but scattered.** It lives on the core block only, the
   melt-rate line appears only in `Melting`, and there is no window - a player must look at the right block.
   Whether the furnace is one of the two machines that earns a full GUI is undecided.
