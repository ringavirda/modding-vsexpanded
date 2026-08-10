# Practices and traps from the vanilla source

Patterns vanilla follows consistently, and behaviour its source reveals that the docs do not. Every
entry cites the file it was read out of, under `.compat/vintagestory/`.

Ordered by how much it matters to this repo, not by where it was found. See
[api-map.md](api-map.md) for where the cited types live, and [README.md](README.md) for the
vendored revisions.



## High relevance

### Always call `base.` on every BlockEntity override — the base implementations are not empty, they fan out to `Behaviors`.

`Initialize` :85, `OnBlockRemoved` :220, `OnBlockUnloaded` :275, `OnBlockPlaced` :292, `To/FromTreeAttributes` :320/:339, `OnReceivedClient/ServerPacket` :357/:438, `GetBlockInfo` :470, `OnTesselation` :536 and `OnStore/OnLoadCollectibleMappings` :484/:517 all loop `Behaviors`. Skipping base silently disables every attached BlockEntityBehavior — including third-party ones a player added by JSON patch. Additionally, skipping base on `OnBlockRemoved`/`OnBlockUnloaded` leaks your own tick listeners (they are unregistered at :213 and :267).

— `vsapi/Common/Collectible/Block/BlockEntity.cs:212-226, :261-285, :302-324, :332-345` · vsapi

### Behavior chains use the `preventDefault` / `PreventSubsequent` idiom: iterate all behaviors, accumulate `result &=`, only skip the default body if some behavior set a non-PassThrough handling, and return immediately on `PreventSubsequent`.

This is the shape of every single dispatch in Block.cs. If you write a custom Block base class that hosts behaviors, copy it exactly — the subtle part is that `handled != PassThrough` (i.e. `Handled` too) sets `preventDefault`, so `EnumHandling.Handled` also suppresses the default body in most methods while still letting later behaviors run.

— `vsapi/Common/Collectible/Block/Block.cs:887-902 (TryPlaceBlock), :1447-1462 (OnBlockInteractStart), :1281-1291 (GetDrops)` · vsapi

### A block-info readout is built by `Block.GetPlacedBlockInfo` calling `BlockEntity.GetBlockInfo` FIRST, inside a try/catch that swallows the exception and appends `(error in <TypeName>)`.

A throwing GetBlockInfo will not crash the client — it produces a near-invisible '(error in X)' line and an entry in client-main.log. If a readout mysteriously shows that string, do not look for a crash; look at the log.

— `vsapi/Common/Collectible/Block/Block.cs:2272-2287` · vsapi

### Guard client-only work behind `Api.Side == EnumAppSide.Client` / `Api is ICoreClientAPI`, and re-check `Api != null` in any helper reachable from FromTreeAttributes.

BEContainerDisplay does exactly this in `RedrawAfterReceivingTreeAttributes` (`worldForResolving.Side == Client && Api != null`) precisely because FromTreeAttributes runs before Initialize on the first load. `updateMeshes` :188 and `updateMesh` :202 both re-check `Api == null || Api.Side == Server`.

— `vssurvivalmod/BlockEntity/BEContainerDisplay.cs:168-175, :186-210` · vsapi

### Keep a `MarkMeshesDirty()`-style cheap invalidation separate from the actual remesh, and pair it with `MarkDirty(true)` / `MarkBlockDirty(Pos)`; do the real work lazily inside `OnTesselation`.

`OnTesselation` runs on the tesselation thread, so building meshes there keeps the main thread free; the vanilla pattern nulls `tfMatrices` as the dirty flag and rebuilds at the top of OnTesselation. Invalidating without a MarkBlockDirty means the chunk is never re-tesselated and the change never appears.

— `vssurvivalmod/BlockEntity/BEContainerDisplay.cs:181-184, :324-342` · vsapi

### Register block/BE/behavior classes in `ModSystem.Start(ICoreAPI)`, on BOTH sides, never in StartClientSide/StartServerSide.

Every Register* doc says "Must happen before any blocks are loaded. Be sure to register it on the client and server side." A class registered only server-side produces blocks that fall back to the base class on the client (wrong mesh, no renderer) with no error.

— `vsapi/Common/API/ICoreAPI.cs:56-104` · vsapi

### Use `Block.GetInterface<T>(world, pos)` instead of hand-rolling a Block→behavior→BE→BE-behavior probe.

It searches in a defined order (Block itself, then BlockBehavior with inheritance, then BlockEntity, then BlockEntityBehavior) and null-guards a null pos. Hand-rolled versions routinely miss the BE-behavior tier.

— `vsapi/Common/Collectible/Block/Block.cs:2706-2723` · vsapi

### For a BE that needs a network packet, use `capi.Network.SendBlockEntityPacket(BlockPos, packetId, data)` / `sapi.Network.BroadcastBlockEntityPacket(pos, packetId, data)` rather than registering a channel — but pick packet ids ≥ 2000 and ≠ 5000/5001 if there is any chance of a container BE in the hierarchy.

There is a generic protobuf overload `SendBlockEntityPacket<T>(BlockPos, int, T)` so you do not need SerializerUtil by hand. The id space is shared: `<1000` is claimed by the inventory network util, 1000/1001 by Open/Close, 5000/5001 by the container lid.

— `vsapi/Client/API/IClientNetworkAPI.cs:128 & :227, vsapi/Server/API/IServerNetworkAPI.cs:76 & :128, vssurvivalmod/BlockEntity/BEOpenableContainer.cs:156, vsapi/Client/UI/Dialog/GuiDialogBlockEntity.cs:196-200` · vsapi

### Validate permission and reach on EVERY server-side `OnReceivedClientPacket` using the new `BlockEntity.CachedAccessPerms` helper.

Client packets are attacker-controlled; without this a player can drive any BE at any distance through any claim. The struct caches the claim lookup so several handlers can share one instance, and it writes a `Logger.Audit` line on rejection. Vanilla containers do this and also roll the inventory back on rejection.

— `vsapi/Common/Collectible/Block/BlockEntity.cs:367-428, vssurvivalmod/BlockEntity/BEOpenableContainer.cs:158-163` · vsapi

### Implement `IRotatable.OnTransformed` on any BE whose saved tree encodes an orientation, and remember the BE is not in the world when it runs.

WorldEdit/schematic rotation calls `CreateBehaviors` then `OnTransformed` on a detached BE with only `Pos` set — `Api` is null, `Initialize` has not run, and reading neighbouring blocks is undefined. Without it, rotated schematics of your machines keep their old facing in the tree.

— `vsapi/Common/Collectible/Block/IRotatable.cs:10-26, vsapi/Common/Collectible/Block/BlockSchematic.cs:950-961` · vsapi

### Use `MultiblockStructure.InitForUse(float rotateYDeg)` with DEGREES and prefer `WalkMatchingBlocks` / `InCompleteBlockCount` over hand-written offset loops.

The parameter name is `rotateYDeg`; passing radians silently produces a structure that never validates. `InCompleteBlockCount` takes a `PositionMismatchDelegate` so you get the offending block and the expected code for free.

— `vsapi/Common/MultiblockStructure.cs:52, :76, :106` · vsapi

### Read block state through the `Get*(IBlockAccessor, BlockPos, …)` virtuals (`GetBlockMaterial`, `GetResistance`, `GetAttributes`, `GetLightAbsorption`, `GetSounds`, `GetRequiredMiningTier`) rather than the raw fields, when not on a hot path.

Vanilla and mods override these per-position (BlockMultiblock overrides `GetAttributes` to forward to the controller). Reading `block.BlockMaterial` directly bypasses that and gives the wrong answer for multiblock parts.

— `vsapi/Common/Collectible/Block/Block.cs:656, :667, :704 ("Position-aware version of Attributes, for example can be used by BlockMultiblock"), :719, :686, :2365` · vsapi

### Do all mesh/shape/texture allocation client-side only, and null out client-only fields on the server.

From 1.20.4 the server nulls `Textures`, `TexturesInventory`, `ShapeInventory`, `Lod0Shape`, `Lod2Shape` and all `ModelTransform`s via `FreeRAMServer()`. Code that touches `block.Textures` in a server-reachable path NREs on a dedicated server while working fine in singleplayer.

— `vsapi/Common/Collectible/Block/Block.cs:2912-2925, :233, :244, :259, :272; vsapi/Common/Collectible/Item/Item.cs:168-177` · vsapi

### Iterate neighbours with `foreach (var face in BlockFacing.ALLFACES) { face.IterateThruFacingOffsets(pos); ... }` and close with `BlockFacing.FinishIteratingAllFaces(pos)`, instead of allocating `pos.AddCopy(face)` per face.

The offsets are CUMULATIVE deltas, not absolute — the loop only lands on the right positions if you visit all six faces in exactly ALLFACES order and never break early. Doing it any other way silently reads the wrong blocks. The pay-off is zero allocation per neighbour and dimension-awareness (the vanilla xmldoc calls the naive Vec3i loop out as not dimension-aware).

— `vsapi/Math/BlockFacing.cs:369-412` · vsapi

### For anything that must survive a save/reload, prefer `SetDouble`/`GetDouble` and `SetLong`/`GetLong` over `SetFloat`/`SetInt` when the same key may ever be authored in JSON.

VS itself documents this: Newtonsoft deserializes JSON numbers into Double and Long attributes, so a value written by JSON and then read with GetFloat/GetInt returns the DEFAULT (the `as` cast fails). The xmldoc says it in one line: 'If you need this attribute to be compatible with deserialized json - use SetLong()/SetDouble()'.

— `vsapi/Datastructures/AttributeTree/TreeAttribute.cs:411-413 and :457-459` · vsapi

### Use the `RegisterGameTickListener` overloads that take an `Action<Exception> errorHandler` for machine ticks.

vsapi ships errorHandler variants specifically because an unhandled throw inside a tick listener is otherwise invisible; four of the five overloads exist only to carry it.

— `vsapi/Common/API/IEventAPI.cs:199, :222, :234` · vsapi

### Register every block/item/BE/behaviour class in `Start(ICoreAPI)` — never in StartClientSide/StartServerSide — and never touch `api.Assets` there.

The xmldoc is explicit: Start() runs before assets are loaded and before blocks/items exist, and asset lookups at that point fail even when the file is on disk. Class registration must have happened before the block loader runs at ExecuteOrder 0.2.

— `vsapi/Common/API/ModSystem.cs:70-80` · vsapi

### Set `ExecuteOrder()` deliberately against vanilla's published table (JsonPatch 0.05, mantle 0.1, Block+Item loader 0.2, recipes 1.0) rather than leaving the 0.1 default.

AssetsLoaded() at ExecuteOrder < 0.2 sees no registered blocks; < 0.6 sees no recipes. Silently reading an empty world.Blocks is the failure mode.

— `vsapi/Common/API/ModSystem.cs:39-59 and :85-86` · vsapi

### Prefer `AddMerge` over `Add` in JSON patches that target arrays.

The vanilla docs call this out for mod compatibility: `Add` replaces, `AddMerge` merges the existing array with the patched one so two mods patching the same array both survive.

— `vsapi/docs/json-docs/jsondocs/Vintagestory.ServerMods.NoObf.EnumJsonPatchOp.html (Add=0 and AddMerge=6 descriptions)` · vsapi

### Use `WithPathPrefixOnce` / `WithPathAppendixOnce` / `CopyWithPathPrefixAndAppendixOnce` rather than the unconditional variants when a code may already carry the prefix.

The unconditional versions blindly concatenate, producing textures/textures/foo.png.png. The Once variants exist precisely because that is a common bug. Also remember the non-Copy* forms mutate the receiver.

— `vsapi/Common/Assets/AssetLocation.cs:325-368 and :464-474` · vsapi

### Wrap every custom BE renderer registration/unregistration symmetrically per EnumRenderStage, and dispose MeshRefs.

RegisterRenderer/UnregisterRenderer are keyed by (renderer, stage). Registering for Opaque and unregistering for OIT leaves a live renderer on a destroyed BE. AnimationUtil demonstrates the pattern — it registers Opaque in its ctor and unregisters Opaque in Dispose.

— `vsapi/Common/Model/Animation/AnimationUtil.cs:35 and :183-187` · vsapi

### Register network message types in an identical, order-stable sequence on both sides, and keep protobuf contracts append-only.

The wire id of a message is its registration index; a client that registers types in a different order will decode a packet as the wrong type. vsapi states it in the xmldoc of both RegisterMessageType overloads.

— `vsapi/Client/API/IClientNetworkChannel.cs:47-58` · vsapi

### Do numeric string parsing with the `StringUtil` extensions (`text.ToFloat()`, `.ToDouble()`, `.ToInt()`) rather than raw `float.Parse`.

They all pin `GlobalConstants.DefaultCultureInfo` (InvariantCulture) and `NumberStyles.Any`, so a German or Ukrainian locale does not turn "1.5" into 15. Raw Parse/TryParse uses the current culture.

— `vsapi/Util/StringUtil.cs:127-184 and vsapi/Config/GlobalConstants.cs:20` · vsapi

### Test 'is this chunk actually loaded?' with `GetChunkAtBlockPos(pos) != null` or `GetBlockOrNull(...)`, never by comparing a GetBlock result to air.

GetBlock returns the air block for unloaded chunks AND for real air, so a network walk that treats air as 'end of network' will silently truncate whenever a chunk is unloaded — and the result depends on chunk load order, i.e. it reproduces only sometimes.

— `vsapi/Common/API/IBlockAccessor.cs:242-246, :256-262, :316-324` · vsapi

### When a mod's lang file lives under `assets/<moddomain>/lang/`, always call `Lang.Get("<moddomain>:key")`.

TranslationService namespaces every loaded entry by the asset's own domain, and Lang defaults a colon-less key to `game:`. A bare key therefore looks up `game:key`, misses, and Lang.Get returns the raw key string to the player.

— `vsapi/Localization/TranslationService.cs:86 and :606-613` · vsapi

### Make exactly one class the network *owner* (a `ModSystem`) holding `Dictionary<long, Network>`; block entities hold only a `long NetworkId` and a transient reference. On save-game load the owner throws all networks away and lets producers rediscover them.

If networks are persisted, a world edit / chunk-corruption / mod update leaves stale topology that no longer matches the blocks, and every subsequent rebuild inherits the corruption. Vanilla explicitly resets: `Event_SaveGameLoaded` does `this.data = new MechPowerData()`. Note the corollary — `MechanicalNetwork.ReadFromTreeAttribute`/`WriteToTreeAttribute` exist but are never called from anywhere in the codebase; they are abandoned scaffolding.

— `vssurvivalmod/Systems/MechanicalPower/MechanicalPowerMod.cs:223 and Network/MechanicalNetwork.cs:313` · vssurvivalmod

### Only nodes that declare a non-null `OutFacingForNetworkDiscovery` may create a network; everything else is inert and waits to be discovered. That property is set in the *rotor constructor* from `Block.Variant["side"]`, not in `Initialize`.

If inert nodes could create networks, chunk-load order would decide how many networks exist and every axle would spawn a competing one. Setting it in the constructor matters because `MechanicalPowerMod.RebuildNetwork` reads `newnode.OutFacingForNetworkDiscovery` on a freshly fetched behavior before `Initialize` has necessarily run.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPRotor.cs:57 and MechanicalPowerMod.cs:268` · vssurvivalmod

### Topology is expressed by overriding one method — `GetMechPowerExits(MechPowerPath entryDir)`. Return `Array.Empty<MechPowerPath>()` to be a dead end (all rotors and all consumers do), return `[entry, entry.Opposite-with-inverted-sense]` to be a pass-through, return a computed set to be a junction.

The flood-fill (`spreadTo` → `JoinAndSpreadNetworkToNeighbours` → `GetMechPowerExits`) is the only thing that walks the world. A block that forgets to return empty becomes a hub and joins networks that should be separate; a block that returns a face without a matching `HasMechPowerConnectorAt` silently drops the branch.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPBase.cs:556, BEBehaviorMPRotor.cs:132, BEBehaviorMPConsumer.cs:73` · vssurvivalmod

### Discovery is chunk-aware: `spreadTo` returns `false` with a `missingChunkPos` when the target chunk is not loaded (but returns `true` when the position is outside the map). The caller records it via `network.AwaitChunkThenDiscover(missingChunkPos)`, the network's `fullyLoaded` flag goes false, `MechanicalPowerMod` stops ticking it, and `Event_ChunkDirty` re-tests and calls `RebuildNetwork` once every chunk in `inChunks` is present.

Without this, a network that straddles a chunk boundary is silently truncated at whatever the load order happened to be, and the truncated half runs at the wrong speed forever. The `OutsideMap` check at l.543 is equally necessary or every network touching worldedge stays permanently un-loaded.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPBase.cs:516 and MechanicalPowerMod.cs:326` · vssurvivalmod

### Never let the server read network state out of the block entity's tree. `BEBehaviorMPBase.FromTreeAttributes` wraps the whole networkId/propagationDir/gearedRatio restore in `if (worldAccessForResolve.Side == EnumAppSide.Client)`.

The in-line comment states the failure exactly: "don't ever change network settings from tree on server side - networkId is not data to be saved (otherwise would mess up networks on chunk loading, if BE tree loaded after a BE has already had network assigned on the server by propagation from a neighbour)". Server truth comes from discovery; client truth comes from the tree + the `vsmechnetwork` broadcast.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPBase.cs:326` · vssurvivalmod

### Carry the gear ratio in the propagation token, not in the node. `GearedRatio` is only ever written by `SetPropagationDirection` from `path.gearingRatio` — plus one direct assignment at `CreateJoinAndDiscoverNetwork` l.464 and the `LargeGear3m` override that divides by 5.5. Nodes that need a side-dependent view expose `GetGearedRatio(BlockFacing)`.

A ratio that a node computes for itself desynchronises the moment discovery arrives from the other side. Because the whole chain multiplies through the `MechPowerPath`, adding a ratio-changing block means overriding `SetPropagationDirection` + `GetGearedRatio` + `GetMechPowerExits` together — `BEBehaviorMPLargeGear3m` is the only vanilla example and it overrides all three.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPBase.cs:399 and BEBehaviorLargeGear3m.cs:45,79,85` · vssurvivalmod

### The network hands each node a *pre-scaled* speed: `powerNode.GetTorque(tick, speed * r, out resistance)` where `r = node.GearedRatio`; the returned torque is scaled back by `r` and resistance by `|r|`.

This is the entire gearing model — a node never needs to know about the ratio in its own physics. If you write `GetTorque` assuming raw network speed you get a machine whose behaviour changes when someone puts a large gear anywhere upstream. Consumers should read `BEBehaviorMPConsumer.TrueSpeed` (`|Network.Speed * GearedRatio|`) rather than `Network.Speed` for the same reason.

— `vssurvivalmod/Systems/MechanicalPower/Network/MechanicalNetwork.cs:221 and BlockEntityBehavior/BEBehaviorMPConsumer.cs:23` · vssurvivalmod

### A producer expresses itself as (TargetSpeed, TorqueFactor, Resistance, AccelerationFactor), never as a speed it forces on the network. Torque is `max(0, capableSpeed - |speed|) * TorqueFactor * dir`, so it falls to zero as the network reaches the rotor's target speed, and resistance rises when the network overspeeds it.

This is what makes chaining producers additive in torque and non-additive in speed. Two windmills on one network give twice the torque and the same top speed. Hard-setting speed would make the last producer to tick win.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPRotor.cs:88-125` · vssurvivalmod

### Machines that could be spun up dangerously fast clamp the network on join: `if (|network.Speed * GearedRatio| * 1.6 > 1) { network.Speed /= that; network.clientSpeed /= that; }`.

Without it, attaching a helve hammer or pulverizer to an already-fast, low-resistance network produces one burst of absurd output before the resistance sum catches up. Both the toggle and the pulverizer carry the identical block with the identical comment.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorToggle.cs:94-106 and BEBehaviorPulverizer.cs:116-128` · vssurvivalmod

### Runtime disconnect is implemented by returning no exits and letting the block's `GetNetwork` return null — then triggering a full `RebuildNetwork` via `manager.OnNodeRemoved(this)`. There is no incremental "split network" code path anywhere.

Splitting a graph incrementally is where correctness bugs live; vanilla always deletes and rediscovers from the producers. `MechanicalPowerMod.RebuildNetwork` then copies the old speed/torque onto each new network, negating them if the node's propagation direction flipped, so the visual doesn't snap.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorTransmission.cs:84,96 and MechanicalPowerMod.cs:241-280` · vssurvivalmod

### Bespoke (non-instanced) renderers read `AngleRad` off the MP behavior *after* drawing, and animated machines set the animation's `CurrentFrame` from the angle rather than letting the animator advance itself.

Setting `animState.CurrentFrame = mpc.AngleRad / TWOPI * 60` keeps the animation phase-locked to network angle across clients and across pauses; letting `AnimationSpeed` alone drive it drifts. `BlockEntityMechPoweredBellows` also sets `AnimationSpeed = Network.Speed * 2f` anyway, with the comment "Otherwise doesn't animate or sound doesn't play properly".

— `vssurvivalmod/BlockEntity/BlockEntityMechPoweredBellows.cs:59-74 and BlockEntityRenderer/QuernTopRenderer.cs:78-81` · vssurvivalmod

### Time-based machines store `double lastTickTotalHours` in the tree, and on each tick compute `hoursPassed = Api.World.Calendar.TotalHours - lastTickTotalHours`, **guarding against a negative result** (schematic import / calendar rollback) by resetting the stamp and treating it as zero.

This is the only away-catch-up mechanism vanilla has, and every implementation that skips the negative guard mis-behaves on imported schematics. `BlockEntityFastForwardGrowth` adds the two further refinements worth copying: clamp the catch-up to one in-game year, and step the simulation in 3–4 hour intervals instead of one giant delta.

— `vssurvivalmod/BlockEntity/BEForge.cs:197-248 and BlockEntity/BlockEntityFastForwardGrowth.cs:49-136` · vssurvivalmod

### A machine that can be either hand-driven or MP-driven exposes one `GrindSpeed`-style property that folds both, and flips an `automated` flag from `BEBehaviorMPConsumer.OnConnected`/`OnDisconnected` wired up in `CreateBehaviors` (not `Initialize`).

`CreateBehaviors` runs before `FromTreeAttributes` and before `Initialize`, so the callbacks are already installed when the behavior joins a network during chunk-load discovery. Wiring them in `Initialize` misses the first `JoinNetwork`.

— `vssurvivalmod/BlockEntity/BEQuern.cs:74-83,208-234` · vssurvivalmod

### Simulate on the server only; sync by calling `MarkDirty()`; let `FromTreeAttributes` drive all client-side visuals.

If you tick simulation on both sides you get divergence and the client fights the server every sync. Vanilla firepit literally early-returns on the client at the top of the burn tick and only updates its renderer.

— `vssurvivalmod/BlockEntity/Firepit/BEFirepit.cs:194 (`if (Api is ICoreClientAPI) { renderer...OnUpdate(...); return; }`) and l.181-188 (`On500msTick` -> `MarkDirty()` when burning)` · vssurvivalmod

### Accumulate machine progress from `Api.World.Calendar.TotalHours` deltas stored in the tree, never from the tick `dt`.

A dt-based machine stops when the chunk unloads and runs at a different speed at different tick rates. The hours delta survives save/load and gives free away-time catch-up. Vanilla also guards against a negative delta (calendar rollback) by resetting the anchor.

— `vssurvivalmod/BlockEntity/BEForge.cs:199-206 (`hoursPassed = Calendar.TotalHours - lastTickTotalHours; if (hoursPassed < 0) { lastTickTotalHours = ...; hoursPassed = 0; }`) and BEBloomery.cs:318 (`burningUntilTotalDays = TotalDays + 10/24.0`)` · vssurvivalmod

### Express fractional consumption as a `float partial…Consumed` accumulator that is folded into the integer StackSize with `%= 1`.

Lets you burn 0.5 coal/hour without ever holding a fractional item, and it round-trips through the tree cleanly.

— `vssurvivalmod/BlockEntity/BEForge.cs:493-501 (`updateFuelLevel`) with `FuelLevel => FuelSlot.StackSize - partialFuelConsumed` at l.30` · vssurvivalmod

### Change a machine's visual/logical state by swapping to a sibling block variant with `Block.CodeWithVariant(key, value)` + `BlockAccessor.ExchangeBlock`, and cache `this.Block` afterwards.

ExchangeBlock keeps the block entity alive (SetBlock would destroy it). Forgetting to reassign `this.Block` leaves the BE reading the old variant for the rest of the tick.

— `vssurvivalmod/BlockEntity/Firepit/BEFirepit.cs:521-529 (`setBlockState`)` · vssurvivalmod

### Build `WorldInteraction[]` once per world inside `Block.OnLoaded` under `ObjectCacheUtil.GetOrCreate(api, "<name>Interactions", …)`, client-side only, and filter live with `GetMatchingStacks`.

Scanning `api.World.Collectibles` per block instance costs seconds of load time on a big modpack; the cache is keyed per world so it is invalidated correctly on reconnect.

— `vssurvivalmod/Block/BlockBloomery.cs:15-88 and vssurvivalmod/Block/BlockForge.cs:21-118` · vssurvivalmod

### Declare a large fixed structure as data (`MultiblockStructure` in `Block.Attributes["multiblockStructure"]`), call `InitForUse(rotationDegrees)` once in Initialize, then poll `InCompleteBlockCount(...) == 0`.

Hand-written neighbour checks do not rotate and cannot highlight what is missing. `HighlightIncompleteParts` gives the player free diagnostics. You must call InitForUse or every later call throws.

— `vssurvivalmod/BlockEntity/BEBeeHiveKiln.cs:76 + 126 + 256 (`StructureComplete = structure.InCompleteBlockCount(Api.World, Pos) == 0`); vsapi/Common/MultiblockStructure.cs:52` · vssurvivalmod

### Put the temperature on the ITEMSTACK, not on the machine, and read/write it through `Collectible.GetTemperature`/`SetTemperature`.

Cooling is lazy and free — no ticking required — and the heat then follows the item into the player's inventory, chests and the ground. Every vanilla heat machine (firepit, forge, bloomery, molds, crucible) does this.

— `vssurvivalmod/BlockEntity/BEForge.cs:221-226 and vssurvivalmod/BlockEntity/BEBloomery.cs:207 (`OutStack.Collectible.SetTemperature(Api.World, OutSlot.Itemstack, 900, true)`)` · vssurvivalmod

### Override `GetDropsForHandbook` with `GetHandbookDropsFromBreakDrops(...)` whenever `GetDrops` is computed from the block entity.

Otherwise the handbook shows the static JSON `drops` array, which for a dynamic block is empty or wrong. Vanilla does this on the bloomery, the smelted container and both molds.

— `vssurvivalmod/Block/BlockBloomery.cs:177-180, vssurvivalmod/Block/BlockSmeltedContainer.cs:369-372, vssurvivalmod/Block/BlockToolMold.cs:340-343` · vssurvivalmod

### Give a dual-form (held stack / placed block) machine ONE Block class with paired `(ItemStack)` and `(BlockPos)` overloads of every accessor.

It keeps the two representations from drifting and means a single `ILiquidSink` implementation serves the bucket in your hand and the barrel on the floor.

— `vssurvivalmod/Systems/Liquid/BlockLiquidContainerBase.cs:253/262 (GetCurrentLitres), 353/370 (SetContent), 388/401 (GetContent), 432/456 (TryTakeContent), 496/561 (TryPutLiquid)` · vssurvivalmod

### Resolve cross-cutting contracts through `block.GetInterface<T>(world, pos)` / `collectible.GetCollectibleInterface<T>()` instead of a direct cast.

The helper searches Block -> BlockBehavior -> BlockEntity -> BlockEntityBehavior in order, so a contract can be satisfied by a behavior later without touching call sites. A direct cast silently stops working the moment someone moves the implementation into a behavior.

— `vsapi/Common/Collectible/Block/Block.cs:2706 and vsapi/Common/Collectible/Collectible.cs:3622; used at vssurvivalmod/BlockEntity/BEAnvil.cs:154 and Systems/Handbook/CollectibleBehaviorHandbookTextAndExtraInfo.cs:137` · vssurvivalmod

### Dispose renderers and looping sounds in BOTH `OnBlockRemoved` and `OnBlockUnloaded`.

Chunk unload does not call OnBlockRemoved. Miss one and you leak a renderer/ILoadedSound per machine per session.

— `vssurvivalmod/BlockEntity/BEBloomery.cs:349-361, vssurvivalmod/BlockEntity/BEForge.cs:364-377, vssurvivalmod/BlockEntity/BEIngotMold.cs:517 + 787` · vssurvivalmod

### Read all tuning numbers from `Block.Attributes` / `ItemAttributes` sub-objects rather than hardcoding per-material constants.

One class then serves every variant, and a patch mod can retune you without a code change. Vanilla scopes them under a machine-named key.

— `vssurvivalmod/BlockEntity/BEForge.cs:42 + 51 (`ItemAttributes["inForge"]["durationMul"]`, `["tempGainDeg"]`), BEBloomery.cs:426 (`ItemAttributes["bloomeryFuelRatio"]`), BEToolMold.cs:57 (`Block.Attributes["requiredUnits"]`)` · vssurvivalmod

### Add handbook prose through lang keys `<domain>:<block|item>-handbooktitle-<code>` and `-handbooktext-<code>` rather than JSON `handbook.extraSections`.

It is picked up automatically with no asset changes, is fully translatable, and supports VTML. The JSON form exists but duplicates strings into the asset.

— `vssurvivalmod/Systems/Handbook/CollectibleBehaviorHandbookTextAndExtraInfo.cs:2414-2435` · vssurvivalmod

### Guard recursion in any block that forwards calls to a neighbour with an explicit `if (block is <MyForwarderType>) return;`.

Corrupt or partially-removed structures produce two forwarders pointing at each other, which stack-overflows the server. BlockMultiblock has this guard on literally every override.

— `vsessentialsmod/Block/BlockMultiblock.cs:91, 109, 278, 371, 481, 491` · vssurvivalmod

### Version-migrate saves inside `FromTreeAttributes` by detecting the absence of the new key.

It is the only hook that sees old data before anything else touches it, and it costs one null check.

— `vssurvivalmod/BlockEntity/BEIngotMold.cs:721-730 (pre-1.21 molds), vssurvivalmod/BlockEntity/BEForge.cs:403-412 (pre-1.22 forges), Systems/Microblock/BEMicroBlock.cs:1139-1143 (1.15.0-1.15.5 missing `sideAlmostSolid`)` · vssurvivalmod

### Drive incandescent glow from temperature with `ColorUtil.GetIncandescenceColorAsColor4f((int)T)` plus `extraGlow = Clamp((T-550)/1.5f, 0, 255)`, and set `prog.TempGlowMode = 1`.

It matches every other hot thing in the game; rolling your own curve makes your metal look wrong next to a vanilla ingot.

— `vssurvivalmod/BlockEntityRenderer/IngotMoldRenderer.cs:92-98` · vssurvivalmod

### Register your recipe list with `api.RegisterRecipeRegistry<RecipeRegistryGeneric<T>>(name)` in `ModSystem.Start` on BOTH sides, but load assets only server-side in `AssetsLoaded`.

The engine serializes the registry to the client on connect for free; if you only register on the server the client has an empty list and every handbook/GUI lookup fails.

— `vssurvivalmod/Systems/Cooking/RecipeRegistrySystem.cs:166-175 vs AssetsLoaded l.178-180 (`if (!(api is ICoreServerAPI sapi)) return;`)` · vssurvivalmod

### Drive a block-entity animation by mutating the *existing* AnimationMetaData in `activeAnimationsByAnimCode`, never by restarting the animation.

`AnimationUtil.StartAnimation` returns false and leaves the old meta in place when the code is already active, so a 'stop then start with new speed' round-trip is the only alternative — and that resets `CurrentFrame` to 0 (AnimatorBase.AnimNowActive :269), visibly snapping the machine back to phase zero every time the network speed changes.

— `vsapi/Common/Model/Animation/AnimationUtil.cs:105-116 ; vssurvivalmod/Lore/ResoArchives/BEBehaviorControlPointAnimatable.cs:100` · vsessentialsmod

### Use `AnimManager.RegisterFrameCallback(new AnimFrameCallback{Animation, Frame, Callback})` to run logic at a specific animation frame, and store the frame number in the anim meta's `Attributes` divided by `AnimationSpeed`.

It is the only phase-accurate hook; polling `CurrentFrame` from a game tick listener aliases badly because the animation advances on render frames, not ticks. Vanilla's melee-attack damage timing is built entirely on this. Note the trigger is one-shot: `runTriggers` removes the entry after firing, so re-register per swing/cycle.

— `vsessentialsmod/CollectibleBehavior/BehaviorAnimationAuthoritative.cs:31-53, :98-110 ; vsapi/Common/Model/Animation/AnimationManager.cs:498-523` · vsessentialsmod

### Toggle the static chunk mesh off by returning true from `OnTesselation` while animating, and flip `renderer.ShouldRender` inside the `MarkBlockDirty(pos, callback)` retesselation callback.

If you set ShouldRender outside the callback the animated renderer and the static chunk mesh are both visible (or neither is) for one to several frames while the chunk re-tesselates — a visible double-image or flicker on every start/stop.

— `vsessentialsmod/BlockEntityBehavior/BEBehaviorAnimatable.cs:51-54 ; vsessentialsmod/BlockEntityBehavior/BlockEntityAnimationUtil.cs:96-109` · vsessentialsmod

### Create looping `ILoadedSound`s once, with `ShouldLoop=true` and `DisposeOnFinish=false`; gate `Start()` behind a rising-edge boolean latch, modulate with `SetVolume`/`SetPitch` every update, `Stop()` below a small threshold, and `Dispose()` in the owner's Dispose.

`Start()` on an already-playing sound is not free and `DisposeOnFinish=true` on a looping sound is a use-after-free waiting to happen. Without the latch you re-Start every tick; without the threshold Stop you leave inaudible sounds occupying OpenAL sources.

— `vsessentialsmod/Systems/Weather/WeatherSimulationSound.cs:58-134 (create), :262-270 (latch), :306-317 (stop), :354-377 (dispose) ; vsapi/Client/Audio/SoundParams.cs:41` · vsessentialsmod

### Wait for `ILoadedSound.IsReady` on every sound in a group before touching any of them.

Sounds are decoded asynchronously; calling SetVolume/Start on a not-yet-ready sound is silently dropped, so a machine that starts on world join can come up permanently silent. Vanilla guards the whole weather sound group with a single `soundsReady` flag.

— `vsessentialsmod/Systems/Weather/WeatherSimulationSound.cs:170-186` · vsessentialsmod

### Bulk placement order is: `schematic.Init(ba)` → `Place(...)` → `PlaceDecors(...)` → `ba.Commit()` → `PlaceEntitiesAndBlockEntities(...)` → `ba.CommitBlockEntityData()`.

Block entities do not exist until the block commit has run, so calling PlaceEntitiesAndBlockEntities before Commit silently drops every BE tree. `BlockSchematic.Place` itself skips the BE step whenever the accessor is an `IBlockAccessorRevertable` precisely because the caller must do it (BlockSchematic.cs:706-709).

— `vscreativemod/Tool/ImportTool.cs:311-325 ; vscreativemod/Workspace.cs:800-813 ; vsapi/Common/Collectible/Block/BlockSchematic.cs:706-709` · vsessentialsmod

### When moving a block entity to a new position, re-write `posx`/`posy`/`posz` into its tree before `FromTreeAttributes` — and use `Pos.InternalY`, not `Pos.Y`, for `posy`.

`BlockEntity.FromTreeAttributes` reads its position out of the tree. Skipping the rewrite gives you a BE that believes it lives at the old coordinates; using `Y` instead of `InternalY` breaks any non-normal dimension (mini-dimension previews, in particular).

— `vscreativemod/Workspace.cs:735-767 ; vsapi/Common/Collectible/Block/BlockSchematic.cs:1152-1154` · vsessentialsmod

### Implement `IRotatable.OnTransformed(world, tree, degreeRotation, oldBlockIdMapping, oldItemIdMapping, flipAxis)` on any block entity whose state encodes a direction; on `BlockEntityGeneric` it fans out to behaviors automatically.

`BlockSchematic.TransformWhilePacked` is the only place rotation reaches BE data. A BE that does not implement IRotatable gets its blocks rotated but its stored facing/orientation left untouched — schematic-pasted or worldedit-rotated machines come out internally inconsistent.

— `vsapi/Common/Collectible/Block/BlockSchematic.cs:945-960 ; vsessentialsmod/BlockEntity/BEGeneric.cs:12-23` · vsessentialsmod

### Generate multiblock offset tables with `/we generate-multiblock-code` (aliases `gmc`, legacy `mgencode`) rather than hand-writing them: mark the area, look at the controller block, run the command, read `server-main.log`.

Hand-written offsets are relative to a centre you have to keep in your head, and the block-number table must be consistent. The generator emits both. Caveat: air blocks are skipped, so 'this cell must be empty' constraints have to be added by hand.

— `vscreativemod/WorldEdit.cs:427-464 ; vscreativemod/WorldEditCommands.cs:155-158 ; vsapi/Common/MultiblockStructure.cs:24-27` · vsessentialsmod

### Always write an explicit `"side"` on a patch whose target file is in a server-only asset category (blocktypes, itemtypes, recipes, worldgen, entities). Vanilla does this on every single patch it ships.

`JsonPatch.Side` defaults to Universal (JsonPatchLoader.cs:166), so an omitted side does NOT get inferred from the file's category. The patch then also runs client-side, where `api.Assets.TryGet` returns null for those categories, incrementing notfoundCount and logging a VerboseDebug line. The code comment at :628 says this outright: mods leaving out a side "would cause significant log spam". Our assets/smex/patches/compat/em/nugget-crushing.json already gets this right; assets/smex/patches/compat/em/disable-crushing.json does NOT set a side and patches em:recipes/grid/crushing.json — a server-only category.

— `.game/1.21/assets/survival/patches/keepinventory.json (every entry ends with "side": "Server"); mechanism at vsessentialsmod/Loading/JsonPatchLoader.cs:254-255 and :495-516` · vsmodexamples

### Append to a JSON array with `"path": ".../-"`, never with a hard-coded numeric index.

A numeric last token makes AddReplace call JArray.Insert(i, value) (JsonNetTargetAdapter.cs:77-78). If any other mod (or a vanilla update) changed the array's length, index i is either the wrong slot or out of range → ArgumentOutOfRangeException and a failed patch. Vanilla exclusively uses `/behaviors/-`, `/client/behaviors/-`, `/server/behaviors/-`. The official example mod does the opposite and hard-codes `/client/behaviors/12` and `/server/behaviors/13` — do not copy that.

— `good: .game/1.21/assets/survival/patches/*.json (`"path": "/server/behaviors/-"`); bad: vsmodexamples/Code Mods/VSTutorial - 8 - Entity Behavior & Server To Client Data Tutorial/vscodetutorial-entitybehavior/VSTutorial/assets/vstutorial/patches/game-entities-humanoid-player.json:4` · vsmodexamples

### Prefer `op: addmerge` over `op: add` when the target key may already exist and hold an object or array.

In VS, `add` is wired to AddReplaceOperation, which OVERWRITES `token[key] = value` wholesale — it silently discards whatever another mod put there first. `addmerge` merges into an existing JObject or concatenates into an existing JArray. The enum's own XML doc recommends it "for improved mod compatability".

— `vsessentialsmod/Loading/JsonPatchLoader.cs:28 and :532 vs :571; behaviour at Tavis.JsonPatch/src/JsonPatch/Adaptors/JsonNetTargetAdapter.cs:73-82 vs :39-71` · vsmodexamples

### Gate compat patches with `dependsOn: [{modid, invert}]` rather than shipping two mod versions or branching in C#.

It is the only supported way to make one patch file react to another mod's presence, it is evaluated before the file is even opened, and it costs nothing at runtime. All entries in the array are ANDed (`loaded ^ invert`), so a single patch can require mod A and require the absence of mod B. There is no version comparison available — only presence.

— `vsessentialsmod/Loading/JsonPatchLoader.cs:277-295; our own use at assets/smex/patches/compat/em/nugget-crushing.json` · vsmodexamples

### Address a mod by its `modinfo.json` identifier, not by numeric ids, wherever an endpoint accepts it — `/api/mod/{modIdStr}` and `/api/updates?mods=id@ver` and `/api/v2/mods/install-information?ids=id@ver` all key on `modReleases.identifier`.

modId and assetId are database surrogate keys that appear nowhere in our repo; the identifier is the one string we control from `modinfo.json`. Hard-coding 9254/53606 into a script silently breaks if a mod is ever re-created, and gives no signal when it does.

— `vsmoddb/lib/api/v1/functions.php:8` · vsmoddb

### Use `/download/{fileId}/{name}` (what `formatDownloadTrackingUrl` builds and what v2 returns as `fileUrl`) when you actually want the file; use the `?dl=` CDN url from v1 `mainfile` only when you deliberately want an untracked fetch.

`/download/…` increments `files.downloads` and `mods.downloads` (deduped per IP per 24 h) and 410s on retracted releases. The v1 `mainfile` url points straight at Bunny and bypasses both — scripting against it silently under-reports our own download stats and happily serves a retracted build.

— `vsmoddb/download.php:19` · vsmoddb

### Send array query params with PHP bracket syntax — `tagids[]=4&tagids[]=7`, `gameversions[]=1.22.6` — url-encoded as `%5B%5D`.

`listMods` does `foreach ($_GET["tagids"] as …)`. A scalar `tagids=4` is not an array, PHP 8 emits a warning and skips the loop, and the endpoint returns 200 with the filter silently ignored. Verified live: `?text=Steelmaking&tagids=4` returns unfiltered results.

— `vsmoddb/lib/api/v1/functions.php:172` · vsmoddb

### Treat v1 and v2 as two different protocols: v1 = always HTTP 200, read `body.statuscode`; v2 = real HTTP status, no `statuscode` field.

A client that checks `response.ok` will treat every v1 404/400 as success and then explode on a missing key. A client that parses `statuscode` from v2 will always read undefined.

— `vsmoddb/lib/api/v1/entry.php:8` · vsmoddb

### Never derive game-version compatibility from `modinfo.json` `dependencies` when reading the API — read `compatibleGameVersions` (v2) or `release.tags` (v1) instead.

The `game@X` dependency only pre-ticks checkboxes on the upload form at publish time; the author is free to change them and nothing re-syncs afterwards. The stored compat list is the truth ModDB and the launcher use.

— `vsmoddb/edit-release.php:308` · vsmoddb

### When you need per-release download counts, use v1 `/api/mod/{id}`; v2 does not expose them at all.

v2's release endpoints return only releaseId/identifier/version/compat/created/file. The download number per release lives solely in the v1 detail payload (`releases[].downloads`, from `files.downloads`).

— `vsmoddb/lib/api/v1/functions.php:68` · vsmoddb

### Ask for comments by assetId, and expect raw sanitised HTML with no author name.

`/api/comments/{x}` filters on `comments.assetId`, which is the MOD's asset, so passing a modId returns someone else's comments or nothing. The payload has `userid` only — resolving it to a name needs a second call to `/api/authors`, and the `text` is HTML (`<p>…</p>`, embedded `<img>`, spoiler divs), not plain text.

— `vsmoddb/lib/api/v1/logic.php:59` · vsmoddb


## Medium relevance

### Ship a `<domain>:blockdesc-<path>` lang entry rather than appending static prose in GetBlockInfo.

`GetPlacedBlockInfo` already looks up `Code.Domain + ":" + itemclass + "desc-" + Code.Path` via `Lang.GetMatching` and appends it when it resolves. Hand-appending the same text in the BE duplicates it.

— `vsapi/Common/Collectible/Block/Block.cs:2295-2299` · vsapi

### Prefer `Shape.TryGet(api, assetLocation)` and null-check, rather than `api.Assets.Get(...).ToObject<Shape>()`.

TryGet catches and logs deserialization exceptions and returns null; the raw path throws inside a tesselation thread where the exception is much harder to attribute. It also sets `ShapeElement.locationForLogging` so element-level warnings name the file.

— `vsapi/Common/Model/Shape/Shape.cs:512-546` · vsapi

### When a BlockBehavior overrides `GetPlacedBlockInteractionHelp`, also override `GetPlacedBlockInteractionHelpCount` to return exactly the same length, cheaply.

Block.cs pre-sizes an uninitialized array from the sum of the Count calls, then writes via `InsertAt` whose return value is discarded. See the gotcha entry — a mismatch either drops entries or leaves nulls in the array.

— `vsapi/Common/Collectible/Block/BlockBehavior.cs:262-268, vsapi/Common/Collectible/Block/Block.cs:2195-2237` · vsapi

### Guard Harmony patching with `Harmony.HasAnyPatches(Mod.Info.ModID)`, patch by category, and `UnpatchAll(Mod.Info.ModID)` in `Dispose()`.

In singleplayer the client and server ModSystems live in ONE process, so `Start()` runs twice and the patches would be applied twice. And without UnpatchAll, patches survive leaving a world — a later world loads with the mod disabled but still patched. The official example spells out both reasons in comments.

— `.compat/vintagestory/vsmodexamples/Code Mods/VSTutorial - 7 - Basic Harmony Patching/vscodetutorial-harmonypatching/VSTutorial/VSTutorialModSystem.cs:24-40` · vsapi

### Use `Mod.Logger` (the per-mod logger injected into every ModSystem) rather than `api.Logger`.

`Mod.Logger` is a distinct ILogger instance per mod, so lines are attributable in server-main.log. `api.Logger` is the shared engine logger.

— `vsapi/Common/Assets/Mod.cs:39` · vsapi

### Cache derived AllowedOrientations/facing arrays as properties on the Block, and use `AssetLocation.PermanentClone()` for AssetLocations held for the lifetime of the game.

PermanentClone de-duplicates the domain and path strings, trading a little CPU at load for a permanent RAM saving — vanilla's stated rule is 'use for objects expected to be held for a long time - for example, building Blocks at game launch'. Ordinary Clone() keeps the JSON-parsed string instances alive forever.

— `vsapi/Common/Assets/AssetLocation.cs:432-440` · vsapi

### Reach for `ObjectCacheUtil.GetOrCreate(api, key, factory)` for anything expensive and per-block-type (meshes, animators, pre-computed tables).

It is the vanilla idiom (AnimationUtil uses it for its animator cache) and it keeps the object alive exactly as long as the sided API. Note the cache is per-side, so a singleplayer client and server hold separate copies — never store shared mutable simulation state there.

— `vsapi/Util/ObjectCacheUtil.cs:19-32 and vsapi/Common/Model/Animation/AnimationUtil.cs:139-145` · vsapi

### Prefer `sapi.Event.ServerRunPhase(EnumServerRunPhase.X, handler)` / the ModSystem lifecycle over ad-hoc 'first tick' flags.

The phase enum is the engine's own ordering contract (assets ready at 3, finalize at 4, mods+config at 5, all blocks registered at 6, world ready at 7), and the obsolete aliases show which names moved — hand-rolled sequencing drifts across versions.

— `vsapi/Server/EnumServerRunPhase.cs and vsapi/Server/API/IServerAPI.cs:38` · vsapi

### Rendering is registered imperatively (`manager.AddDeviceForRender(this)` in `Initialize`, `RemoveDeviceForRender` in `OnBlockRemoved` **and** `OnBlockUnloaded`) and keyed on the `CompositeShape` object; changing `Shape` at runtime goes through a property setter that de-registers and re-registers.

Forgetting the `OnBlockUnloaded` removal leaks a device that renders at a position with no block entity. The `Shape` setter's guard (`prev != null && manager != null && prev != value`) means the very first assignment in `Initialize` deliberately does *not* register — the explicit `AddDeviceForRender` on the next line does.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPBase.cs:79-96,166,289-306` · vssurvivalmod

### Invisible "filler" blocks for multiblocks (`BlockMPMultiblockGear`, `BlockMPMultiblockPulverizer`) each carry a `BEMPMultiblock` whose only state is `Principal` (the centre `BlockPos`), and every player-facing method — `OnGettingBroken`, `OnBlockBroken`, `GetParticleBreakBox`, `GetRandomColor`, `OnPickBlock`, `OnBlockInteractStart` — forwards to the principal block.

Miss one and the filler behaves like a separate block: unbreakable, black break particles, or an invalid picked itemstack. Note `GetPlacedBlockName` deliberately calls `base.OnPickBlock` to avoid recursing into the forwarding override, and `OnBlockBroken` uses `Principal == null` as the sentinel for "being removed by game code, break normally".

— `vssurvivalmod/Systems/MechanicalPower/Block/BlockMPMultiblockGear.cs:72-172 and BlockEntity/BEMultiblock.cs:24-49` · vssurvivalmod

### `HasMechPowerConnectorAt` receives the *requesting* block (`BlockMPBase forBlock`), letting a block accept a connection only from its own kind.

`BlockSpurGear` uses it to allow lateral meshing (`face == Orientation || (forBlock == this && face != Orientation.Opposite)`) without accepting a random axle on its side. Ignoring the parameter is the easy way to build a block that connects to things it shouldn't.

— `vssurvivalmod/Systems/MechanicalPower/Block/BlockSpurGear.cs:20-23` · vssurvivalmod

### Container GUIs: server never opens a dialog. Client calls `toggleInventoryDialogClient(player, () => new MyDialog(...))`; the server side only reacts to `EnumBlockEntityPacketId.Open`/`Close` (1000/1001) and routes everything below 1000 straight into `Inventory.InvNetworkUtil.HandleClientPacket` after a permission check.

Bypassing `InvNetworkUtil` means no rollback on a rejected move and no chunk `MarkModified()`, so the change is lost on reload. The permission failure path calls `SendInventoryRollback` rather than just dropping the packet.

— `vssurvivalmod/BlockEntity/BEOpenableContainer.cs:118-191` · vssurvivalmod

### Handle client GUI packets with the `packetid < 1000` -> inventory / `>= 1000` -> custom convention, and gate every mutating packet behind `new BlockEntity.CachedAccessPerms(world, Pos, player).IsInteractingPlayerAllowedTo(EnumBlockAccessFlags.Use, true, "name")`.

Without the perms check any client can drive your machine from across the map through a land claim. Vanilla rolls the inventory back on failure rather than dropping the packet silently.

— `vssurvivalmod/Systems/Barrel/BEBarrel.cs:304-355 and vssurvivalmod/BlockEntity/BEOpenableContainer.cs:139-180` · vssurvivalmod

### Write an audit log line for every player-visible item movement in or out of a machine.

It is the only way to diagnose "the server ate my steel" reports, and vanilla does it uniformly with `Api.World.Logger.Audit("{0} Took 1x{1} from X at {2}.", …)`.

— `vssurvivalmod/BlockEntity/BEForge.cs:281-285 + 323-327, vssurvivalmod/BlockEntity/BEIngotMold.cs:332-336` · vssurvivalmod

### Use a re-entrancy flag when a slot-modified handler mutates the same inventory.

`FindMatchingRecipe` crafts inside `Inventory_SlotModified`, which would re-enter itself forever without the flag.

— `vssurvivalmod/Systems/Barrel/BEBarrel.cs:119-136 + 165/191 (`ignoreChange`)` · vssurvivalmod

### Mutate a shared static `SimpleParticleProperties` immediately before each `SpawnParticles` call rather than allocating.

This is what vanilla does everywhere, so you must assume any particle props object you hand out can be mutated by someone else between frames — and you must set every field you care about on every spawn.

— `vssurvivalmod/BlockEntity/BEBloomery.cs:220-243 (sets MinPos/VertexFlags/MinVelocity/AddVelocity each time), vssurvivalmod/Block/BlockSmeltedContainer.cs:196-231` · vssurvivalmod

### Cache generated meshes in `ObjectCacheUtil.GetOrCreate(Api, key, factory)` keyed by block code + state.

Every BE otherwise re-tesselates the same shape; the firepit keys on `burnstate + "-" + contentstate`, the mold on the block code.

— `vssurvivalmod/BlockEntity/Firepit/BEFirepit.cs:967-984, vssurvivalmod/BlockEntity/BEIngotMold.cs:622-649` · vssurvivalmod

### For a sound attached to a moving thing, call `sound.SetPosition(x,y,z)` every tick and end it with `FadeOut(seconds, s => s.Dispose())` rather than `Stop()`.

A looping sound created with a Position is not re-positioned automatically; and an abrupt Stop on a mechanical loop is audibly wrong. The dispose-in-the-fade-callback idiom is how vanilla avoids leaking the handle.

— `vsessentialsmod/Entities/EntityBlockFalling.cs:466-477, :530-545` · vsessentialsmod

### Preview a structure with a mini-dimension (`ba.CreateMiniDimension` + `sapi.Server.LoadMiniDimension` + `SetSubDimensionId` + `PasteToMiniDimension`) instead of placing ghost blocks in the world.

Mini-dimension previews are per-player, cost nothing in the real world, need no cleanup pass, and survive undo. Vanilla explicitly returns early from `PlaceEntitiesAndBlockEntities` when the accessor is an `IMiniDimension` so entities never spawn into the preview.

— `vscreativemod/Workspace.cs:883-918 ; vsapi/Common/Collectible/Block/BlockSchematic.cs:1167, :1516-1522` · vsessentialsmod

### Register a per-player highlight in a *distinct* `EnumHighlightSlot`, and clear it by highlighting an empty list.

Highlights are keyed by (player, slot int) and overwrite each other. Vanilla reserves 0-6 by enum and MultiblockStructure squats on 23; picking a colliding number makes your highlight disappear whenever the player opens a worldedit selection.

— `vsapi/Common/API/IWorldAccessor.cs:11-20, :697 ; vsapi/Common/MultiblockStructure.cs:30, :132-135 ; vscreativemod/Workspace.cs:434-454` · vsessentialsmod

### Spawn ambient/high-volume particles from `capi.Event.RegisterAsyncParticleSpawner(handler)` and read blocks through the supplied `manager.BlockAccess`, not the world accessor.

The async spawner runs off-thread; the manager hands you a thread-safe block accessor. Touching `capi.World.BlockAccessor` from that callback is a data race. The trade-off documented in the API is that async particles are not exactly in sync with player interactions, so keep them ambient.

— `vsessentialsmod/Systems/ParticleEntity/AmbientParticles.cs:29, :102-146 ; vsapi/Client/API/IClientEventAPI.cs:249-253` · vsessentialsmod

### Ship optional cross-mod content under `assets/<yourdomain>/compatibility/<othermodid>/...` — it is auto-remapped onto `<othermodid>:...` only when that mod is loaded.

It avoids the usual `if (api.ModLoader.IsModEnabled(...))` dance and json-patch guards, and it runs before json patching so patches can target the remapped assets.

— `vsessentialsmod/IntermodTools.cs:20, :33-66` · vsessentialsmod

### Catch up on missed in-game time by looping in fixed increments from a persisted `lastCheckAtTotalDays`, clamping it to `min(stored, now)` first.

A single `deltaHours` step gives wrong results for any non-linear process, and an imported/older world can hand you a *future* timestamp which then never catches up. BETransient does both guards in six lines.

— `vsessentialsmod/BlockEntity/BETransient.cs:117-130` · vsessentialsmod

### Build multi-level chat commands with the fluent `ChatCommands.GetOrCreate(root).BeginSub(...).WithDesc(...).WithAlias(...).WithArgs(parsers...).RequiresPrivilege(...).HandleWith(...).EndSub()` builder, and put legacy aliases behind a world-config flag.

It gets you autocompletion, per-sub privileges, argument parsing and generated help for free; `GetOrCreate` means several ModSystems can extend the same root. The legacy-alias-behind-a-flag trick is how worldedit renamed 20 commands without breaking muscle memory.

— `vscreativemod/WorldEditCommands.cs:24-160, :330-341` · vsessentialsmod

### Order `remove` operations on the same array back-to-front within a patch file.

Remove on an array element calls token.Remove() (JsonNetTargetAdapter.cs:127-129), which shifts every later index down. Two forward-ordered removes on `/recipes/2` and `/recipes/3` delete elements 2 and 4. Patches within one ApplyPatches run share a cached JToken, so the second op genuinely sees the first op's result.

— `Tavis.JsonPatch/src/JsonPatch/Adaptors/JsonNetTargetAdapter.cs:123-134; caching at vsessentialsmod/Loading/JsonPatchLoader.cs:188,430-458` · vsmodexamples

### Namespace every registration with `Mod.Info.ModID + "."` — block classes, block-entity classes, behaviors, entity behaviors, network channels.

Registration keys are a single flat global namespace across all loaded mods. A bare "moveable" or "networkchannel" collides with any other mod that picked the same word, and the failure surfaces as the wrong class being instantiated rather than as an error. Every current tutorial does this; the old_code_mods (`RegisterBlockClass("BlockCustomShape", …)`, `RegisterChannel("networkapitest")`) predate the convention.

— `vsmodexamples/Code Mods/VSTutorial - 1 - Simple Block Tutorial/vscodetutorial-simpleblock/VSTutorial/VSTutorialModSystem.cs:23; tutorial 4 line 30; tutorial 8 line 32; tutorial 9 line 23` · vsmodexamples

### Harmony: guard with `Harmony.HasAnyPatches(Mod.Info.ModID)`, use `[HarmonyPatchCategory(modid)]` + `patcher.PatchCategory(modid)`, and `UnpatchAll(modid)` in `Dispose()`.

Client and server run in one process in singleplayer, so Start(ICoreAPI) fires twice and the patches would be applied twice without the guard. Without UnpatchAll in Dispose, patches survive a world exit and stay active even after the mod is disabled — the tutorial's comment says exactly this.

— `vsmodexamples/Code Mods/VSTutorial - 7 - Basic Harmony Patching/vscodetutorial-harmonypatching/VSTutorial/VSTutorialModSystem.cs:22-41; category attribute at Patches/TutorialPatches.cs:20` · vsmodexamples

### Server→client state sync goes through `entity.WatchedAttributes` sub-trees plus `MarkPathDirty(treeName)`; a BlockEntity's equivalent is To/FromTreeAttributes.

The tutorial documents the contract precisely: a watched attribute is readable on both sides, writable only from the server, and is saved to disk AND pushed to clients the moment the path is marked dirty. Forgetting MarkPathDirty gives a value that is correct on the server and stale on the client — the same failure class as our own 'a readout field missing from ToTreeAttributes reads zero on the client'.

— `vsmodexamples/Code Mods/VSTutorial - 8 - Entity Behavior & Server To Client Data Tutorial/vscodetutorial-entitybehavior/VSTutorial/EntityBehaviors/EntityBehaviorTotalPlayTime.cs:26-51` · vsmodexamples

### A BlockEntity's RegisterGameTickListener is auto-disposed; an EntityBehavior's world.RegisterGameTickListener is NOT — store the id and UnregisterGameTickListener in OnEntityDespawn.

The two tutorials state the asymmetry explicitly. BlockEntityTicking.cs:29 says "there is no need to dispose of it yourself in a BlockEntity"; EntityBehaviorTotalPlayTime.cs:17-19 keeps `listenerId` precisely because the entity path leaks otherwise.

— `vsmodexamples/Code Mods/VSTutorial - 5 - Block Entity Tutorial/vscodetutorial-blockentity/VSTutorial/BlockEntities/BlockEntityTicking.cs:31 vs VSTutorial - 8 …/EntityBehaviors/EntityBehaviorTotalPlayTime.cs:75,101-105` · vsmodexamples

### Split a networked feature into three ModSystems (shared registration, client, server) and bump the dependents' ExecuteOrder above 0.1.

RegisterChannel must happen in a Start(ICoreAPI) that runs before any GetChannel. The default ExecuteOrder is 0.1 for every ModSystem and ties are not deterministic, so the client/server systems return 0.11 to guarantee the channel exists. ShouldLoad(forSide) then keeps each half off the wrong side entirely.

— `vsmodexamples/Code Mods/VSTutorial - 9 - Specific Networking Tutorial/VSTutorial/Networking/VSTutorialClientSystem.cs:29-42 and VSTutorialServerSystem.cs:23-26` · vsmodexamples

### Read a BlockBehavior's tunables out of `Initialize(JsonObject properties)` with typed defaults, and remember a behavior instance is per-blocktype, not per-placed-block.

`properties["distance"].AsInt(1)` gives JSON-driven configuration with a code-side fallback, so one behavior class serves many blocktypes. The class doc also warns that a fresh BlockBehavior is constructed for each blocktype INCLUDING each variant, and that behaviors cannot hold per-position state — that needs a BlockEntity.

— `vsmodexamples/Code Mods/VSTutorial - 4 - Block Behavior Tutorial/vscodetutorial-blockbehavior/VSTutorial/BlockBehaviors/BlockBehaviorMoveable.cs:8-13,33-41; JSON side at assets/vstutorial/blocktypes/moving.json:4-12` · vsmodexamples

### Reference game assemblies with `<Private>false</Private>` and a `$(VINTAGE_STORY)` HintPath; set `AppendTargetFrameworkToOutputPath=false` and point OutputPath at `bin\$(Configuration)\Mods\mod`.

Private=false stops VintagestoryAPI.dll and friends being copied into the mod folder, which would ship a duplicate assembly and can break loading. Without AppendTargetFrameworkToOutputPath=false the build inserts a `net8.0/` segment and `--addModPath bin/Debug/Mods` no longer finds the mod. Our own tree still shows the stale form: src/*/bin/Debug/net8.0/Mods/mod/… exists alongside src/SteelmakingExpanded/bin/Debug/Mods/mod/….

— `VSdotnetModTemplates/VSModTemplates/templates/VintageStoryMod/_ProjectName_/_ProjectName_.csproj:5-14` · vsmodexamples

### Launch for testing with `--tracelog --addModPath <bin>/Mods --addOrigin <project>/assets` and cwd = the game install.

--addModPath picks up the compiled dll without copying it to %appdata%; --addOrigin mounts the source assets folder directly so JSON/texture edits need no rebuild. This is the whole reason the template keeps `assets/` out of the csproj Content for the Cake variant. Both the VS launchSettings profiles and the VSCode launch configs use the identical argument set.

— `VSdotnetModTemplates/VSModTemplates/templates/VintageStoryMod/_ProjectName_/Properties/launchSettings.json:10,21 and .vscode/launch.json` · vsmodexamples

### Point modinfo.json at the published schema with `"$schema": "https://moddbcdn.vintagestory.at/schema/modinfo.latest.json"`.

Both the game (Newtonsoft ignores it) and modpeek (explicitly whitelisted at ParseJSON.cs:28) tolerate it, and it gives editor validation against the same rules ModDB enforces. Our src/*/modinfo.json already do this; the official template does too.

— `VSdotnetModTemplates/VSModTemplates/templates/VintageStoryMod/_ProjectName_/modinfo.json:2; whitelist at modpeek/LibModPeek/ParseJSON.cs:28-33` · vsmodexamples

### Cache API responses and back off on your own; do not poll.

There is no rate limiting of any kind — no 429, no nginx limit_req — which means there is also nothing telling you when you are being abusive. `/api/mods` is a single unpaginated 3.5 MB document (7 990 mods, verified 2026-08-10) and `/api/authors` with no `name` dumps the entire user table (the project's own test raises memory_limit to 10 GB to run it).

— `vsmoddb/tests/api-v1.php:201` · vsmoddb

### Encode versions the way `compileSemanticVersion` does when you need to sort or compare: `major<<48 | minor<<32 | patch<<16 | suffix`, with plain releases taking suffix 0xffff.

It makes a plain integer comparison agree with SemVer precedence including `rc > pre > dev`. Any string comparison or naive tuple sort gets pre-releases wrong, and `compilePrimaryVersion` deliberately uses suffix 0 so that `1.22` sorts before `1.22.0-dev.1` — masked comparison only works with that convention.

— `vsmoddb/lib/version.php:20` · vsmoddb

### Read `db/000_tables.sql` for schema questions and ignore `db/1xx_migrate.*`.

The migrations still refer to the pre-rename singular lowercase tables (`mod`, `release`, `file`, `follow`, `teammember`, `notification`) that no longer exist; `000_tables.sql` is the already-migrated current shape. Grepping the migrations for a column will hand you names that are wrong today.

— `vsmoddb/db/116_migrate.sql:20` · vsmoddb

### Send `Accept: application/json` (or no Accept header) on every request.

index.php short-circuits any GET whose Accept header contains neither `text/html` nor `application/json` and isn't `*/*`, returning the plain-text body `not an image` at HTTP 200. A client defaulting to `Accept: text/plain` gets a 200 with garbage and no error.

— `vsmoddb/index.php:12` · vsmoddb

### Assume no browser can call this API.

Not a single `Access-Control-Allow-*` header is emitted anywhere in the codebase or the nginx config, so any fetch from a page (including from an Artifact) is blocked by CORS. Server-side or CLI only.

— `vsmoddb/docker/moddb.conf:20` · vsmoddb


## Low relevance

### Give AI-style / priority-arbitrated systems two numbers — `Priority` and `PriorityForCancel` — and require `newTask.Priority > activeTask.PriorityForCancel` to preempt.

A single priority number makes equal-priority tasks thrash. Raising PriorityForCancel above Priority while running is how vanilla lets a task 'finish what it started'.

— `vsessentialsmod/Entity/AI/IAiTask.cs:24-29 ; vsessentialsmod/Entity/AI/AiTaskManager.cs:456` · vsessentialsmod

### Register a mod's own shader by cloning a stock one with a `PrefixCode` #define, then `capi.Shader.RegisterFileShaderProgram(name, prog)` and re-register on `capi.Event.ReloadShader`.

Without the ReloadShader hook your program is dead after any in-game shader reload (F-key or settings change) and the block renders black. Every renderer in vsessentials wires it the same way.

— `vsessentialsmod/Core.cs:50-66 ; vsessentialsmod/Systems/ParticleEntity/EntityParticleSystem.cs:178-195 ; vsessentialsmod/EntityRenderer/ModSystemFpHands.cs:20-42 ; vsessentialsmod/Systems/Weather/AuroraRenderer.cs:28-46` · vsessentialsmod

### Ship native binaries in a `native/` folder inside the mod and install a DllImportResolver keyed off `((ModContainer)Mod).FolderPath`, with the extension chosen from `RuntimeEnv.OS`.

There is no other supported way to P/Invoke from a mod — the default probing paths do not include the mod folder. The Cake script must also be taught to copy `native/` into the release, which the stock template does not do.

— `vsmodexamples/Code Mods/old_code_mods/NativeInterop/nativeInterop/NativeInteropModSystem.cs:15,27-40 and Readme.md` · vsmodexamples


---

# Gotchas

Surprising behaviour, found by reading the source. Each would cost hours to rediscover.


## vsapi — blocks, block entities, items

**`BlockEntity.FromTreeAttributes` runs BEFORE `Initialize`, so `Api` is null — and `MarkDirty()` silently returns when `Api == null`. Any MarkDirty/redraw issued from FromTreeAttributes is a no-op with no warning.**

The classic 'value is right on the server, stale/zero on the client until I break and replace the block' bug. Vanilla's own workaround is `BlockEntityDisplay.RedrawAfterReceivingTreeAttributes` (vssurvivalmod/BlockEntity/BEContainerDisplay.cs:168-175) which explicitly tests `Api != null` before marking dirty — meaning on the very first FromTreeAttributes of a chunk load, no redraw happens at all and the mesh must be built later in OnTesselation.

— `vsapi/Common/Collectible/Block/BlockEntity.cs:328 ("FromTreeAttributes is always called before Initialize() is called, so the this.api field is not yet set!"), :451-453 (`if (Api == null) return;`)`

**`BlockEntity.FromTreeAttributes` REBUILDS `Pos` from the tree's posx/posy/posz keys, overwriting whatever position the BE was created at.**

Feeding a BE a saved tree from a different position (recipe `blockEntityAttributes`, schematic import, copy/paste of a saved tree, a test fixture) teleports the BE's Pos to the old coordinates. Every subsequent `MarkDirty`, tick listener and BlockAccessor read then targets the wrong block. Note also that `posy` is written as `Pos.InternalY` (:313) and the 3-arg `BlockPos(x,y,z)` ctor decodes the dimension back out of y (vsapi/Math/BlockPos.cs:60-65) — so hand-built trees must use InternalY, not Y, or the BE lands in dimension 0.

— `vsapi/Common/Collectible/Block/BlockEntity.cs:333-337`

**`BlockEntity.RegisterGameTickListener` returns `0L` and registers NOTHING when `Dimensions.ShouldNotTick(Pos, Api)` is true — i.e. for any block in the WorldEdit blocks-preview mini-dimension.**

A machine placed in a preview/mini-dimension is completely frozen with no log line and a valid-looking handler id of 0. Code that treats a returned id of 0 as 'registered' and later calls UnregisterGameTickListener(0) is unregistering nothing. Any test or tool that stages blocks in dimension 1 will see zero ticks.

— `vsapi/Common/Collectible/Block/BlockEntity.cs:132 and :148; vsapi/Config/Dimensions.cs:52-60`

**`BlockEntity.ToTreeAttributes` early-returns when the block is missing on a non-client side, writing ONLY a cached raw tree — no posx/posy/posz, no behavior data, and none of your own overrides run.**

If a mod is removed and re-added, or a block code is renamed, `Block.IsMissing` is true and the BE round-trips a frozen snapshot. Your `ToTreeAttributes` override runs base first, base returns, and if you write your fields AFTER `base.ToTreeAttributes(tree)` you will corrupt the preserved missing-block tree; if you write them BEFORE, they are preserved but posx/y/z are not written by base. Neither is what you expect. Also `FromTreeAttributes` dereferences `Block.IsMissing` at :344 — so `Block` must be non-null, i.e. `CreateBehaviors` must have run first.

— `vsapi/Common/Collectible/Block/BlockEntity.cs:303-310, with the cache set at :344`

**`Block.OnBlockInteractStart` returns **false** by default, and the doc explicitly says a false return is NOT synced to the server.**

A block with a BE whose override does `return base.OnBlockInteractStart(...)` when it has no BlockBehaviors gets: interaction appears to do nothing, and — worse — the client never tells the server, so server-side handlers never fire and the desync is invisible in logs. Same trap on `OnBlockInteractStep` :1515 (base false = the multi-second use immediately aborts). By contrast `OnBlockInteractCancel` :1573 returns **true** by default. Also note the very first thing OnBlockInteractStart does is a claim check for `EnumBlockAccessFlags.Use` and returns false without running any behavior (:1440-1443).

— `vsapi/Common/Collectible/Block/Block.cs:1435 ("If you return false, the interaction will not be synced to the server.") and :1462 (`return false;`)`

**Since 1.21, `Block.OnBlockPlaced` spawns the BlockEntity BEFORE running BlockBehaviors, and a behavior's `PreventDefault`/`PreventSubsequent` no longer prevents BE creation — the reverse of the pre-1.21 contract.**

Any behavior written against 1.20 that set handling in OnBlockPlaced to suppress BE creation now gets a BE anyway. Mirror-image trap on removal: `Block.OnBlockRemoved` runs behaviors FIRST and only calls `RemoveBlockEntity` if nothing set PreventDefault (:1354-1368) — so a behavior that PreventDefaults on removal leaves an orphaned BlockEntity at a position where the block is gone.

— `vsapi/Common/Collectible/Block/Block.cs:1381-1395 (SpawnBlockEntity at :1383, behavior loop starts :1388); the change is documented in vsapi/Common/Collectible/Block/BlockBehavior.cs:337-338`

**`Block.GetPlacedBlockInteractionHelp` allocates an UNINITIALIZED array sized from `GetPlacedBlockInteractionHelpCount()` and then writes into it with `InsertAt`, DISCARDING InsertAt's return value.**

If a BlockBehavior's `GetPlacedBlockInteractionHelp` returns MORE entries than its `GetPlacedBlockInteractionHelpCount` promised, `InsertAt` allocates a bigger array, returns it, and the caller throws it away — those interactions vanish silently. If it returns FEWER, the tail of the array stays null and the interaction-help HUD gets nulls. The default `GetPlacedBlockInteractionHelpCount` (BlockBehavior.cs:265-268) just calls the real method, so overriding only one of the pair is the bug.

— `vsapi/Common/Collectible/Block/Block.cs:2195-2237 (alloc at :2207/:2224, `interactions.InsertAt(interactionsIndex, bhi);` at :2233), and vsapi/Util/ArrayExtensions.cs:345-357 (falls back to `Append` — a NEW array — when it would overflow)`

**`BlockGeneric`'s box-collecting overrides do `return allboxes.ToArray()` when `preventDefault` is set, but `allboxes` stays null if the behavior that set PreventDefault returned null boxes — a guaranteed NullReferenceException.**

A `StrongBlockBehavior` that legitimately wants 'no collision boxes at all' (set `handled = PreventDefault`, return null) crashes the physics/selection path. The only safe way to express it is to return `Array.Empty<Cuboidf>()`, not null.

— `vsapi/Common/Collectible/Block/BlockGeneric.cs:121-130 (GetCollisionBoxes), identically at :92-101 (GetParticleCollisionBoxes) and :151-160 (GetSelectionBoxes)`

**`StrongBlockBehavior`'s extra hooks (collision boxes, selection boxes, SideIsSolid, GetLightAbsorption, worldgen placement, decals, partial selection, colors) are dispatched ONLY by `BlockGeneric` or a subclass — a plain `Block` subclass ignores them entirely.**

Write a StrongBlockBehavior, attach it to your own `class BlockFoo : Block`, and none of those methods are ever called — no error, no log, the boxes simply stay the JSON defaults. Custom block classes that want behavior-driven geometry must derive from `BlockGeneric` or copy its dispatch loops.

— `vsapi/Common/Collectible/Block/StrongBlockBehavior.cs:10 ("Requires the block to use the GenericBlock block class (or inherit from it)"); the dispatch lives only in vsapi/Common/Collectible/Block/BlockGeneric.cs:109-166 etc.`

**`BlockEntity.OnExchanged` is the ONLY teardown-ish hook that does not fan out to `Behaviors`, and it neither re-runs `Initialize` nor re-creates behaviors.**

After `ExchangeBlock` (used for wrench rotation, snow cover, door open/close, block-state swaps) the BE keeps living with a NEW `Block` but every cached shape, renderer, animator, mesh and `Block.Attributes`-derived value inside the BE and inside its behaviors is stale. You must override OnExchanged and re-init those yourself — and a BlockEntityBehavior cannot even observe the event.

— `vsapi/Common/Collectible/Block/BlockEntity.cs:232-236 — the whole body is `if (block != this.Block) MarkDirty(true); this.Block = block;`; contrast `OnBlockRemoved` :220 and `OnBlockUnloaded` :275 which both loop Behaviors`

**`IBlockAccessor.MarkBlockEntityDirty` documents 'Client side call: No effect', but `BlockEntity.MarkDirty` documents 'When called on Client: Triggers a block changed event on the client'. The two doc comments contradict each other.**

Do not rely on client-side `MarkDirty(false)` doing anything at all — the accessor-level doc is the one describing the actual implementation. If you need a client-side visual update, you must pass `redrawOnClient: true`, which routes to `MarkBlockDirty` (:458) and that IS documented to redraw the chunk client-side.

— `vsapi/Common/API/IBlockAccessor.cs:576-580 vs vsapi/Common/Collectible/Block/BlockEntity.cs:446-451`

**`BlockEntity.UnregisterAllTickListeners` unregisters every id but never CLEARS the `TickHandlers` list, and `OnBlockRemoved`/`OnBlockUnloaded` likewise leave `CallbackHandlers` populated.**

A BE that is unloaded and later re-registers listeners (or a BE whose OnBlockUnloaded is followed by anything that re-registers) accumulates stale ids; a subsequent UnregisterAllTickListeners then unregisters ids belonging to other objects' handlers if the engine reuses ids. Manage your own handler ids if you register/unregister dynamically.

— `vsapi/Common/Collectible/Block/BlockEntity.cs:165-174, :212-226, :261-285`

**`Block.ShouldReceiveServerGameTicks` is invoked on a SEPARATE server thread, not the main thread.**

Reading BlockEntity state or mutating collections there is a race. The `extra` out-parameter exists specifically so you can hand the result to `OnServerGameTick` (:1772) which does run on the main thread. Same class of trap for `OnTesselation` (BlockEntity.cs:526-527: "The Tesselator runs in a seperate thread, so you have to make sure the fields and methods you access inside this method are thread safe") and for `GetCollisionBoxes` (:627) / `GetBlockMaterial` (:650) / `GetLightHsv` (Collectible.cs:304).

— `vsapi/Common/Collectible/Block/Block.cs:1739-1748 ("This method is called on a separate server thread… If you do anything with random inside this method, don't use world.Rand because Random its not thread safe, use this [offThreadRandom]")`

**On the server, `Block.Textures`, `Block.ShapeInventory`, `Block.Lod0Shape`, `Block.Lod2Shape`, `Block.TexturesInventory`, `Item.Textures` and ALL five `ModelTransform`s are nulled after load (since 1.20.4).**

Code that reads `block.Textures[...]` or `collectible.GuiTransform` in a path reachable on the server NREs on a dedicated server but works in singleplayer (where a client instance exists). `Block.Shape` :240 is deliberately kept (the FreeRAMServer line for it is commented out at :2914) — so Shape is safe, Textures is not.

— `vsapi/Common/Collectible/Block/Block.cs:2912-2925 (`FreeRAMServer`), field docs at :233, :244, :249, :259, :272; vsapi/Common/Collectible/Item/Item.cs:168-177`

**`IBlockAccessor.SpawnBlockEntity(BlockEntity be)` — the pre-initialized overload — does NOT call CreateBehaviors, Initialize, or OnBlockPlaced.**

A BE spawned this way has an empty `Behaviors` list, a null `Api`, and no tick listeners — it looks alive (GetBlockEntity returns it) but does nothing. Use the `SpawnBlockEntity(string classname, pos, byItemStack)` overload (:482) unless you have deliberately performed the three steps yourself. Note `Block.OnBlockPlaced` uses the string overload (Block.cs:1383).

— `vsapi/Common/API/IBlockAccessor.cs:484-488`

**`ItemStack.Clone()` deep-copies `Attributes` but silently DROPS `TempAttributes`; and `ItemStack.ToBytes` never writes the collectible's code, only its numeric id.**

Anything stashed in TempAttributes vanishes the moment a stack is cloned, split, merged or moved between slots — which is the intended design but routinely surprises. And because only the id is serialized, a stack saved in a BE tree becomes a DIFFERENT item if block/item ids shift; that is why `OnLoadCollectibleMappings` + `ItemStack.FixMapping` (:442-481) exist and why a BE holding stacks must implement `OnStore/OnLoadCollectibleMappings` or its schematics/worldedit copies will contain wrong items.

— `vsapi/Common/Collectible/ItemStack.cs:411-432 (`GetEmptyClone` copies only `Attributes`), :329-335 (ToBytes writes Class/Id/stacksize/attributes)`

**In an openable-container BE, ALL packet ids below 1000 are consumed by the inventory network util before your handler sees them — and the handler returns immediately after.**

A custom packet id like 42 or 500 on any BE deriving from BlockEntityOpenableContainer is swallowed as an inventory operation, gets a permission check you did not write, and your own code never runs. The occupied ids are: everything <1000, 1000 (Open), 1001 (Close), 5000 (OpenInventory), 5001 (OpenLidOthers).

— `vssurvivalmod/BlockEntity/BEOpenableContainer.cs:156-170 (`if (packetid < 1000) { … HandleClientPacket … return; }`)`

**`ShapeElement.From`/`To` are relative to the PARENT element, and `GetLocalTransformMatrix` composes in two different orders depending on the shape's animation version.**

Measuring a child element's world position by reading its `From` gives a number that is meaningless without walking `ParentElement` up the tree (`GetParentPath()` :151). And a shape authored for animVersion 0 that is loaded as animVersion 1 (or vice versa) produces subtly wrong rotations only when scale ≠ 1 — the two orders coincide at unit scale, so the bug hides until you scale something.

— `vsapi/Common/Model/Shape/ShapeElement.cs:286-359 — animVersion 1 is Translate·Scale·Rotate (:309-341), animVersion 0 is Translate·Rotate·Scale (:343-359); `RotationOrigin` and `From` are both divided by 16 (:300-302, :331-333)`

**An unregistered `entityBehaviors` name is a WARNING, not an error — the behavior is skipped and the BE runs without it.**

Forget `RegisterBlockEntityBehaviorClass` on one side (typically the client) and you get a machine that saves and ticks fine on the server but is inert/invisible on the client, with only a single `Lang.Get("Block entity behavior {0} for block {1} not found")` line buried in the log. Grep the log for that exact phrase when a behavior 'does nothing'.

— `vsapi/Common/Collectible/Block/BlockEntity.cs:109-113`

**`AnimationUtil` registers itself as an `EnumRenderStage.Opaque` renderer in its constructor and stops advancing animations entirely while the game is paused, but `AnimationTickServer` has no such guard.**

Client and server animation phase drift apart across any pause in singleplayer, and the client animator only advances during Opaque render — so a machine whose logic keys off client animator frame position desynchronises from server-side state. Note also `InitializeAnimator` :67 disposes the previous renderer before replacing it, so re-initialising after `OnExchanged` is safe, while forgetting to re-initialise leaves a renderer bound to the OLD mesh.

— `vsapi/Common/Model/Animation/AnimationUtil.cs:35 (ctor registers renderer), :89-103 (`OnRenderFrame` returns early on `IsGamePaused`), :79-87 (`AnimationTickServer` has no pause check)`


## vsapi — math, datastructures, API surfaces, client/server

**A `byte[]` stored in a TreeAttribute is capped at **32767 bytes**, and the failure mode above that is corruption, not an error. `ByteArrayAttribute.ToBytes` writes the length as `(ushort)value.Length` but `FromBytes` reads it back with the SIGNED `stream.ReadInt16()`. Lengths 32768..65535 come back negative and `ReadBytes(negative)` throws ArgumentOutOfRangeException mid-tree; lengths above 65535 wrap silently and the rest of the tree decodes as garbage.**

Any BE that serializes its state into a single `tree.SetBytes(...)` blob works fine in testing and then destroys a save the moment the blob crosses 32 KB — the exception surfaces as a corrupt/unloadable chunk far from the code that wrote it. Split large payloads across multiple keys or a TreeArrayAttribute.

— `vsapi/Datastructures/AttributeTree/ByteArrayAttribute.cs:29 (write) vs :35 (read)`

**`GetLong`, `TryGetLong` and `GetItemstack` are the ONLY TreeAttribute getters that throw. They use hard casts `(LongAttribute)` / `(ItemstackAttribute)` instead of `as`, so a key that exists with a different attribute type raises InvalidCastException. Every other Get* silently returns the default in the same situation.**

A migration that changed a key from SetInt to SetLong (or vice versa) makes every existing save crash on load, while the symmetric int/float mistake would have been an invisible silent zero. The inconsistency means you cannot reason about one getter from another.

— `vsapi/Datastructures/AttributeTree/TreeAttribute.cs:753-757, :764-767, :741-745 — compare GetInt at :581-585`

**`SyncedTreeAttribute` cannot mark itemstacks dirty. `TreeAttribute.SetItemstack` is declared `public void` — NOT virtual — so SyncedTreeAttribute has no override for it; `SetStringArray` is virtual but is also not overridden; and assigning through the indexer `tree[key] = attr` bypasses SetAttribute entirely.**

Writing an ItemStack or string[] into entity WatchedAttributes updates the server copy and never syncs it to clients — the client reads whatever was there at the last full sync. You must follow the write with an explicit MarkPathDirty(key).

— `vsapi/Datastructures/AttributeTree/TreeAttribute.cs:518 (non-virtual) and :483; vsapi/Datastructures/AttributeTree/Other/SyncedTreeAttribute.cs:105-173 (the complete list of overrides)`

**`Lang.Get()` NEVER resolves wildcard or regex lang entries. It calls `HasTranslation(key, findWildcarded: false)` and both branches then go through `GetUnformatted`, which only consults the exact-match `entryCache`. Only `Lang.GetMatching` / `GetMatchingIfExists` walk the wildcard and regex caches.**

You add `"smex:pipe-*": "Pipe"` to en.json, `Lang.HasTranslation` says true, and `Lang.Get` still prints the raw key to the player. Worse: `Lang.GetIfExists` has the mirrored bug — HasTranslation (wildcards on) says true, but GetIfExists only checks entryCache and returns null.

— `vsapi/Localization/Lang.cs:191-196 → vsapi/Localization/TranslationService.cs:461-464 → :484-489 (vs GetMatchingIfExists at :560-575)`

**`EnumAttributeType` disagrees with the actual serialized attribute ids. The enum says `StringArray = 8, Bool = 9`; the real registration table says `8 = ByteArrayAttribute, 9 = BoolAttribute, 10 = StringArrayAttribute`, and the enum stops at 9 while ids run to 16.**

Any code that maps `attr.GetAttributeId()` through EnumAttributeType mislabels byte arrays as string arrays and cannot name IntArray/FloatArray/DoubleArray/TreeArray/LongArray/BoolArray at all. Treat the enum as dead and read ids from the static ctor.

— `vsapi/Datastructures/AttributeTree/Other/EnumAttributeType.cs:14-16 vs vsapi/Datastructures/AttributeTree/TreeAttribute.cs:141-157`

**`BlockFacing.IterateThruFacingOffsets(pos)` applies a CUMULATIVE delta from the previous face, not an absolute offset from the origin. NORTH does `Z--`, EAST does `Z++; X++`, DOWN does `Y -= 2`. It is only correct if you walk all six faces in ALLFACES order and then call `FinishIteratingAllFaces(pos)` (which does `Y++`) to restore the origin.**

Calling it for a single face, in a filtered loop, or breaking out early leaves `pos` pointing somewhere arbitrary — and since it MUTATES the caller's BlockPos, the damage escapes the loop. A `continue` inside the loop body is fine; a `break` is not.

— `vsapi/Math/BlockFacing.cs:375-412`

**`JsonObject.this[key]` is case-INSENSITIVE but `JsonObject.KeyExists(key)` is case-SENSITIVE, and `AsDouble`'s string branch parses with the CURRENT culture while `AsFloat`'s uses InvariantCulture.**

`if (attrs.KeyExists("MyKey")) ... attrs["mykey"]` behaves inconsistently depending on the author's casing. And a JSON string `"1.5"` read with AsDouble() on a comma-decimal locale (uk/ru/de) yields 15 — a 10x config error that only reproduces on some users' machines. Use AsFloat, or a numeric JSON literal.

— `vsapi/Datastructures/JsonObject.cs:55 (indexer, OrdinalIgnoreCase) vs :81-83 (KeyExists); :306 (AsFloat, DefaultCultureInfo) vs :334 (AsDouble, no culture)`

**`GetBlock(pos)` returns the AIR block (id 0) for unloaded chunks and out-of-range coordinates — it never returns null. The only null-returning read is `GetBlockOrNull(x,y,z,layer)`, which is in the [Obsolete] int-coordinate region of the interface and defaults to `MostSolid`, not `Default`.**

Network/multiblock walks cannot distinguish 'the structure ends here' from 'that chunk is not loaded'. On a dedicated server the answer changes with chunk load order, producing the classic 'works in singleplayer, breaks in MP / after a reload' bug. Check `GetChunkAtBlockPos(pos) != null` first.

— `vsapi/Common/API/IBlockAccessor.cs:256-262 ('Will never return null … you'll get a block instance with block code "air" and id 0') and :316-324`

**`Vintagestory.API.Common` declares its own `Func<T1,TResult>` … `Func<T1..T7,TResult>` delegates that shadow `System.Func`.**

Any file with both `using System;` and `using Vintagestory.API.Common;` gets CS0104 'ambiguous reference' the moment it names `Func<>`, with an error message that points at your code rather than at the collision. Same file also declares `ActionConsumable`, `ActionBoolReturn` — which are what most vsapi callbacks actually take, not System.Func/Predicate.

— `vsapi/Common/API/Delegates.cs:85-89`

**`AnimatorBase.OnFrame` silently REMOVES any animation code from the caller's `activeAnimationsByAnimCode` dictionary when the shape has no matching animation — it logs at Debug level, once per (entity code, anim code) pair, and only if `entityForLogging` is set (it is null for block entities).**

A typo'd or renamed animation code on a block entity produces ZERO output anywhere: StartAnimation returns true, the dictionary quietly empties on the next frame, and the machine just never animates. Also note codes are compared lowercased (:89, :123), so a capitalised code in JSON works but a capitalised lookup in the dict does not round-trip.

— `vsapi/Common/Model/Animation/AnimatorBase.cs:121-142`

**`AnimatorBase.CurAnims` is a fixed 20-element array with no bounds check.**

A shape/BE running more than 20 simultaneously *running* animations (which includes ones still easing out after being stopped) throws IndexOutOfRangeException from deep inside the render loop every frame.

— `vsapi/Common/Model/Animation/AnimatorBase.cs:50 (allocation) and :225 (`CurAnims[activeAnimCount++] = anim;`)`

**`AnimationUtil.GetAnimator` caches `RootPoses` (the actual ElementPose object tree) per `cacheDictKey` in `api.ObjectCache` and hands the SAME List<ElementPose> to every subsequent animator built with that key.**

Two block entities of the same type sharing a cacheDictKey share their pose tree — they overwrite each other's element transforms every frame, so they can only ever animate in lockstep, and the last one to tick wins. If you need per-instance animation phase, the cache key must be per-instance (or per-phase).

— `vsapi/Common/Model/Animation/AnimationUtil.cs:149-155 and :171-176; the receiving ctor is vsapi/Common/Model/Animation/ClientAnimator.cs:108-125 (`this.RootPoses = rootPoses`)`

**`ModelTransform.Translation` and `.Rotation` default to the sentinel `(-0.000099, -0.000099, -0.000099)`, not to zero, and `ModelTransform.Scale` is a WRITE-ONLY property.**

A hand-constructed `new ModelTransform()` that you never pass through `EnsureDefaultValues()` renders a hair off-origin and rotated by a fraction of a degree; and `float s = tf.Scale;` does not compile (read `ScaleXYZ` instead). JSON-deserialized transforms are fixed automatically by [OnDeserialized]; code-constructed ones are not.

— `vsapi/Common/Collectible/ModelTransform.cs:40, :47, :54, :61; EnsureDefaultValues at :309-315 and OnDeserializedMethod at :380`

**`BlockPos.Set(BlockPos)` does NOT copy the dimension, while `Copy()`, all `*Copy()` variants and every operator DO preserve it. And `ToVec3d()/ToVec3i()/ToVec3f()` are documented 'Note this is dimension unaware' but actually emit `InternalY` (i.e. they ARE dimension-encoded).**

Reusing a scratch BlockPos with `.Set(other)` keeps the scratch pos's old dimension — in the normal world that is invisible, in a minidimension/Devastation it points at the wrong world. And believing the ToVec3d xmldoc leads you to add the dimension offset twice.

— `vsapi/Math/BlockPos.cs:228-234 (Set) vs :411-414 (Copy) vs :841-864 (ToVec3d and its wrong xmldoc)`

**A leading `@` in any wildcard makes the ENTIRE remaining string an anchored regex, so `*` stops meaning 'any characters' and starts meaning 'zero or more of the previous token'.**

A legend or ingredient written as `@(smex:pipe-*|smex:valve-*)` does not match what you think: inside the regex, `-*` matches 'zero or more dashes'. A multiblock layout legend that silently matches nothing produces a structure that never completes, with no error.

— `vsapi/Util/WildcardUtil.cs:150-155 and RegexCache.IsMatch at :252-256 (`"^" + pattern[1..] + "$"`)`

**`TreeAttribute.Equals(world, other, currentPath, ignorePaths)` builds the child path as `curPath` but then recurses passing `currentPath` — the un-extended parent path.**

`ignorePaths` entries deeper than one level never match, so nested attributes you asked to ignore are still compared. Stack-comparison and 'has this BE changed' checks then report differences you explicitly excluded.

— `vsapi/Datastructures/AttributeTree/TreeAttribute.cs:917-927 (curPath computed at :919, `currentPath` passed at :927)`

**`TreeAttribute.Values` and `.Keys` allocate a fresh array on every property read, because the backing ConcurrentSmallDictionary returns defensive copies.**

`for (int i = 0; i < tree.Values.Length; i++)` allocates one array per iteration. In a per-tick BE loop this is a measurable GC source. Enumerate the tree directly (it implements IEnumerable<KeyValuePair<string,IAttribute>>) or hoist the array.

— `vsapi/Datastructures/AttributeTree/TreeAttribute.cs:128-136 → vsapi/Datastructures/ConcurrentSmallDictionary.cs:33-40 (`contents.KeysCopy()` / `ValuesCopy()`)`

**`new AssetLocation(domain, path)` does NOT lowercase either argument, unlike the single-string constructor which lowercases both.**

`new AssetLocation("smex", "Pipe-Iron")` produces a code that never Equals the registered `smex:pipe-iron` (Equals uses EqualsFast, an ordinal compare — :518-522) and never matches a wildcard, while looking correct in the debugger. Also `IsWildCard` reads `Path[0]` unguarded and throws IndexOutOfRange on an empty path (:156).

— `vsapi/Common/Assets/AssetLocation.cs:198-205 ('for efficiency it is the responsibility of calling code to ensure these are lowercase') vs :170-196`

**Adding a variantgroup shifts the position of every later variant in the generated code, because expansion appends `-<state>` strictly in declaration order and `RegistryObject.Variant` iterates in that same order.**

Inserting a group in the middle silently invalidates every `LastCodePart(n)` / `FirstCodePart(n)` index, every `foo-*-north` wildcard, every shape/creative-tab/blockNumbers pattern, and every `skipVariants` entry — with no error at load, just blocks that stop matching.

— `vsapi/docs/json-docs/jsondocs/Vintagestory.ServerMods.RegistryObjectVariantGroup.html (variantgroups example) + vsapi/Common/Registry/RegistryObject.cs:25-30 (VariantStrict is an OrderedDictionary) and :122-186 (CodeWithVariant rebuilds the code by walking Variant in order)`

**`GameMath.Sqrt(double)` and most Vec distance methods return **float**, not double.**

Double-precision world coordinates silently collapse to ~7 significant digits inside any distance computation. At world coordinates in the hundreds of thousands the squared distance loses whole blocks of precision, which shows up as flickering range checks.

— `vsapi/Math/GameMath.cs:203-206 (`public static float Sqrt(double value)`) and vsapi/Math/Vector/Vec3d.cs:483-519 (`public float SquareDistanceTo(double…)`, `public float DistanceTo(Vec3d)`)`

**`LimitedList(maxCapacity, initialElements)` does not truncate the initial elements to the capacity.**

A list seeded from a longer sequence starts over capacity and stays over capacity until the next Add(), which then drops only ONE element. Anything that assumes Count <= capacity (a rolling average, a graph) reads a longer window than it thinks.

— `vsapi/Datastructures/LimitedList.cs:31-41 (`elems = new List<TElem>(initialElements)` — the whole enumerable, capacity only stored)`

**`Lang.HasTranslation(key)` defaults to `logErrors: true` and emits a 'Lang key not found' VerboseDebug line — but only inside the `findWildcarded` branch, and it is suppressed for any key containing 'desc-'.**

Using HasTranslation as a cheap existence probe in a per-frame or per-tick path spams verbose logs on first miss per key (a HashSet dedups after that). Conversely, if you rely on that log to find missing keys, anything named `*desc-*` never reports. Pass `HasTranslation(key, true, false)` for a silent probe.

— `vsapi/Localization/TranslationService.cs:527-541 (the log at :537 is inside `if (findWildcarded)`)`

**`BlockFacing.GetHorizontalRotated(int angle)` uses `angle / 90` (integer division) against `index` (the N,E,S,W order), not `horizontalAngleIndex` (the E,N,W,S order).**

Any angle that is not an exact multiple of 90 truncates toward zero, so `GetHorizontalRotated(89)` and `GetHorizontalRotated(-45)` both return the face unchanged with no warning. And mixing this with `HorizontalAngleIndex`/`HORIZONTALS_ANGLEORDER` arithmetic elsewhere in the same file mixes two different orderings.

— `vsapi/Math/BlockFacing.cs:272-277, compare HORIZONTALS at :87 with HORIZONTALS_ANGLEORDER at :100`

**Version comparison treats a missing pre-release suffix as rank 3 — higher than -rc (2), -pre (1) and -dev (0) — and `IsLowerVersionThan` is defined as `version != reference && !IsNewerVersionThan(...)`, so it returns TRUE for any unparseable/shorter version rather than throwing.**

A modinfo dependency `"smex": "0.9.7"` is a MINIMUM, not a pin — 0.9.7-rc.1 fails it while 1.5.0 satisfies it. And a malformed version string quietly compares as 'lower', silently disabling a dependent mod instead of erroring.

— `vsapi/Config/GameVersion.cs:106-141 (SplitVersionString) and :224-227 (IsLowerVersionThan)`

**Harmony is not a NuGet dependency of the API; the game ships `0Harmony.dll` under `$(VINTAGE_STORY)/Lib/` and mods reference it by path.**

Adding the Lib.Harmony NuGet package instead loads a SECOND Harmony assembly into the process; the two patch registries do not see each other, so `HasAnyPatches` guards and `UnpatchAll` silently no-op against the other copy. Always `<Reference Include="0Harmony"><HintPath>$(VINTAGE_STORY)/Lib/0Harmony.dll</HintPath></Reference>`.

— `vsapi/VintagestoryAPI.csproj (no Harmony PackageReference) vs .compat/vintagestory/vsmodexamples/Code Mods/VSTutorial - 7 - Basic Harmony Patching/vscodetutorial-harmonypatching/VSTutorial/VSTutorial.csproj:36-37`

**`GetLockFreeBlockAccessor()` can return block id 0 for a chunk that is packed or being packed, and `GetCachingBlockAccessor()` will crash the game if you forget `.Begin()`.**

Both are performance accessors whose failure mode is intermittent and data-dependent — the lock-free one produces phantom air blocks under memory pressure only, which reads exactly like an unrelated network-walk bug.

— `vsapi/Common/API/IWorldAccessor.cs:761-775 ('This comes at the cost of sometimes reading invalid data (block id = 0)') and :761-768 ('DONT FORGET: Call .Begin() … Not calling it can cause the game to crash')`

**`Vintagestory.API.Datastructures.OrderedDictionary<K,V>` shadows the .NET 9 BCL `System.Collections.Generic.OrderedDictionary<K,V>`, and vsapi itself has to fully qualify the BCL one.**

After a TFM bump to net9+, files that use `OrderedDictionary<,>` with both namespaces imported break with CS0104. The VS one is also explicitly documented as 'not a very efficient implementation, recommend use only for small sets of data' — do not use it as a hot-path map.

— `vsapi/Datastructures/Dictionary/OrderedDictionary.cs:22 vs vsapi/Common/API/IWorldAccessor.cs:780 (`System.Collections.Generic.OrderedDictionary<IRecipeIngredientBase, List<IRecipeBase>>`)`


## vssurvivalmod — mechanical power

**`MechanicalNetwork.Speed` is SIGNED on the server and UNSIGNED on the client. `UpdateFromPacket` does `speed = Math.Abs(packet.speed)` with the comment "ClientTick() expects speed to be positive always"; the direction survives only in `TurnDir`, which is reconstructed from `packet.direction`. Any client-side code that branches on `Network.Speed < 0` is dead code.**

A machine whose direction matters (a reversible pump, a screw, a conveyor) will read the correct direction on the server and always-forward on the client, producing a permanent visual/logic mismatch that only shows up in multiplayer or after a client rejoin. Use `TurnDir`, `IsRotationReversed()` or `propagationDir` for client-side direction, never the sign of `Speed`.

— `vssurvivalmod/Systems/MechanicalPower/Network/MechanicalNetwork.cs:286 (write) vs. :243,:277 (server-side sign use)`

**There is no explicit maximum network speed anywhere. The ceiling is emergent: each rotor's `TargetSpeed` (windmill `min(0.6, windSpeed)`, water wheel `min(0.3, flowRate)`, creative rotor `0.1*speedSetting` up to 1.0) plus the per-update acceleration clamp `min(0.05f, step*unusedTorque)` and the quadratic air-resistance term `speed*speed*r*r/1000f` added to `totalResistance` for every node.**

You cannot raise the top speed of a vanilla-derived network by adding torque — you must raise a rotor's `TargetSpeed`. Conversely, adding a gear ratio `r` makes air resistance grow as r², so a heavily geared-up branch self-limits hard. Anyone tuning numbers against `Speed` alone without accounting for the r² term will get results that change with gearing.

— `vssurvivalmod/Systems/MechanicalPower/Network/MechanicalNetwork.cs:226,246 and BlockEntityBehavior/BEBehaviorMPWaterWheel.cs:34, BEBehaviorWindmillRotor.cs:122`

**`MechanicalPowerMod` ticks every 20 ms, but `MechanicalNetwork.updateNetwork` (all torque/resistance/speed math) only runs on `tickNumber % 5 == 0` and the client broadcast only on `% 40 == 0`. The `tick` value passed to `GetTorque` therefore advances in steps of 5, and the client's view of speed/torque is up to 800 ms stale.**

A `GetTorque` implementation that integrates against "one tick" silently runs at 1/5 the assumed rate, and any readout driven from `Network.Speed` on the client lags by nearly a second. The overheat pass on `% 10` in `OnServerGameTick` runs at 200 ms — a different cadence again.

— `vssurvivalmod/Systems/MechanicalPower/MechanicalPowerMod.cs:92 and Network/MechanicalNetwork.cs:156-169`

**`MechanicalNetwork.UpdateAngle(float speed)` is `angle += speed / 10f`. Callers already pre-multiply: server passes `speed * dt * 50f`, client passes `f * clientSpeed` where `f = dt * 50f`. So the actual radians-per-second is `Speed * 5` — a `Speed` of 1.0 is ~0.8 revolutions per second, not 1.**

Any attempt to convert `Speed` to rpm/rad·s⁻¹ for a readout or for a physical simulation gets it wrong by a factor of 5 unless this /10-with-×50 is unwound. Vanilla's own creative-rotor tooltip prints `speedSetting * 0.1` "rps" (BEBehaviorCreativeRotor.cs:108), which is the *TargetSpeed*, not the derived rotation rate.

— `vssurvivalmod/Systems/MechanicalPower/Network/MechanicalNetwork.cs:158,144,185-189`

**`MechanicalNetwork.ClientTick` early-returns when `speed < 0.001f`, before it touches `angle` or `clientSpeed`. When a network stops, the client-side angle freezes at whatever it was and `clientSpeed` retains its last value; it also never converges toward `serverSideAngle` while stopped.**

A machine that keys an animation or a shutter position off `AngleRad` will stop mid-frame at an arbitrary phase and stay there, and it will keep whatever server/client angle divergence existed at the moment it stopped. If a stopped machine must rest in a defined pose, you have to force it yourself.

— `vssurvivalmod/Systems/MechanicalPower/Network/MechanicalNetwork.cs:137`

**`MechPowerPath.IsInvertedTowards(testPos)` returns two completely different things depending on whether `fromPos` was supplied to the constructor: `fromPos == null ? invert : fromPos.AddCopy(NetworkDir()) != testPos`. Half the vanilla call sites pass `null` and half pass a real position, and the constructor parameter is optional and third in the list.**

Omitting `fromPos` when constructing a path silently changes the rotation-sense answer for `BEBehaviorMPLargeGear3m.SetPropagationDirection` (which calls `IsInvertedTowards`), producing a large gear that turns the wrong way with no error. There is no compile-time or runtime signal — both forms type-check.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPBase.cs:41-44; null-passing callers at BEBehaviorLargeGear3m.cs:94,106-107,113 and BEBehaviorAngledGears.cs:387,395-396; non-null callers at BEBehaviorMPBase.cs:223,559`

**`BEBehaviorMPBase.Initialize` sets `AxisSign = new int[3] {0,0,1}` at line 168, *after* the base has already been used and *before* `SetOrientations()`. Subclasses that set `AxisSign` in their own `Initialize` must do it after `base.Initialize(...)` or it is overwritten; subclasses that set it in `SetOrientations` (angled gears) or in `OnTesselation` (pulverizer, creative rotor) are relying on ordering that is not documented anywhere.**

A new MP behavior that assigns `AxisSign` before calling `base.Initialize` renders unrotated (or rotated on the wrong axis) with no error. Worse, the pulverizer/creative-rotor pattern means `AxisSign` is `{0,0,1}` until the first tesselation happens — anything reading it earlier gets the default.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPBase.cs:168-169; BEBehaviorPulverizer.cs:130-147 sets it in OnTesselation; BEBehaviorCreativeRotor.cs:60-83 likewise`

**Angled gears use a SIX-element `AxisSign` for two-face variants and a THREE-element one for single-face variants; `AngledGearsBlockRenderer` silently `return`s after rendering only the peg mesh when `AxisSign.Length < 4`. The commented-out `Debug.WriteLine("3 length AxisSign")` at that spot shows this was a debugging pain point.**

Building an angled-gear-like block and giving it a 3-element AxisSign means the cage half of the model never draws, with no exception and no log line. The `MechBlockRenderer` base indexes `AxisSign[0..2]` unconditionally, so a shorter array throws instead.

— `vssurvivalmod/Systems/MechanicalPower/Renderer/AngledGearBlockRenderer.cs:59-63 and BlockEntityBehavior/BEBehaviorAngledGears.cs:91,125`

**`MechNetworkRenderer.AddDevice` does `Activator.CreateInstance(RendererByCode[rendererCode], ...)` with a raw dictionary index and no `TryGetValue`. An unrecognised `mechanicalPower.renderer` string in block JSON throws `KeyNotFoundException` from inside `BEBehaviorMPBase.Initialize`. It also silently no-ops when `device.Shape == null`.**

A typo in one block's JSON crashes block-entity initialisation on the client rather than falling back to `generic`. And a behavior whose `GetShape()` returns null (e.g. an unbuilt `BEBehaviorRightClickConstructable` water wheel) is simply never rendered and never registered, so a later `RemoveDevice` also no-ops — easy to misread as a renderer bug.

— `vssurvivalmod/Systems/MechanicalPower/Renderer/MechNetworkRenderer.cs:51,64`

**`BEBehaviorMPRotor.AngleRad` and `PulverizerRenderer.UpdateLightAndTransformMatrix` have side effects. The rotor getter calls `Api.World.PlaySoundAt` when enough time has elapsed; the pulverizer renderer calls `bhpu.OnClientSideImpact(...)` (sound + particles) and mutates `prevProgressLeft/Right` and `leftDir/rightDir` on the behavior. Both are invoked once per frame from `MechBlockRenderer.UpdateCustomFloatBuffer`.**

Reading `AngleRad` from your own code for a readout, a debug print, or a second renderer double-fires sounds and impact effects. Vanilla's own game logic deliberately avoids it — `BEHelveHammer.onEvery25ms` accumulates hits from `Network.Speed * GearedRatio` server-side rather than from the rendered angle (BEHelveHammer.cs:223-235), so the visual and the gameplay hit counter are two independent approximations of the same thing.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPRotor.cs:37-49 and Renderer/PulverizerRenderer.cs:132-141,156-165`

**`BEBehaviorMPToggle.JoinNetwork` and `BEBehaviorMPPulverizer.JoinNetwork` mutate the network they are joining — they divide `network.Speed` and `network.clientSpeed` by `|network.Speed * GearedRatio| * 1.6` when that exceeds 1. A node joining a network can therefore change the speed of every other machine on it.**

Because `RebuildNetwork` re-runs discovery (and hence `JoinNetwork`) whenever any node is broken or a chunk finishes loading, a network with a helve hammer or pulverizer on it gets speed-divided again on every rebuild. Repeated chunk load/unload cycles visibly slow such a network until torque re-accelerates it.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorToggle.cs:94-106 and BEBehaviorPulverizer.cs:116-128`

**`BEBehaviorMPWaterWheel.CheckWater` **writes to the world from a 1-second tick that also runs on the client**: `ReplaceRapidWater` calls `BlockAccessor.SetBlock(..., BlockLayersAccess.Fluid)` plus a manual `TriggerNeighbourBlockUpdate` because "setting blocks to the fluids layer does not update neighbours, by default". In DEBUG builds it *throws* when it cannot find the downstream water block.**

A vanilla-derived water wheel silently converts rapid water into ordinary water around itself, which is the mechanic that stops rapids from being infinitely reusable. Any mod wheel that copies `CheckWater` inherits both the world mutation and a debug-build exception on unusual water geometry.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPWaterWheel.cs:51,230-251,199-202`

**`BEBehaviorMPBase.ToTreeAttributes` writes `networkid`, `turnDirFromFacing` and `g`, but `FromTreeAttributes` reads them only when `worldAccessForResolve.Side == EnumAppSide.Client`. The server-side values in the savegame are therefore write-only. Meanwhile `GearedRatio` on the server is reconstructed purely by discovery order.**

You cannot debug a server-side ratio problem by inspecting the savegame — the stored `g` reflects the last state the server happened to broadcast. And any mod field you add to the MP behavior's tree must be restored on BOTH sides explicitly; copying the vanilla shape of `FromTreeAttributes` puts your read inside the client-only branch by accident.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPBase.cs:316-364`

**`MechanicalPowerMod.RebuildNetwork` contains a suppressed known bug: `if (network.nodes.Values.Count == 0)` is followed by a commented-out log line reading "This case shouldn't happen, but it does occasionally, should get debugged eventually, until then, it makes no sense to spam the log files with this".**

Empty networks are a real, unfixed vanilla state. Code that assumes `RebuildNetwork` always produces at least one node — or that a `MechanicalNetwork` instance in `networksById` has members — will hit it. `OnServerGameTick` guards with `network.nodes.Count > 0` for exactly this reason.

— `vssurvivalmod/Systems/MechanicalPower/MechanicalPowerMod.cs:247-251`

**`BEBehaviorMPAngledGears.SetPropagationDirection` has a brace-less `if (this.turnDir1 != null)` followed by a blank line and then a four-branch `if/else if` chain. Only the first `if (turnDir == turnDir1)` is guarded by the null check; the `else if` branches belong to it and are reached only through it, so the code happens to be correct — but it reads as a bug and any edit that adds a statement between them changes behaviour.**

Copying this method into a derived gear and "tidying" the formatting is very likely to change the semantics. The identical remapping logic appears three more times in the same file (`SetOrientations` l.76-83, `GetPropagationDirectionInput` l.293-304, `IsPropagationDirection` l.306-317) with different guard shapes.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorAngledGears.cs:278-291`

**`IMechanicalPowerNode.OverheatValue` is a mutable interface property driven purely from `MechanicalPowerMod.OnServerGameTick`, is never serialized, and the consequence it was built for is commented out ("Disabled until stable" — the `BEBehaviorBurning.OnFirePlaced` call).**

Overspeeding a vanilla network produces smoke particles and nothing else, and the accumulated overheat resets to 0 on every chunk reload. Anyone building a balance rule on top of vanilla overheating is building on a stub.

— `vssurvivalmod/Systems/MechanicalPower/MechanicalPowerMod.cs:177-189 and Network/IMechanicalPowerNode.cs:24`

**`BlockAngledGears.DidConnectAt` replaces the block with a different variant (`ExchangeBlockAt`) as a side effect of a connection query. `BEBehaviorMPBase.tryConnect` therefore calls `connectedToBlock.DidConnectAt(...)` *before* reading `node.GetGearedRatio` / `node.IsPropagationDirection`, with the in-line comment "do this first to set the new Angled Gear block correctly prior to getting propagation direction".**

`DidConnectAt` is not a notification — it can mutate the world and invalidate any `Block`/`BlockEntity` reference you are holding for that position. Caching the neighbour's block before calling it, or calling it after reading propagation state, produces gears that connect with the wrong orientation.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPBase.cs:222 and Block/BlockAngledGears.cs:91-102`

**No mechanical-power machine in vanilla does away-catch-up. Quern, pulverizer, helve hammer and Archimedes screw all accumulate progress from `dt * speed` in a real-time `RegisterGameTickListener`; when the chunk unloads, production simply stops. The only calendar-based catch-up code in the mod is agricultural/thermal (`BlockEntityFastForwardGrowth`, `BEForge`, `BEBoiler`, `BEBeehive`, `BEBeeHiveKiln`, `BESpawner`, `BEFruitpress`).**

There is no vanilla precedent to copy for "my mechanical machine should keep producing while the player is away". You have to build it: the network itself does not tick when `!fullyLoaded`, and `MechPowerData` is discarded on save load, so there is no stored speed history to integrate against — a catch-up design must persist its own last-known speed alongside `lastTickTotalHours`.

— `vssurvivalmod/Systems/MechanicalPower/BlockEntity/BEPulverizer.cs:109-137 and BlockEntity/BEQuern.cs:242-297 vs. BlockEntity/BlockEntityFastForwardGrowth.cs:49`

**`BlockEntityFastForwardGrowth.Update` guards against NEGATIVE elapsed time (`hoursSinceLastUpdate < 0` → `onRollback` + reset stamp) and clamps catch-up to one in-game year, skipping the excess silently. `BEForge` has the negative guard but no clamp; `BEBoiler` has neither, it just requires `dh > 0.1f`.**

Negative elapsed time is a real condition (schematic import writes a BE whose stamp is ahead of the calendar). Without the guard a `hoursPassed` multiplication runs backwards — fuel is refunded, temperature is subtracted, progress goes negative. Any new catch-up code must include both the negative guard and an upper clamp, or a long-abandoned chunk produces an unbounded burst on first load.

— `vssurvivalmod/BlockEntity/BlockEntityFastForwardGrowth.cs:56-77, BEForge.cs:201-206, BEBoiler.cs:83-88`

**`BlockEntityOpenableContainer.OnReceivedClientPacket` routes ALL packet ids below 1000 into `Inventory.InvNetworkUtil.HandleClientPacket` after a permission check — including ids your own BE might want to define. `EnumBlockEntityPacketId.Open/Close` are 1000/1001, and `EnumBlockContainerPacketId.OpenInventory/OpenLidOthers` are 5000/5001.**

A custom block-entity packet id under 1000 on an openable container is swallowed by the inventory network layer (and triggers a `SendInventoryRollback` if the player lacks Use permission). Custom ids must be ≥1002 and must avoid 5000/5001.

— `vssurvivalmod/BlockEntity/BEOpenableContainer.cs:156-170 and vsapi/Client/UI/Dialog/GuiDialogBlockEntity.cs:196-200`


## vssurvivalmod — blocks, heat, recipes, handbook

**`CollectibleObject` has TWO incompatible `GetTemperature` implementations. `GetTemperature(world, stack)` uses default cooldownSpeed 120 deg/hour times `max(1, T/200)`, only recomputes past `1/150` hour, and advances `temperatureLastUpdate` ONLY when it actually cooled. `GetTemperature(world, stack, didReceiveHeat)` uses default 90, no velocity multiplier, a `1/85` hour threshold, skips the behavior walk — and writes `temperatureLastUpdate = nowHours` on EVERY call regardless.**

Calling the 3-arg overload more often than once per 1/85 game hour (~0.7 real seconds) permanently prevents the stack from ever cooling, because the anchor is reset before the threshold is ever met. Mixing the two overloads on the same stack also produces two different cooling rates. Pick the 2-arg overload and never call the other one.

— `vsapi/Common/Collectible/Collectible.cs:3260-3285 vs 3293-3340`

**`SetTemperature(..., delayCooldown: true)` (the DEFAULT) pushes `temperatureLastUpdate` **0.5 game hours into the future** whenever the new temperature is higher than the old — despite the code comment saying 0.25.**

A stack you heat is frozen at that temperature for the next half game hour (30 real seconds), and `GetTemperature` will compute a negative hourDiff during that window. Any test that heats a stack then asserts on cooling will see zero decay. Vanilla passes `delayCooldown: false` wherever it wants immediate physics (BEIngotMold.cs:494, BEIngotMold.cs:841).

— `vsapi/Common/Collectible/Collectible.cs:3365-3370`

**`"temperature"` is a member of `GlobalConstants.IgnoredStackAttributes`, so a 1400 °C ingot and a cold ingot compare EQUAL under `stack.Equals(world, other, GlobalConstants.IgnoredStackAttributes)` and merge freely.**

Any "is this the same material" check written with the standard ignore-list will happily let a player stack a molten item onto a cold one. Vanilla compensates by averaging temperature manually on merge — see BEForge.cs:339-343 (`(myTemp*StackSize + histemp)/(StackSize+1)`) and Collectible.DoSmelt using `TryMergeStacks` "to average spoilage rate and temperature" (Collectible.cs:2803).

— `vsapi/Config/GlobalConstants.cs:372 (`{ "temperature", "toolMode", "renderVariant", "transitionstate" }`)`

**`BlockMultiblock.GetRotatedBlockCode` hardcodes the string `"multiblock-monolithic"` when rebuilding the rotated code, ignoring whatever `type` the behavior actually used.**

A multiblock declared with a non-monolithic `type` silently mutates into monolithic parts when rotated by worldedit/schematic placement. Also note `OffsetToString(0)` returns `"-0"`, not `"-p0"`, so the zero component uses a third spelling.

— `vsessentialsmod/Block/BlockMultiblock.cs:521 (`new AssetLocation(Code.Domain, "multiblock-monolithic" + OffsetToString(...)...)`) vs BehaviorMultiblock.cs:132 which builds `"multiblock-" + type + ...``

**`BlockMultiblock.GetSelectionBoxes`/`GetCollisionBoxes` return a FULL CUBE (`[Cuboidf.Default()]`) whenever the controller position holds another BlockMultiblock, and `GetPlacedBlockInfo` returns an empty string in the same case.**

Orphaned multiblock ghosts (controller broken by a non-standard path, chunk corruption, `/we` paste) become invisible full-solid unbreakable-looking blocks that report nothing in the block info HUD. If you build your own multiblock, add a self-heal that clears ghosts whose controller BE is null — vanilla has none.

— `vsessentialsmod/Block/BlockMultiblock.cs:159, 181, 371`

**`BlockBehaviorMultiblock.OnBlockPlaced` **throws `IndexOutOfRangeException`** — not a logged warning — if the computed `multiblock-<type>-<dx>-<dy>-<dz>` block does not exist. The message blames a 5x5x5 limit.**

A `sizex/sizey/sizez` bigger than 5, or an offset combination whose ghost block was never generated, hard-crashes block placement rather than failing gracefully. `cposition` defaults to (1,0,1), so a 3-tall structure with the controller at the bottom needs `cposition.y = 0` explicitly.

— `vsessentialsmod/BlockBehavior/BehaviorMultiblock.cs:135`

**`MultiblockStructure.InitForUse` takes DEGREES, and calling `WalkMatchingBlocks` / `InCompleteBlockCount` before it throws `InvalidOperationException("call InitForUse() first")`. Air blocks are not in the exported offsets at all.**

Passing a radian value or a `BlockFacing.Index` produces a structure check that silently validates the wrong cells (a rotation of e.g. 2 degrees rounds every offset back to itself, so an east-facing kiln validates as if it faced north). Because air is not exported, `InCompleteBlockCount` will NOT complain about a cell you expected to be empty being filled with something.

— `vsapi/Common/MultiblockStructure.cs:52, 78-81, 108-111 and the class doc at l.26-28`

**`BlockLiquidContainerBase.TryPutLiquid(ItemStack, …)` returns `Math.Min(desiredItems, placeableItems)` in the empty-container branch, but actually stores `GameMath.Min(availItems, desiredItems, placeableItems)`.**

When the source stack holds less than you asked for, the method reports moving MORE than it stored, so a caller that decrements the source by the return value destroys liquid. The `BlockPos` overload (l.605-618) computes it correctly, so the two overloads disagree. Always recompute what you actually moved from the resulting content stack.

— `vssurvivalmod/Systems/Liquid/BlockLiquidContainerBase.cs:544-551`

**Liquid volume is not stored as litres anywhere. It is `contentStack.StackSize`, and litres = `StackSize / WaterTightContainableProps.ItemsPerLitre`. The JSON key is `waterTightContainerProps` while the C# class is `WaterTightContainableProps`, and `ItemsPerLitre` defaults to 1 with an explicit comment about divide-by-zero.**

An int StackSize means litres are quantized by `ItemsPerLitre` — with the default of 1 you cannot represent 0.5 L at all, and `(int)(props.ItemsPerLitre * desiredLitres)` truncates, so a `TransferSizeLitres` below 1/ItemsPerLitre transfers exactly zero forever. `GetContainableProps` swallows every exception and returns null, so a malformed props block looks like "not a liquid".

— `vssurvivalmod/Systems/Liquid/BlockLiquidContainerBase.cs:253-258 + 314-326; vssurvivalmod/Systems/Liquid/WaterTightContainableProps.cs:23`

**`Block.GetHandbookDropsFromBreakDrops` calls `GetDrops(api.World, forPlayer.Entity.Pos.XYZ.AsBlockPos, forPlayer)` — i.e. at the PLAYER's feet, not at any real instance of the block.**

Any `GetDrops` override that reads its block entity gets `null` back and must have a sane fallback, or the handbook page throws / shows nothing. This is exactly why `BlockToolMold.GetDrops` ends with `else { stacks.Add(new ItemStack(this)); }` (Block/BlockToolMold.cs:361-364).

— `vsapi/Common/Collectible/Block/Block.cs:1252-1266`

**`GuiHandbookTextPage.Init` passes the page's `Text` through `Lang.Get(...)` whenever `Text.Length < 255`.**

A short literal English paragraph in your `config/handbook/*.json` is treated as a translation key. If it happens to collide with a real key you get someone else's text; otherwise you get the raw string back and it looks fine until a translator adds that key. Always use an explicit lang key, or keep the body over 255 characters.

— `vssurvivalmod/Systems/Handbook/Gui/GuiHandbookTextPage.cs:38-41`

**`RecipeRegistrySystem` slams `canRegister = false` in `AssetsFinalize` via a separate ModSystem with `ExecuteOrder() => 99999`, and every `RegisterXRecipe` then throws `InvalidOperationException`.**

Registering a smithing/alloy/barrel recipe from any hook that runs after AssetsFinalize (or from a ModSystem whose ExecuteOrder is >= 99999) is a hard crash at world load, not a warning. Register during AssetsLoaded/AssetsFinalize with ExecuteOrder < 99999.

— `vssurvivalmod/Systems/Cooking/RecipeRegistrySystem.cs:113-121 and the throw at l.218`

**`BlockEntity.FromTreeAttributes` is always called BEFORE `Initialize`, so `Api` is null on the first call — but on later syncs it is not, and vanilla branches on exactly that.**

Any FromTreeAttributes body that touches `Api.World` crashes on world load but works fine when you test by placing the block. Use the `worldAccessForResolve` parameter for resolution and null-check `Api` for everything else.

— `vsapi/Common/Collectible/Block/BlockEntity.cs:326-345 (doc comment: "FromTreeAttributes is always called before Initialize() is called, so the this.api field is not yet set!"); branch example at vssurvivalmod/Systems/Microblock/BEMicroBlock.cs:1109 and 1122`

**On the server, `BlockEntity.ToTreeAttributes` early-returns after dumping a stashed `missingBlockTree` when `Block.IsMissing`, skipping your entire serialization AND the behavior loop.**

If a mod is temporarily disabled, its BE data is preserved verbatim — good — but it also means base.ToTreeAttributes() can silently no-op, so any code that assumes your keys are present after a save round-trip must tolerate a missing-block world.

— `vsapi/Common/Collectible/Block/BlockEntity.cs:302-310, with the stash set in FromTreeAttributes at l.344`

**`BlockEntityBloomery.DoSmelt` computes `q = OreStack.StackSize / SmeltedRatio` (integer) and then does `OutSlot.Itemstack = SmeltedStack.Clone(); OutStack.StackSize *= q;` — it multiplies the SmeltedStack's own StackSize, not sets it. In the `mergeUnitsInBloomery` branch it instead writes a FLOAT `units` attribute = `qf * 100`.**

A SmeltedStack with StackSize 2 yields 2*q, which is easy to misread as q. And the two branches produce structurally different outputs (a stack of N vs one stack carrying `units`), so downstream code must handle both. The iron bloom path is the `units` one.

— `vssurvivalmod/BlockEntity/BEBloomery.cs:191-205`

**`BlockEntityForge.MaxTemperature` is `700 + Attributes["inForge"]["tempGainDeg"].AsInt(1)` — the fallback is **1**, giving 701 °C, and there is no warning when the attribute is missing.**

A new fuel item that forgets the `inForge` attribute block silently caps the forge at 701 °C — hot enough to look like it works, too cold to forge most metals. Same shape for `BurnRate`'s `durationMul` fallback of 1f at l.42.

— `vssurvivalmod/BlockEntity/BEForge.cs:47-53`

**`BlockEntityIngotMold.ReceiveLiquidMetal` stamps `cooldownSpeed = 300` on the content stack, but ONLY on the very first pour (when `SelectedContents == null`); and `GetStateAwareContentsSided` strips `cooldownSpeed` off the ingot you take out.**

Metal in a mold cools 2.5x faster than the 120 default, but that only holds while it is in the mold. If you refactor the fill path so `SelectedContents` is pre-created, the mold silently starts cooling at the normal rate and every solidification timing changes. Also note `IsHardened` is `T < 0.3*meltingpoint` while the crucible's `HasSolidifed` is `T < 0.9*meltingpoint` — two different thresholds for "solid".

— `vssurvivalmod/BlockEntity/BEIngotMold.cs:490-497 and l.285`

**`BEBehaviorTemperatureSensitive.Initialize` throws `InvalidOperationException` if the block entity does not implement `ITemperatureSensitive`.**

Adding `{ "name": "TemperatureSensitive" }` to a block's `entityBehaviors` before the BE class implements the interface crashes on placement, not at load. Note also that the JSON array key is `entityBehaviors` (BE behaviors), not `behaviors`.

— `vssurvivalmod/BlockEntityBehavior/BEBehaviorTemperatureSensitive.cs:70-73`

**Anvil voxels are `byte[16,6,16]` — only SIX layers in Y — and only the low 2 bits of each byte survive serialization (4 voxels packed per byte).**

Any material enum beyond value 3 is silently truncated to 0-3 on save/load, and any recipe layer beyond y=5 is unreachable. `SmithingRecipe.QuantityLayers => 6` matches (Systems/Recipe/SmithingRecipe.cs:42).

— `vssurvivalmod/BlockEntity/BEAnvil.cs:100 (comment: "Only the first 2 bits of each byte are used and serialized") and serializeVoxels at l.1039-1058`

**Microblock cuboids pack min/max into 4 bits each with the max stored as `max-1`, and the material field is the top 8 bits indexing into `BlockIds` — which are raw runtime BLOCK IDs, not codes.**

Block IDs are world-specific, so anything that copies a microblock between worlds MUST go through `OnStoreCollectibleMappings`/`OnLoadCollectibleMappings` (l.1259/1310). Also, `ToUint` has `Debug.Assert` preconditions only — in a release build an out-of-range cuboid corrupts neighbouring bit fields instead of failing.

— `vssurvivalmod/Systems/Microblock/BEMicroBlock.cs:1229-1256 (ToUint/FromUint) and ToTreeAttributes l.1207 (`tree["materials"] = new IntArrayAttribute(BlockIds)`)`

**`BlockContainer.OnBlockBroken` drops exactly ONE stack — `OnPickBlock(world, pos)` — and never consults `GetDrops` or the JSON `drops` array.**

Overriding `GetDrops` on a BlockContainer subclass has no effect on breaking; you must override `OnPickBlock` (or `OnBlockBroken` itself). Conversely `Block.SpawnDropsAndRemoveBlock` calls `entity.OnBlockBroken(byPlayer)` BEFORE `GetDrops` (Block.cs:1108-1119), so a BE that clears its inventory in OnBlockBroken makes the block's own GetDrops see an empty machine.

— `vssurvivalmod/Block/BlockContainer.cs:150-187`

**`BlockEntityBarrel` crafts zero-seal-hour recipes **synchronously inside the `SlotModified` handler**, on the server, guarded only by an `ignoreChange` bool.**

Any code that writes to a barrel slot can have the contents replaced under it before the write returns. If you model a machine on the barrel, expect the same: never hold a cached ItemSlot reference across a slot write.

— `vssurvivalmod/Systems/Barrel/BEBarrel.cs:121-195 (`Inventory_SlotModified` -> `FindMatchingRecipe` -> `recipe.TryCraftNow(Api, 0, inputSlots)` at l.177)`

**`BlockEntityFirepit.OnBurnTick` bails out entirely on `if (Block.Code.Path.Contains("construct")) return;` and `OnTesselation` does the same.**

A substring match on the block path is the vanilla "machine under construction" gate. If any of your variant values contain the literal text `construct`, your firepit-derived machine is frozen with no error. The generic RCC path (`Systems/RightClickConstruction.cs`) is the modern replacement.

— `vssurvivalmod/BlockEntity/Firepit/BEFirepit.cs:196 and l.872`

**`ICustomHandbookPageContent` is resolved with `GetCollectibleInterface<T>()`, which returns only the FIRST implementer (the collectible itself, else the first matching CollectibleBehavior), and it is invoked as the very last step of page composition.**

Two behaviors both implementing it means one is silently ignored, and there is no way to insert content in the middle of the generated page — only append. To restructure a page you must subclass `CollectibleBehaviorHandbookTextAndExtraInfo` and override `GetHandbookInfo`.

— `vssurvivalmod/Systems/Handbook/CollectibleBehaviorHandbookTextAndExtraInfo.cs:137 and vsapi/Common/Collectible/Collectible.cs:3622`

**`ModSystemSurvivalHandbook` injects a `CollectibleBehaviorHandbookTextAndExtraInfo` into every collectible that lacks one, at `LevelFinalize`, client-side.**

You never need to declare the behavior in JSON — but the injection also means the behavior array differs between client and server, and that a subclass of yours declared in JSON wins (the check is `HasBehavior<CollectibleBehaviorHandbookTextAndExtraInfo>()`, which is true for subclasses). Order matters: anything you do to CollectibleBehaviors after LevelFinalize can be clobbered.

— `vssurvivalmod/Systems/Handbook/SurvivalHandbook.cs:138-158 (`obj.CollectibleBehaviors = obj.CollectibleBehaviors.Append(bh)`)`

**`Block.ParticleProperties` entries loaded from JSON are shared singletons that vanilla mutates in place and then resets.**

Reading another block's `ParticleProperties` and holding onto it, or spawning from two threads, gives you whatever the last mutator set. Treat them as a scratch buffer: set every field you need immediately before `SpawnParticles`, and restore anything you changed.

— `vssurvivalmod/Block/BlockForge.cs:170-181 (`props.basePos = dpos; props.Quantity.avg = 1; … SpawnParticles(props, byPlayer); props.Quantity.avg = 0;`)`

**`vssurvivalmod/BlockEntity/Unfinished/BEBlastFurnace.cs` exists and looks like a working blast furnace, but is not registered in `Systems/Core.cs` and is dead code.**

Copying it as a reference implementation copies an abandoned 2016-era design (integer `state`/`burntime`/`receivedAirBlows` counters, no calendar-hours accounting, no `ITemperatureSensitive`). Use `BEFirepit` + `BEForge` as the live references instead.

— `vssurvivalmod/BlockEntity/Unfinished/BEBlastFurnace.cs:20 (`class FurnaceSection : BlockEntityOpenableContainer`); absent from RegisterDefaultBlockEntities (Systems/Core.cs:788-925)`

**`BlockEntityBloomery.CanAdd`/`TryAdd` classify a stack as ore vs fuel purely by numeric thresholds on `CombustibleProperties`: ore if `SmeltedStack != null && 1000 <= MeltingPoint < 1500`, fuel if `BurnTemperature >= 1200 && BurnDuration > 30`. There is no tag or code check.**

Any modded item that happens to satisfy both predicates is accepted in whichever slot is tested first (ore wins), and an ore whose melting point you retune to 1500+ silently becomes unaddable with no error message to the player. `BlockBloomery.OnLoaded` uses a THIRD threshold (`MeltingPoint < 1500`, no lower bound) when building the interaction help, so the help list and the accept list can disagree.

— `vssurvivalmod/BlockEntity/BEBloomery.cs:249-267 and 279-306`


## vsessentialsmod + vscreativemod — render, animation, sound, schematics

**Looping `animationSounds` LEAK: `loopingSounds` is declared `= null` and never assigned, and the static `ShouldPlaySound` takes it **by value**. The `if (loopingSounds == null) loopingSounds = new Dictionary<…>()` inside only assigns the local parameter, so every trigger creates a brand-new looping `ILoadedSound` in a throwaway dictionary, starts it, and never stops or disposes it. This is broken in BOTH the entity path (`AnimationManager`) and the block-entity path (`AnimationUtil`).**

A machine with a looping `animationSounds` entry and the default `onAnimationEnd: Repeat` starts a fresh, never-stopped looping sound on every loop iteration (ClientAnimator.cs:312 fires once per `Iterations` value). Within a minute the player hears a growing wall of overlapping loops and the OpenAL source pool is exhausted. Do not use declarative `animationSounds` with `looping: true` — own the `ILoadedSound` yourself, WeatherSimulationSound-style.

— `vsapi/Common/Model/Animation/AnimationUtil.cs:190, :194 ; vsapi/Common/Model/Animation/AnimationManager.cs:545, :549, :552, :561 ; the cleanup at AnimationManager.cs:483-495 and the dispose at :530 therefore always see null`

**`AnimationUtil.activeAnimationsByAnimCode` is a **case-sensitive** dictionary, while `AnimationManager.ActiveAnimationsByAnimCode` uses `StringComparer.OrdinalIgnoreCase` — and `AnimatorBase` lowercases every animation code on construction and compares `activeAnimationsByAnimCode.ContainsKey(anim.Animation.Code)` against the lowercased name.**

On a BLOCK ENTITY, an AnimationMetaData whose `Code` has any uppercase letter starts (lookup at :123 lowercases) and is then immediately treated as stopped on the very same `OnFrame` pass (the ContainsKey at :148 fails), so it eases out and dies. The same meta works fine on an entity. Keep every block-entity animation code lowercase. `AnimationMetaData.OnDeserializedMethod` lowercases `Animation` but NOT `Code` (AnimationMetaData.cs:294-295), so a JSON `"code": "Rotate"` reaches you mixed-case.

— `vsapi/Common/Model/Animation/AnimationUtil.cs:14 (plain `new Dictionary<string, AnimationMetaData>()`) vs vsapi/Common/Model/Animation/AnimationManager.cs:38 ; vsapi/Common/Model/Animation/AnimatorBase.cs:93, :123, :148`

**A block-entity animation advances on the RENDER loop, not the game tick: `AnimationUtil.OnRenderFrame` passes the render `deltaTime` straight into `animator.OnFrame`, and returns early when the game is paused. `RunningAnimation.Progress` then advances `CurrentFrame` by `30 * dt * AnimationSpeed * GlobalConstants.OverallSpeedMultiplier`.**

There is no server-authoritative animation phase for block entities. Two clients watching the same machine are in different phases, and phase drifts with framerate hitches, pause, and `OverallSpeedMultiplier`. Anything that must be phase-locked to a mechanical network has to derive the phase from a synced quantity each frame (a network angle/counter) rather than trusting `CurrentFrame`. The server-side variant (`AnimationTickServer`, registered at 20 ms by `BlockEntityAnimationUtil.InitializeAnimatorServer`) is a completely separate clock.

— `vsapi/Common/Model/Animation/AnimationUtil.cs:89-96 ; vsapi/Common/Model/Animation/RunningAnimation.cs:109, :120 ; vsapi/Common/Model/Animation/AnimationMetaData.cs:255-258`

**An entity's animations only advance when the entity is being rendered: `AnimationManager.OnClientFrame` wraps `Animator.OnFrame` in `if (entity.IsRendered || entity.IsShadowRendered || !entity.Alive)`.**

Off-screen entities freeze mid-animation, so frame-callback logic (`RegisterFrameCallback`) never fires for them and any state machine driven off `AnimProgress` stalls. Block entities are NOT gated this way — `AnimationUtil` keeps ticking regardless of visibility — so the two paths behave differently and code ported between them changes semantics silently.

— `vsapi/Common/Model/Animation/AnimationManager.cs:467-481`

**Hard cap of 16 concurrent animations per ClientAnimator, enforced by nothing. `ClientAnimator.MaxConcurrentAnimations = 16` sizes `frameByDepthByAnimation` / `nextFrameTransformsByAnimation` / `weightsByAnimationAndElement` / `prevFrame` / `nextFrame`, but `AnimatorBase.CurAnims` is a `RunningAnimation[20]` filled with an unchecked `CurAnims[activeAnimCount++] = anim`.**

The 17th simultaneously-active animation throws `IndexOutOfRangeException` inside `calculateMatrices` (ClientAnimator.cs:336) every render frame, and the 21st throws in `ProgressRunningAnimation`. Neither is guarded or logged usefully. A megablock that starts one animation per moving sub-part can walk into this.

— `vsapi/Common/Model/Animation/ClientAnimator.cs:32, :156-158, :289-290, :336 ; vsapi/Common/Model/Animation/AnimatorBase.cs:50, :225`

**`Animation.OnAnimationEnd` defaults to `Repeat` (0) in C#, but the value is baked into the shape JSON by the model creator — so a shape exported with the default 'stop at end' setting arrives as `Stop`, and `AnimatorBase.ProgressRunningAnimation` then **removes the animation code from your own dictionary** the first time `Iterations > 0`.**

A cycle animation authored without `onAnimationEnd: "Repeat"` plays exactly once and then silently deletes itself from `activeAnimationsByAnimCode` — so your `ContainsKey` check says the machine is stopped and any code that only calls StartAnimation once never restarts it. This is our known trap, and the mechanism is that the *animator*, not the caller, mutates the caller's dictionary.

— `vsapi/Common/Model/Animation/Animation.cs:68-71 (defaults) ; vsapi/Common/Model/Animation/AnimatorBase.cs:199-234, and specifically the `return false` at :220 feeding `activeAnimationsByAnimCode.Remove(Code)` at :176`

**`RunningAnimation.Progress` returns immediately when `meta.AnimationSpeed == 0`, and `CalcBlendedWeight` does too.**

Setting AnimationSpeed to 0 does not mean 'run at zero rate, hold the current pose blended in' — it freezes the whole animation including easing and weight, so the pose stops updating and the blend weight stops converging. A machine whose network speed drops to 0 should be given a small epsilon speed or be stopped properly, not set to 0.

— `vsapi/Common/Model/Animation/RunningAnimation.cs:105, :92`

**`BlockSchematic.TransformWhilePacked` flip is off by one: `dy = SizeY - dy` (and X/Z equivalents) instead of `SizeY - 1 - dy`.**

Every `/we imflip` / `immirror` shifts the schematic one block along the flipped axis and grows the bounding box by one. Round-tripping flip-flip does not return the original; a multiblock validated against a flipped schematic will be one block out.

— `vsapi/Common/Collectible/Block/BlockSchematic.cs:810, :818, :826 (blocks), :886, :895, :904 (decors), :938-940 (block entities)`

**`TransformWhilePacked` resolves a block entity's block with `worldForResolve.GetBlock(BlocksUnpacked[pos])` — an unguarded dictionary lookup into the *solid* layer only.**

If a block that carries a BlockEntity is `ForFluidsLayer` (it goes into `FluidsLayerUnpacked` at :851 instead), or if its code failed to resolve and the entry was dropped at :800-804, rotating the schematic throws `KeyNotFoundException` rather than logging. Also note the rotated BE data is re-stamped with `tree.SetString("blockCode", …)` (:958) which `PlaceEntitiesAndBlockEntities` later type-checks (:1129-1148) — a BE whose block class changed between versions is silently skipped with a warning.

— `vsapi/Common/Collectible/Block/BlockSchematic.cs:945`

**`MultiblockStructure.InitForUse` takes DEGREES (`float rotateYDeg` → `mat.RotateYDeg`), and every other method throws `InvalidOperationException("call InitForUse() first")` if you skip it. `HighlightSlotId` is a hardcoded static 23.**

Passing radians gives a structure rotated by ~57°·n that silently fails to validate. And because the highlight slot is a single shared static, two multiblock structures highlighting at the same time (or a mod reusing slot 23) overwrite each other's highlights.

— `vsapi/Common/MultiblockStructure.cs:51-53, :77-80, :108-111, :30`

**Block codes are built by appending `"-" + <variant value>` for each variant group **in declaration order**; there is no key in the code, only position.**

Inserting a variantgroup anywhere but the end shifts every subsequent segment of every generated code. Wildcard patterns, `blockNumbers` tables, shape/texture `byType` keys and creative-tab filters that matched by position silently stop matching — with no error, because a wildcard that matches nothing is legal.

— `vsessentialsmod/Loading/RegistryObjectTypeLoader.cs:33-43 (ResolvedVariant.ResolveCode), :499-552 (GatherVariants)`

**Vanilla's `BlockBehaviorMultiblock` throws a raw `IndexOutOfRangeException` from `OnBlockPlaced` when the computed filler block code does not exist, and the filler naming scheme caps the structure at 5×5×5 with the controller in the middle.**

Any structure larger than 5 in any axis, or with an off-centre `cposition` that pushes an offset past ±2, crashes on placement rather than failing gracefully. This is why the vanilla behavior is unusable for large machines and a custom megablock/StructureRig is required.

— `vsessentialsmod/BlockBehavior/BehaviorMultiblock.cs:116-141, message at :135`

**`EntityPartitioning` keeps creatures and non-creatures in two separate arrays, and the un-suffixed `WalkEntities`/`GetNearestEntity` overloads search **creatures only** since 1.19.2. Also, since 1.19.2 there is no `Interactable` check inside the walk.**

A search for dropped items, projectiles or block-falling entities using the obvious overload returns nothing at all, silently. Pass `EnumEntitySearchType.Inanimate` explicitly, and do your own `e.IsInteractable` filtering (spectator players and bees are not interactable).

— `vsessentialsmod/Systems/EntityPartitioning.cs:26-33, :179-195, :264-275, :341`

**`EntityParticleSystem.SpawnParticle` and `KillParticle` throw `InvalidOperationException("Only in the entityparticle thread")` — the whole entity-particle sim runs on a private dedicated thread, and it silently no-ops while `capi.IsGamePaused`.**

You cannot spawn an entity particle from a block-entity tick or a render callback. The only legal entry point is the `OnSimTick` event (:82). Note this is a *different* system from `world.SpawnParticles(SimpleParticleProperties)`, which is safe from anywhere.

— `vsessentialsmod/Systems/ParticleEntity/EntityParticleSystem.cs:126-128, :199-216, :244, :268, :323, :386`

**`BlockSchematic` packs positions into 10 bits per axis (`PosBitMask = 0x3ff`), and `Pack` bails with a warning and `return false` — leaving the schematic half-built with `PackedOffset = (0,0,0)` — for any area ≥1024 blocks on an axis.**

The failure is a `Logger.Warning` and a bool nobody usually checks; the `BlockSchematic(world, start, end, notLiquids)` constructor (:150) ignores the return value entirely, so you get a silently empty/garbage schematic.

— `vsapi/Common/Collectible/Block/BlockSchematic.cs:132, :478-486 ; vscreativemod/Workspace.cs:829-832 (the loader-side guard)`

**`BlockSchematic.Place` skips `PlaceEntitiesAndBlockEntities` entirely when the accessor is an `IBlockAccessorRevertable`, and `PlaceEntitiesAndBlockEntities` itself returns early (before spawning any entities) when the accessor is an `IMiniDimension`.**

Pasting with a revertable accessor and not calling PlaceEntitiesAndBlockEntities + CommitBlockEntityData yourself produces blocks with completely default block-entity state — a machine with no inventory, no heat, no configuration — and no error anywhere.

— `vsapi/Common/Collectible/Block/BlockSchematic.cs:706-709, :1167`

**`AnimationUtil.GetAnimator` caches baked animation frames in a **process-global** dictionary at `api.ObjectCache["animUtil-animCache"]`, keyed only by `"animutil-" + cacheDictKey`, and the cached entry holds `RootElems`/`RootPoses` from whichever block created it first.**

Two block entities that pass the same cacheDictKey but different shapes share one skeleton — the second one animates against the first one's element tree, producing subtly wrong or wildly broken poses with no error. Include every shape-selecting variant (orientation, tier, selective elements) in the cache key.

— `vsapi/Common/Model/Animation/AnimationUtil.cs:130-180`

**`RoomRegistry`'s flood fill is bounded by a 29³ array and `MAXROOMSIZE = 14`; 'small room / cellar' means ≤7 per axis (or ≤9 with volume ≤150).**

Any enclosure larger than 14 blocks on an axis is simply not a Room — `GetRoomForPosition` reports it as open air with `ExitCount > 0`. A large industrial building will never register as an enclosed space, so heat/cellar mechanics keyed off `Room` silently do not apply to it.

— `vsessentialsmod/Systems/RoomRegistry.cs:403-409, :417-418`

**`AnimatableRenderer` registers itself on four render stages (the requested one plus `OIT`, `ShadowFar`, `ShadowNear`) but `disposeInternal` unregisters `Opaque`, `OIT`, `ShadowFar`, `ShadowNear` — hardcoded, ignoring the `renderStage` argument you passed to the constructor.**

If you construct it with a non-Opaque `renderStage` (e.g. `AfterOIT`), `Dispose()` leaves a dangling renderer registered on that stage referencing a disposed mesh ref. It survives the guard at :102 only by luck (`mtmeshrefOpaque.Disposed`), and leaks the registration for the lifetime of the client.

— `vsapi/Common/Model/Animation/AnimatableRenderer.cs:86-95 vs :194-202`

**`ShapeToPositionList.Ball(center, radius)` treats `radius` as a diameter: it uses `radInt = ceil(radius/2)` and compares against `radius*radius/4`.**

Reusing this helper (or copying its math) for a machine's area-of-effect gives you a sphere half the size you asked for. The sibling `Cuboid` is also half-open on all three axes (`while (curPos.X < finalPos.X)`, :20) so it excludes the max corner — mixing the two conventions in one feature produces off-by-one areas.

— `vscreativemod/ShapeToPositionList.cs:41-61`


## examples, templates, JSON patching, tooling

**`"op": "add"` in a VS patch is NOT RFC 6902 add and is NOT the merging add that Tavis's own readme advertises. VS maps it to AddReplaceOperation, which does a flat `token[key] = value` overwrite. The merging behaviour is a separate op, `addmerge`. Tavis's PatchDocument.Parse maps the string "add" to AddMergeOperation — but the game never calls that parser, so the readme actively misleads you.**

An `add` patch onto an existing object silently destroys any keys another mod added there — a one-way compatibility break that produces no log line at all. It also means the same patch file behaves differently if it is ever run through Tavis's own parser (e.g. in a unit-test harness) than it does in game.

— `vsessentialsmod/Loading/JsonPatchLoader.cs:532 (Add → AddReplaceOperation) vs :571 (AddMerge → AddMergeOperation); Tavis.JsonPatch/src/JsonPatch/PatchDocument.cs:64 ("add" → AddMergeOperation, dead code); Tavis.JsonPatch/readme.md:5`

**AddReplaceOperation and AddEachOperation cast the parent to JArray unconditionally when the last path token parses as an integer, even though JsonPointer.Find deliberately does not. `"path": "/someObject/12"` where someObject is a JSON object throws InvalidCastException, not PathNotFoundException.**

Reading a value at an int-like object key works fine (IntlikeKeyTests.cs proves it), so `remove` and `replace` succeed on the same path that `add` crashes on. The exception is caught by the generic handler at JsonPatchLoader.cs:485 and reported as a bare "Patch N failed" with a stack trace, giving no hint that the cause is the numeric key.

— `Tavis.JsonPatch/src/JsonPatch/Adaptors/JsonNetTargetAdapter.cs:77-78 and :101-107, vs the guarded lookup at JsonPatch/JsonPointer.cs:42`

**Writing `"side": null` explicitly in a patch makes it apply on NO side at all. The check computes targetSide from the file's asset category when Side is null, then compares the still-null `patch.Side` against `api.Side` — which is never equal — so any non-Universal category short-circuits to `continue`.**

The patch is skipped silently — it is not counted in totalCount, unmetConditionCount, notfoundCount or errorCount, and produces no log line whatsoever. The omitted-field case is safe (the field initialiser at :166 is Universal, not null), so this only bites code or tooling that emits explicit nulls.

— `vsessentialsmod/Loading/JsonPatchLoader.cs:254-255 (`EnumAppSide targetSide = patch.Side == null ? patch.File.Category.SideType : (EnumAppSide)patch.Side; if (targetSide != EnumAppSide.Universal && patch.Side != api.Side) continue;`)`

**`addmerge` onto a key that already holds a JArray throws ArgumentException("Value must be a JArray") unless your `value` is itself an array. Merging one object into an existing array of objects is not expressible.**

The natural-looking `{"op":"addmerge","path":"/behaviors","value":{"name":"…"}}` fails at runtime; you must write `"value": [{"name":"…"}]` or use `"path": "/behaviors/-"` with a plain `add`. The message names the value but not the file or path, so it is hard to attribute in a log full of patches.

— `Tavis.JsonPatch/src/JsonPatch/Adaptors/JsonNetTargetAdapter.cs:61-66`

**A single malformed patch file takes down every patch inside it. `asset.ToObject<JsonPatch[]>()` is wrapped in one try/catch around the whole array, and on failure `patches` stays null and the file is skipped entirely.**

One typo'd `op` value (Newtonsoft cannot map it to EnumJsonPatchOp) or one trailing comma silently disables the other forty patches in that file. The log says "Failed loading patches file X" once, and the missing effects surface much later as unexplained vanilla behaviour.

— `vsessentialsmod/Loading/JsonPatchLoader.cs:234-246`

**Patch application order is asset-registry order across all mods, and patches to the same file compose in-place through a shared jsonCache — but the result is only written back to asset.Data at the very end of the run, and `IsPatched` is only set for files that had at least one successful op.**

Two mods patching the same path produce a last-writer-wins outcome that depends on mod load order, with no diagnostic. And because a file that only ever failed its patches never gets IsPatched=true, it stays eligible for unloading — a subtle difference in behaviour between a patch that errored and one that applied.

— `vsessentialsmod/Loading/JsonPatchLoader.cs:217 (GetMany("patches/")), :188 and :430-458 (jsonCache), :342-368 (deferred write-back), vsapi/Common/Assets/IAsset.cs:62 (IsPatched keeps the asset from being unloaded)`

**ModDB's version regex is much stricter than anything the game enforces, and it is applied to the mod Version, the NetworkVersion, and every dependency version: `^\d{1,5}\.\d{1,4}\.\d{1,4}(?:-(?:rc|pre|dev)\.\d{1,4})?$`. Only rc/pre/dev prereleases, only a numeric suffix, exactly three numeric components, no build metadata.**

`1.0.0-alpha`, `1.0.0-rc1` (no dot), `1.0.0.1`, and `1.0.0+build5` all pass the game's loader but are rejected at upload with MalformedPrimaryVersion, and the offending field is then nulled out. A dependency with a bad version is dropped from the list outright (:187), so the upload can succeed with a silently missing dependency.

— `modpeek/LibModPeek/Validate.cs:197-202, applied at :68, :82 and :184`

**modpeek treats any unrecognised modinfo.json key as an error, and the published schema sets `additionalProperties: false`. `backgroundPaths` is a genuine `[JsonProperty]` on ModInfo that the game honours, but it is in neither list.**

Using a documented ModInfo feature fails ModDB validation. The accepted key set is exactly: custom, $schema, name, modid, version, networkversion, texturesize, side, type, requiredonclient, requiredonserver, iconpath, description, website, authors, contributors, dependencies. `custom` is the sanctioned escape hatch — it is an object that is explicitly exempt from all validation (ParseJSON.cs:24-26).

— `modpeek/LibModPeek/ParseJSON.cs:242-246 (default → UnexpectedProperty); vsmoddb/web/schema/modinfo.v2.rc3.json:79; the field itself at vsapi/Common/API/ModInfo.cs:91-92`

**The published modinfo schema contradicts its own documentation on dependency versions: its `oneOf` accepts only a full version string or the literal `"*"`, while the description text in the same file says "You can set the value to an empty string or an asterisk". The game and modpeek both accept `""`; a JSON-schema-aware editor flags it.**

Since our modinfo.json files carry `$schema`, an editor will red-squiggle a perfectly valid `""`. Prefer `"*"` for 'any version'. Related: the schema's dependency key pattern is `^[a-z]+$` (:57), which rejects digits — but ModInfo.IsValidModID allows digits after the first character, so a dependency on a modid like `exlib2` would be schema-invalid yet accepted everywhere else.

— `vsmoddb/web/schema/modinfo.v2.rc3.json:59-65 (oneOf) vs :65 (description); modpeek normalises both "" and "*" to null at modpeek/LibModPeek/Validate.cs:180-183; the official examples ship `"game": ""` (vsmodexamples/Code Mods/VSTutorial - 1 …/VSTutorial/modinfo.json:9)`

**A dependency version in modinfo.json is a MINIMUM, never a pin. The game will happily load a mod declaring `"exlib": "0.1.0"` against exlib 3.0.0.**

There is no upper bound mechanism at all. `"game": "1.22.0"` in src/SteelmakingExpanded/modinfo.json does not stop the mod loading on 1.23; breaking-change protection has to come from NetworkVersion (for multiplayer) or from a runtime check in code. This is the same trap recorded in our moddb-api-and-release-checks note.

— `vsapi/Common/API/ModDependency.cs:15-19 ("The minimum version requirement of this dependency"); version comparison helpers at vsapi/Config/GameVersion.cs:195-235 (IsAtLeastVersion / IsNewerVersionThan / IsLowerVersionThan)`

**The CakeBuild PackageTask calls `CleanDirectory("../Releases")` before every package, deleting the entire Releases folder — not just the current mod's output.**

In a monorepo where several mods share one Releases directory (which is exactly our dist/Releases/<gameversion>/<modid>/ layout), packaging one mod wipes the previously built zips of the others. Any per-mod adaptation of this script must scope the clean to Releases/<modid>.

— `VSdotnetModTemplates/VSModTemplates/templates/VintageStoryMod/CakeBuild/Program.cs:98-99`

**The official templates and examples have ZERO multi-version support. Every csproj is single-`<TargetFramework>` with an unconditional `$(VINTAGE_STORY)` HintPath; there is no `TargetFrameworks`, no `DefineConstants`, and no `Condition` on any Reference anywhere in either repo.**

There is no upstream convention to converge on for our 1.21/1.22 dual-target build — our `-p:Legacy=true` + src/LegacyUsings.cs approach is unopposed, but also unsupported. Note the template's own TFM moved 7→8→10 across releases, so a template regenerated today targets net10.0 and will not load into a 1.21 client.

— `grep across VSdotnetModTemplates and vsmodexamples for TargetFrameworks|DefineConstants|Condition= on references returns only Condition="Exists('modicon.png')" at VSdotnetModTemplates/VSModTemplates/templates/VintageStoryMod/_ProjectName_/_ProjectName_.csproj:78; TFM values are net10.0 (templates), net8.0 (9 current tutorials), net7.0 (10 old_code_mods)`

**The `vsInstall` template symbol is implemented as a literal text replacement of the string `$(VINTAGE_STORY)` throughout the generated files, not as an MSBuild property override.**

Passing `--vsInstall` rewrites the HintPaths, launchSettings and (in the DLL template) nothing else — but it also means the generated project no longer responds to the VINTAGE_STORY environment variable at all, and the hard-coded path breaks for every other developer on the project. Leave the default.

— `VSdotnetModTemplates/VSModTemplates/templates/VintageStoryMod/.template.config/template.json:21-27 (`"replaces": "$(VINTAGE_STORY)"`)`

**Tavis's `test` operation is broken: it compares the value found at `path` against the whole document root instead of against `operation.Value`, so it throws for any non-trivial document.**

Harmless today because EnumJsonPatchOp has no Test member and ModJsonPatchLoader.CreateOperation can never construct one — but it is a trap for anyone using Tavis.JsonPatch directly (e.g. in a test fixture that builds PatchDocuments by hand) and a reason not to trust the library's non-VS code paths.

— `Tavis.JsonPatch/src/JsonPatch/Adaptors/JsonNetTargetAdapter.cs:146-151 (`if (!existingValue.Equals(_target)) throw …`)`

**JsonPointer percent-decodes every path token with Uri.UnescapeDataString before applying the RFC ~1/~0 unescaping.**

A JSON key containing a percent-escape-shaped substring is silently rewritten — `"path": "/byType/foo%2Fbar"` resolves to the key `foo/bar`, not `foo%2Fbar`. Non-standard versus RFC 6901 and invisible in the error message when it fails.

— `Tavis.JsonPatch/src/JsonPatch/JsonPointer.cs:78-81`

**The legacy `ModJsonPatchLoader.ApplyPatch` overload omits the Value null-check on the AddMerge branch that the current CreateOperation has.**

Any external caller of the public ApplyPatch(...) API with an addmerge patch that has no `value` gets a NullReferenceException rather than the intended "AddMerge operation requires Value" error. Only reachable from third-party code, since ApplyPatches() no longer routes through it.

— `vsessentialsmod/Loading/JsonPatchLoader.cs:684-686 (`op = new AddMergeOperation() { … Value = jsonPatch.Value.Token }`) vs the guarded version at :563-571`

**`Condition` with `useValue: true` mutates the shared JsonPatch object by assigning `patch.Value` from the world config, and a `Condition.When` key that is simply absent from the world config skips the patch without counting it anywhere.**

A typo in the `when` key makes the patch vanish with no error, no warning, and no contribution to the unmetConditionCount that the summary line reports — indistinguishable from the patch file not existing. Only the isValue mismatch path (:269) logs a VerboseDebug line.

— `vsessentialsmod/Loading/JsonPatchLoader.cs:258-274 (`if (attr == null) continue;` at :261, `patch.Value = new JsonObject(...)` at :265)`

**A patch `path` of `""` or one not starting with `/` produces a zero-token or misaligned pointer that fails with an index exception rather than a path error.**

`"path": ""` gives Depth 0, so Find(skipLast:true) returns the root and then `Path.Last` throws ArgumentOutOfRangeException. `"path": "attributes/foo"` (missing leading slash) drops the first segment entirely and looks for `foo` at the root. Both surface as generic caught exceptions with no mention of the path being malformed.

— `Tavis.JsonPatch/src/JsonPatch/JsonPointer.cs:24-32 (Split('/').Skip(1)) and :16 (`Last => Tokens[Depth - 1]`)`


## vsmoddb — the ModDB site and its public API

**Live production emits its error message under the key `error`; this vendored checkout writes `reason`. Verified 2026-08-10: `GET /api/v2/mods/99999999/releases` -> `{"error":"Mod not found or not released."}` while the source at that line says `['reason' => 'Mod not found or not released.']`. The README documents neither.**

A client written from this source that reads `body.reason` will log `undefined` for every v2 error. Read `body.error ?? body.reason`. More broadly: the checkout (commit f73e881, ~2026-05) is behind production — verify any v2 detail against the live endpoint before relying on it.

— `vsmoddb/lib/api/public/mods.php:254`

**The retraction query parameter is `ignore-retractions` (plural) in the code, everywhere — install-information, the releases list, and releases/latest. The README documents it as `ignore-retraction` (singular) in all three places.**

Following the official docs silently does nothing: the parameter is read with `$_GET['ignore-retractions'] ?? false`, so the misspelled one is just ignored and retracted releases stay hidden with no error.

— `vsmoddb/lib/api/public/mods.php:27`

**Truthiness of those flags is PHP `boolval`, so `ignore-retractions=false`, `hosted-mode=no` and `hosted-mode=off` all evaluate to TRUE. Only `0`, the empty string, or omitting the parameter are false.**

`?hosted-mode=false` makes every requested id come back as `errorCode: 4031` (forbidden in hosted mode) with no downloads at all — the exact opposite of the intent.

— `vsmoddb/lib/api/public/mods.php:28`

**`GET /api/v2/mods/{modId}/releases/{releaseId}` ignores `{modId}` when looking up the release. The numeric branch OVERWRITES the where-clause (`$queryWhere = 'r.releaseId = '.$releaseId;`) instead of appending, so modId is used only for an existence check.**

Verified live: `/api/v2/mods/9254/releases/52367` (smex's modId, exlib's releaseId) returns the exlib release with HTTP 200. Any code that trusts the path modId to scope the answer will happily attribute another mod's release to yours.

— `vsmoddb/lib/api/public/mods.php:308`

**Every v1 response is HTTP 200, including failures. `fail()`/`good()` in the v1 entry point only json-encode a `statuscode` field; neither ever calls `http_response_code()`.**

Verified: `/api/mod/doesnotexist99` -> HTTP 200, body `{"statuscode":"404"}`. `curl -f`, `response.raise_for_status()` and `if response.ok` are all useless against v1. The 410-Gone `/api/changelogs` is also served as HTTP 200.

— `vsmoddb/lib/api/v1/entry.php:8`

**`/api/comments/{id}` takes an **assetId**, not a modId, and the two are numerically unrelated (smex is modId 9254 / assetId 53606).**

Passing a modId does not error — `intval(...) > 0` is true, the WHERE just matches a different asset or nothing, and you get someone else's comments or an empty list with statuscode 200. Always resolve the assetId from `/api/mod/{identifier}` first.

— `vsmoddb/lib/api/v1/logic.php:59`

**`/api/mods` array filters must use bracket syntax. `tagids` and `gameversions` are iterated with `foreach`; a scalar value makes PHP 8 warn and skip the loop, so the filter is silently dropped.**

Verified live: `?text=Steelmaking&tagids=4` returns mods that do not carry tag 4, with statuscode 200. `?tagids%5B%5D=4` returns 4 correctly filtered mods. A wrong-shaped filter is indistinguishable from a broad result set.

— `vsmoddb/lib/api/v1/functions.php:172`

**`gameversion` and `gv` parse with DIFFERENT functions: `gameversion` goes through `compilePrimaryVersion` (major.minor ONLY) and `gv`/`gameversions[]` through `compileSemanticVersion` (major.minor.patch ONLY). Each returns `false` for the other's format, and `false` is then interpolated straight into the SQL as `0`.**

`?gameversion=1.22.6` matches nothing (no error), and `?gv=1.22` matches nothing. Both look like "there are no mods for that version". Verified: `gameversion=1.22` + text=Steelmaking -> 7 mods, `gv=1.22.6` + same text -> 5.

— `vsmoddb/lib/api/v1/functions.php:143`

**v1 `/api/mod` `release.mainfile` is a direct Bunny CDN url with `?dl=`, not the tracked `/download/…` path, and there is no dedicated field for the tracked one. v2 returns the tracked path in `fileUrl`.**

Any tool that fetches `mainfile` (installers, mirrors, CI) never increments the download counters and, critically, is never blocked by a retraction — `download.php` is where the 410 for retracted releases lives.

— `vsmoddb/lib/api/v1/functions.php:65`

**In `install-information`, `formatDownloadTrackingUrl` is called without checking whether the release actually has an attached file, unlike the release-detail endpoint which guards with `$release['fileId'] ? … : null`.**

A release row with no file (possible: `files.assetId` is nullable and files can be deleted) yields `"fileUrl": "/download//"` — a well-formed-looking but broken url instead of a null or an error code.

— `vsmoddb/lib/api/public/mods.php:143`

**`GET /api/v2/game-versions` is registered in the AUTHENTICATED router, so it 401s for anonymous callers even though it returns nothing but public data. The README flags this as a bug and it is still live.**

Verified: HTTP 401 `{}`. Use the v1 `/api/gameversions` endpoint instead, remembering that its `tagid` is the NEGATED 64-bit compiled version (e.g. `-281492156858370` for `1.4.4-dev.2`).

— `vsmoddb/lib/api/authenticated/_routing.php:38`

**`mods.lastModified` is bumped by download-counter writes, not by content edits, and the API exposes it as the mod's `lastmodified`. The source calls this out and refuses to fix it for compatibility.**

Using `lastmodified` as a change-detection or cache key means re-fetching constantly for popular mods and never noticing a description edit on a quiet one. Use `lastreleased` for release changes.

— `vsmoddb/lib/api/v1/functions.php:127`

**`modinfo.json` `dependencies` is never exposed by any endpoint of either API version. It is stored verbatim as an unparsed TEXT blob in `modPeekResults.rawDependencies`; the normalised table that would make it queryable exists only as a commented-out block in the schema.**

There is no way to ask ModDB "what does mod X depend on" or "who depends on exlib". Building a dependency graph requires downloading every zip and reading modinfo.json yourself. The one derived signal — the minimum `game@` version — is consumed once at upload time to pre-tick checkboxes and is never stored as such.

— `vsmoddb/db/000_tables.sql:124`

**A dependency version in modinfo is a MINIMUM, never a pin, and an unversioned or `*` dependency is stored as version 0 meaning "any".**

Bumping our declared `exlib` dependency does not stop older exlib builds from loading; it only refuses ones below the floor. Conversely, the game-version pre-tick derived from `game@X` selects EVERY known version >= X, including future ones that did not exist at upload — so a release can claim compatibility with versions it was never tested against.

— `vsmoddb/lib/modinfo.php:3`

**A mod may publish several distinct identifiers under one modId — `modReleases` is UNIQUE on `(modId, identifier, version)`, and the v1 list returns `modidstrs` as an array.**

`/api/v2/mods/{id}/releases/latest` without `?identifier=` returns the highest VERSION across all identifiers, which can be a different mod-of-the-mod than you meant. Real examples exist in production (`laborostoolsmetalcompat` + `laborostoolsmeteoricsteeladdon` under modId 10120). Always pass `identifier` when a modId hosts more than one.

— `vsmoddb/db/000_tables.sql:265`

**In v1 payloads, GROUP_CONCAT nulls become a one-element array containing the empty string, and a null game-version list decodes to the version `"0.0.0"`.**

A mod with no tags yields `"tags": [""]` rather than `[]`, and a release with no compatible game versions (tool/other category mods never get compat rows at all) yields `"tags": ["0.0.0"]`. Filter empty strings and treat `0.0.0` as "unspecified", not as a real version.

— `vsmoddb/lib/api/v1/functions.php:69`

**The `created`/`lastreleased` timestamp format differs between API versions: v1 returns MySQL `DATETIME` strings in the server's timezone with no offset (`"2026-08-09 23:16:11"`), v2 returns a UNIX epoch integer.**

Parsing the v1 string as UTC skews every timestamp by the server offset, and there is nothing in the payload to detect it. Cross-referencing a v1 `created` with a v2 `created` needs an explicit conversion.

— `vsmoddb/lib/api/public/mods.php:315`

**The user-tag endpoints (`POST /mods/{id}/tags`, `PUT /mods/{id}/tags/{tagId}/vote`) are shipped but hard-disabled: `DISABLE_USER_TAGS` defaults to true and the handler answers 503 before doing anything.**

The README documents them as working (200 on success) with no mention of the kill switch. Any tag automation will get 503 `{"reason":"User tags are currently disabled."}` forever.

— `vsmoddb/lib/config.php:62`

**There is no rate limiting anywhere — no 429 code path in PHP, no `limit_req` in the nginx config — and no CORS headers either.**

Nothing will tell you that you are hammering the service; the only self-defence is your own backoff. And because no `Access-Control-Allow-Origin` is ever sent, the API cannot be called from any browser page, so anything client-side needs a server-side proxy.

— `vsmoddb/docker/moddb.conf:20`

**PHP fatals and uncaught exceptions on an API route render the HTML error page, not JSON — `lib/ErrorHandler.php` has no API-aware branch (no reference to `api`, `json` or `Content-Type` anywhere in it).**

A JSON client can receive a full HTML document with an arbitrary status. Always guard `json.loads` and inspect the first byte before parsing.

— `vsmoddb/lib/api/v2.php:63`

**`/api/v2/mods/{id}/releases/all` and `/releases/new` are documented as `400: Not implemented`, but `all` actually falls into the numeric-releaseId branch and fails validation.**

Verified live: `{"error":"Malformed releaseId."}` with HTTP 400. Anyone probing for a bulk endpoint will read that message as their own bug rather than as "this route does not exist". The real list endpoint is `/releases` with no suffix.

— `vsmoddb/lib/api/public/mods.php:310`
