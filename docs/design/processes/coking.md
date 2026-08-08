# Coking

**Status** partial (verified against source 2026-08-07) - the product is live and charged, priced and burned
as its own bands by every shaft furnace, but the mod builds no oven. Vanilla's 3 × 3 × 3 chamber is the only
coke source in the game today; the bulk oven is designed only
**Mods** iwex (the designed oven and every consumer). No other mod participates - hpex, lpex and smex only
spend coke

**Owns** - the facts this page is canonical for:

* the coal → coke loop as it actually runs today, on vanilla's own oven, and the exact vanilla facts it
  stands on: the chamber form, the 12-hour cycle, the coke item's properties, and the three coal grades;
* the coke accounting - what one unit of carbon buys, what a cast pig costs in coke items, and the charcoal
  substitution expressed at item count rather than at carbon value;
* the `game:cokeovendoor` census: the five mod structures that consume the vanilla coke-oven door as a
  building part, and the competition that creates;
* the fact that the mod adds no coke item, no coke recipe and no coking code at all - the evidence;
* the scale argument: why a bulk oven is required rather than nice, stated as a rate against the vanilla
  chamber.

**Does not own** - cited only, never restated:
[beehive coke oven](../machines/coke-oven.md) - the machine: the two-chamber bank, its draft layout, the
crown-charging decision, the *lid closed = coking* rule, the block list, and the machine-level coke-only /
no-by-product scope decision ·
[scope](../scope.md) - the Industrial Homestead deferral, the producer-gas carve-out, the tar criterion,
and the graphite-electrode dependency chain that runs off it ·
[fuels](../items/fuels.md) - the `fuel` role, the per-item carbon values (coke 2, charcoal 1), and the
two-taxonomy rule that keeps raw coal out of a shaft ·
[layered charge](../layered-charge.md) - how coke reaches a furnace: its own bands, laid under burden ·
[burden](../items/burden.md) - the ore-bearing half of the charge (it carries no fuel) ·
[ironmaking](ironmaking.md) - the loop coke feeds ·
[heat balance](../mechanics/heat-balance.md) - what carbon at the raceway does once it is in a furnace ·
[cold blast furnace](../machines/blast-furnace-cold.md) · [cupola](../machines/cupola.md) ·
[puddling furnace](../machines/puddling-furnace.md) · [reheat furnace](../machines/reheat-furnace.md) ·
[gas producer](../machines/gas-producer.md) - the one gas that survived the cut ·
[roasting](roasting.md) - the other thing the same reverberatory would do

---

## What it is

Destructive distillation of coal. Coal is heated out of contact with air; it does not burn, it decomposes.
The volatile matter - tar, light oils, ammonia liquor and a rich flammable gas - leaves, and the fixed carbon
and ash fuse into a hard, porous, mechanically strong cellular mass. Raw coal crushes to dust under the
burden column and chokes a blast furnace; coke does not. Coke is the structural fuel, not merely the hotter
one.

Two oven families:

| | **Beehive** | **By-product** |
|---|---|---|
| Where the heat comes from | its own volatiles, burned inside the chamber | an external flue, fired separately |
| What it recovers | nothing but coke | coke, tar, ammonia, light oil, surplus gas |
| What it costs to build | a brick dome | a battery of retorts, condensers, scrubbers, a gasholder |
| When | from the 1830s, everywhere | Otto-Hoffmann, from the 1880s |

The beehive burns its by-products to pay for its own heat. That is why it was the cheap oven, and why the
by-product oven that replaced it was a different and far more expensive building. Modelling recovery on a
beehive would delete the reason ever to build the expensive one.

What the mod abstracts away:

| Real thing | What the mod does |
|---|---|
| Coal rank chemistry — only certain bituminous coals coke at all | modelled - bituminous only (ruled 2026-08-05). Lignite and anthracite do not coke; one coke item out. Coal type is a prospecting constraint on entering the iron tier |
| Volatile matter %, swelling index, coking pressure, wall damage | none |
| Quenching, the coke wharf, screening into nut/breeze/foundry sizes | none - coke is one item |
| The 48–72 h real cycle | 12 in-game hours (vanilla) |

---

## The loop

### Today — vanilla's oven is the only one

| # | Step | Where | Player verb | Out |
|---|---|---|---|---|
| 1 | mine coal | vanilla | pick | `ore-lignite` / `ore-bituminouscoal` / `ore-anthracite` |
| 2 | smith the door | vanilla anvil | smithing recipe | `game:cokeovendoor` (`.game/1.20/assets/survival/recipes/smithing/cokeovendoor.json`) |
| 3 | build the chamber | vanilla | lay a 3 × 3 × 3 cube of fire-clay or refractory brick, centre empty, one cardinal cell of the centre also empty, and hang the iron hatch door in it | — |
| 4 | charge | the centre cell | place coal layers | a `game:coalpile` |
| 5 | ignite and seal | the door | light with a torch, then shut the door | — |
| 6 | wait | — | 12 in-game hours | the pile converts to coke in place |
| 7 | draw | the chamber | break out the pile | `game:coke` |

Every fact in that table is vanilla's, quotable from one string:
`.game/1.20/assets/game/lang/en.json:5732` (`craftinginfo-cokeoven-text`). Fire-clay brick carries
`cokeOvenViableByType: { "*-fire": true }` (`.game/1.20/assets/survival/blocktypes/clay/brick.json:9-11`),
which is the tag the engine's chamber detection reads.

### Designed — the bulk oven

A bank of two chambers sharing a wall, charged through the crown and drawn through a side door, with
lid closed = coking as the operating rule. Layout, block list, why a `iwex:hopper-tall` and an `iwex:ovenlid`
rather than a trapdoor, and why not `game:cokeovendoor`, all belong to
[beehive coke oven](../machines/coke-oven.md). No part of it exists in `src/`.

### Nothing in this mod touches *coking* — though coke itself is first-class

A repo-wide search finds *(re-run 2026-08-07)*:

| Search | Result |
|---|---|
| `beehiveoven`, `beehiveovencore`, `ovenlid`, `chargelid` in `src/` | no hits ([beehive coke oven](../machines/coke-oven.md) § Status) |
| an iwex/lpex/smex/hpex coke *item* | none — the design uses vanilla `game:coke` verbatim |
| a recipe outputting coke | none |
| a recipe consuming coke | none either — fuel enters the economy only by being charged |

The JSON row (`materialroles.json:4`, `fuel`, value 2) is the single authority on what a coke item is worth.
Coke is load-bearing across the shaft family: the `fuel` role admits it to every shaft hopper, `IsFuelCode`
orders it under burden on the columns, `CarbonPerUnit` prices it at the raceway, and the burn-out retention
hands the unburnt top of a dead shaft back. All of that is [fuels](../items/fuels.md)' and
[layered charge](../layered-charge.md)'s; what is true here is that the making of coke has no mod code at
all - the process is vanilla's from coal to item.

---

## Inputs and outputs

### In — the three vanilla coals

| Coal | Burn temperature | Burn duration | file:line |
|---|---|---|---|
| `ore-lignite` | 1100 °C | 77 s | `.game/1.20/assets/survival/itemtypes/resource/ore-ungraded.json:103-105` |
| `ore-bituminouscoal` | 1200 °C | 84 s | `:107-109` |
| `ore-anthracite` | 1200 °C | 196 s | `:111-113` |

Only bituminous cokes *(ruled 2026-08-05)*. Lignite is low-rank and crumbles; anthracite is already near-pure
carbon and never softens into a coherent coke. The ruling binds the mod's own bulk oven (its scope is bulk
`game:coke` from bituminous coal, and its designed bed filter refuses lignite already). Caution: vanilla's
3 × 3 × 3 chamber is outside the mod's hands and cokes all three; the mod ships no patch against it, so until
the bulk oven exists the constraint is a design fact rather than an in-game one. Anthracite is not wasted by
the ruling - it is a first-rate heat fuel, and the reverberatory firebox takes it
([fuels](../items/fuels.md) § Two taxonomies).

### Out — one item, no by-products

| Out | Value | file:line |
|---|---|---|
| `game:coke` | burn temperature 1340 °C, duration 40 s | `.game/1.20/assets/survival/itemtypes/resource/coke.json:19-20` |
| | `class: "ItemCoal"` — coke is coal to the engine; it piles, stacks and burns through the same code | `:5` |
| | stacks to 64 | `:6` |
| **tar** | — | none |
| **coal gas** | — | none |
| **ammonia liquor / light oil** | — | none |

Coke burns 140 °C hotter than the best coal and for half as long as lignite: a temperature fuel, not an
endurance fuel.

---

## Numbers

Only what belongs to the process. The oven's own yield and cycle time are
[beehive coke oven](../machines/coke-oven.md)'s and are both undecided.

### Owned — what coke actually costs the ironmaking loop

Every input is cited to its owner, the arithmetic is this page's, and it is worked at the reference charge
(20 % carbon by items, standard 5 % flux). `MeltSpeedFactor` (0.5×–2.0×,
[heat balance](../mechanics/heat-balance.md)) multiplies iron per carbon, so a superheated furnace beats
every per-coke figure below - that is Neilson, and the point of hot blast.

| Quantity | Value | Derived from |
|---|---|---|
| carbon per coke item | 2 ÷ 2 = 1.0 | role value 2 (`materialroles.json:4`) over `BfFuelCarbonReference` 2 ([fuels](../items/fuels.md)) |
| carbon per charcoal item | 1 ÷ 2 = 0.5 — two charcoal carry one coke's carbon | role value 1 (`materialroles.json:5`) |
| burden melted per coke item | 4 items | `BfBurdenPerCarbonUnit` (`IwexConfig.cs:383`) |
| iron per coke item | 4 × 0.95 × 8.5 = 32.3 u | ore share at 5 % flux; `BfIronPerOreUnit` (`IwexConfig.cs:470`) |
| coke per cast pig (375 u) | 375 ÷ 32.3 = ≈ 11.6 items | `ItemPig.cs:39` |
| pigs per stack of coke | 64 × 32.3 ÷ 375 = ≈ 5.5 | coke stacks to 64 |
| charcoal per pig, same furnace | ≈ 23 items — and the course must be laid richer to melt at all | 2 : 1 carbon; break-even φ ≈ 0.32 by volume ([fuels](../items/fuels.md)) |
| coke to charge a full cold shaft at reference | 1 216 ÷ 5 = ≈ 243 items ≈ 3.8 stacks | shaft capacity 38 cells × 32 items, one item in five coke ([ironmaking](ironmaking.md) § Derived) |
| how long that coke burns, fully blown | 243 ÷ 0.35 = ≈ 11½ min | `BfRacewayCarbonPerTuyerePerSecond` × 2 tuyeres (`IwexConfig.cs:352`) |

Eleven-and-a-half coke per pig is the number that decides whether the bulk oven gets built. One vanilla
chamber is a 3 × 3 × 3 structure that produces one pile of coke per 12 in-game hours; a single cold-furnace
charge wants ~3.8 stacks and burns through them in about eleven minutes of blast. Scaling that with vanilla
chambers is a full-time job, which is the argument [beehive coke oven](../machines/coke-oven.md) § Role
makes.

The charcoal path is playable and punishing: two charcoal items per coke item of carbon (settled 2026-08-06 -
[fuels](../items/fuels.md) § What "value 2" means), charged as its own bands exactly like coke and priced at
the raceway per segment. A charcoal works hauls twice the volume and must lay a visibly richer course to
clear the melt line.

### Owned — the `game:cokeovendoor` census

The vanilla coke-oven door is the most reused vanilla part in the suite. Five mod structures require one, and
all five route through one helper - `VanillaCodes.Sealing` / `CokeOvenDoor` - so the wildcard-and-facing
reasoning lives in one place (`src/ExpandedLib/Definitions/VanillaCodes.cs:257-289`):

| Structure | Mod | Glyph | file:line |
|---|---|---|---|
| reheat furnace — firebox stoking door | iwex | `K` | `BlockHeatingFurnaceCore.cs:78` |
| puddling furnace — firebox stoking door | iwex | `K` | `BlockPuddlingFurnaceCore.cs:89` |
| Cornish boiler — firedoor | lpex | `d` | `BlockBoilerCornish.cs:88` |
| Lancashire boiler — firedoor | hpex | `d` | `BlockBoilerLancashire.cs:97` |
| cowper stove intake | smex | `D` | `BlockCowperStoveIntake.cs:57` |

Two consequences:

1. A player cannot build the reheat furnace, the puddling furnace, either boiler or a cowper without first
   having made the part that vanilla coking needs. The prerequisite is free - by the time coke matters, the
   door is a known item.
2. They compete. Each door spent on a building is a door not hanging in a coke chamber, and there is no
   in-game signal that the same part serves both purposes.

### Cited — owned elsewhere

| Fact | Owner |
|---|---|
| the bulk oven's chamber count, cell counts, layout, lid rule, block list, and the coke-only machine decision | [beehive coke oven](../machines/coke-oven.md) |
| coke yield per coal and cycle time for the bulk oven — both undecided | [beehive coke oven](../machines/coke-oven.md) Open #2 |
| the `fuel` role, the values 2 / 1, `CarbonPerUnit`, and the per-segment spend | [fuels](../items/fuels.md) |
| band order, courses, and how fuel is charged at all | [layered charge](../layered-charge.md) |
| what carbon at the raceway does to blast demand, gas, melt and campaign length | [heat balance](../mechanics/heat-balance.md) |
| the Homestead deferral, the tar criterion, producer gas, the electrode chain | [scope](../scope.md) |

---

## Why it is like this

### 1. No recovery, on purpose — and it is load-bearing three mods downstream

The beehive burns its volatiles for its own heat. The mod adopts that literally, so the mod produces no tar,
and that absence settles a scope question larger than an oven:

```
beehive burns its volatiles
      └─▶ no tar
            └─▶ no coal-tar pitch
                  └─▶ no graphite electrodes (Acheson: petcoke filler + coal-tar pitch binder)
                        └─▶ elex's arc furnace needs Industrial Homestead ...
                              └─▶ ... or the period-correct carbon electrode, which is in scope
```

Every link in that chain is [scope](../scope.md)'s to own (`scope.md:100-116`, `:177`, `:183-186`); what is
this page's is that the chain starts at the oven, and that the starting point is a metallurgical fact rather
than a scoping preference.

The degraded path is not a wall: early Héroult furnaces ran on coke-and-anthracite carbon electrodes, and
Acheson graphite is 1896 (`scope.md:183-185`). The mod's coke is an electrode feedstock; only the 1896
upgrade needs a gasworks.

### 2. Producer gas survives, coal gas does not — and coke is the reason

The surviving carve-out is producer gas, made by blowing air and steam through hot coke, yielding a lean fuel
gas and ash. Coal gas is made by carbonising coal in retorts and yields tar, ammonia and benzene. The
criterion is tar (`scope.md:108-110`), and the input is the tell: producer gas consumes the thing this oven
makes, coal gas consumes the thing this oven refuses to be. So the open hearth can burn gas
([gas producer](../machines/gas-producer.md)) without the mod acquiring a chemistry tier.

### 3. Vanilla coke, not a mod item

The oven produces `game:coke` and is built from vanilla fire-clay brick, which vanilla already tags as
coke-oven material. The player scales a process they already know rather than learning a new one - the same
trick the [cupola](../machines/cupola.md) plays on the blast furnace. It also means the bulk oven cannot
strand anything: coke made in it is coke, and every vanilla use still works.

### 4. Coke is the *structural* fuel, and the mod says so through pressure

Nothing in the code models coke's crush strength; the charge model gets the same result sideways. Coke is the
permeable skeleton of the charge column, so a coke-lean column packs dense and demands blast pressure the
iron tier cannot raise ([twin-tub blower](../machines/twin-tub-blower.md),
[heat balance](../mechanics/heat-balance.md) § Blast demand). The player experiences "not enough coke" as
the furnace will not blow, which is what raw coal in a blast furnace does.

---

## Gotchas

1. **The mod's own oven must not use `game:cokeovendoor`.** Its doors are `iwex:chargedoor`, so vanilla's
   chamber detection never fires inside the bulk oven's chambers
   ([beehive coke oven](../machines/coke-oven.md) § Gotchas). The consequence is that the walls being
   `cokeOvenViable` buys nothing mechanically - it is thematic only.

2. **The reverberatory firebox filters its fuel.** The fuel bed is `iwex:furnace-firebox`, and
   `BEBehaviorFirebox.IsFuel` filters what it takes: coke, bituminous, anthracite and charcoal in;
   lignite explicitly refused (`BEBehaviorFirebox.cs:55-82`). A firebox burns the raw coals a shaft must
   never see, because it wants heat, not a reductant that survives a burden column
   ([fuels](../items/fuels.md) § Two taxonomies).

3. **Coke is `ItemCoal`** (`coke.json:5`) - so a hand-piled lump of it is a `game:coalpile` like any other,
   which is what the heat-side machines read (the boilers' pile check, the cowper's under-stove pile). The
   shaft family does not: a shaft's standing charge is `iwex:furnace-chargepile`, a window onto the
   furnace-owned column, and coke enters it only through the hopper
   ([layered charge](../layered-charge.md)). One item, two substrates, on purpose.

4. **`materialroles.json` is where shaft fuel is defined, and it is short.** Two fuels, both vanilla
   (`:4-5`). Adding one is a JSON row and needs no C# ([fuels](../items/fuels.md) § Where a caller hooks in) -
   but the `fuel` role is a shaft-charge permit, so "is it cokeable/charge-safe" is exactly what a grant
   asserts, and raw coal must never receive it. `FuelRoleGrantTests` enumerates every grant and fails on one
   that is chargeable but unburnable, or priced but unacceptable.

5. **Vanilla's cycle is stated in in-game hours and the mod's machines all run on seconds.** The bulk oven's
   cycle time is undecided ([beehive coke oven](../machines/coke-oven.md) Open #2); whoever picks it has to
   decide which clock it runs on, because the shared furnace core is a per-second tick with game-time
   catch-up ([heat balance](../mechanics/heat-balance.md) § Away catch-up) and vanilla's oven is not.

---

## Open

1. **The whole bulk oven.** Block, block entity, layout in C#, goldens, recipe, the `iwex:ovenlid`, and the
   crown-charging fix - [beehive coke oven](../machines/coke-oven.md) § Open, items 1–7. Nothing exists.

2. **Yield and cycle time are undecided, and they are the only two numbers that matter.** The bulk oven must
   be a better rate than vanilla's, not merely a bigger box
   ([beehive coke oven](../machines/coke-oven.md) Open #2). This page supplies the demand side of that
   calculation - ≈ 11.6 coke per pig, ≈ 3.8 stacks per cold-shaft charge, burnt in ≈ 11½ minutes of blast
   (§ Numbers) - and nothing has been sized against it.

3. **Enforcement of the bituminous-only ruling.** The ruling (2026-08-05) binds the unbuilt bulk oven, while
   vanilla's own chamber still cokes all three coals (§ In). Until the bulk oven exists, nothing enforces it
   in game.

4. **Coke has no quality axis anywhere**, so there is no route by which a better oven produces better fuel -
   only more of it. The hook is real: the per-item fuel value (`materialroles.json:4`) is read live at the
   raceway as `CarbonPerUnit` ([fuels](../items/fuels.md)), so a premium coke would be one JSON row.
   Whether one should exist is undecided.

5. **Nothing totals coke consumption in game.** The hopper HUD shows the course being laid and the shaft
   line shows carbon %, but no surface shows what a campaign cost - the ≈ 11.6-coke-per-pig figure is
   invisible to the player who is paying it ([ironmaking](ironmaking.md) § The coke dial).

6. **Producer gas has no producer yet** in the ferrous line - [gas producer](../machines/gas-producer.md) is
   the one machine that would turn this page's output back into a fuel gas, and it is the carve-out the scope
   cut kept ([scope](../scope.md) § 1).
