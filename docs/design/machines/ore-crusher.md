# Ore crusher

**Status** designed, shape drafted (2026-09-06) - no code   **Mod** iiex
**Since** 2026-07-31

**Owns** - the facts this page is canonical for:

* the decision that the crusher is an mp-energy consumer, not a self-contained steam machine, and why;
* the stacking model - N crushers on one network, throughput bought with power;
* the pulsed-load behaviour, and the fact that this is the machine that makes the flywheel necessary rather
  than merely available;
* the hardness gate - harder ores cost more energy per unit, expressed as a threshold rather than a tier
  flag;
* the rule that the crusher is throughput, never access;
* the block form: one pan, 1 x 2 x 2 cells, fed by a vanilla chute from above and discharging below;
* the drive: the eccentric shaft is the mp shaft, through both side faces; the network's flywheel block
  is the crusher's flywheel.

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| Energy in joules, load-driven speed, the governor, pulsed supply and burst | [mp-energy](../mechanics/mp-energy.md) |
| The flywheel block, its ½Iω² reservoir and the vanilla-MP bridge | [flywheel-and-shafting](flywheel-and-shafting.md) |
| The Watt engine and the Cornish boiler that feed the network at iiex | [engine-watt](engine-watt.md) · [boiler-cornish](boiler-cornish.md) |
| What crushed ore is *for* | [layered-charge](../layered-charge.md) · [ironmaking](../processes/ironmaking.md) |
| Roasting, the other ore-prep step | [roasting](../processes/roasting.md) |
| Ferroalloys, and therefore what chromite is *for* | [blast-furnace-cold](blast-furnace-cold.md) § Its second act |
| Megablock footprints, fillers, MP filler ports | [multiblock](../mechanics/multiblock.md) |
| EM nugget-crushing compat | [recipes-config](../mechanics/recipes-config.md) |

**Depends on** [mp-energy](../mechanics/mp-energy.md) · [flywheel-and-shafting](flywheel-and-shafting.md) ·
[layered-charge](../layered-charge.md) · [roasting](../processes/roasting.md) ·
[blast-furnace-cold](blast-furnace-cold.md)

---

## Role

A powered ore breaker. It does what a vanilla pulverizer does, faster and to harder rock.

Historically this is Blake's jaw crusher, patented 1858. The reference engraving is a steam-driven double
stone breaker: two crushing pans with corrugated jaws either side of a central engine with twin flywheels,
feed rock in the middle, fines heaped at the sides.

It exists for two reasons:

1. **Throughput.** Crushing is the highest-click, lowest-decision operation in the iron chain - pan or hammer,
   per lump, forever. That is the operating-side friction the suite's thesis says to optimise hard, paid for
   with build complexity.
2. **Harder ores.** Chromite and its relatives are beyond what a pulverizer will break, and they are what the
   [cold furnace's second act](blast-furnace-cold.md) needs.

**It is throughput, never access.** An iiex-only player must reach a complete early-19th-century experience,
and the vanilla pulverizer route stays viable forever - just slower. Same rule as pig beds against direct
charging: the early route never becomes impossible, only less good.

---

## Why mp-energy and not a self-contained steam machine

The reference engraving shows the engine built into the machine, and iiex has an engine + docked sub-machine
idiom (the pump, the blower) that would fit it. That is the wrong shape here, for four reasons.

**1. The hardness gate becomes physics instead of a flag.** Chromite needs more joules per unit than hematite.
mp-energy models energy in J with load-driven speed, so "harder ore" is a continuous power requirement with no
branch on tier anywhere - the same trick as `RequiredBlastPressureFor` and the twin-tub blower's 17.3 % coke
line. A self-contained steam crusher would have to gate chromite by being the steam version, which is a tier
flag wearing a costume.

**2. It is the machine that makes the flywheel necessary.** A crusher takes an enormous torque spike the
instant rock enters the jaws and almost nothing between, which is why the engraving carries two huge
flywheels. mp-energy already designs for this shape of load (pulsed supply, governor, burst). So:

> Hook a crusher straight to a waterwheel and it stalls on every rock. Add a flywheel and it runs smooth.

[iiex-bringup](../../internal/plans/iwex-bringup.md) records that mp-energy's weakness is too few consumers for the
flywheel to justify itself. A machine that cannot run well without one teaches the flywheel rather than
announcing it.

**3. Being MP-driven does not make it iiex's.** The forming-line rule (iiex = MP · iiex = steam · smex =
extends, [settled 2026-07-29](../../internal/plans/STATE.md)) governs variants of one machine, not which mod owns every
MP consumer. There is one crusher and it lives in iiex, because that is where it sits in the player's arc: a
throughput upgrade bought once a steam plant exists, never required by the iiex loop, with a harder-ore half
that serves the steel-era ferroalloys. Keeping it out of iiex also protects the rule that nothing before cast
iron requires power - an iiex-tier crusher would sit upstream of the blast furnace and invite exactly the
dependency the [burdenmaker](burdenmaker.md) was stripped of MP to avoid.

**4. A docked sub-machine cannot be shared.** The pump and blower are effectively the engine's other end and
scale off its absolute power. A crusher is a general load that should compete with every other load on the
network. Docking it would make the most power-hungry machine in the chain the one machine that cannot share a
power plant.

The engraving still works as the art with the boiler not modelled as part of it. Re-examined 2026-09-06 for
historicity and immersion, the decision holds: Blake crushers in fixed installations were belt- or gear-driven
from line shafting (water or steam); the engine-on-frame units of the engraving were portable road-metal
breakers. An mp-driven crusher with the network's 3 x 3 flywheel block on its shaft reproduces the engraving's
silhouette at the right scale - the flywheel taller than the machine - while the steam plant stays the
player's, shared with every other load.

---

## Stacking

Crushers are independent consumers, so a player can build as many as they can feed. Throughput scales with
installed power, not with a per-machine tier:

| Want | Build |
|---|---|
| more crushed ore | another crusher |
| the crushers you have to stop stalling | more power, or a flywheel |
| chromite | enough power to clear its energy threshold |

This is the first place in the suite where "how much power do I have" becomes a question with a visible
answer. One waterwheel runs one crusher badly; a steam plant with a flywheel runs four.

It also means the crusher must be cheap enough to want several of. A 157-cell megablock would kill the
stacking story before it starts.

---

## The upgrade path is already in the player's hands

Vanilla pulverizers are themselves mechanical-power driven, so by the time a player wants this they very
likely already have a windmill or waterwheel turning. The crusher is the next thing on that same shaft, and it
is the one that teaches them the waterwheel is not enough.

The crusher attaches to an ordinary MP line with no special connector, exactly as a pulverizer does.

---

## Where it sits in ore prep

| Tier | Owns | Gives |
|---|---|---|
| **iiex** | [roasting](../processes/roasting.md) - an unbuilt pre-step; the [burdenmaker](burdenmaker.md) already accepts crushed *or roasted* ore | better ore |
| **iiex** | this machine, plus the engine and boiler that make it run properly | more ore |

Neither obsoletes the other, and both feed the same burdenmaker.

---

## Compat

Expanded Metallurgy already splits nugget crushing 1:1 on EM-absent / EM-present
([recipes-config](../mechanics/recipes-config.md)). The crusher's outputs must respect that split or the two
will double up and a player with EM installed will get twice the ore.

---

## Form, feed and drive (settled 2026-09-06)

**One pan, 1 x 2 x 2 cells.** A single Blake jaw the player stacks, per the stacking argument. Front cells
hold the jaws over a sheet discharge hopper, rear cells the drive and the toggle frame. In the north
authored frame with the front lower cell as principal:

| cell | holds |
|---|---|
| front lower (0,0,0) | four cast legs on a base ring, sheet hopper trough with its floor and a 6 x 6 spout ending on the bottom face at (8, 0, 8) |
| front upper (0,1,0) | the cheeks around the jaw chamber, the corrugated fixed jaw, the swing jaw on its pivot shaft; the mouth (11 x 5) centred under the top face at (8, 32, 8) |
| rear upper (0,1,1) | the eccentric shaft with its sheave, the strapped pitman, both toggles, the toggle seat on the rear frame |
| rear lower (0,0,1) | rear legs, rear standard |

**Fed by chutes.** A vanilla chute standing on the front upper cell pushes ore down into the mouth; a chute or
container under the front lower cell takes crushed ore from the spout. The block's inventory accepts from
above and offers downward on those two cells; no window interaction is needed for material.

**The eccentric shaft is the mp shaft.** It crosses both side faces of the rear upper cell at their centres
((0, 24, 24) and (16, 24, 24)), so crushers chain along one line and the network's flywheel block stands on
that shaft beside the machine: the crusher carries no flywheel of its own and no buffer - one flywheel
system, not two. The throw is exaggerated for legibility (jaw bottom 1.2 units) against the eccentric's 0.8.

**Art.** `workbench/shapes/machines/mpenergy/machine-mp-megablock-crusher-sketch.json`, built by
`builders/vsexpanded/crusher.py` in the tools repo; the pitman, toggle and jaw poses are solved from the link
lengths per frame. Awaiting the owner's Model Creator pass, then a machines.txt line.

## Open

Everything here is a decision, not a placeholder - none of it blocks the ones already made above.

1. **Output rates and the energy-per-unit table**, including where chromite's threshold sits relative to
   hematite's. Cannot be set until [mp-energy](../mechanics/mp-energy.md) has live numbers.
2. **Whether it accepts raw ore blocks or only vanilla-crushable items**, and whether it produces the same
   `game:crushed-*` items or its own.
3. **Chromite does not exist.** The harder-ore half of this machine's purpose is gated behind the ferroalloy
   work, which is entirely unbuilt - no third burden family, no ferroalloy metal descriptor, no ore. Until
   that lands the crusher is a throughput machine only, and it should be designed so that is enough on its
   own.

---

## Staging

Not in the six-stage iiex bring-up. It is a throughput machine, and throughput only matters once the loop it
feeds is running and the player is bored of pulverizers - which is feedback the playtest build will supply.

Slot it after the playtest, alongside or after [layered-charge](../layered-charge.md), and before the
ferroalloy second act that needs its harder-ore half.
