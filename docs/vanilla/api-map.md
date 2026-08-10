# Vanilla source map

Where things live in the vendored Vintage Story source at `.compat/vintagestory/`. Written so a
question about the game's own code can be answered by opening one file rather than searching the
tree. Paths are relative to `.compat/vintagestory/`; line numbers are from the vendored revisions
listed in [README.md](README.md).


## Contents

- [vsapi — blocks, block entities, items](#vsapi-blocks)
- [vsapi — math, datastructures, API surfaces, client/server](#vsapi-core)
- [vssurvivalmod — mechanical power](#vssurvival-mp)
- [vssurvivalmod — blocks, heat, recipes, handbook](#vssurvival-blocks)
- [vsessentialsmod + vscreativemod — render, animation, sound, schematics](#vsessentials-creative)
- [examples, templates, JSON patching, tooling](#examples-tooling)
- [vsmoddb — the ModDB site and its public API](#moddb)


---

<a id="vsapi-blocks"></a>

# vsapi — blocks, block entities, items

> All paths below are relative to `.compat/vintagestory/`. Line numbers are from **vsapi @ v1.22.5** / **vssurvivalmod @ v1.22.5** as vendored.

## ⚠ Correction to the assignment's premise

The assignment said `Common/Entity/` holds `BlockEntity` + `BlockEntityBehavior` + `BlockEntityContainer` + `BlockEntityOpenableContainer` + `BlockEntityDisplay`. **None of that is true.**

| Type | Actually lives at |
|---|---|
| `BlockEntity` | `vsapi/Common/Collectible/Block/BlockEntity.cs:17` |
| `BlockEntityBehavior` | `vsapi/Common/Collectible/Block/BlockEntityBehavior.cs:48` |
| `BlockEntityBehaviorType` (the JSON DTO) | `vsapi/Common/Collectible/Block/BlockEntityBehavior.cs:26` |
| `BlockEntityContainer` | **`vssurvivalmod/BlockEntity/BEContainer.cs:15`** (NOT in vsapi) |
| `BlockEntityOpenableContainer` | **`vssurvivalmod/BlockEntity/BEOpenableContainer.cs:81`** |
| `BlockEntityDisplay` | **`vssurvivalmod/BlockEntity/BEContainerDisplay.cs:51`** |
| `IBlockEntityContainer` (the only vsapi part) | `vsapi/Common/Collectible/IBlockEntityContainer.cs:7` |

`vsapi/Common/Entity/` holds **entities** (`Entity`, `EntityAgent`, `EntityPlayer`, `EntityItem`, `EntityBehavior`, `EntityPos`, `EntityProperties`, `SpawnConditions`, `EntityControls`, `PlayerAnimationManager`, `EntityTalkUtil`, plus `Physics/` and `Player/` subdirs). Nothing block-entity related.

Practical consequence: **subclassing `BlockEntityContainer` creates a hard dependency on `VSSurvivalMod.dll`, not just `VintagestoryAPI.dll`.**

---

## `vsapi/Common/Collectible/` — the CollectibleObject layer

Shared base for Block + Item.

| File | What is in it |
|---|---|
| `Collectible.cs` (3735 ln) | `CollectibleObject : RegistryObject` — the fat base class. See "key types". |
| `CollectibleBehavior.cs` (500 ln) | `CollectibleBehavior` base (:18). `Initialize(JsonObject)` :44, `OnLoaded(ICoreAPI)` :54, `ClientSideOptional` :33. All held-item hooks with `ref EnumHandling`. |
| `ItemStack.cs` (501 ln) | `ItemStack : IItemStack`. |
| `IItemStack.cs` | read-only stack interface |
| `JsonItemStack.cs` | JSON-declared stack (`type`/`code`/`quantity`/`attributes`), `.Resolve(world, src)` |
| `ModelTransform.cs` | `ModelTransform` — gui/fp/tp/ground transforms; `.EnsureDefaultValues()` |
| `ModelTransformKeyFrame.cs` | held-hit anim keyframes |
| `CombustibleProperties.cs` | `BurnTemperature`, `BurnDuration`, `MeltingPoint`, `MeltingDuration`, `SmeltedStack`, `SmeltedRatio`, `SmeltingType` |
| `NutritionProperties.cs`, `TransitionableProperties.cs`, `GrindingProperties.cs`, `CrushingProperties.cs`, `FertilizerProps.cs` | JSON property blocks hung off `CollectibleObject` |
| `HeldSounds.cs` | idle/equip/unequip/attack sounds |
| `CreativeTabAndStackList.cs` | `creativeinventory` JSON entry |
| `DecorFlags.cs`, `DecorSelectionBox.cs` | decor bit semantics |
| `EnumItemClass.cs` | `Block` / `Item` |
| `EnumItemUseCancelReason.cs` | `ReleasedMouse`, `Death`, `Destroyed`, `Death`, … |
| `EnumRandomizeAxes.cs`, `EnumTool.cs` | |
| `IBlockEntityContainer.cs` | `Inventory`, `InventoryClassName`, `DropContents`, `CheckInventoryClearedMidTick` |
| `ICollectibleDisplayable.cs`, `IResolvableCollectible.cs` | display transform / handbook-drop resolution hooks |

## `vsapi/Common/Collectible/Block/` — the block layer

| File | What is in it |
|---|---|
| **`Block.cs` (2927 ln)** | `Block : CollectibleObject` :28. The single most important file. Map below. |
| **`BlockEntity.cs` (24 KB)** | `BlockEntity` :17 — full lifecycle. Map below. |
| **`BlockEntityBehavior.cs`** | `BlockEntityBehaviorType` :26 (JSON), `BlockEntityBehavior` :48 |
| **`BlockBehavior.cs` (392 ln)** | `BlockBehavior : CollectibleBehavior` :17 — every hook takes `ref EnumHandling` |
| **`StrongBlockBehavior.cs` (101 ln)** | `StrongBlockBehavior : BlockBehavior` :12 — collision/selection/solidity/lightabsorption/worldgen hooks. **Only invoked if the block class is `BlockGeneric` or a subclass.** |
| **`BlockGeneric.cs` (382 ln)** | `BlockGeneric : Block` :9 — the only vanilla block class that dispatches `StrongBlockBehavior` |
| `BlockSelection.cs` (159 ln) | `BlockSelection` :15 — `Position`, `Face`, `HitPosition`, `SelectionBoxIndex`, `SelectionBoxId`, `DidOffset`, `Block`; `FullPosition` :69; `ToDecorIndex()` :123 |
| `BlockDropItemStack.cs` | `BlockDropItemStack` :37 — `Quantity` (NatFloat), `LastDrop` :68, `Tool` :74, `ResolvedItemstack` :79, `DropModbyStat` :85, `Resolve()` :111, `ToRandomItemstackForPlayer()` :229. `WeightedBlockDropItemstack` :10 |
| `BlockSounds.cs` | `Walk/Inside/Break/Place/Hit` as `SoundAttributes`, `ByTool` dict :87, `GetBreakSound(IPlayer)` :125 |
| `BlockSchematic.cs` (62 KB) | schematic (de)serialization; `TransformWhilePacked` calls `be.CreateBehaviors` then `IRotatable.OnTransformed` at :950-961 |
| `VertexFlags.cs` (17 KB) | bit layout: `GlowLevelBitMask` 0xFF :155, `ZOffsetBitPos` 8 :157, `ReflectiveBitMask` 1<<11 :166, `Lod0BitMask` :170, `NormalBitPos` 13 :172, `WindModeBitsPos` 25 :183, `WindDataBitsPos` 29 :191. Props `GlowLevel` :379, `ZOffset` :393, `WindMode` :449. `EnumWindBitModeMask` consts :88-122 |
| `DecorBits.cs` | packing face+subposition into the decor index |
| `EnumBlockMaterial.cs`, `EnumMatterState.cs` | |
| `BlockPatchAttributes.cs` | worldgen patch config |
| `ConnectorMetaData.cs` | dungeon/structure connector struct (namespace `Vintagestory.Common.Collectible.Block`) |
| `IRotatable.cs` | `OnTransformed(world, tree, degreeRotation, oldBlockIdMapping, oldItemIdMapping, flipAxis)` :24 — **called with the BE NOT in the world**. Also `IMaterialExchangeable` :29 |
| `IWrenchOrientable.cs` | `Rotate(EntityAgent, BlockSelection, int dir)` — namespace `Vintagestory.GameContent` |
| `IExternalTickable.cs` | `SetExternallyTicked/SetInternallyTicked/OnExternalTick` — namespace `Vintagestory.Common.Collectible.Block` |
| `ICoolingMedium.cs` | `CanCool/CoolNow` — namespace `Vintagestory.GameContent` |
| `IAcceptsDecor.cs`, `IBlockFlowing.cs`, `ICustomSelectionBoxRender.cs`, `IDrawYAdjustable.cs`, `IGeometryTester.cs`, `IHarvestable.cs`, `ILookAwarePlacement.cs`, `IWithDrawnHeight.cs` | small marker interfaces |
| `Crop/` | `BlockCropProperties.cs`, `CropBehavior.cs`, `Farmland.cs` |

### `Block.cs` navigation map

**Fields (all public, JSON-populated)**: `BlockId` :66 · `DrawType` :71 · `RenderPass` :76 · `WalkSpeedMultiplier` :86 · `PartialSelection` :96 · `Sounds` :101 · `VertexFlags` :106 · `LightAbsorption` :116 · `PlacedPriorityInteract` :121 · `Replaceable` :134 · `Fertility` :140 · `RequiredMiningTier` :145 · `Resistance` :150 · `BlockMaterial` :155 · texture-bleed block :160-196 (new) · `RandomDrawOffset` :216 · `ShapeInventory` :235 · `Shape` :240 · `Lod0Shape`/`Lod2Shape` :246/:251 · `Textures` :261 · `TexturesInventory` :274 · `SideOpaque` :284 · `SideSolid` :289 · `SideAo` :294 · `EmitSideAo` :299 · `AllowSpawnCreatureGroups` :304 · `FaceCullMode` :310 · `ClimateColorMap` :315 · `SeasonColorMap` :321 · `CollisionBoxes` :337 · `SelectionBoxes` :342 · `ParticleCollisionBoxes` :347 · `Climbable` :352 · `LiquidLevel` :362 · `LiquidCode` :368 · **`BlockBehaviors` :379** · **`BlockEntityBehaviors` :384** · `Drops` :389 · `SplitDropStacks` :394 · `CropProps` :399 · **`EntityClass` :404** · `PushVector` :411 · `DecorThickness` :424 · `InteractionHelpYOffset` :426

**Statics**: `DefaultCubeShape` :30 · `DefaultCollisionBox` :35 · `DefaultCollisionSelectionBoxes` :41 · `miningTierNames` :442 · `SuggestedHVOrientation(byPlayer, blockSel)` :1912

**Lifecycle / placement**: `OnLoaded` :456 · `TryPlaceBlockForWorldGen` :841 · `TryPlaceBlock` :882 → `CanPlaceBlock` :921 → `DoPlaceBlock` :976 (`SetBlock` at :998) · `OnBlockPlaced` :1379 (spawns BE at :1381-1384 **before** behaviors) · `OnBlockRemoved` :1350 (removes BE at :1365-1368 **after** behaviors) · `OnNeighbourBlockChange` :1409 · `OnExchanged` → see BE

**Breaking / drops**: `OnGettingBroken` :1028 (client-only, 40 ms) · `OnBlockBroken` :1090 · `SpawnDropsAndRemoveBlock` :1106 · `SpawnBlockBrokenParticles` :1149 · `OnBrokenAsDecor` :1169 · `GetDropsForHandbook` :1219 · `GetHandbookDropsFromBreakDrops` :1252 · `GetDrops` :1276 · `OnPickBlock` :1319 · `OnBlockExploded` :2509/:2523 · `GetBlastResistance` :2483

**Interaction**: `OnBlockInteractStart` :1436 · `Activate` (command-block path) :1472 · `OnBlockInteractStep` :1495 · `OnBlockInteractStop` :1525 · `OnBlockInteractCancel` :1553 · `OnBeingLookedAt` :1009 · `OnEntityInside` :1582 · `OnEntityCollide` :1598 · `OnFallOnto` :1630 / `CanAcceptFallOnto` :1644

**Info / HUD**: `GetPlacedBlockInteractionHelp` :2179 · `GetPlacedBlockName` :2251 · `GetPlacedBlockInfo` :2268 · `AddMiningTierInfo` :2337/:2348 · `GetRequiredMiningTier` :2365 · `GetHeldItemInfo` :2377 · `AddExtraHeldItemInfoPostMaterial` :2411

**Geometry / render**: `GetSelectionBoxes` :608 · `GetCollisionBoxes` :632 · `GetParticleCollisionBoxes` :643 · `GetParticleBreakBox` :587 · `SideIsSolid` :554/:562 · `ShouldMergeFace` :574 · `DoEmitSideAo` :709 · `GetLightAbsorption` :719/:724 · `GetDecal` :750 · `OnDecalTesselation` :1791 · `OnJsonTesselation` :1820 · `DetermineTopMiddlePos` :1843 · `OnCollectTextures` :2443 · `GetRandomColor` :2579/:2611 · `GetColor` :2624 · `GetColorWithoutTint` :2642 · `GetSelectionColor` :2432 · `DoPartialSelection` :2421 · `LoadTextureSubIdForBlockColor` :486

**Ticking**: `ShouldReceiveClientParticleTicks` :1659 · `OnAsyncClientParticleTick` :1716 (every 25 ms, ≤32 blocks) · `ShouldReceiveServerGameTicks` :1750 (off-thread!) · `OnServerGameTick` :1772 (main thread) · `GetAmbientSoundStrength` :1703

**Rotation / worldedit**: `GetRotatedBlockCode(int angle)` :2056 · `GetVerticallyFlippedBlockCode` :2084 · `GetHorizontallyFlippedBlockCode(EnumAxis)` :2114

**Lookup helpers**: `GetBehavior(Type, bool)` :2145 · `GetBlockEntity<T>(BlockSelection)` :2668 · `GetBlockEntity<T>(BlockPos)` :2679 · `GetBEBehavior<T>` :2690 · **`GetInterface<T>` :2706** (Block → BlockBehavior → BlockEntity → BlockEntityBehavior, in that order) · `HasBlockBehavior<T>` :2813 · `HasBehavior<T>` :2824 · `Clone()` :2730 · `FreeRAMServer()` :2912

### `BlockEntity.cs` navigation map

`TickHandlers`/`CallbackHandlers` :19-20 · `Api` :25 · `Pos` :30 · `Block { get; set; }` :35 · `Behaviors` :40 · `stackForWorldgen` :42 · `GetBehavior<T>` :52 · **`Initialize(ICoreAPI)` :72** · **`CreateBehaviors(Block, IWorldAccessor)` :103** · `RegisterGameTickListener` :130 & :146 · `UnregisterGameTickListener` :159 · `UnregisterAllTickListeners` :165 · `RegisterDelayedCallback` :182 · `UnregisterDelayedCallback` :195 · `TickingExceptionHandler` :201 · **`OnBlockRemoved()` :212** · **`OnExchanged(Block)` :232** · `OnBlockBroken(IPlayer)` :242 · `HistoryStateRestore()` :253 · **`OnBlockUnloaded()` :261** · **`OnBlockPlaced(ItemStack)` :290** · **`ToTreeAttributes` :302** · **`FromTreeAttributes` :332** · `OnReceivedClientPacket` :355 · `CachedAccessPerms` struct :367-428 · `OnReceivedServerPacket` :436 · **`MarkDirty(bool, IPlayer)` :451** · `GetBlockInfo` :468 · `OnStoreCollectibleMappings` :482 · `OnLoadCollectibleMappings` :515 · **`OnTesselation` :532** · `OnPlacementBySchematic` :554

## `vsapi/Common/Collectible/Item/`

`Item.cs` (179 ln) — `Item : CollectibleObject` :16; `ItemId` :31, `Shape` (CompositeShape, nullable → voxelized-from-texture) :36, `Textures` :43, `GetHeldItemInfo` :74, `Clone` :103, `FreeRAMServer` :168.
`EnumItemStorageFlags.cs` · `EnumItemDamageSource.cs`.

## `vsapi/Common/Model/`

### `Shape/`
| File | Contents |
|---|---|
| `Shape.cs` (808 ln) | `Shape` :16 — `Textures` :23, `Elements` :29, `Animations` :35, `TextureWidth/Height` :43/:49, `JointsById` :55. `ResolveReferences` :67/:72, `CollectAndResolveReferences` :84/:95, `SubclassForStepParenting` :144, `StepParentShape` :214/:229, `CollectElements` :372, `ResolveAndFindJoints` :408/:413, **`static TryGet(api, path)` :512/:534**, `WalkElements(wildcardpath, …)` :548, `FindElement(wildcard)` :585, `GetElementByName` :600, `RemoveElements` :622, `CloneElements` :671, `CacheInvTransforms` :700, `Clone` :718, `InitForAnimations` :738/:743, `FreeRAMServer` :769 |
| `ShapeElement.cs` (17 KB) | `ShapeElement` :13 — `Name` :25, `From`/`To` :29/:31, `Faces`/`FacesResolved` :47/:52, `RotationOrigin` :58, `RotationX/Y/Z` :64-76, `ScaleX/Y/Z` :82-94, `RenderPass` :101, `ZOffset` :103, `Children` :114, `AttachmentPoints` :120, `StepParentName` :126, `ParentElement` :131, `JointId` :136. `GetLocalTransformMatrix(animVersion, …)` :286 (v0 = R·S·T, v1 = T·S·R), `Clone` :364, `WalkRecursive` :435 |
| `CompositeShape.cs` (368 ln) | `CompositeShape` :47 — `Base` :53, `Format` :59, `rotateX/Y/Z` :71-83, `offsetX/Y/Z` :89-101, `Scale` :107, `Alternates` :116, `BakedAlternates` :121, `Overlays` :127, `VoxelizeTexture` :133, `QuantityElements` :139, `SelectiveElements` :145, `IgnoreElements` :150. `Clone` :183, `CloneWithoutAlternates` :206, `Bake` :252 |
| `ShapeElementFace.cs` | `Texture`, `Uv`, `Rotation`, `Glow`, `ReflectiveMode`, `WindMode`, `Enabled` |
| `AttachmentPoint.cs` | `Code`, `PosX/Y/Z`, `RotationX/Y/Z` |
| `IBlockShapeSupplier.cs` | legacy |

### `Animation/`
`AnimationUtil.cs` :10 (`IRenderer`; `InitializeShapeAndAnimator` :38, `InitializeAnimator` :59, `InitializeAnimatorServer` :71, `AnimationTickServer` :79, `OnRenderFrame` :89, `StartAnimation` :105, `StopAnimation` :124, `static GetAnimator` :130) · `AnimatableRenderer.cs` :12 (`ShouldRender` :29, `ModelMat` :26, `CustomTransform` :27, `rotationDeg` :33, `ScaleX/Y/Z` :35-37) · `AnimationMetaData.cs` (`Code`, `Animation`, `AnimationSpeed`, `Weight`, `BlendMode`, `EaseIn/OutSpeed`, `TriggeredBy`, `ElementWeight`) · `AnimatorBase.cs` · `ClientAnimator.cs` · `ServerAnimator.cs` · `AnimationManager.cs` · `NoAnimationManager.cs` · `RunningAnimation.cs` · `ElementPose.cs` · `AnimationCache.cs` · `IAnimator.cs` · `EntityHeadController.cs` · `AnimationSound.cs` · `Animation.cs` · `AnimationKeyFrame(Element).cs` · `AnimationJoint.cs` · `AnimationFrame.cs`

## `vsapi/Client/` — mesh & tesselation

| Path | Contents |
|---|---|
| `Client/Model/Mesh/MeshData.cs` (2169 ln) | `MeshData` :16. Arrays: `xyz` :36, `Flags` :41, `Normals` :56, `Uv` :61, `Rgba` :66, `Indices` :71, `TextureIndices` :76, `TextureIds` :31, `XyzFaces` :215, `ClimateColorMapIds` :231, `SeasonColorMapIds` :235, `RenderPassesAndExtraBits` :249. Counts `VerticesCount` :194 / `IndicesCount` :199. Custom parts :82-97. Transforms: `Translate` :384/:393, `Rotate` :407/:417, `Scale` :426/:444, `ModelTransform` :452, `MatrixTransform` :473+. Builders: `WithColorMaps` :714, `WithXyzFaces` :725, `WithRenderpasses` :734, `WithNormals` :744. `AddMeshData` overloads :774/:785/:948/:1012. `SetVertexFlags(int)` :1430. `Clone` :1787, `EmptyClone` :1982, `Dispose` :1967 |
| `Client/Model/Mesh/CubeMeshUtil.cs`, `QuadMeshUtil.cs`, `LineMeshUtil.cs`, `MeshUtil.cs`, `NormalUtil.cs` | mesh generators |
| `Client/Model/Mesh/CustomMeshDataPart{Float,Int,Short,Byte}.cs` | per-vertex custom attribute buffers (used for MP rotation, glow etc.) |
| `Client/Render/ITerrainMeshPool.cs` | 3 `AddMeshData` overloads (`MeshData`, `+float[] tfMatrix`, `+ColorMapData`), all with `lodLevel = 1` |
| `Client/API/ITesselatorAPI.cs` (290 ln) | `TesselationMetaData` :11 (`TexSource`, `SelectiveElements` :37, `IgnoreElements` :38, `WithJointIds` :41, `Rotation` :43). `ITesselatorManager` :82 (`GetDefaultBlockMesh` :89, `GetDefaultBlockMeshRef` :96, `GetCachedShape` :105, `CreateMesh` :106). `ITesselatorAPI` :129 (`TesselateBlock` :136, `TesselateItem` :143/:151/:160, `TesselateShape` ×4 :175/:186/:201/:225, `TesselateShapeWithJointIds` :217, `VoxelizeTexture` :235, `GetTextureSource` :257/:268/:278). `RetryTesselationException` :285 |
| `Client/API/MeshRef.cs` | `MeshRef`, `MultiTextureMeshRef` |
| `Client/Texture/ITextureSource.cs` | `ITexPositionSource` :10 (`this[string]`, `AtlasSize`), `ContainedTextureSource` :24 |
| `Client/Texture/CompositeTexture.cs`, `TextureDictionary.cs`, `TextureAtlasPosition.cs`, `ShapeTextureSource.cs`, `LoadedTexture.cs`, `FastBitmap.cs` | atlas plumbing |
| `Client/API/WorldInteraction.cs` | `WorldInteraction` :13 — `MouseButton` :20, `HotKeyCode(s)` :27/:34, `ActionLangCode` :41, `Itemstacks` :52, `RequireFreeHand` :57, `GetMatchingStacks` :62, `ShouldApply` :67 |
| `Client/UI/Dialog/GuiDialogBlockEntity.cs` | `GuiDialogBlockEntity`; **`enum EnumBlockEntityPacketId { Open = 1000, Close = 1001 }` :196** |
| `Client/UI/Dialog/GuiDialogBlockEntityInventory.cs` | packet-id-offset variant :121 |
| `Client/API/IClientNetworkAPI.cs` | `SendBlockEntityPacket(BlockPos, int, byte[])` :128, `SendBlockEntityPacket<T>(BlockPos, int, T)` :227 (protobuf), `SendBlockEntityPacketWithOffset` :167, `SendHandInteraction` :209 |
| `Client/MeshPool/*` | chunk mesh pooling internals — not mod-facing |
| `Client/Render/EnumChunkRenderPass.cs`, `EnumDrawType.cs`, `EnumFaceCullMode.cs`, `EnumRenderStage.cs`, `IRenderer.cs`, `ColorMapData.cs` | render enums / renderer interface |

## `vsapi/Common/API/` — the accessor + registration surface

| File | What matters |
|---|---|
| `ICoreAPI.cs` | `RegisterBlockClass(name, Type)` :60 · `RegisterBlockEntityClass` :75 · `RegisterItemClass` :82 · `RegisterCollectibleBehaviorClass` :89 · `RegisterBlockBehaviorClass` :97 · `RegisterBlockEntityBehaviorClass` :104 · `RegisterEntity` :46 · `RegisterEntityBehaviorClass` :53 · `RegisterCropBehavior` :67 · `RegisterMountable` :113 · `RegisterRecipeRegistry<T>` :33 · `RegisterColorMap` :39. **All say "Be sure to register it on the client and server side" and "Must happen before any blocks are loaded"** (i.e. in `ModSystem.Start`, not `StartServerSide`). |
| `IClassRegistryAPI.cs` (181 ln) | `CreateBlock` :26 · `CreateBlockEntity` :40 · `CreateBlockBehavior` :70 · `CreateCollectibleBehavior` :78 · **`GetBlockEntityBehaviorClass(name)` :85** · `CreateBlockEntityBehavior` :93 · `GetBlockBehaviorClass` :100 · `GetCollectibleBehaviorClass` :107 · `GetBlockEntityClass(Type)` :157 · `CreateCropBehavior` :172 |
| `IBlockAccessor.cs` | `SetBlock` :415/:423/:432 · **`ExchangeBlock` :438** (no OnBlockRemoved/OnBlockPlaced, BE survives) · `BreakBlock` :447 · `DamageBlock` :457 · `SpawnBlockEntity(string, pos, stack)` :482 · `SpawnBlockEntity(BlockEntity)` :488 (**skips CreateBehaviors/Initialize/OnBlockPlaced**) · `RemoveBlockEntity` :495 · `GetBlockEntity` :502/:510 · `Commit`/`Rollback` :567/:572 · **`MarkBlockEntityDirty` :580** · `TriggerNeighbourBlockUpdate` :586 · **`MarkBlockDirty(pos, skipPlayer)` :594** · `MarkBlockModified` :601 (client: deletes & recreates BEs) · `MarkBlockDirty(pos, Action OnRetesselated)` :609 |
| `IWorldAccessor.cs` | `Claims` :54 · `Side` :120 · `BlockAccessor` :125 · `BulkBlockAccessor` :130 · `ClassRegistry` :135 |
| `IEventAPI.cs` | `RegisterGameTickListener(…, BlockPos, Action<Exception>, ms, offset)` — the overload `BlockEntity` uses |
| `EnumBlockAccessFlags.cs` | `Use`, `BuildOrBreak`, … |
| `BlockLayersAccess.cs` | `Solid` / `Fluid` layer ids for `SetBlock(id, pos, layer)` |
| `IMiniDimension.cs` | mini-dimension (preview / vehicle) |
| `ModSystem.cs`, `ModInfo.cs`, `IModLoader.cs` | mod entry points |

Also at `Common/` root: `EnumHandling.cs` (`PassThrough`/`Handled`/`PreventDefault`/`PreventSubsequent`), `MultiblockStructure.cs` (`BlockNumbers` :33, `Offsets` :35, `InitForUse(float rotateYDeg)` :52 — **degrees**, `WalkMatchingBlocks` :76, `InCompleteBlockCount` :106, `HighlightIncompleteParts` :138, `HighlightSlotId = 23` :31), `IMultiblockOffset.cs` (`GetControlBlockPos` — mutates the passed pos).

`vsapi/Config/Dimensions.cs` — `NormalWorld=0` :15, `MiniDimensions=1` :19, `AltWorld=2` :23, **`ShouldNotTick(BlockPos, ICoreAPI)` :52**.

`vsapi/Math/BlockPos.cs` — `InternalY` :28 (`Y + dimension * DimensionBoundary`), `dimension` :38, `DimensionBoundary` :40, `BlockPos(int,int,int)` :60 **decodes the dimension out of y**.

## `vsapi/Common/Inventory/`

`ItemSlot.cs` :15 (`Itemstack` :60, `Empty` :79, `StorageType` :84, `MaxSlotStackSize` :33, `CanTakeFrom` :110, `CanHold` :137, `TakeOut` :188, `TryPutInto` :208/:220, `TryFlipWith` :272, `OnItemSlotModified` :455, `MarkDirty` :472, `GetStackName` :497) · `InventoryBase.cs` · `InventoryGeneric.cs` · `DummySlot.cs` / `DummyInventory.cs` (test-friendly) · `ItemSlotSurvival/Output/Universal/Backpack/Character/OffHand/PerPlayer.cs` · `ItemStackMergeOperation.cs` · `ISlotProvider.cs` · `IInventoryNetworkUtil.cs` · `WeightedSlot.cs` · `BagInventory.cs`

## `vsapi/Common/Registry/RegistryObject.cs`

`Code` :20 · `VariantStrict` :25 · `Variant` :30 · `Class` :35 · `CodeWithPath` :48 · `CodeWithoutParts` :58 · `CodeWithParts` :102/:115 · **`CodeWithVariant(type, value)` :122** · `CodeWithVariants` :143/:165 · `CodeWithPart` :195 · `LastCodePart` :213 · `FirstCodePart` :227 · `WildCardMatch` :241-282 · `FillPlaceHolder` :294-327

## vssurvivalmod container BEs (for reference — not vsapi)

- `vssurvivalmod/BlockEntity/BEContainer.cs` — `BlockEntityContainer` :15; abstract `Inventory` :17 / `InventoryClassName` :18; `InWorldContainer container` :21 (ctor :23); `Initialize` :28 (`Inventory.LateInitialize(className + "-" + Pos, api)` :33, 10 s tick :37); `OnBlockPlaced` :46 (copies `BlockContainer` contents); `OnBlockBroken` :66 (server-only `DropAll` + `Logger.Audit`); `From/ToTreeAttributes` :111/:117 delegate to `container`; `GetBlockInfo` :134 (perish rates); `Dispose` :180
- `vssurvivalmod/BlockEntity/BEOpenableContainer.cs` — `EnumBlockContainerPacketId { OpenInventory = 5000, OpenLidOthers = 5001 }` :16; `BlockEntityContainerOpen` wire format :41-76; `BlockEntityOpenableContainer` :81; `toggleInventoryDialogClient` :118; **`OnReceivedClientPacket` :140** (`packetid < 1000` ⇒ inventory packet, perms-checked at :158, rollback at :161); `OnReceivedServerPacket` :193; `Dispose` :264
- `vssurvivalmod/BlockEntity/BEContainerDisplay.cs` — `IContainedInteractable` :13, `IContainedCustomName` :25, `IContainedMeshSource` :39, `BlockEntityDisplay : BlockEntityContainer, ITexPositionSource` :51; `ClassCode` :60, `DisplayedItems` :62, `AttributeTransformCode` :66, `this[textureCode]` :68 (4-tier texture fallback), `MarkMeshesDirty` :181, `updateMeshes` :186, `getMeshCacheKey` :213, `MeshCache` :224, `getOrCreateMesh` :233, `getDefaultMesh` :296, `genTransformationMatrices` (abstract) :321, `OnTesselation` :324
- `vssurvivalmod/BlockEntity/BEDisplayCase.cs` :14 — a concrete `BlockEntityDisplay` worth copying from

## Key types

### `BlockEntity`

`vsapi/Common/Collectible/Block/BlockEntity.cs:17`

Per-position mutable state for a block. Created by the class registry from Block.EntityClass; owns tick listeners, save/sync tree, custom chunk mesh and the block-info HUD text.

THE EXACT LIFECYCLE (both sides): (1) parameterless ctor — Api/Pos/Block are ALL null; (2) `CreateBehaviors(block, worldForResolve)` :103 sets `this.Block` and instantiates behaviors from `block.BlockEntityBehaviors`; (3) `FromTreeAttributes(tree, worldForResolve)` :332 — ONLY for a pre-existing BE (chunk load / server sync), runs BEFORE Initialize so `Api` is still null; it rebuilds `Pos` from tree keys posx/posy/posz :333-337; (4) `Initialize(ICoreAPI)` :72 sets `Api`, calls `behavior.Initialize` for each, then — worldgen path only — calls `OnBlockPlaced(stackForWorldgen)` :88-99; (5) `OnBlockPlaced(byItemStack)` :290 only on genuine placement, always after Initialize. Teardown: `OnBlockRemoved()` :212 (block gone for good — unregisters tick+callback handlers) vs `OnBlockUnloaded()` :261 (chunk unloaded — same unregistration, BE will be re-created later). `OnBlockBroken(IPlayer)` :242 is called from `Block.SpawnDropsAndRemoveBlock` BEFORE the block is set to 0. `OnExchanged(Block)` :232 fires on `ExchangeBlock` and calls `MarkDirty(true)` if the block type actually changed — Initialize is NOT re-run. `ToTreeAttributes(tree)` :302 writes posx/posy(=Pos.InternalY)/posz/blockCode + behaviors. `MarkDirty(bool redrawOnClient=false, IPlayer skipPlayer=null)` :451 → `MarkBlockEntityDirty(Pos)` always, `MarkBlockDirty(Pos, skipPlayer)` only when redrawOnClient. `OnReceivedClientPacket(IPlayer, int, byte[])` :355 (server side) / `OnReceivedServerPacket(int, byte[])` :436 (client side) — base impls fan out to behaviors, ALWAYS call base. `OnTesselation(ITerrainMeshPool, ITesselatorAPI)` :532 runs on a TESSELATION THREAD, returns true to suppress the default block mesh; base ORs the behaviors' results. `GetBlockInfo(IPlayer, StringBuilder)` :468. `RegisterGameTickListener` :130/:146 auto-disposes on remove/unload. `CachedAccessPerms` struct :367 — the 1.22 permission helper for packet handlers (`IsInteractingPlayerAllowedTo(flags, validatePickRange, nameForLog)` :386).

### `Block`

`vsapi/Common/Collectible/Block/Block.cs:28`

The immutable, shared, per-blocktype singleton. One instance per block code+variant for the whole world — NEVER store per-position state on it.

`EntityClass` :404 is the string that makes `OnBlockPlaced` :1379 spawn a BE (`world.BlockAccessor.SpawnBlockEntity(EntityClass, blockPos, byItemStack)` at :1383) and `OnBlockRemoved` :1350 destroy it (:1367). Placement chain: `TryPlaceBlock` :882 → `CanPlaceBlock` :921 → `DoPlaceBlock` :976. Interaction: `OnBlockInteractStart` :1436 (claim `Use` check first at :1440, then behaviors, base returns **false** at :1462), `OnBlockInteractStep` :1495 (base false = stop), `OnBlockInteractStop` :1525, `OnBlockInteractCancel` :1553 (base **true** = allow cancel). HUD: `GetPlacedBlockInfo` :2268 (calls `be.GetBlockInfo` inside try/catch :2277-2285, then `<domain>:blockdesc-<path>` lang :2295, then decors, then mining tier, then behaviors), `GetPlacedBlockName` :2251 (derives from `OnPickBlock(...)?.GetName()`), `GetPlacedBlockInteractionHelp` :2179. Lookup: `GetBlockEntity<T>(BlockPos)` :2679, `GetBEBehavior<T>` :2690, `GetInterface<T>(world,pos)` :2706 (Block → BlockBehavior → BlockEntity → BlockEntityBehavior). Ticking: `ShouldReceiveServerGameTicks` :1750 runs on a SEPARATE server thread (use the supplied `offThreadRandom`, not `world.Rand`), `OnServerGameTick` :1772 on the main thread; `OnAsyncClientParticleTick` :1716 every 25 ms within 32 blocks. `OnLoaded(ICoreAPI)` :456 — sets `api`, resolves the `cover` snow variants :466-478, client-only texture-subid load :480. `Shape` :240 / `Textures` :261 / `ShapeInventory` :235 / `Lod0Shape` :246 / `Lod2Shape` :251 are all **null on the server** from 1.20.4 (see `FreeRAMServer` :2912).

### `BlockBehavior / StrongBlockBehavior`

`vsapi/Common/Collectible/Block/BlockBehavior.cs:17 and StrongBlockBehavior.cs:12`

Composable per-blocktype behavior attached via the blocktype JSON `behaviors` array. Every hook carries `ref EnumHandling`.

`BlockBehavior : CollectibleBehavior` — so it also gets every held-item hook. Placement pipeline documented as steps 1-4 at :289/:306/:321/:336: `TryPlaceBlock` :299 → `CanPlaceBlock` :314 → `DoPlaceBlock` :329 → `OnBlockPlaced` :343. **The comment at :337-338 records a 1.21 behavior change: the BlockEntity now already exists when `OnBlockPlaced` runs, and setting handling no longer suppresses BE creation** (until 1.20 it did). `GetPlacedBlockInteractionHelp` :256 must be paired with a cheap, EXACTLY-matching `GetPlacedBlockInteractionHelpCount` :265. `GetMiningSpeedModifier` :366 multiplies dt in `Block.OnGettingBroken` :1039. `GetRetention(pos, facing, type, ref handled)` :382 replaces the obsolete `GetHeatRetention` :377. `StrongBlockBehavior` adds `GetCollisionBoxes` :29, `GetSelectionBoxes` :35, `GetParticleCollisionBoxes` :23, `GetParticleBreakBox` :41, `SideIsSolid` :77/:83, `GetLightAbsorption` :89/:95, `TryPlaceBlockForWorldGen` :52, `DoPartialSelection` :59, `GetDecal` :18, `GetRandomColor`/`GetColorWithoutTint` :71/:65 — but the class doc at :10 states these are dispatched ONLY by `BlockGeneric`.

### `BlockEntityBehavior / BlockEntityBehaviorType`

`vsapi/Common/Collectible/Block/BlockEntityBehavior.cs:48 and :26`

Composable per-position behavior. Declared in blocktype JSON as `entityBehaviors: [{ name, properties }]`; the DTO is BlockEntityBehaviorType.

`Blockentity` :53 with `Pos` :58 and `Block` :63 aliases; `properties` (JsonObject) :68; `Api` :70 assigned in `Initialize(api, properties)` :82. It mirrors, but does NOT inherit, the BlockEntity hooks: `OnBlockRemoved` :87, `OnBlockUnloaded` :92, `OnBlockBroken` :97, `OnBlockPlaced` :102, `To/FromTreeAttributes` :107/:112, `OnReceivedClient/ServerPacket` :117/:122, `GetBlockInfo` :127, `OnTesselation` :156, `OnPlacementBySchematic` :161. Note the *missing* hook: there is **no `OnExchanged`** on BlockEntityBehavior — `BlockEntity.OnExchanged` :232 does not fan out to behaviors, so a behavior that caches shape/renderer state must be re-initialised by the owning BE.

### `CollectibleObject`

`vsapi/Common/Collectible/Collectible.cs:25`

Shared base of Block and Item — stack semantics, held interaction, smelting, transitions, temperature.

`abstract Id` :44 / `abstract ItemClass` :57 · `MaxStackSize` :63 · `Durability` :68 · `Attributes` (JsonObject) :165 · `CombustibleProps` :170 · `NutritionProps` :175 · `TransitionableProps` :180 · `GrindingProps` :185 · `CrushingProps` :190 · `ParticleProperties` :195 · `StorageFlags` :211 · `MaterialDensity` :216 · `CollectibleBehaviors` :260 · `LightHsv` :265 · `Tags` :267 (new tag system) · `protected ICoreAPI api` :254. **`OnLoadedNative(ICoreAPI)` :272 is non-virtual on purpose** — it assigns `api` then calls the virtual `OnLoaded` :282, so a subclass that forgets `base.OnLoaded` still gets `api`. Temperature helpers: `GetTemperature(world, stack)` :3293, `SetTemperature(world, stack, temp, delayCooldown = true)` :3349, `HasTemperature` :3247. Smelting: `GetMeltingPoint` :2735, `GetMeltingDuration` :2722, `CanSmelt` :2750, `DoSmelt` :2771. Merging: `GetMergableQuantity` :2538, `TryMergeStacks` :2570. Equality: `Equals(thisStack, otherStack, params ignoreAttributeSubTrees)` :3382, `Satisfies` :3397. Held: `OnHeldInteractStart/Step/Stop/Cancel` :1625/:1684/:1728/:1759. `GetHeldItemInfo` :1960. `GetCollectibleInterface<T>()` :3622.

### `ItemStack`

`vsapi/Common/Collectible/ItemStack.cs:12`

A resolved (block|item) + count + attribute tree. The unit of inventory content and of network/save serialization.

`Class` :17, `Id` :22, `Collectible` :36, `Block` :55 / `Item` :47 (null unless Class matches), `StackSize` :63. **`Attributes` :83 is saved AND synced; `TempAttributes` :94 is neither** — the standard place to stash client-only or per-frame data. `ToBytes/FromBytes` :329/:341 write Class, Id, stacksize, attributes — note they do NOT write the code, so ids must be remapped on import via `FixMapping(oldBlockMapping, oldItemMapping, worldForNewMapping)` :442. `ResolveBlockOrItem(resolver)` :354 turns an id back into a Block/Item. `Clone()` :411 = `GetEmptyClone()` :423 + stacksize, and DOES deep-clone Attributes but DROPS TempAttributes. `GetName()` :387 → `Collectible.GetHeldItemName`. `ctor(CollectibleObject|Item|Block, stacksize)` :196/:223/:240 THROW on null.

### `MeshData`

`vsapi/Client/Model/Mesh/MeshData.cs:16`

CPU-side vertex/index buffers. What OnTesselation hands to ITerrainMeshPool and what you upload for a custom renderer.

Parallel arrays `xyz` :36, `Uv` :61, `Rgba` :66, `Flags` :41, `Indices` :71, plus optional `Normals` :56, `XyzFaces` :215, `ClimateColorMapIds`/`SeasonColorMapIds` :231/:235, `RenderPassesAndExtraBits` :249 and `CustomFloats/Ints/Shorts/Bytes` :82-97. `ITerrainMeshPool.AddMeshData` requires xyz, uv, rgba, indices, flags AND **xyzFaces** to be set (ITerrainMeshPool.cs:8) — use `.WithXyzFaces()` :725 / `.WithColorMaps()` :714 / `.WithRenderpasses()` :734 on a mesh you built yourself. In-place transforms return `this`: `Translate` :384, `Rotate(origin, radX, radY, radZ)` :407 (RADIANS), `Scale` :426, `ModelTransform(ModelTransform)` :452, `MatrixTransform(float[16])` :473. `Clone()` :1787 deep-copies; `EmptyClone()` :1982 keeps the flags/config but no data. `SetVertexFlags(int)` :1430 ORs a flag onto every vertex (glow, wind, z-offset).

### `Shape / CompositeShape / ShapeElement`

`vsapi/Common/Model/Shape/Shape.cs:16, CompositeShape.cs:47, ShapeElement.cs:13`

Shape = the loaded JSON model (element tree + animations). CompositeShape = the JSON reference to it plus rotation/offset/scale/alternates/overlays/selectiveElements.

`Shape.TryGet(api, AssetLocation)` :534 is the standard loader and returns null (logged) on failure — it does NOT throw. Before animating you must run `CollectAndResolveReferences` :84/:95 → `CacheInvTransforms` :700 → `ResolveAndFindJoints` :408 (this is exactly what `AnimationUtil.InitializeShapeAndAnimator` does at AnimationUtil.cs:40-42). `WalkElements(wildcardpath, onElement)` :548 walks a SLASH-SEPARATED path where `*` matches one level (and `*` alone recurses, :568); `FindElement(wildcard)` :585 does a recursive `WildcardUtil.Match` on the name; `GetElementByName` :600 is exact-ignorecase. `ShapeElement.From`/`To` :29/:31 are in 1/16 block units and are **relative to the parent element** — `GetLocalTransformMatrix` :286 composes parent-then-child, and it has TWO orders: animVersion 1 = Translate·Scale·Rotate :309-341, animVersion 0 = Translate·Rotate·Scale :343-359. `CompositeShape.SelectiveElements` :145 / `IgnoreElements` :150 / `QuantityElements` :139 are honoured via `TesselationMetaData` :37/:38/:36.

### `IClassRegistryAPI + ICoreAPI.Register*`

`vsapi/Common/API/IClassRegistryAPI.cs:13, vsapi/Common/API/ICoreAPI.cs:56-113`

The registration path: name string in JSON -> Type -> instance.

Register in `ModSystem.Start(ICoreAPI)` — every Register* doc comment says "Must happen before any blocks are loaded. Be sure to register it on the client AND server side" (ICoreAPI.cs:56-104). Names: `RegisterBlockClass` (JSON `class`), `RegisterBlockEntityClass` (JSON `entityClass`), `RegisterBlockBehaviorClass` (JSON `behaviors[].name`), `RegisterBlockEntityBehaviorClass` (JSON `entityBehaviors[].name`), `RegisterCollectibleBehaviorClass` (works for both items and blocks), `RegisterItemClass`. Resolution happens in `BlockEntity.CreateBehaviors` :107-120 — an unregistered entityBehavior name logs `Lang.Get("Block entity behavior {0} for block {1} not found")` :111 and is **silently skipped**, not fatal. `Block.HasBehavior(string, IClassRegistryAPI)` :2830 checks BOTH the collectible-behavior and block-behavior registries.

### `BlockSelection`

`vsapi/Common/Collectible/Block/BlockSelection.cs:15`

What the player is aiming at. Passed to every interaction/placement hook and protobuf-serialized to the server.

`Position` :21 (the position to place/break AT, already offset), `Face` :29 (non-serialized; rebuilt from `FaceIndex` in `[OnDeserialized]` :144), `HitPosition` :35 (0..1 RELATIVE to Position), `SelectionBoxIndex` :41 (index into `Block.GetSelectionBoxes()`), `SelectionBoxId` :47 (matches `CuboidfWithId.Id`), `DidOffset` :56 (**always false during block USE; only meaningful during placement** :50-54), `Block` :61 (non-serialized). `FullPosition` :69 uses `Position.InternalY`. `Clone()` :106 notably does NOT copy `Block`.

### `BlockEntityContainer / BlockEntityOpenableContainer / BlockEntityDisplay`

`vssurvivalmod/BlockEntity/BEContainer.cs:15, BEOpenableContainer.cs:81, BEContainerDisplay.cs:51`

The vanilla inventory-BE stack. NOT part of vsapi — depending on these means depending on VSSurvivalMod.dll.

`BlockEntityContainer` requires `abstract InventoryBase Inventory` :17 and `abstract string InventoryClassName` :18; its `Initialize` :28 does `Inventory.LateInitialize(InventoryClassName + "-" + Pos, api)` :33 and registers a 10 s tick for perish :37. `OnBlockBroken` :66 drops everything server-side. `BlockEntityOpenableContainer` reserves packet ids: **anything < 1000 is routed to `Inventory.InvNetworkUtil.HandleClientPacket`** :156-170, `EnumBlockEntityPacketId.Open=1000/Close=1001` (GuiDialogBlockEntity.cs:198-199), `EnumBlockContainerPacketId.OpenInventory=5000/OpenLidOthers=5001` :16-20 — so a custom container's own packet ids must avoid <1000, 1000, 1001, 5000, 5001. `BlockEntityDisplay` implements `ITexPositionSource` and caches item meshes in a per-`ClassCode` global dict `MeshCache` :224; you must implement `genTransformationMatrices()` :321 and call `MarkMeshesDirty()` :181 + a `MarkBlockDirty` whenever contents change (`RedrawAfterReceivingTreeAttributes` :168 is the FromTreeAttributes-safe helper).


---

<a id="vsapi-core"></a>

# vsapi — math, datastructures, API surfaces, client/server

> All paths below are relative to `.compat/vintagestory/`. The vendored checkout is **VintagestoryAPI @ v1.22.5**.
> **Namespace ≠ folder.** The folder is `vsapi/Math/`, the namespace is `Vintagestory.API.MathTools`. Likewise `vsapi/Localization/Lang.cs` declares `namespace Vintagestory.API.Config`. Don't grep by namespace path.

# Top-level map of `vsapi/`

| Folder | Namespace(s) | What lives there |
|---|---|---|
| `Math/` | `Vintagestory.API.MathTools` | BlockPos, BlockFacing, Vec*/FastVec*, Cuboid*, Rectangle*, Matrix (Mat4f/Mat4d/Matrixf/Quaternion*), GameMath, Noise, ColorUtil, LCGRandom, NatFloat, CollisionTester |
| `Datastructures/` | `Vintagestory.API.Datastructures`, `Vintagestory.Common` | Attribute tree (all 16 attribute types), dictionaries, JsonObject, SemVer, data maps, FastMemoryStream, tags |
| `Util/` | `Vintagestory.API.Util` | Extension methods: arrays, strings, dicts, lists, hashsets, wildcards, JSON, protobuf serializer, object cache |
| `Config/` | `Vintagestory.API.Config` | GlobalConstants, GameVersion, GamePaths, Dimensions, RuntimeEnv |
| `Localization/` | `Vintagestory.API.Config` (!) | `Lang` static facade, `ITranslationService`, `TranslationService` |
| `Common/` | `Vintagestory.API.Common` (+ `.Entities`) | The bulk: API interfaces, assets, collectibles/blocks, entities, inventory, model/shape/animation, particles, crafting, registry |
| `Client/` | `Vintagestory.API.Client` | ICoreClientAPI, render, mesh, texture, GUI (Cairo-based), audio, input |
| `Server/` | `Vintagestory.API.Server` | ICoreServerAPI, IServerAPI, run phases, worldgen hooks, save game, privileges |
| `docs/` | — | **Generated docfx HTML**, including `docs/json-docs/jsondocs/*.html` which is the ONLY source in this checkout for closed-source `Vintagestory.ServerMods.NoObf` types (JsonPatch, RegistryObjectType, RegistryObjectVariantGroup) |

---

## `Math/` (22 files + subfolders)

- `BlockFacing.cs` — the 6-face singleton table. Constants `indexNORTH=0 … indexDOWN=5` at :25-30, face declarations :46-67, `ALLFACES` :72, `HORIZONTALS` :87, `VERTICALS` :95, `HORIZONTALS_ANGLEORDER` (E,N,W,S) :100. Instance API: `Opposite` :238, `Normali/Normalf/Normald` :138-144, `Plane` :149, `PlaneCenter` :175, `Axis` :201, `IsHorizontal/IsVertical/IsAxisNS/IsAxisWE` :185-197, `Negative` :240, `GetCCW/GetCW` :252/:261, `GetHorizontalRotated` :272, `FaceWhenRotatedBy` :287, `ToAB` :355, `IsAdjacent` :451, `IterateThruFacingOffsets` :375. Statics: `FinishIteratingAllFaces` :409, `FromCode` :480, `FromFirstLetter` :497/:507, `FromNormal(Vec3f/Vec3i)` :527/:548, `FromVector` :560, `FromFlag` :589, `HorizontalFromAngle` :611, `HorizontalFromYaw` :624, `FlagContains` :638.
- `BlockPos.cs` (1053 lines) — class, `IEquatable<BlockPos>, IVec3`. Public **fields** `X, Y, Z, dimension`; `InternalY` property (:28) = `Y + dimension*32768`. `DimensionBoundary` :40. Mutating movers `Up/Down/North/East/South/West/Add/Sub/Set` all return `this`; `*Copy()` variants allocate. `Copy()` :411, `CopyAndCorrectDimension()` :422, `SetAndCorrectDimension` :178/:194, `SetAndEquals` :258, `FacingFrom` :319, `IterateHorizontalOffsets` :676, `DistanceTo` :695, `DistanceSqToNearerEdge` :754, `ToVec3d/i/f` :846-863, `ToColumnIndex3d` :992, `ToSchematicIndex` :1008, `Walk` :966, operators :910-958. Subclass `FluidBlockPos` :1036.
- `Vector/` (20 files) — `Vec3d/Vec3f/Vec3i/Vec4*/Vec2*` are **classes**; `FastVec2f/FastVec2i/FastVec3d/FastVec3f/FastVec3i` are **mutable structs**. `IVec3.cs` is the common read interface. `Ray.cs`, `Vec3iAndFacingFlags.cs`.
- `Cuboid/` — `Cuboidf` (float, the block-collision/selection box type), `Cuboidd`, `Cuboidi`, `ICuboid`. Cuboidf: `RotatedCopy(deg…, origin)` :511, `RotatedCopyRad` :526/:536, `TransformedCopy(Span<float>)` :545 (re-derives an AABB from the 8 transformed corners), `OffsetCopy` :589, `Intersects` :315, `ContainsOrTouches` :301, `OmniGrowBy` :360, `ClampTo` :397, `pushOutX/Y/Z` :442-508.
- `Matrix/` — `Mat4f` (float[16], **column-major glMatrix port**; index layout documented at `Mat4f.cs:38-40`), `Mat4d`, `Mat3f/3d`, `Mat22/Mat23`, `Quaternionf/d`, `MatrixTools`, and `Matrix.cs` which declares the **fluent `Matrixf`** wrapper (`Create/Identity/Translate/Scale/Rotate*/Mul/ReverseMul/Invert/TransformVector/Clone`, `Values` float[16]).
- `GameMath.cs` (1719) — `PI/TWOPI/PIHALF/DEG2RAD/RAD2DEG` :35-51; `Sin/Cos/Acos/Asin/Tan` (float+double) :61-116; `FastSin/FastCos/FastSinDeg/FastCosDeg` (4096-entry LUT, built in static ctor :166-193); `Sqrt` :198; `Clamp` (float/int/byte/double) :238-282; `InverseClamp` :289; `Mod` (int/uint/float/double) :303-333; `RoundRandom` :345; `AngleDegDistance/AngleRadDistance` :371/:383; `NormaliseAngleRad` :393; `Lerp/Mix/Serp/BiLerp/SmoothStep/Smootherstep/TriangleStep` :761-1030; `Min/Max` incl. `params` overloads :1032-1105; `SmoothMin/SmoothMax` :1107; hashes `Crc32/Md5Hash/oaatHash*/MurmurHash3` :1135-1400; `R2Sequence*` :1402; `Shuffle` :1472; `BresenHamPlotLine3d` :1538; `IntFromBools/BoolsFromInt` :1639-1670; `Map<T> where T:INumber<T>` :1679; `RoundTo` :1687. Also `Easings` :14.
- `EnumAxis.cs` — `X=0, Y=1, Z=2`. `EnumCombination.cs` — `Add / Multiply / SelectiveMultiply` (variantgroup combination modes).
- `Cardinal.cs` — 8-way compass (`North…NorthWest`, `ALL`, `Index`, `Opposite`, `Normali`, `IsDiagnoal`), keyed by initial and by normal.
- Others: `ColorUtil.cs` (ARGB/HSV packing, `ColorMultiply3Clamped`, `ColorOverlay`, light HSV merge), `Noise/` (Simplex family), `ShapeUtil.cs`, `CollisionTester.cs`, `MultiCollisionTester.cs`, `AABBIntersectionTest.cs`, `Sphere.cs`, `NatFloat.cs`, `EvolvingNatFloat.cs`, `LCGRandom.cs`, `WeightedValue.cs`, `MapUtil.cs`, `Ascii85.cs`, `Crc32Algorithm.cs`, `Rectangle/`, `Size/` (`Size2i`, `Size3f`), `EnumTransformFunction.cs`.

## `Datastructures/`

### `AttributeTree/` — the save/network serialization backbone
- `Other/IAttribute.cs` — `ToBytes/FromBytes/GetAttributeId/GetValue/ToJsonToken/Equals(world,attr)/Clone`.
- `Other/ScalarAttribute.cs` — abstract base for single-value attrs (`public T value;` + `SetValue`).
- `Other/ArrayAttribute.cs` — abstract base for array attrs (`public T[] value;`).
- `Other/EnumAttributeType.cs` — **stale/wrong id enum** (see gotchas).
- Concrete types and their **wire ids** (registered in `TreeAttribute.cs:139-158`): 1 `IntAttribute`, 2 `LongAttribute`, 3 `DoubleAttribute`, 4 `FloatAttribute`, 5 `StringAttribute`, 6 `TreeAttribute`, 7 `ItemstackAttribute`, 8 `ByteArrayAttribute`, 9 `BoolAttribute`, 10 `StringArrayAttribute`, 11 `IntArrayAttribute`, 12 `FloatArrayAttribute`, 13 `DoubleArrayAttribute` (**in file `DoubleAttrayAttribute.cs` — filename typo**), 14 `TreeArrayAttribute`, 15 `LongArrayAttribute`, 16 `BoolArrayAttribute`.
- `StreamedByteArrayAttribute.cs` / `StreamedTreeAttribute.cs` — write-only, produce byte-identical output to `ByteArrayAttribute`/`TreeAttribute` straight into a `BinaryWriter`, to avoid intermediate `byte[]`.
- `TreeAttribute.cs` — the class plus static extension helper `TreeAttributeUtil` at :18 (`GetVec3i/SetVec3i/GetBlockPos/SetBlockPos/GetVec3is/SetVec3is`; note `SetBlockPos` stores **InternalY**, :42).
- `Other/SyncedTreeAttribute.cs` — dirty-tracking subclass used for entity `WatchedAttributes` and similar.
- `Other/JsonTreeAttribute.cs` — the JSON-authored counterpart used in blocktype/itemtype `attributesByType`.

### `Dictionary/`
`OrderedDictionary.cs` (`Vintagestory.API.Datastructures.OrderedDictionary<K,V>` — insertion ordered, index accessors, `ValuesOrdered`, `TryGetValue(key)` single-arg overload, `InsertBefore`), `IOrderedDictionary.cs`, `FastSmallDictionary.cs`, `ListDictionary.cs`, `LimitedDictionary.cs`, `RelaxedReadOnlyDictionary.cs` (indexer returns `default` instead of throwing; every mutator throws `InvalidOperationException`), `DictionaryExtensions.cs`.

### Rest of `Datastructures/`
`ConcurrentSmallDictionary.cs` (namespace `Vintagestory.Common`; backing store of every `TreeAttribute`; `Keys`/`Values` return **fresh array copies**), `JsonObject.cs`, `Net/JsonObject_ReadOnly.cs`, `LimitedList.cs`, `FastList.cs`, `RingArray.cs`, `UniqueQueue.cs`, `SortableQueue.cs`, `QueueOfInt.cs`, `FastSetOfInts/Longs`, `FastLargeSetOfLongs.cs`, `SmallBoolArray.cs`, `FastMemoryStream.cs` (non-seekable reusable `Stream`), `IntDataMap2D.cs`, `ByteDataMap2D.cs`, `FloatDataMap3D.cs`, `SemVer.cs`, `Net/GameReleaseVersion.cs`, `CachedCuboidList(Faster).cs`, `RotatableCube.cs`, `NaturalShape.cs`, `ShapeCell.cs`, `StackMatrix4.cs`, `BoolRef.cs`, `IMergeable.cs`, `SequencesCache.cs`, `CachedConcurrentDictionary.cs`, `Tags/` (`TagSet`, `TagSetFast`, `ITagRegistry`, `ComplexTagCondition` — the 1.21+ tag system).

## `Util/` (23 files)
`ArrayExtensions.cs` (also declares `ArrayUtil` and `EnumerableExtensions`: `CreateFilled`, `FastCopy`, `Slice`, `Append`, `InsertAt`, `RemoveEntry`, `Fill`, `Shuffle`, `Nearest`, `DeepEquals`, `IndexOf(predicate)`, `Foreach`), `StringUtil.cs` (`ToInt/ToFloat/ToDouble/ToBool` — all invariant-culture; `StartsWithFast/EqualsFast/ContainsFast/EqualsFastIgnoreCase`, `*Ordinal` wrappers, `UcFirst`, `CountChars`, `ToSearchFriendly`, `GetNonRandomizedHashCode`), `StringExtensions.cs` (`DeDuplicate` string-interning, StringBuilder helpers), `WildcardUtil.cs`, `JsonUtil.cs` (`ToObject<T>(text, domain)` — domain-aware AssetLocation deserialization, `PopulateObject`, `FromBytes/ToBytes`), `SerializerUtil.cs` (protobuf-net facade), `ObjectCacheUtil.cs`, `DictExtensions.cs`, `ListExtensions.cs`, `HashsetExtensions.cs`, `EqualityUtil.cs` (`NumberEquals` cross-numeric-type comparison used by attribute `Equals`), `PosUtil.cs`, `TagUtil.cs`, `NetUtil.cs`, `ReaderWriterExtensions.cs`, `AsyncHelper.cs`, `ThreadSafeRandom.cs`, `ObjectHandleExtensions.cs`, `IgnoreUtil.cs`, `BitmapExtensions.cs`, `SKColorExtensions.cs`, `ExifData.cs`, `FastSerializer.cs`.

## `Config/` + `Localization/`
- `GlobalConstants.cs` — `DefaultDomain="game"` :25, `ChunkSize=32` :39, `MaxWorldSizeXZ` :30, `MaxWorldSizeY=16384` :34, `DimensionSizeInChunks` :44, `DefaultCultureInfo=InvariantCulture` :20, physics/movement tunables :71-137, chat group ids :314-343, `IgnoredStackAttributes` :372, gameplay modifiers `PerishSpeedModifier/HungerSpeedModifier/CreatureDamageModifier/ToolMiningSpeedModifier` :377-391, `ReservedCharacterSequences` :212, `TooHotToTouchTemperature=250` :177, `CollectibleDefaultTemperature=20` :179.
- `GameVersion.cs` — `OverallVersion` :41, `APIVersion` (major.minor.0) :68, `NetworkVersion` :73, `SplitVersionString` :106, `GetReleaseType` :143, `IsCompatibleApiVersion` :165, `IsAtLeastVersion` :195/:207, `IsNewerVersionThan` :235, `EnsureEqualVersionOrKillExecutable` :252.
- `Dimensions.cs` — `NormalWorld=0`, `MiniDimensions=1`, `AltWorld=2`, `subDimensionSize`, `ShouldNotTick(pos, api)` (skip ticking minidimension previews).
- `GamePaths.cs`, `RuntimeEnv.cs`.
- `Localization/Lang.cs` (static facade, `namespace Vintagestory.API.Config`), `ITranslationService.cs`, `TranslationService.cs` (entryCache / wildcardCache / regexCache, plural formatter).

## `Common/` (the big one)

### `Common/API/` (26 files)
`ICoreAPI.cs` (declares `ICoreAPICommon` + `ICoreAPI`), `IWorldAccessor.cs`, `IBlockAccessor.cs` (also `ICachingBlockAccessor`, `EnumLightLevelType`, `ClimateCondition`), `IBulkBlockAccessor.cs`, `IBlockAccessorPrefetch.cs`, `IBlockAccessorRevertable.cs`, `BlockLayersAccess.cs` (Default/SolidBlocks/Solid/Fluid/FluidOrSolid/MostSolid), `IWorldChunk.cs` (+ `IChunkBlocks`, `IChunkLight`, chunk `SetModdata/GetModdata`), `IMapChunk.cs`, `IMapRegion.cs`, `IMiniDimension.cs`, `IEventAPI.cs` (+ `EnumChunkDirtyReason` :18 and most engine delegates), `IGameCalendar.cs`, `IClassRegistryAPI.cs`, `ILogger.cs` (+ `LoggerBase`), `ILandClaimAPI.cs`, `IModLoader.cs`, `ModSystem.cs`, `ModInfo.cs` (+ `EnumModType`), `ModInfoAttribute.cs`, `ModDependency.cs` (+ `ModDependencyAttribute`), `Delegates.cs` (**declares `Vintagestory.API.Common.Func<>`**), `EnumAppSide.cs` (+ `IsServer()/IsClient()` extensions), `EnumBlockAccessFlags.cs`, `EnumBlockAccessResponse.cs`, `EnumHandInteractNw.cs`, `ChatCommand/` (`IChatCommandApi`, `IChatCommand`, `CommandArgumentParsers`, `CmdArgs`, legacy `ChatCommand`).

### `Common/Assets/`
`AssetLocation.cs` (also `AssetLocationAndSource`, `SourceStringComponents`, `StringAssetLocationConverter`, `AssetLocationExtensions`), `AssetCategory.cs` (the 17 known categories + their side), `IAsset.cs`, `IAssetManager.cs`, `IAssetOrigin.cs`, `Mod.cs` (+ `EnumModSourceType`).

### `Common/Model/`
- `Shape/` — `Shape.cs` (`Elements`, `Animations`, `JointsById`, `Textures`, `CollectAndResolveReferences` :84, `ResolveAndFindJoints` :408, `StepParentShape` :214, `WalkElements(wildcardpath)` :548, `GetElementByName` :600, `RemoveElements` :622, `CloneElements` :671, `CacheInvTransforms` :700, `InitForAnimations` :738, static `TryGet(api, path)` :512), `ShapeElement.cs` (`From/To` in **1/16 block units**, `RotationOrigin`, `Children`, `ParentElement`, `JointId`, `GetLocalTransformMatrix(animVersion,…)` :286, `StepParentName`), `ShapeElementFace.cs`, `CompositeShape.cs`, `AttachmentPoint.cs`, `IBlockShapeSupplier.cs`.
- `Animation/` (19 files) — `IAnimator.cs`, `AnimatorBase.cs` (`OnFrame` :110, `Matrices` :56, `CurAnims` :50, `GetAnimationState` :74, `AttachmentPointByCode` :49), `ClientAnimator.cs`, `ServerAnimator.cs` (**extends ClientAnimator**), `AnimationManager.cs`, `NoAnimationManager.cs`, `AnimationMetaData.cs` (+ `AnimationTrigger`, `EnumAnimationBlendMode`), `Animation.cs` (+ `EnumEntityAnimationEndHandling`, `EnumEntityActivityStoppedHandling`), `RunningAnimation.cs`, `AnimationKeyFrame(Element).cs`, `AnimationFrame.cs`, `AnimationJoint.cs`, `ElementPose.cs`, `AnimationCache.cs`, `AnimationSound.cs`, `AnimationUtil.cs` (the **block-entity** animation helper: implements `IRenderer`, holds `animator`+`AnimatableRenderer`+`activeAnimationsByAnimCode`), `AnimatableRenderer.cs`, `EntityHeadController.cs`.

### Other `Common/` folders
`Collectible/` (Block, BlockEntity, BlockBehavior, Collectible, ItemStack, ModelTransform, VertexFlags, …) — **covered by the block/collectible assignment, not here**. `Entity/`, `Inventory/`, `Particle/`, `Crafting/`, `Registry/` (`RegistryObject.cs`, `JsonConverters.cs`), `Worldproperty/` (`WorldProperty`, `WorldPropertyVariant`, `MetalProperty*` — the `loadFromProperties` source for variantgroups), `Playstyle/` (`WorldConfiguration`, `Playstyle`), `Text/` (`VtmlParser`, `EnumChatType`, `ChatLine`), `Texture/`, `Combat/`, `Controls/`, `IO/`, `Platform/`, plus loose files `MultiblockStructure.cs`, `IMultiblockOffset.cs`, `EnumHandling.cs`, `EnumHighlightShape.cs`, `EnumLogType.cs`, `FrameProfilerUtil.cs`, `TyronThreadPool.cs`, `ByteSerializable.cs`, `Climate.cs`, `AmbientModifier.cs`, `SoundAttributes.cs`, `LandClaim.cs`, `PlayerSpawn.cs`.

## `Client/`
- `API/` — `ICoreClientAPI.cs`, `IClientWorldAccessor.cs`, `IClientEventAPI.cs` (`RegisterRenderer` :211, `UnregisterRenderer` :230, `RegisterItemstackRenderer` :238, `RegisterAsyncParticleSpawner` :253; also declares `DummyRenderer` :77), `IRenderAPI.cs`, `IRenderer.cs` (the `RenderOrder` table for every vanilla renderer is in its xmldoc), `IShaderAPI.cs`, `ITesselatorAPI.cs` (+ `TesselationMetaData`, `RetryTesselationException`), `ITextureAtlasAPI.cs`, `IGuiAPI.cs`, `IInputAPI.cs`, `ISettings.cs`, `IAmbientManager.cs`, `IColorPresets.cs`, `IMacroManager.cs`, `MeshRef.cs` (+ `MultiTextureMeshRef`), `UBORef.cs`, `WorldInteraction.cs`, `IClientNetworkAPI.cs` (**declares `INetworkAPI` in `Vintagestory.API.Common` and `EnumChannelState`**), `IClientNetworkChannel.cs` (**declares `INetworkChannel`**), `IClientPlayer.cs`.
- `Render/` — `EnumRenderStage.cs` (Before, Opaque, OIT, AfterOIT, ShadowFar/Done, ShadowNear/Done, AfterPostProcessing, AfterBlit, Ortho, AfterFinalComposition, Done), `IShaderProgram.cs`, `IStandardShaderProgram.cs`, `ITerrainMeshPool.cs`, `EnumChunkRenderPass.cs`, `EnumBlendMode.cs`, `EnumDrawType.cs`, `EnumDrawMode.cs`, `EnumFaceCullMode.cs`, `EnumFrameBuffer.cs`, `DefaultShaderUniforms.cs`, `FrustumCulling.cs`, `ColorMapData.cs`, `ModelCubeUtilExt.cs`, `WireframeCube.cs`, `PerceptionEffects/`.
- `Model/Mesh/` — `MeshData.cs` (~1200 lines: `xyz/Uv/Rgba/Flags/Indices/Normals/CustomFloats/CustomInts/…`, `Translate/Rotate/Scale/MatrixTransform/ModelTransform`, `AddMeshData` overloads incl. a per-face filter delegate, `WithColorMaps/WithXyzFaces/WithRenderpasses/WithNormals`), `CubeMeshUtil.cs`, `QuadMeshUtil.cs`, `LineMeshUtil.cs`, `MeshUtil.cs`, `NormalUtil.cs` (`PackNormal`), `CustomMeshDataPart*.cs`.
- `Texture/` — `CompositeTexture.cs` (`AlphaSeparator`, `BlendmodeSeparator`, `OverlaysSeparator`), `TextureAtlasPosition.cs`, `ITextureSource.cs` (**declares `ITexPositionSource` + `ContainedTextureSource`**), `LoadedTexture.cs`, `ShapeTextureSource.cs`, `FastBitmap.cs`, `TextureDictionary.cs`.
- `UI/` — Cairo-based GUI: `GuiComposer.cs`, `ElementBounds.cs`, `GuiStyle.cs`, `CairoFont.cs`, `Dialog/`, `Elements/Impl/**`, `Richtext/`, `JsonDialog.cs`, `ItemRenderInfo.cs`, `TextDrawUtil.cs`.
- `Audio/`, `Input/` (`HotKey`, `KeyCombination`, `EnumGlKeys`), `MeshPool/`, `ParticlePhysics.cs`, `EnumCameraType.cs`.

## `Server/`
`API/ICoreServerAPI.cs`, `API/IServerAPI.cs`, `API/IServerEventAPI.cs`, `API/IServerNetworkAPI.cs`, `API/IServerNetworkChannel.cs`, `API/IServerWorldAccessor.cs`, `API/IServerPlayer.cs`, `API/IServerChunk.cs`, `API/IWorldManagerAPI.cs`, `API/IPlayerDataManager.cs`, `API/IAsyncServerSystem.cs`; `EnumServerRunPhase.cs`, `EnumClientState.cs`, `EnumCheatProtectionLevel.cs`, `EnumPlayerJoinBehavior.cs`, `ISaveGame.cs`, `IServerConfig.cs`, `IServerPlayerData.cs`, `PlayerGroup.cs`, `Privilege.cs`, `ITreeGenerator.cs`, `PathfinderTask.cs`, `MultiplayerServerEntry.cs`, `Worldgen/` (`IWorldGenBlockAccessor`, `IChunkColumnGenerateRequest`, `IChunkProviderThread`, `EnumWorldGenPass`, …).

## Where the JSON-asset pipeline actually lives
Neither JSON patching nor variantgroup expansion is C# **in this repo** — both are in the closed `VSEssentials.dll` (`Vintagestory.ServerMods.NoObf`). The authoritative reference vendored here is the generated docs:
- `vsapi/docs/json-docs/jsondocs/Vintagestory.ServerMods.NoObf.JsonPatch.html`
- `vsapi/docs/json-docs/jsondocs/Vintagestory.ServerMods.NoObf.EnumJsonPatchOp.html`
- `vsapi/docs/json-docs/jsondocs/Vintagestory.ServerMods.NoObf.PatchCondition.html`
- `vsapi/docs/json-docs/jsondocs/Vintagestory.ServerMods.RegistryObjectVariantGroup.html`
- `vsapi/docs/json-docs/jsondocs/Vintagestory.ServerMods.NoObf.RegistryObjectType.html`

**JsonPatch fields**: `file` (AssetLocation, required), `op` (required), `path` (RFC-6902 JSON-pointer, required), `value`, `condition` {`when`, `isValue`, `useValue`}, `dependsOn` (`PatchModDependence[]`), `enabled` (default true), `fromPath` (for move/copy), `side` (default Universal; `sideType` obsolete).
**EnumJsonPatchOp**: `Add=0`, `AddEach=1` (arrays only), `Remove=2`, `Replace=3`, `Copy=4`, `Move=5`, `AddMerge=6` (merges into arrays instead of replacing — preferred for mod compat). Patches are applied by the JsonPatch loader with `ExecuteOrder 0.05`, i.e. **before** the block/item loader at 0.2 (`vsapi/Common/API/ModSystem.cs:50-53`).

**Variantgroup expansion** (`RegistryObjectVariantGroup`): fields `code` (required), `states` (required unless `loadFromProperties`/`loadFromPropertiesCombine`), `combine` (`Multiply` default / `Add` / `SelectiveMultiply`), `loadFromProperties` (an AssetLocation into `assets/*/worldproperties/**`), `loadFromPropertiesCombine`, `onVariant` (required for SelectiveMultiply). `RegistryObjectType` carries `code`, `variantgroups`, `skipVariants`, `allowedVariants`, `class`, `enabled`. Expansion appends `-<state>` to the base code **in variantgroup declaration order**, which is also the iteration order of `RegistryObject.Variant` / `VariantStrict` (`vsapi/Common/Registry/RegistryObject.cs:25-30`) and therefore of `CodeWithVariant(s)` (:122-186).

## Harmony
Not referenced by `vsapi` at all (`vsapi/VintagestoryAPI.csproj` has no Harmony package). The game **ships** `0Harmony.dll` at `$(VINTAGE_STORY)/Lib/0Harmony.dll` and mods add a plain `<Reference Include="0Harmony">` — see `vsmodexamples/Code Mods/VSTutorial - 7 - Basic Harmony Patching/vscodetutorial-harmonypatching/VSTutorial/VSTutorial.csproj:36`. The canonical lifecycle is in `.../VSTutorialModSystem.cs:24-40`.

## Key types

### `BlockFacing`

`vsapi/Math/BlockFacing.cs:22`

Immutable singleton for each of the 6 cube faces. The canonical source of index order, flag bits, normals and opposites. Right-handed coords: North=-Z, East=+X, South=+Z, West=-X, Up=+Y, Down=-Y.

EXACT CONTRACT — Index: NORTH=0, EAST=1, SOUTH=2, WEST=3, UP=4, DOWN=5 (constants indexNORTH..indexDOWN at :25-30, mirrored by ALLFACES at :72). Flag (byte): N=1, E=2, S=4, W=8, U=16, D=32 (:46-67); HorizontalFlags=15 (:35), VerticalFlags=48 (:40). MeshDataIndex = Index+1 (:206). HorizontalAngleIndex: E=0, N=1, W=2, S=3, and is -1 for UP/DOWN (:46-67) — HORIZONTALS_ANGLEORDER at :100 is {EAST, NORTH, WEST, SOUTH} i.e. 0°/90°/180°/270°, which is a DIFFERENT order from HORIZONTALS at :87 ({N,E,S,W}). Opposite (:238) is an ALLFACES lookup by a baked oppositeIndex — N<->S, E<->W, U<->D — and is a property, not a method (GetOpposite() is [Obsolete] at :242). Normali (:138) returns the SHARED Vec3i instance (NORTH.normali is (0,0,-1)); it is NOT a copy, so never mutate it — ALLNORMALI (:77) and HORIZONTAL_NORMALI (:91) alias the same objects. Normalf/Normald likewise. Plane (:149) is a degenerate Cuboidf with one axis flattened; PlaneCenter (:175) is the face midpoint in 0..1 block space. Axis (:201): N/S=Z, E/W=X, U/D=Y. Negative (:240) is true for NORTH, WEST, DOWN. GetCW/GetCCW (:252,:261) walk HORIZONTALS_ANGLEORDER and are meaningless (index -1 → wraps) for UP/DOWN. GetHorizontalRotated(int angleDeg) (:272) returns `this` unchanged for UP/DOWN and otherwise indexes HORIZONTALS by `Mod(angle/90 + index, 4)` — integer division, so any angle not a multiple of 90 truncates. Parsers: FromCode ("north"…, lowercased, null if unknown, :480), FromFirstLetter('n'/'e'/'s'/'w'/'u'/'d', :507), FromFlag (:589, exact single-bit only), FromNormal(Vec3i) (:548, exact match else null), FromNormal(Vec3f)/FromVector (:527,:560, nearest-angle, never null), HorizontalFromAngle (0°=EAST, :611), HorizontalFromYaw (0°=NORTH, :624). IsAdjacent (:451) means "perpendicular". ToAB(Vec3f) (:355) projects a point onto the face's 2D plane.

### `BlockPos`

`vsapi/Math/BlockPos.cs:20`

Mutable integer block coordinate with a separate dimension field. Used as a dictionary key everywhere (Equals/GetHashCode include dimension).

Public FIELDS X, Y, Z, dimension (:24-38) — not properties. InternalY (:28) = Y + dimension*DimensionBoundary, and its setter splits a packed Y back into Y+dimension. DimensionBoundary = 32768 (:40). new BlockPos(x,y,z) (:60) SPLITS y by DimensionBoundary; new BlockPos(x,y,z,dim) (:68) does not. The parameterless ctor and BlockPos(Vec3i) are [Obsolete] as dimension-unaware (:43,:76). Mutators Up/Down/North/South/East/West/Add/Sub/Set all mutate in place and return `this` (:118-312, :438+); the *Copy() family (WestCopy/EastCopy/NorthCopy/SouthCopy/UpCopy/DownCopy/AddCopy/SubCopy/DivCopy, :342-403,:647-668) allocates and PRESERVES dimension. Copy() :411 preserves dimension; CopyAndCorrectDimension() :422 re-splits Y. Set(BlockPos) :228 does NOT copy dimension (use Set(BlockPos,int) :236 or SetDimension :245). SetAndCorrectDimension :178/:194 splits a packed Y. SetAndEquals(x,y,z) :258 returns true if nothing changed — handy for change detection. FacingFrom(other) :319 = the direction you'd travel FROM other TO this. DistanceTo(BlockPos) :695 returns float.MaxValue across dimensions; DistanceTo(double,double,double) :723 is dimension-blind. DistanceSqToNearerEdge :754. IterateHorizontalOffsets(i) :676 — cumulative NESW walk, same contract as BlockFacing.IterateThruFacingOffsets. Equals/GetHashCode :871-884 include dimension; hash = ((17*23+X)*23+Y)*23+Z + dimension*269023. ToBytes/CreateFromBytes :267/:291 write 4 ints including dimension. ToColumnIndex3d/SetFromColumnIndex3d :992/:1000, ToSchematicIndex/SetFromSchematicIndex :1008/:1013.

### `TreeAttribute / ITreeAttribute`

`vsapi/Datastructures/AttributeTree/TreeAttribute.cs:91 (interface at vsapi/Datastructures/AttributeTree/ITreeAttribute.cs:12)`

The universal key/value tree used for BlockEntity save data, network packets, ItemStack attributes and entity watched attributes. Binary-serialized with a 1-byte type id per entry.

GET SEMANTICS — the rule is 'wrong type or missing key => defaultValue, silently', with TWO EXCEPTIONS. Return-default getters (all use `as`): GetBool :569, GetInt :581, GetFloat :646, GetDouble :634, GetString :659 (interns the result), GetStringArray :683, GetBytes :696, GetTreeAttribute :707 (returns null), GetModelTransform :769 (null). Nullable probes: TryGetBool/TryGetInt/TryGetDouble/TryGetFloat :533-561. THROWING getters (hard casts): GetLong :753 and TryGetLong :764 cast `(LongAttribute)` — an InvalidCastException if the key holds any other type; GetItemstack :741 casts `(ItemstackAttribute)` — same. GetOrAddTreeAttribute :718 throws InvalidOperationException if the key exists but is not a tree. Coercing getters: GetDecimal(key,def) :616 reads Int/Float/Double/Long/String-as-double; GetAsInt :593 = (int)GetDecimal; GetAsBool :598 accepts numeric>0 and the strings "true"/"1"; GetAsString :671 is ToString() of whatever is there. SET SEMANTICS — Set* reuse the existing attribute object when the type matches (so an existing IntAttribute is mutated, not replaced), else replace :401-526. SetItemstack :518 is NON-VIRTUAL. Structure: indexer :104 returns null for a missing key; HasAttribute :322; TryGetAttribute :327; RemoveAttribute :391; Count :120; Keys/Values :128-136 (each access allocates a copy); GetAttributeByPath('a/b/c') :335 and DeleteAttributeByPath :362 (slash-separated); Clone() :831 is a deep copy; MergeTree :993 (throws on type mismatch, :1025); IsSubSetOf :855 (skips GlobalConstants.IgnoredStackAttributes); Equals(world, other) :885 and the ignorePaths overload :907; GetHashCode(string[] ignored) :1073; SortedCopy :1040. Serialization: ToBytes/FromBytes :215-253, CreateFromBytes :170, depth limit 30 (:188), static RegisterAttribute :160 to add a custom attribute type id. Extension helpers in TreeAttributeUtil :18: GetVec3i/SetVec3i, GetBlockPos/SetBlockPos (stores InternalY), GetVec3is/SetVec3is.

### `SyncedTreeAttribute`

`vsapi/Datastructures/AttributeTree/Other/SyncedTreeAttribute.cs:17`

TreeAttribute subclass with dirty-path tracking and change listeners; this is what Entity.WatchedAttributes is.

MarkAllDirty :53 / AllDirty :59 / PartialDirty :64 / MarkClean :69. MarkPathDirty(path) :81 fires every listener whose registered path is a prefix of `path` (StartsWithOrdinal) or whose path is null, THEN records the dirty path (skipped when allDirty). RegisterModifiedListener(path, Action) :32 / UnregisterListener :37. Overrides that auto-mark dirty: SetInt, SetLong, SetFloat, SetBool, SetBytes, SetDouble, SetString, SetAttribute (:105-167) and RemoveAttribute (:169, which escalates to MarkAllDirty). GetIntAndIncrement :117 is a post-increment read. GetDirtyPathData :190/:196 produces (paths, per-path byte[]) for the network layer; PartialUpdate(path, data) :235 applies one and fires matching listeners; a null data payload deletes the path. Clone() :177 round-trips through a MemoryStream (so it is a full deep copy AND drops listeners). FromBytes :225 fires ALL listeners unconditionally.

### `AssetLocation`

`vsapi/Common/Assets/AssetLocation.cs:137`

domain:path identifier for every asset, block code, item code, entity code and recipe ingredient. Implicitly convertible to/from string.

LocationSeparator = ':' (:139). Domain getter returns GlobalConstants.DefaultDomain ("game") when the backing field is null (:146) — so `HasDomain()` (:374) is the only way to tell 'no domain given' from 'explicitly game'. new AssetLocation("a:b") :170 lowercases BOTH parts and interns; new AssetLocation(domain, path) :201 does NOT lowercase — caller's responsibility. Static Create(str, defaultDomain) :223 and CreateOrNull :212. IN-PLACE mutators returning `this`: WithPathPrefix(Once) :325/:331, WithLocationPrefixOnce :340, WithPathAppendix(Once) :346/:361, WithoutPathAppendix :352, WithPath :481, WithFilename :492, RemoveEnding :401. ALLOCATING copies: Clone :427, PermanentClone :437 (de-duplicates strings for long-lived objects), CopyWithPath :454, CopyWithPathPrefixAndAppendix(Once) :459/:464, CloneWithoutPrefixAndEnding :442. Code-part helpers: FirstPathPart(n) :291 (splits on '/'), FirstCodePart :297 / SecondCodePart :303 / CodePartsAfterSecond :310 / EndVariant :415 (split on '-'), GetName :383 (after last '/'), GetNameWithDomain :393, PathOmittingPrefixAndSuffix :406, Category :320. IsWildCard :156 is true if the path contains '*' OR starts with '@'; EndsWithWildCard :157. Valid :238. Equals :518 / GetHashCode :513 use the resolved Domain, so game:x equals (null-domain):x. Implicit operators :563-564: to string gives the FULL "domain:path", from string uses Create() with the game domain.

### `Lang (static) + ITranslationService`

`vsapi/Localization/Lang.cs:26 (impl vsapi/Localization/TranslationService.cs:27)`

i18n. All lookups are keyed by 'domain:key'; entries are loaded from assets/<domain>/lang/<locale>.json.

KEY FORMAT — KeyWithDomain (TranslationService.cs:606): if the key contains ':' it is used as-is, otherwise 'game:' is prefixed. The domain of a loaded entry is the DOMAIN OF THE FILE, taken from asset.Location.Domain (:86) — a key written as "myblock-name" inside assets/smex/lang/en.json becomes "smex:myblock-name", so mod code must call Lang.Get("smex:myblock-name"). WILDCARDS — LoadEntry (:585-604) buckets each entry by its star count: 0 stars → exact entryCache; exactly 1 star AND it is the LAST character → wildcardCache keyed by the prefix (StartsWith match); anything else → a compiled Regex anchored ^…$ with every '*' replaced by '(.*)'. LOOKUP MATRIX — Lang.Get(key,args) :191 checks HasTranslation(key, findWildcarded:false), so it only ever hits entryCache and RETURNS THE KEY ITSELF when missing (GetUnformatted :484-489). Lang.GetMatching :230 is the only entry point that resolves wildcard/regex entries; it falls back to Format(key,args). Lang.GetIfExists :157 and Lang.GetMatchingIfExists :243 return null when missing. Lang.GetUnformatted :217 skips string.Format. Lang.GetWithFallback(key, fallbackKey, args) :205. Lang.GetL(langcode,…) :171 / GetMatchingL :178 force a locale. Lang.HasTranslation(key, findWildcarded=true, logErrors=true) :272. Lang.GetAllEntries :254 merges DefaultLocale entries under CurrentLocale. CurrentLocale :34, DefaultLocale :35 ("en"), ChangeLanguage :70, AvailableLanguages :28. PLACEHOLDERS — args go through string.Format, so standard {0},{1}. VS also supports plurals: {p<argIndex>:zero|one|two|…} (TranslationService.cs:265-320); the chosen branch is Ceiling(Round(N,guessedDigits)) clamped to the last branch, then overridden so N==1 → branch 1 and N<1 → branch 0 (:339-350). A branch may embed a .NET numeric format made of #/0/./, characters, which is substituted with N (WithNumberFormatting :413). A malformed format string is caught, logged and the RAW value is returned (TryFormat :222-263). NOTE: there is no `EntryExists` member anywhere in 1.22.5 — the check is HasTranslation.

### `IBlockAccessor / IBulkBlockAccessor / IWorldAccessor accessor factories`

`vsapi/Common/API/IBlockAccessor.cs:170 (factories on vsapi/Common/API/IWorldAccessor.cs:719-775)`

Read/write world blocks. Several flavours trade safety for speed; picking the wrong one is a classic source of MP-only bugs.

GetBlock(BlockPos) :262 / GetBlock(BlockPos, layer) :282 NEVER return null — an unloaded chunk or an out-of-range coord yields the air block (id 0). GetBlockOrNull(x,y,z,layer=MostSolid) :324 is the only variant that returns null for unloaded/invalid. GetBlockRaw(x,y,z,layer) :273 takes an ALREADY dimension-packed y (i.e. pos.InternalY). All the (int,int,int) overloads are [Obsolete] as dimension-unaware (:313,:337,:348,:359). Layer selection via BlockLayersAccess (vsapi/Common/API/BlockLayersAccess.cs): Default=0 (solid, falling back to fluid), SolidBlocks/Solid=1, Fluid=2, FluidOrSolid=3, MostSolid=4 (ice-then-solid; used for collision). GetBlockEntity<T>(pos) :510. SetBlock(id,pos[,layer|byItemstack]) :415-432, ExchangeBlock :438 (keeps the BE), BreakBlock :447, MarkBlockDirty :594/:609, MarkBlockEntityDirty :580, MarkBlockModified :601, TriggerNeighbourBlockUpdate :586. WalkBlocks :372 / SearchBlocks :382 / SearchFluidBlocks :392 all take an onChunkMissing callback and are documented as NOT dimension-aware. Decor: SetDecor/GetDecors/GetDecor/BreakDecor :776-826. Light: GetLightLevel(pos, EnumLightLevelType) :617, GetLightRGBs :638. Chunk access: GetChunk :222/:229, GetChunkAtBlockPos :246 (null when unloaded — the reliable loaded-check), GetMapChunk :696. FACTORIES: GetBlockAccessor(sync, relight, strict, debug) :719; GetBlockAccessorBulkUpdate(sync, relight) :729 (defers relight/sync to Commit()); GetBlockAccessorBulkMinimalUpdate :737; GetBlockAccessorRevertable :751 (undo/redo per Commit); GetBlockAccessorPrefetch :759 (must call PrefetchBlocks first); GetCachingBlockAccessor :768 (10-50% faster in tight loops but YOU MUST CALL .Begin() or the game can crash — see ICachingBlockAccessor :157); GetLockFreeBlockAccessor :775 (READ-ONLY, and can return block id 0 for a packed chunk — never use it for correctness-critical reads). IBulkBlockAccessor adds StagedBlocks, ReadFromStagedByDefault, GetStagedBlockId and a BeforeCommit event (vsapi/Common/API/IBulkBlockAccessor.cs:13-53).

### `ICoreAPI / ICoreClientAPI / ICoreServerAPI`

`vsapi/Common/API/ICoreAPI.cs:179, vsapi/Client/API/ICoreClientAPI.cs, vsapi/Server/API/ICoreServerAPI.cs`

The sided root API objects handed to every ModSystem.

ICoreAPICommon (:22) holds every Register* used at Start(): RegisterBlockClass :60, RegisterBlockEntityClass :75, RegisterItemClass :82, RegisterBlockBehaviorClass :97, RegisterBlockEntityBehaviorClass :104, RegisterCollectibleBehaviorClass :89, RegisterEntity :46, RegisterEntityBehaviorClass :53, RegisterCropBehavior :67, RegisterMountable :113, RegisterColorMap :39, RegisterRecipeRegistry<T> :33 — all of which MUST be called on BOTH sides. Also ObjectCache (Dictionary<string,object>, per-side, :122), DataBasePath :128, GetOrCreateDataPath :135, StoreModConfig/LoadModConfig<T> :146-173 (per-side, NOT synced). ICoreAPI adds Logger, Side, Event (IEventAPI), World (IWorldAccessor), Network (INetworkAPI), Assets (IAssetManager), ModLoader (IModLoader), ClassRegistry, ChatCommands, CollectibleTagRegistry, EntityTagRegistry (:184-237). ICoreClientAPI narrows Event/Network/World to IClientEventAPI/IClientNetworkAPI/IClientWorldAccessor and adds Render (IRenderAPI), Gui, Input, Tesselator, TesselatorManager, BlockTextureAtlas/ItemTextureAtlas/EntityTextureAtlas, Shader, Settings, Ambient, IsGamePaused, IsSinglePlayer, ElapsedMilliseconds, InWorldEllapsedMilliseconds, TesselationThreadId, ShowChatMessage, TriggerIngameError. ICoreServerAPI adds Server (IServerAPI: CurrentRunPhase, IsDedicated, Config, ShutDown, LoadMiniDimension, the Log* family), WorldManager, Permissions, Groups, PlayerData, plus SendIngameError :65, SendMessage :87, RegisterBlock/RegisterItem/RegisterCraftingRecipe :127-139, RegisterTreeGenerator :147.

### `ModSystem / Mod / ModInfo / ModDependency / IModLoader`

`vsapi/Common/API/ModSystem.cs:14, vsapi/Common/Assets/Mod.cs:11, vsapi/Common/API/ModInfo.cs:47, vsapi/Common/API/ModDependency.cs:10, vsapi/Common/API/IModLoader.cs:7`

The mod lifecycle. Every mod entry point derives from ModSystem.

ModSystem lifecycle in call order: ShouldLoad(ICoreAPI)/ShouldLoad(EnumAppSide) :26/:34 → StartPre(api) :65 (all mods) → Start(api) :77 (register classes and network channels here; api.Assets is NOT usable yet) → AssetsLoaded(api) :89 (JSONs read+patched; blocks/items only registered if your ExecuteOrder > 0.2, recipes only if > 0.6) → AssetsFinalize(api) :98 (last chance to mutate block/item properties) → StartClientSide(ICoreClientAPI) :110 / StartServerSide(ICoreServerAPI) :121 → Dispose() :129. ExecuteOrder() :56 defaults to 0.1 and orders StartPre/Start across ALL mods; the vanilla reference table is in its xmldoc (:42-53): JsonPatch loader 0.05, mantle block 0.1, Block+Item loader 0.2, recipes 1, GenTerra 0, RockStrata 0.1, Deposits 0.2, Caves 0.3, Blocklayers 0.4. `Mod` property is injected by the loader. Mod (Mod.cs:11) exposes SourceType (CS/DLL/ZIP/Folder), SourcePath, FileName, Info, WorldConfig, Icon, Logger (a per-mod prefixed ILogger — use this, not api.Logger), Systems. ModInfo (:47) mirrors modinfo.json: Type (Theme/Content/Code, required), Name (required), ModID (auto-derived from Name via ToModID :200 if absent), Version, NetworkVersion (falls back to Version in Init :188), Side (default Universal), RequiredOnClient/RequiredOnServer (default true), Dependencies, Description, Authors, Website, IconPath. IsValidModID :229 — must start with a lowercase letter and contain only [a-z0-9]. ModDependency(modID, version) :26 — the Version is a MINIMUM, and an empty string means 'any'. IModLoader: GetModSystem<T>(withInheritance=true) :41, GetModSystem(fullName) :35, IsModEnabled(modID) :28, GetMod :23, Mods/Systems :12-17.

### `JsonObject`

`vsapi/Datastructures/JsonObject.cs:17`

Newtonsoft JToken wrapper used for every JSON-authored attribute block (block/item `attributes`, patch `value`, mod configs).

this[key] :49 is CASE-INSENSITIVE (OrdinalIgnoreCase, :55) and NEVER returns null — it returns a JsonObject wrapping a null token, so chained access is safe and `Exists` (:64) is the presence test. KeyExists(key) :81 uses `token?[key]` and is CASE-SENSITIVE — it disagrees with the indexer. Readers all take a defaultValue and never throw: AsBool :239 (bool or parseable string), AsInt :265 (long/int/float/double/string), AsFloat :293 (int/float/long/double/string, invariant culture), AsDouble :322 (int/long/double/string — note: NO float branch, and the string branch uses the CURRENT culture), AsString :187, AsStringArray :193, AsFloatArray :199, AsArray() :166 / AsArray<T>(default, defaultDomain) :211, AsObject<T>(default[, domain]) :92-145 (the domain overloads make bare AssetLocations resolve to that mod's domain instead of "game"). IsArray :369, Count :531, GetEnumerator :550, Clone :505, ToAttribute() :379 (converts to an IAttribute tree), IsTrue :516, FillPlaceHolder :384, static FromJson :21.

### `AnimatorBase / ClientAnimator / ServerAnimator / AnimationUtil / AnimationMetaData`

`vsapi/Common/Model/Animation/AnimatorBase.cs:25, ClientAnimator.cs:21, ServerAnimator.cs:10, AnimationUtil.cs:10, AnimationMetaData.cs:114`

Skeletal animation. AnimationUtil is the block-entity-facing helper that owns an animator plus a renderer.

AnimatorBase.OnFrame(activeAnimationsByAnimCode, dt) :110 is the whole tick: it starts newly-added codes, stops removed ones per Animation.OnActivityStopped (Stop/Rewind/PlayTillEnd/EaseOut), progresses running ones, then calls calculateMatrices. Animation codes are LOWERCASED at construction (:89) and looked up lowercased (:123). Matrices :56 returns TransformationMatricesDefaultPose when nothing is active. ActiveAnimationCount :63; GetAnimationState(code) :74 returns the RunningAnimation (Active/Running/CurrentFrame/Iterations/EasingFactor/BlendedWeight); GetAttachmentPointPose :276; GetPosebyName :282; ReloadAttachmentPoints :287; DumpCurrentState :237. ServerAnimator EXTENDS ClientAnimator (ServerAnimator.cs:10), so `animator as ClientAnimator` is non-null on both sides. AnimationUtil: ctor :28 registers itself as an EnumRenderStage.Opaque renderer named "beanimutil"; InitializeShapeAndAnimator :38 (resolves refs, caches inv transforms, finds joints, tesselates); InitializeAnimator(cacheKey, meshdata, shape, rotation, renderStage) :59; InitializeAnimatorServer :71; AnimationTickServer(dt) :79; StartAnimation(AnimationMetaData) :105 (no-op if the code is already active, returns false); StopAnimation(code) :124; static GetAnimator(api, cacheDictKey, shape, soundListener) :130 (caches Animations+RootElems+RootPoses in api.ObjectCache["animUtil-animCache"]); Dispose :183 unregisters the Opaque renderer only. AnimationMetaData JSON fields (:114-247): Code, Animation, Weight (1), ElementWeight, AnimationSpeed (1), MulWithWalkSpeed, WeightCapFactor, EaseInSpeed (10), EaseOutSpeed (10), BlendMode (Add), ElementBlendMode, TriggeredBy, SupressDefaultAnimation, ClientSide, AnimationSounds, Attributes.

### `IRenderer / EnumRenderStage / MeshRef / MeshData / ITexPositionSource`

`vsapi/Client/API/IRenderer.cs:10, vsapi/Client/Render/EnumRenderStage.cs, vsapi/Client/API/MeshRef.cs:33, vsapi/Client/Model/Mesh/MeshData.cs:16, vsapi/Client/Texture/ITextureSource.cs:9`

Client rendering plumbing for custom block-entity renderers.

IRenderer: RenderOrder (0 first, 1 last) — its xmldoc is the authoritative table of every vanilla renderer's order per stage (terrain opaque 0.37, entities 0.4, particles 0.6, GUI 1.0); RenderRange (currently unused); OnRenderFrame(dt, stage). Register with capi.Event.RegisterRenderer(renderer, stage, profilingName) (vsapi/Client/API/IClientEventAPI.cs:211) and ALWAYS UnregisterRenderer for the SAME stage in Dispose (:230) — registration is per-stage. EnumRenderStage order: Before=0, Opaque=1, OIT=2, AfterOIT=3, ShadowFar=4, ShadowFarDone=5, ShadowNear=6, ShadowNearDone=7, AfterPostProcessing=8, AfterBlit=9, Ortho=10, AfterFinalComposition=11, Done=12. MeshRef is an abstract GPU handle with Initialized and Disposed; MultiTextureMeshRef (:6) wraps one MeshRef per texture id. MeshData holds the raw CPU arrays (xyz, Uv, Rgba, Flags, Indices, Normals, TextureIndices, CustomFloats/Ints/Shorts/Bytes) plus VerticesCount/IndicesCount; fluent transforms Translate/Rotate/Scale/ModelTransform/MatrixTransform (:384-560) MUTATE and return `this`; AddMeshData has overloads with an xyz offset (:1012), a render-pass exclusion (:774) and a per-face filter delegate (:785). ITexPositionSource is a two-member interface (indexer by texture code, AtlasSize) — implement it to feed a custom tesselation; ContainedTextureSource (:24) is the ready-made implementation that lazily inserts textures into an atlas and falls back to UnknownTexturePosition with an error log.

### `INetworkAPI / INetworkChannel / IClientNetworkChannel`

`vsapi/Client/API/IClientNetworkAPI.cs:11 and :72, vsapi/Client/API/IClientNetworkChannel.cs:7 and :40`

Custom client<->server packet channels. Note both the common and client interfaces are declared inside Client/API files.

INetworkAPI: RegisterChannel(name) :18, RegisterUdpChannel(name) :25 (UDP payloads must stay under 508 bytes or NAT/firewalls drop them), GetChannel :32. IClientNetworkAPI narrows those and adds GetChannelState(name) :93 returning EnumChannelState (NotFound/Registered/Connected/NotConnected, :48-66), SendBlockEntityPacket(BlockPos, packetId, byte[]|T) :128/:227 and SendEntityPacket :136 for channel-free BE/entity messaging. IClientNetworkChannel: Connected :45, RegisterMessageType<T>() :58, SetMessageHandler<T>(handler) :65, SendPacket<T> :71. HARD CONTRACT: message types must be registered in the SAME ORDER on client and server (xmldoc at :48 and :54) because the wire id is the registration index. Message classes need [ProtoContract]/[ProtoMember] (protobuf-net 2.4.9 is the serializer, see vsapi/VintagestoryAPI.csproj).

### `IEventAPI / IServerEventAPI / IClientEventAPI`

`vsapi/Common/API/IEventAPI.cs:90, vsapi/Server/API/IServerEventAPI.cs, vsapi/Client/API/IClientEventAPI.cs`

Timers, callbacks and engine event hooks. Where machine ticks are registered.

Common: RegisterGameTickListener(Action<float>, intervalMs, initialDelayOffsetMs=0) :187 and the overloads taking an Action<Exception> errorHandler (:199/:222/:234) and a BlockPos (:210) — prefer the errorHandler overloads for machine ticks so one throw does not kill the listener silently. UnregisterGameTickListener :277. RegisterCallback(Action<float>, delayMs[, permittedWhilePaused]) :244/:254, RegisterCallback(…, BlockPos, delayMs) :263, UnregisterCallback :270. EnqueueMainThreadTask(action, code) :285 — the only safe way back onto the main thread. Event bus: PushEvent(name, IAttribute) :166, RegisterEventBusListener(handler, priority=0.5, filterByEventName) :175. Events: ChunkDirty (with EnumChunkDirtyReason NewlyCreated/NewlyLoaded/MarkedDirty, :18), MapRegionLoaded/Unloaded, OnEntitySpawn/Loaded/Death/Despawn, OnGetClimate, OnGetWindSpeed, OnTestBlockAccess. Server-only (IServerEventAPI): SaveGameLoaded, SaveGameCreated, GameWorldSave, ServerRunPhase(EnumServerRunPhase, handler) :241, PlayerJoin/PlayerNowPlaying/PlayerLeave/PlayerDisconnect, DidPlaceBlock/BreakBlock/DidBreakBlock/DidUseBlock/CanPlaceOrBreakBlock, ChunkColumnLoaded/Unloaded, AssetsFinalizers, plus all the worldgen registration hooks. Client-only (IClientEventAPI): RegisterRenderer :211 / UnregisterRenderer :230, RegisterItemstackRenderer :238, RegisterAsyncParticleSpawner :253, and a DummyRenderer helper class :77.

### `EnumServerRunPhase`

`vsapi/Server/EnumServerRunPhase.cs`

The server's launch state machine; read via sapi.Server.CurrentRunPhase and hooked via sapi.Event.ServerRunPhase.

Standby=-1, Start=0, Initialization=1, Configuration=2, AssetsReady=3 (LoadAssets is the obsolete alias), AssetsFinalize=4, ModsAndConfigReady=5 (LoadGamePre obsolete alias), GameReady=6 (LoadGame obsolete alias), WorldReady=7, RunGame=8, Shutdown=9, Exit=10. StartServerSide fires at ModsAndConfigReady; all blocks are registered by GameReady.

### `WildcardUtil`

`vsapi/Util/WildcardUtil.cs:10`

The matcher behind every block/item code wildcard, recipe ingredient and JSON `allowedVariants`.

Match(AssetLocation needle, AssetLocation haystack) :63 — the needle's Domain must equal the haystack's or be "*". Match(AssetLocation wildCard, AssetLocation inCode, string[] allowedVariants) :76 additionally requires the captured segment to be in allowedVariants (MatchesVariants :110). Match(string[] needles, string haystack) :53. fastMatch(needle, haystack) :145: if needle[0]=='@' the ENTIRE remainder is compiled as an anchored ^…$ Regex (cached in RegexCache :247 with a 1s timeout, CultureInvariant) — otherwise it is a hand-rolled '*'-glob with backtracking. Matching is ALWAYS case-insensitive (SameCharIgnoreCase :230). GetWildcardValue(wildCard, inCode) :130 extracts the first capture. WildCardReplace(code, search, replace) :20 rewrites a code by substituting captures into a second pattern's stars. ClearRegexCache :267. Internal Prepare(needle) :341 explains the three-way pre-analysis (null = no wildcard, plain string = single-star tail, ^…$ = full regex).

### `GameVersion`

`vsapi/Config/GameVersion.cs:31`

Version string parsing and comparison for the game and for mods.

SplitVersionString :106 normalizes "1.22" to "1.22.0", then splits on '.' and '-' and maps the suffix to a 4th component: -dev=0, -pre=1, -rc=2, missing (stable)=3. So "1.22.5" ranks ABOVE "1.22.5-rc.1". IsAtLeastVersion(version, reference) :207 and IsNewerVersionThan :235 compare component-by-component. IsLowerVersionThan :224 is defined as `!= && !IsNewerVersionThan`. IsCompatibleApiVersion :165 / IsCompatibleNetworkVersion :180 compare only major+minor. Constants: OverallMajorMinor "1.22" :36, OverallVersion "1.22.5" :41, APIVersion "1.22.0" :68, NetworkVersion "1.22.6" :73, WorldGenVersion 3 :81, ChunkdataVersion 2 :91.

### `OrderedDictionary<TKey,TValue> / LimitedList<T> / RelaxedReadOnlyDictionary<T,K> / ConcurrentSmallDictionary<K,V>`

`vsapi/Datastructures/Dictionary/OrderedDictionary.cs:22, vsapi/Datastructures/LimitedList.cs:12, vsapi/Datastructures/Dictionary/RelaxedReadOnlyDictionary.cs:10, vsapi/Datastructures/ConcurrentSmallDictionary.cs:19`

The four collection types mod code actually touches.

OrderedDictionary keeps insertion order and adds index access: GetValueAtIndex :152, GetKeyAtIndex :157, SetAtIndex :162, RemoveAt :141, InsertBefore :128, ValuesOrdered :292, ContainsValue :394, and a single-argument TryGetValue(key) :306 that returns default. Its own xmldoc warns it is inefficient — small sets only. NOTE it collides by name with System.Collections.Generic.OrderedDictionary (.NET 9+); IWorldAccessor.FastSearchRecipesByIngredient explicitly fully-qualifies the BCL one (vsapi/Common/API/IWorldAccessor.cs:780). LimitedList<T> is a ring-ish list that drops index 0 on overflow (Add :44); LastElement :78, IsFull :83, SetCapacity :60. RelaxedReadOnlyDictionary wraps another dictionary: the indexer returns default(K) instead of throwing (:19), every mutator throws InvalidOperationException — this is what RegistryObject.Variant is. ConcurrentSmallDictionary (namespace Vintagestory.Common) is the backing store of every TreeAttribute: low-lock, insertion-ordered, and its Keys/Values properties return FRESH ARRAY COPIES each call (:33-40).

### `ObjectCacheUtil / SerializerUtil / JsonUtil`

`vsapi/Util/ObjectCacheUtil.cs:8, vsapi/Util/SerializerUtil.cs:8, vsapi/Util/JsonUtil.cs:10`

The three utility facades mods lean on for caching, protobuf packets and domain-aware JSON.

ObjectCacheUtil.GetOrCreate<T>(api, key, factory) — the standard pattern for one-per-side cached meshes/animators; TryGet<T> hard-casts and will throw on a type mismatch; Delete(api, key). The cache is api.ObjectCache, which is PER SIDE (client and server have separate dictionaries even in singleplayer). SerializerUtil.Serialize<T>/Deserialize<T> :28/:58 are protobuf-net; DeserializeInto<T>(instance, data) :73 uses Serializer.Merge; Deserialize<T>(data, defaultValue) :89 null-guards; ToBytes(ByteWriteDelegate)/FromBytes(byte[], ByteReadDelegate) :99/:112 wrap a BinaryWriter/Reader. The FastMemoryStream overload :45 RESETS the stream — never nest it. JsonUtil.ToObject<T>(text, domain) :106 and the JToken extension :128 install a domain-aware AssetLocation converter so unqualified codes in a mod's JSON resolve to that mod's domain; PopulateObject :64/:90 merges JSON into an existing instance.


---

<a id="vssurvival-mp"></a>

# vssurvivalmod — mechanical power

# vssurvivalmod — Mechanical Power + container/GUI/catch-up map

All paths relative to `.compat/vintagestory/vssurvivalmod/`.
Namespace for everything under `Systems/MechanicalPower/` is `Vintagestory.GameContent.Mechanics`
(a few helpers such as `BEBehaviorRightClickConstructable` live in `Vintagestory.GameContent`).

## `Systems/MechanicalPower/` — 58 files, 6 sub-dirs

### `MechanicalPowerMod.cs` (root of the system, 388 lines)
The `ModSystem` + `IRenderer`. **Read this first — the design commentary at lines 24–49 is the
authoritative statement of intent** ("only power producers trigger creation of a mechanical network";
"block entities forget their networkid upon unloading").
- `MechPowerData` (l.16) — `networksById`, `nextNetworkId`, `tickNumber`. Reset wholesale on
  `Event_SaveGameLoaded` (l.223). **Networks are never persisted.**
- `Start` (l.70): client registers `EnumRenderStage.Before` renderer + `vsmechnetwork` channel;
  server registers a **20 ms** game tick listener (l.92).
- `OnServerGameTick` (l.152) — bumps `tickNumber`, ticks every `fullyLoaded` network with nodes,
  and every 10th tick runs the **overheat/smoke** pass (l.163–191).
- `RebuildNetwork` (l.241) — the network teardown/rediscovery routine. `OnNodeRemoved` (l.233).
- `GetOrCreateNetwork` (l.298), `CreateNetwork` (l.354), `DeleteNetwork` (l.365).
- `Event_ChunkDirty` (l.326) — the deferred-discovery driver for not-yet-loaded chunks.
- `AddDeviceForRender`/`RemoveDeviceForRender` (l.282–290) — the only hook into rendering.
- `OnRenderFrame` (l.376) — drives `MechanicalNetwork.ClientTick` for every network each frame.

### `Network/` — interfaces + the network object
| File | Contents |
|---|---|
| `MechanicalNetwork.cs` | `MechanicalNetwork` (l.15), `MechNetworkPacket` (l.350), `NetworkRemovedPacket` (l.361), `MechClientRequestPacket` (l.371). The physics is `updateNetwork` (l.203). |
| `IMechanicalPowerNode.cs` | Smallest unit: `GetTorque(tick, speed, out resistance)`, `LeaveNetwork()`, `GetPosition()`, `GearedRatio`, `OverheatValue`. |
| `IMechanicalPowerDevice.cs` | `enum EnumRotDirection` (l.8) + the full device contract: `OutFacingForNetworkDiscovery`, `GetPropagationDirection/Input`, `IsPropagationDirection`, `SetPropagationDirection`, `JoinAndSpreadNetworkToNeighbours`, `CreateJoinAndDiscoverNetwork`, `IsRotationReversed`, `IsInvertedNetworkFor`, `DestroyJoin`, `GetGearedRatio(face)`, `GetPropagatingTurnDir`. |
| `IMechanicalPowerBlock.cs` | 3 members only: `GetNetwork`, `HasMechPowerConnectorAt`, `DidConnectAt`. The **Block**-side contract. |
| `IMechanicalPowerRenderable.cs` | `AngleRad`, `Block`, `Position`, `LightRgba`, `AxisSign`, `Shape`. Implemented by BE *behaviors* and by two plain BEs (`BEClutch`, `BlockEntityGrindingWheel`). |
| `IMechanicalPowerTransfer.cs` | `internal`, one method `TransferPower`. **Nothing implements it** — dead. |

### `BlockEntityBehavior/` — the actual node implementations
| File | Class | Role |
|---|---|---|
| `BEBehaviorMPBase.cs` | `MechPowerPath` (l.14), `BEBehaviorMPBase` (l.55) | **The core.** 580 lines. Everything else derives from it. |
| `BEBehaviorMPRotor.cs` | `BEBehaviorMPRotor` | Abstract producer. `GetTorque` (l.88) is the torque model for all rotors. |
| `BEBehaviorMPConsumer.cs` | `BEBehaviorMPConsumer` | Generic dead-end consumer, JSON-configured (`resistance`, `mechPartShape`). `TrueSpeed` (l.23). |
| `BEBehaviorAxle.cs` | `BEBehaviorMPAxle` | Pass-through 2-way; also `IsAttachedToBlock` (static, l.104) and the stand meshing. |
| `BEBehaviorAngledGears.cs` | `BEBehaviorMPAngledGears` | 90° turns. `SetOrientations` (l.70) is the giant `AxisSign`/`turnDir1`/`turnDir2` table. |
| `BEBehaviorLargeGear3m.cs` | `BEBehaviorMPLargeGear3m` | `ratio = 5.5f` (l.12). Overrides `SetPropagationDirection`, `GetGearedRatio`, `GetMechPowerExits`. |
| `BEBehaviorMPSpurGear.cs` | `BEBehaviorMPSpurGear` | Side-by-side meshing gears; uses `SmallBoolArray`. `GetPropagatingTurnDir` returns `propagationDir.Opposite`. |
| `BEBehaviorToggle.cs` | `BEBehaviorMPToggle` | Helve-hammer drive. Resistance 0.125 with hammer, 0.0005 without. Speed-clamps on join. |
| `BEBehaviorTransmission.cs` | `BEBehaviorMPTransmission` | Clutch-controlled bridge; `GetMechPowerExits` returns empty when disengaged. |
| `BEBehaviorBrake.cs` | `BEBehaviorMPBrake` | extends `BEBehaviorMPAxle`; resistance ramps 0→3 over ~60 s. |
| `BEBehaviorCreativeRotor.cs` | `BEBehaviorMPCreativeRotor` | `TargetSpeed = 0.1*speedSetting`, `TorqueFactor = 0.5*powerSetting`. |
| `BEBehaviorWindmillRotor.cs` | `ModSystemWindTurbulence` (l.20), `BEBehaviorWindmillRotor` (l.110) | Wind producer + the region-based turbulence registry. |
| `BEBehaviorMPWaterWheel.cs` | `BEBehaviorMPWaterWheel` | Water producer. `CheckWater` (l.90) samples a ring of points and computes a moment. |
| `BEBehaviorPulverizer.cs` | `BEBehaviorMPPulverizer` | Consumer + particle/sound host for the pounders. |
| `BEBehaviorArchimedesScrew.cs` | `BEBehaviorMPArchimedesScrew` | Consumer that gates item flow. |
| `BEBehaviorRightClickConstructable.cs` | `BEBehaviorRightClickConstructable` (ns `GameContent`) | Multi-stage build-in-place; used by the water wheel via `OnShapeChanged`/`IsComplete`. |

### `Block/` — the `IMechanicalPowerBlock` side
`BlockMPBase.cs` (abstract `BlockGeneric` + `IMechanicalPowerBlock`; `WasPlaced` l.15, `tryConnect`
l.39, `GetNetwork` l.53, `ExchangeBlockAt` l.59) is the base for:
`BlockAxle`, `BlockAngledGears`, `BlockLargeGear3m`, `BlockToggle`, `BlockTransmission`,
`BlockBrake`, `BlockSpurGear`, `BlockPulverizer`, `BlockArchimedesScrew`, `BlockWindmillRotor`,
`BlockWaterWheel` (extends `BlockWindmillRotor`), `BlockCreativeRotor`,
plus `Block/BlockMPConsumer.cs` and `Block/BlockQuern.cs` **outside** this folder.
Non-`BlockMPBase` blocks here: `BlockClutch` (plain `Block`), `BlockHelvehammer` (plain `Block`),
`BlockMPMultiblockGear` / `BlockMPMultiblockPulverizer` (invisible filler blocks).
`IMPPowered.cs` — empty marker interface, used **only** to forbid rotor-to-rotor placement
(`BlockWindmillRotor.GetFacingForPlacement` l.61, `BlockCreativeRotor` l.45, `BlockWaterWheel.CanPlaceBlock` l.73).

### `BlockEntity/` (inside MechanicalPower)
`BEBrake`, `BEClutch`, `BEHelveHammer`, `BELargeGear3m`, `BEMultiblock.cs`→`BEMPMultiblock`,
`BEPulverizer` (+ `InventoryPulverizer` at l.411), `BEArchimedesScrew.cs`→`BlockEntityArchimedesScrew`,
`IGearAcceptor.cs` (`CanAcceptGear`/`AddGear`/`RemoveGearAt`).

### `Renderer/` — instanced GPU rendering
`MechNetworkRenderer.cs` — the dispatcher. `RendererByCode` (l.23) maps the JSON string
`Block.Attributes.mechanicalPower.renderer` to a `MechBlockRenderer` subclass:
`generic`, `angledgears`, `angledgearcage`, `transmission`, `clutch`, `pulverizer`, `autorotor`.
`MechBlockRenderer.cs` — abstract base; `UpdateCustomFloatBuffer` (l.45) is where `dev.AngleRad` is read.
Concrete: `GenericMechBlockRenderer`, `AngledGearBlockRenderer.cs`→`AngledGearsBlockRenderer`,
`AngledCageGearRenderer`, `TransmissionBlockRenderer`, `ClutchBlockRenderer`,
`CreativeRotorRenderer`, `PulverizerRenderer`.

## Mechanical-power files OUTSIDE `Systems/MechanicalPower/`
- `Block/BlockMPConsumer.cs` — the generic consumer block (`HasMechPowerConnectorAt` == `face == Facing`).
- `Block/BlockQuern.cs` — `BlockMPBase`; connector is UP or DOWN only (l.123).
- `BlockEntity/BEQuern.cs` — `BlockEntityQuern : BlockEntityOpenableContainer`; the canonical
  "hand-crank or MP-driven" machine. `CreateBehaviors` (l.208) wires `mpc.OnConnected/OnDisconnected`.
- `BlockEntity/BEGrindingWheel.cs` — `BlockEntityGrindingWheel : BlockEntity, IMechanicalPowerRenderable`
  (a plain BE that registers itself for MP rendering, l.58).
- `BlockEntity/BlockEntityMechPoweredBellows.cs` — MP consumer driving a **skeletal animation**
  by frame-setting (`animState.CurrentFrame = mpc.AngleRad / TWOPI * 60`, l.68).
- `BlockEntityRenderer/QuernTopRenderer.cs`, `BlockEntityRenderer/HelveHammerRenderer.cs` — bespoke
  (non-instanced) renderers that read `AngleRad` off the MP behavior.
- `Systems/Core.cs` — all registrations: blocks l.495–639, BE behaviors l.709–754, BEs l.860–870.

## Container / GUI files
- `BlockEntity/BEOpenableContainer.cs` — `EnumBlockContainerPacketId` (l.16, `OpenInventory=5000`,
  `OpenLidOthers=5001`), `OpenContainerLidPacket`, `BlockEntityContainerOpen` (byte-stream
  ser/deser of an inventory, l.41), `CreateDialogDelegate`, `BlockEntityOpenableContainer` (l.81).
- `BlockEntity/BEContainer.cs` — `BlockEntityContainer` base (`InWorldContainer`, 10 s `OnTick`).
- `BlockEntity/BEGenericTypedContainer.cs` — `BlockEntityGenericTypedContainer` (l.16): variant-typed
  inventory sizing from `Block.Attributes`, per-player inventories, lid animations, mesh caching.
- `Gui/GuiDialogBlockEntityQuern.cs` — model for a custom machine dialog with a progress arrow.
- vsapi: `vsapi/Client/UI/Dialog/GuiDialogBlockEntity.cs` — `EnumBlockEntityPacketId { Open=1000, Close=1001 }` at l.196.

## Away-catch-up / calendar files (no MP machine does catch-up)
- `BlockEntity/BlockEntityFastForwardGrowth.cs` — **the reference implementation** of fast-forward:
  `Update` (l.49), rollback guard (l.56), one-year clamp (l.70), 3–4 h interval loop (l.110).
- `BlockEntity/BEForge.cs` `OnCommonTick200ms` (l.197) — simplest `hoursPassed` pattern.
- `BlockEntity/BEBoiler.cs` (l.83), `BEBeehive.cs` (l.173), `BEBeeHiveKiln.cs` (l.280),
  `BESpawner.cs` (l.240), `BEFruitpress.cs` (l.274) — same `TotalHours - lastTick` idiom.

## Key types

### `MechanicalPowerMod`

`vssurvivalmod/Systems/MechanicalPower/MechanicalPowerMod.cs:51`

The ModSystem owning all networks, the server tick, the client render tick, and the `vsmechnetwork` network channel. Loaded on both sides (`ShouldLoad` returns true unconditionally, l.65).

`OnServerGameTick(float)` l.152 — registered at **20 ms** (l.92); increments `data.tickNumber` and calls `network.ServerTick(dt, tickNumber)` only for networks that are `fullyLoaded && nodes.Count > 0`. Every 10th tick it walks all nodes and applies `OverheatValue` (l.163–191): `nodespeed = |GearedRatio * network.Speed|`; >4.5 spawns smoke, >5.5 accumulates overheat up to 1.1; the actual fire-setting is commented out (l.188 "Disabled until stable"). `RebuildNetwork(network, nowRemovedNode)` l.241 — sets `Valid=false`, deletes the network server-side, makes every node `LeaveNetwork()`, then for each node with a non-null `OutFacingForNetworkDiscovery` (and that is not the removed node) calls `CreateJoinAndDiscoverNetwork` and **copies speed/AngleRad/TotalAvailableTorque/NetworkResistance onto the new network, negating speed and torque if the node's propagation direction flipped** (l.272–276). `CreateNetwork` l.354 sets `fullyLoaded = true` immediately. `Event_ChunkDirty` l.326 — the deferred-discovery pump. `Event_SaveGameLoaded` l.223 throws away all network data. `getTickNumber()` l.103.

### `MechanicalNetwork`

`vssurvivalmod/Systems/MechanicalPower/Network/MechanicalNetwork.cs:15`

One rotating group: a `Dictionary<BlockPos, IMechanicalPowerNode>`, a signed `speed`, an `angle`, and cached torque/resistance sums. `[ProtoContract]` but only ever serialized as the small `MechNetworkPacket`.

`updateNetwork(long tick)` l.203 — the whole physics, run every 5th server tick (≈100 ms). Sums `totalTorque += r*t` and `totalResistance += |r|*resistance + speed²·r²/1000` over all nodes where `r = node.GearedRatio` and `t = node.GetTorque(tick, speed*r, out resistance)` — note **the speed handed to each node is already multiplied by that node's ratio**. `drag = max(1, nodes.Count^0.25)`, `step = 1/drag` (l.240–241): larger networks accelerate and decelerate more slowly. Accel is capped: `speed += min(0.05, step*unusedTorque)*torqueSign` (l.246). Decel clamps `change` to `-|speed|` (l.252) giving momentum. `TotalAvailableTorque` creeps toward `totalTorque` by `step` per update, otherwise decays ×0.9 (l.264–275). `ServerTick` l.156 — `UpdateAngle(speed*dt*50)`, `updateNetwork` every 5 ticks, `broadcastData` every 40 ticks (≈800 ms). `ClientTick` l.130 — **early-returns when `speed < 0.001f`**, eases `clientSpeed` toward `speed` at ±0.01·f per frame, then nudges `angle` toward `serverSideAngle`. `UpdateAngle(speed)` l.185 is `angle += speed/10f` — the /10 is the radians-per-"speed-unit" conversion. `UpdateFromPacket` l.280 — **`speed = Math.Abs(packet.speed)`**; the sign lives only in `TurnDir`. `Join`/`Leave` l.91/106 maintain `inChunks` refcounts. `testFullyLoaded(api)` l.300. `ReadFromTreeAttribute`/`WriteToTreeAttribute` l.313/324 are **dead code — nothing calls them**.

### `BEBehaviorMPBase`

`vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPBase.cs:55`

Abstract `BlockEntityBehavior` implementing `IMechanicalPowerDevice`. Every mechanical block entity gets one of its subclasses. Owns `propagationDir`, `gearedRatio`, the network reference, and the whole discovery algorithm.

`Initialize` l.148 — order matters: `Shape = GetShape()`; grab the manager; client-side rejoin from `NetworkId` if >0; `AddDeviceForRender(this)`; **then** `AxisSign = {0,0,1}` and `SetOrientations()`; **then**, server-side only and only if `OutFacingForNetworkDiscovery != null`, `CreateJoinAndDiscoverNetwork`. `CreateJoinAndDiscoverNetwork(BlockFacing)` l.419 — creates a network if the neighbour has none/invalid one, else joins the neighbour's. `tryConnect(toFacing, out unloadedChunkPos)` l.205 — the placement-time join path. `JoinAndSpreadNetworkToNeighbours` l.486 and `spreadTo` l.516 — the recursive flood-fill; `spreadTo` sets `beMechBase.Api = api` (l.532) because target BEs may not be `Initialize`d yet. `GetMechPowerExits(MechPowerPath entryDir)` l.556 — **the one method you override to define topology**; the default returns entryDir plus its opposite with `invert` flipped. `SetPropagationDirection(MechPowerPath)` l.390 — writes `propagationDir` AND `GearedRatio`, and flips `network.TurnDir`/`DirectionHasReversed` when the direction reverses. `AngleRad` l.114 — `(network.AngleRad * gearedRatio) % TWOPI`, or `TWOPI - that` when `IsRotationReversed()`; caches into `lastKnownAngleRad` so a networkless block freezes at its last angle. `IsRotationReversed()` l.129 — **true iff `propagationDir` is DOWN, EAST or SOUTH**. `ToTreeAttributes` l.357 writes `networkid`, `turnDirFromFacing` (`propagationDir.Index`) and `g` (gearedRatio); `FromTreeAttributes` l.316 reads them **client-side only**. `GetTorque` l.403 returns 0 by default (pure consumer); `GetResistance()` l.409 is abstract. `OnBlockRemoved` l.289 → `manager.OnNodeRemoved` → `RebuildNetwork`. `OnPlacementBySchematic` l.564 tries all six faces.

### `MechPowerPath`

`vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPBase.cs:14`

The token passed along during discovery: which face power exits through, whether the sense is inverted, the accumulated gearing ratio, and optionally an explicit turn direction. It is the only carrier of gear ratio and rotation sense between blocks.

`OutFacing`, `invert`, `gearingRatio`, `turnDir`, private `fromPos`. `NetworkDir()` l.36 = `turnDir ?? (invert ? OutFacing.Opposite : OutFacing)` — **an explicit `turnDir` overrides `invert` entirely**. `IsInvertedTowards(testPos)` l.41 — if `fromPos` is null it just returns `invert`, otherwise it tests whether `fromPos + NetworkDir()` equals `testPos`; so *whether you passed `fromPos` to the constructor changes the answer*. `PropagatedClone(outfacing, inverted, turnDir, fromPos)` l.46 preserves `gearingRatio` only.

### `BEBehaviorMPRotor`

`vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPRotor.cs:10`

Abstract base for every power producer (windmill, water wheel, creative rotor). Defines the producer contract as four virtual properties and one torque formula.

Constructor (l.51) reads `Block.Variant["side"]` and sets `OutFacingForNetworkDiscovery = ownFacing.Opposite` — **this is what makes a block a producer**. Overrides to supply: `Resistance`, `AccelerationFactor`, `TargetSpeed` (0..1, the speed this rotor wants the network to run at), `TorqueFactor`, `Sound`, `GetSoundVolume()`. `GetTorque` l.88: `capableSpeed += (TargetSpeed - capableSpeed) * AccelerationFactor`; `dir = propagationDir == OutFacingForNetworkDiscovery ? 1 : -1`; `wrongDirection = dir*speed < 0`; resistance is `Resistance*TorqueFactor*min(0.8, |speed|*400)` when forced backwards, `Resistance*min(0.2, excess²*80)` when overspeeding, else 0; returns `max(0, wrongDirection ? capableSpeed : capableSpeed-|speed|) * TorqueFactor * dir`. `GetMechPowerExits` returns `Array.Empty` — **rotors are dead ends, power never passes through them**. `WasPlaced` is overridden to a no-op (l.127). `AngleRad` getter (l.37) **plays a sound as a side effect** on the client.

### `BEBehaviorMPConsumer`

`vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPConsumer.cs:11`

The generic, JSON-configured consumer. Attach `{ "name": "MPConsumer", "properties": { "resistance": 0.3, "mechPartShape": {...} } }` to any block entity and it becomes a mechanical sink.

`Initialize` l.27 reads `properties["mechPartShape"]` (defaults to `Block.Shape`, path-prefixed with `shapes/` and suffixed `.json`) and `properties["resistance"]` (default 0.1), then sets `AxisSign` from `Block.Variant["side"]`. `TrueSpeed` l.23 = `|Network.Speed * GearedRatio|` — **the number consumers should actually use**, already ratio-corrected and unsigned. `OnConnected`/`OnDisconnected` `Action` fields (l.15–16) fired from the overridden `JoinNetwork`/`LeaveNetwork` — this is how `BlockEntityQuern` learns it became automated. `GetMechPowerExits` returns empty (dead end).

### `BEBehaviorMPLargeGear3m`

`vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorLargeGear3m.cs:10`

The 3 m large gear — the only vanilla gear-ratio changer. `ratio = 5.5f` (l.12).

`SetPropagationDirection` l.45 — **does not call base**. If the incoming turn is horizontal it forces `propagationDir` to UP or DOWN (via `path.IsInvertedTowards(Position)`) and sets `GearedRatio = path.gearingRatio / ratio`; if vertical it keeps `gearingRatio` unchanged. `GetGearedRatio(face)` l.79 — `face.IsHorizontal ? GearedRatio*ratio : GearedRatio` — the side-sensitive accessor that lets neighbours see the right ratio. `GetMechPowerExits` l.85 builds `2 + beg.CountGears(Api)` paths, multiplying/dividing by `ratio` on the horizontal↔vertical transitions. `IsPropagationDirection` l.65 special-cases horizontal tests to mean DOWN/UP. `GetSmallgearAngleRad()` l.142 = `AngleRad * ratio` — used by the cage-gear renderer.

### `BEBehaviorMPAngledGears`

`vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorAngledGears.cs:13`

90° power turns and the small gears that mesh with a large gear. The most intricate sign handling in the system.

`SetOrientations` l.70 — a 20-case switch on `BlockAngledGears.Orientation` setting `AxisSign` (**3-element for single-face variants, 6-element for two-face variants** — the renderer branches on `AxisSign.Length < 4`), `axis1`/`axis2` (render-reverse markers), `turnDir1`/`turnDir2`, and `orientation`. Its first block (l.76–83) re-maps `propagationDir` when the variant changes after a neighbour break. `SetPropagationDirection` l.278 rotates the incoming turn dir through the `turnDir1/turnDir2` mapping before delegating to base — note the dangling `if (this.turnDir1 != null)` with no braces at l.281–282, which guards only the first `if` of the chain. `GetMechPowerExits` l.359 — output side is `!invert` relative to input (an angled gear reverses sense); `newlyPlaced` (l.376) suppresses removal of the input side so a fresh gear searches both faces. `AngleRad` l.23 applies an extra flip for DOWN/WEST. `AddToLargeGearNetwork` l.248. `LargeGearAngleRad` l.259.

### `BEBehaviorMPToggle`

`vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorToggle.cs:12`

The helve-hammer drive shaft. Notable because it is the one node whose resistance depends on a *neighbouring* block entity's contents.

`GetResistance()` l.70 — looks at the two `sides[]` positions for a `BEHelveHammer` with a non-null `HammerStack`; returns **0.125 with a hammer, 0.0005 without**. The exponential speed limiter is present but commented out (l.87–91, "tyron 5dec 2025: disabled. Causes the mechanical network speed to oscillate heavily"). `JoinNetwork` l.94 — **mutates the network it is joining**: if `|network.Speed * GearedRatio| * 1.6 > 1` it divides both `network.Speed` and `network.clientSpeed` by that factor. `BEBehaviorMPPulverizer.JoinNetwork` (BEBehaviorPulverizer.cs:116) does the identical thing.

### `BEBehaviorMPTransmission`

`vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorTransmission.cs:12`

A switchable network bridge driven by an adjacent `BEClutch`. The vanilla answer to "how do I connect/disconnect two networks at runtime".

`GetMechPowerExits` l.84 returns `Array.Empty` when `!engaged` — that alone severs the network. `ChangeState(bool)` l.96 — on engage: `CreateJoinAndDiscoverNetwork(orients[0])`, then `orients[1]`, then `tryConnect(orients[0])`; on disengage: `manager.OnNodeRemoved(this)` (i.e. a full rebuild). `CheckEngaged(access, updateNetwork)` l.66 polls the clutch on either lateral side. `RotationNeighbour(side, allowIndirect)` l.114 — reads the *neighbour's* `Network.AngleRad * GearedRatio` and inverts based on `GetPropagationDirection() != orients[side]`, caching in `rotPrev` so the visual doesn't snap when the neighbour disappears. `BlockTransmission.GetNetwork` (Block/BlockTransmission.cs:85) returns null when disengaged, so discovery can't walk through it.

### `BEBehaviorMPWaterWheel`

`vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorMPWaterWheel.cs:16`

Water producer built on `BEBehaviorMPRotor` + `BEBehaviorRightClickConstructable`. `TargetSpeed = min(0.3f, flowRate)` — the hard practical speed ceiling of vanilla water power.

`CheckWater(dt)` l.90, on a 1 s tick both sides — walks `4 + radius*4` points round the rim, rejects if any is side-solid (`blocked`, which raises `Resistance` to 1), accumulates `moment += (dX,dY,dZ).Normalize().Cross(axialVec).Dot(pushVector)` for flowing water with `Attributes["flowspeed"] > requiresMinFlowSpeed` (default 1.5), multiplies by `radius`, then `flowRate = |moment * 750|`. **It also mutates the world**: `ReplaceRapidWater` (l.230) converts the downstream rapid-water block into ordinary water. `dir = Math.Sign(moment) * -1` and, when it changes, calls `SetPropagationDirection` directly (l.217) — a producer rewriting its own propagation at runtime. `CreateJoinAndDiscoverNetwork` l.253 is overridden to also enqueue a main-thread `tryConnect` on the *opposite* axle face. `AngleRad => base.AngleRad * dir` (l.37). `GetShape()` returns the constructable's staged shape.

### `BEBehaviorWindmillRotor / ModSystemWindTurbulence`

`vssurvivalmod/Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorWindmillRotor.cs:110`

Wind producer plus a server-only registry of turbulence sources keyed by map region, used to halve the output of windmills built too close together.

`TargetSpeed => min(0.6f, windSpeed)`; `TorqueFactor => sailLength/4f * powerMul * (turbulenceExposed ? 0.5f : 1f)` (l.122–123 — the comment insists on `/4f` so 5 sails read as 125%). `Resistance => 0.003f`, `AccelerationFactor => 0.05d`. `CheckWindSpeed` l.149 (1 s) zeroes wind below sunlight 5 unless world config `undergroundWindmills` is `"true"`, and on 10% of ticks re-checks turbulence and obstruction; if obstructed it drops all sails and calls `network.updateNetwork(manager.getTickNumber())` **synchronously** (l.197). `ModSystemWindTurbulence.SetTurbulence/RemoveTurbulence/GetNearestTurbulenceDistance` l.32/38/50 — server-side only, `ShouldLoad` l.23. Registration/removal is in `Initialize` l.144, `OnBlockRemoved` l.304, `OnBlockUnloaded` l.313.

### `BlockMPBase`

`vssurvivalmod/Systems/MechanicalPower/Block/BlockMPBase.cs:8`

`abstract class BlockMPBase : BlockGeneric, IMechanicalPowerBlock`. The Block-side half of the system: it answers "is there a shaft socket on this face" and triggers connection on placement.

`HasMechPowerConnectorAt(world, pos, face, forBlock)` — abstract; **the single most important method to get right for a new MP block**. Note it receives `forBlock`, which `BlockSpurGear` uses to allow side-by-side meshing only between identical gears (Block/BlockSpurGear.cs:22). `DidConnectAt(world, pos, face)` — abstract; most implementations are empty, but `BlockAngledGears.DidConnectAt` (Block/BlockAngledGears.cs:91) **swaps the block for a different variant**, which is why `tryConnect` calls it before reading propagation direction (BEBehaviorMPBase.cs:222). `WasPlaced(world, ownPos, connectedOnFacing)` l.15 — when facing is null it scans the four horizontals. `tryConnect(world, byPlayer, pos, face)` l.39. `GetNetwork` l.53 pulls the network off the BE behavior. `ExchangeBlockAt(world, pos)` l.59 — swaps block id in place, then `SetOrientations()`, `Shape`, `MarkDirty()` on the surviving BE.

### `BlockEntityOpenableContainer`

`vssurvivalmod/BlockEntity/BEOpenableContainer.cs:81`

Base for every BE with an inventory GUI. Handles the open/close handshake, permission checks and lid-animation broadcast.

`toggleInventoryDialogClient(byPlayer, CreateDialogDelegate)` l.118 — the client-side entry point: creates the dialog, hooks `OnClosed` to send `EnumBlockEntityPacketId.Close`, `TryOpen()`, then sends `Inventory.Open(byPlayer)` **and** a `EnumBlockEntityPacketId.Open` block-entity packet (the comment at l.131 notes the first is probably a no-op). `OnReceivedClientPacket` l.140 — **packetid < 1000 is routed to `Inventory.InvNetworkUtil.HandleClientPacket` after a `CachedAccessPerms` check, and marks the chunk modified** (l.167); `Open`/`Close` (1000/1001) manage `player.InventoryManager` and broadcast `OpenLidOthers`. `OnReceivedServerPacket` l.193 handles `OpenInventory` (5000) by deserializing `BlockEntityContainerOpen` and constructing a `GuiDialogBlockEntityInventory`. `OpenSound`/`CloseSound` come from `Block.Attributes["openSound"/"closeSound"]` (l.104). `LidOpenEntityId` HashSet tracks who holds the lid open. `Dispose` l.264 closes the dialog and clears `openedByPlayerGUIds` server-side.

### `BlockEntityGenericTypedContainer`

`vssurvivalmod/BlockEntity/BEGenericTypedContainer.cs:16`

The chest/basket/crate implementation: one BE class whose inventory size, columns, dialog title, sounds, spoil rates and shape all come from `Block.Attributes[...][type]`. The model to copy for a data-driven container.

`InitInventory(Block)` l.178 — reads `collisionSelectionBoxes`, `inventoryClassName`, `dialogTitleLangCode[type]`, `quantitySlots[type]`, `quantityColumns[type]`, `retrieveOnly[type]`, `typedOpenSound[type]`, `typedCloseSound[type]`, `spoilSpeedMulByFoodCat[type]`, `transitionSpeedMul[type]`; then builds `InventoryGeneric` or `InventoryPerPlayer`. `FromTreeAttributes` l.115 re-inits the inventory when `type` or `isPerPlayer` changed, and **assigns `this.Api = worldForResolving.Api` if Api is null** (l.152) so `LateInitInventory` works during load. `OnPlayerRightClick` l.316 runs entirely server-side: sends `BlockEntityContainerOpen.ToBytes(...)` as packet 5000 to the one player, then broadcasts the lid packet to everyone else. `OpenLid`/`CloseLid` l.266/281 drive the `lidopen` animation through `BEBehaviorAnimatable.animUtil`. `GenMesh` l.348 caches meshes and shapes in `Api.ObjectCache` keyed by block code + type + subtype + rndTexNum. `OnTransformed` l.442 implements `IRotatable` by adjusting `meshAngle`.

### `MechBlockRenderer / MechNetworkRenderer`

`vssurvivalmod/Systems/MechanicalPower/Renderer/MechBlockRenderer.cs:9`

Instanced rendering: one draw call per (shape, renderer-code) pair for all devices of that kind in the world. Devices register through `MechanicalPowerMod.AddDeviceForRender`.

`MechNetworkRenderer.AddDevice` (MechNetworkRenderer.cs:49) — returns immediately if `device.Shape == null`; picks the renderer class from `Block.Attributes["mechanicalPower"]["renderer"]` (default `"generic"`) and buckets by `device.Shape.GetHashCode() + rendererCode.GetHashCode()`. `MechBlockRenderer.UpdateCustomFloatBuffer` l.45 — iterates `renderedDevices` each frame and calls `UpdateLightAndTransformMatrix(i, distToCamera, dev.AngleRad % TWOPI, dev)`; **`dev.AngleRad` is read once per frame per device, and several implementations do real work in that getter**. `UpdateLightAndTransformMatrix(values, index, distToCamera, lightRgba, rotX, rotY, rotZ)` l.63 — builds a translate→quaternion→translate matrix into 20 floats (4 light + 16 matrix). Buffers are pre-allocated for **10100 instances** per shape (`GenericMechBlockRenderer.cs:37`), 200 for the pulverizer, 2100 for the creative rotor. `MechNetworkRenderer.OnRenderFrame` l.85 uses the `"instanced"` shader created by vsessentialsmod's Core.


---

<a id="vssurvival-blocks"></a>

# vssurvivalmod — blocks, heat, recipes, handbook

# `vssurvivalmod` — blocks & block entities map (v1.22.5, ~885 .cs)

Root: `.compat/vintagestory/vssurvivalmod`. Namespace is `Vintagestory.GameContent` almost
everywhere (exceptions noted). Most files carry `#nullable disable`; the newer ones
(`BEIngotMold.cs`, `BlockLiquidContainerBase.cs` in parts, `BEBeeHiveKiln.cs`) are nullable-enabled.

## The registration index — read this first
`Systems/Core.cs` (1098 lines) is the single authoritative "what class is called what in JSON" table.
Line ranges:
| method | lines | registers |
|---|---|---|
| `RegisterDefaultBlocks()` | 384–643 | `api.RegisterBlockClass("BlockFirepit", …)` etc. |
| `RegisterDefaultBlockBehaviors()` | 644–704 | `"HorizontalAttachable"`, `"Multiblock"` is NOT here (it's in vsessentialsmod) |
| `RegisterDefaultBlockEntityBehaviors()` | 705–768 | `"TemperatureSensitive"`, `"Burning"`, `"Door"` … |
| `RegisterDefaultCollectibleBehaviors()` | 769–787 | `"GroundStorable"`, `"Quenchable"` … |
| `RegisterDefaultBlockEntities()` | 788–925 | `api.RegisterBlockEntityClass("Firepit", …)` |
| `RegisterDefaultCropBehaviors()` | 926–931 | |
| `RegisterDefaultItems()` | 932–1028 | `"ItemIngot"`, `"ItemWorkItem"`, `"ItemOre"` … |
| `RegisterDefaultEntities()` / `…EntityBehaviors()` | 1029–1098 | |
`Start(ICoreAPI)` is at line 106, `StartClientSide` 178, `StartServerSide` 197.

## `Block/` (167 files) — Block subclasses
Naming: `Block<Thing>.cs`. Heat/metal-relevant ones:
- `BlockBloomery.cs` (206) — `Block, IIgnitable`. Interaction help built with `ObjectCacheUtil.GetOrCreate`;
  chimney is placed by right-clicking the bloomery with a `bloomerychimney` stack (l.131–148);
  `OnBlockBroken` breaks the block above too (l.165–174).
- `BlockForge.cs` (223) — `Block, IIgnitable`. `DoPlaceBlock` snaps `MeshAngleRad` to 90° (l.131–147).
  Fire particles borrowed from the vanilla `fire` block's `ParticleProperties` (l.170–182).
- `BlockAnvil.cs` (182) / `BlockAnvilPart.cs` — `MetalTier` property drives `BEAnvil.OwnMetalTier`.
- `BlockSmeltingContainer.cs` (295) — the **crucible**. Overrides `GetMeltingPoint/Duration/CanSmelt/DoSmelt`
  for a multi-slot container; `GetMatchingAlloy`, `GetSingleSmeltableStack`, `GetMetal` (static).
- `BlockSmeltedContainer.cs` (477) — the **filled crucible**; pouring into any `ILiquidMetalSink`,
  `GetContents/SetContents` on the itemstack (`output` stack + `units` int), `HasSolidifed`.
- `BlockIngotMold.cs`, `BlockToolMold.cs` — dual-form molds; `GetDrops` reads the BE.
- `BlockFirepit.cs`, `BlockCharcoalPit.cs`, `BlockCoalPile.cs`, `BlockBellows.cs`, `BlockPitkiln.cs`,
  `BlockBeeHiveKilnDoor.cs` (103), `BlockStoneCoffinSection.cs` (204), `BlockCokeOvenDoor.cs`,
  `BlockGasifier.cs`, `BlockCondenser.cs`, `BlockBoiler.cs`.
- `BlockContainer.cs` (295) — base for anything that carries an inventory **inside its itemstack**
  (`contents` tree attribute); `SetContents/GetContents`, `ucontents` JSON form, overrides
  `SetTemperature` to fan out to contents, `OnBlockBroken` drops one `OnPickBlock()` stack.
- `BlockMultiblock`-adjacent helpers: `BlockShapeFromAttributes.cs`, `BlockMaterialFromAttributes.cs`.
- Generic infra: `BlockGenericTypedContainer.cs`, `BlockRockTyped.cs`, `BlockTinted.cs`, `BlockLayered.cs`.

## `BlockEntity/` (82 files) — `BE<Thing>.cs`
- `Firepit/BEFirepit.cs` (992) + `Firepit/FirepitInterfaces.cs` (74; `IFirePit`, `IInFirepitRendererSupplier`,
  `IInFirepitMeshSupplier`, `InFirePitProps`, `EnumFirepitModel`).
- `BEBloomery.cs` (432), `BEForge.cs` (513), `BEAnvil.cs` (1329), `BEAnvilPart.cs`,
  `BESmeltedContainer.cs` (68), `BEIngotMold.cs` (877, declares `ILiquidMetalSink`), `BEToolMold.cs`,
  `BEBellows.cs` (`IBellowsAirReceiver`), `BECoalPile.cs` (607), `BECharcoalPit.cs` (449),
  `BEPitKiln.cs` (572), `BEBeeHiveKiln.cs` (608), `BEStoneCoffin.cs` (737, cementation furnace),
  `BEGroundStorage.cs` (fuel pile that `IsBurning`/`GetHoursLeft`/`OnExternalTick`).
- `BEContainer.cs` (185) `BlockEntityContainer : BlockEntity, IBlockEntityContainer` — the inventory
  serialization + drop-all-on-break base. `BEOpenableContainer.cs` adds the GUI packet protocol.
- `BEItemFlow.cs` (hopper), `BEGenericContainer.cs`, `BECrate.cs`, `BEBarrel` lives under `Systems/Barrel/`.
- `Unfinished/BEBlastFurnace.cs` (287) and `Unfinished/BEStove.cs` — **dead code**, an abandoned
  vanilla blast-furnace prototype (`FurnaceSection : BlockEntityOpenableContainer`). Not registered.
- `Legacy/BlockEntity/` — pre-1.13 pile block entities kept for world upgrade only.

## `BlockBehavior/` (46) and `BlockEntityBehavior/` (22)
- `BehaviorHeatSource.cs`, `BehaviorCanIgnite.cs` (has `BlockBehaviorCanIgnite.CanIgniteStacks(api,bool)`
  used everywhere for interaction help), `BehaviorIgnitable.cs`, `BehaviorChimney.cs`,
  `BehaviorUnstableFalling.cs`, `BehaviorDoor.cs`, `BehaviorWrenchOrientable.cs`,
  `BehaviorHorizontalOrientable.cs`, `BehaviorOmniRotatable.cs`, `BehaviorReinforcable.cs`.
- `BEBehaviorTemperatureSensitive.cs` (126) — **declares `ITemperatureSensitive` and
  `OnStackToCool`**; the JSON code is `"TemperatureSensitive"`. `BEBehaviorBurning.cs` (fire spread),
  `BEBehaviorDoor.cs`, `BEBehaviorFirepitAmbient.cs`, `BEBehaviorShapeFromAttributes.cs`.

## `BlockEntityRenderer/` (18)
`IngotMoldRenderer.cs` (175), `ToolMoldRenderer.cs`, `ForgeContentsRenderer.cs`,
`BloomeryContentsRenderer.cs`, `FirepitContentsRenderer.cs`, `AnvilWorkItemRenderer.cs`,
`ClayFormRenderer.cs`, `KnappingRenderer.cs`, `QuernTopRenderer.cs`, `GroundStorageRenderer.cs`.
All are `IRenderer` registered in `BE.Initialize` with `capi.Event.RegisterRenderer(…, EnumRenderStage.Opaque, "name")`
and disposed in **both** `OnBlockRemoved` and `OnBlockUnloaded`.

## `Item/` (71)
`ItemIngot.cs` (168, `IAnvilWorkable`), `ItemWorkItem.cs` (291), `ItemIronBloom.cs` (195),
`ItemMetalPlate.cs`, `ItemNugget.cs`, `ItemOre.cs`, `ItemCoal.cs`, `ItemChisel.cs`,
`ItemHammer.cs`, `ItemTongs.cs`, `ItemLiquidPortion.cs`, `ItemClay.cs`, `ItemFlint.cs`.

## `Systems/` — the subsystems
- `Systems/Core.cs` — registration (above). `Systems/IAnvilWorkable.cs` (48) — the smithing contract.
- `Systems/Temperature.cs` (162) — **`ModTemperature`, only ambient/climate temperature**
  (`api.Event.OnGetClimate`). Nothing to do with itemstack heat. Do not confuse the two.
- `Systems/Recipe/` — `AlloyRecipe.cs` (256), `SmithingRecipe.cs` (70), `KnappingRecipe.cs` (48),
  `ClayFormingRecipe.cs` (56), `MetalAlloyIngredient.cs` (73), `RecipeLoader.cs` (145,
  **namespace `Vintagestory.ServerMods`**), `Barrel/BarrelRecipe.cs` (351) + ingredient/output.
- `Systems/Cooking/RecipeRegistrySystem.cs` (304) — the `ApiAdditions` extension methods
  (`api.GetMetalAlloys()`, `api.GetSmithingRecipes()`, …) and all `RegisterXRecipe` entry points.
- `Systems/Liquid/` — `BlockLiquidContainerBase.cs` (1443), `BlockLiquidContainerTopOpened.cs` (307),
  `ILiquidInterfaces.cs` (136: `ILiquidInterface`/`ILiquidSource`/`ILiquidSink`),
  `WaterTightContainableProps.cs` (55), `ContainerTextureSource.cs`, `LiquidItemStackRenderer.cs`.
- `Systems/Barrel/` — `BEBarrel.cs` (467), `BlockBarrel.cs` (563), `GuiDialogBarrel.cs`.
- `Systems/Handbook/` — `CollectibleBehaviorHandbookTextAndExtraInfo.cs` (**2926 lines**, the whole
  page generator; `ICustomHandbookPageContent` at l.20; `AddHeading` l.2362, `AddSubHeading` l.2370),
  `SurvivalHandbook.cs` (243, `ModSystemSurvivalHandbook`, link protocols, `OnInitCustomPages` event),
  `CommandHandbook.cs`; `Handbook/Gui/` — `GuiDialogHandbook.cs` (589), `GuiDialogSurvivalHandbook.cs`
  (106, loads `config/handbook` assets + tabs), `GuiHandbookPage.cs` (62, the abstract page),
  `GuiHandbookItemStackPage.cs` (132, `PageCodeForStack`), `GuiHandbookTextPage.cs` (88),
  `GuiHandbookGroupedItemstackPage.cs`, `GuiHandbookMealRecipePage.cs` (563), `GuiElementFlatList.cs`;
  `Handbook/Tutorial/` — the tutorial pages.
- `Systems/Microblock/` (16 files + 8 chisel modes, ~9.4k lines) — `BEMicroBlock.cs` (2585, `partial`),
  `BEChisel.cs` (553), `BlockMicroBlock.cs` (888), `BlockChisel.cs` (239), `VoxelMaterial.cs`,
  `CuboidWithMaterial.cs`, `MicroBlockModelCache.cs`, `MicroblockTool.cs`, `MicroblockCommands.cs`,
  `ChiselMode/ChiselMode.cs` (55, abstract) + One/Two/Four/EightBy + Rotate/Flip/Rename modes.
- `Systems/RightClickConstruction.cs` (384) — `RightClickConstruction`, `ConstructionStage`,
  `ConstructionIngredient` (`StoreWildCard`). Driver behavior:
  `Systems/MechanicalPower/BlockEntityBehavior/BEBehaviorRightClickConstructable.cs` (114).
- `Systems/MechanicalPower/` — covered by another agent, **except** `BlockEntity/BEMultiblock.cs` (72),
  which is `BEMPMultiblock` (namespace `Vintagestory.GameContent.Mechanics`), an unrelated
  "I am a slave block, my controller is at `Principal`" BE.
- `Systems/SupportBeams/`, `Systems/FruitTree/`, `Systems/Farming/`, `Systems/Loot/`,
  `Systems/JonasDevices/`, `Systems/Prospecting/`, `Systems/TiledDungeon/`, `Systems/Trading/`.

## Where the multiblock machinery actually lives (NOT in vssurvivalmod)
- `vsessentialsmod/Block/BlockMultiblock.cs` (536) — `BlockMultiblock : Block, IMultiblockOffset`
  plus the five forwarding interfaces `IMultiBlockColSelBoxes`, `IMultiBlockActivate`,
  `IMultiBlockInteract`, `IMultiBlockBlockBreaking`, `IMultiBlockBlockProperties` (l.11–51).
- `vsessentialsmod/BlockBehavior/BehaviorMultiblock.cs` (183) — `BlockBehaviorMultiblock`,
  JSON code `"Multiblock"`, props `sizex/sizey/sizez/cposition/type/offsetHitboxes`.
- `vsapi/Common/IMultiblockOffset.cs` (13) — `GetControlBlockPos`.
- `vsapi/Common/MultiblockStructure.cs` (175) — the **declarative** structure checker
  (`BlockNumbers` + `Offsets`, `InitForUse(deg)`, `InCompleteBlockCount`, `WalkMatchingBlocks`,
  `HighlightIncompleteParts`). Exported via worldedit `/we mgencode`.

## Supporting vsapi files you will keep opening
- `vsapi/Common/Collectible/Collectible.cs` — `GetTemperature` (3260 & 3293), `SetTemperature` (3349),
  `HasTemperature` (3247), `GetMeltingDuration` (2722), `GetMeltingPoint` (2735), `CanSmelt` (2750),
  `DoSmelt` (2771), `OnSmeltAttempt` (3694), `GetHandBookStacks` (2474), `GetCollectibleInterface` (3622),
  `ExtraHandbookSection` (l.20), `CreativeInventoryTabs` (120) / `CreativeInventoryStacks` (130).
- `vsapi/Common/Collectible/CombustibleProperties.cs` (304) — `EnumSmeltType` (l.11), `CombustibleProperties`
  (l.89), `BakingProperties` (l.233).
- `vsapi/Common/Collectible/Block/Block.cs` — `Drops` (389), `OnBlockBroken` (1090),
  `SpawnDropsAndRemoveBlock` (1106), `GetDropsForHandbook` (1219), `GetHandbookDropsFromBreakDrops` (1252),
  `GetDrops` (1276), `OnPickBlock` (1319), `GetInterface<T>` (2706), `ParticleProperties` usage (1682–1730).
- `vsapi/Common/Collectible/Block/BlockEntity.cs` — `ToTreeAttributes` (302), `FromTreeAttributes` (332),
  `MarkDirty` (451), `GetBlockInfo` (468), `OnTesselation` (532), `OnPlacementBySchematic` (554),
  `CachedAccessPerms` (367).
- `vsapi/Common/Collectible/Block/BlockDropItemStack.cs` (253) — `Quantity` (NatFloat), `LastDrop`,
  `Tool`, `DropModbyStat`, `ToRandomItemstackForPlayer` (229).
- `vsapi/Common/Collectible/Block/ICoolingMedium.cs`, `IRotatable.cs` (also declares
  `IMaterialExchangeable` at l.29).
- `vsapi/Common/Particle/` — `SimpleParticleProperties.cs`, `AdvancedParticleProperties.cs`,
  `CollectibleParticleProperties.cs`, `ExplosionParticles.cs`.
- `vsapi/Common/Crafting/` — `RecipeBase.cs`, `RecipeBaseApi.cs` (`IRecipeBase`, `IRecipeIngredient`,
  `EnumRecipeMatchType`), `LayeredVoxelRecipe.cs`, `RecipeRegistry.cs` (`RecipeRegistryGeneric<T>`),
  `GridRecipe.cs`, `CraftingRecipeIngredient.cs`.
- `vsapi/Common/Text/VtmlParser.cs` (`VtmlUtil.TagConverters`, `RegisterVtmlTagConverter`, built-in
  `<br> <hk> <i> <a> <icon> <itemstack> …` at l.72+).
- `vsapi/Config/GlobalConstants.cs` — `CollectibleDefaultTemperature = 20` (179),
  `IgnoredStackAttributes = { "temperature","toolMode","renderVariant","transitionstate" }` (372).
- `vsapi/Common/Registry/RegistryObject.cs` — `Variant`/`VariantStrict`, `CodeWithVariant(s)`,
  `CodeWithPart(s)`, `CodeWithoutParts`.
- `vsapi/Util/WildcardUtil.cs` — `Match`, `WildCardReplace`.
- `vsapi/Client/UI/Dialog/GuiDialogBlockEntity.cs:196` — `EnumBlockEntityPacketId { Open=1000, Close=1001 }`.

## Key types

### `BlockMultiblock`

`vsessentialsmod/Block/BlockMultiblock.cs:59`

The generic 'ghost' block placed at every non-controller cell of a JSON-declared multiblock. Every call it receives is translated by `OffsetInv` and re-dispatched at the controller position.

`Offset`/`OffsetInv` are parsed in `OnLoaded` from `Variant["dx"|"dy"|"dz"]` with `"n"->"-"`, `"p"->""` (l.68). Private `Handle<T,K>` (l.79) resolves the controller via `block as K` then `block.GetInterface<K>(world, pos)`, and guards recursion with `if (block is BlockMultiblock)`. Overrides forwarded: Activate, GetSounds, Get{Selection,Collision,ParticleCollision}Boxes, DoPartialSelection, OnGettingBroken, OnBlockBroken, OnPickBlock, OnBlockInteract{Start,Step,Stop,Cancel}, GetPlacedBlockInteractionHelp, GetPlacedBlockInfo, GetRandomColor, GetColorWithoutTint, CanAttachBlockAt, SideIsSolid x2, GetAttributes, GetRetention, GetLiquidBarrierHeightOnSide, GetBlockEntity<T> x2, GetRotatedBlockCode, GetControlBlockPos. Opt-in interfaces the CONTROLLER implements to receive the offset: IMultiBlockColSelBoxes / IMultiBlockActivate / IMultiBlockInteract / IMultiBlockBlockBreaking / IMultiBlockBlockProperties (l.11-51).

### `BlockBehaviorMultiblock`

`vsessentialsmod/BlockBehavior/BehaviorMultiblock.cs:35`

JSON behavior `"Multiblock"` that claims a sizeX*sizeY*sizeZ box around a controller and fills it with `multiblock-<type>-<dx>-<dy>-<dz>` blocks.

Props `sizex`/`sizey`/`sizez` (default 3), `cposition` (default 1,0,1), `type` (default "monolithic"), `offsetHitboxes` (default false). `CanPlaceBlock` fails with `failureCode="notenoughspace"` (l.88). `OnBlockPlaced` builds the code and **throws IndexOutOfRangeException** if that block does not exist (l.135). `OnBlockRemoved` clears only cells that are `BlockMultiblock`. `IterateOverEach(controllerPos, ActionConsumable<BlockPos>)` is public and reuses one mutable `BlockPos`.

### `MultiblockStructure`

`vsapi/Common/MultiblockStructure.cs:29`

Declarative structure validator used by the beehive kiln and cementation furnace. Data-only: a `BlockNumbers` code->int map plus `Offsets` (Vec4i x,y,z,blockNumber). Loaded from `Block.Attributes["multiblockStructure"]`.

`InitForUse(float rotateYDeg)` — **DEGREES**, must be called before anything else or `WalkMatchingBlocks`/`InCompleteBlockCount` throw `InvalidOperationException`. `InCompleteBlockCount(world, centerPos, PositionMismatchDelegate)` returns 0 when complete; matching is `WildcardUtil.Match(code, block.Code)`. `WalkMatchingBlocks(world, centerPos, Action<Block,BlockPos>)`. `HighlightIncompleteParts` / `ClearHighlights` use `HighlightSlotId = 23`. Air blocks are not exported by `/we mgencode`, so empty cells are simply absent.

### `BlockEntityFirepit`

`vssurvivalmod/BlockEntity/Firepit/BEFirepit.cs:17`

The canonical vanilla heat machine: fuel burn-down, furnace temperature lerp, input heating, smelt trigger, block-state swap, GUI sync. Copy its shape for any furnace.

`OnBurnTick` every 100ms + `On500msTick` sync (l.120-121). Server-only simulation, `MarkDirty()` to sync (l.183-188). `changeTemperature(from,to,dt)` (l.286) is the vanilla lerp: `dt += dt*(diff/28)`. `heatInput` (l.333) averages new temp across StackSize and divides the rate by 11 once above melting point. `igniteWithFuel` sets `maxFuelBurnTime = fuelCopts.BurnDuration * BurnDurationModifier`, `maxTemperature = BurnTemperature * HeatModifier`. `setBlockState(state)` (l.521) = `Block.CodeWithVariant("burnstate", state)` + `ExchangeBlock`; states are lit/extinct/cold. Implements `IHeatSource` (10 / 0.25) and `ITemperatureSensitive.CoolNow`. Virtual config hooks: `BurnsAllFuell`, `HeatModifier`, `BurnDurationModifier`, `enviromentTemperature()`, `maxCookingTime()`.

### `BlockEntityBloomery`

`vssurvivalmod/BlockEntity/BEBloomery.cs:13`

The simplest 'batch smelter': 3-slot InventoryGeneric (fuel/ore/output), one 10-in-game-hour timer, one DoSmelt at the end. Nearest vanilla analogue to a small shaft furnace.

`MinTemp=1000`, `MaxTemp=1500` (l.27-28) gate what counts as ore via `CombustibleProperties.MeltingPoint`; fuel must have `BurnTemperature >= 1200 && BurnDuration > 30` (l.259). `Ore2FuelRatio` reads `OreStack.ItemAttributes["bloomeryFuelRatio"]` falling back to `SmeltedRatio` (l.420). `TryIgnite` requires the block above to contain `bloomerychimney` (l.315). `DoSmelt` (l.185) honours `mergeUnitsInBloomery` — output becomes ONE stack with a `units` float attribute * 100 — and always `SetTemperature(…, 900, true)`. Particle fields are `static SimpleParticleProperties` built in a static ctor.

### `BlockEntityForge`

`vssurvivalmod/BlockEntity/BEForge.cs:14`

`BlockEntityContainer, IHeatSource, ITemperatureSensitive, IBellowsAirReceiver` — the bellows/oxygen-boost model, fractional fuel consumption, and calendar-hour (not dt) accounting.

`BurnRate` = `0.5f / ItemAttributes["inForge"]["durationMul"]` coal per game hour (l.38). `MaxTemperature` = `700 + ItemAttributes["inForge"]["tempGainDeg"]` (l.47). `MaxExtraHeatRate = 1150/700f - 1` (l.54). `BlowAirInto(amount, facing)` raises `extraOxygenRate` (ignored while not burning). `OnCommonTick200ms` (l.197): `hoursPassed = Calendar.TotalHours - lastTickTotalHours`, `oxygenBurnMul = 1+extraOxygenRate`, `partialFuelConsumed += BurnRate*hoursPassed*oxygenBurnMul`, work-item `tempGain = hoursPassed*1500*oxygenBurnMul`, then `extraOxygenRate *= max(0, 1 - hoursPassed*30)` (all extra O2 gone after 0.1h). Auto-ignites when the work item exceeds 900 °C (l.238-242). `updateFuelLevel()` (l.493) folds `partialFuelConsumed` into StackSize with `%= 1`.

### `BlockEntityAnvil`

`vssurvivalmod/BlockEntity/BEAnvil.cs:26`

Voxel smithing. `byte[16,6,16] Voxels` with only 2 bits used per cell (`EnumVoxelMaterial` Empty/Metal/Slag), matched against a rotated `SmithingRecipe.Voxels`.

`serializeVoxels`/`deserializeVoxels` (l.1039/1060) pack 4 voxels per byte = 384 bytes. `recipeVoxels` (l.116) rotates the recipe pattern by `rotation/90` with `rotVoxels[z,y,x] = origVoxels[15-x,y,z]`. `OnSplit`, `OnUpset`, `OnHit`, `OnHelveHammerHit`, `CheckIfFinished`. Packets via `EnumAnvilPacket` and `SendUseOverPacket` (manual BinaryWriter of Vec3i). Work item conversion is delegated to the ITEM through `IAnvilWorkable` (`workItemStack.Collectible.GetCollectibleInterface<IAnvilWorkable>()`, l.154).

### `IAnvilWorkable`

`vssurvivalmod/Systems/IAnvilWorkable.cs:15`

The contract an ITEM implements to be placeable on an anvil. Implemented by ItemIngot, ItemWorkItem, ItemIronBloom, ItemMetalPlate.

`GetRequiredAnvilTier(stack)`, `GetMatchingRecipes(stack)`, `CanWork(stack)`, `TryPlaceOn(stack, beAnvil)` (**must also set `beAnvil.Voxels`** — see the doc comment l.24-26), `GetBaseMaterial(stack)`, `GetHelveWorkableMode`, `VoxelCountForHandbook(stack)`. `ItemIngot.CanWork` = `temperature >= meltingpoint/2` unless `Attributes["workableTemperature"]` overrides (ItemIngot.cs:55). `ItemIngot.VoxelCount = 42` (7x2x3, ItemIngot.cs:101).

### `CombustibleProperties / EnumSmeltType`

`vsapi/Common/Collectible/CombustibleProperties.cs:89`

The single JSON block that makes an item either a FUEL (BurnTemperature/BurnDuration/SmokeLevel/HeatResistance) or a SMELTABLE (MeltingPoint/MeltingDuration/SmeltedRatio/SmeltedStack/MaxTemperature/RequiresContainer/SmeltingType).

`SmeltedRatio` defaults 1, `RequiresContainer` defaults **true**, `SmokeLevel` 1, `HeatResistance` 500. `MaxTemperature = 0` means no cap. `EnumSmeltType` (l.11) = Smelt/Cook/Bake/Convert/Fire — only Bake (clay oven, needs `BakingProperties`) and Fire (kiln; `CanSmelt` refuses it in an open fire unless world config `allowOpenFireFiring`) have behaviour. Read at runtime via `collectible.GetCombustibleProperties(world, stack, pos)` (Collectible.cs:322) so it can be per-stack dynamic.

### `CollectibleObject.GetTemperature / SetTemperature`

`vsapi/Common/Collectible/Collectible.cs:3293`

The whole itemstack heat model. State lives in the stack attribute subtree `temperature` with keys `temperature` (float), `temperatureLastUpdate` (double, in Calendar.TotalHours) and optional `cooldownSpeed` (float). Cooling is lazy — evaluated on read, never ticked.

`GetTemperature(world, stack)` (3293): default cooldownSpeed 120 deg/hour, velocity multiplied by `Math.Max(1, temp/200)`, only recomputes when `hourDiff >= 1/150f`, honours `stack.Attributes.GetBool("timeFrozen")`, and walks `CollectibleBehavior.GetTemperature`/`AfterGetTemperature`. `GetTemperature(world, stack, didReceiveHeat)` (3260) is a DIFFERENT model: default 90, no velocity multiplier, threshold `1/85f`, no behavior walk, and it writes `temperatureLastUpdate` unconditionally. `SetTemperature(world, stack, temp, delayCooldown=true)` (3349): creates the subtree, and when heating up pushes `temperatureLastUpdate` **0.5 game hours into the future** (comment says 0.25). `HasTemperature` (3247).

### `ILiquidMetalSink`

`vssurvivalmod/BlockEntity/BEIngotMold.cs:14`

The pour-molten-metal-into-me contract; the crucible (BlockSmeltedContainer) is the only vanilla source, molds are the only vanilla sinks.

`bool CanReceiveAny { get; }`, `bool CanReceive(ItemStack)`, `void BeginFill(Vec3d hitPosition)`, `void ReceiveLiquidMetal(ItemStack key, ref int transferedAmount, float temp)`, `void OnPourOver()`. Driven from `BlockSmeltedContainer.OnHeldInteractStep` (BlockSmeltedContainer.cs:159) which transfers `Math.Min(2, contents.Value)` units per pour tick, decrements the held stack's `units` int attribute, and swaps the stack for `Attributes["emptiedBlockCode"]` at zero.

### `BlockEntityIngotMold`

`vssurvivalmod/BlockEntity/BEIngotMold.cs:23`

Two-sided mold; the reference implementation for fill-level + solidification + shattering + rotated tesselation.

`RequiredUnits = 100`. `IsHardened` = `T < 0.3*meltingpoint`, `IsLiquid` = `T > 0.8*meltingpoint`, `IsHot` = `T >= 200` (l.45-51). `ReceiveLiquidMetal` (l.480) clones the metal, sets temp with `delayCooldown:false` and stamps `cooldownSpeed = 300` on the FIRST pour only. `GetStateAwareContentsSided` (l.264) returns null for partial pours, returns `Attributes["shatteredStack"]` scaled `fillLevel/5` when shattered, and **removes `cooldownSpeed`** from the ingot you take out (l.285). `CoolNow` (l.821) computes a shatter chance `max(0, amountRel-0.6)*max(T-250,0)/5000`. Implements `IRotatable.OnTransformed` for `meshAngle`. Pre-1.21 save migration in `FromTreeAttributes` (l.722).

### `BlockSmeltingContainer`

`vssurvivalmod/Block/BlockSmeltingContainer.cs:11`

Crucible. Shows how to override the smelt contract for a container whose real input is the cooking slots, not the input slot.

`GetMeltingPoint` = MAX over ingredients; `GetMeltingDuration` = sum of `singleDuration * StackSize / SmeltedRatio`; `GetIngredientsTemperature` = **MIN** (lowest) temperature (l.215). `CanSmelt` returns false if ANY ingredient has `RequiresContainer == false`. `DoSmelt` (l.94) makes `CodeWithVariant("type","smelted")`, sets `units = round(quantity*100)` and clears every cooking slot. `GetMatchingAlloy` walks `api.GetMetalAlloys()`. Static `GetSingleSmeltableStack(stacks)` returns null if the stacks smelt into different outputs; static `GetMetal(stack)` localizes `material-<metal variant>` with an `ironbloom` special case.

### `BlockLiquidContainerBase`

`vssurvivalmod/Systems/Liquid/BlockLiquidContainerBase.cs:17`

`BlockContainer, ILiquidSource, ILiquidSink`. Litres are NOT stored: the content is an ItemStack whose StackSize is `litres * ItemsPerLitre`. Every method exists twice — one for the held ItemStack, one for a placed BlockPos backed by a BlockEntityContainer.

`CapacityLitres`/`DrinkPortionSize` read `Attributes["capacityLitres"]` or `Attributes["liquidContainerProps"]` in `OnLoaded` (l.121). `static GetContainableProps(stack)` (l.314) reads the **JSON key `waterTightContainerProps`** into a `WaterTightContainableProps`. `GetCurrentLitres`, `SetCurrentLitres`, `GetContent`/`SetContent` (stack + pos), `TryTakeContent`, `TryTakeLiquid`, `TryPutLiquid` (stack l.496 / pos l.561 — both average transition/spoil states before merging), `SplitStackAndPerformAction` (l.1060), `GetTransferStackSize` (l.294). `CreateItemStackFromJson` honours a `makefull` flag (l.409).

### `BlockEntityBarrel`

`vssurvivalmod/Systems/Barrel/BEBarrel.cs:13`

`BlockEntityLiquidContainer, ICoolingMedium` — two slots (0 = ItemSlotBarrelInput, 1 = ItemSlotLiquidOnly), recipe matching on every slot change, sealing for timed recipes.

`CapacityLitres` (default 50) pushed into `(inventory[1] as ItemSlotLiquidOnly).CapacityLitres` in Initialize (l.104-109). `FindMatchingRecipe(byPlayer)` — recipes with `SealHours == 0` craft **instantly on the server inside the slot-modified handler** (l.177), guarded by an `ignoreChange` reentrancy flag. `OnEvery3Second` runs sealed recipes with `TotalHours - SealedSinceTotalHours`. `Inventory_OnAcquireTransitionSpeed1` returns 0 while sealed (no spoiling). `OnBlockBroken` drops nothing when sealed (l.242). Packet 1337 = seal request, 1338 = server rejection. Delegates `ICoolingMedium` to whatever liquid is in slot 1.

### `BlockEntityContainer / BlockEntityOpenableContainer`

`vssurvivalmod/BlockEntity/BEContainer.cs:15`

Inventory-owning BE base. Serializes via an `InWorldContainer` helper under the tree key "inventory"; drops everything and writes an audit log line on break.

`container = new InWorldContainer(() => Inventory, "inventory")` in the ctor; `container.Init(api, ()=>Pos, ()=>MarkDirty(true))` in Initialize plus a 10s tick for perish/room. `OnBlockPlaced` copies `BlockContainer.GetContents(byItemStack)` into slots and **throws InvalidOperationException if the stack has more stacks than the inventory has slots** (l.56). `GetContentStacks(bool cloned=true)` / `GetNonEmptyContentStacks`. `BEOpenableContainer` (BEOpenableContainer.cs:81) adds `toggleInventoryDialogClient`, `OpenSound`/`CloseSound` from `Block.Attributes["openSound"]`, and the `packetid < 1000` -> `Inventory.InvNetworkUtil.HandleClientPacket` convention with `CachedAccessPerms` checks.

### `RecipeRegistrySystem + ApiAdditions`

`vssurvivalmod/Systems/Cooking/RecipeRegistrySystem.cs:123`

Owns every non-grid recipe list and the extension methods used to read them. ExecuteOrder 0.6.

`Start` calls `api.RegisterRecipeRegistry<RecipeRegistryGeneric<T>>(name)` for cookingrecipes/alloyrecipes/smithingrecipes/knappingrecipes/clayformingrecipes/barrelrecipes (l.168-174) — that registration happens on BOTH sides and the engine handles server->client sync. Only cooking recipes are loaded here (`AssetsLoaded`, server only). Register* methods throw `InvalidOperationException` once `canRegister == false`. `DisableRecipeRegisteringSystem` (l.113) flips that flag in `AssetsFinalize` with `ExecuteOrder() => 99999`. `ApiAdditions` (l.13) gives `api.GetCookingRecipes/GetBarrelRecipes/GetMetalAlloys/GetSmithingRecipes/GetKnappingRecipes/GetClayformingRecipes` and the matching `RegisterX` on ICoreServerAPI.

### `RecipeLoader`

`vssurvivalmod/Systems/Recipe/RecipeLoader.cs:12`

Server-only, ExecuteOrder 1, namespace **Vintagestory.ServerMods**. Loads alloy/grid/smithing/clayforming/knapping/barrel recipes from assets.

`LoadRecipes<TRecipe>(api, name, path, classExclusiveRecipes, registerDelegate)` (l.62) is public and generic — reusable for a mod's own `IRecipeBase`. Handles both a JObject and a JArray per file. Per-recipe pipeline (l.113): skip `!Enabled`, null `RequiresTrait` when class-exclusive recipes are off, default `Name` to the asset location, `OnParsed(world)`, `GenerateRecipesForAllIngredientCombinations(world)`, then `Resolve` each and register. Clears `RecipeBase.CollectiblePreSearchResultsCache` at the end.

### `AlloyRecipe`

`vssurvivalmod/Systems/Recipe/AlloyRecipe.cs:46`

`IByteSerializable` (not IRecipeBase). Ingredient list of `MetalAlloyIngredient` with MinRatio/MaxRatio and one output.

`Matches(ItemStack[], useSmeltedWhereApplicable=true)` — merges duplicate stacks, converts each to its `SmeltedStack` dividing StackSize by `SmeltedRatio`, then compares ratios as ints scaled by 10000 to dodge double comparison (l.87-93). `GetTotalOutputQuantity` returns the count in INGOTS; callers multiply by 100 to get units. `mergeAndCompareStacks` returns null if any input has no matching ingredient OR if any ingredient is left unconsumed (l.175).

### `CollectibleBehaviorHandbookTextAndExtraInfo`

`vssurvivalmod/Systems/Handbook/CollectibleBehaviorHandbookTextAndExtraInfo.cs:25`

Generates the entire per-stack handbook page as a `RichTextComponentBase[]`. 2926 lines of section builders.

`GetHandbookInfo(inSlot, capi, allStacks, openDetailPageFor)` (l.73) calls, in order: addGeneralInfo, addDropsInfo, addObtainedThroughInfo, addFoundInInfo, addAlloyForInfo, addAlloyedFromInfo, addProcessesIntoInfo, addProcessorForInfo, addIngredientForInfo, addCreatedByInfo, addExtraSections, addEatenByInfo, addStorableInfo, addStoredInInfo, then `collObj.GetCollectibleInterface<ICustomHandbookPageContent>()?.OnHandbookPageComposed(...)` (l.137). Static `AddHeading(components, capi, langKey, ref haveText)` (l.2362) and `AddSubHeading(components, capi, openDetailPageFor, langKey, detailPageCode)` (l.2370) — pass a non-null detailpage to get a clickable bullet. `ExtraHandBookSections` comes from `Attributes["handbook"]["extraSections"]`; `addExtraSections` (l.2387) ALSO auto-appends `<domain>:<block|item>-handbooktitle-<code>` / `-handbooktext-<code>` lang entries if they exist.

### `ICustomHandbookPageContent`

`vssurvivalmod/Systems/Handbook/CollectibleBehaviorHandbookTextAndExtraInfo.cs:20`

The one hook for adding your own handbook content to a stack's page.

`void OnHandbookPageComposed(List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)`. Resolved with `GetCollectibleInterface<T>()` so it may live on the Block/Item itself or on a CollectibleBehavior — but only the FIRST match is used, and it always runs LAST so your content lands at the bottom of the page. Vanilla example: `Systems/FruitTree/BlockFruitTreeBranch.cs:31`.

### `GuiHandbookPage / GuiHandbookItemStackPage / GuiHandbookTextPage`

`vssurvivalmod/Systems/Handbook/Gui/GuiHandbookPage.cs:29`

The abstract handbook list entry (`IFlatListItem`) and its two concrete kinds. Custom pages are added by subscribing to `ModSystemSurvivalHandbook.OnInitCustomPages`.

Abstract: `PageCode`, `CategoryCode`, `IsDuplicate`, `SearchWeightOffset`, `GetPageText()` -> `PageText{Title,Text}`, `RenderListEntryTo(...)`, `ComposePage(detailViewGui, textBounds, allstacks, openDetailPageFor)`, `Dispose()`. `GuiHandbookItemStackPage.PageCodeForStack(stack)` (GuiHandbookItemStackPage.cs:48) builds `"item-<code>"` / `"block-<code>"`, appending a sorted JSON of the stack attributes minus `GlobalConstants.IgnoredStackAttributes` and `durability`. `GuiHandbookTextPage` is deserialized straight out of `config/handbook/*.json` (fields `pageCode`, `Title`, `Text`, `categoryCode`) by `GuiDialogSurvivalHandbook.initCustomPages` (GuiDialogSurvivalHandbook.cs:42).

### `ModSystemSurvivalHandbook`

`vssurvivalmod/Systems/Handbook/SurvivalHandbook.cs:14`

Client-only handbook owner. Also the reason every collectible has handbook info even without JSON.

`event InitCustomPagesDelegate OnInitCustomPages` (l.19) — the extension point for mod pages. Registers link protocols `handbook` and `handbooksearch` (l.41-42); `handbook://tab-<code>` selects a tab, `handbook://<pagecode>` opens a page, `handbooksearch://<text>` runs a search. `SetupBehaviorAndGetItemStacks` (l.138) **injects a `CollectibleBehaviorHandbookTextAndExtraInfo` into every collectible that lacks one** at `LevelFinalize`. All stacks are cached in `ObjectCacheUtil` under key `"handbookallstacks"`. `IHandBookPageCodeProvider.HandbookPageCodeForStack` (GuiHandbookPage.cs:8) lets a collectible redirect its own page code.

### `BlockEntityMicroBlock`

`vssurvivalmod/Systems/Microblock/BEMicroBlock.cs:74`

`partial class BlockEntityMicroBlock : BlockEntity, IRotatable, IAcceptsDecor, IMaterialExchangeable` — the chiseled-block storage and mesher. `BlockEntityChisel` (Systems/Microblock/BEChisel.cs) adds the player interaction.

State: `List<uint> VoxelCuboids`, `int[] BlockIds` (aliased `MaterialIds`), `int[] DecorIds`, `int DecorRotations`, `string BlockName`, `short rotationY`, `emitSideAo`, `sidecenterSolid`/`sideAlmostSolid` (SmallBoolArray). `ToUint(minx,miny,minz,maxx,maxy,maxz,material)` (l.1229) packs a cuboid into 32 bits: `minx | miny<<4 | minz<<8 | (maxx-1)<<12 | (maxy-1)<<16 | (maxz-1)<<20 | material<<24`; `FromUint` (l.1247) unpacks. Tree keys: `materials` (IntArrayAttribute of BLOCK IDs), `cuboids` (IntArrayAttribute), `decorIds`, `decorRot`, `rotation` (`(rotationY+360)<<10`), `emitSideAo`/`sideSolid`/`sideAlmostSolid` (single-byte arrays), `blockName`, `originalCuboids`. `WasPlaced(block, blockName)` (l.200) seeds it from a source block. `SetVoxel`, `BeginEdit`/`EndEdit`, `RebuildCuboidList`, `RotateModel`, `FlipVoxels`, `GetMajorityMaterialId`, `RemoveMaterial`, `ConvertToVoxels`. `MaterialIdsFromAttributes(tree, world)` (l.1164) is public/static and handles the pre-1.13 StringArray format.

### `ChiselMode`

`vssurvivalmod/Systems/Microblock/ChiselMode/ChiselMode.cs:13`

Abstract chisel brush. Subclasses in the same folder: One/Two/Four/EightBy, Rotate, Flip, Rename.

`virtual int ChiselSize => 1`, `abstract DrawSkillIconDelegate DrawAction(ICoreClientAPI)`, `virtual bool Apply(BlockEntityChisel, IPlayer, Vec3i voxelPos, BlockFacing, bool isBreak, byte currentMaterialIndex)`. The default `Apply` removes at `voxelPos` on break, otherwise adds at `voxelPos + ChiselSize*facing.Normali` if still inside 0..15.

### `ITemperatureSensitive / OnStackToCool`

`vssurvivalmod/BlockEntityBehavior/BEBehaviorTemperatureSensitive.cs:14`

Lets rain, water submersion and the watering can cool a hot machine. The JSON behavior code is `"TemperatureSensitive"`.

`bool IsHot { get; }` + `void CoolNow(float amountRel, OnStackToCool onStackToCoolCallback)`. Static helper `ITemperatureSensitive.CoolStack(api, slot, pos, amountRel, targettemp, sizzle)` (l.26) routes through `CollectibleBehaviorQuenchable` when present, otherwise `SetTemperature(min(1100, T - amountRel*20), delayCooldown:false)`. The behavior (l.54) **throws InvalidOperationException in Initialize if the BE does not implement the interface** (l.70-73), ticks every ~1.9-2.1s, calls `CoolNow(25f)` when submerged in non-lava liquid and `CoolNow(rainLevel)` when rained on.

### `BlockDropItemStack`

`vsapi/Common/Collectible/Block/BlockDropItemStack.cs:37`

One entry of a block's `drops` JSON array.

`Type` (default Block), `Code`, `Quantity` (NatFloat, default 1), `Attributes` (JSON tree glued onto the resulting stack), `LastDrop` (stop iterating further drops), `Tool` (drop only if `byPlayer.InventoryManager.ActiveTool` matches), `DropModbyStat` (multiplies quantity by `entity.Stats.GetBlended(code)`), `ResolvedItemstack`. `ToRandomItemstackForPlayer` (l.229) applies the tool gate + stat multiplier and resolves `IResolvableCollectible`. `WeightedBlockDropItemstack` adds a `Weight`.

### `MicroBlock/anvil-adjacent enums`

`vssurvivalmod/BlockEntity/BEAnvil.cs:18`

`EnumVoxelMaterial { Empty=0, Metal=1, Slag=2, Placeholder1=3 }` — only 2 bits, which is why the anvil voxel array can be packed 4-per-byte.

Used by `ItemIngot.CreateVoxelsFromIngot(api, ref voxels, isBlisterSteel)` (ItemIngot.cs:105) which fills x 4..10, y 0..1, z 6..8 and randomly speckles Metal/Slag one layer up for blister steel; `AddVoxelsFromIngot(ref voxels)` (l.133) tops up at most 2 voxels per column and returns how many it added (0 => 'hammer down first').


---

<a id="vsessentials-creative"></a>

# vsessentialsmod + vscreativemod — render, animation, sound, schematics

# Map of `vsessentialsmod` + `vscreativemod` (paths relative to `.compat/vintagestory`)

Both are `ModSystem`-only assemblies loaded before survival. Neither ships blocks/items of its
own beyond a handful of primitives; they provide **the loaders, the renderers, the particle/AI/weather
engines, and world-edit**. A lot of what the assignment calls "the animation system" actually lives in
`vsapi/Common/Model/Animation/` — it is mapped here too because vsessentials only *consumes* it.

---

## A. `vsessentialsmod` — top level

| File | What it is |
|---|---|
| `Core.cs` (187 ln) | The mod's `ModSystem`. `StartClientSide` registers the 4 entity renderer classes (`Item`→`EntityItemRenderer`, `Dummy`→`EntityRendererInvisible`, `Shape`→`EntityShapeRenderer`, `PlayerShape`→`EntityPlayerShapeRenderer`) at :36-40 and compiles the shared `"instanced"` shader program (:56-66) that `ClothManager` later fetches by name. `Start` registers `BlockMultiblock`, the 3 block behaviors (`Decor`, `Multiblock`, `TransformBreak`), 3 block entities (`ParticleEmitter`, `Transient`, `Generic`) and ~30 entity behaviors (:95-179). `StartPre` hard-kills the process on a version mismatch (:30). |
| `InerhitableRotatableCube.cs` | Tiny helper cube mesh type. |
| `IntermodTools.cs` (68 ln) | `ModCompatiblityUtil`. Vendored from DArkHekRoMaNT/CompatibilityLib (MIT). `ExecuteOrder 0.04` (before json patching). Scans every mod origin for `compatibility/<modid>/…` and re-maps those assets onto the real asset path **only if that mod is loaded** (:33-66). This is the sanctioned "optional cross-mod asset override" mechanism. |
| `JsonExport.cs` | `/dev jsonexport` — dumps all items/blocks to json. Server-only, `controlserver` privilege. |

## A1. `Loading/` — the JSON registry-object pipeline (4321 ln)

This is where every block/item/entity JSON becomes a registered object. Critical for anyone doing
code-first defs or wildcard matching.

- `RegistryObjectType.cs` (386) — abstract base for `BlockType`/`ItemType`/`EntityType`.
  `Code`, `VariantGroups`, `Variant` (an **ordered** dict), `SkipVariants`, `AllowedVariants`, `Class`.
  `CreateBasetype` (:187) strips `variantgroups`/`skipVariants`/`allowedVariants`/`enabled` out of the
  JObject; `loadInherits` (:230) implements `inheritFrom` with `MergeArrayHandling.Replace`;
  `solveByType` (:306) resolves all `*byType` properties; `CreateResolvedType<T>` (:270) populates the
  concrete type.
- `RegistryObjectVariantGroup.cs` (75) — `Code`, `States`, `LoadFromProperties`,
  `LoadFromPropertiesCombine`, `Combine` (`Multiply`/`Add`/`SelectiveMultiply`), `OnVariant`.
- `RegistryObjectTypeLoader.cs` (807) — `ModRegistryObjectTypeLoader`. `ResolvedVariant.ResolveCode`
  (:33) builds the final code as `base + "-" + part` for **each variant part in declaration order**.
  `GatherVariants` (:499) → collect → `MultiplyProperties` → add. `LoadWorldProperties` (:181),
  `GatherAllTypes_Async` (:353), `PreloadTags` (:293), `FreeRam` (:795).
- `BlockType.cs` (917), `ItemType.cs` (195), `CollectibleType.cs` (368), `EntityType.cs` (503),
  `CollectibleBehaviorType.cs`, `CropBehaviorType.cs`, `BlockCropPropertiesType.cs`.
- `JsonPatchLoader.cs` (757) — the `patches/` engine (`add`/`remove`/`replace`/`addmerge`, `dependsOn`,
  `side`, `file`).
- `BasicBlocksLoader.cs` (64) — creates `air`/`multiblock-*` style primitives.
- `LoadColorMaps.cs`.

## A2. `Block/`, `BlockBehavior/`, `BlockEntity/`, `BlockEntityBehavior/`, `CollectibleBehavior/`

- `Block/BlockMultiblock.cs` (~700) — vanilla's multiblock filler block, `class BlockMultiblock : Block,
  IMultiblockOffset` (:59). `Offset` is parsed from the code's `dx/dy/dz` variants at `OnLoaded` (:64-71,
  `"n"`→`"-"`, `"p"`→`""`). Every `Block` override forwards to `pos + OffsetInv` through a generic
  `Handle<T,K>` dispatcher (:78-110) that first tries a `IMultiBlock*` interface on the controller,
  then guards against `BlockMultiblock → BlockMultiblock` recursion. The interfaces are declared at the
  top of the same file: `IMultiBlockColSelBoxes` (:11), `IMultiBlockActivate` (:17),
  `IMultiBlockInteract` (:22), `IMultiBlockBlockProperties` (:43), plus `MBOnBlockBroken` /
  `MBGetRandomColor` / `MBOnGettingBroken`.
- `BlockBehavior/BehaviorMultiblock.cs` (183) — `BlockBehaviorMultiblock`. JSON props
  `sizex/sizey/sizez` (default 3), `cposition` (default `1,0,1`), `type` (default `monolithic`),
  `offsetHitboxes`. `CanPlaceBlock` (:88) checks every cell is replaceable; `OnBlockPlaced` (:116)
  fills the shell with blocks named `multiblock-<type>-<sdx>-<sdy>-<sdz>` and **throws
  `IndexOutOfRangeException` if that block does not exist** (hard 5×5×5 cap); `OnBlockRemoved` (:163)
  clears them and `MarkBlockModified`s the controller.
- `BlockBehavior/BehaviorDecor.cs` (183) — the decor system; sets `block.decorBehaviorFlags`
  (`IsDecor`/`DrawIfCulled`/`AlternateZOffset`/`NotFullFace`/`Removable`/`CanAddToAnything`/
  `HasSidedVariants`) and `DecorThickness` (default 1/32) in `Initialize` (:56-79).
- `BlockBehavior/BehaviorBEInteract.cs` (187) — `BlockBehaviorBlockEntityInteract`. Declares
  `IInteractable` / `ILongInteractable` / `IInteractableWithHelp` (:13-27) and forwards block
  interaction to the BE or the **first BE behavior** implementing them.
- `BlockBehavior/BehaviorTransformBreak.cs`, `BehaviorRopeTieable.cs`.
- `BlockEntity/BEGeneric.cs` (25) — `BlockEntityGeneric : BlockEntity, IRotatable`; its
  `OnTransformed` just fans out to any `IRotatable` **behaviors** (:12-23). This is the class to use
  when all your rotation logic lives in behaviors.
- `BlockEntity/BETransient.cs` (243) — `TransientProperties` (`InGameHours`, `WhenBelow/AboveTemperature`,
  `ConvertTo/From`) + catch-up loop. `Initialize` (:43) contains the canonical "the BE's `Block` doesn't
  match the block at `Pos`" self-heal (:48-76). `CheckTransition` (:107) walks
  `lastCheckAtTotalDays` forward **one in-game hour at a time** — the away-catch-up idiom.
- `BlockEntityBehavior/BEBehaviorAnimatable.cs` (71) — `"Animatable"`. Creates a
  `BlockEntityAnimationUtil` in `Initialize`, disposes on unload/broken/removed, and — the key line —
  `OnTesselation` returns `true` (i.e. *suppress the static chunk mesh*) iff any animation is
  active (:51-54).
- `BlockEntityBehavior/BlockEntityAnimationUtil.cs` (111) — `BlockEntityAnimationUtil : AnimationUtil`.
  `InitializeAnimator(cacheKey, shape, texSource, rotationDeg)` (:21), the two-stage
  `CreateMesh(...)` (:44) which resolves references/joints and honours
  `QuantityElements`/`SelectiveElements`/`IgnoreElements` (with a `metaOverride`), and
  `OnAnimationsStateChange` (:96) which flips `renderer.ShouldRender` **inside a
  `MarkBlockDirty(pos, callback)`** so the static→animated swap lands on the same frame.
- `Legacy/BEParticleEmitter.cs` (48) — `BlockEntityParticleEmitter`, the minimal "block spits its
  `block.ParticleProperties` every 25 ms if the player is within 128 blocks" BE.
- `CollectibleBehavior/BehaviorAnimationAuthoritative.cs` (165) — reads `damageAtFrame` /
  `soundAtFrame` out of the anim meta's `Attributes` (divided by `AnimationSpeed`, :31-53) and
  registers them via `AnimManager.RegisterFrameCallback` (:98-110). **This is the vanilla pattern for
  "run game logic at animation frame N".**
- `CollectibleBehavior/`: `CollectibleBehaviorHeldBag.cs` (453), `Quenchable.cs` (409),
  `Buffable.cs` (369), `Throwable.cs` (138).

## A3. `Entities/`, `EntityRenderer/`

- `Entities/EntityBlockFalling.cs` (~600) — the falling-block entity. Notable for us: it **overrides
  `Entity.LightHsv`** (:396) with the value captured from the source block (:450) so a lit block keeps
  glowing while it falls; and it drives a positional looping sound with `LoadSound` (:466),
  `sound.SetPosition(...)` every tick (:532) and `sound.FadeOut(3f, s => s.Dispose())` on landing (:543).
  Registers `ModSystemRenderFallingBlocksFast` as an `EnumRenderStage.Opaque` renderer (:44).
- `Entities/EntityProjectile*.cs`, `EntityThrownItem.cs`.
- `EntityRenderer/EntityShapeRenderer.cs` (1060) — the big one. `ITexPositionSource` + `EntityRenderer`.
  `TesselateShape` (:178/:205) with `OverrideCompositeShape` / `OverrideEntityShape` /
  `OverrideSelectiveElements` hooks; `BeforeRender` (:335) re-tesselates when `!entity.ShapeFresh`,
  samples `GetLightRGBs` at the entity foot **and one block up if `SelectionBox.Y2 > 1`** (:639-645),
  and early-outs on `capi.IsGamePaused` (:359);
  `DoRender3DOpaqueBatched` (:620) uploads `entity.AnimManager.Animator.Matrices` into the
  `"Animation"` UBO (:663) and sets `rgbaLightIn`/`extraGlow`/`frostAlpha`/`glitchEffectStrength`;
  `RenderHeldItem` (:426) resolves the `"RightHand"`/`"LeftHand"` attachment-point pose;
  `loadModelMatrix` (:767); `DoRender2D` (:670) projects the nametag.
- `EntityRenderer/EntityPlayerShapeRenderer.cs` (576) — first-person hands, seraph.
- `EntityRenderer/EntityItemRenderer.cs` (337) — includes `ModSystemItemRendererOptimizer` (:13) which
  skips re-rendering identical stacked ground items.
- `EntityRenderer/ModSystemFpHands.cs` (64) — builds two extra shader programs
  (`standard`, `entityanimated`) with `#define ALLOWDEPTHOFFSET` and a hand-sized `"Animation"` UBO
  (:24-42). Good template for "recompile a stock shader with a prefix define".
- `EntityRenderer/EntityRendererInvisible.cs` (21).

## A4. `Entity/AI/` — the mob task system

- `IAiTask.cs` — the interface: `Id`, `Slot` (0-7), `Priority`, `PriorityForCancel`, `ProfilerName`,
  `ShouldExecute()`, `StartExecute()`, `CanContinueExecute()`, `ContinueExecute(dt)`,
  `FinishExecute(cancelled)`, `Notify(key,data)`, lifecycle hooks.
- `AiTaskManager.cs` (532) — contains `AiTaskRegistry` (static ctor registers ~35 codes, :33-74),
  `ApiTaskAdditions.RegisterAiTask<T>(code)` extension (:16), `AiRuntimeConfig` (:77, polls
  `runAiTasks`/`runAiActivities` world config every 250 ms), and `AiTaskManager` (:99).
  8 concurrent slots (`ActiveTasksSlotsNumber`, :111). `StartNewTasks` (:448) starts a task iff
  `task.Priority > activeTask.PriorityForCancel`; `ProcessRunningTasks` (:488) skips a task whose
  `CanContinueExecute()` is false (async pathfinding gate) and stops it when `ContinueExecute` returns
  false. Events: `OnTaskStarted/Stopped/ShouldExecuteTask/OnExecuteTask`.
- `BehaviorTaskAI.cs` (134) — `EntityBehaviorTaskAI`. Reads `aitasks` array from the entity JSON and
  `Activator.CreateInstance(taskType, entityAgent, taskConfig, attributes)` (:91). `OnGameTick` (:112)
  bails unless `entity.State == Active && entity.Alive`, ticks `WaypointsTraverser` then the manager,
  wrapped in `FrameProfiler` marks.
- `AiTaskBase.cs` / `AiTaskBaseTargetable.cs` — legacy base.
- `Tasks/` (16 files) — the pre-1.21 tasks (`wander`, `seekentity`, `meleeattack`, …).
- `TasksRefactored/` (19 files) — the `-r` suffixed 1.21 rewrite. `TasksRefactored/AiTaskBase.cs`
  is worth reading as a **config-object pattern**: `AiTaskBaseConfig` is a `[JsonObject(OptIn)]` DTO
  with ~40 documented `[JsonProperty]` fields (cooldowns in ms *and* in in-game hours, emotion-state
  gates, `TagsAppliedToEntity`, `animation`+`animationSpeed`, `Sound`/`SoundStartMs`/`SoundRepeatMs`/
  `FinishSound`). `StartExecute` (:475-491) starts the anim and schedules the sound;
  `FinishExecute` (:527-533) stops the anim and plays `FinishSound`; `ContinueExecute` (:744) handles
  `SoundRepeatMs`.
- `Tasks/Guard/` — 5 guard/jealousy tasks.
- `EnumCreatureHostility.cs`.

## A5. `Entity/Pathfinding/`

`PathfindingAsync.cs` (`ModSystem, IAsyncServerSystem`, own thread + `ConcurrentQueue<PathfinderTask>`),
`Astar/AStar.cs`, `PathNode.cs`, `PathNodeSet.cs`, `Pathfind.cs`, `WaypointsTraverser.cs`,
`PathTraverserBase.cs`, `StraightLineTraverser.cs`, `PathFindDebug.cs`.

## A6. `Entity/Behavior/` (32 files)

Highlights: `BehaviorInterpolatePosition.cs` — **the reference implementation of client-side
smoothing against a discrete server tick stream**. `PositionSnapshot` struct, a `Queue<PositionSnapshot>`,
`const float interval = 1/15f` (:110), `PushQueue`/`PopQueue`, and an `EnumRenderStage.Before` renderer
(:64) that accumulates `dtAccum += dt * targetSpeed` and pops while `dtAccum > pN.interval` (:243-250).
`OnReceivedServerPos` (:171) scales the interval by the `"tickDiff"` attribute and flushes on teleport.
Others: `BehaviorControlledPhysics`, `BehaviorPlayerPhysics` (both `EnumRenderStage.Before` renderers),
`BehaviorSelectionBoxes.cs` (per-attachment-point selection boxes + a wireframe debug renderer on
`AfterFinalComposition`, :125), `BehaviorNameTag` (`Ortho` renderer), `BehaviorGait` (positional
looping `gaitSound`, :183/:281), `BehaviorHealth`, `BehaviorHarvestable`, `BehaviorEmotionStates`,
`BehaviorRepulseAgents` (the main `EntityPartitioning` consumer), `BehaviorContainer`,
`BehaviorEntityStateTags` (`SetupTagIds` is called from `Core.AssetsFinalize`).

## A7. `Systems/` — the mod systems

| Path | Contents |
|---|---|
| `Systems/EntityPartitioning.cs` (379) | `EntityPartitioning : ModSystem, IEntityPartitioning`. `partitionsLength = 4` → a 8×8-block grid inside each 32³ chunk (`gridSizeInBlocks = 32/4`, :63-64). `Partitions` is keyed by `chunkIndex3d` **including the dimension's Y** (:73). Rebuilt from scratch every 32 ms on both sides (:109/:116/:137). Creatures and non-creatures go into two separate arrays (`EntityPartitionChunk.Add`, :26). `WalkEntities(x,y,z,radius,cb,rangeTest,searchType)` (:306) is the fast spatial query; `WalkEntityPartitions` skips the radius test; `GetNearestEntity` / `GetNearestInteractableEntity` (:188/:200). `LargestTouchDistance` is published for collision code. |
| `Systems/POIRegistry.cs` (309) | Server-side, **not synced**. `IPointOfInterest{Position,Type}`, `IAnimalFoodSource`, `IAnimalNest`, `CreatureDiet` (food categories + weighted food tags + blacklist, :33-105). POIs bucketed per chunk *column* (`Dictionary<Vec2i, List<…>>`). `AddPOI`/`RemovePOI`/`WalkPois`/`GetNearestPoi`/`GetWeightedNearestPoi` (:283/:297/:165/:194/:232). |
| `Systems/RoomRegistry.cs` (~600) | `Room` (ExitCount, IsSmallRoom, Skylight/NonSkylight, Cooling/NonCoolingWall counts, `Cuboidi Location`, `byte[] PosInRoom`) + `ChunkRooms`. `GetRoomForPosition` (:346) runs a BFS flood fill bounded by `ARRAYSIZE=29`, `MAXROOMSIZE=14`, `MAXCELLARSIZE=7`, `ALTMAXCELLARSIZE=9`, `ALTMAXCELLARVOLUME=150` (:403-409). This is what "is this a sealed room / cellar" means in vanilla. |
| `Systems/ProgressBarRenderer.cs` (130) | `ModSystemProgressBar.AddProgressbar()` → `IProgressBar` rendered on `EnumRenderStage.Ortho`, stacked 30 px apart. Cheap generic debug/progress HUD. |
| `Systems/EntityNameTagRenderer.cs` (100) | Nametag texture generation. |
| `Systems/LeafBlockDecay.cs`, `Systems/CharacterExtraDialogs.cs`, `Systems/Emotes.cs` (`/emote`, reads `entity.Properties.Attributes["emotes"]`), `Systems/ErrorReporter.cs` (client log viewer over network), `Systems/ModSystemTfEditFromAttributes.cs` (event-bus `onsettransform`/`ongettransform` so a *stack attribute* can pick the held/gui transform). |
| `Systems/Cloth/` | `ClothManager.cs` (verlet rope sim, an `Opaque` renderer, instanced rendering via the `"instanced"` shader, a global looping `stretchSound` at :463), `ClothSystem.cs`, `ClothPoint.cs`, `ClothConstraint.cs`. |
| `Systems/Gui/` (`../Gui/`) | `GuiDialogCreatureContents.cs`, `GuiDialogLogViewer.cs`. |
| `Inventory/` | `InWorldContainer.cs` (the reusable "a BE/entity owns an inventory at a world position" helper), `ItemSlotMouth.cs`. |

### `Systems/ParticleEntity/` — the *entity*-particle engine (bugs, fish, gnats)

Distinct from the normal `SimpleParticleProperties`/`AdvancedParticleProperties` engine (which lives in
the closed engine + `vsapi`).

- `EntityParticleSystem.cs` (471) — `ModSystem, IRenderer`, client only. Owns a **dedicated
  simulation thread** (`TyronThreadPool.CreateDedicatedThread`, :126) that sleeps 10 ms and calls
  `OnNewFrameOffThread` (:384). Triple-ish buffering: 5 `MeshData` update buffers + 5 velocity arrays,
  `writePosition`/`readPosition`/`advanceCount` under a lock (:66-70, :329-343, :389-395) — the render
  thread interpolates between physics ticks with `step = dt / ticktime` (:351). Instanced cube mesh,
  pool of 10 000, custom float interleave `(3 pos, 3 size, 4 dir)` + custom byte `(4 lightrgb, 4 rgba)`
  (:136-158). Own shader `particlescube` compiled with `#define VEC3SCALE` (:183-195).
  `SpawnParticle`/`KillParticle` maintain an intrusive doubly-linked list and **throw if called off
  the sim thread** (:244, :268).
- `EntityParticle.cs` (107) — abstract `EntityParticle : ParticleBase`; `TickNow` does collision +
  gravity + buoyancy; re-samples `GetLightRGBsAsInt` only every 3rd tick (`doSlowTick`, :64).
- `EntityParticleSpawner.cs` — hooks `sys.OnSimTick` and spawns per-biome swarms.
- `AmbientParticles.cs` — `ModSystemAmbientParticles`; uses
  `capi.Event.RegisterAsyncParticleSpawner(AsyncParticleSpawnTick)` (:29) — the **off-thread** particle
  spawn API (`IAsyncParticleManager.Spawn`, `manager.BlockAccess`). Exposes an
  `event ActionBoolReturn ShouldSpawnAmbientParticles` (:24) other mods can veto with.
- `Types/`: `EntityParticleCicada`, `Coqui`, `Fish`, `Grasshopper`, `MatingGnats`, `WaterStrider`.

### `Systems/Weather/` (7609 ln)

- `WeatherSystemBase.cs` (347) — `weatherSimByMapRegion`, `GetPrecipitation*`, `GetEnvironmentWetness`
  (:102), `GetClimateFast` (:191), `getWeatherDataReader()` (:208),
  `getOrCreateWeatherSimForRegion` (:219/:236), `SpawnLightningFlash` (:303),
  `OverridePrecipitation`, `CloudLevelRel`, `CloudTileSize`, and the
  `OnLightningImpactBegin/End` events (:42-43).
- `WeatherSystemServer.cs` (277) / `WeatherSystemClient.cs` (365). The client registers **two**
  renderer stages, `Before` and `Done` (:91-92). `OnRenderFrame` (:132) reloads/lerps the region sims
  at the player, then smooths wind into `GlobalConstants.CurrentWindSpeedClient` with a small
  sinusoidal jitter (:157-166) and repeats for surface wind at the rain-map height.
- `WeatherDataReader.cs` (374) — `WeatherDataReaderBase` (`AdjacentSims[4]`, `LerpLeftRight/TopBot`),
  `WeatherDataReader` (position-taking accessors: `GetWindSpeed(Vec3d)`, `GetPrecType`,
  `GetBlendedCloud*`), `WeatherDataReaderPreLoad` (pre-loaded, cheaper in a loop).
- `WeatherDataSnapshot.cs` (108) — the blended per-position weather: `Ambient` (`AmbientModifier`),
  `PrecIntensity`, `BlendedPrecType`, `climateCond`, `curWindSpeed`, `snowThresholdTemp` (4 °C default),
  lightning rates. `SetLerped`/`BiLerp`.
- `WeatherSimulationRegion.cs` (744), `WeatherState.cs`, `WeatherSystemConfig.cs`,
  `Model/WeatherPatternConfig.cs`, `Model/WindPatternConfig.cs`, `Model/WeatherEventConfig.cs`,
  `Model/ConditionalPatternConfig.cs`, `Model/Impl/{WeatherPattern,WindPattern,WeatherEvent}.cs`.
- `WeatherSimulationSound.cs` (378) — **the best looping-sound reference in either repo.** Six
  `ILoadedSound`s created once in `Initialize` (:58-134) with `ShouldLoop=true`,
  `DisposeOnFinish=false`, `RelativePosition=true`, `SoundType=EnumSoundType.Weather`. `Update` (:135)
  gates on a 0.25 s accumulator; `updateSounds` waits for every sound's `IsReady` before touching any
  of them (:170-186), computes target volumes/pitches, `Start()`s only on the rising edge with a
  `bool …SoundsOn` latch, `SetVolume`/`SetPitch` each tick, and `Stop()`s below 0.01 (:306-317).
  A background `TyronThreadPool.QueueTask` computes `roomVolumePitchLoss` from
  `GetDistanceToRainFall` (:194-203). `Dispose` disposes all of them (:354).
- `WeatherSimulationParticles.cs` (682) — rain/snow/hail; uses `RegisterAsyncParticleSpawner` and a
  `DummyRenderer` on `EnumRenderStage.Before` for the desert storm (:250).
- `WeatherSimulationLightning.cs` (300) + `LightningFlash.cs` (297) — **the dynamic-light reference**:
  `LightiningPointLight : IPointLight` (:283-294) added/removed via
  `capi.Render.AddPointLight/RemovePointLight`; distance-banded one-shot thunder sounds (:66-87).
- `WeatherSimulationSnowAccum.cs` (672), `UpdateSnowLayerChunk.cs`.
- `CloudRenderer.cs` (623) + `CloudTile.cs` + `CloudMeshUtil.cs` — legacy tiled clouds, `OIT` stage.
- `Newclouds/`: `CloudRendererModSystem.cs` (picks map vs volumetric), `CloudRendererMap.cs` (601),
  `CloudRendererVolumetric.cs`.
- `AuroraRenderer.cs` (186) — a compact "register a file shader program + one quad mesh + OIT
  renderer" template (`RegisterFileShaderProgram("aurora", prog)`, :44).
- `WeatherCommands.cs` (868) — `/weather …`.

### `Systems/WorldGen/Standard/` — the whole vanilla terrain generator

Ordered by pass, mirrored in the folder names:
`0.GenMaps/` (GenMaps + `MapLayer/*` 19 layers + `Noise*`), `1.GenTerra/`, `2.GenRockStrata/` (+`GeoProv/`),
`3.GenCaves/`, `4.GenBlockLayers/`, `5.GenDeposits/` (+`Generators/` 7 disc generators),
`6.GenStructures/` (`WorldGenStructure.cs`, `WorldGenVillage.cs`, `Story/StoryStructureLocation.cs`,
`HookGeneratedStructure.cs`), `7.GenPonds/`, `8.GenVegetationAndPatches/` (+`Tree/`, `Treegen/`),
`9.GenRivulets.cs`, `91.GenLight.cs`, `92.GenSnowLayer.cs`, `93.GenCreatures.cs`.
Support: `ModStdWorldGen.cs`, `GenPartial.cs`, `TerraGenConfig.cs`, `GlobalConfig.cs`,
`WgenCommands.cs`, `WorldGenAreaProvider.cs`, `IBlockLog/IBlockSoil/IBlockForestFloor/IWorldGenArea/
IStoryStructureSystem.cs`, `ForestFloorHelper.cs`.

**`91.GenLight.cs`** (52 ln) is the entire vanilla lighting hook: on `EnumWorldGenPass.Vegetation` it
calls `blockAccessor.BeginColumn()` → `api.WorldManager.SunFloodChunkColumnForWorldGen(...)` →
`blockAccessor.RunScheduledBlockLightUpdates(cx, cz)`, and on `NeighbourSunLightFlood` it calls
`SunFloodChunkColumnNeighboursForWorldGen`. Block-light *propagation itself* is engine-internal; the
only public levers are `Block.LightHsv` / `Block.LightAbsorption` /
`Block.GetLightAbsorption(...)` (`vsapi/Common/Collectible/Block/Block.cs:116, :719, :724`),
`IBlockAccessor.GetLightLevel/GetLightRGBs/GetLightRGBsAsInt/RemoveBlockLight`
(`vsapi/Common/API/IBlockAccessor.cs:617-654, :764`), `IWorldGenBlockAccessor.ScheduleBlockLightUpdate`,
and `sapi.WorldManager.FullRelight(start,end)`.

### `Systems/WorldGen/Standard/Datastructures/` — schematics for worldgen

- `BlockSchematicStructure.cs` (540) — `: BlockSchematic`. Adds `blocksByPos[x,y,z]` +
  `FluidBlocksByPos` unpacked caches built in `Init` (:41), `OffsetY`, `MaxYDiff=3`,
  `MaxBelowSealevel=20`, `SatisfiesMinSpawnDistance` (:34).
  `PlaceRespectingBlockLayers(...)` (:110) is the big one — swaps soil/rock for the local block layer,
  optional `suppressSoilIfAirBelow` / `displaceWater`. `PlaceReplacingBlocks` (:353) applies a
  rock-type remap table. `Unpack(api)` / `Unpack(api, orientation)` (:522/:531) pre-rotate the four
  orientations.
- `BlockSchematicPartial.cs` (339) — `: BlockSchematicStructure` + `EnumStructurePlacement` (:16).
  `PlacePartial(chunks, worldgenBlockAccessor, …)` (:31) places only the part of a structure that
  falls inside the chunk column currently being generated. **The model to copy if you ever place a
  multiblock during worldgen.**
- `LerpedWeightedIndex2DMap.cs`, `WeightedIndex.cs`.

### `Systems/WorldMap/`

`WorldMapManager.cs`, `MapLayer.cs`, `MapComponent.cs`, `GuiDialogWorldMap.cs`, `GuiElementMap.cs`,
`IMapDataSink.cs`; `ChunkLayer/` (`ChunkMapLayer`, `MapDB`, `MultiChunkMapComponent`, `BlurTool`),
`EntityLayer/` (`EntityMapLayer`, `PlayerMapLayer`, `SystemRemotePlayerTracking`),
`WaypointLayer/` (`WaypointMapLayer`, `WaypointMapComponent`, add/edit dialogs).

---

## B. `vscreativemod` (~4 800 ln of C#, 31 files)

| File | What it is |
|---|---|
| `Core.cs` (77) | Adds `assets/creative` as a mod origin in `StartPre` (:33). Registers `ItemMagicWand`, `ItemPocketSun`, `EntityTestShip`. Forces creative game mode + `PickingRange = 100` for new players in a creative world (:56-67); disables entity spawning for the `creativebuilding` playstyle (:69-75). |
| `WorldEdit.cs` (614) | `partial class WorldEdit : ModSystem`. `workspaces` per player UID; `GetOrCreateWorkSpace` (:409) builds an `IBlockAccessorRevertable` per player and hooks `BeforeCommit` → `constrainEditsToSelection` (:314). `CanUseWorldEdit` (:388) = creative mode + `worldedit` privilege. **`GenMarkedMultiblockCode` (:427)** walks the marked cuboid and emits a ready-to-paste `multiblockStructure: { blockNumbers: {…}, offsets: [{x,y,z,w},…] }` JSON block to `server-main.log`, centered on the block you are looking at. `RebuildRainMap` (:467), `HandleHistoryChange` (:533, undo/redo), `OnDidBuildBlock`/`OnBreakBlock` tool hooks (:582/:593), `Good`/`Bad` chat helpers (:604/:609). |
| `WorldEditCommands.cs` (1312) | `/we` built with the fluent `sapi.ChatCommands.GetOrCreate("we").BeginSub(...).WithAlias(...).WithArgs(parsers.…).HandleWith(...).EndSub()` builder (:24-…). Notable subs: `import-rotation`/`impr`, `import-flip`, `constrain`, `copy`, `paste`, `undo`/`redo`, `relight` (:80), `relight-selection` → `sapi.WorldManager.FullRelight` (:997), **`generate-multiblock-code` / `gmc` / legacy `mgencode` (:155)**, `tool`, `op` (overload protection). Legacy aliases are re-registered behind the `legacywecommands` world config (:332-340). |
| `Workspace.cs` (954) | `WorldEditWorkspace` — the per-player state. Markers (`StartMarker`/`EndMarker` + `…Exact`), `ImportAngle`, `ImportFlipped`, `StepSize`, `ToolAxisLock`, `DoRelight`, `DimensionId`, `PreviewBlockData`/`PreviewPos`, and generic `FloatValues/IntValues/StringValues/ByteDataValues` dictionaries that tools use as their persisted settings (:105-108). `Init` (:131) creates the per-player **mini dimension** used for previews. `ResendBlockHighlights` (:383) and `HighlightSelectedArea` (:434) — the canonical use of `world.HighlightBlocks(player, slot, positions, colors, mode, shape)`. `CopyArea` (:771), **`PasteBlockData` (:781)**, `ImportArea` (:816), `ExportArea` (:839), `CreatePreview` (:871), **`CreateDimensionFromSchematic` (:883)**, `DestroyPreview` (:913), `MoveArea` (:656), `MayPlace` (:617), `GrowSelection` (:487), `GetFacing` (:519). `ToBytes`/`FromBytes` (:213/:283) sync it to the client. |
| `WorldEditSelectionTools.cs` (446) | The other half of `WorldEditWorkspace`: `ModifyMarker` (:13), `HandleRotateCommand` (:26), `HandleFlipCommand` (:56), `HandleRepeatCommand`/`RepeatArea` (:87/:103), `HandleMirrorCommand`/`MirrorArea` (:243/:260), `HandleMoveCommand` (:417), `HandleShiftCommand` (:433). |
| `WorldEditClientHandler.cs` (989) | Client half. Four `GuiJsonDialog`s (toolbar / controls / tool options / settings) driven entirely by `OnGetValue*`/`OnSetValue*` string callbacks. `OnReceivedPreviewBlocks` (:351) points the client at the preview mini-dimension. File drag-and-drop import (`Event_FileDrop`, :279) and a `GuiDialogConfirmAcceptFile` (:18) before accepting a schematic pushed by the server (:179/:244). |
| `HudWorldEditInputCapture.cs` (102) | Steals the `toolmodeselect` hotkey while a scroll-enabled tool is active and turns **ctrl+mousewheel** into `/we shift`, `/we move`, `/we g`, `/we imr 90|270` depending on `ToolInstance.ScrollMode`. |
| `WorldEditScrollToolMode.cs` (91), `EnumWeToolMode.cs` (`Move`/`MoveNear`/`MoveFar`/`Rotate`), `Enums.cs` (`EnumToolOffsetMode.Center/Attach`) | Scroll-mode radial UI. |
| `ToolRegistry.cs` (51) | `ToolTypes` ordered dict, `RegisterToolType(name, type)`, `InstanceFromType` via `Activator.CreateInstance(type, workspace, blockAccessor)`. `RegisterDefaultTools` (:35) registers the 12 stock tools. |
| `Tool/ToolBase.cs` (150) | Abstract tool: `Size`, `ScrollEnabled`, `ScrollMode`, `OnSelected/Deselected`, `OnInteractStart/OnAttackStart`, `OnBuild`/`OnBreak` (which apply the `EnumToolOffsetMode.Attach` half-size offset along `blockSel.Face.Normali`, :58-63), `OnWorldEditCommand`, and the highlight trio `GetBlockHighlights()` / `GetBlockHighlightColors()` / `GetBlockHighlightShape()` funnelled through `HighlightBlocks` → `sapi.World.HighlightBlocks(player, (int)EnumHighlightSlot.Brush, …)` (:139-142). |
| `Tool/*.cs` | `SelectTool` (377), `MoveTool` (280), `RepeatTool` (176), `PaintBrushTool` (707), `AirBrushTool` (284), `LineTool` (107), `EraserTool` (39), `FloodFillTool` (252), `RaiseLowerTool` (229), `GrowShrinkTool` (171), `ErodeTool` (247), **`ImportTool` (414)**. |
| `Tool/ImportTool.cs` | All settings stored in `workspace.IntValues/StringValues` (`std.pasteToolOrigin`, `std.importReplaceMode`, `std.pasteToolRandomRotate`, `std.previewAtPlayer`, …). Commands `imr` (rotate, only 0/90/180/270, :226), `imflip` (Y axis, :258), `immirror` (X or Z, :274), `imo` (origin), `tm` (replace mode). `Commit` (:311) is **the canonical bulk-place order**: `Init(ba)` → `Place(...)` → `PlaceDecors(...)` → `ba.Commit()` → `PlaceEntitiesAndBlockEntities(...)` → `ba.CommitBlockEntityData()`. |
| `ShapeToPositionList.cs` (63) | `Cuboid(start,end)` and `Ball(center,radius)` → `List<BlockPos>`. Ball uses `radInt = ceil(r/2)` and `radSq = r²/4`, i.e. **`radius` is a diameter**. |
| `Item/ItemMagicWand.cs` | `ItemPocketSun` (:9, always 6000 °C) and `ItemMagicWand` (:22) which just forwards attack/interact to `WorldEdit.OnAttackStart/OnInteractStart`. |
| `Entities/EntityTestShip.cs` | Moving mini-dimension demo. |
| `WorldGen/` | `CreativeWorldGenConfig.cs`, `GenBlockLayersFlat.cs` (superflat), `GenLight.cs` (identical to the survival one but registered for the `"superflat"` worldgen). |

---

## C. Where the animation / highlight primitives actually live (`vsapi`)

These are consumed by both repos and are the substance of the "animation" and "highlighting" parts of
this assignment.

`vsapi/Common/Model/Animation/`
- `Animation.cs` — the compiled animation. `QuantityFrames`, `KeyFrames`, `EaseAnimationSpeed`,
  and the two behaviour enums at the top: `EnumEntityActivityStoppedHandling{PlayTillEnd,Rewind,Stop,EaseOut}`
  (:12) and **`EnumEntityAnimationEndHandling{Repeat=0,Hold,Stop,EaseOut}`** (:20). Defaults:
  `OnActivityStopped = Rewind`, `OnAnimationEnd = Repeat` (:68-71). `GenerateAllFrames` (:98) bakes
  `PrevNextKeyFrameByFrame`; throws if a keyframe number ≥ `QuantityFrames` (:146) or if the joint
  count exceeds `GlobalConstants.MaxAnimatedElements` (:117-121).
- `AnimationMetaData.cs` — the JSON-facing play descriptor: `Code`, `Animation`, `Weight`,
  `ElementWeight`, `AnimationSpeed`, `MulWithWalkSpeed`, `EaseInSpeed`/`EaseOutSpeed` (both 10),
  `BlendMode` + `ElementBlendMode`, `WeightCapFactor`, `TriggeredBy`, `SupressDefaultAnimation`,
  `ClientSide`, `AnimationSounds[]`, `AdjustCollisionBox`. `Init()` (:260) computes `CodeCrc32` and
  builds the `-fp` variant. `GetCurrentAnimationSpeed` multiplies by
  `GlobalConstants.OverallSpeedMultiplier` (:255). Network (de)serialisation is version-gated (:442).
- `AnimationSound.cs` — `Frame`, `Chance`, **`Looping`**, and a `SoundAttributes` filled from the json
  aliases `location`/`path`/`pitch`/`volume`/`range`/`randomizePitch` (:36-77).
- `RunningAnimation.cs` — per-animation runtime state (`CurrentFrame`, `Iterations`, `EasingFactor`,
  `BlendedWeight`, `Active`, `Running`, `NowPlaying`, `SoundPlayedAtIteration`). **`Progress(dt, walkspeed)`
  (:103) is the frame clock: `newFrame = CurrentFrame + 30 * dt * speed`** — animations are authored at
  a nominal 30 fps (:120). Ease-in/out at :111-118, the `Hold`/`EaseOut` terminal clamp at :131-145,
  iteration counting at :157-166.
- `AnimatorBase.cs` — `OnFrame(activeAnimationsByAnimCode, dt)` (:110) is the whole state machine:
  start newly-added codes (:121-129), log+drop codes the shape doesn't have (:130-141), detect
  removed codes and apply `OnActivityStopped` (:148-169), then `ProgressRunningAnimation` (:197).
  When an animation with `OnAnimationEnd=Stop|EaseOut` finishes, the animator **removes the key from
  the caller's dictionary itself** (:174-178).
- `ClientAnimator.cs` — matrix math, `MaxConcurrentAnimations = 16` (:32),
  `CreateForEntity` (:41/:69), `calculateMatrices` (:324 and the recursive :395),
  and the **animation-sound trigger** at :305-318.
- `ServerAnimator.cs`, `NoAnimationManager.cs`, `IAnimator.cs`, `AnimationFrame/Joint/KeyFrame*.cs`,
  `AnimationCache.cs`.
- `AnimationManager.cs` — the **entity** side. `ActiveAnimationsByAnimCode` is
  `StringComparer.OrdinalIgnoreCase`. `OnClientFrame` (:467) only advances the animator when
  `entity.IsRendered || entity.IsShadowRendered || !entity.Alive` (:476) and stops looping anim-sounds
  whose animation went inactive (:483-495). `RegisterFrameCallback`/`runTriggers` (:498/:504).
  `static ShouldPlaySound(world, pos, dim, loopingSounds, code, sound, entity)` (:552).
- `AnimationUtil.cs` — the **block-entity / arbitrary-position** side. `IRenderer` registered on
  `Opaque` as `"beanimutil"` (:35). `InitializeAnimator` (:59) → `GetAnimator` (:130, caches
  `AnimCacheEntry` in `api.ObjectCache["animUtil-animCache"]` keyed `"animutil-"+cacheDictKey`) +
  a fresh `AnimatableRenderer`. `OnRenderFrame` (:89) is the tick — **it runs off the render loop and
  bails on `IsGamePaused`** (:91). `StartAnimation` (:105), `StopAnimation` (:124), `ShouldPlaySound` (:192).
- `AnimatableRenderer.cs` — registers itself on the requested stage **plus `OIT`, `ShadowFar`,
  `ShadowNear`** (:86-95), splits transparent vs opaque geometry, builds the model matrix as
  translate→(+0.5,0,+0.5)→scale→rotateY→(−0.5,0,−0.5) (:114-127), uploads `animator.Matrices` to the
  `"Animation"` UBO (:169). Public knobs: `ShouldRender`, `CustomTransform`, `ScaleX/Y/Z`,
  `rotationDeg`, `renderColor`, `backfaceCulling`, `LightAffected`, `StabilityAffected`, `FogAffectedness`.

`vsapi` highlight / multiblock / schematic:
- `vsapi/Common/API/IWorldAccessor.cs:11` — `EnumHighlightSlot{Selection=0,Brush=1,Spawner=2,LandClaim=3,SelectionStart=4,SelectionEnd=5,TerrainVisualizer=6}`;
  `HighlightBlocks(...)` overloads at :697 and :707.
- `vsapi/Client/Render/EnumHighlightBlocksMode.cs` — `Absolute`, `CenteredToSelectedBlock`,
  `CenteredToSelectedBlockFollowTerrain`, `AttachedToSelectedBlock`, `CenteredToBlockSelectionIndex`,
  `AttachedToBlockSelectionIndex`.
- `vsapi/Common/EnumHighlightShape.cs` — `Arbitrary`, `Cube`, `Ball`, `Cubes`, `Cylinder`.
- `vsapi/Common/MultiblockStructure.cs` — `HighlightSlotId = 23` (:30), `BlockNumbers`,
  `Offsets` (`BlockOffsetAndNumber : Vec4i`, W = block number), `InitForUse(float rotateYDeg)` (:51),
  `WalkMatchingBlocks` (:74), `InCompleteBlockCount` (:106), `ClearHighlights` (:132),
  `HighlightIncompleteParts` (:137). Its own doc comment names the generator command (`/we mgencode`)
  and warns **air blocks are not exported** (:26).
- `vsapi/Client/API/IRenderAPI.cs:898-916` — `AddPointLight` / `RemovePointLight` / `IPointLight{Color,Pos}`.
- `vsapi/Client/Render/EnumRenderStage.cs` — `Before=0, Opaque, OIT, AfterOIT, ShadowFar,
  ShadowFarDone, ShadowNear, ShadowNearDone, AfterPostProcessing, AfterBlit, Ortho,
  AfterFinalComposition, Done=12`.
- `vsapi/Client/Audio/ILoadedSound.cs` — `Start/Stop/Pause/Toggle`, `SetVolume`/`SetPitch`/
  `SetPitchOffset`/`SetPosition`/`SetLooping`, `FadeTo`/`FadeIn`/`FadeOut`/`FadeOutAndStop`,
  `IsReady`/`IsPlaying`/`HasStopped`/`IsDisposed`, `SetLowPassfiltering`/`SetReverb`.
- `vsapi/Client/Audio/SoundParams.cs` — `Location`, `Position`, `RelativePosition`, `ShouldLoop`,
  `DisposeOnFinish` (comment at :41 explicitly says set it **false** for looping sounds and dispose
  yourself), `Pitch`, `Volume`, `Range` (32), `ReferenceDistance` (3), `SoundType`,
  `LowPassFilter`, `ReverbDecayTime`.
- `vsapi/Common/Collectible/Block/BlockSchematic.cs` (1524) — see keyTypes.

## Key types

### `AnimationUtil / BlockEntityAnimationUtil`

`vsapi/Common/Model/Animation/AnimationUtil.cs:10 ; vsessentialsmod/BlockEntityBehavior/BlockEntityAnimationUtil.cs:12`

The block-entity animation driver. Owns the AnimatorBase, the AnimatableRenderer, and the `activeAnimationsByAnimCode` dictionary that is the single source of truth for what is playing.

`activeAnimationsByAnimCode` (public field, AnimationUtil.cs:14) — you mutate this directly to retune a running animation: `animUtil.activeAnimationsByAnimCode[code].AnimationSpeed = x` (the only vanilla example is vssurvivalmod/Lore/ResoArchives/BEBehaviorControlPointAnimatable.cs:100). `StartAnimation(meta)` (:105) returns **false and changes nothing** if the code is already present — it will not replace the meta. `StopAnimation(code)` (:124) is a plain `Remove`. `OnRenderFrame` (:89) is the tick: it calls `animator.OnFrame(dict, deltaTime)` **on the render thread with the render dt**, and returns early when `capi.IsGamePaused`. `InitializeAnimator(cacheDictKey, meshdata, shape, rotation, renderStage = Opaque)` (:59) disposes and recreates the renderer. `GetAnimator` (:130) caches baked animations globally under `api.ObjectCache["animUtil-animCache"]` keyed by `"animutil-" + cacheDictKey` — two blocks sharing a cacheDictKey share baked frames. The subclass adds `CreateMesh(name, shape, out resultingShape, texSource, metaOverride)` (BlockEntityAnimationUtil.cs:44) which honours QuantityElements/SelectiveElements/IgnoreElements, `OnAfterTesselate` hook, and throws if you re-init off the main thread once a renderer exists (:25).

### `AnimatorBase`

`vsapi/Common/Model/Animation/AnimatorBase.cs:25`

The animation state machine shared by client and server. Syncs `RunningAnimation[]` against the caller's activeAnimationsByAnimCode every frame.

`OnFrame(Dictionary<string,AnimationMetaData>, float dt)` (:110) — start/stop/progress in one pass. `ProgressRunningAnimation` (:197) implements the OnAnimationEnd/OnActivityStopped matrix and, crucially, **returns false to make OnFrame remove the code from your dictionary** (:174-178) — so a `Stop`/`EaseOut` animation deletes its own entry. `GetAnimationState(code)` (:74) — lowercases before lookup; returns the live `RunningAnimation` (CurrentFrame, Iterations, EasingFactor). `Matrices` (:56) returns the default-pose matrices whenever `activeAnimCount == 0`. `CurAnims` is a fixed `RunningAnimation[20]` (:50) filled unchecked at :225. `AnimNowActive` (:261) resets ShouldRewind/ShouldPlayTillEnd and seeds `CurrentFrame = animData.StartFrameOnce` then zeroes it — `StartFrameOnce` is genuinely once-only. `DumpCurrentState()` (:237) prints easing/frame/iterations per animation; `entityForLogging` enables the missing-animation warning.

### `RunningAnimation`

`vsapi/Common/Model/Animation/RunningAnimation.cs:16`

Live state of one animation. This is what you read to know the phase of a machine animation.

`CurrentFrame` (float), `Iterations` (int, +1 per completed loop), `EasingFactor`, `BlendedWeight`, `Active` vs `Running` (Running stays true through rewind/play-till-end), `NowPlaying`, `AnimProgress => CurrentFrame / (QuantityFrames-1)` (:48). `Progress(dt, walkspeed)` (:103) — returns immediately if `meta.AnimationSpeed == 0` (so speed 0 **freezes** rather than being a valid zero-rate); frame advance is `30 * dt * meta.GetCurrentAnimationSpeed(walkspeed)` (:120), i.e. authored fps is 30 and `AnimationSpeed` is a plain multiplier. `Stop()` (:176) zeroes everything including `SoundPlayedAtIteration`.

### `AnimatableRenderer`

`vsapi/Common/Model/Animation/AnimatableRenderer.cs:12`

Draws the animated mesh at a fixed world position. What BEBehaviorAnimatable actually puts on screen.

`ShouldRender` (:29) is the on/off switch AnimationUtil toggles. `CustomTransform` (float[16], :27) fully replaces the default translate/scale/rotateY chain (:117-127) — use it for anything the Y-rotation cannot express. `ScaleX/Y/Z`, `rotationDeg`, `renderColor` (Vec4f), `backfaceCulling`, `LightAffected`, `StabilityAffected`, `FogAffectedness`. It self-registers on the requested stage plus OIT, ShadowFar and ShadowNear (:86-95) and splits transparent geometry into `mtmeshrefTransparent` automatically (:74-87). Light comes from a single `GetLightRGBs((int)pos.X, (int)pos.Y, (int)pos.Z)` sample per frame (:137).

### `AnimationSound + AnimationManager.ShouldPlaySound`

`vsapi/Common/Model/Animation/AnimationSound.cs:13 ; vsapi/Common/Model/Animation/AnimationManager.cs:552`

Declarative per-animation sounds, one-shot at a frame or looping for the duration.

`Frame`, `Chance`, `Looping`, and `Attributes` (SoundAttributes: Location/Pitch/Volume/Range/Type) populated from the json aliases `location`|`path`, `pitch`, `volume`, `range`, `randomizePitch` (:36-77). Trigger site is ClientAnimator.cs:305-318: fires once per `Iterations` value, only while `anim.Active && anim.NowPlaying` — so with `OnAnimationEnd:Repeat` it re-fires every loop, with `Hold` exactly once. The looping branch (AnimationManager.cs:556-583) builds a `SoundParams{ShouldLoop=true, DisposeOnFinish=false}` — see gotchas, the bookkeeping dictionary is broken.

### `BEBehaviorAnimatable`

`vsessentialsmod/BlockEntityBehavior/BEBehaviorAnimatable.cs:21`

The `"Animatable"` block-entity behavior — the two-line wiring every animated machine block uses.

`animUtil` (public field, :25). `OnTesselation` (:51) returns `animUtil.activeAnimationsByAnimCode.Count > 0 || animator.ActiveAnimationCount > 0` — returning true means "I drew myself, skip the static chunk mesh". That is the static↔animated swap; combined with `BlockEntityAnimationUtil.OnAnimationsStateChange` (BlockEntityAnimationUtil.cs:96) which calls `MarkBlockDirty(pos, () => renderer.ShouldRender = …)` so the flag flips in the retesselation callback, not before. Disposes animUtil in OnBlockUnloaded/Broken/Removed (:33-49) — a leak if you subclass and forget.

### `BlockSchematic`

`vsapi/Common/Collectible/Block/BlockSchematic.cs:48`

The save/load/rotate/paste format for a cuboid of blocks + decors + block entities + entities. Everything multiblock-placement-related in vanilla goes through it.

Packed form: `Indices` (uint, `dy<<20 | dz<<10 | dx`, `PosBitMask=0x3ff`, hard 1024 limit per axis, :132/:479), `BlockIds`, `DecorIndices`/`DecorIds` (`faceAndSubposition<<24 | blockId`), `BlockEntities` (index→Ascii85 tree), `Entities` (Ascii85 blobs), `BlockCodes`/`ItemCodes` remap tables. `AddArea` (:396) reads Solid and Fluid layers separately and calls `be.OnStoreCollectibleMappings`. `Pack` (:453) computes `PackedOffset` and **clears all the Unpacked dictionaries**. `Place(ba, world, startPos, mode, replaceMetaBlocks)` (:642) + `PlaceDecors` (:715) + `PlaceEntitiesAndBlockEntities` (:1085). `TransformWhilePacked(world, origin, angle, flipAxis)` (:771) is the rotation entry point. `GetRotatedPos` (:1027). `AdjustStartPos(pos, EnumOrigin)` (:1261). `ClonePacked` (:1490), `PasteToMiniDimension` (:1516), `LoadFromFile`/`LoadFromString`/`Save`/`ToJson`. Static `BlockRemaps`/`ItemRemaps` are filled by the RemapperAssistant and used to survive version renames (:1129-1141).

### `BlockSchematicStructure / BlockSchematicPartial`

`vsessentialsmod/Systems/WorldGen/Standard/Datastructures/BlockSchematicStructure.cs:15 ; BlockSchematicPartial.cs:25`

Worldgen-flavoured schematics: terrain-aware placement and chunk-by-chunk placement.

`Init(ba)` (:41) unpacks into `blocksByPos[SizeX+1,SizeY+1,SizeZ+1]` + `FluidBlocksByPos`. `PlaceRespectingBlockLayers(ba, world, startPos, climate×4, replaceBlocks, replaceWithBlockLayersBlockids, replaceMetaBlocks, replaceBlockEntities, suppressSoilIfAirBelow, displaceWater)` (:110). `PlaceReplacingBlocks(...)` (:353) applies a per-rocktype substitution map. `Unpack(api, orientation)` (:531) pre-bakes rotated copies. `OffsetY` (default −1), `MaxYDiff` (3), `MaxBelowSealevel` (20). `BlockSchematicPartial.PlacePartial(chunks, worldgenBlockAccessor, …)` (:31) places only the slice inside the chunk column currently generating — the pattern to copy if a structure must straddle chunks.

### `MultiblockStructure`

`vsapi/Common/MultiblockStructure.cs:28`

Vanilla's declarative multiblock validator + highlighter, the thing `/we generate-multiblock-code` emits JSON for.

`BlockNumbers` (AssetLocation→int, 1-based), `Offsets` (`BlockOffsetAndNumber : Vec4i`, W = block number), `OffsetsOrientation`. **`InitForUse(float rotateYDeg)` takes DEGREES** (:51) and must be called before anything else — the other methods throw `InvalidOperationException` otherwise. `WalkMatchingBlocks` (:74), `InCompleteBlockCount(world, centerPos, onMismatch)` (:106) uses `WildcardUtil.Match(BlockCodes[offset.W], block.Code)` so the codes are wildcard patterns. `HighlightIncompleteParts` (:137) colours occupied-but-wrong red (215,94,94,64) and missing with the wanted block's own colour at alpha 96. `HighlightSlotId = 23` (:30) — a fixed slot, so two multiblocks highlighting at once fight.

### `WorldEditWorkspace`

`vscreativemod/Workspace.cs:24 (+ WorldEditSelectionTools.cs:11, same partial class)`

Per-player world-edit state; the reference implementation of preview-in-a-mini-dimension, selection highlighting and undoable bulk edits.

`revertableBlockAccess` (IBlockAccessorRevertable) with `OnStoreHistoryState`/`OnRestoreHistoryState` hooks so marker positions ride along with undo (:152/:173). `CreateDimensionFromSchematic(blockData, startPos, origin)` (:883) — `CreateMiniDimension` + `sapi.Server.LoadMiniDimension` + `SetSubDimensionId` + `PasteToMiniDimension`, i.e. a live ghost preview with no world writes. `PasteBlockData` (:781) clones→`TransformWhilePacked`→`Init`→`Place`→`PlaceDecors`→`Commit`→`PlaceEntitiesAndBlockEntities`→`CommitBlockEntityData`. `MoveArea(offset, start, end)` (:656) is the full "move blocks *and* their BE data" recipe (BeginMultiEdit → read trees → delete+Commit → place+Commit → rewrite posx/posy/posz into each tree → FromTreeAttributes → EndMultiEdit). `HighlightSelectedArea` (:434) shows the four-slot pattern (Brush / Selection / SelectionStart / SelectionEnd). Settings live in `IntValues`/`StringValues`/`FloatValues` dictionaries keyed `"std.*"` so tools stay stateless and serialisable (`ToBytes`/`FromBytes`, :213/:283).

### `ToolBase / ToolRegistry`

`vscreativemod/Tool/ToolBase.cs:12 ; vscreativemod/ToolRegistry.cs:9`

The extensible world-edit tool contract. `WorldEdit.RegisterTool(name, type)` (WorldEdit.cs:265) lets a mod add its own.

`abstract Vec3i Size`, `ScrollEnabled`, `ScrollMode`, `OnInteractStart`/`OnAttackStart`, `ApplyToolBuild`/`ApplyToolBreak`, `OnWorldEditCommand(worldEdit, TextCommandCallingArgs)`, `GetAvailableModes(capi)` → `List<SkillItem>`, and the highlight trio `GetBlockHighlights`/`GetBlockHighlightColors`/`GetBlockHighlightShape` funnelled through `HighlightBlocks` (:139). `PlaceOldBlock` (:68) shows the fluid-layer-aware restore. Tools are constructed by `Activator.CreateInstance(type, new object[]{workspace, blockAccessor})` so **every tool needs a `(WorldEditWorkspace, IBlockAccessorRevertable)` ctor** (ToolRegistry.cs:18).

### `EntityPartitioning`

`vsessentialsmod/Systems/EntityPartitioning.cs:61`

The 8×8-block spatial hash used for all 'entities near this point' queries. Rebuilt from scratch every 32 ms.

`WalkEntities(centerPos, radius, callback, EnumEntitySearchType)` (:286) and the raw `WalkEntities(x,y,z,radius,callback,RangeTestDelegate,searchType)` (:306) — callback returns false to abort. `WalkEntityPartitions` (:301) skips the exact-radius test. `GetNearestEntity(pos, radius, matches, searchType)` (:200), `GetNearestInteractableEntity` (:188). `EnumEntitySearchType.Creatures` vs `Inanimate` — since 1.19.2 creatures and item/other entities are in **separate lists** and the default overloads only search creatures (the `[Obsolete]` ones at :179/:264/:271). `LargestTouchDistance` (:81). `RePartitionPlayer` (:168) is called on dimension change and gamemode switch.

### `AiTaskManager / IAiTask / EntityBehaviorTaskAI`

`vsessentialsmod/Entity/AI/AiTaskManager.cs:99 ; Entity/AI/IAiTask.cs:7 ; Entity/AI/BehaviorTaskAI.cs:9`

The mob behaviour system: 8 parallel priority slots, JSON-configured task instances.

`AiTaskRegistry.Register<T>(code)` / `sapi.RegisterAiTask<T>(code)` (AiTaskManager.cs:16, :27) — registration is a **static dictionary populated in a static constructor**, so it is global, not per-API-instance. `ActiveTasksSlotsNumber = 8` (:111). Preemption rule: a task starts only when `task.Priority > activeTask.PriorityForCancel` (:456) — the two-number scheme is what stops flapping. `ProcessRunningTasks` (:488) `continue`s (does *not* stop) when `CanContinueExecute()` is false, which is how async pathfinding stalls a task without killing it. Extension points: `OnTaskStarted`, `OnTaskStopped`, `OnShouldExecuteTask` (all delegates must return true), `OnExecuteTask`. `Notify(key, data)` (:340) handles the magic keys `"starttask"`/`"stoptask"` by task Id. `EntityBehaviorTaskAI.Initialize` instantiates tasks via `Activator.CreateInstance(taskType, entityAgent, taskConfig, attributes)` (:91) — that 3-arg ctor is the contract.

### `EntityParticleSystem`

`vsessentialsmod/Systems/ParticleEntity/EntityParticleSystem.cs:59`

A second, independent particle engine for 'creature' particles (fish, gnats, cicadas) simulated on a dedicated thread and drawn with instanced cubes.

`SpawnParticle(EntityParticle)` (:242) and `KillParticle` (:266) both **throw `InvalidOperationException` unless called on the sim thread** — hook `OnSimTick` (:82) and spawn from there. `partPhysics` (`ParticlePhysics`) with `PhysicsTickTime` set to 0.125/8 per off-thread frame (:413). Ring of 5 update buffers with `advanceCount` under `advanceCountLock` (:329-343 read side, :389-395 write side); if the sim runs ahead it skips a frame, if it lags the render side re-uses the last buffer. Pool 10 000, mesh layout at :136-161. Shader `particlescube` with `#define VEC3SCALE` (:190). `EPCounter Count` (:99) exposes per-type live counts.

### `WeatherSimulationSound`

`vsessentialsmod/Systems/Weather/WeatherSimulationSound.cs:13`

The best in-tree template for a continuously-modulated set of looping sounds.

Sounds are created **once** in `Initialize` (:58) with `ShouldLoop=true, DisposeOnFinish=false, RelativePosition=true`. `updateSounds` (:170) refuses to touch anything until every `ILoadedSound.IsReady` (:172-186) — a real ordering hazard on world join. Rising-edge `Start()` behind a `bool xSoundsOn` latch (:262-270), per-frame `SetVolume`/`SetPitch`, `Stop()` below a 0.01 threshold (:306-317), and `Dispose()` on all of them (:354). Volumes are lerped as `cur += (target - cur) * dt` (:335-336). Runs on a 0.25 s accumulator (:141-146) with `dt` clamped to 0.5 (:139).

### `EntityBehaviorInterpolatePosition`

`vsessentialsmod/Entity/Behavior/BehaviorInterpolatePosition.cs:52`

Client-side smoothing of a discrete server-tick stream. The exact shape of the problem a machine animation phase-locked to a network has.

`const float interval = 1/15f` (:110) — the assumed server position tick. `Queue<PositionSnapshot> positionQueue` + `pL`/`pN` lerp endpoints. `OnReceivedServerPos` (:171) pushes a snapshot whose interval is scaled by the `"tickDiff"` entity attribute (:173) and flushes hard on teleport. `OnRenderFrame` (EnumRenderStage.Before, :219) accumulates `dtAccum += dt * targetSpeed` (targetSpeed = 0.6, i.e. it deliberately runs the buffer *slow* to build slack) and pops while `dtAccum > pN.interval`. `wait`/`queueCount` implement a re-buffering stall when the queue drains. Drops more than 20 queued snapshots (:210-213).

### `BlockMultiblock / BlockBehaviorMultiblock`

`vsessentialsmod/Block/BlockMultiblock.cs:59 ; vsessentialsmod/BlockBehavior/BehaviorMultiblock.cs:35`

Vanilla's built-in (and deliberately limited) multiblock: invisible filler blocks that forward everything to a controller.

`Offset`/`OffsetInv` parsed from the block code variants `dx`/`dy`/`dz` in `OnLoaded` (:64-71). Every override forwards to `pos + OffsetInv` through `Handle<T,K>` (:78) which tries an `IMultiBlock*` interface first, then guards `block is BlockMultiblock` to avoid infinite recursion. Interfaces you implement on the controller: `IMultiBlockInteract`, `IMultiBlockColSelBoxes`, `IMultiBlockActivate`, `IMultiBlockBlockProperties`, plus `MBOnBlockBroken`/`MBOnGettingBroken`/`MBGetRandomColor`. The behavior's JSON is `sizex/sizey/sizez` (max 5), `cposition`, `type` (default `monolithic`), `offsetHitboxes`; `OnBlockPlaced` (:116) resolves `multiblock-<type>-<sdx>-<sdy>-<sdz>` and **throws IndexOutOfRangeException** when the block doesn't exist.

### `POIRegistry / RoomRegistry`

`vsessentialsmod/Systems/POIRegistry.cs:148 ; vsessentialsmod/Systems/RoomRegistry.cs:91`

Two server-side spatial registries a machine mod may want to hook: 'points of interest' (food/nests) and 'is this position inside a sealed room'.

POIRegistry: implement `IPointOfInterest{Vec3d Position; string Type}` and call `AddPOI`/`RemovePOI` (:283/:297); query with `GetNearestPoi(center, radius, matcher)` (:194) or `WalkPois` (:165). Bucketed per chunk **column** (Vec2i), never synced to clients. RoomRegistry: `GetRoomForPosition(pos)` (:346) returns a cached `Room` with `ExitCount`, `IsSmallRoom`, `SkylightCount`/`NonSkylightCount`, `CoolingWallCount`/`NonCoolingWallCount`, `Cuboidi Location` and a `byte[] PosInRoom` bitmap. Hard limits: BFS array 29³, `MAXROOMSIZE=14`, `MAXCELLARSIZE=7`, `ALTMAXCELLARSIZE=9`, `ALTMAXCELLARVOLUME=150` (:403-409). `Room.AnyChunkUnloaded` (:40) is a retry counter for rooms on the loaded-world edge.

### `ModRegistryObjectTypeLoader / ResolvedVariant`

`vsessentialsmod/Loading/RegistryObjectTypeLoader.cs:52 ; :27`

Turns every block/item/entity JSON into concrete registry objects; defines exactly how a variant code string is built.

`ResolvedVariant.ResolveCode(baseCode)` (:33) appends `"-" + part` for every non-empty code part **in `CodeParts` insertion order**, which is variantgroup declaration order. `GatherVariants` (:499): collect from state lists / world properties, `MultiplyProperties`, then apply `allowedVariants` and `skipVariants` filters. `CollectFromStateList` (:620) handles `EnumCombination.Add` / `Multiply` / `SelectiveMultiply` (+`OnVariant`). `GetWorldPropertyByCode` (:490). `RegistryObjectType.solveByType` (RegistryObjectType.cs:306) resolves all `*byType` keys against the resolved variant dictionary; `loadInherits` (:230) implements `inheritFrom` with `MergeArrayHandling.Replace` (arrays replace, they do not concatenate).

### `ModCompatiblityUtil`

`vsessentialsmod/IntermodTools.cs:11`

The supported way to ship assets that only apply when another mod is present.

`ExecuteOrder() => 0.04` — runs in `AssetsLoaded`, before json patching (:20). Any asset placed at `compatibility/<otherModId>/<normal asset path>` is re-registered as `<otherModId>:<normal asset path>` if and only if that mod is loaded (:33-66). Skips categories whose `SideType` does not include the current side (:59). Logs added/replaced counts.


---

<a id="examples-tooling"></a>

# examples, templates, JSON patching, tooling

All paths below are relative to `.compat/vintagestory/`. Six repos are in scope. Two of them (`vsmodexamples`, `VSdotnetModTemplates`) are documentation-by-example; three (`Tavis.JsonPatch`, `Cairo`, `nanosvg`) are libraries the game ships and mods link against; one (`modpeek`) is the ModDB-side validator.

---

# 1. `vsmodexamples/` — official example mods (@ 2b07243)

Two top-level trees + `README.md` + `LICENSE`. **No `.csproj` in the repo root**; each example is a self-contained mod project. Verified against game 1.21.0; the nine current code tutorials target **net8.0**, the ten `old_code_mods` still target **net7.0**.

```
vsmodexamples/
├── README.md                         # index; "Content Mods: 1.21.0, Code Mods: 1.21.0"
├── Content Mods/                     # 9 pure-JSON mods, no C#
└── Code Mods/
    ├── VSTutorial - 1..9 …/          # 9 current C# tutorials (net8.0)
    └── old_code_mods/                # 10 legacy samples (net7.0), no READMEs except NativeInterop
```

## 1a. `Code Mods/VSTutorial - N - …/` — the nine current tutorials

Uniform shape. Each tutorial folder contains `README.MD`, a `zips/` folder with `…-complete.zip` (and usually `…-setup.zip`), and one source folder `vscodetutorial-<topic>/` holding:

```
vscodetutorial-<topic>/
├── VSTutorial.sln
├── build.ps1 / build.sh              # one-liners: dotnet run --project ZZCakeBuild/CakeBuild.csproj -- $args
├── ZZCakeBuild/{CakeBuild.csproj,Program.cs}   # "ZZ" prefix so it sorts after the mod project
└── VSTutorial/
    ├── VSTutorial.csproj             # net8.0, OutputPath bin\$(Configuration)\Mods\mod
    ├── modinfo.json                  # modid "vstutorial", dependencies {"game": ""}
    ├── VSTutorialModSystem.cs        # the entry point in every tutorial
    ├── Properties/launchSettings.json
    ├── <Topic>/<TheClass>.cs         # the one file that carries the lesson
    └── assets/vstutorial/…           # only when the lesson needs JSON
```

| # | Folder | Technique demonstrated | Key file (under `Code Mods/VSTutorial - N - …/vscodetutorial-*/VSTutorial/`) |
|---|---|---|---|
| 1 | Simple Block Tutorial | Subclass `Block`, override `OnEntityCollide`, register via `api.RegisterBlockClass(Mod.Info.ModID + ".trampoline", …)` | `Blocks/BlockTrampoline.cs:25`; asset `assets/vstutorial/blocktypes/trampoline.json` |
| 2 | Simple Item Tutorial | Subclass `Item`, override `OnAttackingWith`, build a `DamageSource` | `Items/ItemThornsBlade.cs:17` |
| 3 | Simple Command Tutorial | `api.ChatCommands.Create(...).RequiresPlayer().RequiresPrivilege(Privilege.chat).HandleWith(...)`; server (`/`) vs client (`.`) commands | `Commands/VSTutorialCommands.cs:23` |
| 4 | Block Behavior Tutorial | `BlockBehavior` + `Initialize(JsonObject properties)` reading `properties["distance"].AsInt(1)`; `EnumHandling.PreventDefault` | `BlockBehaviors/BlockBehaviorMoveable.cs:33,47`; JSON wiring `assets/vstutorial/blocktypes/moving.json:4` |
| 5 | Block Entity Tutorial | `BlockEntity`, `RegisterGameTickListener(OnGameTick, 50)`, `To/FromTreeAttributes`, `CodeWithParts("on"/"off")` | `BlockEntities/BlockEntityTicking.cs:21,59,71` |
| 6 | Simple GUI & Hotkeys Tutorial | `GuiDialog` + `ElementBounds`/`capi.Gui.CreateCompo(...).Compose()`; `api.Input.RegisterHotKey` / `SetHotKeyHandler`; `ShouldLoad(side)==Client` | `GUI/GuiDialogCenteredTextBox.cs:30`; `VSTutorialModSystem.cs:20,34` |
| 7 | Basic Harmony Patching | `[HarmonyPatchCategory("vstutorial")]`, postfix with `__instance`/`__result`, prefix returning `bool`; `Harmony.HasAnyPatches` guard + `UnpatchAll` in `Dispose` | `Patches/TutorialPatches.cs:20,30,45,63`; `VSTutorialModSystem.cs:25-41` |
| 8 | Entity Behavior & Server→Client Data | `EntityBehavior`, `WatchedAttributes.GetTreeAttribute(...)` + `MarkPathDirty`, `PropertyName()`, manual `UnregisterGameTickListener`; **wires itself in via a JSON patch** | `EntityBehaviors/EntityBehaviorTotalPlayTime.cs:40,61,101,110`; patch `assets/vstutorial/patches/game-entities-humanoid-player.json` |
| 9 | Specific Networking Tutorial | `api.Network.RegisterChannel(...).RegisterMessageType<T>()`, `[ProtoContract]/[ProtoMember]` DTOs, split client/server `ModSystem`s, `ExecuteOrder()` bumped to 0.11 so the channel exists | `VSTutorialModSystem.cs:23`; `Networking/{VSTutorialClientSystem,VSTutorialServerSystem,VSTutorialNetworkMessage,VSTutorialNetworkResponse}.cs` |

Note tutorial 9 is the odd one out: it has **no README.MD and no `zips/`**, and its project sits directly at `Code Mods/VSTutorial - 9 - Specific Networking Tutorial/VSTutorial/` (no `vscodetutorial-*` wrapper).

## 1b. `Code Mods/old_code_mods/` — ten legacy samples (net7.0)

Each is `<Name>/{<Name>.sln, build.ps1, build.sh, CakeBuild/, <name>/}`.

| Folder | Technique | Key file |
|---|---|---|
| `BackpackRenderer` | Custom `IRenderer` on `EnumRenderStage.Opaque`+`ShadowFar`+`ShadowNear`; `UploadMesh(TesselatorManager.GetDefaultBlockMesh(block))`, `ModelTransform`, `Mat4f` | `BackpackRenderer/BackpackRendererModSystem.cs:11,45` |
| `CustomShapeBlock` | Runtime block shape mutation without a renderer: `BlockEntity : ITexPositionSource`, `OnTesselation(ITerrainMeshPool, ITesselatorAPI)` | `CustomShapeBlock/CustomShapeBlockModSystem.cs:44,54` (all classes nested in one file) |
| `HouseGenerator` | Block read/write basics: `BlockAccessor.SetBlock`, `WalkBlocks`, `api.Event.PlayerJoin` | `HouseGenerator/HouseGeneratorModSystem.cs:17,26` |
| `HudOverlaySample` | 2D HUD via `EnumRenderStage.Ortho`, `LineMeshUtil.GetRectangle`, `QuadMeshUtil.GetQuad`, raw shader uniforms | `HudOverlaySample/WeirdProgressBarRenderer.cs:33` |
| `NativeInterop` | **Shipping native binaries in a mod**: `native/lib*.{dll,so,dylib}` + `NativeLibrary.SetDllImportResolver` resolving off `((ModContainer)Mod).FolderPath`; C/Go sources in `libcopystring/`; Cake copies `native/` into the release | `nativeInterop/NativeInteropModSystem.cs:15,27`; `Readme.md`; `CakeBuild/Program.cs` (has the extra `native/` copy branch) |
| `NetworkApiTest` | Older networking form using `[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]` and `RegisterMessageType(typeof(T))` | `NetworkApiTest/NetworkApiTestModSystem.cs:9,27` |
| `OldWorldEdit` | Large server-command sample (350 lines): `api.Permissions.RegisterPrivilege`, `api.GetOrCreateDataPath`, schematic export | `OldWorldEdit/OldWorldEditModSystem.cs:27` |
| `PushPlayerJoinLeave` | `api.LoadModConfig<T>("pushconfig.json")` + `HttpClient` on player join/leave; server-only | `PushPlayerJoinLeave/PushPlayerJoinLeaveModSystem.cs:24`; `PushConfig.cs` |
| `ScreenOverlayShaderExample` | Custom GLSL shader: `api.Event.ReloadShader += LoadShader`, `IShaderProgram`, full-screen quad in Ortho | `ScreenOverlayShaderExample/ScreenOverlayShaderExampleModSystem.cs:25`; `ExampleOverlayRenderer.cs` |
| `TreasureChest` | Worldgen chunk hook + inventory filling; includes a reusable `ShuffleBag<T>` | `TreasureChest/TreasureChestMod.cs:14` (the `ModSystem`); `TreasureChestModSystem.cs` is an empty stub |

## 1c. `Content Mods/` — nine JSON-only mods

Each folder is `<N - Title>/{README.MD, <modfolder>/, zips/{…-setup.zip, …-complete.zip}}`; `<modfolder>` holds `modinfo.json`, `modicon.png`, `assets/<domain>/…`.

| # | Domain | Demonstrates | Key asset |
|---|---|---|---|
| 1 | `simpleitem` | minimal item + shape + texture | `itemtypes/simplewand.json` |
| 2 | `simpleblock` | minimal block | `blocktypes/simpleshinyblock.json` |
| 3 | `simplerecipe` | grid recipes | `recipes/grid/{simpleblockrecipe,simplewandrecipe}.json` |
| 4 | `itemvariants` | variantgroups on items | `itemtypes/advancedwand.json` |
| 5 | `blockvariants` | variantgroups on blocks | `blocktypes/advancedshinyblock.json` |
| 6 | `complexgridrecipes` | ingredient patterns / wildcards | `recipes/grid/sleekDoor.json` |
| 7 | `furtherrecipes` | barrel, clayforming, knapping, smithing recipe types | `recipes/{barrel/agedlog,clayforming/bricks,knapping/stone-brick,smithing/metalsheets}.json` |
| 8 | `simpleworldgen` | deposits + blockpatches | `worldgen/deposits/sand.json`, `worldgen/blockpatches/{bones,loosesticks}.json` |
| 9 | `entitycreation` | entity JSON + AI + spawn item (2-part tutorial) | `entities/jackrabbit.json`, `itemtypes/creature.json`, `shapes/entity/jackrabbit.json` |

---

# 2. `VSdotnetModTemplates/` — the official `dotnet new` templates (@ 934a4c9, "update templates to net 10")

Two independently-packed NuGet template packages, both `PackageVersion 1.22.0`, both `TargetFramework net10.0`.

```
VSdotnetModTemplates/
├── README.md                     # lists vsmod / vsmoddll, dotnet pack + dotnet new install instructions
├── modicon.png                   # NuGet PackageIcon for both packages
├── .vscode/{settings.json,tasks.json}
├── VSBasicModTemplate/           # PackageId VintageStory.Mod.BasicTemplate
│   ├── README.md
│   ├── VSBasicModTemplate.csproj # PackageType=Template, packs VintageStoryBasicMod/**
│   └── VintageStoryBasicMod/     # shortName "vsbasicmod"; only symbol is ModIdLowercase
│       ├── .template.config/template.json
│       ├── _ProjectName_.sln
│       ├── build.ps1, build.sh, .gitignore
│       ├── ZZCakeBuild/{CakeBuild.csproj,Program.cs}
│       └── _ProjectName_/{_ProjectName_.csproj, _ProjectName_ModSystem.cs,
│                          modinfo.json, Properties/launchSettings.json,
│                          assets/_modid_/lang/en.json}
└── VSModTemplates/               # PackageId VintageStory.Mod.Templates
    ├── README.md
    ├── VSModTemplates.csproj     # packs templates/**, excludes bin/obj
    └── templates/
        ├── VintageStoryMod/      # shortName "vsmod"  (full: mod project + CakeBuild + sln)
        │   ├── .template.config/template.json
        │   ├── .vscode/{extensions.json,launch.json,tasks.json}
        │   ├── CakeBuild/{CakeBuild.csproj,Program.cs}
        │   ├── _ProjectName_.sln, build.ps1, build.sh, .gitignore
        │   └── _ProjectName_/{…same as basic…}
        └── VintageStoryModDLL/   # shortName "vsmoddll" (flat, no Cake, no assets, dll only)
            ├── .template.config/template.json
            ├── .vscode/…
            ├── _ProjectName_.csproj, _ProjectName_ModSystem.cs
            └── Properties/launchSettings.json
```

**Canonical mod-project csproj** (`VSModTemplates/templates/VintageStoryMod/_ProjectName_/_ProjectName_.csproj`):
- `net10.0`, `Nullable=enable`, `AppendTargetFrameworkToOutputPath=false`, `OutputPath=bin\$(Configuration)\Mods\mod` (the dll-only variant drops the trailing `\mod`).
- Every game reference is `<Reference Include="…"><HintPath>$(VINTAGE_STORY)/…</HintPath><Private>false</Private>`. Optional refs are guarded by `<!--#if (IncludeX) … #endif -->` comment-conditionals: `VSSurvivalMod`, `VSEssentials`, `VSCreativeMod` (under `Mods/`), and `Newtonsoft.Json`, `0Harmony`, `protobuf-net`, `cairo-sharp`, `Microsoft.Data.Sqlite` (under `Lib/`), plus `VintagestoryLib` at the root.
- `modinfo.json` and (conditionally) `modicon.png` are `<Content … CopyToOutputDirectory=PreserveNewest>`.

**Template symbols** (`.template.config/template.json`, `vsmod` variant): `SuppressWindowsConsoleWindow`, `vsInstall` (string, **`replaces: "$(VINTAGE_STORY)"`** — a literal text substitution), `AddSampleCode`, `AddAssetFolder`, `AddSolutionFile`, `IncludeVSSurvivalMod`, `IncludeVSEssentials`, `IncludeVSCreativeMod`, `IncludeNewtonsoft`, `IncludeHarmony`, `IncludeVintagestoryLib`, `IncludeProtobuf`, `IncludeCairoSharp`, `IncludeSQLite`, `IncludeVSCode`, and generated `ModIdLowercase` (casing generator on `name`, `replaces "_modid_"`, `fileRename "_modid_"`). Source modifiers strip `assets/`, `.vscode/`, `_ProjectName_.sln` when the flags are off.

**Cake build/packaging** (`CakeBuild/Program.cs`): `BuildContext` reads `../<ProjectName>/modinfo.json` into `Vintagestory.API.Common.ModInfo` (so CakeBuild itself references `VintagestoryAPI.dll`) and takes `Version`/`ModID` from it. Task chain `Default → Package → Build → ValidateJson`. `ValidateJsonTask` `JToken.Parse`es every `assets/**/*.json` (skippable with `--skipJsonValidation`). `PackageTask` (`:94-113`) does `EnsureDirectoryExists("../Releases")`, **`CleanDirectory("../Releases")`**, copies `bin/<cfg>/Mods/mod/publish/*` → `Releases/<modid>/`, then `assets/` (if present), `modinfo.json`, `modicon.png` (if present), and finally `Zip(...)` to `Releases/<modid>_<version>.zip`. Cake deps: `Cake.Frosting 6.1.0`, `Cake.Json 7.0.1`, `Newtonsoft.Json 13.0.4`.

**Run/debug**: `Properties/launchSettings.json` defines `Client` and `Server` profiles; default form launches `dotnet "$(VINTAGE_STORY)/Vintagestory.dll" --tracelog --addModPath "$(ProjectDir)/bin/$(Configuration)/Mods" --addOrigin "$(ProjectDir)/assets"` with `workingDirectory=$(VINTAGE_STORY)`; the `SuppressWindowsConsoleWindow` branch launches `Vintagestory.exe` and drops `--addOrigin`. `.vscode/launch.json` mirrors this with `${env:VINTAGE_STORY}` and per-OS `program` overrides, plus a third `CakeBuild` config; `.vscode/tasks.json` has `build`, `package`, `build (Cake)`.

**Multi-version targeting: none.** A repo-wide grep for `TargetFrameworks`, `DefineConstants`, or a `Condition=` on a `<Reference>` across `VSdotnetModTemplates` and `vsmodexamples` returns only `Condition="Exists('modicon.png')"`. Every project is single-TFM against one `$(VINTAGE_STORY)` install. Our `-p:Legacy=true` / `src/LegacyUsings.cs` scheme is entirely our own invention — there is no upstream pattern to align with.

---

# 3. `Tavis.JsonPatch/` — the JSON-patch engine (@ 963f03e, `Version 0.5.3-vs.2`, `AssemblyName Tavis.JsonPatch`, `RootNamespace JsonPatch`, net10.0)

```
Tavis.JsonPatch/
├── readme.md, License.txt, package.sh, JsonPatch.sln
└── src/
    ├── JsonPatch/
    │   ├── Tavis.JsonPatch.csproj
    │   ├── PatchDocument.cs            # wire-format parser + ApplyTo
    │   ├── JsonPointer.cs              # RFC 6901 pointer, Find(), ~0/~1 decoding
    │   ├── PathNotFoundException.cs
    │   ├── Adaptors/{IPatchTarget,BaseTargetAdapter,JsonNetTargetAdapter}.cs   # ← ALL the semantics live here
    │   └── Operations/
    │       ├── Abstractions/{Operation,IValueOperation}.cs
    │       ├── AddMergeOperation.cs    # base class
    │       ├── AddReplaceOperation.cs  # : AddMergeOperation  (empty body)
    │       ├── AddEachOperation.cs, InsertOperation.cs, PrependOperation.cs
    │       └── RemoveOperation.cs, ReplaceOperation.cs, MoveOperation.cs,
    │           CopyOperation.cs, TestOperation.cs
    └── JsonPatchTests/                 # xUnit; IntlikeKeyTests.cs is the int-like-key regression suite
```

Anego's fork diverges from RFC 6902 in three tracked commits: `325ae92` (`add` merges when target is an object; new `addreplace` keeps old behaviour), `e546052` (`addeach`), `0fc44b7` (rename `AddOperation`→`AddMergeOperation`, "**in VS we now default for add to addreplace to restore old functionality**"), `2bbafe5` (pointer only indexes by int when the token really is a `JArray`).

## The full operation set as VS exposes it

`vsessentialsmod/Loading/JsonPatchLoader.cs` is the *only* consumer that matters — `PatchDocument.Parse`'s own string→op map (`PatchDocument.cs:60-77`, which maps `"add"`→`AddMergeOperation`, and also knows `append`, `insert`, `prepend`, `test`, `addreplace`) is **never used by the game**. The game builds `Operation` objects directly from the `EnumJsonPatchOp` enum, so only seven ops exist for mod authors:

| `"op"` value | → Tavis op | Adapter method | Semantics |
|---|---|---|---|
| `add` | `AddReplaceOperation` (`JsonPatchLoader.cs:532`) | `AddReplace` (`JsonNetTargetAdapter.cs:73-82`) | last token numeric → `((JArray)parent).Insert(i, value)`; last token `-` → `((JArray)parent).Add(value)`; otherwise **`parent[key] = value`, i.e. overwrite** |
| `addmerge` | `AddMergeOperation` (`:571`) | `AddMerge`→`AddInsertPrepend` (`:23,39-71`) | same array handling, but for a named key: existing `JObject` → `jObject.Merge(value)`; existing `JArray` → append each element of `value` (value **must** be a `JArray`); missing/scalar → assign |
| `addeach` | `AddEachOperation` (`:542`) | `AddEach` (`:84-119`) | both `value` and target must be `JArray`; numeric index inserts each in order at `index++`; `-` appends all; any other last token → `ArgumentException("Path must be an index or '-'")` |
| `remove` | `RemoveOperation` (`:545`) | `Remove` (`:123-134`) | `Find(path)` then `token.Remove()` if parent is a `JArray`, else `token.Parent.Remove()` (drops the whole `JProperty`) |
| `replace` | `ReplaceOperation` (`:555`) | `Replace` (`:17-21`) | `Find(path).Replace(value)` — path must resolve to an existing node |
| `copy` | `CopyOperation` (`:558`) | `Copy` (`:153-158`) | `DeepClone` of `from` then an **AddMerge** at `path` |
| `move` | `MoveOperation` (`:561`) | `Move` (`:136-144`) | guard `path.StartsWith(from)` → `ArgumentException`; then AddMerge + Remove |

`Add`, `AddEach`, `Replace`, `AddMerge` all null-check `Value` and log `"… requires Value"` (`:525,535,548,564`); `Copy`/`Move` do not check `FromPath`.

## Path syntax (`JsonPointer.cs`)

- `new JsonPointer(s)` → `s.Split('/').Skip(1)`, so a path **must start with `/`**; `""` yields zero tokens (`IsRoot`) and then blows up on `Path.Last`.
- Each token is decoded as `Uri.UnescapeDataString(token).Replace("~1","/").Replace("~0","~")` (`:78-81`).
- `Find` (`:34-66`) indexes numerically **only when the current node is a `JArray`** (`:42`), so object keys like `"12"` or `"0xdeadbeef"` resolve correctly (`IntlikeKeyTests.cs`).
- Array append token is `-`. Array insert is a bare integer (`/behaviors/3`). There is **no** `~` wildcard; a `*` inside a path segment is a literal key character (vanilla relies on this: `"/server/spawnconditionsByType/*-corrupt-sawblade/runtime/maxQuantity"` in `.game/1.21/assets/survival/patches/homosapiens.json`).
- Failures throw `PathNotFoundException` with the traversed prefix and a dump of the parent node.

## How VS wraps it — `vsessentialsmod/Loading/JsonPatchLoader.cs` (namespace `Vintagestory.ServerMods.NoObf`)

`ModJsonPatchLoader : ModSystem` (`:182`), `ShouldLoad → true` for every side (`:190`), `ExecuteOrder() => 0.05` (`:195`, matching the table in `vsapi/Common/API/ModSystem.cs:50`), work happens in `AssetsLoaded` (`:197`).

`ApplyPatches(string forPartialPath = null)` (`:214`):
1. `api.Assets.GetMany("patches/")` — every `assets/<domain>/patches/**.json` from every mod, in asset-registry (mod-load) order.
2. Each file deserializes to `JsonPatch[]`; a parse failure logs and drops the **whole file**.
3. Per patch, in order: `Enabled` (`:251`) → side (`:254`) → `Condition` (`:258`) → `DependsOn` (`:277`) → `forPartialPath` (`:298`).
4. `DependsOn` is an AND over `loaded ^ invert` (`:284`) — every entry must be satisfied.
5. `Condition`: reads `api.World.Config[When]`; `attr == null` → silently skipped; `useValue:true` overwrites `patch.Value` with the world-config value; else string-compares against `IsValue` case-insensitively.
6. Target resolution: a trailing `*` on `File.Path` fans out via `api.Assets.GetMany(basePath, domain, false)` (`:319-331`); otherwise a single file. `fileLoc.WithPathAppendixOnce(".json")` (`:427`).
7. A per-run `jsonCache` keyed by `AssetLocation` holds the parsed `JToken` (`:188,430-458`) so patches **compose** — later patches see earlier patches' output. Modified files are serialized back into `asset.Data` and marked `IsPatched = true` only at the very end (`:342-368`).
8. Summary line `"JsonPatch Loader: N patches total, …"` (`:378-414`).

The legacy public `ApplyPatch(int, AssetLocation, JsonPatch, ref int, ref int, ref int)` (`:585-726`) is kept for back-compat and re-parses/re-writes the asset per patch.

The patch file schema (`JsonPatch`, `:113-180`), matched case-insensitively by Newtonsoft:

| field | type | required | notes |
|---|---|---|---|
| `op` | `EnumJsonPatchOp` | yes | `Add\|AddEach\|Remove\|Replace\|Copy\|Move\|AddMerge` (`:25-61`) |
| `file` | `AssetLocation` | yes | domain-qualified, e.g. `game:itemtypes/resource/nugget.json`; `.json` appended if absent; trailing `*` = prefix wildcard |
| `path` | string | yes | JSON pointer into the target |
| `fromPath` | string | Copy/Move only | serialized as `FromPath` in C#; the wire name is `fromPath` |
| `value` | `JsonObject` | required for Add/AddEach/Replace/AddMerge | converted by `JsonAttributesConverter` |
| `side` | `EnumAppSide?` | optional, **defaults `Universal`** (`:166`) | `SideType` is the `[Obsolete]` alias (`:154-160`) |
| `enabled` | bool | optional, default `true` | |
| `condition` | `PatchCondition` | optional | `{ when (req), isValue, useValue }` (`:67-86`) |
| `dependsOn` | `PatchModDependence[]` | optional | `[{ modid (req), invert (default false) }]` (`:92-105`) |

---

# 4. `modpeek/` — ModDB's mod-file inspector/validator (@ 5b94254, ModPeek 2.1.1, LibModPeek 2.0.0, net8.0)

```
modpeek/
├── LibModPeek/                 # the reusable library
│   ├── FromGeneric.cs          # dispatch by extension, else by magic bytes (PK\x03\x04 / MZ), else try .cs
│   ├── FromZIP.cs              # ZipArchive; needs modinfo.json at archive ROOT; optional worldconfig.json
│   ├── FromCS.cs               # Roslyn parse of [assembly: ModInfo(...)] / [assembly: ModDependency(...)]
│   ├── FromDLL.cs              # Mono.Cecil read of the same assembly attributes
│   ├── ParseJSON.cs            # hand-rolled case-insensitive modinfo/worldconfig reader (System.Text.Json)
│   ├── Validate.cs             # ValidateModInfo / ValidateWorldConfig; the version regex lives here
│   └── Errors.cs               # ~20 typed error records with Severity
├── ModPeek/Program.cs          # CLI: modpeek [-i|--idandversion] [-p|--always-print] [-f file] input
├── Tests/                      # real-world Valid/ and Defect/ mod files as fixtures
└── lib/{Mono.Cecil.0.11.5, Vintagestory/{VintagestoryAPI.dll,Newtonsoft.Json.dll,cairo-sharp…}}
```

**What it does**: given a `.zip`, `.cs` or `.dll` mod file it extracts `ModInfo` (+ `ModWorldConfiguration`), validates it, prints `Id/Name/Version/Type/Side/RequiredOnClient/RequiredOnServer/NetworkVersion/IconPath/Description/Authors/Contributors/Website/Dependencies` (or just `modid:version` with `-i`), and exits non-zero on any error. Errors are rendered by `FormatError` (`ModPeek/Program.cs:150-184`).

**Is it useful to us?** Yes, but as a *specification*, not a tool. It is the exact gate a ModDB upload passes through, so `LibModPeek/Validate.cs` and `LibModPeek/ParseJSON.cs` are the authoritative list of what our `src/*/modinfo.json` may contain and what our version strings may look like — stricter than the game itself. Running it is optional (it needs `dotnet build` and a `VintagestoryAPI.dll`); reading `Validate.cs:197` and `ParseJSON.cs:22-243` gives us the same answer for free. It is *not* useful for inspecting third-party mods' contents (it reads metadata only, not assets).

---

# 5. `Cairo/` — the `cairo-sharp` wrapper (@ b5a93a4)

```
Cairo/
├── Cairo.csproj             # AssemblyName cairo-sharp; TargetFramework $(FrameworkVersion) (built inside the game solution); PackageReference SkiaSharp 3.116.1; AllowUnsafeBlocks
├── NativeMethods.cs         # the P/Invoke surface into native libcairo
├── wrapper/                 # ~55 files: Context, Surface, ImageSurface, Pattern, Gradient(Linear/Radial),
│                            #   Matrix, Path, Color, PointD, Rectangle, FontFace/ScaledFont/FontOptions,
│                            #   enums (Antialias, Content, Extend, FillRule, Filter, Format, LineCap,
│                            #   LineJoin, Operator, Status, HintStyle, SubpixelOrder, …), plus
│                            #   platform surfaces (Win32/Xlib/Xcb/Pdf/PS/Svg/Glitz/DirectFB) VS never uses
├── util/                    # VS additions: ContextUtils, FreeType, FreeTypeFontFace,
│                            #   SurfaceDemultiplyAlpha, SurfaceDrawImage (SkiaSharp bridge), SurfaceTransformBlur
└── Svg/{NanoSvg.cs, SvgNativeMethods.cs}   # managed binding to the nanosvg native lib (Rasterizer is a WIP stub)
```

This is the Mono.Cairo binding (Ximian/Novell lineage, MIT) that Anego vendored and extended. **A mod author touches it only through the GUI layer**: 99 files across `vsapi`/`vssurvivalmod`/`vsessentialsmod` do `using Cairo`, and the entry points that matter are `vsapi/Client/UI/CairoFont.cs` (`CairoFont.WhiteDetailText()` and friends, as used in `vsmodexamples` tutorial 6) and `vsapi/Client/UI/Elements/Impl/GuiElement.cs`, where custom `GuiElement` subclasses receive a `Cairo.Context` and an `ImageSurface` in `ComposeElements`/`DrawInteractiveElements` and draw with `Rectangle`/`Fill`/`SetSourceRGBA`/`ShowText` before the surface is uploaded as a texture. `ImageSurface` is created either from a `Format`+`w`+`h` or over existing pixel data (`wrapper/ImageSurface.cs:48-60`); the VS-specific `util/SurfaceDrawImage.cs` lets you blit an `SKBitmap` onto a surface and calls `MarkDirty()`. To compile against any of it you add the `cairo-sharp` reference (`$(VINTAGE_STORY)/Lib/cairo-sharp.dll`, the `IncludeCairoSharp` template flag). Everything else in `wrapper/` — PDF/PS/SVG/X11/Win32 surfaces, `Device`, `Region` — is dead weight from the upstream binding that a mod will never instantiate.

---

# 6. `nanosvg/` — vendored NanoSVG C library (@ 754dbb0)

```
nanosvg/
├── README.md, LICENSE.txt
├── include/{nanosvg.h, nanosvgrast.h}    # the upstream single-header parser + rasterizer
├── src/{nanosvg.c, nanosvgrast.c}        # Anego's .c shims that compile the headers into a real shared lib
├── build.zig, build.zig.zon, premake5.lua # zig build (linux targets pinned to gnu/glibc 2.34); vs-mode installs straight into the game repo
└── example/{example1.c, example2.c, *.svg, stb_image_write.h}
```

Anego's only change to upstream NanoSVG is packaging: upstream ships header-only, this fork emits a standalone `nanosvg` shared library so the game can P/Invoke it. **A mod author never touches this repo directly.** The only reachable surface is the managed binding in `Cairo/Svg/SvgNativeMethods.cs` (`private const string Nsvg = "nanosvg"`, with `NsvgSize`/`NsvgViewbox` structs and `nsvgDeleteRasterizer` etc.) and `Cairo/Svg/NanoSvg.cs`, whose `Rasterizer` class is still an explicitly marked `// TODO wip //` stub that only implements `Dispose`. The practical consequence: SVG rasterization is a game-internal facility (used for GUI icons) with no stable modding API, so if we ever want vector-sourced GUI art we should pre-render to PNG rather than plan on calling into this.

## Key types

### `ModInfo`

`vsapi/Common/API/ModInfo.cs:47`

The deserialization target for modinfo.json. Every field a mod may declare, with its default. Also the type CakeBuild and modpeek both deserialize into.

[JsonRequired] Type (EnumModType, :54) and Name (:64) — a modinfo.json missing either throws. ModID (:72) optional, auto-derived via ToModID(Name) in [OnDeserialized] (:180-185). Version="" (:76). NetworkVersion=null → falls back to Version in Init() (:82,188-194). IconPath=null → game falls back to ./modicon.png (:89). BackgroundPaths (:92) is a real [JsonProperty] but is NOT in the published schema and is rejected by modpeek. Description, Website, Authors, Contributors. Side=Universal with StringEnumConverter (:127). RequiredOnClient/RequiredOnServer=true (:134,141). Dependencies uses a custom DependenciesConverter (:253-277) that reads a JSON OBJECT of {modid: versionString}, not an array. CoreMod (:149) is deliberately not a JsonProperty. TextureSize=32 (:60). Static helpers ToModID (:200) and IsValidModID (:229 — lowercase letters/digits only, must not start with a digit).

### `ModDependency`

`vsapi/Common/API/ModDependency.cs:10`

One entry of modinfo.json's `dependencies` object.

ModID and Version, both get-only. The XML doc on Version (:15-19) states it plainly: "The minimum version requirement of this dependency. May be empty if no specific version is required." A dependency is a FLOOR, never a pin — `"exlib": "0.1.0"` means 0.1.0-or-newer. Constructor validates ModID via ModInfo.IsValidModID and throws ArgumentException otherwise. ToString() renders `modid@version`. The sibling [assembly: ModDependency(modID, version)] attribute (:51) is the dll/cs-mod equivalent, AllowMultiple=true, and is superseded by modinfo.json when both exist.

### `ModInfoAttribute`

`vsapi/Common/API/ModInfoAttribute.cs:12`

Assembly-level alternative to modinfo.json for single-file .cs and .dll mods. Superseded by modinfo.json when present.

Ctor takes (name) or (name, modID). Settable: IconPath, Version, CoreMod, NetworkVersion, Description, Website, Authors[], Contributors[], Side (a STRING here, not the enum, default "Universal"), RequiredOnClient, RequiredOnServer, WorldConfig (a raw JSON string — this is how a dll-only mod ships a worldconfig). There is deliberately no Type property (:14-15): cs and dll mods are implicitly EnumModType.Code.

### `EnumJsonPatchOp`

`vsessentialsmod/Loading/JsonPatchLoader.cs:25`

The complete, closed set of `op` values a mod may write in an assets/<domain>/patches/*.json file.

Add, AddEach, Remove, Replace, Copy, Move, AddMerge — seven, and nothing else. The XML doc on Add (:28) explicitly recommends AddMerge instead "for improved mod compatability". Note Tavis also implements insert/prepend/test/append, but ModJsonPatchLoader.CreateOperation never constructs them, so they are unreachable from JSON.

### `JsonPatch`

`vsessentialsmod/Loading/JsonPatchLoader.cs:113`

One element of a patches/*.json array — the full wire schema for asset patching.

Op (:119, required), File (AssetLocation, :125, required, domain-qualified, trailing `*` = prefix wildcard), FromPath (:131, Copy/Move only — wire name `fromPath`), Path (:137, required JSON pointer), DependsOn (PatchModDependence[], :143), Enabled (:149, default true), Side (EnumAppSide?, :166, DEFAULT Universal — not null), SideType (:156, [Obsolete] alias forwarding to Side), Condition (:172), Value (JsonObject via JsonAttributesConverter, :179). Unknown keys are ignored by Newtonsoft's default MissingMemberHandling, which is why our own `"comment"` fields in assets/smex/patches/**.json are safe.

### `PatchCondition`

`vsessentialsmod/Loading/JsonPatchLoader.cs:67`

Gates a patch on a world-config value, or injects that value as the patch payload.

When (required, a world-config key), IsValue (string compared case-insensitively against attr.GetValue()+""), useValue (bool — when true the world-config value REPLACES patch.Value and IsValue is ignored, see :263-266). If the world config has no such key at all the patch is skipped silently without even incrementing the unmet-condition counter (:260-261). Vanilla's .game/*/assets/survival/patches/globmodifiers.json is the canonical useValue:true example.

### `PatchModDependence`

`vsessentialsmod/Loading/JsonPatchLoader.cs:92`

Cross-mod compatibility gate on a single patch — the mechanism our assets/smex/patches/compat/** already relies on.

modid (required) and invert (default false). Evaluated at :281-285 as `enabled = enabled && (loaded ^ dependence.invert)` over the whole array — a logical AND, so multiple entries all have to pass. `[{"modid":"em","invert":true}]` means "only when EM is absent". Membership is tested against api.ModLoader.Mods.Select(m => m.Info.ModID) (:226), i.e. loaded mods, with no version comparison available.

### `ModJsonPatchLoader`

`vsessentialsmod/Loading/JsonPatchLoader.cs:182`

The ModSystem that finds, filters and applies every mod's JSON patches. This is the wrapper the assignment asked about — it is in vsessentialsmod, NOT vsapi.

ShouldLoad → true on all sides (:190). ExecuteOrder() => 0.05 (:195), i.e. before the block/item loader at 0.2 and recipes at 1 (table in vsapi/Common/API/ModSystem.cs:50). AssetsLoaded (:197) calls ApplyPatches(). Public ApplyPatches(string forPartialPath = null) (:214) can be re-run for a subtree — vsessentialsmod/Systems/WorldGen/Standard/WgenCommands.cs:1905-1906 does exactly that (`api.Assets.Reload(AssetCategory.worldgen); patchLoader.ApplyPatches("worldgen/")`) after a /wgen regen. Private CreateOperation (:520) is the enum→Tavis-op map. Public legacy ApplyPatch(...) (:585) is kept for back-compat and re-parses/re-writes per patch. jsonCache (:188) makes patches to one file compose within a run.

### `JsonNetTargetAdapter`

`Tavis.JsonPatch/src/JsonPatch/Adaptors/JsonNetTargetAdapter.cs:8`

Where all the actual patch semantics live. Read this file, not the RFC, when reasoning about what a patch will do.

AddReplace (:73-82) — numeric last token → JArray.Insert; `-` → JArray.Add; else `token[key] = value` (plain overwrite). AddInsertPrepend (:39-71), used by AddMerge/Insert/Prepend — same array handling but for a named key it switches on the EXISTING value: JObject → Merge, JArray → append each element of value (value must be a JArray or ArgumentException, :63), default (missing or scalar) → assign. AddEach (:84-119) — both sides must be JArray, index inserts at index++, `-` appends, anything else throws. Remove (:123-134) — array element vs JProperty. Move (:136-144) — StartsWith guard then AddMerge+Remove. Copy (:153-158) — DeepClone then AddMerge. Test (:146-151) is BROKEN (compares the found value to the document root) but is unreachable from EnumJsonPatchOp.

### `JsonPointer`

`Tavis.JsonPatch/src/JsonPatch/JsonPointer.cs:10`

Parses and resolves the `path`/`fromPath` strings.

Ctor (:24) does pointer.Split('/').Skip(1) — the path MUST start with '/'; `""` produces zero tokens. Decode (:78) is Uri.UnescapeDataString then ~1→/ then ~0→~ (correct RFC order, plus a non-standard percent-decode). Find(sample, skipLast) (:34-66) indexes numerically ONLY when the node is actually a JArray (:42), which is what makes object keys like "12" work; on failure it throws PathNotFoundException carrying the traversed prefix and the parent JSON. Last / Depth / IsRoot are used by the adapter to decide insert-vs-assign.

### `AddReplaceOperation / AddMergeOperation`

`Tavis.JsonPatch/src/JsonPatch/Operations/AddReplaceOperation.cs:3`

The two 'add' flavours. AddReplaceOperation is an EMPTY subclass of AddMergeOperation — the distinction exists only so BaseTargetAdapter's type switch can route them apart.

BaseTargetAdapter.ApplyOperation (Adaptors/BaseTargetAdapter.cs:10-42) pattern-matches `case AddReplaceOperation` at :12 BEFORE `case AddMergeOperation` at :15 — reorder those two arms and every vanilla `add` patch silently becomes a merge. AddMergeOperation.Write (:15) rewrites a numeric last token to `-` on serialization, which only matters for round-tripping.

### `AssetCategory`

`vsapi/Common/Assets/AssetCategory.cs:7`

Maps the first path segment of an AssetLocation to a side. Determines which patches can possibly find their target file.

Server-only: blocktypes, itemtypes, recipes, worldgen, entities (:11,12,27,28,29). Client-only: shaders, shaderincludes, music, dialog (:21,22,24,25). Universal: lang, patches, config, worldproperties, sounds, shapes, textures (:13,15,16,17,18,19,23). SideType is read by ModJsonPatchLoader at :254 and :504 to decide whether a missing target is an Error or just a VerboseDebug. AffectsGameplay is the second ctor arg.

### `ModPeek (static partial class)`

`modpeek/LibModPeek/Validate.cs:8`

The ModDB upload validator. Its rules are stricter than the game's and are what actually gate a release.

VERSION_REGEX (:197) = ^\d{1,5}\.\d{1,4}\.\d{1,4}(?:-(?:rc|pre|dev)\.\d{1,4})?$ — applied to Version, NetworkVersion and every dependency version. ValidateModInfo (:12) also: derives ModID from Name when absent; rejects CoreMod for anything but game/creative/survival (:45-57); normalises dependency version "" or "*" to null (:180-183); rejects authors/contributors containing CR or LF; checks Website parses as a Uri and IconPath cannot escape its root. TryExtractModInfoAndWorldConfig lives in FromGeneric.cs:8 and dispatches by extension then by magic bytes. TryParseModInfoFromJsonCaseInsensitive (ParseJSON.cs:10) is the authoritative accepted-key list, with `default:` → UnexpectedProperty error at :242-243.

### `BuildContext / PackageTask (CakeBuild)`

`VSdotnetModTemplates/VSModTemplates/templates/VintageStoryMod/CakeBuild/Program.cs:27`

The official packaging convention — what a released mod zip is supposed to contain and be named.

BuildContext (:27) deserializes ../<ProjectName>/modinfo.json into ModInfo, so Version and Name(=ModID) come from modinfo, never from the csproj. ValidateJsonTask (:47) JToken.Parses every assets/**/*.json. PackageTask (:94) CleanDirectory("../Releases") — it WIPES the whole Releases folder — then copies bin/<cfg>/Mods/mod/publish/*, assets/, modinfo.json, modicon.png into Releases/<modid>/ and zips to Releases/<modid>_<version>.zip. Task chain Default→Package→Build→ValidateJson via [IsDependentOn].


---

<a id="moddb"></a>

# vsmoddb — the ModDB site and its public API

# `.compat/vintagestory/vsmoddb` — ModDB (mods.vintagestory.at)

PHP 8, no framework, no composer. ADOdb (vendored) over MySQL/MariaDB. One front controller
(`index.php`) + one flat file per page + a hand-rolled template engine (`lib/View.php`).
Vendored HEAD is `f73e881` (~2026-05). **Production is newer than this checkout** — see gotchas.

## 1. Root directory map

| Path | What it is |
|---|---|
| `index.php` | **The only entry point.** nginx rewrites everything to it (`docker/moddb.conf:33`). Routes `/api/**` before anything else (`index.php:29-39`), then a fixed switch of page prefixes (`:54-100`), then mod url-aliases (`:95`). 404 via `showErrorPage` (`:102`). |
| `home.php` | Logged-in dashboard (own mods, followed mods). |
| `list-mod.php` | `/list/mod` — the mod browser. Also serves the infinite-scroll page fragments when `?paging` is set (`:5-19`), returning HTML + an `X-Fetch-Cursor` header. |
| `list-tag.php`, `list-user.php`, `list-sponsorable.php` | Admin/moderator-only listings. |
| `show-mod.php` | `/show/mod/{assetId}` — the mod page. Builds comments tree (`:103-171`), releases (`:173-201`), recommendations (`:274`). |
| `show-user.php` | `/show/user/{userHash}` — hex user hash, not userId. |
| `edit-mod.php`, `edit-release.php`, `edit-tag.php`, `edit-profile.php` | Form pages (HTML, session-auth). |
| `edit-uploadfile.php`, `edit-deletefile.php` | JSON-returning AJAX endpoints for the edit forms (**not** under `/api`). |
| `download.php` | `/download/{fileId}[/{name}]` — counts a download then 302s to the CDN. Also accepts legacy `?fileid=`. 410 if the release is retracted (`download.php:17`). |
| `login.php` / `logout.php` | SSO against `account.vintagestory.at` / `auth.vintagestory.at`. |
| `notifications.php`, `accountsettings.php`, `moderate-user.php`, `terms.php` | Pages. |
| `updateversiontags.php` | Pulls `http://api.vintagestory.at/stable-unstable.json` and inserts new rows into `gameVersions`. |
| `cmd-updatetrending.php` | Cron. `trendingPoints = downloads(72h) + 5 * comments(72h)` (`:32`). |
| `cmd-fixmodversionscached.php` | Cron. Rebuilds the version caches for every mod. |
| `db/` | `000_tables.sql` = **current** schema baseline (camelCase, post-rename). `100..134_*.sql|.php` = migration history (older names are snake/lowercase — ignore them). `999_sampledata.sql`, `model.mwb` (MySQL Workbench). |
| `lib/` | Everything shared. See §2. |
| `templates/*.tpl` | View templates for the pages above. |
| `web/` | `_sass/`, `_ts/` (sources), `css/`, `js/` (built), `img/`, `favicon/`, `schema/` (`modinfo.v2.rc3.json`, `worldconfig.v1.dev1.json` — the JSON-Schemas the game/editors use). |
| `util/` | `modpeek.dll` + deps (Mono.Cecil, Roslyn, VintagestoryAPI.dll). Invoked out-of-process to read `modinfo.json` out of an uploaded zip/dll. |
| `tests/` | phpunit.phar + `api-v1.php` (response-shape assertions), `version-recommendations.php`, `TrimHtmlTest.php`, `prelude.php`. |
| `docker/`, `.devcontainer/` | Local stack (nginx `moddb.conf`, php-fpm, mysql, adminer). |

## 2. `lib/` map

| Path | Purpose |
|---|---|
| `lib/config.php` | Env split on `SERVER_NAME`. Prod: `CDN=bunny`, `DOWNLOAD_DEDUPLICATION_TIMESPAN=24*3600`, `MOD_SEARCH_PAGE_SIZE=200`. `DISABLE_USER_TAGS=true` by default (`:62`). |
| `lib/core.php` | 1035 lines of everything: DB connect (`:258`), HTML sanitising (`sanitizeHtml` `:184`, `trimHtml` `:637`, `postprocessCommentHtml` `:611`, `inflateLinks` `:714`), `formatDownloadTrackingUrl` (`:875`), all `HTTP_*` constants (`:901-912`), `CATEGORY_*` (`:1005-1009`), `ASSETTYPE_*` (`:1011`), `STATUS_*` (`:1014-1017`), `TAG_KIND_*` (`:998`), `MODACTION_KIND_*` (`:414`), `logAssetChanges` (`:386`). Includes `lib/user.php` and the CDN driver at the bottom (`:857-865`). |
| `lib/user.php` | **Session auth** (`:3`, `:20-22`), `NOTIFICATION_*` kinds (`:25-38`), `canEditAsset` (`:101`), `canModerate` (`:158`), `ROLE_*` (`:334-337`), `validateActionToken` (`:326`). |
| `lib/version.php` | The 64-bit version encoding. `compileSemanticVersion` (`:20`), `compilePrimaryVersion` (`:46`), `formatSemanticVersion` (`:58`), `isPreReleaseVersion` (`:77`), `VERSION_MASK_*` (`:82-89`). |
| `lib/modinfo.php` | `modpeek()` subprocess wrapper (`:13`), `deserializeModInfoArrayFields` (`:95`), `findMinCompatibleGameVersion` (`:123`). |
| `lib/edit-release.php` | `createNewRelease` (`:11`), `updateRelease` (`:65`), **`updateGameVersionsCached`** (`:139`) — rebuilds both denormalised caches. |
| `lib/search-mods.php` | The *site* search (not the API). `VALID_ORDER_BY_COLUMNS` (`:3`), `validateModSearchInputs` (`:30`), `queryModSearch` (`:198`), keyset `getNextFetchCursor` (`:413`). |
| `lib/recommend-release.php` | `selectDesiredVersions` (`:55`), `recommendReleases` (`:107`) — the "Recommended / For testers / Latest outdated" logic on the mod page. Big spec comment at `:3-44`. |
| `lib/fileupload.php` | Upload → CDN → `files` row → `modPeekResults` row (`:147-166`). |
| `lib/cdn/bunny.php` / `lib/cdn/none.php` | `formatCdnUrl`, `formatCdnUrlFromCdnPath`, `formatCdnDownloadUrl`, `uploadToCdn`, `deleteFromCdn`. Bunny download url = `{assetserver}/{cdnPath}?dl={name}` (`bunny.php:170`). |
| `lib/img.php`, `lib/file.php`, `lib/upload-limits.php`, `lib/csp.php`, `lib/View.php`, `lib/ErrorHandler.php`, `lib/notification.php`, `lib/timezones.php`, `lib/webhook-handlers.php` | Support. `UPLOAD_LIMITS` at `upload-limits.php:43` (release: 1 file, 40 MB, `dll|zip|cs`). |
| `lib/3rdparty/` | ADOdb 5, htmLawed. Never read these. |

## 3. API routing chain

```
index.php:29   urlparts[0] === 'api'  ->  shift
index.php:31     urlparts[0] === 'v2' ->  shift ; include lib/api/v2.php
index.php:36     else                 ->  include lib/api/v1/entry.php
index.php:38   exit()
```

* **v1** — `lib/api/v1/entry.php` sets `Content-Type: application/json`, defines `fail($code)` / `good($data,$code)`, then includes `functions.php` + `logic.php`. `logic.php` is one `switch($urlparts[0])`.
* **v2** — `lib/api/v2.php` defines `fail($code,$data)` (**does** set the real HTTP status), `good($data,$flags)`, `validateMethod`, `validateContentType`, the readonly gate (`:38-48`), then includes `public/_routing.php` then `authenticated/_routing.php`, then `fail(404)`.
* `public/_routing.php` dispatches `tags` / `users` / `mods`. If a `mods` sub-handler doesn't terminate, `$urlparts` is restored and execution **falls through to the authenticated router** (`public/_routing.php:15-19`).
* `authenticated/_routing.php:3-5` — `if(empty($user)) fail(401)`. Then `notifications` / `comments` / `mods` / `game-versions`.

---

## 4. API v1 — `https://mods.vintagestory.at/api/…`

Read-only, no auth, no pagination. **Every v1 response is HTTP 200**, even errors; the real code
is the `statuscode` string field in the body.

### `GET /api/tags` — `v1/logic.php:10`
```json
{"statuscode":"200","tags":[{"tagid":"467","name":"Absolute Cinema","color":"#92C96AFF"}]}
```
`tagid` is a **string**; `color` is `#RRGGBBAA` (8 hex digits, alpha last).

### `GET /api/gameversions` — `v1/logic.php:15`
```json
{"statuscode":"200","gameversions":[{"tagid":-281492156858370,"name":"1.4.4-dev.2","color":"#CCCCCC"}]}
```
`tagid` is the **negated 64-bit compiled version** (legacy shim). Ascending by version.

### `GET /api/authors[?name=…]` — `v1/logic.php:40`
```json
{"statuscode":"200","authors":[{"userid":52736,"name":"fallenstar"}]}
```
`name` does a `LIKE %…%` (first 20 chars), `LIMIT 10`, banned users excluded. **Without `name` it
dumps every user in the DB** (the test at `tests/api-v1.php:201` has to raise `memory_limit` to 10 GB).

### `GET /api/comments[/{assetId}]` — `v1/logic.php:55`
```json
{"statuscode":"200","comments":[{"commentid":215107,"assetid":53606,"userid":105404,
  "text":"<p>…</p>","created":"2026-08-10 01:22:05","lastmodified":"2026-08-10 01:22:05"}]}
```
* Path arg is the **assetId, not the modId**. Get it from `/api/mod/{x}` → `mod.assetid`.
* Without it: latest **100 comments site-wide**, ordered `lastModified DESC`.
* With it: **all** comments for that asset, no limit, still `lastModified DESC`.
* `text` is sanitised HTML (htmLawed, safe subset + youtube iframes). Deleted comments excluded.
* There is **no** username in the payload — join `userid` against `/api/authors` yourself.

### `GET /api/mods` — `v1/logic.php:29` → `v1/functions.php:148`
Returns **all published mods in one document** (7 990 mods / 3.5 MB / ~1 s as of 2026-08-10).

Query params (all optional):

| Param | Form | Handling |
|---|---|---|
| `text` | string | `asset.name LIKE %…% OR asset.text LIKE %…%` |
| `tagids[]` | **array** | AND-ed; one `EXISTS(modTags …)` per value |
| `author` | int userId | exact |
| `gameversion` | `"1.22"` or `-<int>` | matches `modCompatibleMajorGameVersionsCached` (major.minor only) |
| `gv` | `"1.22.6"` or `-<int>` | single exact version |
| `gameversions[]` | **array** | OR-ed exact versions (ignored if `gv` present) |
| `orderby` | one of `asset.created` (default) / `lastreleased` / `downloads` / `follows` / `comments` / `trendingpoints` | literal strings, whitelist at `functions.php:156` |
| `orderdirection` | `asc` / `desc` (default) | anything not `asc` ⇒ `desc` |

Element shape (`functions.php:239-256`):
```json
{"modid":9254,"assetid":53606,"downloads":12147,"follows":525,"trendingpoints":4,
 "comments":191,"name":"Steelmaking Expanded","summary":"…",
 "modidstrs":["smex"],"author":"fallenstar","urlalias":"smex","side":"both","type":"mod",
 "logo":"https://moddbcdn.vintagestory.at/….jpg","tags":["Metal","Technology"],
 "lastreleased":"2026-08-09 23:16:11"}
```
`side` ∈ `client|server|both|null`; `type` ∈ `mod|externaltool|other` (server tweaks report `mod`,
`functions.php:266-276`). `logo` may be `null`. Dates are raw MySQL `DATETIME` strings, server TZ.

### `GET /api/mod/{modId|modIdStr}` — `v1/logic.php:33` → `v1/functions.php:3`
A non-numeric arg is resolved via `modReleases.identifier` of any non-retracted release
(`functions.php:8-13`). Only `assets.statusId = 2` (published) mods resolve; otherwise `404`.

```json
{"statuscode":"200","mod":{
  "modid":9254,"assetid":53606,"name":"…","text":"<full html description>","author":"fallenstar",
  "urlalias":"smex","logofilename":"…","logofile":"…","logofiledb":"…",
  "homepageurl":"…","sourcecodeurl":"…","trailervideourl":"","issuetrackerurl":"…","wikiurl":"",
  "downloads":12147,"follows":525,"trendingpoints":4,"comments":191,
  "side":"both","type":"mod",
  "created":"2026-06-01 13:40:05","lastreleased":"2026-08-09 23:16:11","lastmodified":"2026-08-10 15:33:39",
  "tags":["Construction","Metal","Technology"],
  "releases":[{
    "releaseid":52369,
    "mainfile":"https://moddbcdn.vintagestory.at/smex_0.9.6_….zip?dl=smex_0.9.6.zip",
    "filename":"smex_0.9.6.zip","fileid":113595,"downloads":202,
    "tags":["1.22.0","1.22.1","…","1.22.6"],
    "modidstr":"smex","modversion":"0.9.6","created":"2026-08-09 23:16:11",
    "changelog":"<p>…</p>"}],
  "screenshots":[{"fileid":97352,"mainfile":"…","filename":"SE logo.jpg",
    "thumbnailfilename":"…_55_60.jpg","created":"2026-06-01 13:50:21"}]}}
```
* `releases` are ordered `created DESC`, **retracted releases are omitted entirely**.
* `release.tags` = compatible **game** versions (nothing to do with mod tags).
* `release.mainfile` is a **direct CDN url with `?dl=`** — it bypasses `/download/…` and therefore
  **does not increment the download counter**.
* `logofilename` is `@obsolete` and identical to `logofile` (`functions.php:111`).
* `lastmodified` bumps on every download-count write — useless as a content-change signal
  (comment at `functions.php:127-129`).

### `GET /api/updates?mods=a@1.0.0,b@2.3.4` — `v1/logic.php:95` → `functions.php:281`
The game client's update check. Every entry **must** carry `@version` or the whole call fails 400.
Returns only identifiers with a strictly newer non-retracted release:
```json
{"statuscode":"200","updates":{"smex":{"releaseid":52369,"mainfile":"…?dl=…","filename":"smex_0.9.6.zip",
 "fileid":113595,"downloads":202,"tags":["1.22.0",…],"modidstr":"smex","modversion":"0.9.6",
 "created":"2026-08-09 23:16:11"}}}
```
No `changelog` field here (unlike `/api/mod`). **Ignores game-version compatibility entirely** —
it only compares mod versions.

### `GET /api/changelogs` — `v1/logic.php:84`
Permanently retired. HTTP 200, `statuscode:"410"`, `Cache-Control: max-age=604800, immutable`,
one placeholder row explaining it's gone.

---

## 5. API v2 — `https://mods.vintagestory.at/api/v2/…`

Real HTTP status codes. Public subset = `mods/install-information`, `mods/{id}/releases…`,
`tags/by-name`, `users/by-name`. Everything else needs the session cookie.

### `GET /api/v2/mods/install-information` — `public/mods.php:18`
The launcher/one-click-install endpoint.

| Param | Meaning |
|---|---|
| `ids` | **required**, comma list of `{identifier}[@{version}]`. `@version` optional only when `gv` is given. |
| `gv` | semver game version, e.g. `1.22.6`. Enables upgrade recommendation. |
| `ignore-retractions` | truthy ⇒ still return the file for a retracted release (unless force-retracted). **Note the plural — README says singular and is wrong.** |
| `hosted-mode` | truthy ⇒ every id gets `errorCode 4031`, nothing else is queried (`:58-67`). |

```json
{"data":{
  "smex":{"recommendedUpgrade":"0.9.6","fileName":"smex_0.9.4.zip","fileUrl":"/download/102250/smex_0.9.4.zip"},
  "nope":{"errorCode":4041}}}
```
Error codes (`public/mods.php:3-9`): `4001` spec parse failed · `4002` no version and no `gv` ·
`4031` forbidden in hosted mode · `4032` cannot ignore retraction · `4041` spec not found ·
`4101` retracted · `4102` force-retracted (retracted by a moderator who isn't the owner).
`fileUrl` is a **tracked** `/download/{fileId}/{name}` path (relative).

### `GET /api/v2/mods/{modId}/releases` — `public/mods.php:244`
`?ignore-retractions=1` to include ignorable retractions. Map keyed by releaseId, `version DESC`:
```json
{"52369":{"identifier":"smex","version":"0.9.6"},
 "46656":{"identifier":"smex","version":"0.9.4"},
 "44495":{"identifier":"smex","version":"0.8.3","retractionReason":"<p>…</p>"}}
```
404 `{"error":"Mod not found or not released."}` if the mod isn't `statusId = 2`.

### `GET /api/v2/mods/{modId}/releases/{releaseId}` — `public/mods.php:307`
### `GET /api/v2/mods/{modId}/releases/latest[?identifier=…][&ignore-retractions=1]` — `public/mods.php:295`
```json
{"releaseId":52369,"identifier":"smex","version":"0.9.6",
 "compatibleGameVersions":["1.22.6","1.22.5","1.22.4","1.22.3","1.22.2","1.22.1","1.22.0"],
 "created":1786317371,
 "fileName":"smex_0.9.6.zip","fileUrl":"/download/113595/smex_0.9.6.zip"}
```
`created` is a **unix timestamp int** here (v1 uses a datetime string). `compatibleGameVersions`
descending. `retractionReason` present only when retracted; `fileName`/`fileUrl` are then omitted
unless the retraction is ignorable. `fileUrl` is `null` when the release has no attached file.

`/releases/all` and `/releases/new` are **not implemented** — `all` falls into the releaseId branch
and 400s with `{"error":"Malformed releaseId."}`.

### `GET /api/v2/tags/by-name/{search}?limit=N` — `public/tags.php:8`
### `GET /api/v2/users/by-name/{search}?limit=N[&contributors-only=1]` — `public/users.php:8`
`limit` default 10, max 200 (else 400). Exact match `UNION` `LIKE %…%`.
```json
{"4":"Technology"}                       // tagId -> name
{"1907F17CC43B88830C72":"fallenstar"}    // 20-hex user HASH -> name  (NOT userId)
```

### Authenticated (cookie `vs_websessionkey`; mutations also need `at=`)

| Endpoint | Method | Source |
|---|---|---|
| `/api/v2/game-versions` | GET (**401 without a session — known bug, README:330**), POST (admin) | `authenticated/game-versions.php:9`, `:14` |
| `/api/v2/game-versions/{version}` | DELETE (admin) | `game-versions.php:54` |
| `/api/v2/notifications` | GET → array of unread notification ids | `notifications.php:6` |
| `/api/v2/notifications/clear` | POST `ids=1,2,3` or `ids[]=` | `notifications.php:14` |
| `/api/v2/notifications/settings/followed-mods/{modId}` | POST `new=<flags>`; bit0 = notify on release | `notifications.php:33` |
| `…/followed-mods/{modId}/unfollow` | POST | `notifications.php:61` |
| `/api/v2/mods/{modId}/comments` | GET → **404 not implemented**; PUT body = comment HTML, `?response-to={commentId}` | `authenticated/mods.php:11` |
| `/api/v2/mods/{modId}/lock` | POST `reason=` (moderator) | `authenticated/mods.php:111` |
| `/api/v2/mods/{modId}/releases/upload-limit` | GET / PUT `limit=` (moderator) | `authenticated/mods.php:148` |
| `/api/v2/mods/{modId}/releases/{releaseId}/retraction` | PUT `reason=` | `authenticated/mods.php:202` |
| `/api/v2/mods/{modId}/tags` | POST `tags[]=` (**disabled**: `DISABLE_USER_TAGS=true`, 503) | `authenticated/mods.php:287` |
| `/api/v2/mods/{modId}/tags/{tagId}/vote` | PUT `vote=-1|0|1` (same 503) | `authenticated/mods.php:360` |
| `/api/v2/comments/{commentId}` | POST body = HTML (edit) / DELETE | `authenticated/comments.php:13`, `:69` |

### Non-`/api` machine endpoints
* `POST /webhooks/game-tag` — body = version string, header `X-Secret: …`. Adds a game version
  (`lib/webhook-handlers.php:9`).
* `POST /edit-uploadfile`, `POST /edit-deletefile` — JSON, session-auth, form-internal.
* `GET /list/mod?paging=1&…` — HTML fragments + `X-Fetch-Cursor` header (`list-mod.php:5-19`).

---

## 6. Data model (`db/000_tables.sql`)

**The polymorphic core.** Both a mod and a release are an `assets` row.

* `assets` (`:10`) — `assetId`, `assetTypeId` (1 = mod, 2 = release, `core.php:1011`), `statusId`
  (1 draft / 2 published / 4 locked, `core.php:1014`), `createdByUserId` (= the "author"),
  `name` (mod title; releases leave it null), `text` (mod description **or** release changelog).
* `mods` (`:192`) — `modId`, `assetId` (FK), `urlAlias`, `summary` (100 chars),
  `descriptionSearchable`, denormalised counters `downloads`/`follows`/`comments`/`trendingPoints`,
  `side` enum `client|server|both`, `category` TINYINT, `cardLogoFileId`/`embedLogoFileId`,
  `homepageUrl`/`sourceCodeUrl`/`issueTrackerUrl`/`wikiUrl`/`trailerVideoUrl`/`donateUrl`,
  `lastReleased`, `uploadLimitOverwrite`.
  `category`: 1 = game mod, 2 = external tool, 3 = other, 129 = server tweak (`GAME_MOD | 1<<7`).
* `modReleases` (`:253`) — `releaseId`, `modId`, `assetId` (UNIQUE), `identifier` (the
  `modinfo.json` modId string, nullable for tools), `version` BIGINT UNSIGNED (compiled),
  `created`. UNIQUE `(modId, identifier, version)` — **one mod may publish several identifiers**.
* `modReleaseRetractions` (`:273`) — presence = retracted. `reason` HTML, `lastModifiedBy`.
  Split out of `modReleases.retractionReason` by migration `128_migrate.sql`.
* `files` (`:72`) — `fileId`, `assetId` (nullable while "hovering" pre-attach), `assetTypeId`,
  `userId`, `downloads`, `name`, `cdnPath`, `order`. The release zip and every screenshot live here.
* `fileImageData` (`:92`) — `hasThumbnail`, `size` POINT.
* `modPeekResults` (`:102`) — **one row per uploaded release file**, the parsed `modinfo.json`:
  `modIdentifier`, `modVersion`, `type` (`Theme|Content|Code`), `side`, `requiredOnClient/Server`,
  `networkVersion`, `description`, `iconPath`, `website`, `rawAuthors`, `rawContributors`,
  **`rawDependencies`**, `errors`.
* `fileDownloadTracking` (`:297`) — `(ipAddress INET6, fileId, lastDownload)`; dedup window.
* `gameVersions` (`:321`) — `version` (compiled) + `sortIndex` (dense ascending rank).
* `modReleaseCompatibleGameVersions` (`:328`) — `(releaseId, gameVersion)`, the real m:n.
* `modCompatibleGameVersionsCached` (`:336`) / `modCompatibleMajorGameVersionsCached` (`:345`) —
  denormalised per-mod caches for search, rebuilt by `updateGameVersionsCached`
  (`lib/edit-release.php:139`). Exclude retracted releases.
* `tags` (`:178`) — `tagId`, `kind` (2 predefined / 3 user-defined), `name` UNIQUE, `color` INT.
  `modTags` (`:227`) `(modId, tagId, votes)`; `modTagVotes` (`:239`) per-user ±1.
* `comments` (`:141`) — `commentId`, **`assetId` (the mod's asset)**, `responseTo`,
  `conversationRoot`, `responseDepth`, `userId`, `text` HTML, `textShort`, `deleted`.
  Deliberately **no FK on assetId** (`:156 :NoCommentAssetFK`).
* `users` (`:29`) — `userId`, `hash` BINARY(10) (the public 20-hex id used in URLs), `uid`,
  `roleId` (1 admin / 2 moderator / 3 player / 4 player-no-comment), `actionToken` BINARY(8),
  `sessionToken` BINARY(32), `sessionValidUntil`, `bannedUntil`, `bio`.
* `changelogs` (`:164`) — audit trail of edits (`logAssetChanges`), coalesced within 5 minutes.
* `notifications` (`:307`), `userFollowedMods` (`:354`), `modTeamMembers` (`:366`),
  `moderationRecords` (`:53`), `roles` (`:286`), `status` (`:132`).

### Version encoding — `lib/version.php`
```
64 bits: [major:16][minor:16][patch:16][suffix:16]
suffix  = 0xffff                      for a plain release (sorts after all pre-releases)
        = kind<<12 | number           kind 4=dev, 8=pre, 12=rc
"1.22.5"        -> 281569466384383
"1.22" (primary)-> (1<<48)|(22<<32)   suffix 0, so it sorts before every pre-release of 1.22.x
```
`compilePrimaryVersion` returns `false` for a 3-part string, and `compileSemanticVersion` returns
`false` for a 2-part string — they are **not** interchangeable.

### Releases ↔ game versions
Compat is an explicit author-curated list, not a range. On upload, `findMinCompatibleGameVersion`
reads the `game@X` entry from the modinfo dependencies and **pre-ticks every known game version
≥ X** in the form (`edit-release.php:308-315`); the author can then tick/untick freely
(`lib/edit-release.php:36`, `:111`). Nothing enforces consistency with the shipped modinfo
afterwards. A game version only exists once it is in `gameVersions` (added by
`updateversiontags.php`, `POST /api/v2/game-versions`, or `POST /webhooks/game-tag`).

### How `modinfo.json` `dependencies` is surfaced
1. Upload → `modpeek(...)` runs `dotnet util/modpeek.dll -p <file>` and prints `Key: value` lines
   (`lib/modinfo.php:17`, parsed `:65-87`). `Dependencies: game@1.20.0, exlib@0.7.0`; an
   unversioned/`*` dependency is emitted **without** `@`.
2. Stored **verbatim** as `modPeekResults.rawDependencies` TEXT. The normalised
   `releaseFileDependencies` table exists only as a commented-out block (`db/000_tables.sql:124-130`).
3. `deserializeModInfoArrayFields` (`lib/modinfo.php:95`) turns it into `id => compiledVersion`,
   with **0 meaning "any"**. The value is a **MINIMUM**, not a pin — explicit note at
   `lib/modinfo.php:3-6`.
4. Its only functional consumer is `findMinCompatibleGameVersion` (`:123`) → the game-version
   pre-tick. `lib/fileupload.php:153` returns it to the upload AJAX as `gameversiondep`.
5. **No API endpoint of either version exposes dependencies.** If you need the dependency graph of
   a published mod you must download the zip and read `modinfo.json` yourself.

### Auth & limits
* **Auth = cookie `vs_websessionkey`** (base64 session token) matched against
  `users.sessionToken` with `sessionValidUntil > NOW()` (`lib/user.php:3`, `:20-22`).
  Issued for 14 days by `login.php:43`, which validates the token against
  `https://auth.vintagestory.at/webprofile`. There is **no API key, no bearer token, no OAuth**.
* **CSRF `at`** — hex of `users.actionToken`, GET or POST, required by every mutating v2 endpoint
  (`authenticated/_routing.php:16-20`). Rotated on each login.
* **Rate limits: none.** No 429 anywhere in the codebase, no nginx `limit_req`. The only throttles
  are (a) download de-duplication per `(fileId, ip)` for 24 h (`lib/config.php:57`,
  `download.php:24`) and (b) `DB_READONLY` mode → 503 + `Retry-After: 1800` for non-GET
  (`lib/api/v2.php:38-48`).
* **No CORS headers** are emitted anywhere — browser-side cross-origin use is impossible.
* `index.php:12-14`: a GET carrying an `Accept` header that contains neither `text/html` nor
  `application/json` and isn't `*/*` gets the plain-text body `not an image` with HTTP 200.

---

## 7. curl cookbook (verified against production 2026-08-10)

Our published mods: **smex** modId `9254` / assetId `53606`, **ppex** modId `9568` / assetId `55302`,
**exlib** modId `9564` / assetId `55292`. (`iwex`, `hpex`, `lpex` are not published.)

```bash
# --- all our mods: version, downloads, follows, comments, last release ---
curl -s "https://mods.vintagestory.at/api/mods?author=52736&orderby=downloads" \
| jq -r '.mods[] | [.modid, (.modidstrs|join(",")), .downloads, .follows, .comments, .lastreleased] | @tsv'

# --- our author id, if it ever changes ---
curl -s "https://mods.vintagestory.at/api/authors?name=fallenstar"

# --- latest published version of one mod (+ its game-version compat) ---
curl -s "https://mods.vintagestory.at/api/v2/mods/9254/releases/latest" | jq

# --- full release history, newest first ---
curl -s "https://mods.vintagestory.at/api/v2/mods/9254/releases" \
| jq -r 'to_entries[] | [.key, .value.version, (.value.retractionReason // "-")] | @tsv'

# --- per-release download counts (only v1 exposes these) ---
curl -s "https://mods.vintagestory.at/api/mod/smex" \
| jq -r '.mod.releases[] | [.modversion, .downloads, .created, (.tags|join(","))] | @tsv'

# --- are we shipping the latest? (what the game client asks) ---
curl -s "https://mods.vintagestory.at/api/updates?mods=smex@0.9.6,exlib@0.7.1,ppex@0.9.6" | jq '.updates'
# empty object == everything is current

# --- what a 1.22.6 client would install, given what it already has ---
curl -s "https://mods.vintagestory.at/api/v2/mods/install-information?ids=smex@0.9.4,exlib@0.7.0&gv=1.22.6" | jq

# --- newest player comments on smex (assetId, NOT modId) ---
curl -s "https://mods.vintagestory.at/api/comments/53606" \
| jq -r '.comments[:20][] | [.created, .userid, (.text|gsub("<[^>]*>";"")|.[0:140])] | @tsv'

# --- comment count deltas across our three mods ---
for a in 53606 55302 55292; do
  printf "%s\t%s\n" "$a" "$(curl -s "https://mods.vintagestory.at/api/comments/$a" | jq '.comments|length')"
done

# --- resolve a commenter's name (v1 gives only userid) ---
curl -s "https://mods.vintagestory.at/api/authors" | jq -r '.authors[] | select(.userid==105404) | .name'

# --- which game versions exist right now ---
curl -s "https://mods.vintagestory.at/api/gameversions" | jq -r '.gameversions[-15:][].name'

# --- competitors / prior art on a tag ---
curl -s "https://mods.vintagestory.at/api/v2/tags/by-name/Technology"      # -> {"4":"Technology"}
curl -s "https://mods.vintagestory.at/api/mods?tagids%5B%5D=4&gv=1.22.6&orderby=downloads" \
| jq -r '.mods[:20][] | [.name, .downloads] | @tsv'
```
Always send `-H 'Accept: application/json'` or nothing at all; a wrong `Accept` yields the string
`not an image`. Cache aggressively — `/api/mods` alone is 3.5 MB.

## Key types

### `index.php (front controller / API router)`

`vsmoddb/index.php:29`

The single entry point. Decides v1 vs v2 API vs HTML page vs mod url-alias.

`/api/**` is handled before CSP, view init and mod-alias lookup, so no page code runs for API calls. `api/v2/...` -> lib/api/v2.php; anything else under `/api` -> lib/api/v1/entry.php. Lines 12-14 reject GETs whose Accept header is neither text/html, application/json nor */* with the literal body `not an image` at HTTP 200. Mod url-aliases are resolved LAST (`:94`) so a mod cannot shadow `api`, `show`, `edit`, `download`, …

### `lib/api/v1/logic.php (v1 dispatcher)`

`vsmoddb/lib/api/v1/logic.php:9`

One switch implementing the whole public read-only v1 API: tags, gameversions, mods, mod, authors, comments, changelogs, updates.

`comments` takes an ASSET id (`:59`), not a modId, and returns the latest 100 site-wide when omitted. `changelogs` is permanently 410 (`:84`). Falls through to `fail("400")` at `:114` for anything unrecognised. Every response is HTTP 200 — the code lives only in the `statuscode` body field (fail/good defined at `lib/api/v1/entry.php:8` and `:14`, neither calls http_response_code).

### `listMods() / listMod() (v1 payload builders)`

`vsmoddb/lib/api/v1/functions.php:148`

Build the `/api/mods` list and `/api/mod/{id}` detail documents.

`listMods` whitelist for `orderby` at `:156` (literal `asset.created`, `lastreleased`, `downloads`, `follows`, `comments`, `trendingpoints`). No LIMIT — returns every published mod (7 990 / 3.5 MB). `listMod` (`:3`) resolves a non-numeric arg through `modReleases.identifier`, requires `assets.statusId = 2`, drops retracted releases (`:55`), and returns `release.mainfile` as a raw `?dl=` CDN url that does NOT count as a download. `mapCategoryToType` (`:266`) collapses server tweaks into `"mod"`.

### `lib/api/v2.php (v2 kernel)`

`vsmoddb/lib/api/v2.php:7`

Defines fail/good/validateMethod/validateContentType and chains the public then authenticated routers.

`fail($code,$data)` DOES set the real HTTP status (`:10`) — unlike v1. `validateMethod` emits an `Allow:` header + 405. Readonly gate at `:38-48` answers every non-GET with 503 + `Retry-After: 1800`. Public routes are tried first, then `authenticated/_routing.php`, then a final `fail(HTTP_NOT_FOUND)`.

### `lib/api/public/mods.php (the only public v2 mod endpoints)`

`vsmoddb/lib/api/public/mods.php:17`

`mods/install-information` plus `mods/{modId}/releases[…]`.

Error-code constants at `:3-9` (4001/4002/4031/4032/4041/4101/4102). `install-information` reads `ignore-retractions` (PLURAL, `:27`) and `hosted-mode` (`:28`). The releases list uses `getAssoc` so the JSON is keyed by releaseId (`:273`, `:289` with JSON_FORCE_OBJECT). `latest` accepts `?identifier=` (`:298`). `created` is emitted as a UNIX_TIMESTAMP int (`:315`), unlike v1's datetime string. Non-GET methods deliberately fall through to the authenticated router.

### `lib/api/authenticated/_routing.php (auth gate)`

`vsmoddb/lib/api/authenticated/_routing.php:3`

401s anything that reaches it without a session, then routes notifications/comments/mods/game-versions.

`validateActionTokenAPI()` (`:16`) compares `$_REQUEST['at']` against `users.actionToken` — GET or POST both work. `validateUserNotBanned()` (`:9`). Because `game-versions` is registered here, `GET /api/v2/game-versions` needs a session even though it is pure read data (acknowledged bug, README:330).

### `lib/version.php (the version codec)`

`vsmoddb/lib/version.php:20`

Encode/decode the 64-bit sortable version integer used for both mod and game versions everywhere in the DB.

`compileSemanticVersion` regex `^(\d+)\.(\d+)\.(\d+)(?:-(dev|pre|rc)\.(\d+))?$` — returns false for anything else, including 2-part strings and `+metadata`. Non-prerelease suffix is 0xffff so releases sort after their pre-releases. `compilePrimaryVersion` (`:46`) takes ONLY `major.minor` and uses suffix 0. `formatSemanticVersion` (`:58`) is the inverse. `VERSION_MASK_*` at `:82-89`.

### `lib/modinfo.php (modinfo.json ingestion)`

`vsmoddb/lib/modinfo.php:13`

Runs util/modpeek.dll over an uploaded release file and parses its `Key: value` output; the only place modinfo data enters the system.

`modpeek()` shells out to `dotnet util/modpeek.dll -p <file>` and polls proc_get_status in a 10 ms sleep loop (`:31-41`) because PHP's proc_close exit code is unreliable. `deserializeModInfoArrayFields` (`:95`) splits `id@version` and stores 0 for "any" — the header comment at `:3-6` states dependencies are MINIMUMS, not pins. `findMinCompatibleGameVersion` (`:123`) extracts the `game@X` dependency; it is the ONLY functional use of dependencies anywhere.

### `lib/edit-release.php (release lifecycle + version caches)`

`vsmoddb/lib/edit-release.php:139`

createNewRelease / updateRelease / updateGameVersionsCached.

`updateGameVersionsCached($modId)` deletes and re-inserts both `modCompatibleGameVersionsCached` and `modCompatibleMajorGameVersionsCached`, excluding retracted releases (`:156`, `:165`). The major cache masks with `0xffffffff00000000`. Compat rows are only written when `category & CATEGORY__MASK === CATEGORY_GAME_MOD` (`:33`, `:75`) — tool/other mods have no game-version rows at all.

### `lib/user.php (session auth + permissions)`

`vsmoddb/lib/user.php:3`

Resolves $user from the `vs_websessionkey` cookie; defines roles, notification kinds and the canEdit/canModerate predicates.

`$_COOKIE['vs_websessionkey']` -> `WHERE sessionToken = FROM_BASE64(?) AND sessionValidUntil > NOW()` (`:21`). No API key mechanism exists. `ROLE_ADMIN=1 / ROLE_MODERATOR=2 / ROLE_PLAYER=3` (`:334`). `NOTIFICATION_*` kinds `:25-38`. `canEditAsset` (`:101`) also grants mod-team members with canEdit.

### `lib/core.php (shared kernel)`

`vsmoddb/lib/core.php:875`

URL/HTML/date helpers, all shared constants, DB connection.

`formatDownloadTrackingUrl($file)` = `/download/{fileId}/{urlencode(name)}` — the counted download path (`:875`). `HTTP_*` `:901-912`. `CATEGORY_GAME_MOD=1 / EXTERNAL_TOOL=2 / OTHER=3 / SERVER_TWEAK=1|(1<<7)=129`, `CATEGORY__MASK=0b01111111` (`:1003-1009`). `ASSETTYPE_MOD=1 / ASSETTYPE_RELEASE=2` (`:1011`). `STATUS_DRAFT=1 / RELEASED=2 / LOCKED=4` (`:1014`). `sanitizeHtml` (`:184`) = htmLawed safe mode, iframes restricted to youtube embeds only (`:234-246`).

### `db/000_tables.sql (current schema)`

`vsmoddb/db/000_tables.sql:10`

The authoritative, already-migrated schema. Read this, not the 100..134 migration files.

Polymorphic `assets` (`:10`) shared by mods (`:192`) and releases (`:253`). `modReleases` UNIQUE `(modId, identifier, version)` at `:265` — one mod can host several identifiers. `modReleaseRetractions` (`:273`) is presence-based. `modPeekResults.rawDependencies` (`:117`) is unparsed TEXT; the normalised dependency table is commented out at `:124-130`. `comments.assetId` has deliberately NO foreign key (`:156`).

### `lib/recommend-release.php (which release the site suggests)`

`vsmoddb/lib/recommend-release.php:107`

Picks the Recommended / For-testers / Latest-outdated releases shown on a mod page.

The full ruleset is spelled out in the comment block at `:3-44`. `selectDesiredVersions` (`:55`) caps the target to the highest STABLE game version when the user didn't search for one, so a game pre-release does not mark every mod outdated (`:86-90`). Not reachable through any API — v2 `install-information`'s `recommendedUpgrade` uses a completely separate SQL self-join (`public/mods.php:99-118`).

### `lib/search-mods.php (site search, mirrors nothing in the API)`

`vsmoddb/lib/search-mods.php:30`

The /list/mod browser's filtering, keyset pagination and relevance ranking.

`VALID_ORDER_BY_COLUMNS` (`:3`) uses DIFFERENT keys from the v1 API (`trendingPoints`, `lastReleased`, `created`, `name` vs the API's `asset.created`, `lastreleased`, …). Relevance score formula and its bit-twiddling explanation at `:217-247`. Keyset cursor via `?cursor[]=val&cursor[]=modId&cursor[]=score` (`:413`). This is the only paginated mod query in the codebase — the API has none.

### `README.md (official API docs — partly wrong)`

`vsmoddb/README.md:6`

The published API documentation.

v1 section `:29-73`, v2 section `:75-347`. Documents `ignore-retraction` where the code reads `ignore-retractions`; documents the error field as absent while live responses use `error` and this checkout uses `reason`; lists `/releases/all` and `/releases/new` as `400: Not implemented` when `all` actually 400s as "Malformed releaseId". Treat it as a starting index, verify against the source.
