# Expanded — design docs

One page per entity, and each page says what it owns. A page that cites another never restates it.

## What this tree is

Design only: systems, blocks, features and processes. Status, sequencing, progress and code
conventions live outside it: a page here is never also a task tracker.

| | |
|---|---|
| [plans/STATE.md](../internal/plans/STATE.md) | what is actually true right now - live vs designed vs blocked, every blocker with code evidence, and the open decisions. Read before trusting any page's status |
| [plans/NEXT.md](../internal/plans/NEXT.md) | what is being built right now and what comes next |
| [plans/iiex-bringup.md](../internal/plans/iwex-bringup.md) | the art queue and the playtest gates for bringing iiex online |
| [workbench/layouts.md](../../workbench/layouts.md) | multiblock layout scratchpad. Not a source of truth - the goldens are |
| [vanilla/](../internal/vanilla/README.md) | the vendored Vintage Story source: where its types live, the practices it follows, the traps it hides. What the game does, cited to a line |
| [CONTRIBUTING.md](../../CONTRIBUTING.md) | code style, comment conventions, formatting |

## Read first

| | |
|---|---|
| [layered-charge.md](layered-charge.md) | largely built - the two-stream burden, the charge-pile block, the burdenmaker, and the counter-current furnace. The charge column and counter-current model landed 2026-08-06; the burdenmaker landed 2026-08-07, replacing the retired ore mixer and ore bunker |
| [overview.md](overview.md) | the mod map, dependency chain and build order |
| [conventions.md](conventions.md) | the numbered invariants R1–R9, the units, the block-size vocabulary, the five network families |
| [scope.md](scope.md) | what was cut, why, and what was carved out of the cut. The release target |
| [materials.md](materials.md) | material identities, alloy ratios, ladle rules. Its unit table is retired - masses are derived (R9) |

## The tree

[mechanics/](mechanics/) - the shared models. Every machine cites these.

[density-rule](mechanics/density-rule.md) · [diagram-crafting](mechanics/diagram-crafting.md) ·
[gas-system](mechanics/gas-system.md) ·
[heat-balance](mechanics/heat-balance.md) ·
[metal-recovery](mechanics/metal-recovery.md) · [molten-network](mechanics/molten-network.md) ·
[mp-energy](mechanics/mp-energy.md) · [multiblock](mechanics/multiblock.md) ·
[naming](mechanics/naming.md) (home of R10) ·
[orientation-schemes](mechanics/orientation-schemes.md) · [pipe-network](mechanics/pipe-network.md) ·
[plant-layout](mechanics/plant-layout.md) · [recipes-config](mechanics/recipes-config.md) ·
[recoverability](mechanics/recoverability.md) · [tooling-wear](mechanics/tooling-wear.md)

[machines/](machines/) - one page per block, live and planned. Status is on the page.

*iiex - iron* · [blast-furnace-cold](machines/blast-furnace-cold.md) · [cupola](machines/cupola.md) ·
[puddling-furnace](machines/puddling-furnace.md) · [reheat-furnace](machines/reheat-furnace.md) ·
[coke-oven](machines/coke-oven.md) · [crucible-furnace](machines/crucible-furnace.md) ·
[firebox](machines/firebox.md) (shared fuel bed) ·
[charge-pile](machines/charge-pile.md) (the furnace charge block) ·
[design-table](machines/design-table.md) ·
[burdenmaker](machines/burdenmaker.md) ·
[tall-hopper](machines/tall-hopper.md) · [twin-tub-blower](machines/twin-tub-blower.md) ·
[casting-bed](machines/casting-bed.md) · [casting-cell](machines/casting-cell.md) ·
[long-cell](machines/long-cell.md) · [molten-canal](machines/molten-canal.md) ·
[rolling-mill](machines/rolling-mill.md) · [shear](machines/shear.md) ·
[nail-machine](machines/nail-machine.md) · [heading-machine](machines/heading-machine.md) ·
[stock-rack](machines/stock-rack.md) · [flywheel-and-shafting](machines/flywheel-and-shafting.md) ·
[iron-chutes](machines/iron-chutes.md)

*iiex - steam* · [boiler-cornish](machines/boiler-cornish.md) · [engine-watt](machines/engine-watt.md) ·
[cast-pipes](machines/cast-pipes.md) · [pumps](machines/pumps.md) · [fluid-tank](machines/fluid-tank.md) ·
[ore-crusher](machines/ore-crusher.md) ·
[steam-hammer](machines/steam-hammer.md) · [wide-hall](machines/wide-hall.md) ·
[bending-roller](machines/bending-roller.md) · [boring-machine](machines/boring-machine.md) ·
[gears](machines/gears.md)

*smex - steel* · [blast-furnace-hot](machines/blast-furnace-hot.md) · [cowper](machines/cowper.md) ·
[smokestack](machines/smokestack.md) · [bessemer](machines/bessemer.md) ·
[open-hearth](machines/open-hearth.md) · [ladle](machines/ladle.md) ·
[gas-producer](machines/gas-producer.md) · [steel-roll-sets](machines/steel-roll-sets.md)

*hpex - high pressure* · [boiler-lancashire](machines/boiler-lancashire.md) ·
[engine-cornish](machines/engine-cornish.md) · [rolled-pipe](machines/rolled-pipe.md) ·
[hp-hammer](machines/hp-hammer.md) · [bearings](machines/bearings.md)

[items/](items/) - the catalogues, and every mass with its `file:line`.

[pig](items/pig.md) · [burden](items/burden.md) · [fuels](items/fuels.md) · [stock](items/stock.md) ·
[rolled-parts](items/rolled-parts.md) · [cast-parts](items/cast-parts.md) ·
[fasteners](items/fasteners.md) · [alloys](items/alloys.md) · [blown-iron](items/blown-iron.md) ·
[roll-sets](items/roll-sets.md) · [patterns](items/patterns.md) · [dies](items/dies.md) ·
[economy-landing](items/economy-landing.md) (the settled mass batch - lands together or not at all)

[processes/](processes/) - the player's loops across machines.

[ironmaking](processes/ironmaking.md) · [coking](processes/coking.md) · [roasting](processes/roasting.md) ·
[puddling](processes/puddling.md) · [shingling](processes/shingling.md) · [rolling](processes/rolling.md) ·
[stamping](processes/stamping.md) · [bending](processes/bending.md) ·
[fabrication](processes/fabrication.md) · [casting](processes/casting.md) ·
[direct-charging](processes/direct-charging.md) · [recarburising](processes/recarburising.md) ·
[alloying](processes/alloying.md)

[deferred/](deferred/) - not being built. The pages hold the reasoning so it is not re-argued.

*non-ferrous* · [tilting-crucible](deferred/non-ferrous/tilting-crucible.md) ·
[copper-reverberatory](deferred/non-ferrous/copper-reverberatory.md) ·
[pierce-smith](deferred/non-ferrous/pierce-smith.md)

*elex* · [electrical-grid](deferred/elex/electrical-grid.md) · [dynamo](deferred/elex/dynamo.md) ·
[alternator](deferred/elex/alternator.md) · [arc-furnace](deferred/elex/arc-furnace.md) ·
[electrolysis-cell](deferred/elex/electrolysis-cell.md) · [wire-extruder](deferred/elex/wire-extruder.md)

*Industrial Homestead* · [gasworks](deferred/homestead/gasworks.md) ·
[chemistry](deferred/homestead/chemistry.md) · [gas-lighting](deferred/homestead/gas-lighting.md) ·
[climate-control](deferred/homestead/climate-control.md) · [kiln](deferred/homestead/kiln.md) ·
[sprinkler](deferred/homestead/sprinkler.md) · [oil](deferred/homestead/oil.md)

## Pointer pages

[diagram-crafting.md](mechanics/diagram-crafting.md) at the tree root is a pointer page - the mechanic lives at
[mechanics/diagram-crafting](mechanics/diagram-crafting.md) and the design table at
[machines/design-table](machines/design-table.md).

[layouts-workbench.md](../../workbench/layouts.md) is the multiblock drafting workbench - layouts are drafted
there by hand, and the shipped ones are copied back as snapshots. The blocktype goldens are the truth; a
snapshot can be stale.

## Writing a page

```markdown
# <Name>
**Status** live | shell | designed | art-only | blocked | deferred   **Mod** <owner>
**Owns** <the facts this page is canonical for — nothing may restate them, only link>
**Depends on** <links, so a change's blast radius is readable>
```

Then: Role · Structure · Assets · Construction · Operation · Numbers · Drops · Code · Gotchas · Open.
Processes swap the middle for What it is · The loop · Inputs and outputs · Why it is like this.

Three rules:

1. A page that cites must never also claim. Put it in `Owns` or link it - never both.
2. Every number carries `file:line`, and a hard-coded value is labelled as such.
3. Generated sections stay generated - masses from shapes (R9). A header saying "this is generated"
   is not a mechanism.
