# Bearings (ball bearings + the ball die)

**Status** designed - nothing built. No item, no die, no recipe, no shape, no lang key, no metal
descriptor. `grep -rn -i "bearing" src/` returns six hits and every one is prose - a windage coefficient
comment, four uses of "load-bearing" in unrelated docstrings, and one "lid-bearing cell". Chrome steel does
not exist as a material either; it has no row in [materials.md](../materials.md). The three machines that
would make the parts - the [heading machine](heading-machine.md), the [rolling mill](rolling-mill.md)'s rod
route and the [bending roller](bending-roller.md) - are themselves designed-not-built or blocked.
**Mod** hpex (`HighPressureExpanded`)

## Owns

* the bearing as an entity: what it is made of, what it assembles from, and the ruling that it is made by a
  process, not forged (Fischer 1883);
* the **ball die**'s own spec - feedstock, yield arithmetic, torque requirement - and the ruling that a die
  on an existing bench is the answer rather than a new grinding machine;
* the chrome-steel supply chain end to end: chromite → ferrochrome → chrome steel → rod → balls, and which
  existing block owns each step;
* the bootstrap invariant - the exact set of machines that must not require a bearing, and the two
  independent reasons the loop is currently open;
* who requires bearings (the Corliss and above) and the rule that nothing already built is retrofitted,
  because earlier machinery runs on plain journal bearings abstracted into build cost;
* the race gap: no shipped tooling catalogue contains a ring race, and "large radius" is the wrong tool for
  one.

## Does not own — cited only, never restated

| Fact | Owner |
|---|---|
| the `ItemDie` tooling contract (fields, `TryParse`, fitting, refusal) and the die catalogue and who ships each entry | [heading machine](heading-machine.md) |
| the bench itself - footprint, drive contract, verbs, drops, why the spur gear is art | [heading machine](heading-machine.md) |
| bending as a verb, the three-rolls-in-a-triangle argument, the roller's tooling catalogue | [bending roller](bending-roller.md) |
| `grooved` schedules, `δ_max = μ²R`, `RollSetSpec`, `WorkPiece`, every `Rolling*` key | [rolling mill](rolling-mill.md) |
| the crop that turns `rolledrod` into 25 u rod, and the cold-cut `MinTorque` gate | [shear](shear.md) |
| the cold blast furnace's ferroalloy second act - burden family, coke cost, the fuel-against-throughput trade | [cold blast furnace](blast-furnace-cold.md), [STATE.md § the ferroalloy furnace](../../internal/plans/STATE.md) |
| melting a ferroalloy for the ladle | [cupola](cupola.md) |
| alloying in the bath vs in the ladle (D6) | [STATE.md § D6](../../internal/plans/STATE.md) |
| `1 vx³ = 2.5 u` and every stock mass | [density rule](../mechanics/density-rule.md) |
| the mpenergy run a bench loads and the flywheel that carries a pulsed blow | [mp-energy](../mechanics/mp-energy.md) |
| what hadfield, HSS and the industrial steels are | [materials.md](../materials.md) |
| code-first item defs, `ExRecipeCosts`, goldens | [recipes & config](../mechanics/recipes-config.md) |
| the HP machines that consume bearings | [hp hammer](hp-hammer.md), [STATE.md](../../internal/plans/STATE.md) |

**Depends on** [heading machine](heading-machine.md) · [bending roller](bending-roller.md) ·
[rolling mill](rolling-mill.md) · [shear](shear.md) · [cold blast furnace](blast-furnace-cold.md) ·
[cupola](cupola.md) · [density rule](../mechanics/density-rule.md) ·
[recipes & config](../mechanics/recipes-config.md) · [materials.md](../materials.md) ·
[STATE.md § N2](../../internal/plans/STATE.md)

---

## Role

A bearing is the first thing in the suite whose value is accuracy rather than material. Everything else the
mod makes is defined by what it is made of and how much of it there is - a plate is 600 u of steel, a rail is
a section, a boiler is grade-gated. A ball bearing is a few grams of steel that is only worth anything if it
is round to a tolerance, and no recipe in this suite can currently express that.

Friedrich Fischer's 1883 ball-grinding machine is the invention that created the ball-bearing industry:
before it, balls could not be made round enough for the bearing to matter
([STATE.md:477-479](../../internal/plans/STATE.md)).

Ruling: bearings are not forged and assembled. A grid recipe of the form `steel + steel → bearing` would make
the most process-dependent object in the game the cheapest one to obtain.

### But it needs no new machine: the ball die

Heading a ball from a short length of chrome-steel rod is the same verb the bench already performs for rivets
and nails ([STATE.md:481-484](../../internal/plans/STATE.md)). The die is the whole difference, the same argument that
made the [heading machine](heading-machine.md) one block instead of two.

| Reason a die beats a grinding machine | |
|---|---|
| It is the established extension point | a die is an ordinary item with an attribute, so hpex ships one item and touches no block - the contract's stated purpose ([heading machine § the `ItemDie` spec](heading-machine.md)) |
| A grinder would have exactly one product | a block whose only output is one item is the dangling end the placement rule forbids ([heading machine:170](heading-machine.md)) |
| The accuracy story is told by infrastructure | to get a bearing the player must have built the mill, the shear and the bench, and be running them off a flywheel |
| It respects one-machine-more-tooling | a different verb earns its own block ([bending roller](bending-roller.md) exists because bending is not reduction); heading a ball is not a different verb from heading a rivet |

Naming. [STATE.md:481](../../internal/plans/STATE.md) says *"give the cutter bench a ball die"*, which is ambiguous
between the two die-fed benches. [heading machine:32](heading-machine.md) resolves it to the header: the ball
die's feedstock is rod, and rod is what the header eats - the [nail machine](nail-machine.md) eats
`nailplate`.

### Nothing already built is retrofitted

Bearings are an hpex-tier item only. Earlier machinery ran on plain journal bearings, left abstracted into a
machine's build cost - no existing recipe changes, no existing machine gains a requirement, and no save is
invalidated ([STATE.md:486-488](../../internal/plans/STATE.md)). Line shafting, mill stands and beam engines ran on
babbitt and bronze journals for a century, and tin bronze is already listed in the mod's material table as
*"bearings, fittings, cocks"* ([materials.md:73](../materials.md)).

What changes when bearings arrive is that the high-speed machines become possible, and speed is the thing a
journal bearing cannot do.

---

## Structure — the assembly, not a footprint

A bearing is an item, so "structure" is its bill of parts and where each part is made.

| Part | Form | Made on | Owner of that machine |
|---|---|---|---|
| **balls** | chrome-steel rod @ 25 u → *n* balls | [heading machine](heading-machine.md) + the ball die | iwex ships the bench, hpex ships the die ([STATE.md:583](../../internal/plans/STATE.md)) |
| **race** | a rolled ring | [bending roller](bending-roller.md) | lpex |
| assembly | balls + race → bearing | a grid recipe | hpex |

Both parts are products the forming line already makes ([STATE.md:483-484](../../internal/plans/STATE.md)). No new
machine, no new mechanism, two new items and one die.

### The race gap

No shipped tooling catalogue contains a ring race. The [bending roller](bending-roller.md)'s four jobs are
conical → pipe from skelp · cylindrical → shells · cylindrical → barrels · large radius → wheel rims
([bending roller § the four jobs](bending-roller.md)). A bearing race is none of them.

"Large radius" is the wrong tool for it: a wheel rim is the largest curvature the machine makes, and a
bearing race is a ring a few voxels across, the tightest. The race is not the wheel-rim tool applied to
smaller stock; it is a fifth tooling entry, small-radius ring rolls, and adding it is a decision for
[bending roller](bending-roller.md)'s page, not this one.

Or it is not rolled at all. Two alternatives, both cheaper to build and both worse fits:

| Alternative | Why it is worse |
|---|---|
| bore a race out of a `blank` on the [boring machine](boring-machine.md) | a bore makes a hole, not a raceway groove; and it wastes the metal a ring saves |
| forge it on the [hp hammer](hp-hammer.md) with a die | this would be a real ≥ 2-voxel die and would fill that machine's empty catalogue - but it makes the bearing require a machine that itself requires bearings. See [the bootstrap invariant](#the-bootstrap-invariant) |

[STATE.md:483-484](../../internal/plans/STATE.md) settles it as rolled, and this page follows that. The open work is
which tool.

---

## Assets

Nothing exists.

| Asset | State |
|---|---|
| bearing item | none - no def, no code, no shape, no texture |
| ball item | none |
| race item | none |
| ball die item | none. Do not repeat `RollSetItemDefinitions.cs:121`, which still points its tooling item at `game:item/ingot` as a placeholder |
| chrome-steel metal | none - no descriptor, no ingot, no row in [materials.md](../materials.md) |
| lang / handbook | no key in `assets/hpex/lang/en.json`; `docs/hpex/handbook/` holds one page, `00-highpressure.html`, which does not mention bearings |
| reference art | the 1867 machine-tool plate at `assets/editable/refs/rivetsnails/` covers the bench, not the bearing. Folder is untracked |

A ball, a ring and an assembled bearing are sub-voxel shapes that can share one texture. Nothing here is
art-blocked.

---

## Construction — the chain

Every arrow below is a step that exists in the design. None of them is built.

```
game:ore-chromite / game:nugget-chromite        [VANILLA - exists today]
        │  vanilla nugget crushing @ 0.33 avg
        ▼
crushed chromite  ──burden──▶  COLD BLAST FURNACE, high-coke ferroalloy burden   [designed]
        │                          ferrochrome  (blast-furnace-cold.md § second act)
        ▼
chrome steel   ←── alloyed in the OPEN-HEARTH bath (D6, primary) or the LADLE (second option)  [designed]
        │
        ▼
long cell ──cast──▶ castbillet 3x3x27, 600 u  ──reheat──▶  ROLLING MILL, grooved 1.0   [B3/B4]
        │
        ▼
rolledrod 2x2x10, 100 u  ──shear──▶  rod @ 25 u (1x1x10)     [shear designed]
        │
        ▼
HEADING MACHINE + BALL DIE  ──▶  bearing balls          [bench designed, die not designed]

plate or bar ──BENDING ROLLER, ring tool──▶ ring race     [tool does not exist - see the race gap]

balls + race  ──grid recipe──▶  BEARING                   [no recipe]
```

### Where chrome steel comes from

Ferrochrome is a blast-furnace product, one member of the ferroalloy family - ferromanganese, ferrochrome,
later ferrosilicon - that gives the cold furnace its permanent second act
([STATE.md:322-324](../../internal/plans/STATE.md), [cold blast furnace § second act](blast-furnace-cold.md)). The
mechanism is a new burden family on an existing block, exactly as the [cupola](cupola.md) is a data override;
the cost is a very high-coke burden the cold furnace can just barely reach. This page adds nothing to that
model; it is the second customer for it.

Ferrochrome has two consumers: chrome steel here, and ~32 u of chromium per HSS heat far downstream
([materials.md:108](../materials.md)).

Chrome steel has no row in [materials.md](../materials.md). The shipped table runs Bessemer, open-hearth,
crucible, hadfield, HSS ([materials.md:20-26](../materials.md)); chromium appears only inside HSS's
composition ([materials.md:72](../materials.md), ~4 % Cr). Adding the row is a prerequisite, and it is also
where the grade question gets answered - under D3 an alloy inherits its base's grade as a continuous penalty,
so chrome steel on a Bessemer base and chrome steel on an open-hearth base are not the same product.

---

## The bootstrap invariant

Bearings gate HP machines. Chrome steel comes from a chain of machines. If any machine on that chain is
itself bearing-gated, hpex cannot be entered at all.

The chain is safe today, and both safeguards are accidental:

| Chain step | Machine | Tier / power | Bearing-gated? |
|---|---|---|---|
| chromite | vanilla ore + vanilla nugget crushing | none | no |
| ferrochrome | [cold blast furnace](blast-furnace-cold.md) | iwex, MP blower | no |
| melt / ladle the alloy | [cupola](cupola.md) · ladle · open hearth | iwex / smex, producer gas | no |
| cast the billet | [long cell](long-cell.md) | iwex | no |
| reheat | [reheat furnace](reheat-furnace.md) | iwex | no |
| roll to rod | [rolling mill](rolling-mill.md) | iwex, mpenergy | no |
| crop to 25 u | [shear](shear.md) | iwex, mpenergy | no |
| head the balls | [heading machine](heading-machine.md) | iwex, mpenergy | no |
| roll the race | [bending roller](bending-roller.md) | lpex, mpenergy | no |

Break 1 - chromite is a vanilla ore and vanilla already crushes it. `game:ore-chromite`,
`game:nugget-chromite` and `game:crushed-chromite` all exist in the base game, and smex's own EM-compat patch
preserves vanilla's 0.33-average nugget crushing for cassiterite, chromite and ilmenite
(`assets/smex/patches/compat/em/nugget-crushing.json:3`). A player can obtain crushed chromite with nothing
but a hand pulverizer.

If HP jaws were instead the only thing that cracked chromite, the loop would close:

> HP steam ore crusher *(hpex)* → chromite → chrome steel → balls → bearings → HP machines → …

The invariant is therefore: the HP ore crusher is a throughput machine, never the only source of chromite.
Vanilla's route must stay open. The crusher's point is bulk comminution against hand-pulverizing; implemented
as an exclusive gate it makes the tier unenterable.

Break 2 - the requirement stops at the Corliss. [STATE.md:467](../../internal/plans/STATE.md) requires bearings for
*"HP machines (Corliss)"*, and the Corliss is planned, not live. The two machines a player builds first in
hpex - the Lancashire boiler and the Cornish engine, both live - are beam-and-crank machines running at
engine speeds a journal bearing handles, and must stay unrequiring.

The safe requirement set, stated positively:

| Requires bearings | Must not require bearings |
|---|---|
| Corliss engine *(planned)* | Lancashire boiler *(live)* |
| Compound / tandem Corliss *(planned)* | Cornish engine *(live)* |
| elex's dynamo and alternator - Corliss flywheel variants | HP steam ore crusher *(planned)* - it is on the chromite path |
| any future high-speed rotary machine | Large Cornish pumping engine · large blast furnace · skip hoist |
| | every iwex, lpex and smex machine - nothing is retrofitted |

The gate lands on the Corliss because the Corliss's selling point is that it is governed and fast.

---

## Operation

A bearing has no verbs. It is consumed by a recipe and never placed, never fitted and never fitted-back.

| Consumer | What it buys | State |
|---|---|---|
| **Corliss engine** | the governed HP → MP mill engine | planned |
| **Compound / tandem Corliss** | the ~36 kW upgrade tier | planned |
| **elex dynamo / alternator** | both are Corliss flywheel variants | deferred |

The ball die has the full die verb set, and every one of them belongs to
[heading machine § Operation](heading-machine.md): RMB with a die fits it and hands back the previous one,
RMB with rod heads a piece, RMB empty collects from the tray, sneak + RMB takes the die back. Refusal must
return the tooling rather than eat it.

The one operational fact this page adds: the ball die's `Accepts` must match chrome-steel rod specifically. A
die that accepts any 25 u rod turns bearing balls into a wrought-iron item and deletes the entire chain above.
That is a `MinTorque`-style requirement expressed as a code match, and it is the only place in the design
where a die cares which metal it is fed.

---

## Numbers

Nothing is in config. No `Bearing*` or `Ball*` key exists in `HpexConfig.cs`. Rows below are either owned
elsewhere (cited) or proposed (marked).

### Owned elsewhere — the constraints the bearing is sized against

| Quantity | Value | file:line | Why it matters here |
|---|---|---|---|
| density rule | 1 vx³ = 2.5 u | [density rule](../mechanics/density-rule.md) | fixes every mass below |
| `rolledrod` | 2 × 2 × 10 = 40 vx³ = 100 u | [rolled-parts](../items/rolled-parts.md) | the mill's `grooved` 1.0 product |
| `rod` @ 25 u | 1 × 1 × 10 = 10 vx³ = 25 u, 4 per `rolledrod` | [rolled-parts](../items/rolled-parts.md) | the ball die's feedstock |
| `castbillet` | 3 × 3 × 27 = 243 vx³ = 600 u → 6 `rolledrod` | [stock](../items/stock.md) | one chrome-steel billet = 24 rods |
| `HeadingMinTorque` (proposed at the bench) | 0.3 | [heading machine:116](heading-machine.md) | the ball die should not undercut it |
| chromium per HSS heat | ~32 u on a 624 u OH base | [materials.md:108](../materials.md) | ferrochrome's second consumer |
| vanilla chromite nugget crush yield | 0.33 avg | `assets/smex/patches/compat/em/nugget-crushing.json:3` | the bootstrap break |
| `RccBrokenDropsRatio` (hpex) | 0.8 | `HpexConfig.cs:124` | what a broken bearing-bearing machine returns |
| `RecipeLevel` (hpex) | `"normal"` | `HpexConfig.cs:131` | the cost tier the assembly recipe prices at |

### Proposed — every one of these is unchosen

| Quantity | Proposal | Arithmetic |
|---|---|---|
| **ball** | 2.5 u = 1 vx³ | the smallest mass the density rule can express; anything smaller needs a fractional voxel |
| ball die `Count` | 10 balls per 25 u rod | 25 / 2.5 = 10 exactly. Mass-neutral, the rule the bench already follows for the bolt ([heading machine:118](heading-machine.md)) |
| **race** | 25 u | one 25 u rod's worth of ring, so a race and a rod-of-balls cost the same and neither part dominates |
| **bearing** | 8 balls + 1 race = 20 + 25 = 45 u | 8 leaves 2 balls over per rod, which is wrong; see [Open](#open) |
| ball die `MinTorque` | 0.3 | matches the bench's proposed floor; a ball is not harder to head than a bolt |
| ball die `Bench` | `heading` | the field that keeps it off the nail bench ([heading machine § the `ItemDie` spec](heading-machine.md)) |
| ball die `Accepts` | chrome-steel rod only | see [Operation](#operation) |
| bearings per Corliss | 4 | pure guess. It should fall out of the Corliss's build table, which does not exist |

The 10-balls / 8-per-bearing pair does not divide, and that is a defect in the proposal rather than a rounding
detail: every other quantity in the ladder was chosen so that every crop divides exactly
([rolled-parts](../items/rolled-parts.md)). Either the bearing takes 10 balls (one rod, one bearing's worth,
which makes the rod the natural unit) or the ball mass changes. Choose before shipping either number.

---

## Drops

| Broken | Returns |
|---|---|
| a bearing item | it is an item; nothing to break |
| the ball die, fitted to a bench | spawned at the block - the bench's `OnBlockBroken` spawns fitted tooling before `base`, copying `BlockEntityRollingMill.OnBlockBroken` (`:374-383`) ([heading machine § Drops](heading-machine.md)) |
| a machine built with bearings | hpex's RCC salvage ratio, `RccBrokenDropsRatio` 0.8 (`HpexConfig.cs:124`) - hpex carries its own because exlib's salvage lookup keys on the broken block's domain ([Lancashire boiler § Drops](boiler-lancashire.md)) |

Whether a salvaged Corliss returns its bearings intact or at 0.8 is a decision. A bearing is the one part in
that machine for which "80 % of a bearing" is meaningless. The
[recoverability](../mechanics/recoverability.md) principle (declared recovery, R2) says the answer must be
declared, not emergent.

---

## Code — where it will hook in

Nothing exists.

| Piece | Where | Model it on |
|---|---|---|
| the ball die item | wherever `DieItemDefinitions` lands, plus an hpex variant entry | `RollSetItemDefinitions.cs:17-128` - one item, a `type` variant group, per-variant specs via `.Raw("attributesByType", byType)` (`:126`) |
| the `ItemDie` spec it fills in | iwex (or exlib) - unresolved, see below | `RollSetSpec.cs:31-201`, `MoldSpec.cs:32-48` |
| ball / race / bearing items | `ExItemDef` in hpex | [recipes & config](../mechanics/recipes-config.md) |
| the assembly grid recipe | `src/HighPressureExpanded/Recipes/Grid/` - today it holds exactly one file | `MachineRecipeDefinitions.cs` |
| its cost key | `HpexRecipeConfig` | `src/HighPressureExpanded/HpexRecipeConfig.cs` |
| ferrochrome as a burden family + metal | the cold furnace's product override path | [cold blast furnace](blast-furnace-cold.md), [cupola](cupola.md) (the existing data-override precedent) |
| chrome steel as a metal | `MetalRegistry` + a [materials.md](../materials.md) row | [materials.md](../materials.md) |

Where `ItemDie` lives is an open cross-mod question and the ball die is the case that forces it.
[heading machine § Open](heading-machine.md) records it: the spec's obvious home is iwex beside
`RollSetSpec`, but lpex ships the rivet die and hpex ships this one. hpex references `ExpandedLib` and
`LowPressureExpanded` and, per its own csproj comment, does not reference smex
(`HighPressureExpanded.csproj:81`). It does reference lpex, and lpex references iwex, so an iwex-homed
`ItemDie` is reachable from hpex through the chain. The chain permits it, but nobody has checked that a
chrome-steel rod and a ball fit the same `Accepts` / `Output` / `Count` shape, which is
[heading machine § Open](heading-machine.md)'s own last item.

---

## Gotchas

* Never make the HP ore crusher the only chromite source. See
  [the bootstrap invariant](#the-bootstrap-invariant). The crusher buys bulk throughput; it never gates the
  alloying elements.
* Never require a bearing on the Lancashire boiler, the Cornish engine or anything upstream of chrome steel.
  Those are the machines a player builds to reach hpex.
* Do not retrofit. No existing recipe gains a bearing. Journal bearings are abstracted into build cost
  ([STATE.md:486-488](../../internal/plans/STATE.md)); adding one to an iwex machine would invalidate saves.
* The ball die must accept chrome-steel rod only. A wildcard `Accepts` deletes the whole chain.
* A bearing must not be craftable from an anvil or a grid alone. That is the ruling in [Role](#role); a
  `steel + steel → bearing` recipe defeats the entire design.
* The race has no tool. The [bending roller](bending-roller.md)'s catalogue has four entries and none is a
  ring; "large radius" is the wrong one. Decide before promising the part.
* Chrome steel is not in `materials.md` and has no grade. Under D3 an alloy inherits its base's grade as a
  continuous penalty, so "chrome steel" is really two products depending on whether the base was Bessemer or
  open-hearth.
* Fitting must hand the die back on refusal. `FitRollSet` removes the item before asking and puts it back if
  refused (`BlockRollingMill.cs:306-314`); getting the order wrong eats the player's die.
* `ResolveBlockOrItem` after `GetItemstack`. A stack read off a tree carries no resolved collectible
  (`BlockEntityRollingMill.cs:440-442`) - the bench's tooling slot has the same trap.
* The rod route is blocked upstream (B4). `grooved`'s first gap is 1.0 against 3.0 stock, a 2.0 draft against
  `δ_max` 1.0, so no rod can be made at all today ([STATE.md:51](../../internal/plans/STATE.md),
  `RollSetItemDefinitions.cs:86-93`, `RollingPass.cs:43`). Bearings sit directly downstream of it.

---

## Open

| # | Question | Blocking? |
|---|---|---|
| 1 | Balls per bearing, and balls per rod. 10 per rod and 8 per bearing do not divide; the ladder's rule is that every crop divides exactly. One rod = one bearing is the clean answer | yes |
| 2 | Which tool rolls the race - a fifth (small-radius ring) entry on the [bending roller](bending-roller.md), or something else. STATE.md settles that it is rolled; it does not settle how | yes |
| 3 | One race or two? [STATE.md:483](../../internal/plans/STATE.md) says *"balls + a rolled ring race"*, singular. A real bearing has an inner and an outer. Singular is simpler and is the settled text; two is the honest engineering. Pick and write it down | yes |
| 4 | Chrome steel needs a `materials.md` row, a `MetalRegistry` entry and a grade under D3 | yes |
| 5 | Ferrochrome needs the ferroalloy burden family, which is the cold furnace's unbuilt second act - no third burden family, no ferroalloy metal, nothing in `src/` ([cold blast furnace § Open](blast-furnace-cold.md)) | yes |
| 6 | Where `ItemDie` lives. The ball die is the case that forces the iwex-vs-exlib decision ([heading machine § Open](heading-machine.md)) | yes |
| 7 | How many bearings a Corliss costs. It should fall out of the Corliss build table, which does not exist - the Corliss is planned with no bill beyond a rough hadfield mass | |
| 8 | Is a bearing a wear part? Nothing in the suite wears out, and introducing wear for one item would be a new mechanic with one consumer. Default: no | |
| 9 | Does the ball die need `MinTorque` above the bolt die's? Chrome steel is harder than wrought iron, and `MinTorque` is the suite's established way of saying "harder metal costs more drive" ([shear § the cold-cut torque gate](shear.md)). It makes the material gate a cost rather than a lockout, consistent with D3 | |
| 10 | Should a race be forgeable on the [hp hammer](hp-hammer.md) as an alternative? It would fill that machine's empty ≥ 2-voxel die catalogue - but only if it is a second route, never the only one, or the bootstrap loop closes | |
