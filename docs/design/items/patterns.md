# Casting patterns

**Status** live - 8 iiex types × 12 woods = 96 items, each carrying a `mold` spec that the casting
stations read, validated at load and pinned by a golden. Both stations are served: five `cell` types and
the three `longcell` stock types (the cast stock ladder). The craft chain works end to end except that its
first link, the diagram, is creative-only. Five drawn impressions still have no pattern, and no second mod
has shipped a pattern yet.
**Mod** iiex owns the pattern system and every shipped entry; the `Itemtype()` factory is the seam a
second mod contributes through.

Type names follow the drawn art (settled 2026-08-05) and are plural where the impression yields more than
one piece: `castbillets` (3 lanes), `castblooms` (2), `castslab` (1). The items stay singular.

## Owns

* the pattern as an item - one `pattern` itemtype × 8 types × 12 woods = 96 items, the
  type-first-wood-last variant ordering and the three things that depend on it, `MaxStackSize(1)`, and the
  fact that a pattern is a tooling family that wears;
* the pattern → diagram → recipe chain - the type list as the one source that carries a diagram variant
  and a grid recipe automatically, and the creative-only gate that sits one level above the pattern;
* the art rule and the full art census - a pattern wears the cast item's own shape with the texture
  swapped to plain wood; which item shapes are untracked; and the unreferenced filling shapes, with the
  settled pattern each belongs to;
* the cross-mod pattern roster - every pattern that exists in code or is settled on paper, and which mod
  owns it;
* the item-side authoring of `MoldSize` - which shipped patterns declare `size: longcell` and which take
  the `cell` default;
* the lang census across three languages.

## Does not own — cited only, never restated

| Fact | Owner |
|---|---|
| the `mold` **attribute schema**, every field's meaning and validation rule, where the spec is read, the shipped catalogue's **capacities / cavity boxes / outputs / `minPourTemp`**, `WoodenPatternDurability`'s value, ram-up / shake-out, misrun and short pour | [casting cell](../machines/casting-cell.md) |
| the long cell's drawn geometry, its four impression cavities, its lane ladder and its build list | [long cell](../machines/long-cell.md) |
| the spec-carrying **tooling idiom** itself - its five invariants and the four places a new family must touch | [roll sets](roll-sets.md) |
| `1 vx³ = 2.5 u`, the audit of every cavity capacity against it, and `materialUnits` being dead data | [density rule](../mechanics/density-rule.md) |
| the bed the cell is deliberately not, and the launder that feeds both | [casting bed](../machines/casting-bed.md), [molten canal](../machines/molten-canal.md) |
| the ≤ 32 / ≤ 48 handling invariant the cast stock ladder must satisfy | [recoverability](../mechanics/recoverability.md) |
| code-first defs, `attributesByType`, the goldens harness, the cost catalogue | [recipes & config](../mechanics/recipes-config.md) |
| the diagram-crafting system as a system | [diagram-crafting](../mechanics/diagram-crafting.md) |

**Depends on** [casting cell](../machines/casting-cell.md) · [long cell](../machines/long-cell.md) ·
[roll sets](roll-sets.md) · [density rule](../mechanics/density-rule.md) ·
[recipes & config](../mechanics/recipes-config.md) · [dies](dies.md) ·
[STATE.md § placement rule](../../internal/plans/STATE.md)

---

## Role

A pattern is **the wooden positive a sand mold is rammed around**, and in this mod it is the item that tells
the casting cell what to cast. The cell never names a mod, a shape or a product in code; it reads the `mold`
attribute off the held stack (`BlockEntitySandCastingCell.cs:69`, `:77`) and does what it says. So *"a mod
adds a castable part with a pattern def alone - no cell change and no dependency on iiex beyond the shared
attribute shape"* (`MoldSpec.cs:22-25`).

That is the same contract [roll sets](roll-sets.md) carries and the designed [dies](dies.md) copy.

The pattern is the odd member of the family:

| | Roll set | Die *(designed)* | **Pattern** |
|---|---|---|---|
| stack size | 1 | 1 | 1 |
| wears out | ruled: yes ([tooling-wear](../mechanics/tooling-wear.md)) | ruled: yes | yes, first - `durability` is declared on the def (`PatternItemDefinitions.cs:318`) |
| cosmetic variant axis | none | none | wood, 12 states, spec-irrelevant (`:253`) |

The wood axis is why the pattern's variant machinery is more complicated than either sibling's, and it is the
only reason the mold wildcard needs a trailing `-*`.

---

## The catalogue

A pattern has no mass row. Like every tooling item in the suite it declares neither `materialUnits` nor
`MaterialDensity` (confirmed by `test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/pattern.json`), so the
[density rule](../mechanics/density-rule.md) has nothing to check on the pattern itself. It does carry a
`capacity` for the part it casts, and that number belongs to
[casting cell](../machines/casting-cell.md) § The shipped pattern catalogue.

### Shipped — eight types × twelve woods = 96 items, all iiex

| Item | Casts | Output kind | Station | Impression shape |
|---|---|---|---|---|
| `iiex:pattern-castheavyplate-{wood}` | `iiex:castplate-heavy` | item | `cell` | `iiex:casting/cell-filling-heavyplate` |
| `iiex:pattern-castingotmold-{wood}` | `iiex:casting-mold-ingot` | block | `cell` | `…/cell-filling-ingotmold` - the tray is one bay, 100 u, one ingot |
| `iiex:pattern-castbarrel-{wood}` | `iiex:cast-barrel` | item | `cell` | `…/cell-filling-moltenbarrel` |
| `iiex:pattern-castwheelsection-{wood}` | `iiex:castwheelsection` | item | `cell` | `…/cell-filling-flywheelpart` - the filling keeps the older name |
| `iiex:pattern-castshell-{wood}` | `iiex:castshell` | item | `cell` | `…/cell-filling-castshell` |
| `iiex:pattern-castbillets-{wood}` | cast billet stock ×3 | item | `longcell` | `…/longcell-filling-billets` |
| `iiex:pattern-castblooms-{wood}` | cast bloom stock ×2 | item | `longcell` | `…/longcell-filling-blooms` |
| `iiex:pattern-castslab-{wood}` | cast slab stock | item | `longcell` | `…/longcell-filling-castslab` |

The `Molds` table is `PatternItemDefinitions.cs:108-230`; the shape table is `:92-101`.

One type casts a block rather than an item (`outputType: "block"`) - the iron ingot mold that replaces the
fired-clay one. That is the cell's bootstrap job and the only reason the `outputType` parameter exists on the
`Mold` helper. No plate-mold pattern (retired 2026-08-05): `game:metalplate-*` is a rolled product, so
casting plate in a tray duplicated the mill.

Ownership of the shell and the wheel section (settled 2026-08-05): both are iiex's. The wheel section feeds
iiex's flywheel (four segments, eight for the large wheel), and the shell builds the
[ladle](../machines/ladle.md)'s cast variant, which is iiex's. The shape is *iiex owns it, iiex consumes it*
(water tank, ore crusher, engine housings), which needs no cross-mod pattern indirection: the pattern and the
part live in one mod (`PatternItemDefinitions.cs:144-147` records the reasoning in-source).

### Settled but unwritten — five more patterns, one more mod

Each row is a drawn impression with no `Molds` entry. The mod column follows the placement rule
([STATE.md](../../internal/plans/STATE.md)): a pattern lives with the content it feeds.

| Pattern *(proposed)* | Would cast | Station | Impression already drawn | Owner |
|---|---|---|---|---|
| `castframe` | the machine frame | `longcell` | `longcell-filling-castframe.json` | iiex (machine parts) |
| `cylinder` | engine cylinder blank | `cell` | `cell-filling-cylinder.json` | iiex |
| `axle` | cast-iron axle blank | `cell` | `cell-filling-axle.json` | iiex |
| `gearblanksmall` | small gear blank | `cell` | `cell-filling-gearblanksmall.json` | iiex |
| `gearblanklarge` | large gear blank | `cell` | `cell-filling-gearblanklarge.json` | iiex |

The cross-mod seam is built and has no outside caller yet. `PatternItemDefinitions.Itemtype` builds a mod's
whole `pattern` itemtype from its own mold table (`PatternItemDefinitions.cs:272-280`), the cell's
recognition gate is domain-blind (`FirstCodePart() == "pattern"`, `BlockEntitySandCastingCell.cs:188`), and
the spec is read off whichever pattern is held - so iiex ships its five without iiex naming a single iiex
code. `src/IronIndustryExpanded/` contains no casting folder and no pattern provider today, so the five rows
above are the first exercise of that contract. Caution: an in-source comment still calls iiex's `castshell`
"the first outside caller" (`PatternItemDefinitions.cs:276`) - the shell is iiex's and always compiled from
iiex; the comment is stale.

A roll blank belongs on this list and is not on it.
`assets/editable/shapes/item-rollers-castblank.json` is a drawn cast blank and the rolls are chilled cast
iron in every tier, so the shortest route to a craftable roll set runs through a pattern entry that nobody
has proposed ([roll sets § Construction](roll-sets.md)).

---

## Numbers

### The item definition — every constant it declares

| Property | Value | file:line |
|---|---|---|
| itemtype code | `pattern` (asset `{domain}:itemtypes/pattern.json`) | `PatternItemDefinitions.cs` (emit `:300-320`) |
| fallback shape | `game:item/plate` | the emit |
| per-type shape | `shapeByType["*-{type}-*"]` from the shape table | `:92-101` |
| texture (`all`) | `game:block/wood/debarked/{wood}` - overrides whatever the borrowed shape declares | the emit |
| variant group 1 | `type` over `PatternTypes` (8 states, derived from `Molds.Keys`) | `:247` |
| variant group 2 | `wood` over `PatternWoods` (12 states) | `:253`, `:315` |
| items emitted | 8 × 12 = 96 | — |
| max stack size | 1 | the emit |
| durability | `WoodenPatternDurability` - value and the charge rule are [casting cell](../machines/casting-cell.md)'s | `:241`, `:318` |
| per-variant specs | `attributesByType["*-{type}-*"]` | the emit |
| creative inventory | `*` | the emit |
| `materialUnits` / `MaterialDensity` | none declared | — |

### Why type comes first and wood comes last

Three separate mechanisms key off the ordering, and all three break if it is reversed:

| Mechanism | Wildcard |
|---|---|
| the mold spec | `"*-" + type + "-*"` - the trailing `-*` absorbs the wood |
| the item shape | `"*-" + type + "-*"` - same shape |
| the lang key | `item-pattern-{type}-*` |

The spec is per type, not per wood: every wood of a type casts the same part, so the 12-way axis costs one
wildcard character and nothing else. A pattern family that keyed the spec per variant would emit 96 specs
instead of 8.

### The `mold` field roster — pointers, not rules

Meanings and validation rules belong to [casting cell](../machines/casting-cell.md) § The mold spec. This
table exists so a pattern author knows what an entry must contain and where to read the rule.

| JSON key | C# member | Required? | Default | Rule lives at |
|---|---|---|---|---|
| `size` | `Size` | no | `"cell"` | `MoldSpec.cs:33`, `:60-71` |
| `shape` | `Shape` | yes, non-blank | — | `MoldSpec.cs:34`, `:73-78` |
| `capacity` | `Capacity` | yes, > 0 | — | `:35`, `:80-85` |
| `cavity` | `Cavity` | yes, ≥ 1 well-formed box | — | `:36`, `:87-103` |
| `output` | `Output` | yes, must carry a `code` | — | `:37`, `:105-110` |
| `minPourTemp` | `MinPourTemp` | no | `0f` = check disabled | `:38`, `:112`; helper default 1150 °C |

Caution: the parser's default and the authoring helper's default disagree, silently.
`MoldSpec.TryParse` defaults `minPourTemp` to 0, which disables the misrun check entirely
(`MoldSpec.cs:112`); `PatternItemDefinitions.Mold` defaults it to 1150. So every iiex pattern gets a misrun
check and any third-party pattern that omits the key gets none, with no warning either way.

### `MoldSize` — authored and enforced

`MoldSize` has two members, `Cell` and `LongCell` (`MoldSpec.cs:9`), and both sides of it are live:

* enforced at the station - each station declares the one size it takes
  (`BlockEntitySandCastingCell.AcceptedSize`, `:54`, refusing with `iiex-castingcell-wrongsize`; the long
  cell overrides to `LongCell` with `iiex-longcell-wrongsize`, `BlockEntitySandCastingLongCell.cs:25-28`);
* authored on the items - the three long-cell stock patterns declare `size: longcell`
  (`PatternItemDefinitions.cs:198`, `:206`, `:214`); the five `cell` types take the helper's default.

So a pattern rammed at the wrong station is a refusal with a message, not a silent 28-voxel impression in a
12-voxel cell.

---

## Assets

### The pattern's own art rule

A pattern wears the cast item's shape with the texture swapped to plain wood. `shapeByType` picks the part's
shape; the `all` texture wildcard overrides whatever that shape declares (cast iron) with the plank's
debarked wood. A type with no shape-table entry falls back to `game:item/plate`, which is also a trap: a
typo'd key looks exactly like missing art (`PatternItemDefinitions.cs:278-280`).

The family therefore needs no bespoke pattern art: a new castable part's pattern art is already drawn the
moment the part's own item shape is.

| Type | Borrowed item shape | Tracked? |
|---|---|---|
| `castheavyplate` | `iiex:item/heavyplate` | untracked (`??`) |
| `castingotmold` | `iiex:item/ingotmold` | untracked, exported 2026-08-05 from `item-sandcast-ingotmold` |
| `castbarrel` | `iiex:item/cast-barrel` | tracked |
| `castwheelsection` / `castshell` | `iiex:item/castwheelsection` / `…/castshell` | untracked |
| `castbillets` / `castblooms` / `castslab` | `iiex:item/castbillet` / `…/castbloom` / `…/castslab` | untracked; drawn to stale lengths ([stock](stock.md) § Assets) |

Caution: most of the shapes the item def references are untracked in git, so a clean clone renders those
pattern families as `game:item/plate`, not as a missing-asset error.

### The impression census

A filling shape is named by a pattern's `mold.shape` and by nothing else, so a grep over every `*.cs` in
`src/` and `test/` for each filename gives an exact answer:

| Filling shape (`assets/iiex/shapes/casting/`) | Referenced by | Git | Belongs to |
|---|---|---|---|
| `cell-filling-base.json` | `CastingCellLogic.cs`, `BlockEntitySandCastingCell.cs` | tracked | the flat rammed sand |
| `cell-filling-half.json` | `CastingCellLogic.cs` | tracked | the legacy half sand |
| `cell-filling-heavyplate.json` | `PatternItemDefinitions.cs` | tracked | `castheavyplate` |
| `cell-filling-ingotmold.json` · `-moltenbarrel.json` | `PatternItemDefinitions.cs` | the ingot one is a 2026-08-05 export | the ingot mold and the barrel. `cell-filling-plate.json` and `-doubleingot.json` were deleted with the molds they impressed |
| `cell-filling-castshell.json` | `PatternItemDefinitions.cs` | `??` | `castshell` |
| `cell-filling-flywheelpart.json` | `PatternItemDefinitions.cs` | `??` | `castwheelsection` |
| `longcell-filling-billets.json` · `-blooms.json` · `-castslab.json` | `PatternItemDefinitions.cs` | `??` | the long-cell stock patterns |
| `cell-filling-axle.json` | nothing | `??` | proposed iiex `axle` |
| `cell-filling-cylinder.json` | nothing | `??` | proposed iiex `cylinder` |
| `cell-filling-gearblanklarge.json` · `-gearblanksmall.json` | nothing | `??` | proposed iiex gear blanks |
| `longcell-filling-castframe.json` | nothing | `??` | proposed iiex `castframe` |
| `longcell-filling-base.json` · `-half.json` | nothing | `??` | the long cell's plain sand meshes - they belong to the block, not to a pattern |

Five impressions still need a pattern (axle, both gear blanks, `cylinder`, `castframe`) plus the two block
meshes. Every filling shape, referenced or not, is still untracked in git - Open 5 below. `cylinder` is meant
to teach why a cored mold is harder than a flat one, and it has neither a pattern nor a core
([casting cell § Open](../machines/casting-cell.md)).

The editable sources for the long-cell fillings are
`assets/editable/shapes/molten-sandlongcellfilling-{billets,castblooms,castframe,castslab,full}.json`
plus `molten-megablock-sandlongcell.json` (untracked); the older names are marked deleted in the working
tree.

### Lang

Every shipped type carries `item-pattern-{type}-*` in en/ru/uk (`assets/iiex/lang/en.json:340` region), and
each type's diagram carries its own row too - `item-diagram-item-{type}`, `en.json:148` region - so a new
pattern type needs two lang rows per language, not one. There is no `item-pattern-*` catch-all, so a type
added without its row shows a raw code.

---

## Construction — the chain a pattern sits in the middle of

```
design table [not built]  ──▶  diagram-item-{type}   (creative-only today)
                                    │  + knife (tool) + 2 planks
                                    ▼
                            pattern-{type}-{wood}     ← this page
                                    │  RMB the cell (ram up, costs 1 durability)
                                    ▼
                            an impression in green sand
```

| Link | Where | Note |
|---|---|---|
| the diagram item | `DiagramItemDefinitions.cs:56-58` | `PatternDiagramTypes` is derived from `PatternItemDefinitions.PatternTypes` with an `"item-"` prefix - *"so a new castable part gets its diagram for free"* |
| the craft | `PatternRecipeDefinitions.cs:20-41` | one grid recipe per type, pattern `DKP` 3 × 1: diagram (tool) + knife (tool) + 2 `game:plank-*`, the plank's wood captured into the output variant (`:33-37`) |
| the output | `:39` | `iiex:pattern-{type}-{wood}` × 1 |

One `Molds` entry buys three things. Adding a castable part is one mold row plus the output item and the
filling shape, because the mold table is the single source the item variants
(`PatternTypes = [.. Molds.Keys]`, `:247`), the diagram variants and the grid recipes all derive from.

The gate is one level above the pattern. The diagram is creative-only until the design table can draft it
(`PatternRecipeDefinitions.cs:13-16`), so the pattern has a working recipe that no survival player can reach.
That is the [diagram-crafting](../mechanics/diagram-crafting.md) phase, not a pattern defect.

`PatternWoods` is a hand-written 12-entry literal (`PatternItemDefinitions.cs:253`), not derived from
vanilla's plank set. A vanilla wood added by an update is silently uncraftable-into.

---

## Code

| Piece | file:line | Role |
|---|---|---|
| `MoldSpec` | `src/IronIndustryExpanded/BlockStructures/Casting/MoldSpec.cs:32` | the record; `TryParse` at `:49-116`; `AttributeKey` at `:42` |
| `MoldSize` | `:9-16` | `Cell` · `LongCell`; enforced via `AcceptedSize` |
| `PatternItemDefinitions` | `…/Casting/PatternItemDefinitions.cs` | `IExItemDefProvider`; shapes `:92-101`, `Molds` `:108-230`, `LongCellPatternTypes` `:233`, `PatternTypes` `:247`, `PatternWoods` `:253`, `Itemtype` `:272` |
| `PatternValidation.Validate` | `…/Casting/PatternValidation.cs:19-31` | the `AssetsFinalize` sweep; pure over a collectible sequence; called from `IronworkingExpandedModSystem.cs` |
| the recognition gate | `BlockEntitySandCastingCell.cs:188` | `FirstCodePart() == "pattern"` - domain-blind, the cross-mod contract |
| the spec read + size gate | `BlockEntitySandCastingCell.cs:69`, `:77`, `:54` | where a pattern's spec is resolved and its size checked |
| `DiagramItemDefinitions` | `…/Items/DiagramItemDefinitions.cs:26` | `PatternDiagramTypes` derived at `:56-58` |
| `PatternRecipeDefinitions` | `…/Recipes/Grid/PatternRecipeDefinitions.cs:19` | one recipe per type |
| golden | `test/…/goldens/iiex/itemtypes/pattern.json` | pins every spec and both variant groups |
| tests | `test/IronIndustryExpanded.Tests/Blocks/Casting/MoldSpecTests.cs` · `PatternValidationTests.cs` · `PatternCodeLayoutTests.cs` | schema · validation · the `pattern-{type}-{wood}` code layout |

Where a caller hooks in. To add a castable part from any mod: build a `pattern` itemtype off
`PatternItemDefinitions.Itemtype` with its own mold table, ship a filling shape in its own domain, and the
output item. Nothing in iiex changes and nothing needs to reference the calling mod
([casting cell § Where a caller hooks in](../machines/casting-cell.md)). The generic machinery - the
`attributesByType` wildcard, the validation sweep, the load-time error - is
[roll sets § the idiom](roll-sets.md).

---

## Gotchas

* `minPourTemp` defaults differently in the parser and in the authoring helper - 0 (check off) vs 1150
  (check on). A third-party pattern that omits the key ships without a misrun check and nothing says so. See
  [the field roster](#the-mold-field-roster--pointers-not-rules).
* A pattern from a removed mod strands its cast forever. `Harvest` returns silently when the spec cannot be
  resolved, so the metal stays and the cell keeps offering `Harvest`
  ([casting cell § Gotcha 5](../machines/casting-cell.md)). Because the cell persists the full item code
  (`pattern-{type}-{wood}`, `BlockEntitySandCastingCell.cs:237-248`) rather than the type, the lookup fails
  the moment the wood variant disappears too - a narrower failure surface than it looks.
* The wood axis multiplies the item count but not the spec count, which is correct - but it also means
  96 creative-inventory entries for eight actual products. No `creativeinventory` filtering is applied.
* Most borrowed item shapes are untracked, so a clean clone silently renders those types as a generic
  plate. See [the art rule](#the-patterns-own-art-rule).
* The impression is destroyed and the sand is not. That is the cell's rule, and it is what makes durability
  the pattern's real cost: 24 impressions is 24 castings, not 24 pours
  ([casting cell](../machines/casting-cell.md) § Shake-out and Gotcha 11).
* The shape table and `Molds` are two dictionaries keyed by the same string with no shared source. A type
  present in `Molds` and absent from the shape table falls back to `game:item/plate` silently; the reverse -
  art with no spec - emits a `shapeByType` entry for a variant that does not exist. Nothing checks either
  direction.
* Do not carry an underived capacity into a `Molds` entry. Derive it from the drawn impression at
  1 vx³ = 2.5 u ([density rule](../mechanics/density-rule.md)); several older figures in circulation were
  never voxel-derived.
* A metal-pattern tier is designed and unbuilt. Wooden patterns wear at 24; long runs want a metal one. It
  would be a second `type` axis or a second itemtype - the current variant layout has no room for a material
  axis without moving the mold wildcard again.

---

## Open

| # | Work | Notes |
|---|---|---|
| 1 | Done - `MoldSize` enforced at both stations | see § `MoldSize` - authored and enforced |
| 2 | Done - the three long-cell stock patterns ship | `castslab`, `castblooms`, `castbillets`, pinned by `CastPartCatalogueTests` |
| 3 | **iiex ships its five** (`cylinder`, `axle`, `gearblanksmall`, `gearblanklarge`, `castframe`) | iiex has no casting bootstrap yet; the shared `Itemtype()` factory is ready for it, so each is one mold row, one item and its art |
| 4 | **Propose a roll-blank pattern** | `item-rollers-castblank.json` is drawn and would make [roll sets](roll-sets.md) craftable through the route the art already implies |
| 5 | **Track the untracked item shapes and filling shapes** | a clean clone is visibly wrong today |
| 6 | **Reconcile the two `minPourTemp` defaults** | either make the parser default to a sane temperature or make the key required |
| 7 | **Derive capacity from the impression instead of hand-writing it** | ([casting cell § Open](../machines/casting-cell.md), [density rule](../mechanics/density-rule.md)) |
| 8 | **A metal-pattern tier** | needs a variant-layout decision before it needs a number |
| 9 | **Does `MoldSpec` move to exlib?** | it stayed in iiex by precedent (`MoldSpec.cs:6`) and nobody has re-examined it since the tooling family grew to three - the same open question as `RollSetSpec` and `ItemDie` ([roll sets § Open 6](roll-sets.md), [dies § Open](dies.md)) |
