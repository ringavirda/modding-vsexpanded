# Mechanical Sprinkler

**Status** deferred   **Would live in** Industrial Homestead
**Deferred by** the metalworking-only cut - [scope.md](../../scope.md), which owns the decision
("farm automation, not plumbing"). The specification survives only in git history and on this page - see
[What exists today](#what-exists-today).

**Owns**

- The archived specification, recovered from git verbatim: the saturate-then-stop rule, the wrench-set
  interval, the radius, and the tank duty-cycle relationship.
- Why this is the cut's sharpest test case: the sprinkler is the only deferred item that is already
  fully plumbed - no new medium, no chemistry, no new machine, no vanilla system the mods have not touched.
  It is deferred on subject matter alone.
- The tank/sprinkler pair, which is where [scope.md](../../scope.md) drew its line, and the fact that the
  [fluid tank](../../machines/fluid-tank.md)'s original justification left with the sprinkler and was
  replaced.
- The two engineering traps a builder would hit (area tick load; gravity head vs ceiling mounting).

**Depends on**

[scope.md](../../scope.md) - the cut and the rule · the archived iiex spec (git history) ·
[fluid tank](../../machines/fluid-tank.md) - the block it shared a table with and which was carved back in ·
[pipe network](../../mechanics/pipe-network.md) - R1, run pressure, the connector-reciprocity rule ·
[pumps](../../machines/pumps.md) - what would fill the tank that feeds it.

---

## What it is

A fixed irrigation head fed from a cistern: water piped to a floor- or ceiling-mounted nozzle that wets the
soil under it, then shuts off. Period-plausible for the 1870s (mill-fed garden irrigation off a raised tank);
the whole machine is a valve on a timer.

In game terms it is the automation answer to the vanilla watering can (`BlockWateringCan`,
`.game/1.22/assets/survival/blocktypes/clay/fired/wateringcan.json:3`).

---

## Why it is deferred

[scope.md](../../scope.md) - farm automation, not plumbing. It fails the rule: a foundry works without it.
That decision is not re-argued here.

This page exists because the sprinkler is cheap, and cheap is not a scope argument. Every other deferred
item carries a real cost - the [gasworks](gasworks.md) needs a chemistry industry, climate control needs a
room model, [oil](oil.md) needs worldgen and a depleting resource, the [kiln](kiln.md) needs a furnace
variant. The sprinkler needs none of that:

| Would it need… | |
|---|---|
| a new medium? | No - water, already declared (`assets/exlib/config/liquids.json`) |
| a new network? | No - the pipe network is live |
| a supply chain? | No - the fluid intake and both pumps ship ([pumps](../../machines/pumps.md)) |
| a new vanilla system? | No - vanilla farmland already tracks moisture |
| chemistry? | No |

It is out anyway, because the test is what the feature is for, not what it costs.

The tank is the other half of the same ruling and it went the other way. Both blocks sat in one table
titled "Storage & farm automation" (`git show 791b43b:docs/design/iiex.md`, line 119). The cut split that
table down the middle: the tank stayed because it is plumbing that a boiler feed wants
([fluid tank](../../machines/fluid-tank.md):50-54), and the sprinkler left.

---

## What exists today

No block, no code, no asset.

```
$ grep -rniE "sprinkler|irrigat|soilmoist" src/ --include=*.cs
(0 results)
```

No lang key, no handbook entry, no shape, no config key, no recipe.

One asset does exist: exlib registers the vanilla watering-can trickle as a shared sound, for the pumps.

| | |
|---|---|
| `ExSounds.Watering` = `game:sounds/effect/watering`, documented as "the rhythmic water sound of a working hand pump" | `src/ExpandedLib/Helpers/ExSounds.cs:71-74` |
| used by the manual pump | `BlockEntityManualFluidPump.cs:265-267` |
| used by the engine fluid pump | `BlockEntityEngineFluidPump.cs:81` |

### On the vanilla side, everything it would drive is present

| | |
|---|---|
| Farmland is a block entity (`entityClass: "Farmland"`) with a `dry` / `moist` variant pair | `.game/1.22/assets/survival/blocktypes/soil/farmland.json:5`, `:13` |
| Moisture itself lives in the BE, not in the asset | (no `moisture` key anywhere in `blocktypes/soil/farmland.json`) |
| The single-block behavioural template | `BlockWateringCan`, `.game/1.22/assets/survival/blocktypes/clay/fired/wateringcan.json:3` |

So "water to 100 % then stop" is a block-entity call on each affected farmland, not a block swap.

### The specification survives only in git

The sprinkler's table row was removed from the old iiex design monolith when the cut was made, and the
monolith itself has since been deleted; git history is the only other copy of the row. It is reproduced in
full below.

---

## The design as it stands

From `git show 791b43b:docs/design/iiex.md`, line 124 (the table row) and line 126 (the note):

> | **Mechanical sprinkler** | block (floor- or ceiling-mounted) | tank/pipe water → soil | waters soil in a
> **3-block radius beneath** to 100 % moisture then **stops** (consumes only the deficit); a **wrench-set
> interval** counts down before the next top-up — saturate-then-stop, not continuous | *(planned)* |
>
> Sprinklers run off the tank buffer, so the pump need only top it up every few in-game days.

Broken out:

| Decision | Value | Why it was made that way |
|---|---|---|
| Form | one block, floor- or ceiling-mounted | see Gotcha 2 - only one of those is free |
| Feed | tank or pipe water | it is a pipe-network consumer, nothing else |
| Area | 3-block radius beneath | |
| Rule | wet to 100 % moisture, then stop | |
| Cost | only the deficit is consumed | bounded, computable and readable - a run's water use is a function of how dry the ground was, never a leak |
| Cadence | wrench-set interval, counted down between top-ups | reuses the suite's existing wrench-configuration idiom rather than a GUI |
| Character | saturate-then-stop, not continuous | it is a timer and a valve, not a flow |
| Status | *(planned)*, never started | |

"Consumes only the deficit" makes the machine's demand self-limiting and exactly explainable - the two
properties R7 asks for ([conventions.md](../../conventions.md)) - and it is what makes the tank duty cycle
work: a buffer topped up "every few in-game days" only makes sense if the draw is a deficit rather than a
rate.

### The relationship that was quietly severed

The tank's original stated purpose was to serve this duty cycle (`791b43b:docs/design/iiex.md:126`); after
the cut its justification was rewritten to the boiler feed ([fluid tank](../../machines/fluid-tank.md):45-54).
The tank's 2000 L sizing therefore derives from the boiler ([fluid tank](../../machines/fluid-tank.md)
§ Numbers), i.e. from the second customer, not the first.

---

## What it would unblock

Nobody. It is a leaf.

| | |
|---|---|
| Metalworking line | nothing - not a wall, not a degraded path |
| Homestead | nothing - the gasworks, still, chemistry, lighting and climate control all ignore it |
| The fluid tank | it would give the tank a second consumer, but the tank no longer needs one |

Same verdict as the [lime kiln](kiln.md), for the opposite reason: the kiln unblocks nothing because its
product feeds nothing; the sprinkler unblocks nothing because it is an end-user appliance. Both are terminal.

---

## Gotchas

1. This is an area effect, not a machine, and nothing in the suite does that. A 3-block radius beneath
   is a 7 × 7 footprint - up to 49 foreign block entities touched per top-up. Every machine in the tree
   ticks itself and at most its own footprint and its own connector faces; none reaches into dozens of
   unrelated BEs on a schedule. The saturate-then-stop design keeps the frequency low, which is a mitigation
   the interval was presumably chosen for, but the per-fire cost is unlike anything shipped.

2. Ceiling mounting is not free, because the network has no head model. A tank delivers at a fixed
   1 atm gravity head and does not raise pressure ([fluid tank](../../machines/fluid-tank.md):199-208);
   the pipe network has no notion of elevation ([pipe network](../../mechanics/pipe-network.md)). A floor
   sprinkler fed from a tank above it is physically sensible and mechanically free; a ceiling sprinkler fed
   from a cistern at ground level is pumping uphill for nothing, and the model would not notice. The
   archived row allows both. Pick one, or accept that the physics is decorative.

3. R1 - water only, and the guard is on volume, not on the label. A run that has ever carried steam keeps
   its medium until it drains ([conventions.md](../../conventions.md);
   [fluid tank](../../machines/fluid-tank.md) Gotcha 3). A sprinkler spurred off a boiler-feed line will be
   refused whenever that line is doing its other job.

4. Connector reciprocity. A sprinkler adjacent to a pipe is not plumbed in unless the pipe presents a
   connector back ([pipe network](../../mechanics/pipe-network.md)) - the rule every iiex machine port
   already lives by, and a likely first bug for a block whose "in" face is not obviously the business end.

5. Moisture is a vanilla BE field, so the mod is writing into vanilla farming state. That is a smaller
   patch surface than [climate control](climate-control.md)'s crop-temperature rewrite, but it is still
   vanilla state, and the failure mode (soil stuck wet, crops that never need attention) is a balance change
   to the base game rather than to the mod.

---

## Open

- Whether Homestead is even the right home. Homestead is chiefly gas and chemistry; a sprinkler has
  nothing to do with either, and it is grouped there only because "not metalworking" was the sorting
  criterion. It may belong in a farming add-on that does not exist yet.
- Pipe-fed or MP-fed? The archived design assumes the pipe network. A mechanically-driven sprinkler
  bar off a line shaft is equally period and would sidestep Gotcha 2 entirely. Never considered.
- Floor or ceiling (Gotcha 2) - the row allows both and the pressure model supports one.
