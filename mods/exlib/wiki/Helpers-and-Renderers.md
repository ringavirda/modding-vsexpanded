# Helpers & Renderers

A grab-bag of static helpers and a renderer base, all under `Helpers/` (the renderers sit in its
`Rendering/` subfolder), shared across the family so you don't rebuild rotation math,
particle/sound catalogues, inventory counting or fluid surfaces inline. Everything here is reusable
from a consuming mod.

## `ExOrientation` - rotation math

Single source of truth for horizontal rotation. Use these instead of hand-rolled angle switches.

```csharp
int angle = ExOrientation.AngleFromSide(Variant["side"]);
BlockPos port = ExOrientation.GlobalPos(Pos, 0, 0, 1, angle);   // one step "north" in the block's own frame
```

```csharp
public static class ExOrientation
{
    public static int AngleFromSide(string? side);                 // north 0, west 90, south 180, east 270
    public static Vec3i RotateOffset(Vec3i off, int angle);        // rotate a structure-local offset (Y untouched)
    public static Vec3i RotateOffset(int x, int y, int z, int angle);
    public static BlockPos GlobalPos(BlockPos origin, int localX, int localY, int localZ, int angle);
    public static Vec3i ReadOffset(JsonObject? node, Vec3i fallback);     // read { x, y, z } from JSON
    public static Vec3d ReadOffsetD(JsonObject? node, Vec3d fallback);    // double-precision (particle anchors)
    public static BlockPos WorldPosFromAttr(BlockPos origin, JsonObject? node, Vec3i fallback, int angle);
    public static Cuboidf[] RotateBoxes(Cuboidf[] boxes, int angle);     // rotate collision/selection boxes around centre
    public static BlockFacing RotateFacing(BlockFacing baseFace, int angle);
    public static void RotateAroundCenter(ref float x, ref float z, int angle, float center = 0.5f);
}
```

`WorldPosFromAttr` is the canonical "JSON offset -> world position": `origin + RotateOffset(ReadOffset(node), angle)`.
`RotateBoxes` returns copies (unchanged for angle 0), so cache the result rather than recomputing
each frame. `AllowedOrientations`-style lookups should likewise be cached props.

`ExOrientation.SegmentedCode` splits a block code's path on `-` so a caller can index, rewrite and
rejoin one dash-segment at a time - the shared basis for a multiblock layout finding which segments
of a code name an orientation, and for rotating them to a placed structure's angle.

## `ExMeshCache` - mesh and mesh-ref cache

Beyond the plain `MeshData` cache (`GetOrCreate`), `GetOrCreateRef(capi, group, key, build)` caches
the uploaded GPU `MultiTextureMeshRef` instead: the first call per key uploads, later calls reuse.
Refs are grouped so `DisposeGroup(api, group)` can dispose and drop every ref a blocktype has ever
uploaded in one call from `OnUnloaded`, rather than each block hand-rolling its own `ObjectCache`
dictionary and disposal loop.

```csharp
renderinfo.ModelRef = ExMeshCache.GetOrCreateRef(
  capi, "moltenBarrelMeshRefs:" + Code, key, () => GenMeshWithContent(capi, metal, fillRatio, glow)
);
```

## `ExHighlightSlots` - highlight slot ids

`Reserve(key)` hands out a `world.HighlightBlocks` slot id: the same id for the same key on every
call, a fresh one the first time. Use it instead of picking a slot literal by hand, so two features
can never collide on one.

```csharp
private static readonly int HighlightSlotId = ExHighlightSlots.Reserve("yourmod:network-highlight");
```

## `ExParticles` - particle catalogue

Named colour presets plus a configurable core and high-level effect helpers - don't build
`SimpleParticleProperties` inline.

```csharp
ExParticles.WaterJet(Api.World, Pos, outFace);
```

```csharp
public static class ExParticles
{
    // Colour presets (int ARGB):
    public static readonly int Vapor, Exhaust, GasLeakTint, Water, Smoke, GlowSpark, Dust, AirTint;

    public static void Spawn(IWorldAccessor world, int color, Vec3d minPos, Vec3d maxPos,
        Vec3f minVelocity, Vec3f maxVelocity, float minQuantity, float maxQuantity,
        float lifeLength, float gravityEffect, float minSize, float maxSize,
        EnumParticleModel model = EnumParticleModel.Quad,
        EvolvingNatFloat? opacityEvolve = null, EvolvingNatFloat? sizeEvolve = null,
        bool shouldDieInLiquid = false);

    public static Vec3d FaceCenter(BlockPos pos, BlockFacing face);
    public static Vec3f OutVel(BlockFacing face, float speed, float spread);
    public static int? GasColor(string gasType, bool ventAir = true);

    // High-level effects:
    public static void RisingPlume(/* box bounds + timing */);
    public static void ChimneySmoke(IWorldAccessor world, BlockPos chimneyPos, string gasType);
    public static void SteamPlume(IWorldAccessor world, BlockPos cell, int count);
    public static void SteamPuff(IWorldAccessor world, Vec3d pos, int count);
    public static void AirInhale(IWorldAccessor world, Vec3d mouth, int count);
    public static void SmokeCloud(IWorldAccessor world, Vec3d pos, int count);
    public static void GasLeak(IWorldAccessor world, BlockPos pos, BlockFacing face, float intensity = 1f);
    public static void GasVent(IWorldAccessor world, BlockPos pos, BlockFacing face, string gasType);
    public static void WaterJet(IWorldAccessor world, BlockPos pos, BlockFacing face, float intensity = 1f);
    public static void WaterSpill(IWorldAccessor world, BlockPos cell);
    public static void FallingDust(IWorldAccessor world, BlockPos pos);
}
```

## `ExSounds` - sound catalogue

`AssetLocation` constants for every sound the family reuses (many repurposed vanilla sounds), plus
play helpers with the side-gating done right.

```csharp
ExSounds.Play(world.Api, blockSel.Position, ExSounds.Ingot, 0.7f);
```

```csharp
public static class ExSounds
{
    // Constants (AssetLocation), grouped: molten/heat (Sizzle, MoltenMetal, PourMetal, Embers, Fire,
    // Extinguish, Ignite), mechanical (Latch, Bellows, Ingot, AnvilHit, Build, StoneCrush, ToggleSwitch,
    // CokeOvenDoorOpen/Close, ...), fluids/venting (SmallSplash, WaterPour, Watering, ExtinguishHiss),
    // steam ambience (Cooking, Lava, Creek, MetalGrinding, Swoosh, PlanetaryGears, *Explosion, ...).

    public static void Play(ICoreAPI? api, BlockPos pos, AssetLocation sound, float volume = 1f, float range = 24f);  // server only
    public static void PlayThrottled(ICoreAPI? api, BlockPos pos, AssetLocation sound, ref long lastMs, long intervalMs, float volume = 1f, float range = 24f);
    public static void PlayLocal(IWorldAccessor world, BlockPos pos, AssetLocation sound, float volume = 1f, float range = 16f, bool randomizePitch = true);  // client-safe, no side gate
    public static void PlayLoop(IWorldAccessor world, BlockPos pos, AssetLocation sound, ref long lastMs, long intervalMs, float volume = 1f, float range = 16f);  // client-safe
    public static void PlayAt(IWorldAccessor world, BlockPos pos, AssetLocation sound, IPlayer? byPlayer = null, bool randomizePitch = true, float range = 32f, float volume = 1f);
    public static void PlayChance(IWorldAccessor world, BlockPos pos, AssetLocation sound, double chance, bool randomizePitch = true, float range = 32f, float volume = 1f);
    public static ILoadedSound? CreateLoop(ICoreAPI? api, BlockPos pos, AssetLocation sound, float volume = 1f, float range = 16f, float pitch = 1f);  // client only, gapless loop
    public static void SplashSound(IWorldAccessor world, BlockPos pos);   // quiet splash ~30% of the time
    public static void HissSound(IWorldAccessor world, BlockPos pos);     // soft steam/gas hiss ~30% of the time
}
```

`Play*` (server) vs `PlayLocal`/`PlayLoop` (no side gate, client-safe) is the distinction to get
right; `CreateLoop` returns the `ILoadedSound` for you to start/stop/dispose.

## `ExInventory` - counting & consuming items

```csharp
int had = ExInventory.Count(byPlayer, stack => Matches(stack, r.Codes));
```

```csharp
public static class ExInventory
{
    public static int Count(IPlayer player, Func<ItemStack, bool> matches);            // all inventories
    public static int Take(IPlayer player, Func<ItemStack, bool> matches, int quantity);
    public static int CountHotbar(IPlayer player, Func<ItemStack, bool> matches);      // hotbar only
    public static int TakeHotbar(IPlayer player, Func<ItemStack, bool> matches, int quantity);
}
```

`Take`/`TakeHotbar` remove up to `quantity` matching items and return how many were actually taken
- useful for machine build/operation costs.

## Finding block entities

`accessor.BlockEntity<T>(pos)` is `GetBlockEntity(pos) is X be` with the null guard already done:

```csharp
public static class ExBlockAccess
{
    public static T? BlockEntity<T>(this IBlockAccessor accessor, BlockPos pos) where T : class;
    public static bool TryGetBlockEntity<T>(this IBlockAccessor accessor, BlockPos pos, [NotNullWhen(true)] out T? be) where T : class;
    public static T? Neighbour<T>(this IBlockAccessor accessor, BlockPos pos, BlockFacing facing) where T : class;
    public static IEnumerable<(BlockFacing Facing, T Entity)> Neighbours<T>(this IBlockAccessor accessor, BlockPos pos, IEnumerable<BlockFacing>? facings = null) where T : class;
}
```

`T` can be an interface as well as a concrete type, so a port asks for `IMyPort` rather than a
specific block entity class. `Neighbours` walks `BlockFacing.ALLFACES` by default, or a subset such
as `BlockFacing.HORIZONTALS`, and yields only the faces that actually carry a match - so replacing a
hand-rolled `foreach (var face in BlockFacing.HORIZONTALS) { ... GetBlockEntity(pos.AddCopy(face)) is T ... }`
loop is usually a straight substitution.

## Which side

```csharp
public static class ExSide
{
    public static bool IsServer(this ICoreAPI api);
    public static bool IsClient(this ICoreAPI api);
    public static bool IsServer(this IWorldAccessor world);
    public static bool IsClient(this IWorldAccessor world);
}
```

Reads the same `Side` the engine exposes; `api.IsServer()` over `Api.Side == EnumAppSide.Server`.

## Reading a click

`ExInteraction.Of(world, byPlayer, blockSel)` builds an `Interaction` - what the click carried,
answered once instead of re-read from the player and selection at every guard:

```csharp
public readonly struct Interaction
{
    public Interaction(IWorldAccessor world, IPlayer player, BlockSelection selection);

    public ItemStack? Held { get; }                 // active hotbar stack, or null
    public CollectibleObject? HeldCollectible { get; }
    public bool HeldIs(EnumTool tool);
    public bool HeldIs(AssetLocation code);         // exact code, or a wildcard with '*'
    public bool HandEmpty { get; }
    public bool Sneaking { get; }                   // the player's sneak control
    public BlockFacing? Face { get; }               // selection.Face
    public bool IsServer { get; }
    public bool IsClient { get; }
}
```

`Interaction` only answers questions; it never decides which side runs a mutation, so the handler
still writes its own `if (interaction.IsServer) { ... }` around whatever the click triggers. A null
player or null selection reads as empty-handed / not-sneaking / no-face rather than throwing.

## Block info lines

```csharp
public static class ExInfo
{
    public static StringBuilder Lang(this StringBuilder dsc, string key, params object[] args);
    public static StringBuilder LangIf(this StringBuilder dsc, bool condition, string key, params object[] args);
    public static StringBuilder Measure(this StringBuilder dsc, string key, float value, string unit);
}
```

`dsc.Lang("iiex:some-key")` is `dsc.AppendLine(Lang.Get("iiex:some-key"))`; `LangIf` skips the line
when the condition is false. `Measure` folds the value through `ExMeasure` in the named unit
(`volume`, `pressure`, `temperature`, `power`, `speed`, `flowrate` or `energy`) before handing it to
`Lang.Get`, so a `GetBlockInfo` line reads correctly for both display systems without the caller
calling `ExMeasure` itself.

## `ExItems`, `ExCreativeTabs`, `ExBlockNames`

```csharp
ItemStack[] wrench = ExItems.WrenchStacks(world);        // interaction-help stacks
ExCreativeTabs.EnsureTab(Mod.Info.ModID);                // once, from Start
string name = ExBlockNames.Decorate(this, base.GetHeldItemName(itemStack));
```

```csharp
public static class ExItems
{
    public static ItemStack[] WrenchStacks(IWorldAccessor world);   // one stack per registered wrench, cached, for interaction help
}

public static class ExCreativeTabs
{
    public static void EnsureTab(string tabCode);   // append a custom creative tab (reflection; no-op if internal type absent)
}

public static class ExBlockNames
{
    public static string Decorate(Block block, string baseName);    // append material/rock/brick variant + refractory tier
}
```

## `ExContentGate` - disabling content

Config-gated "turn this content off": hide collectibles from the creative inventory and handbook.
Returns how many it affected.

```csharp
ExContentGate.HideFromCreativeAndHandbook(api, c => c.Code?.Path.StartsWith("toolmold-") == true);
```

```csharp
public static class ExContentGate
{
    public static int HideFromCreativeAndHandbook(ICoreAPI api, Func<CollectibleObject, bool> match);
}
```

Clearing a block's creative tabs and stacks also removes it from the handbook, so
`HideFromCreativeAndHandbook` does both. Call it after content has resolved (from
`StartServerSide`/`StartClientSide`, not `Start`), and pair it with a config toggle for
"disable X" features (e.g. `siex` tool-mold gating behind `/exmod molds`).

## `SurfaceRenderer` - flat fluid surfaces

`Renderers/SurfaceRenderer` is an `IRenderer` base for drawing a flat, textured horizontal surface
(a liquid line) inside a block - water tanks, molten canals. It owns the quad geometry built from
footprint boxes and the standard-shader plumbing; subclasses supply tint, height and texture.

```csharp
_renderer = new MoltenRenderer(Pos, capi, boxes, rotationY: 0f, fillStartY: 0f, fillHeightLevels: 16);
api.Event.RegisterRenderer(_renderer, EnumRenderStage.Opaque);
```

```csharp
public abstract class SurfaceRenderer : IRenderer
{
    protected SurfaceRenderer(BlockPos pos, ICoreClientAPI api, Cuboidf[] footprintBoxes, float rotationY, bool combine);

    public abstract double RenderOrder { get; }
    public virtual int RenderRange => 24;

    // Implement:
    protected abstract bool ShouldRender { get; }                     // draw this frame?
    protected abstract float SurfaceY { get; }                        // absolute surface height in block units
    protected abstract void ConfigureShader(IStandardShaderProgram shader, IRenderAPI render);  // tint/glow
    protected abstract bool BindSurfaceTexture(IRenderAPI render);    // bind texture; false to skip drawing

    // Optionally override:
    protected virtual int SelectMeshIndex() => 0;                     // which box-mesh to draw (combine == false)
    protected virtual bool UseBlend => false;                         // alpha blending for translucent surfaces

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage);
    public virtual void Dispose();
}
```

`combine: true` merges all footprint boxes into one always-drawn mesh; `combine: false` keeps one
mesh per box and lets `SelectMeshIndex` pick the cross-section by fill level.

> **Re-init on `OnExchanged`.** `rotationY` is captured at construction. A wrench-rotated block
> renders its fluid surface in the pre-rotation orientation unless you rebuild the renderer in the
> block entity's `OnExchanged`. Surface glow is push-based, so a hot mold/tap/barrel also needs a
> client tick calling its update path or it freezes hot and snaps cold on interaction.

## Related pages

- [Multiblock Structures](Multiblock-Structures) - `ExOrientation` drives `SetStructureAngle`.
- [Config System](Config-System) - back `ExContentGate` toggles with a live config value.
