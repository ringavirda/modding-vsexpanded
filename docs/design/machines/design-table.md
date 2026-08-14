# Design Table

**Status** built and craftable   **Mod** iiex

**Owns**

* the block, block entity, drafting window and craft recipe;
* the drafting cost and the slot layout;
* the window's picker and info panel - how the catalogue is enumerated and described at runtime;
* what the block still lacks (second-cell collision, candle particles, guide viewer).

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| Model A, the diagram item, the catalogue, migration state | [diagram-crafting](../mechanics/diagram-crafting.md) |
| What any individual diagram builds | the recipe that consumes it (see the catalogue) |
| The other station and the shared-window plan | [boring-machine](boring-machine.md) |
| Pipe tiers and pressure numbers | [pipe-network](../mechanics/pipe-network.md) |

---

## Role

The drafting station: it turns a drawing medium plus parchment into any diagram in the loaded catalogue.
It is the sole source of diagrams in survival, so its craft recipe is what puts the
diagram → pattern → casting chain in survival reach.

It is also the one window iiex owns - a plain case of R7: the interaction is "pick one plan from a
catalogue and read what it builds", which block info cannot express. The window is read-mostly: slots, a
picker, text, one button. All other simulation state in the mod stays on block info.

There are no progression gates on drafting. Every diagram is available from the first table; the only cost
is materials.

---

## Construction

Grid recipe (`Recipes/Grid/CraftingStationRecipeDefinitions.cs`): pattern `CPC,PPP` - 2 candles +
4 planks → `iiex:crafting-designtable-n`. No metal and no tool; the recurring cost sits on each draft.

Block definition (`BlockStructures/Crafting/Blocks/BlockDesignTable.cs`): a single-cell block,
`ExOrientable` with side variants, wood material, and always-lit candles as a static `lightHsv` `[5,7,12]`.
The drawn shape (`iiex:crafting/designtable`) spans roughly two cells; the model overhangs the neighbouring
cell rather than claiming it - see § Open.

---

## How it is used

Right-click opens the drafting window (`GuiDialogDesignTable`):

* **Two typed input slots** - the drawing medium (charcoal or black coal; both draw in vanilla) and
  parchment (vanilla `paper`). The slots refuse anything else.
* **A take-only output slot** - a draft's result cannot be overwritten by hand.
* **The picker** - a dropdown over every loaded `diagram-*` item, sorted by code. It enumerates
  `capi.World.Items` directly, so a diagram contributed by another mod appears with no registration step.
* **The info panel** - the selected diagram's name plus `{domain}:diagramdesc-{type}`, falling back to a
  generic line when a variant has no description.
* **Draw** - sends the draft packet; the server consumes 1 medium + 1 parchment and puts the diagram in the
  output slot (stacking up to the item's max, refusing when the slot holds a different diagram).

The window is a custom container GUI over a plain `BlockEntityContainer`, so slot moves and the draft reach
the server only through the open/close/draft packet handshake in
`BlockEntityDesignTable.OnReceivedClientPacket`; without it the client and server inventories silently
diverge. Draft requests are claim-checked server-side. The last-drafted diagram is persisted as
`dt_selected`, and the window re-opens on it.

Tests: `test/IronIndustryExpanded.Tests/Blocks/Crafting/DesignTableBeTests.cs`,
`DesignTableDraftTests.cs`, and the block-def golden `goldens/iiex/blocktypes/crafting/designtable.json`.

---

## Numbers

| Quantity | Value | Where |
|---|---|---|
| Craft recipe | 2 candles + 4 planks | `CraftingStationRecipeDefinitions` |
| Draft cost | 1 drawing medium + 1 parchment per diagram | `BlockEntityDesignTable.MediumCost` / `ParchmentCost` |
| Inventory | 3 slots: medium, parchment, output | `InventoryDesignTable` |
| Light | `lightHsv [5,7,12]`, static | `BlockDesignTable.Definitions` |

---

## Lang

All three locales (en/ru/uk) carry the same key set: the block name, the window strings
(`designtable-title` / `-inputs` / `-result` / `-draw` / `-desc-generic`), the interaction help
(`blockhelp-designtable-open`), 26 named `item-diagram-*` entries plus the wildcard fallback, and
23 `diagramdesc-*` descriptions.

Against the 27 shipped diagram variants that leaves four gaps, identical in every locale:
`item-castshell` has neither a name nor a description (it shows the wildcard "Diagram" and the generic
line); `item-castbillets` / `item-castblooms` / `item-castslab` have names but no description.

---

## Open

1. **The 2-cell collision.** The drawn table spans about two cells but the block is a single cell; the
   model overhangs its neighbour with no filler claiming that space.
2. **Candle particles.** The wicks light statically; there are no flame particles or dynamic flicker.
3. **The guide viewer.** Proposed: a third window region showing the `docs/setups` overview images
   in-game (a pan/zoom `LoadedTexture` pane, images downscaled to ~2048-2560 px and shipped under
   `assets/<domain>/textures/guides/`), or handbook entries linked from the table. Neither is started.
4. **The shared station-window base.** The dialog is iiex-local; extracting the reusable parts to exlib is
   deferred until the [boring machine](boring-machine.md), the second consumer, is built. See
   [diagram-crafting](../mechanics/diagram-crafting.md) § Stations.
5. **The four missing lang entries** (§ Lang).
