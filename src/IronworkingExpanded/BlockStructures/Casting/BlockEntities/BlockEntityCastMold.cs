using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkMolten;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.Casting.BlockEntities;

/// <summary>
/// The cast-iron casting mold: the iron-tier tool mold, itself cast in the sand cell. Pouring is
/// recognised through <see cref="ILiquidMetalSink"/> rather than by deriving from vanilla's tool-mold
/// class, so the mold never shatters and stays reusable, acts as a heat sink whose body temperature
/// drives its block light, and yields its cast in the metal's own cast domain
/// (<see cref="MetalRegistry.CastProductOf"/>). See docs/design/processes/casting.md.
/// </summary>
[BlockEntityRegister]
public class BlockEntityCastMold : BlockEntity, ILiquidMetalSink {
  /// <summary>The metal cast in the mold, or null when empty.</summary>
  public ItemStack? MetalContent;

  /// <summary>Units of metal currently held.</summary>
  public int CurrentUnitAmount;

  /// <summary>Units this mold takes for a full cast (the <c>requiredUnits</c> block attribute).</summary>
  public int MaxUnitAmount = 200;

  // The mold body's own temperature (°C), the source of the heat-sink glow. Raised by a pour, decayed
  // each tick.
  private float _moldTemperature;

  private byte _lastGlow;
  private MoltenRenderer? _renderer;

  private static float ContentCooldownSpeed =>
    IwexValues.MoltenCooldownSpeed * IwexValues.BarrelCooldownCoefficient;

  /// <summary>Temperature (°C) of the cast metal, or 0 when empty.</summary>
  public float MetalTemperature =>
    MetalContent?.Collectible.GetTemperature(Api.World, MetalContent) ?? 0f;

  /// <summary>Whether the cast metal has cooled past its hardened threshold (ready to take out).</summary>
  public bool IsHardened =>
    MetalContent != null && MoltenMetal.IsHardened(Api.World, MetalContent);

  /// <summary>Whether the mold is filled to its required units.</summary>
  public bool IsFull => CurrentUnitAmount >= MaxUnitAmount;

  #region ILiquidMetalSink (pour in)

  /// <inheritdoc/>
  public bool CanReceiveAny => !IsFull;

  /// <inheritdoc/>
  public bool CanReceive(ItemStack metal) {
    if (IsFull)
      return false;
    if (
      MetalContent != null
      && !MetalContent.Collectible.Equals(
        MetalContent,
        metal,
        GlobalConstants.IgnoredStackAttributes
      )
    )
      return false;
    return GetMoldedStacks(metal) is { Length: > 0 };
  }

  /// <inheritdoc/>
  public void BeginFill(Vec3d hitPosition) { }

  /// <inheritdoc/>
  public void ReceiveLiquidMetal(
    ItemStack metal,
    ref int amount,
    float temperature
  ) {
    if (IsFull)
      return;
    if (
      MetalContent != null
      && !MetalContent.Collectible.Equals(
        MetalContent,
        metal,
        GlobalConstants.IgnoredStackAttributes
      )
    )
      return;

    if (MetalContent == null) {
      MetalContent = metal.Clone();
      MetalContent.ResolveBlockOrItem(Api.World);
      MetalContent.StackSize = 1;
      MoltenMetal.SetCooldownSpeed(MetalContent, ContentCooldownSpeed);
    }
    MoltenMetal.SetTemperature(Api.World, MetalContent, temperature);

    int accepted = Math.Min(amount, MaxUnitAmount - CurrentUnitAmount);
    CurrentUnitAmount += accepted;
    amount -= accepted;

    // Heat sink: the body reaches a fraction of the poured metal's temperature, keeping whichever of
    // the two is hotter.
    float reached = temperature * IwexValues.CastMoldHeatSinkFraction;
    if (reached > _moldTemperature)
      _moldTemperature = reached;

    UpdateRenderer();
    UpdateGlow();
    MarkDirty(true);
  }

  /// <inheritdoc/>
  public void OnPourOver() => MarkDirty(true);

  #endregion

  #region Heat-sink glow

  /// <summary>Block-light value (0-24) from the hotter of the cast metal and the glowing mold body.</summary>
  public byte GlowLightLevel {
    get {
      if (Api?.World == null)
        return 0;
      float hot = Math.Max(_moldTemperature, MetalTemperature);
      return MoltenMetal.GlowLevel(hot);
    }
  }

  private void UpdateGlow() {
    byte g = GlowLightLevel;
    if (g != _lastGlow) {
      _lastGlow = g;
      Api?.World.BlockAccessor.MarkBlockDirty(Pos);
    }
  }

  #endregion

  #region Lifecycle + tick

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    MaxUnitAmount = Block.Attributes?["requiredUnits"].AsInt(200) ?? 200;

    if (api.Side == EnumAppSide.Client) {
      InitRenderer((ICoreClientAPI)api);
      UpdateRenderer();
      RegisterGameTickListener(_ => UpdateRenderer(), 1000);
    } else {
      RegisterGameTickListener(_ => OnServerTick(), 1000);
    }
  }

  /// <summary>Restores a cast carried in on the placing stack (a filled mold set back down).</summary>
  public override void OnBlockPlaced(ItemStack? byItemStack = null) {
    base.OnBlockPlaced(byItemStack);
    if (byItemStack == null)
      return;
    var (contents, units) = MoltenContents.Read(
      byItemStack,
      MoltenContents.MoldUnitsKey,
      Api.World
    );
    if (contents?.Collectible == null || units <= 0)
      return;
    MetalContent = contents;
    MetalContent.ResolveBlockOrItem(Api.World);
    MoltenMetal.SetCooldownSpeed(MetalContent, ContentCooldownSpeed);
    CurrentUnitAmount = units;
    UpdateRenderer();
    UpdateGlow();
    MarkDirty(true);
  }

  private void OnServerTick() {
    if (MetalContent != null && CurrentUnitAmount > 0)
      MoltenMetal.SyncCooldownSpeed(
        Api.World,
        MetalContent,
        ContentCooldownSpeed
      );

    // The mold body decays toward ambient each second.
    if (_moldTemperature > IwexValues.MoltenAmbientTemperature) {
      _moldTemperature = Math.Max(
        IwexValues.MoltenAmbientTemperature,
        _moldTemperature - IwexValues.CastMoldBodyCooldownPerSecond
      );
      MarkDirty();
    }
    UpdateGlow();
  }

  #endregion

  #region Take out the cast

  /// <summary>
  /// Takes the hardened casting out (the mold is reusable, so it stays). Returns true if the click was
  /// consumed. A still-liquid cast refuses with a "too hot" message rather than falling through.
  /// </summary>
  public bool TryTakeOut(IPlayer byPlayer) {
    if (MetalContent == null || CurrentUnitAmount <= 0)
      return false;
    if (Api.Side == EnumAppSide.Client)
      return true;

    if (!IsHardened) {
      (byPlayer as IServerPlayer)?.SendIngameError("iwex-castmold-toohot");
      return true;
    }

    foreach (ItemStack stack in GetMoldedStacks(MetalContent))
      if (byPlayer.InventoryManager?.TryGiveItemstack(stack) != true)
        Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.2, 0.5));

    MetalContent = null;
    CurrentUnitAmount = 0;
    UpdateRenderer();
    UpdateGlow();
    ExSounds.Play(Api, Pos, ExSounds.StoneCrush, 0.5f);
    MarkDirty(true);
    return true;
  }

  /// <summary>
  /// Resolves the cast(s) this mold yields for <paramref name="fromMetal"/> in the metal's own cast
  /// domain, so cast iron comes out as <c>iwex:metalplate-castiron</c> while vanilla metals keep theirs.
  /// Empty when the metal has no product for this mold's template.
  /// </summary>
  public ItemStack[] GetMoldedStacks(ItemStack fromMetal) {
    AssetLocation? metal = fromMetal?.Collectible?.Code;
    if (metal == null)
      return [];

    var molded = new List<ItemStack>();
    foreach (JsonItemStack template in ExMoldDrops.Templates(Block)) {
      if (template.Code == null)
        continue;
      template.Code = MetalRegistry.CastProductOf(template.Code, metal);
      if (
        !template.Resolve(
          Api.World,
          "cast mold drop",
          printWarningOnError: false
        )
      )
        continue;
      if (template.ResolvedItemstack is not { } stack)
        continue;
      if (MetalContent != null)
        stack.Collectible.SetTemperature(Api.World, stack, MetalTemperature);
      molded.Add(stack);
    }
    return molded.ToArray();
  }

  /// <summary>Drops for a broken mold: a full hardened cast yields the part, otherwise recovered bits.</summary>
  public ItemStack[] GetMetalDrops() {
    if (MetalContent == null || CurrentUnitAmount <= 0)
      return [];
    if (IsFull && IsHardened)
      return GetMoldedStacks(MetalContent);
    ItemStack? bits = MoltenChisel.BuildRecovery(
      Api.World,
      MetalContent.Collectible.Code,
      MetalTemperature,
      CurrentUnitAmount
    );
    return bits != null ? [bits] : [];
  }

  #endregion

  #region Rendering

  private void InitRenderer(ICoreClientAPI capi) {
    // A shallow pool over the mold cavity: the tray footprint, one level deep starting at y = 1/16.
    var boxes = new[] { new Cuboidf(2f, 1f, 2f, 14f, 2f, 14f) };
    _renderer = new MoltenRenderer(Pos, capi, boxes, 0f, 1f / 16f, 1f);
    capi.Event.RegisterRenderer(_renderer, EnumRenderStage.Opaque);
  }

  private void UpdateRenderer() {
    if (_renderer == null)
      return;
    if (MetalContent == null || CurrentUnitAmount <= 0) {
      _renderer.FillRatio = 0f;
      _renderer.MetalStack = null;
      return;
    }
    _renderer.FillRatio =
      MaxUnitAmount > 0 ? (float)CurrentUnitAmount / MaxUnitAmount : 0f;
    _renderer.Temperature = MetalTemperature;
    _renderer.MetalStack = MetalContent;
  }

  public override void OnBlockRemoved() {
    _renderer?.Dispose();
    _renderer = null;
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded() {
    _renderer?.Dispose();
    _renderer = null;
    base.OnBlockUnloaded();
  }

  #endregion

  #region HUD + serialization

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    if (MetalContent == null || CurrentUnitAmount <= 0) {
      dsc.AppendLine(Lang.Get("iwex:castmold-empty", MaxUnitAmount));
      return;
    }
    string state = Lang.Get(
      MoltenMetal.StateOf(Api.World, MetalContent) switch {
        MoltenState.Liquid => "iwex:metalstate-liquid",
        MoltenState.Hardened => "iwex:metalstate-hardened",
        _ => "iwex:metalstate-soft",
      }
    );
    dsc.AppendLine(
      Lang.Get(
        "iwex:castmold-units-state",
        CurrentUnitAmount,
        MaxUnitAmount,
        state,
        MoltenMetal.FormatTemperature(MetalTemperature)
      )
    );
  }

  // Persist under the vanilla mold key ("fillLevel") so a carried, filled mold round-trips through
  // MoltenContents and the shared spill/burn/carry handling recognises it.
  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetItemstack("contents", MetalContent);
    tree.SetInt(MoltenContents.MoldUnitsKey, CurrentUnitAmount);
    tree.SetFloat("moldTemp", _moldTemperature);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor world
  ) {
    base.FromTreeAttributes(tree, world);
    MetalContent = tree.GetItemstack("contents");
    CurrentUnitAmount = tree.GetInt(MoltenContents.MoldUnitsKey);
    _moldTemperature = tree.GetFloat("moldTemp");
    MetalContent?.ResolveBlockOrItem(world);
    if (Api?.Side == EnumAppSide.Client) {
      UpdateRenderer();
      UpdateGlow();
    }
  }

  public override void OnStoreCollectibleMappings(
    Dictionary<int, AssetLocation> blockIdMapping,
    Dictionary<int, AssetLocation> itemIdMapping
  ) {
    MetalContent?.Collectible?.OnStoreCollectibleMappings(
      Api.World,
      new DummySlot(MetalContent),
      blockIdMapping,
      itemIdMapping
    );
  }

  public override void OnLoadCollectibleMappings(
    IWorldAccessor worldForResolve,
    Dictionary<int, AssetLocation> oldBlockIdMapping,
    Dictionary<int, AssetLocation> oldItemIdMapping,
    int schematicSeed,
    bool resolveImports
  ) {
    // A false return means the destination world has no such item/block; FixMapping leaves Id at the
    // source world's value, which would resolve to whatever owns that id there. Null the stack instead
    // of keeping a mis-resolved one, matching vanilla's BEIngotMold.cs:806-809.
    if (
      MetalContent?.FixMapping(
        oldBlockIdMapping,
        oldItemIdMapping,
        worldForResolve
      ) == false
    )
      MetalContent = null;
  }

  #endregion
}
