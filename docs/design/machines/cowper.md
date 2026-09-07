# Cowper stove

**Status** scheduled for remake — the stove becomes gas-fired (settled 2026-08-02, gas budget and stove
count 2026-08-05). The shipped code still burns solid coal: the charge rate is multiplied by a burning coal
pile in the cell under the intake, identified by code substring (`BlockEntityCowperStove.cs:122`, `:134`).
**Mod** siex

**Owns** — the facts this page is canonical for:

* the stoves as machines: their count, their size, their lighting ritual, and the dust catcher that
  feeds them;
* the regenerative cycle as designed — charge on fuel gas, blow on cold blast, swapped by hand on valves;
* the heat-in/heat-out asymmetry — a stove heats roughly twice as slowly as it discharges — and the stove
  count that falls out of it;
* the statement of what the shipped code does today, and the sequencing that keeps it alive.

**Does not own** — cited only, never restated:
[gas-system](../mechanics/gas-system.md) (the fuel-gas medium and its two grades, the burner, flaring,
buffering, the surplus loop and the gas-fired blowing engine) ·
[heat balance](../mechanics/heat-balance.md) (the `T_process` law, `BfPreheatCoefficient`, and what a hot
blast is worth at a tuyere) · [hot blast furnace](blast-furnace-hot.md) (the closed top, the exhaust source
and its budget) · [smokestack](smokestack.md) (where burnt gas goes) ·
[pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) (the graph, pools, one-medium rule, valves) ·
[multiblock](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md) · [recipes & config](../mechanics/recipes-config.md)

---

## Role

A **regenerative heat exchanger**: a stack of iron checkers inside a brick shell that soaks up heat on one
half-cycle and gives it back to cold blast air on the other. It is the only thing in the suite that raises a
blast's temperature, and therefore the only thing that makes
[heat balance](../mechanics/heat-balance.md)'s preheat term non-zero. Without a stove the furnace's waste
heat is thrown away and the only lever on `T_in` is how coke-rich the courses are laid.

It is not automatic. A stove has one internal state (a temperature) and no scheduler; which half-cycle it
runs is decided entirely by which of its two gas lines the player has left open. Valves are the interface;
there is no switch on the stove.

## The design

### Fired by the furnace's own gas

A Cowper stove is not coal-fired: it burns blast-furnace gas, the combustible waste the furnace itself
produces. A gravitational filter on the exhaust - the **dust catcher** - converts raw furnace exhaust into
clean fuel gas, and that is what burns inside the stoves. The gas as a system - the medium, its grades, the
burner, flaring and the plant-wide surplus loop - is [gas-system](../mechanics/gas-system.md)'s; this page
owns the stoves and the catcher.

### Stove count is emergent — heating is slower than blasting

A stove takes roughly twice as long to heat as to give up its heat, which is why works ran three: one on
blast, two on gas. The design fixes that ratio and lets the count fall out:

| stoves | what the player sees |
|---|---|
| 2 | the blast sags between swaps - visible, diagnosable, survivable |
| 3 | steady hot blast - the working configuration |
| 4 | headroom and redundancy |

Nothing anywhere states three; the player learns the number by watching the blast.

### Stoves get bigger

Cowper stoves stood as tall as the furnace they served. The in-game stove grows to match - a tall
megablock that reads as the furnace's twin rather than as an accessory. Fixed size, not player-scalable:
height-as-capacity is the chimney's lever and does not need a second owner.

### Lighting

Blast-furnace gas autoignites around 600–650 °C, so lighting is a first-light cost, not a per-cycle one:

| stove state | what happens when gas arrives |
|---|---|
| hot enough inside | the gas lights by itself - no player action |
| cold | the player opens a side hatch and torches the heat sinks - a lighting port, and it only works if gas is *already* flowing |

Once stoves are in alternation they keep themselves going. Ordering is the trap: torching an empty stove
must do nothing. Gas first, flame second - otherwise the player lights a stove that then fills with unburnt
gas.

### The dust catcher gives back

Flue dust is iron-bearing - fine ore and coke carried out of the throat by the gas. Emptying the catcher
yields material that goes back into the burden, routed to whatever consumes fine iron-bearing material (the
same place mill scale goes).

Neglect is legible, not punishing: a full catcher chokes gas flow, so the stoves run cold and the player
sees a failing hot blast rather than a hidden timer. It never silently destroys the gas.

Not modelled: BF gas is toxic (CO-rich) and explosive. Out of scope - the mod does not model asphyxiation.

## Current code

The shipped stove (`BlockEntityCowperStove`, anchor `BlockCowperStoveIntake`) implements the model the
remake replaces: a 3 × 3 × 7 megablock whose charge half-cycle soaks heat out of piped furnace exhaust, at a
rate multiplied by a burning coal pile in the layout cell under the intake - a `coalpile` block below
(`BlockEntityCowperStove.cs:122`) whose item code contains `anthracite` picks the fast rate (`:134`).
Everything else about the shipped machine - the layout, the exact-fit exhaust tuning, the `Cowper*` config
keys, the recipes and the tests - documents that model and is superseded by the design above. It stays live
until the gas loop exists: the stove is not getting a fuel bed of any kind, so the remake is a remake, not a
pile-for-firebox swap.

## Open

1. The dust catcher does not exist, and the fuel gas it would produce has no producer path, no medium
   entry and no consumer. Until it is built, the coal-fired stove stays.
2. The remake moves numbers other pages carry - the exhaust figure, the charge-rate advantage, the
   two-stove assumption in layout and lang, and the hot blast furnace's exhaust arithmetic. The retune list
   is [gas-system](../mechanics/gas-system.md) § Open.
3. The bigger stove has no drawn shape and no layout.
4. The swap stays manual on valves; whether the stove should expose a clearer status, a target
   temperature or an auto-changeover is undecided.
