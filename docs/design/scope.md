# Scope - what was cut, why, and what was carved back out of the cut

**Status** settled 2026-07-29. This is a decision record, not content: nothing here is built or
buildable, and nothing here is a proposal.
**Mods** all of them - `exlib` → `iiex` → `iiex` → `smex` → `hpex`, plus the deferred `elex` and the two
off-spine add-ons.

**Owns**

* the metalworking-only rule and the one-sentence test that applies it;
* the deferral list - what went to the planned Industrial Homestead mod;
* the two carve-outs that survived the cut (producer gas, the phase-change model) and the criterion
  that separates producer gas from coal gas;
* the non-ferrous deferral as a scope fact - that it is a different deferral from Homestead's, and
  what the tilting crucible was going to be load-bearing for;
* elex's three chemistry dependencies with their differing severities, and the shape of the elex subset
  that could ship without any chemistry at all;
* the release target: the complete ferrous line.

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| The gas producer as a machine - its inputs, numbers, medium requirement, and the "no gasholder" ruling | [gas-producer](machines/gas-producer.md) |
| The phase-change / distillation model itself | [conventions.md](conventions.md) § distillation |
| The fluid tank, and why medium-agnostic is load-bearing | [fluid-tank](machines/fluid-tank.md) |
| The ferrous crucible furnace (which is not deferred) | [crucible-furnace](machines/crucible-furnace.md) |
| R3, the ladle-is-the-only-merge rule | [conventions.md](conventions.md), exlib's molten-network mechanics page |
| The ladle as a machine, and the zinc coke cover | [ladle](machines/ladle.md) |
| elex's grid model, arc furnace and electrode mechanic | [deferred/elex/](deferred/elex/electrical-grid.md) |
| Current build status and every open decision (the D/N rulings, the blocker list) | [STATE.md](../internal/plans/STATE.md) |
| The mod chain, the pillars, the build order | [overview.md](overview.md) |

**Depends on** [overview.md](overview.md) · [STATE.md](../internal/plans/STATE.md) · [conventions.md](conventions.md) ·
[materials.md](materials.md) · [gas-producer](machines/gas-producer.md) ·
[fluid-tank](machines/fluid-tank.md) · [crucible-furnace](machines/crucible-furnace.md)

---

## Why this page exists

This is the single place a scope question gets answered, and the page a stale doc should be edited to
point at.

---

## The rule

> A feature earns a place in this suite if a foundry or a rolling mill would not work without it.

The rule lives only here; `overview.md` § Scope is a pointer. Everything else is either deferred to
Industrial Homestead (the domestic and chemical half of the 19th century) or deferred as
non-ferrous. Those are two different deferrals with two different reasons.

---

## What was cut - the Industrial Homestead list

Out of scope, not merely unscheduled. Do not design or build any of it here.

| Cut | Why it goes |
|---|---|
| Coal-gas / coal-chemistry complex - gasification plant, retort house, gas main | its consumers are domestic; it is a chemical feedstock industry, not a metallurgical one |
| Chemical distillation products - fractionating still, coal tar, benzene, kerosene, naphtha, aniline dyes, acids, ammonia | the model stays (below); only its chemical applications go |
| Oil derrick and everything downstream of crude | a whole extraction industry with no metalworking consumer |
| Benchtop chemistry station and its recipe families | ditto |
| Gas lighting - gas lamp, gasholder | lighting demand is intermittent and domestic |
| Climate control - cast radiators, cooling coils, ammonia refrigeration, ice, crop climate patches | a room heat-balance for a home, not a shop |
| Farm automation - the mechanical sprinkler | farm automation, not plumbing |
| Lime kiln / cement / concrete | nothing in the spine needs quicklime: the furnace fluxes on limestone and `powderedslag` + slaked lime already makes mortar |
| Domestic ironware - pots, kettles, cauldrons, firebacks, stove plates, bedsteads, the stove | Darby's 1707 pots founded the coke-iron industry, but this is not what the iron tier is about |
| Malleable cast iron + the annealing box | deferred as a cast-iron upgrade path to revisit, not dropped |

The full specifications survive under [deferred/homestead/](deferred/homestead/gasworks.md), so Homestead
starts from a written design.

Homestead is where the sulfur and acids would have come from, which is why every remaining chemistry
need in this suite traces back to it - see § elex below.

---

## What was carved back out of the cut

Two things survive the deferral because metalworking needs them. They are not the gasworks above.

### 1. Producer gas - and the line is tar

Producer gas (coke + steam + limited air → CO/H₂) stays, in `smex`, because the open hearth has no other
fuel: every other fired machine in the suite burns a solid charge, and a regenerative bath is fired by a
flame that has to arrive down a pipe.

The scope criterion, stated once here:

> A gas that is a lean fuel and makes no tar stays. A gas that is a rich chemical feedstock and drops
> tar goes. Producer gas is made by blowing air and steam through hot coke and yields ash; coal gas is made
> by carbonising coal in retorts and yields tar, ammonia and benzene - which is a chemistry mod.

The machine-side comparison table, the "never stored / no gasometer" ruling and the medium requirement all
belong to [gas-producer](machines/gas-producer.md).

Consequence: the metalworking line never gets coal-tar pitch, and therefore never gets graphite electrodes
without Homestead, because the gas process it kept produces none. Same decision as the beehive coke oven's:
keep the cheap, dirty, by-product-free half of a process, defer the recovery half.

### 2. The phase-change / distillation model

The general "heat a liquid → boil off fractions in ascending boiling-point order → condense each at a cooler
stage" mechanism stays ([conventions.md](conventions.md) § distillation), because the boiler runs its
simplest case today (water → steam) and the steam condenser is live. Only the chemical applications - the
fractionating still as a block, and every product built on it - are deferred.

### And two blocks that came with it

| Kept | Why | Owner |
|---|---|---|
| Fluid tank / cistern | it is plumbing: a shop with a Cornish boiler wants a buffer between its pump and its feed | [fluid-tank](machines/fluid-tank.md) |
| Steam condenser *(live)* | a general phase-change block already shipped | iiex |

---

## Non-ferrous - a different deferral

Decision D8 ([STATE.md](../internal/plans/STATE.md)). Deferred later, not to Homestead: this is period-correct
metalworking that is not on the release path.

| Deferred | Record | What it was for |
|---|---|---|
| Copper reverberatory furnace | [copper-reverberatory](deferred/non-ferrous/copper-reverberatory.md) | crushed copper ore → matte; also the copper-side waste-alloy recycler |
| Pierce-Smith converter | [pierce-smith](deferred/non-ferrous/pierce-smith.md) | matte → blister copper; a mode of the built Bessemer, not a new machine |
| Zinc retorts | git history of the retired smex doc | zinc for brass |
| Tilting crucible | [tilting-crucible](deferred/non-ferrous/tilting-crucible.md) | melts tin/lead/zinc/copper (≤ 1085 °C) and pours them into the molten-canal network |
| The bronzes (tin / brass / bismuth / black) | [alloys](items/alloys.md) | ladle-mixed alloys |
| Converter copper and pure copper as materials | [alloys](items/alloys.md) | impure rod/wire stock; electrolytic cable copper |
| The ladle's zinc coke cover | [ladle](machines/ladle.md) | brass boils its zinc off uncovered |

### What the deferral actually costs: the ladle's merge mechanic has nothing to merge

R3 reserves the ladle as the only block that merges canals ([conventions.md](conventions.md)). The
molten network is fed only by the big ferrous furnaces, so it is effectively iron-only, and on an
iron-only network there is nothing to merge except ferroalloys.

The tilting crucible is the fix. Its tilt is the canal interface: it is to tin and copper what the blast
furnace's tap is to pig iron, making the network a general entry point for small metals, at which point
bronze becomes a canal junction - as R3 already designs it. Deferring non-ferrous therefore leaves a
shipped rule underused until it lands.

Do not confuse it with the ferrous crucible furnace, which is NOT deferred. That one is natural-draught,
~1600 °C, makes crucible steel, and is loaded with tongs; the tilting one is a cast-iron vessel that
physically cannot hold molten steel ([crucible-furnace](machines/crucible-furnace.md),
[tilting-crucible](deferred/non-ferrous/tilting-crucible.md)). Two machines, two eras, one shared pot
problem.

---

## elex - three chemistry dependencies, three different severities

elex is deferred with everything above; its blockers are recorded so they stop being re-derived. They are
not equally severe.

| # | Need | Chemistry input | Severity | Fallback |
|---|---|---|---|---|
| 1 | Arc-furnace electrodes | graphite - Acheson process: petcoke filler + coal-tar pitch binder | not a wall - vanilla ships the mineral | yes, twice over - see below |
| 2 | Electrolysis electrolyte | sulphuric acid | not a wall - vanilla ships the item | zero new recipes - see below |
| 3 | Sulfur for copper roasting | sulfur | over-rated | sulfur is a vanilla mined ore, and the specified copper chain has no roasting step at all |

### All three dissolve - verified against the installed game *(2026-07-29)*

Each was checked by reading the vanilla assets:

| Claim | Reality |
|---|---|
| the acid needs a chemistry mod | vanilla already cooks it. `survival/recipes/cooking/acid.json` makes `acid-full-sulfuric` from 1 L water + 1 saltpeter + 2 powder-sulfur. Not "one small recipe" - zero |
| graphite needs petcoke + pitch | graphite is a vanilla mined ore with its own worldgen deposit (`worldproperties/block/ore-ungraded.json`, `worldgen/deposits/mineralore/graphite.json`). Synthetic Acheson graphite needs Homestead; natural graphite does not, and it is what plumbago crucibles and early arc-lamp carbons were made from |
| sulfur has no fallback | sulfur is a vanilla ore that pulverises to `game:powder-sulfur` - and the copper chain as specified never roasts anything |

elex's real blocker is D8, not chemistry. Pure copper needs the copper chain
(reverberatory → Pierce-Smith), which is deferred as non-ferrous. The no-chemistry subset was described as
shipping "impure copper wire", but converter copper comes from the same deferred chain, so the DC subset
may be gated on D8 twice. Worth checking whether vanilla's own `metalplate-copper` can stand in.

Homestead therefore buys elex quality, not access: synthetic graphite consumes more slowly than the
mineral, which is the R5 shape the electrode mechanic already had.

1 - electrodes. The mechanic is one number, electrode consumption rate: carbon burns away fast,
graphite slowly. That is R5 (gate efficiency, not possibility) and it gives elex a consumable sink either
way. The carbon electrode is period-correct: early Héroult furnaces ran on coke-and-anthracite carbon
electrodes, Acheson graphite is 1896. Both graphite ingredients come from Homestead - oil (refining
residue → petcoke) and the gasworks (retort tar → pitch) - so this depends on the deferred mod, not on
reversing the beehive-oven decision.

2 - the acid may not be a wall at all. Copper electrorefining runs the bath as an electrolyte, not a
reagent: it recirculates, copper dissolving off the anode and plating onto the cathode, so elex needs a
one-time charge plus top-ups, not a supply chain. The period route is the lead chamber process (1746) -
burn sulfur to SO₂ - and both inputs, sulfur and saltpetre, are vanilla. Lead chambers want lead
(non-ferrous, deferred), but the earlier small-scale method used glass bell vessels, and small scale is
this case. So: plausibly one recipe or one small block, and the scope cut survives.

3 - sulfur is the copper add-on's need, not the ferrous line's, and it is doubly deferred with
non-ferrous. Its severity may be overstated - see § Open.

### The shape elex would take with no chemistry at all

Arc furnace + HSS on carbon electrodes, DC only, impure copper wire - but no alternators, because pure
copper needs the electrolysis bath and #2 is the only one of the three with no in-scope precedent to lean
on. That is the whole subset.

---

## The release target

> The complete ferrous line: `exlib → iiex → iiex → smex → hpex`. A player can walk it without
> leaving the spine.

Two consequences follow, both scope facts rather than status facts:

1. Copper and elex are out of the release, so elex's chemistry dependency is not a release blocker.
2. hpex's blockers become release-critical, because hpex is the last station on the line rather than
   an optional tail.

Off-spine and unscheduled but not Homestead: the crucible add-on and the copper add-on
([overview.md](overview.md)). The crucible furnace's ferrous half was pulled back onto the spine - D9
needs it.

---

## How to apply the cut

1. Do not propose anything on the deferral list.
2. If a doc implies otherwise, that doc is stale. Fix it to cite this page rather than re-argue.
3. Check the carve-outs before assuming. Producer gas, the phase-change model, the fluid tank, the steam
   condenser and the ferrous crucible furnace all look deferred and are not.
4. Non-ferrous ≠ Homestead. Non-ferrous is deferred later; Homestead is deferred elsewhere.

---

## Gotchas - contradictions the cut left behind

* `fluid-tank.md` contradicts the "no gasholder" ruling by naming the tank "the buffer for producer gas
  at the open hearth", where the gas producer rules flatly "no gasometer". Logged as the gas producer's
  open question, whose recommendation is to keep "no gasholder" as the product decision and let the tank
  stay medium-agnostic as the code decision.
* `conventions.md` still enumerates "coal gas and the chemistry fractions" as pipe media. Harmless as a
  statement about what the pipe network can carry; misleading as a statement of scope.
* Nothing on the deferral list exists in code. `grep -rni` over `src/` for
  `coalgas|sprinkler|gasholder|distill|retort|petcoke|graphite|electrolys` returns exactly one hit - a doc
  comment in `exlib/src/Fluids/IMediumTaxonomy.cs` mentioning distillation fractions - and `copper`
  appears only in vanilla-facing plumbing (the metal catalogue and tool emitter).

---

## Open

* Whether the sulfur dependency is real. The retired smex notes called the copper add-on's sulfur use
  cosmetic, which does not match the severity row above. It is the only one of elex's three with no
  recorded fallback, so its severity is worth being sure about when non-ferrous returns.
* Whether "Industrial Homestead" gets a doc. The domestic complex's design is recorded under
  [deferred/homestead/](deferred/homestead/gasworks.md) and in git history; if Homestead becomes a project,
  it starts there.
* Whether malleable cast iron is in or out. Deferred as an upgrade path above, but it has been carried
  as an in-suite open question too. It is one metal registration and one block.
* When the tilting crucible lands, R3 gets its first real merge. Nothing to decide now; worth a note in
  whatever brings non-ferrous back, because the mechanic is already written and idle.
