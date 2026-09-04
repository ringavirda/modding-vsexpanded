# Molten Network

**Status** live   **Mod** `exlib` owns the driver (`ExpandedLib.Networks.MoltenNetwork`, `IMoltenCell`, `BEBehaviorMoltenCell`); `iiex` owns every cell block (canal, start, tap, mold pedestal, barrel) and the content tunables.

**Owns**
- The molten flow driver: per-cell metal ownership, the distance-from-source BFS, the wavefront sort, `FlowEdge` level-equalisation and the per-tick thermal pass.
- Every molten tunable and its shipped value: `MoltenFlowRate`, `MoltenMinFlowAmount`, `MoltenCooldownDefault`, `CanalDefaultUnitCapacity`, `CanalDefaultDrainSpeed`, `MoldDefaultUnits`, `BarrelDefaultMaxUnits`, `MoltenCooldownSpeed` + the three per-container cooldown coefficients, `CanalSealClayCost` / `CanalUnsealClayRefund`.
- The per-cell capacity of each fitting (canal 50 / start 100 / tap 25 / pedestal 25 / barrel 800 / hosted cell 100) and where each number comes from.
- The two flow-blocking latches (`Sealed`, `Solidified`), the solidify rule, and the chisel-out recovery gate.
- The back-pressure chain: destination full → canal backs up → furnace stalls and counts a disruption.
- Molten merge/split semantics (they are no-ops) and the fact that no `Ladle` type exists anywhere in `src/`.
- The two second copies of the flow driver (sand casting bed, sand casting cell) and how they differ from the network one.

**Depends on**
- [pipe network](pipe-network.md) - owns the shared block-network graph substrate (node add/remove, BFS fracture, per-second tick dispatch, connector reciprocity, `IsConnectionBroken` re-walk). Everything this page says about *graph* behaviour is that page's fact.
- [density rule](density-rule.md) - what one *unit* of metal is worth. This page never converts units to mass.
- [../materials.md](../materials.md) - metal identities, melting points, per-metal liquid/hardened thresholds, recovery drops.

---

## Role

Liquid metal has to get from a furnace tap-hole to a mold without becoming an inventory item. The molten network is the transport layer: a run of brick or cobblestone canal blocks, each holding its own charge of metal, that a furnace pours into at one end and that drains into a barrel, a tool mold or a casting bed at the other.

Two consequences:

1. No bucket step. The furnace does not emit an item; it hands units down its tap into the canal start, and the run carries them. Nothing is ever picked up in a liquid state.
2. Failure is visible and local. A canal that goes cold plugs at that block, glows, blocks flow, and is chipped out with a chisel, instead of the whole run silently voiding its contents.

The network is not a pool. It provides connectivity and a driver; the metal itself lives in the blocks. That is what makes merge and split free (`MoltenNetwork.cs:270-278`) and what lets two different metals stand side by side in one graph without mixing.

---

## How it works

### 1. Cells own their metal

The network stores no metal. Every node is an `IMoltenCell` (`IMoltenCell.cs:16`) exposing amount / type / temperature / capacity, the two latches, two capability flags, and four operations:

| member | file:line | meaning |
|---|---|---|
| `CellAmount` | `IMoltenCell.cs:19` | units held (liquid, or solidified once latched) |
| `CellMetalType` | `IMoltenCell.cs:22` | full item code, e.g. `game:ingot-iron`; `""` when empty |
| `CellTemperature` | `IMoltenCell.cs:25` | °C |
| `MaxUnitCapacity` | `IMoltenCell.cs:28` | units |
| `Sealed` | `IMoltenCell.cs:31` | clay-sealed manual valve |
| `Solidified` | `IMoltenCell.cs:34` | frozen plug |
| `IsFlowSource` | `IMoltenCell.cs:37` | roots the distance BFS (the canal start) |
| `AcceptsSubMinimumFlow` | `IMoltenCell.cs:40` | drain fitting (tap / pedestal) - takes the final dregs |
| `EnsureMetalStack` / `PushMetalRaw` / `DrainMetal` / `UpdateThermal` | `IMoltenCell.cs:43-52` | the per-tick operations |

Two implementations exist:

- `BlockEntityMoltenCanal` (`BlockEntityMoltenCanal.cs:26`) - a real graph node, registered in the shared network. Start / tap / pedestal subclass it.
- `BEBehaviorMoltenCell` (`BEBehaviorMoltenCell.cs:29`) - the same contract as a composable behaviour, so an invisible megablock footprint filler can be a molten cell. Hosted cells are never auto-registered in the molten graph (`BEBehaviorMoltenCell.cs:18-25`); the principal that owns the cluster drives its own flow. That is how a casting bed stays an isolated internal network while the canal outside carries a different metal.

Temperature is carried on a server-side single-item `ItemStack` so the VS time-based cooling model applies (`BlockEntityMoltenCanal.cs:57-60`). Only *type* and *temperature* persist; the carrier is rebuilt lazily on load by `EnsureMetalStack` (`BlockEntityMoltenCanal.cs:294-308`).

### 2. Ordering: distance-from-source wavefront

Each tick the driver:

1. Resolves every `IMoltenCell` under the network's own `Nodes` set (`MoltenNetwork.cs:169-174`).
2. Fetches the cached distance-from-source map (`MoltenNetwork.cs:178`).
3. Sorts cells descending by distance - farthest first, position breaking ties (`MoltenNetwork.cs:146-156, 179`).
4. Calls `EnsureMetalStack` on all of them (`MoltenNetwork.cs:180-181`).

The distance map is a multi-source BFS rooted at every cell with `IsFlowSource == true` (`MoltenNetwork.cs:104-139`). It walks horizontals only (`MoltenNetwork.cs:125`), requires the block to expose a connector on the face (`:126`), and requires the neighbour to be both in `Nodes` and an `IMoltenCell` (`:130-133`). Cells unreachable from any source are simply absent and sort as `int.MaxValue`, i.e. "farthest" (`MoltenNetwork.cs:152-153`).

The map is cached and rebuilt only when a cheap topology signature changes (`MoltenNetwork.cs:62-97`): `(cell count, XOR-folded position hashes, XOR-folded flow-source hashes)`.

The sort's only job is to decide which side of an undirected edge drives it - each edge is driven exactly once, by the cell farther from the source (`MoltenNetwork.cs:205-208`). It does not set the direction of transfer; see Gotchas.

### 3. `FlowEdge` — the transfer rule

`MoltenNetwork.FlowEdge(a, b, maxFlow, world)` (`MoltenNetwork.cs:220-266`):

```
if aCap <= 0 or bCap <= 0            -> nothing                     (:227-230)
diff = |a.CellAmount - b.CellAmount|
if diff == 0                          -> nothing                     (:232-234)
giver    = the cell with more units                                  (:236-238)
receiver = the other
if receiver has metal of a DIFFERENT type -> nothing (no mixing)     (:243-247)
transfer = min(diff, MoltenFlowRate)                                 (:251)
if transfer < MoltenMinFlowAmount and not receiver.AcceptsSubMinimumFlow
                                      -> nothing                     (:252-256)
accepted = receiver.PushMetalRaw(transfer, giver type, giver temp)   (:258-263)
giver.DrainMetal(accepted)                                           (:264-265)
```

`PushMetalRaw` clamps to the receiver's free space, refuses a type mismatch or a solidified cell, and volume-weight-averages the temperature of the two charges (`BlockEntityMoltenCanal.cs:195-233`, mirrored in `BEBehaviorMoltenCell.cs:181-217`).

Cells that are `Sealed` or `Solidified` are skipped on both sides before `FlowEdge` is reached (`MoltenNetwork.cs:186-191, 203-204`), and both latches also sever the *graph* at that position via `IsConnectionBroken` (`BlockEntityMoltenCanal.cs:81`).

The network tick runs at 1 s and `dt` is clamped to 2 s (`BlockNetworkModSystem.cs:42-45, 341`), so `MoltenFlowRate` is effectively units per connection per second - this is the number the settled 50 u/s target refers to.

### 4. Thermal pass and solidification

After the flow pass, every cell gets `UpdateThermal` (`MoltenNetwork.cs:215-216` → `BlockEntityMoltenCanal.cs:315-349`):

1. Read the live temperature off the carrier stack.
2. Re-stamp the cooldown rate every tick (`:327`). `MoltenMetal.SyncCooldownSpeed` rebases the cooling baseline to the current temperature (`MoltenMetal.cs:85-93`), so a live `/exmod config iiex MoltenCooldownSpeed …` applies to metal already standing in the world, and an unchanged rate is a no-op.
3. If `SolidifiesWhenCold` and `temp < meltingPoint`, latch `Solidified` and retesselate (`:338-343`).

`SolidifiesWhenCold` is `true` for plain canals and inherited by start / tap / pedestal (`BlockEntityMoltenCanal.cs:146`, and see the comments at `BlockEntityMoltenCanalTap.cs:41-44` and `BlockEntityMoltenCanalMoldPedestal.cs:44-47`). Hosted cells take it from their filler declaration (`BEBehaviorMoltenCell.cs:60`).

A solidified cell is chiselable only once `IsHardened` - below `hardenedThreshold × meltingPoint` (`BlockEntityMoltenCanal.cs:119, 356-358`); before that the block reports `iiex:canal-cooling` and refuses with `iiex-canaltoohot`. Chiselling clears the cell, lifts the latch and calls `RebuildFromRoot` so it rejoins the run (`BlockEntityMoltenCanal.cs:367-382`). Recovery goes through `MoltenChisel.BuildRecovery` (`BlockEntityMoltenCanal.cs:393-398`); the fallback item when a metal's solid drop cannot be resolved is `MetalRecoveryFallback` (`ExlibConfig.cs:103`).

Manual severing is the clay seal: `SetSealed` flips the latch and re-walks the graph (`BlockEntityMoltenCanal.cs:152-161`), and a sealed canal caps *every* connector face visually regardless of neighbours (`BlockEntityMoltenCanal.cs:520-525`).

### 5. Back-pressure — full destination stalls the furnace

There is no explicit back-pressure code. It falls out of the fact that cells own their metal and every push returns what was actually accepted:

| step | file:line | what happens when the far end is full |
|---|---|---|
| pedestal / tap drains its own cell into the mold or barrel | `BlockEntityMoltenCanalMoldPedestal.cs:202-221`, `BlockEntityMoltenCanalTap.cs:452-461` | `currentUnits >= maxUnits` → returns 0, the fitting's cell stops emptying |
| `FlowEdge` into that cell | `MoltenNetwork.cs:258-265` | `PushMetalRaw` clamps to free space → `accepted` shrinks to 0, upstream cell keeps its metal |
| the run fills back to the start | — | each cell reaches `MaxUnitCapacity` in turn |
| furnace tap hands metal down | `BlockEntityMoltenMetalTap.cs:187-200` | `CanReceiveOrSoak` still passes so the pour keeps *heating* a brim-full start (`BlockEntityMoltenCanalStart.cs:76-78, 104-112`), but `PushMetalRaw` accepts 0 |
| furnace drain | `BlockEntityBlastFurnace.cs:355-358` | `accepted == 0` → `_moltenIron` never falls |
| furnace pool caps | `BlockEntityBlastFurnace.cs:326-327` | `LiquidCapacityReached` goes true |
| melt cycle stops | `BlockEntityFurnaceCore.cs:704` | no new metal is rendered |
| extinguish timer runs | `BlockEntityFurnaceCore.cs:579-595` | it counts as one disruption; alone that is a 30 s grace, but combined with any second disruption the threshold drops to 0 and the fire goes out immediately |

The "soak" branch: a brim-full canal start being poured onto does not cool and plug, because the tap keeps raising its temperature without adding volume (`SoakHeat`, `BlockEntityMoltenCanal.cs:276-291`). Without it, a stalled run would freeze solid at the one cell the player can least afford to lose.

### 6. What merges — and the missing Ladle

Graph merge and split are no-ops (`MoltenNetwork.cs:273-278`) because there is no pooled state to redistribute; `CanMerge` only checks the other network is also a `MoltenNetwork` (`:270-271`).

Metal itself never merges implicitly: `FlowEdge` refuses a transfer whenever the receiver already holds a *different* metal code (`MoltenNetwork.cs:243-247`), and `PushMetalRaw` refuses the same (`BlockEntityMoltenCanal.cs:204-205`). Two metals sit side by side in one run without mixing.

The design rule (R3) is that the ladle is the only merging/mixing block - `docs/design/conventions.md:39,77` and `docs/design/materials.md:102-119`.

> There is no `Ladle` type in `src/`. A repo-wide search finds the word only in design documents and in two forward-looking source comments (`BEBehaviorMoltenCell.cs:20` "later the ladle / casting cell"; `BlockCastMold.cs:12` naming a ladle as a possible pour source). No block, block entity, item or behaviour implements it. Every alloying/merging fact in the design docs is therefore unbuilt, and nothing in the molten network currently mixes anything.

### 7. The other two copies of the driver

Two megablocks run their own cluster-internal flow instead of joining the graph, because their cells are hosted behaviours rather than nodes:

| driver | file:line | ordering | pull rate | flow rate |
|---|---|---|---|---|
| `MoltenNetwork` | `MoltenNetwork.cs:159-266` | farthest-from-source first; edge driven by the farther cell | n/a (fed by pour) | `MoltenFlowRate` |
| Sand casting bed | `BlockEntitySandCastingBed.cs:227-319` | basin-outward (Manhattan distance from the basin), edge driven nearer→farther, and only where both ends are carved (`:243-256`) | `PullRatePerTick = 25`, hard-coded (`:44`), from any adjacent external `IMoltenCell` on any horizontal face (`:267-287`) | `MoltenFlowRate` (`:307`) |
| Sand casting cell | `BlockEntitySandCastingCell.cs:155-170` | single hosted cell | `PullRatePerTick = 25`, hard-coded (`:33`), from the launder face only (`:172-175`) | n/a |

The bed's `FlowEdge` (`BlockEntitySandCastingBed.cs:291-319`) is a near-copy of the network's with one extra rule: a drain fitting never gives metal back (`:302-303`), so a mold hoards its charge until it hardens.

---

## Numbers

### exlib config — `ExlibConfig.cs`, file `ModConfig/ex_values.json`, section `exlib`

| key | value | file:line | what it does |
|---|---|---|---|
| `MoltenFlowRate` | `50` | `ExlibConfig.cs:80` | max units across one canal connection per tick (= per second) |
| `MoltenMinFlowAmount` | `10` | `ExlibConfig.cs:84` | minimum transfer for any flow at all, unless the receiver is a drain fitting |
| `MoltenCooldownDefault` | `24` | `ExlibConfig.cs:88` | cooldown speed stamped on a carrier stack when a caller gives none |
| `MetalLiquidThreshold` | `0.8` | `ExlibConfig.cs:91` | fraction of melting point above which metal reads *liquid* |
| `MetalHardenedThreshold` | `0.3` | `ExlibConfig.cs:95` | fraction of melting point below which metal reads *hardened* (chiselable) |
| `MetalGlowMinTemp` | `500` | `ExlibConfig.cs:98` | below this °C, hot metal emits no block light |
| `MetalRecoveryFallback` | `iiex:slag` | `ExlibConfig.cs:103` | item recovered when a metal's solid drop cannot be resolved |

### iiex config — `IiexConfig.cs`, file `ModConfig/ex_values.json`, section `iiex`

| key | value | file:line | what it does |
|---|---|---|---|
| `MoltenCooldownSpeed` | `24` | `IiexConfig.cs:77` | base cooldown speed for every molten container this mod owns |
| `BarrelCooldownCoefficient` | `1` | `IiexConfig.cs:81` | multiplier for a standalone molten barrel |
| `TapMoldCooldownCoefficient` | `1` | `IiexConfig.cs:85` | multiplier for a mold parked under a canal tap |
| `MoldPedestalCooldownCoefficient` | `1` | `IiexConfig.cs:89` | multiplier for a mold on a pedestal |
| `MoltenAmbientTemperature` | `20` | `IiexConfig.cs:92` | ambient the molten system cools toward |
| `CastMoldHeatSinkFraction` | `0.7` | `IiexConfig.cs:96` | fraction of pour temperature a cast-iron mold body soaks as its own glow |
| `CastMoldBodyCooldownPerSecond` | `25` | `IiexConfig.cs:99` | °C/s a cast-iron mold body sheds that glow |
| `ClayMoldHeatCeiling` | `1100` | `IiexConfig.cs:111` | pour temperature above which a small fired-clay tool mold shatters |
| `EnhanceVanillaMolds` | `false` | `IiexConfig.cs:119` | opt vanilla clay molds into the enhanced spill/burn/render handling |
| `MoldBurnMinTemperature` | `200` | `IiexConfig.cs:125` | mold-content °C that burns a bare-handed carrier |
| `CanalDefaultUnitCapacity` | `50` | `IiexConfig.cs:132` | base per-canal-block capacity |
| `CanalDefaultDrainSpeed` | `20` | `IiexConfig.cs:135` | tap drain speed (units/tick) when the block sets no `drainSpeed` |
| `MoldDefaultUnits` | `100` | `IiexConfig.cs:138` | mold capacity when the mold sets no `requiredUnits` |
| `BarrelDefaultMaxUnits` | `800` | `IiexConfig.cs:141` | barrel capacity fallback |
| `CanalSealClayCost` | `4` | `IiexConfig.cs:144` | fire clay to seal a straight canal |
| `CanalUnsealClayRefund` | `2` | `IiexConfig.cs:147` | fire clay returned when breaking the seal |

### Settled 2026-08-05 — cooldown derives from charge volume, not a per-container constant

> Heat loss from any vessel - ladle, converter, hearth, canal, barrel, mould - is a function of its
> surface-to-volume ratio, not a number chosen per block.

This is the square–cube law. Heat loss scales with surface area; heat content scales with volume, so the
cooling rate goes as area ÷ volume ∝ 1/L - double a vessel's linear size and it cools at half the rate.

The three per-container cooldown coefficients are therefore the wrong shape. They give the same numbers as a
derived factor does today, and diverge the instant any vessel is resized - at which point nothing fails, the
numbers are merely wrong. One factor should explain all of them:

| vessel | why it behaves as it does |
|---|---|
| canal | thin section, huge surface per unit → chills fast. That is what bounds canal-cast part size. |
| crucible hearth | a block-scale pool → holds heat through a campaign |
| ladle / converter | 3×3×3 of charge, radiating only from its surface → barely cools |

Cooling is not a global knob: big vessels hold heat, so the player's lever against "molten metal solidifies
too fast" is to build the bigger machine.

Implementation note: `MoltenMetal.SyncCooldownSpeed` already rebases to the current temperature every tick, so
a rate derived per-tick from the current charge volume applies correctly to metal already standing in the
world - a partly-drained ladle cools faster as it empties. The parked-barrel literal `300f` and the
`BarrelCooldownCoefficient` it bypasses (Gotcha 11) both dissolve into this.

### Per-cell capacities (derived, not separate config keys)

| cell | capacity | file:line |
|---|---|---|
| plain canal (straight / bend / T / X) | `CanalDefaultUnitCapacity` = 50 | `BlockEntityMoltenCanal.cs:39` |
| canal start | `× 2` = 100 | `BlockEntityMoltenCanalStart.cs:24-25` |
| canal tap | `ceil(/2)` = 25 | `BlockEntityMoltenCanalTap.cs:53-54` |
| mold pedestal | `ceil(/2)` = 25 | `BlockEntityMoltenCanalMoldPedestal.cs:68-69` |
| molten barrel | `maxUnits` attribute = 800 | `BlockMoltenBarrel.cs:36, 60` |
| parked mold (tap or pedestal) | `requiredUnits` attribute, default 100 | `BlockEntityMoltenCanalTap.cs:334-337`, `BlockEntityMoltenCanalMoldPedestal.cs:134-137` |
| hosted cell (`BEBehaviorMoltenCell`) | `capacity` property, default 100 | `BEBehaviorMoltenCell.cs:35, 57` |
| hosted cell with a rammed pattern | pattern spec wins over both | `BEBehaviorMoltenCell.cs:95, 102-106` |

### Hard-coded — **not config**

| constant | value | file:line | note |
|---|---|---|---|
| network tick interval | `1000 ms` | `BlockNetworkModSystem.cs:42-45` | makes `MoltenFlowRate` a per-second rate |
| `dt` catch-up clamp | `2 s` | `BlockNetworkModSystem.cs:341` | |
| sand-bed / sand-cell `PullRatePerTick` | `25` | `BlockEntitySandCastingBed.cs:44`, `BlockEntitySandCastingCell.cs:33` | contradicts the settled one-number 50 u/s rule |
| blast-furnace per-tick hand-down | `min(20, pool)` | `BlockEntityBlastFurnace.cs:346, 382` | contradicts the settled 50 u/s rule |
| furnace iron stack yield factor | `× 0.6` | `BlockEntityBlastFurnace.cs:349` | slag uses `× 0.8` (`:385`) |
| barrel chisel-out bit size | `10 u` per bit | `BlockEntityMoltenBarrel.cs:283-289` | |
| barrel break-drop bit size | `5 u` per bit (the `MoltenChisel` default) | `BlockEntityMoltenBarrel.cs:362-367` | |
| tap barrel-fill cooldown speed | `300f` | `BlockEntityMoltenCanalTap.cs:400-405` | a *parked barrel* deliberately ignores `BarrelCooldownCoefficient` (`:117-118`) |
| pour-tally idle timeout | `5000 ms` | `BlockEntityMoltenCanalStart.cs:36` | |
| pour / drain sound throttle | `2000 ms` | `BlockEntityMoltenCanalStart.cs:122-129`, `BlockEntityMoltenCanalTap.cs:433-441` | |
| `BEBehaviorMoltenCell.DefaultCapacity` | `100` | `BEBehaviorMoltenCell.cs:35` | |
| glow scale | `(T − 500) / 30`, clamped 0–24 | `MoltenMetal.cs:159-162` | |

---

## Code

| type | file:line | role |
|---|---|---|
| `MoltenNetwork : BlockNetwork` | `MoltenNetwork.cs:21` | the driver; `NetworkType => "molten"` (`:23`) |
| `MoltenNetwork.OnTick` | `MoltenNetwork.cs:159` | collect → order → flow → cool. The whole model is here |
| `MoltenNetwork.FlowEdge` | `MoltenNetwork.cs:220` | the transfer rule (static; safe to reason about in isolation) |
| `MoltenNetwork.BuildDistanceFromStart` | `MoltenNetwork.cs:104` | multi-source BFS, horizontals only |
| `MoltenNetwork.ComputeTopologySignature` | `MoltenNetwork.cs:81` | the cache-invalidation fingerprint |
| `IMoltenCell` | `IMoltenCell.cs:16` | the extension point. Implement this to be carried by the network |
| `BEBehaviorMoltenCell` | `BEBehaviorMoltenCell.cs:29` | `IMoltenCell` + `IFillerHostedBehavior`; config via `ConfigureFromFiller` (`:65`) |
| `BlockEntityMoltenCanal` | `BlockEntityMoltenCanal.cs:26` | the node implementation; `IsConnectionBroken` at `:81` |
| `BlockEntityMoltenCanalStart` | `BlockEntityMoltenCanalStart.cs:19` | `IsFlowSource = true` (`:28`); `ILiquidMetalSink` - the furnace-facing entry point (`:87`) |
| `BlockEntityMoltenCanalTap` | `BlockEntityMoltenCanalTap.cs:22` | drains its own cell into a parked barrel/mold (`:379`); closed tap severs itself (`:49-50`) |
| `BlockEntityMoltenCanalMoldPedestal` | `BlockEntityMoltenCanalMoldPedestal.cs:22` | same, for a small tool mold; clay heat gate at `:191-199` |
| `BlockEntityMoltenBarrel` | `BlockEntityMoltenBarrel.cs:23` | not an `IMoltenCell` and not a network node - a plain `ILiquidMetalSink` + `IChiselableMolten` |
| `BlockEntityMoltenMetalTap` | `BlockEntityMoltenMetalTap.cs:21` | the furnace-side spout; `TryPourMetal` at `:168` |
| `MoltenMetal` | `MoltenMetal.cs:30` | temperature/cooldown/classification helpers shared by every fitting |
| `MoltenChisel.BuildRecovery` | used at `BlockEntityMoltenCanal.cs:393` | the one recovery-drop builder |
| registration | `IronIndustryExpandedModSystem.cs:92-95` | `netManager.RegisterNetworkType("molten", () => new MoltenNetwork(netManager))` |

**Where a caller hooks in**

- *To be transported*: implement `IMoltenCell` on a `BlockEntityNetworkNode` whose block reports `NetworkType == "molten"`, or add `BEBehaviorMoltenCell` and drive it yourself.
- *To receive a pour from a furnace or a held crucible*: implement vanilla `ILiquidMetalSink` (see `BlockEntityMoltenCanalStart.cs:60-131` and `BlockEntityMoltenBarrel.cs:91-161`).
- *To be chiselable*: implement `IChiselableMolten` (`BlockEntityMoltenCanal.cs:352-360`); the ritual (sound, give/spawn) lives in `MoltenChisel`, invoked from the block.
- *To gate a pour by temperature*: `ClayHeatGate.WouldShatter` (`BlockEntityMoltenCanalMoldPedestal.cs:194`).

---

## Gotchas

1. `FlowEdge` moves the whole difference, not half of it. `transfer = min(diff, maxFlow)` (`MoltenNetwork.cs:251`). With two 50-cap cells at 40 and 0, the whole 40 moves and the levels invert (0 and 40) rather than equalising at 20; next tick they swap back. The doc comment at `MoltenNetwork.cs:219` ("Moves metal across one connection toward equal fill ratio") describes an algorithm that is not implemented. True equalisation would be `diff / 2`.

2. It compares raw amounts, not fill ratios. Despite the "equal fill ratio" wording, `diff` is `|a.CellAmount − b.CellAmount|` (`:232`). With the shipped unequal capacities (start 100, canal 50, tap 25) equal *amounts* are very different *ratios*.

3. The ordering does not set the flow direction. The comments at `MoltenNetwork.cs:141-144` and `:176-177` say metal is "driven from the farthest cells back toward the source" / "drains toward the source". It is not: direction is decided purely by which cell has more units (`:236-238`). Metal will flow back into the canal start whenever the downstream cell holds more. The sort only picks which endpoint executes each undirected edge. Stale comment.

4. Vertical canal connections never flow. Both the BFS (`:125`) and the flow loop (`:193`) iterate `BlockFacing.HORIZONTALS`, but the graph traversal that decides network membership walks `ALLFACES` (`BlockNetworkModSystem.cs:398`). A vertically stacked pair of canals is in the same network and never exchanges metal.

5. `MoltenNetwork` never overrides `OnTopologyChanged`. `PipeNetwork` does (`PipeNetwork.cs:830`); molten does not, so the distance cache is invalidated only by its own signature (`MoltenNetwork.cs:68`). The signature covers cell count, positions and which cells are sources - not which faces each cell exposes. A `RemoveNode` that does not fracture keeps the same network instance (`BlockNetworkModSystem.cs:244-250`), so a change that only alters connectors can leave a stale distance map.

6. `MoltenMinFlowAmount = 10` leaves permanent residue. Between two plain canals a difference below 10 never moves, so a settled run can hold up to 9 units of imbalance per edge forever, and a cell can never shed its last <10 units except into a tap or pedestal (`AcceptsSubMinimumFlow`, `MoltenNetwork.cs:252-256`).

7. The solidify latch trips at 100 % of the melting point, but chisel-out needs < 30 %. `UpdateThermal` latches at `temp < meltPoint` (`BlockEntityMoltenCanal.cs:338`) while `IsHardened` uses `MetalHardenedThreshold × meltPoint` (`ExlibConfig.cs:95`). Between those a cell is a plug that reads `iiex:canal-cooling` and refuses the chisel. For iron (1482 °C) that is the whole span from 1482 down to ~445 °C. `CellState` can still classify the metal as Liquid (above `0.8 × meltPoint` = ~1186 °C) while `Solidified` is already latched - the two are independent by design (`BlockEntityMoltenCanal.cs:89-112`).

8. The molten barrel is not a cell. `BlockEntityMoltenBarrel` (`:23-27`) extends plain `BlockEntity` - it has no `IMoltenCell`, is not a network node, and is filled only by a tap draining into it or a direct pour. Do not expect network flow to reach it.

9. A closed tap or pedestal severs itself from the run. `IsConnectionBroken()` returns true when not pouring (`BlockEntityMoltenCanalTap.cs:49-50`, `BlockEntityMoltenCanalMoldPedestal.cs:52-53`), so a closed fitting does not merely stop delivering - it drops off the graph and its own cell stops filling. Toggling re-walks the graph via the property setter (`:35-38`, `:38-41`).

10. `BlockEntityMoltenCanal.MaxUnitCapacity`'s doc comment lies. `BlockEntityMoltenCanal.cs:38` says "(from the block's `maxUnits` attribute)" but line 39 returns `IiexValues.CanalDefaultUnitCapacity` unconditionally - no attribute is ever read for a canal cell. Stale comment.

11. The parked-barrel cooldown ignores its own coefficient. The tap passes a literal `300f` when creating the barrel's content stack (`BlockEntityMoltenCanalTap.cs:400-405`) while a parked *mold* uses `MoltenCooldownSpeed × TapMoldCooldownCoefficient`. The comment at `:117-118` says this is deliberate ("the parked barrel cools at a fixed slow rate by design"), but it means `BarrelCooldownCoefficient` has no effect on metal poured through a tap - only on a standalone barrel.

12. The world accessor is resolved once and cached forever (`MoltenNetwork.cs:28-41`), on the assumption that a network never moves between worlds.

13. `IsFlowSource` is the *canal start*, not the furnace. The furnace tap is not part of the molten graph at all; it finds the start block by offset (`BlockEntityMoltenMetalTap.cs:173-183`) and calls `ILiquidMetalSink` on it. Move the start and the run has no BFS root: every cell then sorts as `int.MaxValue` and edge ownership degenerates to position order.

---

## Open

- Throughput is three different numbers, not one. The settled decision is a single 50 u/s for tap, bed pull and canal. Shipping code has canal-to-canal at `MoltenFlowRate = 50` (`ExlibConfig.cs:80`), tap-to-container at `CanalDefaultDrainSpeed = 20` (`IiexConfig.cs:135`), sand-bed and sand-cell pull at a hard-coded `25` (`BlockEntitySandCastingBed.cs:44`, `BlockEntitySandCastingCell.cs:33`), the furnace hand-down at a hard-coded `20` (`BlockEntityBlastFurnace.cs:346`), and the mold pedestal with no rate cap at all - it drains `min(CellAmount, space)` in one tick (`BlockEntityMoltenCanalMoldPedestal.cs:219`). Unifying these is unbuilt work.
- No `Ladle`. R3's mixing block, and with it every alloying rule in `materials.md`, does not exist in code. `BEBehaviorMoltenCell` was built to make it cheap (`:20`), but nothing consumes that yet.
- `FlowEdge` is duplicated three times (network, casting bed, casting cell) with three different orderings and two different pull rates. There is no shared primitive.
- No cross-cell heat conduction. Cells exchange heat only when metal actually moves (via the weighted average in `PushMetalRaw`) or when a pour soaks a full cell. A standing run cools cell-by-cell independently.
- Diagonal / vertical runs. Gotcha 4 means a canal can only ever be a flat horizontal network. Whether vertical drops should flow is undecided.
- The topology signature does not cover connector orientation (Gotcha 5). Whether to add it, or to override `OnTopologyChanged`, is undecided.
