# Workbench

**Status** built 2026-08-20 apart from the interaction sequence. Designed with the owner 2026-08-15 in one
pass: the grid size, why it is not the player's grid, the interaction sequence, the repeat rule, the failure
rule and the discoverability route; the art and the footprint arrived 2026-08-20 and the block, its window
and its crafting were built the same day. ⛔ Every part of it is a **JSON contract** — a third party ships a
bench, a recipe and a sequence without writing C#, which is what E1–E4 asks of every station
**Mod** exlib owns the sequence spec, its registry and the block base; iiex ships the block, its art and
the shipped sequences

**Owns**

* the workbench block: what it is for, its 5 × 5 grid, its output inventory and why both are that size;
* the **interaction sequence** — a craft as an ordered list of tool-held gestures rather than a click — its
  JSON format, its repeat rule and its failure rule;
* the rule that decides which recipes get a sequence at all, and the tedium argument behind it;
* the discoverability contract: what the diagram says, what block interaction help shows;
* the amendment this makes to diagram crafting's Model A, and the part of Model A it does **not** touch.

**Does not own** — cited only, never restated

| Fact | Owner |
|---|---|
| Model A, the diagram item, the catalogue, the recipe idiom | [diagram-crafting](../mechanics/diagram-crafting.md) |
| The design table, its window and drafting cost | [design-table](design-table.md) |
| `MachineTool`, the tier ladder, and why a tool is not a die | [machining-line](../mechanics/machining-line.md) |
| `ExRecipeDef`, goldens, the cost catalogue | [recipes-config](../mechanics/recipes-config.md) |
| Schema versioning and the refuse-a-newer-schema rule | [process-extension](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/process-extension.md) |

**Depends on** [diagram-crafting](../mechanics/diagram-crafting.md) ·
[machining-line](../mechanics/machining-line.md) · [recipes-config](../mechanics/recipes-config.md) ·
[process-extension](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/process-extension.md)

---

## Role

The station for things a player cannot credibly assemble in their hands: boilers, engines, machine frames,
and the fittings — pressure valves, joints, couplings — that read as shop work rather than handicraft.

⛔⛔ **The concrete reason is stack size, not theme.** A `GridRecipe` ingredient's `quantity` is bounded by
what the *slot* holds, so a recipe wanting **48 rods against a 16 stack cap is unsatisfiable in one cell**,
however the recipe is written. Three cells of rods is not a picture of the product — it is just rods, and
[diagram-crafting](../mechanics/diagram-crafting.md)'s "one cell per distinct ingredient" idiom is untouched
by it. That constraint alone is what a bigger grid buys, and it is why the bench exists.

★ The theme is the second reason and it is the one the player feels: assembly with tools, at a bench, in a
sequence — the drawing-office idiom the suite is built around, one step past drafting.

---

## The grid

**5 × 5, and ordinary grid recipes.**

★★ **No new recipe engine, and none is needed.** Vanilla's `GridRecipe.Width` / `.Height` are plain settable
properties defaulting to 3, and `ConsumeInput(player, slots, gridWidth)` takes an arbitrary grid width and
scans every sub-position (`column <= gridWidth - Width`). So a 5 × 5 bench reuses vanilla's matcher whole:
a workbench recipe is an ordinary `ExRecipeDef` grid — or an ordinary JSON grid recipe from another mod —
that happens to be up to 5 wide.

★ Two consequences fall out of that scan, both wanted:

* **Every existing ≤ 3 × 3 recipe matches inside the bench automatically.** The workbench crafts anything
  the player's own grid does, with no per-recipe work. It is a superset on purpose.
* **A third party needs no code.** They ship a grid recipe with `width`/`height` up to 5 and it works.

⛔ **The output is an inventory, not a slot.** The stack cap that motivates the input applies on the way out:
a craft yielding 48 of a 16-cap item needs three output stacks, and a single slot would stall the repeat
rule below after one. Items are taken from the window, never by an empty-handed click in the world — which
is what frees the empty hand to be a sequence step.

---

## The interaction sequence

A recipe may declare that its craft is **work**: an ordered list of gestures, each a tool held for a time
while a hand animation plays. The shipped example is a fitting — hammer, bare hands, wrench, chisel, wrench.

```jsonc
// assets/<domain>/config/craftsequences/workbench.json
{
  "schema": 1,
  "machine": "workbench",
  "sequences": [
    {
      "recipe": "iiex:recipes/grid/pressurevalve",   // the recipe this is the labour for
      "steps": [
        { "tool": "game:hammer-*", "seconds": 1 },
        { "tool": null,           "seconds": 1, "animation": "fitting" },  // bare hands
        { "tool": "game:wrench-*", "seconds": 1 },
        { "tool": "game:chisel-*", "seconds": 1 },
        { "tool": "game:wrench-*", "seconds": 1 }
      ]
    }
  ]
}
```

| Field | Meaning |
|---|---|
| `machine` | The bench this sequence belongs to, so a third party's own station has its own table. Merged by machine exactly as [`ProcessJobRegistry`](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/process-extension.md) does |
| `recipe` | The grid recipe this is the labour for, by its asset name. Keyed on the recipe rather than the output, because two recipes may make one thing |
| `tool` | Item code the held stack must match, wildcards allowed; `null` means bare hands. A tool carrying a `machinetool` tier is gated on it, reusing `MachineTool` rather than inventing a second hardness idea |
| `seconds` | How long RMB is held for the step |
| `animation` | Hand animation to play; the default is the tool's own |

★★ **Declared per recipe, contributed to, never owned.** A sequence lives in a catalogue keyed by machine,
so another mod can put labour on **our** recipe without patching it, and we can put labour on theirs. Same
shape as every other catalogue in the suite: schema-versioned, merged, first declaration wins on a clash.

### No sequence means an ordinary craft

⛔⛔ **This is the rule that keeps the bench from becoming tedium, and it must stay a rule.** A recipe with no
declared sequence crafts on one interaction, like any grid. Sequences are for assemblies where the labour is
the point; they are opt-in, and the temptation to put one on everything is the failure mode to guard.

### Repeat: keep holding

★ **A single-step craft repeats while the button is held**, consuming materials and filling the output until
one runs out or the output is full. Bulk crafting is therefore one gesture rather than N clicks — which
makes the bench *better at repetition* than the player's grid, not merely bigger, and is a real reason to
walk to it.

⛔ **Repeat applies to single-step recipes only.** Re-running a five-step sequence on every repeat would be
consistent and miserable; a five-step craft is one deliberate act. The distinction falls out of the data
rather than a flag: one step repeats, several do not.

### A wrong action does nothing

Settled: a gesture out of order neither resets the sequence nor spoils the materials. It simply does not
advance. The sequence is something the player learns, not something they are punished by, and nothing about
a half-finished bench is destructive.

---

## Discoverability

A player cannot guess "hammer, hands, wrench, chisel, wrench", so the sequence is stated twice:

* **On the diagram**, in its description — the diagram is already the plan, and the design table already
  serves as the guide ([design-table](design-table.md));
* **In block interaction help**, which shows the tool for the *current* step visually.
  `WorldInteraction.Itemstacks` renders held-item requirements and the suite already uses it in five places
  (`BlockNetworkNode` shows wrenches, `MoltenChisel` shows chisels), so this is precedent rather than new
  machinery.

---

## What this amends

⛔⛔ **[diagram-crafting](../mechanics/diagram-crafting.md)'s Model A says "No new recipe engine", and this
does not add one** — vanilla's matcher is grid-size agnostic and the bench uses it unchanged. What is new is
the **sequence**, which Model A did not contemplate: assembly there is "ordinary crafting", and here it can
be work. Recorded as a dated amendment rather than left implicit, because a later reader finding a station
Model A appears to forbid is exactly the failure this suite hit twice in one week — the heading-machine page
silently overwriting STATE's Fasteners row, and cast iron's shared-scrap rationale outliving its reasoning.

★ Model A's substance is untouched: the diagram is still an ingredient, identity is still data, and there is
still no second recipe format.

---

## The block, as built

**Two cells, `O #`, and the art overhangs them on purpose** *(owner, 2026-08-20)*. The bench body — trestle
rails, top, tool shelf — measures exactly X 0..32 / Y 0..16 / Z 0..16, so the footprint is the principal plus
one plain filler at `x=+1`. The **vices are allowed outside it**: the east one reaches 6 voxels past the east
face and both screws pass the south face. Ruled acceptable, because a vice hangs off the end of a bench and
the cells it hangs into are air. ⛔ The consequence to know rather than rediscover: two benches placed side by
side interpenetrate by those 6 voxels.

⛔⛔ **The drawn frame is the `s` variant, and the two rotations must agree.** The vices mount on the +Z edge,
which is the edge the player works from, so both the shape's `rotateYByType` and the block's `StructureAngle`
carry +180 over `ExOrientation.AngleFromSide` — the casting bed's convention. They are declared in two
different places, and a bench whose model faced one way while its reserved cell lay the other would put a
solid invisible cell in the open; `WorkbenchTests.The_mesh_and_the_footprint_turn_together` holds them
together per side.

**The output is one row of five**, under the grid and the width of it. Five stacks is three of a 16-cap item's
48 with room to spare, and matching the grid width keeps the window a single column of controls.

**A craft is the Craft button, and the window shows what the grid makes.** The grid is loaded in the window,
a passive preview slot runs the same matcher the bench crafts with, and the button crafts once per press and
repeats while held. ⛔ This is where a **sequence** will differ: a window cannot hold a tool, so a declared
sequence is worked on the block itself with the window closed, which is also why block interaction help is
the design's second discoverability route. Nothing gates the repeat today because no sequence exists to be
multi-step.

**Either cell opens the window.** The bench declares no `IFillerInteractionTarget`, so the filler's plain
forwarding routes a click anywhere on the block to the principal. The owner's layout writes `#` rather than
`I` for the second cell; that distinction separates *which* interaction on a machine that has several, and
the bench has one.

## Open

* ⛔⛔ **Grid size is settled at 5 × 5 and measured as unjustified.** Across every recipe in the tree the
  largest ingredient `Quantity(n)` is **8**, once, and every machine recipe is `.Size(3, 3)` or smaller. It
  ships at 5 × 5 because the cost is symmetric — vanilla's matcher is size-agnostic and the window is a loop
  over the slot count, so widening or narrowing later is free — but no shipped recipe needs the width. The
  boiler and the engine are the intended first customers and neither is written; size it against one of them
  when it is.
* **The interaction sequence is not built.** `config/craftsequences/` does not exist, so every recipe is a
  one-interaction craft — which the design already makes the default, so the bench is complete without it.
  What a sequence adds is the world-side gesture path and the interaction help that names the current step.
* **Which recipes get a sequence** — the tedium rule says "assemblies where the labour is the point", which
  is a judgement, not a criterion. Worth a short list once the first few exist.
* **Whether the bench needs a tier**, i.e. whether one bench serves the whole game or a later one is required
  for steel-tier assemblies. Nothing yet demands the second.
* **Handbook rendering.** Vanilla draws grid recipes in a 3 × 3 widget; a 5 × 5 recipe has no "how do I make
  this" page without work, and a sequence has none at all. This is the largest unbudgeted cost in the design.
