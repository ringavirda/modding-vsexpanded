# Molten Canal (the canal family)
**Status** live   **Mod** iiex

**Owns**
- The canal family's blocktype inventory: which blocktypes exist, their variant groups, orientations,
  shapes, textures, materials, stack sizes and boxes.
- Every canal-family block attribute: `fillStart` / `fillHeight` / `fillQuadsByLevel` per type, and the
  mold pedestal's separate `moldFill*` set.
- The player verbs: sealing and unsealing a straight canal, chiselling a clogged cell, parking a barrel
  or a large mold under the tap, placing a small mold on the pedestal, and toggling either fitting's pour.
- Recipes and drops for the whole family.
- The end-cap tessellation rule and the open-connector-face logic.
- The clay heat gate as the pedestal applies it, and the large-vs-small mold classification.
- The pedestal's uncapped drain as a code fact.

**Does not own - cited only**
- [molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md) - the model. `IMoltenCell`, the per-tick driver,
  `FlowEdge`, the distance BFS, per-cell capacities (canal / start / tap / pedestal / barrel), every
  `MoltenFlowRate` / `MoltenMinFlowAmount` / `CanalDefaultDrainSpeed` / cooldown / seal-cost tunable, the
  `Sealed` and `Solidified` latches, the solidify and hardened thresholds, `SoakHeat`, back-pressure, and
  the throughput-unification decision. Every number in that page is that page's.
- [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) - the shared block-network graph the canal is a node in.
- [casting bed](casting-bed.md) and [casting cell](casting-cell.md) - the two stations a run ends at.
- [cold blast furnace](blast-furnace-cold.md) and [cupola](cupola.md) - the taps a run starts at.
- [recipes & config](../mechanics/recipes-config.md) - code-first block defs, the `Fhk` ingredient helpers,
  goldens.
- [density rule](../mechanics/density-rule.md) - what a unit of metal is worth.

---

## Role

Liquid metal travels from a tap-hole to a mold without becoming an inventory item. The canal family is the
plumbing: brick or cobblestone troughs that a furnace pours into at one end and that drain into a barrel, a
tool mold or a casting station at the other. Nothing is ever picked up in a liquid state, and failure is
local: a trough that goes cold plugs at that block, glows, and is chipped out with a chisel rather than
voiding the whole run silently.

The family is four transport shapes plus three endpoints:

| Role | Blocktype | What it is for |
|---|---|---|
| transport | `moltencanal-{straight,bend,tjunction,xjunction}` | carry metal; the straight one is also the sealable valve |
| entry | `moltencanal-start` | the furnace-facing sink and the flow root |
| exit - bulk | `moltencanal-tap` | pours down into a parked molten barrel or a large tool mold |
| exit - piece | `moltencanal-moldpedestal` | fills one small tool mold standing on it |

---

## Structure

Single blocks, no footprint. Every one is a `BlockNetworkNode` with `NetworkType == "molten"`
(`BlockMoltenCanal.cs:26`), which is what puts them in the shared graph.

Class chain:

```
BlockMoltenCanal                 →  BlockEntityMoltenCanal
  BlockMoltenCanalStart          →    BlockEntityMoltenCanalStart      (: canal BE)
  BlockMoltenCanalTap            →    BlockEntityMoltenCanalTap        (: canal BE)
    BlockMoltenCanalMoldPedestal →    BlockEntityMoltenCanalMoldPedestal (: canal BE - a sibling of the tap BE)
```

The pedestal block extends the tap block, but the pedestal block entity does not extend the tap block
entity. That asymmetry has already caused one bug (see Gotchas).

### Skins and variants

Two skins share one definition surface (`BlockMoltenCanal.cs:69-104`), built by `CanalFamilyDef`
(`:192-230`):

| Skin | Folder | Material | Variant group | Texture |
|---|---|---|---|---|
| brick | `canalbrick` | Ceramic (+ ceramic place sound) | `brick`: fire, black, brown, cream, gray, orange, red, tan | running-bond base + `{brick}1` tint overlay |
| cobble | `canalcobblestone` | Stone (no place sound) | `rock`: `block/rockwithdeposit`, minus halite / scoria / tuff / travertine | `game:block/stone/cobblestone/{rock}1` |

The tap has neither skin: it is a single blocktype with its own burned-clay + metal-sheet textures,
authored directly rather than through `CanalFamilyDef` (`BlockMoltenCanalTap.cs:27-65`).

| Blocktype | Skins | Orientations | Max stack | file:line |
|---|---|---|---|---|
| `straight` | 2 | `ns`, `we` | 8 | `BlockMoltenCanal.cs:108-116` |
| `bend` | 2 | `nw`, `se`, `en`, `ws` | 4 | `:117-132` |
| `tjunction` | 2 | `nes`, `esw`, `swn`, `wne` | 4 | `:133-149` |
| `xjunction` | 2 | `nswe` | 4 | `:150-162` |
| `start` | 2 | `n`, `w`, `s`, `e` | 1 | `BlockMoltenCanalStart.cs:25-51` |
| `moldpedestal` | 2 | `n`, `w`, `s`, `e` | 1 | `BlockMoltenCanalMoldPedestal.cs:36-61` |
| `tap` | - | `n`, `w`, `s`, `e` | 1 | `BlockMoltenCanalTap.cs:31-65` |

Every def carries `Behavior("Lockable")` and `NonSolid()` (`BlockMoltenCanal.cs:222, 229`). The start
defaults to facing south (toward the pour) rather than its first-listed north
(`BlockMoltenCanalStart.cs:55`); every other type takes the first-listed orientation as its fallback.

---

## Assets

| Asset | Path | Used by |
|---|---|---|
| Shapes | `mods/iiex/assets/iiex/shapes/molten/canal/{straight,bend,tjunction,xjunction,start,tap,moldpedestal}.json` | all tracked |
| End cap | `mods/iiex/assets/iiex/shapes/molten/canal/end.json` | `MoltenMeshes.cs:15-17`, authored facing south |
| Barrel shapes | `mods/iiex/assets/iiex/shapes/molten/barrel-{plated,cast}.json` | the barrel block, not the tap (Gotcha 1) |
| Animation | none | - |

The base mesh is loaded by path, from the block's `type` variant, not from `Block.Shape`:
`iiex:shapes/molten/canal/{Block.Variant["type"]}.json` (`BlockEntityMoltenCanal.cs:419-422`), then rotated
by the block's own `rotateX/Y/Z`. The shape filename is coupled to the variant word, and a new canal type
needs a file named after it.

Open connector faces are capped with the shared `end.json` piece, rotated to the face
(`MoltenMeshes.TesselateEndCap`, `:33-49`; `EndCapRotYDeg` at `:20-27`). Faces are recomputed on every
tessellation, not read from a cache (`BlockEntityMoltenCanal.cs:413-417`): on chunk load the cache is
populated before cross-boundary neighbours exist, which would cap a connected face; refreshing here
self-corrects when the neighbour chunk arrives.

A sealed canal caps every connector face regardless of neighbours - the visible seal (`:520-525`).

---

## Construction

All in `MoltenRecipeDefinitions.cs:49-237`. Every shape is craftable three ways - cobblestone (captures
`{rock}`), coloured running brick (captures `{brick}`), or fire brick (the uncaptured `fire` default) - and
every recipe is the shape's material worked over fire clay with a hammer and a chisel, the shared `Fhk` trio
(`RecipeIngredients.cs:73-80`).

| Output | Pattern | Material cells | file:line |
|---|---|---|---|
| straight | `H_K,FCF` | 1 | `:53-62` (cobble), `:118-137` (brick pair) |
| bend | `HFK,FC_` | 1 | `:63-72`, `:138-157` |
| tjunction | `HFK,FCF` | 1 | `:73-82`, `:158-177` |
| xjunction | `HFK,FCF,_F_` | 1 | `:83-92`, `:178-197` |
| start | `HCK,FFF` / `HBK,FFF` | 1 | `:93-102`, `:198-217` |
| moldpedestal | `HFK,CFC` / `HFK,BFB` | 2 | `:103-112`, `:218-236` |
| tap | `HFK` (3 × 1) | none - clay, hammer, chisel only | `:113-116` |

`F` is fire clay at quantity 2 everywhere except the tap, which uses 4 (`RecipeIngredients.cs:73-80`, the
`clayQty` parameter). The tap has exactly one recipe: no skin, no coloured/fire pair.

A diagram route exists for `straight` and `bend` only: a reusable `iiex:diagram-molten-{type}` tool plus one
cobblestone (`DiagramRecipeDefinitions.cs:22-36`). It is creative-only until the design table can draft
diagrams, and it coexists with the legacy recipes.

---

## Operation

### Transport canals

| Verb | Precondition | Effect | file:line |
|---|---|---|---|
| RMB with chisel (hand) + hammer (off-hand) | cell `Solidified` | chip the plug out, recover metal, rejoin the run | `BlockMoltenCanal.cs:410-423` |
| RMB with fire clay | straight only, this cell and every connector-face canal neighbour empty | seal it into a flow-severing separator | `:429-454`, gate at `:486-507` |
| RMB with a chisel | straight, `Sealed` | break the seal, refund clay, damage the chisel | `:456-479` |
| Wrench | cell empty and not solidified | rotate; refused while it holds metal or has clogged | `:241-250` |

Sealing only ever severs an already-drained section, so a seal can never trap metal against itself. The
refusal error is `iiex-canalnotempty` (`:439`).

The interaction help is state-driven: the chisel hint appears only on a solidified and hardened cell, the
unseal hint only on a sealed one, and the seal hint only when `CanSeal` passes (`:531-582`).

### Canal start

Plain RMB returns `false` by design so the held item's interaction runs instead of being swallowed, which is
how a vanilla smelted crucible pours into the network (`BlockMoltenCanalStart.cs:78-91`). A solidified start
falls through to the base chisel-clear. The interaction help lists every `crucible-*-smelted` block, cached
at load (`:57-72`).

The block entity is the family's only `ILiquidMetalSink`. Its `CanReceiveOrSoak` is looser than
`CanReceive` so a brim-full start keeps taking heat instead of plugging - see
[molten network § back-pressure](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md).

A running pour tally is shown while metal is arriving and self-clears after an idle timeout
(`BlockEntityMoltenCanalStart.cs:33-58, 149-156`).

### Canal tap

| Modifier | Effect | file:line |
|---|---|---|
| sneak (Shift) + RMB, nothing parked | park a `BlockMoltenBarrel` or a large tool mold; a small mold is refused with `iiex-moldtoosmall` | `BlockMoltenCanalTap.cs:143-168` |
| sneak + RMB, something parked | retrieve it with its contents | `:112-141` |
| Ctrl + RMB | toggle pouring | `:175` |
| plain RMB | chisel out a clogged cell, else nothing | `:99-103` |

The modifier split is read off the player's controls at `:97-98`. Parking requires a real solid surface
directly below; invisible megablock fillers are excluded, being solid for collision but an internal part of
another structure rather than a stand (`:186-196`). A mold full of still-liquid metal may only be taken into
an empty hand; anywhere else it instantly spills (`MoltenMoldSpill`, `:115-140`).

Each tick the tap drains its own cell into the parked content at `DrainSpeed`
(`BlockEntityMoltenCanalTap.cs:445-484`). Barrels accept metal even when hardened (it re-melts); molds are
gated, because a hardened mold is a finished cast (`:396-430`).

A closed tap severs itself from the run (`IsConnectionBroken`, `:49-50`) and caps its inlet with the end-cap
mesh (`:656-671`).

### Mold pedestal

Same modifier scheme as the tap, for small molds only; a large one is refused with `iiex-moldtoolarge`
(`BlockMoltenCanalMoldPedestal.cs:148-158`). A plain RMB on a solidified pedestal routes straight to
`MoltenChisel.TryChisel` rather than deferring to the base, because the tap block's handler would reject the
pedestal's block entity (`:119-136`); Ctrl + RMB toggles pouring (`:141-146`).

Per tick, in order (`BlockEntityMoltenCanalMoldPedestal.cs:165-256`):

1. Purge a mold whose type a server admin disabled via `ExMoldGate` (`:170-175`).
2. Re-stamp the cast metal's cooldown rate from live config (`:179-184`).
3. **Clay heat gate** - if the run's metal is hotter than the ceiling, the fired-clay mold shatters
   (`:191-199`).
4. Drain into the mold (`:201-256`).

The clay heat gate is the ceramic tier's ceiling. The pedestal fills a mold by draining the run directly,
bypassing vanilla's `CanReceive`, so without the gate a clay mold on an iron run would silently trap a cast
forever. `ClayHeatGate.WouldShatter(mold, pourTemp)` is true only for a small fired-clay tool mold above
`ClayMoldHeatCeiling` (`ClayHeatGate.cs:29-31`). Shattering destroys the mold with no drop, plays a crack,
and sends `iiex-clayshatter` to every player within 8 m (`:269-289`). Large anvil / helve-hammer molds are
cast at iron temperatures in the tap and are exempt; iiex's own cast-iron molds are a different block class
and never satisfy the predicate.

---

## Numbers

Every capacity, flow rate, drain speed, cooldown coefficient, clay cost and temperature threshold in this
family belongs to [molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md). What follows is only what the blocks
declare.

### Fill geometry (block attributes)

| Blocktype | `fillStart` | `fillHeight` | `fillQuadsByLevel` | file:line |
|---|---|---|---|---|
| every canal-family def | `14` | `1` | per type below | `BlockMoltenCanal.cs:218-220` |
| straight | | | `x 7-9, z 0-16` | `:113` |
| bend | | | `x 7-9, z 0-9` + `x 0-7, z 7-9` | `:122-126` |
| tjunction | | | `x 0-16, z 7-9` + `x 7-9, z 0-7` | `:139-142` |
| xjunction | | | the T pair + `x 7-9, z 9-16` | `:156-160` |
| start | | | `x 5-11, z 5-11` + `x 7-9, z 11-16` | `BlockMoltenCanalStart.cs:36-40` |
| tap | | | `x 7-9, z 0-5` | `BlockMoltenCanalTap.cs:48` |
| moldpedestal (own canal) | | | `x 7-9, z 0-5` | `BlockMoltenCanalMoldPedestal.cs:48` |

Defaults when an attribute is absent: `fillStart` 14, `fillHeight` 1 (`BlockMoltenCanal.cs:37-38`).

### The pedestal's second fill set - the mold it holds

| key | value | file:line |
|---|---|---|
| `moldFillStart` | `12` | `BlockMoltenCanalMoldPedestal.cs:56` |
| `moldFillHeight` | `1` | `:57` |
| `moldFillQuadsByLevel` | `x 2-14, z 2-14` | `:58` |
| accessor defaults | `12` / `1` | `:29-31` |

### Hard-coded block-side constants

| constant | value | file:line | note |
|---|---|---|---|
| tap collision / selection box | `0.0625, 0.0625, 0 → 0.9375, 0.9375, 0.9375` | `BlockMoltenCanalTap.cs:61-62` | |
| tap `MoldMeshRaiseY` | `0f` | `BlockEntityMoltenCanalTap.cs:93` | molds sit on the tap's floor |
| pedestal mold mesh lift | `11f / 16f` | `BlockEntityMoltenCanalMoldPedestal.cs:384` | the pedestal body tops out at y 11/16 |
| renderer fallback box (canal) | `Cuboidf(7, 0, 0, 9, 16, 16)` | `BlockEntityMoltenCanal.cs:550` | used when `fillQuadsByLevel` is absent |
| renderer fallback box (pedestal mold) | `Cuboidf(7, 0, 0, 9, 16, 5)` | `BlockEntityMoltenCanalMoldPedestal.cs:304` | |
| parked-content fallback box (tap) | `Cuboidf(4, 0, 4, 12, 16, 12)`, `fillStart` 1/16, `fillHeight` 8 | `BlockEntityMoltenCanalTap.cs:270-275` | read off the content block, never the tap |
| glow HSV | `[8, 7, level]` | `BlockMoltenCanal.cs:269` | level from `GlowLightLevel` |
| end-cap rotations | N 180 · E 90 · W 270 · S 0 | `MoltenMeshes.cs:20-27` | the shape is authored facing south |
| `MoldKinds.LargeToolTypes` | `helvehammer`, `anvil` | `MoldKinds.cs:13-18` | hard-coded; everything else is "small" |
| `ClayHeatGate.ShatterErrorCode` | `iiex-clayshatter` | `ClayHeatGate.cs:24` | |
| fire clay code | `game:clay-fire` | `BlockMoltenCanal.cs:382` | |

---

## Drops

| Case | Drops | file:line |
|---|---|---|
| any canal-family block | its own block (base `GetDrops`) | `BlockMoltenCanal.cs:338` |
| cell holds solidified metal | plus the recovery stack from `MoltenChisel.BuildRecovery` | `:342-346` |
| cell is sealed | plus `CanalUnsealClayRefund` fire clay | `:348-355` |
| cell holds liquid metal | nothing extra - a sizzle plays and the charge is voided | `:313-329` |
| tap with parked content | the barrel or mold, with its contents preserved in `blockEntityAttributes`, dropped before the tap is removed or it would be silently lost | `BlockMoltenCanalTap.cs:250-275` |
| pedestal with a mold | the mold plus any cast metal, same reason | `BlockMoltenCanalMoldPedestal.cs:67-88` |

`OnPickBlock` on a solidified canal returns a clean variant instead of the drop
(`BlockMoltenCanal.cs:360-379`); see Gotcha 3.

---

## Code

| Type / member | file:line | Role |
|---|---|---|
| `BlockMoltenCanal` | `BlockMoltenCanal.cs:24` | `BlockNetworkNode`; owns the family's definition surface |
| `…CanalFamilyDef` | `:192` | the shared authoring surface; start and pedestal reuse it verbatim |
| `…CanalSkins` / `CanalTypes` | `:69` / `:106` | the two skin records and the four transport types |
| `…OnBlockInteractStart` | `:395` | chisel / seal / unseal, in that order |
| `…CanSeal` | `:486` | this cell and its connector neighbours must be empty |
| `…UpdateEndConnectors` | `:294` | recomputes `OpenConnectorFaces` on place and on neighbour change |
| `…GetLightHsv` | `:256` | incandescent block light off the cell's temperature |
| `BlockMoltenCanalStart` | `BlockMoltenCanalStart.cs:16` | crucible-pour entry; `GetFallbackOrientation` → `"s"` (`:55`) |
| `BlockMoltenCanalTap` | `BlockMoltenCanalTap.cs:21` | park/toggle; `HasSolidSupportBelow` at `:186` |
| `BlockMoltenCanalMoldPedestal` | `BlockMoltenCanalMoldPedestal.cs:22` | extends the tap block; routes its own chisel |
| `MoltenMeshes.TesselateEndCap` | `MoltenMeshes.cs:33` | the one end-cap builder, shared by canal / tap / pedestal |
| `MoldKinds` | `MoldKinds.cs:11` | large (tap-only) vs small (pedestal) tool molds |
| `ClayHeatGate.WouldShatter` | `ClayHeatGate.cs:29` | the pure ceramic-ceiling decision, shared with `ToolMoldHeatGatePatch` |
| `MoltenMoldSpill` | `MoltenMoldSpill.cs` | liquid-mold pickup denial and the spill-on-give path |
| `BlockEntityMoltenCanal` and subclasses | see [molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md) | the cell model |
| Tests | `test/…/Blocks/Molten/{MoltenCanalBeTests,MoltenCanalStartTests,MoltenCanalTapTests,MoltenMoldPedestalTests,MoltenMoldSpillTests,FillQuadsTests,MoldKindsTests}.cs` | |

**Where a caller hooks in.** To receive from a run: be an `IMoltenCell` node on the graph, or be a puller
that drains an adjacent cell (the [casting bed](casting-bed.md) and [casting cell](casting-cell.md) both do
the latter). To feed a run: call `ILiquidMetalSink` on a canal start. To add a canal shape: one
`CanalTypeSpec` entry plus a shape file named after the type.

---

## Gotchas

1. **The tap still references `iiex:moltenbarrel`, a block code that does not exist.** The barrel is a
   construction-variant block (`moltenbarrel-{plated,cast}`, `BlockMoltenBarrel.cs:50-53`) and
   `BarrelConstructionMigration` remaps old worlds. Four sites were not updated:
   - `BlockMoltenCanalTap.cs:73` - the accepted-content interaction hint silently omits the barrel;
   - `BlockEntityMoltenCanalTap.cs:172` - `contentBlock` resolves null, so a parked barrel gets no fill
     renderer;
   - `BlockEntityMoltenCanalTap.cs:303` - `RemoveBarrel()` builds `new ItemStack(null)`, so retrieving a
     parked barrel is at best an invalid stack;
   - `BlockEntityMoltenCanalTap.cs:630` - loads `iiex:shapes/molten/barrel.json`, which does not exist
     (the files are `barrel-plated.json` / `barrel-cast.json`), so a parked barrel is invisible.

   Parking still works (`AddBarrel` tests `heldStack.Block is BlockMoltenBarrel`,
   `BlockMoltenCanalTap.cs:149-151`, and reads the capacity off the parked instance,
   `BlockEntityMoltenCanalTap.cs:291-293`), which is why the breakage is silent.

2. **The pedestal drain has no rate cap.** The tap drains `min(DrainSpeed, space)`
   (`BlockEntityMoltenCanalTap.cs:462`); the pedestal drains `min(CellAmount, space)`
   (`BlockEntityMoltenCanalMoldPedestal.cs:218-219`), its whole cell in one tick. It is the only fitting
   in the family with no throughput limit at all.
   [molten network § Open](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md) owns the decision about unifying it.

3. **`OnPickBlock` asks for variant keys the canal does not have.**
   `CodeWithVariants(["variant","state","orientation"], ["pass","normal","ns"])`
   (`BlockMoltenCanal.cs:369-372`); the canal's actual variant groups are `type` / `brick`|`rock` /
   `orientation` (`:223, 226`). `variant` and `state` are from a retired scheme, so the lookup cannot
   resolve and the code falls back to `this`. Dead branch, harmless but misleading.

4. **Three helpers are defined and never called.** `PassOrEndOrientations` (`:593`),
   `CountConnectedNeighborFaces` (`:597`) and `PickBestOrientation` (`:619`) have no call sites anywhere in
   `src/` or `test/`; leftovers from the pre-`BlockNetworkNode` auto-orientation scheme.

5. **The pedestal block entity is a sibling of the tap block entity, not a subclass.** The pedestal block
   extends `BlockMoltenCanalTap`, so a naive `base.OnBlockInteractStart` lands in the tap's handler, whose
   `is not BlockEntityMoltenCanalTap` guard rejects the pedestal's block entity and silently drops the
   click. That is why the pedestal routes its chisel directly (`BlockMoltenCanalMoldPedestal.cs:118-133`).
   Do not simplify that back to a `base` call.

6. **`UpdateEndConnectors` only recognises `BlockMoltenCanal` neighbours** (`:299-306`). A
   [casting bed](casting-bed.md) or [casting cell](casting-cell.md) is not a canal, so a run that ends at
   one always renders a capped face even though metal is flowing across it.

7. **The base mesh is loaded by variant word.** `iiex:shapes/molten/canal/{type}.json`
   (`BlockEntityMoltenCanal.cs:419-422`): the shape file name is the `type` variant. Renaming a type
   silently blanks the block.

8. **A closed tap or pedestal drops off the graph**, it does not merely stop delivering
   (`BlockEntityMoltenCanalTap.cs:49-50`, `BlockEntityMoltenCanalMoldPedestal.cs:52-53`). Its own cell then
   stops filling too. That is by design - see
   [molten network § Gotcha 9](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md).

9. **The tap's parked-content renderer reads the content block's fill attributes, never the tap's**
   (`BlockEntityMoltenCanalTap.cs:260-276`); using the tap's own `fillStart` would pin a parked barrel's
   metal at spout height. Molds are not `[BlockRegister]` partials and have no generated constants, so this
   reads live block attributes for every content type.

10. **Mold fill quads are rotated by the tap's facing, not the mold's** (`:243-247`): a mold's
    `fillQuadsByLevel` are authored in its already-shape-rotated orientation. The barrel uses its own
    (round, ≈ 0) shape. Getting this backwards puts the metal surface beside the mold.

11. **`ExMoldGate` has no registered predicate today.** `IsToolMoldDisabled` returns false when nothing is
    registered (`ExMoldGate.cs:23-24`), and the `/exmod molds` command was deleted. The pedestal's purge
    branch (`BlockEntityMoltenCanalMoldPedestal.cs:170-175`) is therefore dead, kept as the seam for a
    config gate.

12. **Sealing is straight-only, and the check is on `Type`** (`:426, 553`). A bend or junction cannot be
    sealed, so a valve must be planned onto a straight segment.

---

## Open

### ⛔⛔ A hand pour into a molten barrel transfers nothing *(found 2026-08-22, unverified in game)*

`BlockEntityMoltenBarrel.CanReceive` (`:104`) ends with `GetMoldedStacks(metal) is { Length: > 0 }`, and
`GetMoldedStacks` (`:279-296`) reads `Block.Attributes["drop"]` then `Block.Attributes["drops"]`. The
code-first def `BlockMoltenBarrel.Definitions` (`:59-62`) sets `maxUnits`, `fillHeight`, `fillStart` and
`fillQuadsByLevel` and **neither drop key**, and there is no shipped blocktype JSON for `molten-barrel`. So
`GetMoldedStacks` returns `Array.Empty` and **`CanReceive` is always false** - vanilla bails at
`BlockSmeltedContainer.cs:135` and `:168`, and a crucible poured over a barrel moves no metal.

⛔ The barrel still *advertises* the pour: `CanReceiveAny` (`:89`) is true, so the interaction help shows and
vanilla sets `handHandling = PreventDefault`. A player gets the gesture and no result.

⛔ **Nothing covers it.** `MoltenBarrelTests` asserts only `CanReceiveAny` (`:48`, `:77`) and calls
`ReceiveLiquidMetal` directly, which never consults `CanReceive`.

★ The fix is a decision, not a typo: a barrel is not a mold and casts no shape, so `GetMoldedStacks` is the
wrong question for it. `CanReceive` most likely wants a capacity test - the same one `ReceiveLiquidMetal`
already applies - rather than a molded-stack lookup copied from the mold path. Left for whoever owns the
barrel. The tap path is unaffected: the furnace tap fills through the network, not through vanilla's
crucible interaction.


- **Throughput.** The canal edge, the tap drain and the pedestal drain run at three different rates (one of
  them unbounded), against a settled single 50 u/s. Owned by
  [molten network § Open](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md); the pedestal line is Gotcha 2 here.
- **The dead barrel code (Gotcha 1)** is the family's only functional breakage and is a one-line fix per
  site, except `RemoveBarrel`, which has to pick which construction variant to hand back. The parked
  stack's own block is available at `AddBarrel` time (`BlockEntityMoltenCanalTap.cs:292-293`) but is not
  stored (`BlockEntityMoltenCanalTap.cs:283-296`).
- **No vertical runs.** Flow walks horizontals only while the graph walks all faces, so a stacked pair of
  canals is in one network and never exchanges metal. Whether a vertical drop should flow is undecided -
  [molten network § Gotcha 4](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md).
- **The ladle does not exist.** R3's mixing block, the only thing in the design allowed to merge two metals,
  has no type anywhere in `src/`. Until it does, a canal run can carry two metals side by side and nothing
  will combine them.
- **The tap has one recipe and no skin**, unlike every other endpoint. Whether it should gain the
  brick/cobble pair for consistency is unasked.
- **Migration debt.** `moltencanal-*` predates the smex → iiex split; the migration chain is in
  `BlockMigrations/`, and the pre-split `smex:` codes are still handled. Nothing here is broken, but the
  variant scheme has changed at least twice (Gotcha 3 is the fossil).
