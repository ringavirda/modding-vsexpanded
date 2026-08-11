# Heading machine
**Status** designed - nothing built; no block, no BE, no die item, no recipe, no shape   **Mod** iwex (`IronworkingExpanded`)

**Owns**
* the **`ItemDie` tooling contract** - the spec a die carries, how it is fitted, and the rule that a die-fed bench never names a product in code. Both this bench and the [nail machine](nail-machine.md) read it;
* the die catalogue and who ships each entry (nail → iwex, bolt → iwex, rivet → lpex, ball → hpex), and the reason it is a heading machine rather than a rivet machine;
* this bench specifically: rod @ 25 u in → a headed fastener out, its cast box bed / spur gear / heading cylinder form, footprint, drive contract, verbs and drops;
* why the spur gear in the reference is art, not a ratio.

**Depends on**
[mp-energy](../mechanics/mp-energy.md) (the run it loads; a heading blow is a hammer blow, i.e. a pulsed load) ·
[shear](shear.md) (crops the `rolledrod` into the 25 u rods this bench eats - the mill hands over nothing directly) ·
[rolling mill](rolling-mill.md) (the `grooved` schedule that makes rod) ·
[nail machine](nail-machine.md) (shares this page's `ItemDie` contract and the BE base - not the shape) ·
[multiblock & fillers](../mechanics/multiblock.md) · [density rule](../mechanics/density-rule.md) ·
[recipes & config](../mechanics/recipes-config.md) · [STATE.md § Fasteners](../../plans/STATE.md)

---

## Role

A bolt-and-rivet header: a short length of rod is gripped, and a die upsets a head onto the end of it. Fig 1 of the 1867 machine-tool plate is captioned "rivet making machine" and the same machine made bolts - the die was the whole difference, which is why this is one block with a tooling slot and not two blocks.

It is not a "rivet machine", and the distinction is a placement rule. Rivets belong to lpex: a rivet makes a joint that is strong and tight, so it arrives with the first thing that holds pressure - the boiler. Nails and bolts are strong but not tight, and they are the iron tier's fasteners. Putting the machine in lpex would strand the `grooved` 1.0 gap, whose product is the 25 u rod: an iwex mill making something only lpex can use is the dangling end the placement rule exists to prevent. Resolution: same rod, same bench, different die ([STATE.md § Fasteners](../../plans/STATE.md)).

| Die | Input | Output | Ships with |
|---|---|---|---|
| nail | `nailplate` | `game:metalnailsandstrips` | iwex - on the [nail machine](nail-machine.md), a different mechanism |
| **bolt** | rod @ 25 u | bolts | iwex - this bench |
| rivet | rod @ 25 u | rivets | lpex |
| ball | chrome-steel rod | bearing balls | hpex (N2 - bearings need a process, and heading is the verb it already performs) |

The mod rivets and bolts everything and has no source for either - bolts, nuts, studs and rivets are anvil-forged today. Bolts have no consumer waiting: the plated pipe tier is named for what the tube is made from, and its plate-and-nails cost is self-consistent (all four segments cost `Nails(1)` - `PipeRecipeDefinitions.cs:25`, `:34`, `:43`, `:52`). So what should cost bolts is an open question, and this bench's output needs a consumer chosen on its merits rather than inherited from a name.

One code path, three machines. What the shear, the nail machine and this bench share is the code, not the geometry: one die spec, one block-entity base, three shapes, three blocks.

---

## Structure

1 × 1 × 1 on `"mpenergy"`, cast-iron shafting. As with the other two benches: at one cell the block is its own `BlockNetworkNode`, so no filler, no multiblock, no projection.

| Aspect | Proposal | Note |
|---|---|---|
| Footprint | 1 × 1 × 1 | cheap; the player builds several, and they line up on one shaft |
| Form | cast box bed; a large spur gear on one side; the working heading cylinder / drum on the far side of that gear | Fig 1 of the 1867 plate |
| Orientation | `ns` / `we`, shaft along the orientation axis | `BlockRollingMill.cs:41-59`, `:106` |
| Drive | connectors on the two shaft-axis faces | power passes through a row of benches |
| Feed face | the side opposite the gear, where the drum works | the die sits where the player can see it |
| Direction | does not implement `IMpEnergyDirection` | last-writer-wins across the run (`MpEnergyNetwork.cs:75-77`) |

The spur gear is art. The mpenergy network has exactly one ratio device - `BlockTransmission`, whose ratio is a `switch` on the built variant (`BlockEntityTransmission.cs:59-64`) - and it is a separate block that couples two separate runs without merging them (`BlockTransmission.cs:20-26`). This bench must not invent a second ratio mechanism. If the gearing is to mean anything, express it as a higher `MinTorque` and a slower stroke than the ungeared nail bench, which is what a reduction buys.

---

## Assets

Nothing is drawn.

| Asset | State |
|---|---|
| editable shape | missing. The only bench shape in `assets/editable/shapes/` is `machine-megablock-nailcutter.json`, which is the [nail machine](nail-machine.md) - a different mechanism, and explicitly not to be reused |
| runtime shape | missing |
| die item art | missing - `RollSetItemDefinitions` still points its own tooling item at `game:item/ingot` as a placeholder (`RollSetItemDefinitions.cs:121`); do not repeat that |
| reference | `assets/editable/refs/rivetsnails/machine-tools-…-1867-technology-RY93PB.jpg` Fig 1, plus `the-portable-hydraulic-riveter-…-2E4KE93.jpg` and `vintage-boomer-and-boschert-hydraulic-press-….jpg` for the press family. Folder is untracked |
| lang / handbook | no key in `assets/iwex/lang/en.json`, no page in `docs/iwex/handbook/` |

Animation: a heading stroke clip plus `idle`. Same two conventions as everywhere on this network - one revolution per clip (`EnergyAnim.cs:23-24`) and a running clip must repeat or the mesh vanishes (`BlockEntityPuddlingChimneyCap.cs:31-33`).

---

## Construction

No recipe. Proposed, sitting between the nail bench (cheapest) and the [shear](shear.md):

| Slot | Ingredient | Rationale |
|---|---|---|
| box bed | `castplate` ×2 | the cast bed is the machine's whole silhouette |
| spur gear | `iwex:spurgear` | ships, with two grid routes (`EnergyRecipeDefinitions.cs:39-59`); the transmission's RCC already consumes it (`BlockTransmission.cs:72`) |
| drum | `game:metalplate-iron` ×2 | proposed |
| fasteners | `Nails(1)` (`ExIngredients.cs:36`) | bootstrap: the bench that makes bolts is built with nails |

Cost key `headingmachine-grid` in `IwexRecipeConfig.DefaultCatalogue` (`IwexRecipeConfig.cs:47-70`).

Dies are crafted separately and are the extension point: many dies, one machine. A die is an ordinary item with an attribute, so lpex adds the rivet die and hpex the ball die without touching this block.

---

## Operation

```
rod @ 25 u  +  fitted die  ──RMB on the bench──▶  one headed fastener bundle
```

| Verb | Effect |
|---|---|
| RMB with a die | fit it, handing back whatever was there; refused mid-stroke - mirror `TryFitRollSet` (`BlockEntityRollingMill.cs:143-155`) and `FitRollSet` (`BlockRollingMill.cs:296-320`), which returns the tooling to the slot on refusal and sends `SendIngameError` rather than eating it |
| RMB with rod | head it; refused with a reason if the fitted die does not accept the form - the `FeedVerdict` idiom (`MillFeed.cs:6-29`), where "wrong tooling" and "no tooling" must read differently |
| RMB empty | collect from the tray |
| Sneak + RMB | take the fitted die back |

Rate is the shaft. Each stroke is one heading blow drawn from the run; the bench's own flywheel is its local buffer, so a row of benches can run off one shaft, each buffering its own pulse rather than all of them pulling at once.

No temperature gate. Heading a 1 × 1 rod end is cold or warm work at this scale and nothing in the model gates it; what limits it is force, as with the [shear](shear.md).

---

## Numbers

All proposed - no config section, no keys, no code.

| Key | Proposed | file:line | What it does |
|---|---|---|---|
| `HeadingStrokeMs` | 250 ms | — (cf. hard-coded `PassTickMs`, `BlockEntityRollingMill.cs:42`) | stroke tick |
| `HeadingMinTorque` | 0.3 | — | matches the `grooved` set's shipped `minTorque` (`RollSetItemDefinitions.cs:92`), i.e. the bench costs about what rolling its own input costs - and sits above the nail bench, which is the geared/ungeared distinction expressed as force |
| `HeadingStrokesPerPiece` | 1 | — | one blow, one head |
| die `count` | 1 per 25 u rod | — | mass-neutral: a bolt is the rod plus a head, nothing is added or lost |

### The `ItemDie` spec - this page's contract

Modelled on `RollSetSpec` (`RollSetSpec.cs:31-38`) and `MoldSpec` (`MoldSpec.cs:32-39`): the tooling owns the data, the machine only reads it, so any mod adds a fastener with an item def alone.

| Field | Type | Meaning |
|---|---|---|
| `Bench` | `string` | which bench takes it - `heading` \| `nail`. The one field neither predecessor needs, and it is what keeps the nail die off this block |
| `Accepts` | `string[]` | item codes / wildcards the die will take (`rolledrod`-derived rod at 25 u; `nailplate`) |
| `Output` | `JsonItemStack` | what one operation yields - resolved against the world at use time, as `MoldSpec.Output` is (`MoldSpec.cs:30`) |
| `Count` | `int` | how many per input |
| `MinTorque` | `float` | drive the bench needs before the die will work |

`AttributeKey = "die"`, matching `RollSetSpec.AttributeKey` (`:66`) and `MoldSpec.AttributeKey` (`:42`). `TryParse(JsonObject?, out spec, out error)` returning a human-readable error so a bad die is a load-time complaint rather than a mystery at the bench (`RollSetSpec.cs:104-107`, `MoldSpec.cs:44-48`).

Hard-coded elsewhere, and relevant: network tick 1000 ms (`BlockNetworkModSystem.cs:42-45`) · `MpMaxSpeed` 2.0 (`ExlibConfig.cs:98`) · `ShaftInertia` 0.5 (`IwexConfig.cs:477`) · transmission ratios `x2/x4/clutch` (`BlockEntityTransmission.cs:59-64`).

---

## Drops

| Broken | Returns |
|---|---|
| the bench | itself, one item |
| the fitted die | spawned at the block - copy `BlockEntityRollingMill.OnBlockBroken` (`:374-383`), which spawns the roll set before `base` |
| a rod mid-stroke | handed back unchanged |
| the tray contents | all of them |

---

## Code

Nothing exists. `grep -i "heading\|rivetmachine" src/` finds nothing relevant.

| Piece | Where | Model it on |
|---|---|---|
| `ItemDie` (record + `TryParse`) | `src/IronworkingExpanded/BlockStructures/Forming/ItemDie.cs` | `RollSetSpec.cs:31-201` end to end - including the JSON shape decision: outputs as an array of objects, never a float-keyed object (`RollSetSpec.cs:159-161` explains why: `0.5` vs `"0.50"` never compare equal) |
| `DieItemDefinitions` | `.../Forming/DieItemDefinitions.cs` | `RollSetItemDefinitions.cs:17-128` - one item, a `type` variant group, per-variant specs via `.Raw("attributesByType", byType)` (`:126`) |
| `BlockEntityDieBench` (shared base) | `.../Forming/BlockEntities/` | `BlockEntityRollingMill.cs:24` - `BlockEntityNetworkNode` + `IMpEnergyConsumer` + `IProductionReadiness`; `NetworkType => "mpenergy"` (`:35-38`); `LoadTorque` 0 while idle (`:336-350`); tooling slot + `TryFit…` (`:147-182`); persistence via `SetItemstack` / `ResolveBlockOrItem` (`:428-460` - the resolve call is required or the loaded stack has no `Collectible`) |
| `BlockHeadingMachine` | `.../Forming/Blocks/` | `BlockRollingMill.cs:31`, minus `IFillerHost` / `IFillerInteractionTarget` |
| the decision | `.../Forming/DieFeed.cs`, pure | `MillFeed.Decide` (`MillFeed.cs:95-128`) - pure, so the whole rule set is pinned headless |
| the torque gate | inside that decision | `RollingPass.CanCarry` (`RollingPass.cs:126-127`) - no caller in `src/` today |
| stroke tick | a hosted `BEBehaviorProductionMachine`, server only | `BlockEntityRollingMill.cs:28`, `:48-53` - the process is added in the constructor and states its interval; the bench publishes its gate as `IProductionReadiness` (`:59`, `:63`) and writes the stroke in `OnProductionTick`. That buys the bounded `dt` and the away-catch-up for nothing (`BEBehaviorProductionMachine.cs:77`, `:113`) |
| def + recipe | `IExBlockDefProvider.Definitions(domain)` + `ExRecipeDef` | `BlockRollingMill.cs:44-64`, `CraftingStationRecipeDefinitions.cs:23-36` |

Where a caller hooks in. To add a fastener: ship one die item whose `die` attribute names `Bench`, `Accepts`, `Output` and `Count`. No block change, no dependency on iwex beyond the attribute shape - the same contract `MoldSpec` gives casting patterns (`MoldSpec.cs:18-25`). To add a bench: derive from the shared BE base and give it a different `Bench` string.

---

## Gotchas

- **Do not name the block "rivet machine".** The rivet die is lpex's; a block whose only shipped product came from another mod is the exact failure the placement rule forbids.
- **Bolts have no item and no recipe consumes them yet.** Plated pipe costs `Nails(1)` today (`PipeRecipeDefinitions.cs:25`). Shipping the bench without retargeting at least one recipe gives the player a machine whose output does nothing.
- **`rolledrod` and `rivetrod` do not exist either** (build item 8), and the `grooved` set cannot reach them: its first gap is 1.0 against 3.0 stock (`RollSetItemDefinitions.cs:89`) - a 2.0 draft against `δ_max = μ²R = 0.5² × 4 = 1.0` (`RollingPass.cs:43-44`, `IwexConfig.cs:506`). That is blocker B4, and it sits directly upstream of this bench.
- **The spur gear must not become a ratio.** See Structure. Two ratio systems on one network is how the transmission's `TryCouple` guard (`BlockEntityTransmission.cs:290`) stops meaning anything.
- **Float keys in JSON.** If the die ever keys anything on a thickness or a gap, read `RollSetSpec.cs:159-161` first - and note the double-vs-float authoring note at `RollSetItemDefinitions.cs:22-24`, which exists because a widened float destabilises the emitted def against its golden.
- **Fitting must hand the tooling back on refusal.** `FitRollSet` takes the item out of the slot before asking, and puts it back if the machine says no (`BlockRollingMill.cs:306-314`). Getting that order wrong eats the player's die.
- **`ResolveBlockOrItem` after `GetItemstack`.** A stack read off a tree carries no resolved collectible; both the mill (`BlockEntityRollingMill.cs:457-459`) and the hearth (`BlockEntityHeatingHearth.cs:148-150`) document the trap in-source, and in the hearth's case the symptom is a piece that silently fails to draw.
- **A run with no storage node has no state** (`MpEnergyNetwork.cs:81-89`).
- **Heading is not stamping and not shearing.** The [shear](shear.md) cuts across; a die on the [steam hammer](steam-hammer.md) cuts out; this bench upsets - it adds no geometry to a strip and removes nothing, which is why it is mass-neutral and neither of the other two is.

---

## Open

- **Nothing is built**, and two of its three inputs (`rolledrod`, the bolt item) do not exist.
- **Where the `ItemDie` type should live.** In iwex next to `RollSetSpec` is the obvious home, but lpex and hpex both ship dies - so either they take an iwex reference (the chain allows it: `exlib ← iwex ← lpex ← smex`) or the spec moves to exlib beside the other cross-mod contracts. `MoldSpec` set the precedent by staying in iwex (`MoldSpec.cs:6`); no one has re-examined it since lpex gained a die.
- **One bench with a `Bench` field, or one bench per die family?** The field exists in this page's spec to keep the nail die off this block; if the nail bench ends up hard-coding its die (see [nail machine § Open](nail-machine.md)) the field is dead weight.
- **What a "bolt" is as an item** - one bolt, a bundle, or a `bolts-and-nuts` composite in the shape of vanilla's `metalnailsandstrips`. The vanilla precedent argues for a bundle, and the mass ledger then fixes the count.
- **The rivet die has no home to be written into** - lpex's forming pages ([steam-hammer](steam-hammer.md), [wide-hall](wide-hall.md)) do not yet claim it.
- **Bearing balls (hpex, N2) assume this contract survives to that tier.** Nothing has checked that a chrome-steel rod and a ball die fit the same `Accepts`/`Output`/`Count` shape.
- **STATE.md's proposed page list still names `rivet-machine.md`.** This page replaces it; the list should be corrected when the machines index is written.
