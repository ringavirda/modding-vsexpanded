# Furnace and machine rebalance

**Status** EXECUTED in the 0.9.7 shipping line. Plan of record: the numbers here are what shipped, so it
is the reference for any furnace-side rebalance rather than a task list.

Plan of record for the 0.9 shipping line. Written 2026-08-09, before implementation.

Everything below is a maintainer ruling. Every number is settled; every phase 0 read is done.

## Purpose

Three player complaints and one exploit, plus the machine rebalance they pull in:

1. Blast mix turns to slag when the furnace goes out. Material is lost for no design reason.
2. The furnace sits at its ceiling waiting on conditions the player cannot see.
3. The blast furnace is no better than a bloomery.
4. Crushing iron bits back into crushed ore duplicates iron once the furnace yield rises.

## Standing rulings

- **Cold blast always works.** It melts, slowly, with a small margin over the melting point. Hot
  blast is an improvement - faster heating, a higher ceiling, faster melting - never a requirement.
- **Nothing is destroyed as punishment.** No slag conversion anywhere. A lit pile with no air simply
  goes out and stays blast mix.
- **Melting is a rate, not a gate.** Heat and air both meter it, and the rate is visible.
- **The converter is the remelter.** Scrap goes there, not into the furnace.
- **Every process constant is player-configurable.** No behavioural literals in code.
- Windmills are out of scope: wind-dependent, flax-expensive, and superseded by the waterwheel.

## Phase 0 - reads required before the numbers are final

All four are done. Kept for the record, since each settled a number the plan depends on.

- **P0.1 Cowper internals.** DONE 2026-08-09, see phase 2.3. Heating is
  `tempDiff * fuelFactor * dt` capped at `CowperMaxTemperature`; delivery to air is
  `tempDiff * CowperCoolingSpeedAir * dt` with the air taking the stove's full temperature. The tick
  has three branches - mixing, heating, delivering - and **no else**, which is why an idle stove
  holds its charge forever.
- **P0.2 Converter process temperature.** DONE 2026-08-09. 0.9 holds the bath at a FLAT
  `BessemerProcessTemperature = 1800` via `HoldTemperature` - no heat balance, no losses, no scrap.
  iwex replaces that with a computed balance; see phase 3.2 for the port.
- **P0.3 Tap and pour rates.** DONE 2026-08-09. Smaller than feared - only three literals, listed in
  phase 4.2. The canal edge cap, its floor and the canal capacity are already config keys.
- **P0.4 Metalbit unit value.** DONE 2026-08-09. iwex's `BessemerScrapUnitValue = 5 u` per bit, so the
  crushing loop returns 5 u of scrap as 8.5 u of iron - **exactly 1.7x**, unbounded.

## Phase 1 - furnace heat and melt model

### 1.1 Continuous temperature ceiling

Replace the binary boost threshold with a curve over the blast temperature already read at the
tuyere (`hotBlastTemp`, defaulting to 20 C - no new plumbing needed).

    targetTemp = BfNaturalMaxTemp
               + (BfBoostedMaxTemp - BfNaturalMaxTemp)
                 * clamp(blastTemp / BfBlastTempReference, 0, 1)

| constant | now | new | note |
| --- | --- | --- | --- |
| `BfNaturalMaxTemp` | 1420 | **1540** | 58 C over the 1482 melting point - deliberate leeway |
| `BfBoostedMaxTemp` | 1740 | 1740 | unchanged value, but see 1.4 - it is currently unreachable |
| `BfBlastTempReference` | - | **1240** | new; equals `CowperMaxTemperature` |
| `BfBlastBoostThreshold` | 800 | **deleted** | meaningless under a curve |

Cold blast 1543. Cowper at 800 C, 1669. Full cowper, 1740.

### 1.2 Heating rate scales with blast temperature

    heatRate = BfHeatRateBase + (BfHeatRateHot - BfHeatRateBase)
               * clamp(blastTemp / BfBlastTempReference, 0, 1)

| constant | now | new |
| --- | --- | --- |
| `BfHeatRateBase` | 4 (literal) | **4** C/s - cold blast reaches its ceiling in about 6 minutes |
| `BfHeatRateHot` | - | **8** C/s - full cowper reaches 1740 in about 3.6 minutes |
| `BfHeatRateUnblown` | 2 (literal) | **2** C/s - no blast at all |

### 1.3 Melt speed from heat margin and air, combined

Two factors, resolved by taking the minimum so there is no feedback loop between air demand and melt
speed:

    margin     = internalTemp - BfIronMeltingPoint
    heatFactor = clamp(BfMeltSpeedBase + margin / 100 * BfMeltGainPer100C,
                       BfMeltSpeedMin, BfMeltSpeedMax)
    requested  = tuyereCount * TuyereIntakeVolume * heatFactor
    airFactor  = drawn / requested            // 0..1
    meltSpeed  = heatFactor * airFactor

| constant | new | | margin | heatFactor |
| --- | --- | --- | --- | --- |
| `BfMeltSpeedBase` | **0.2** | | cold blast (58) | **0.61** |
| `BfMeltGainPer100C` | **0.7** | | cowper 800 (187) | 1.51 |
| `BfMeltSpeedMin` | **0.5** | | full cowper (258) | **2.0** (clamped) |
| `BfMeltSpeedMax` | **2.0** | | | |

Cold to full spread is about 3.3x. `meltSpeed` scales the melt cycle's pace: accumulate
`meltSpeed * dt` into the cycle timer and fire one whole cycle at `BfMeltIntervalSec`, so blast mix
is still consumed in whole units and the multiplier is reportable as "melting at 1.6x".

### 1.4 Deletions and unblocking

- **The hardcoded `GameMath.Clamp(_internalTemp, 20f, 1700f)` must read config.** Until it does,
  `BfBoostedMaxTemp = 1740` is unreachable and the whole curve is capped at 1700.
- `BfMeltStartDelay` (300 s soak) - **deleted**, redundant once melting is rate-based.
- The 1200 s `BfMaxFuelBurnTime` extinguish - **deleted** with its `_fuelBurnSeconds` accumulator.
- The `_belowMeltingSeconds >= 30` demotion literal - extract or delete with the state machine.

### 1.5 Visibility

With nothing extinguishing, a furnace whose molten pool is full stops producing silently. Block info
must state: the current melt multiplier, the temperature and its current ceiling, the air drawn
against the air requested, and "reservoir full - tap to resume". This is convention R7, not polish.

## Phase 2 - air, exhaust, stack, blowers, pumps

### 2.1 Air demand and supply

Demand is `tuyereCount * TuyereIntakeVolume * meltSpeed`.

| constant | now | new | rationale |
| --- | --- | --- | --- |
| `TuyereIntakeVolume` | 12 | **20** | 24.4 L/s cold, 40 at 1.0x, 80 in overdrive |
| `AirBlowerOutputPerSecond` | 48 | **300** | Watt gives 90 L/s. **The current value cannot feed one furnace** - 48 x 0.3 = 14.4 against a 24 L/s draw |
| `MpBlowerLitresPerSpeed` | 140 | 140 | unchanged - a waterwheel at 0.175 gives 24.5 L/s, exactly one furnace at cold baseline |

Intended reading, and the ruling this phase serves:

- one MP blower feeds one furnace at baseline;
- a steam air blower feeds several furnaces, or one in overdrive (Watt 90, Cornish high 240);
- an engine-driven MP blower lands at 70 L/s - comparable to the steam blower, which stays ahead and
  keeps sole access to converter pressure.

### 2.2 Exhaust and stack

Exhaust scales with air in; the stack must clear an overdriven furnace.

| constant | now | new |
| --- | --- | --- |
| `SmokestackGasIntakeVolume` | 48 | **96** |
| `BfExhaustOutputPressure` | 2.0 | 2.0 |

Cowper regeneration already scales with the exhaust it receives: heating is proportional to the
temperature difference, so a hotter, fuller exhaust charges the stove faster with no change needed.
The intended loop closes on its own - bigger blower, more air, faster melt, more exhaust, hotter
cowper, higher ceiling, faster melt - bounded by `CowperMaxTemperature`.

### 2.3 A cowper must lose heat

Two defects found in P0.1, both of which make a charged stove a permanent asset:

**A. It never cools when idle.** `BlockEntityCowperStove` branches on mixing, heating from exhaust,
and delivering to air, with no `else`. A stove with neither gas flowing never touches
`_internalTemperature`, so it holds its charge across days and reloads. Add an ambient loss applied
on every tick regardless of branch:

    _internalTemperature -= (_internalTemperature - ambient) * CowperIdleCoolingSpeed * dt

| constant | new | note |
| --- | --- | --- |
| `CowperIdleCoolingSpeed` | **0.0006** /s | half-life about 19 minutes; a stove stays useful for a while but cannot be banked |

**B. Delivery does not depend on how much air is drawn.** The stove gives the air its full
temperature and loses `tempDiff * CowperCoolingSpeedAir * dt` however much or little is flowing, so a
trickle depletes it exactly as fast as a torrent. That is wrong on its own and worse under phase 2.1,
where an overdriven furnace pulls 80 L/s against a baseline 24. Scale the loss by the flow:

    _internalTemperature -= tempDiff * CowperCoolingSpeedAir
                            * (airVol / CowperIntakeVolume) * dt

with `CowperIntakeVolume = 24` as the reference draw. An overdriven furnace then drains its stove
about 3.3x faster than a cold-blast one, which is what makes the two-stove alternation the handbook
already describes into a real operating decision rather than a formality.

### 2.4 Pumps - three MP pumps to one engine pump

| constant | now | new | result on a Watt |
| --- | --- | --- | --- |
| `PumpWaterPerSecond` | 16.67 | **33** | engine pump 30 L/s |
| `MpPumpLitresPerSpeed` | 20 | 20 | MP pump 10 L/s - exactly 3:1 |

Ordering afterwards: manual 2 < MP on a waterwheel 3.5 < MP on a Watt 10 < engine on a Watt 30.

## Phase 3 - the scrap loop and the converter as remelter

### 3.1 Close the duplication

`src/SteelIndustryExpanded/assets/siex/patches/vanilla/metalbit.json` adds `crushingPropsByType` so
`metalbit-iron` crushes to `game:crushed-iron` 1:1. At the shipped 5.0 units per ore that loop is
exactly break-even; at 8.5 it returns 1.7x and iron becomes unbounded.

**Remove the patch.** The blast furnace smelts ore, not scrap. Handbook text that advertises
"crush scrap iron bits back into crushed iron for resmelting" must go with it (all three locales).

### 3.2 The converter accepts scrap - port the iwex model

iwex already implemented remelting and its design is better than a fixed fraction: **there is no
hardcoded scrap cap.** Cold scrap is pure mass on the heat balance, and past the point where the
blast can no longer hold the bath over the refine floor the heat stalls and freezes into the existing
solidified/chisel path. The cap is emergent, which is exactly the dynamic behaviour wanted here.

Replace 0.9's flat `HoldTemperature` with a computed bath temperature. The pressure term is this
branch's addition on top of iwex, so that more air pressure buys back scrap tolerance:

    T_process = BessemerBaseTemperature
              + (pressure - BlastPressureThreshold) * BessemerPressureTempGain
              - BessemerRadiationLoss
              - BessemerColdScrapLossCoefficient * scrapUnits

Refining is gated on `T_process >= BessemerRefineTemperature`; below it the blow stalls.

| constant | now | new | source |
| --- | --- | --- | --- |
| `BessemerBaseTemperature` | - | **1850** | iwex |
| `BessemerRadiationLoss` | - | **50** | iwex; 1850 - 50 = 1800, matching 0.9's current flat hold exactly |
| `BessemerRefineTemperature` | - | **1500** | iwex |
| `BessemerColdScrapLossCoefficient` | - | **0.7** C/u | iwex ships 0.35 against a 4800 vessel; doubled to hold the same ~18 % share in 0.9's 2400 vessel |
| `BessemerScrapUnitValue` | - | **5** u | iwex |
| `BessemerScrapSteelYield` | - | **0.97** | iwex |
| `BessemerPressureTempGain` | - | **100** C/atm | new here - at 4.0 atm the bath runs 1950 and tolerates half again as much scrap |
| `BessemerProcessTemperature` | 1800 | **deleted** | replaced by the balance above |

Resulting scrap ceiling: at the 2.5 atm gate, `(1800 - 1500) / 0.7` = 429 u = 86 bits = about 18 % of
a 2400 vessel. At 4.0 atm it rises to 643 u, about 27 %.

Note for the record: iwex settled its own `BessemerConverterCapacity` at **4800** and was considering
6000, against the 2400 ruled for this branch. Worth revisiting if a 2400 vessel feels small once the
furnace produces 1.7x.

### 3.3 Converter dynamics

Same shape as the furnace: more pressure and more air mean faster conversion.

    convSpeed = clamp(BessemerSpeedMin
                      + (pressure - BlastPressureThreshold) / BessemerPressureReference,
                      BessemerSpeedMin, BessemerSpeedMax) * airFactor

| constant | now | new |
| --- | --- | --- |
| `BessemerBlastPerSecond` | 8.0 | **24** at 1.0x (12 at minimum, 48 at maximum) |
| `BessemerSpeedMin` / `Max` | - | **0.5** / **2.0** |
| `BessemerPressureReference` | - | **1.5** - 2.5 atm gives the minimum, 4.0 atm the maximum |
| `BessemerProcessDuration` | 300 | 300 - divided by `convSpeed` |

The iwex sources to port from, for reference when implementing:
`BlockEntityConverterControl.TryChargeScrap` (scrap intercepts the click, classified by
`Roles.Scrap`, not by item path), `ComputeHeatBalance`, `HoldBathTemperature`, and `RetypeToSteel`,
which melts cold scrap in at `ScrapSteelYield`. Design doc: `docs/design/machines/bessemer.md`.

## Phase 4 - capacities and molten flow

| constant | now | new |
| --- | --- | --- |
| `BfIronPerMeltCycle` | 60 | **102** - 12 ore x 8.5, the iwex currency, 1.7x a bloomery |
| `BfSlagPerMeltCycle` | 10 | **17** - holds the shipped 6:1 |
| `BfMaxMoltenIron` | 2400 | **4800** |
| `BfMaxMoltenSlag` | 600 | **1200** |
| `BessemerConverterCapacity` | 1200 | **2400** |
| `CanalDefaultUnitCapacity` | 50 | **100** |
| `MoltenFlowRate` | 50 | **100** - canal edge cap, already a config key |

### 4.1 Canal starvation

`MoltenNetwork` floors the **gap** at `MoltenMinFlowAmount = 10` and then halves it, so head decays
by up to 9 units per block and a canal delivers nothing from about 8 blocks out. The floor is
deliberate - its comment notes that flooring the step instead would strand values between one and two
minimums - so the fix is to keep the halving, which prevents pair inversion, and move the floor onto
the **transfer** at 1 whole unit. Head then decays about 1 unit per block and a 20-block canal still
delivers.

### 4.2 Tap and pour rates become config

**Ruling: these are player-configurable values, not literals.** P0.3 found only three, all of which
need extracting and then scaling for the doubled production:

| literal | where | new key | value |
| --- | --- | --- | --- |
| `Math.Min(20, (int)_moltenIron)` | `BlockEntityBlastFurnace.cs:176` | `BfIronTapDrainPerTick` | **40** |
| `Math.Min(20, (int)_moltenSlag)` | `BlockEntityBlastFurnace.cs:210` | `BfSlagTapDrainPerTick` | **40** |
| `int unitsPerBit = 5` default parameter | `MoltenChisel.cs:55` | `MoltenUnitsPerBit` | **5** |

`MoltenUnitsPerBit` and the converter's `BessemerScrapUnitValue` are the same physical quantity - one
metal bit's worth of metal - and must be one key, not two that can drift apart.

## Phase 5 - full configurability

Every process constant is a config key. Sweep for behavioural literals and extract them. Known
offenders, not exhaustive:

- `GameMath.Clamp(_internalTemp, 20f, 1700f)` - blast furnace
- heat rates `4f` / `2f` - blast furnace
- `_belowMeltingSeconds >= 30` - blast furnace
- tap drain rates - handled in phase 4.2
- the cowper's ambient reference temperature, once 2.4 introduces one
- `ThroughputScale = 3f` - engine fluid pump
- the manual pump's 1 atm delivery head
- the molten network's step and halving rule

A literal that only expresses a unit conversion (for example the network-to-animation `pi/5`) is not
a process constant and stays in code.

## Phase 6 - migration, tests, text

- **Config migration** for every changed default and every deleted key, under the current mod
  version's entry, so existing configs pick the new values up.
- `_fuelBurnSeconds` becomes a vestigial save key - read and discard.
- **Tests.** The slag path has zero coverage today; it needs tests as it is removed, not after. New
  coverage for: the temperature curve at cold, mid and full blast; melt speed against both factors,
  including the air-starved case; the scrap loop being closed; canal delivery at 8, 12 and 20 blocks;
  every new constant being read.
- **Text.** The handbook and config documentation in **en, ru and uk** are already stale from the
  2026-08-09 mechanical-power change and this batch makes that worse. One pass covering both, with
  the blast furnace article rewritten around the new model, and the scrap-crushing sentence removed.
  The hot-blast article must also say that a stove cools when idle and drains faster under a heavy
  blast, since the two-stove alternation stops being optional.

## Carried, not in scope

- The `IsMpOverstressed` regression: nothing reports that an engine is labouring, so the throttle-aware
  `MpRatedLoad` has no production effect. Wire the indicator or the comment is untrue.
- Pressure relief sheds 8 L per tick against an 18 L/s surplus on a Lancashire and Watt pairing.

## Order of work

Phase 1 first - the capacity and air numbers hang off the melt model. Then 2, then 3 and 4 together
(the scrap fix must land with the yield raise, never after it), then 5, with 6 continuous throughout
rather than at the end.
