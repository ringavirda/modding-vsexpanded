using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace IronworkingExpanded.BlockNetworkMolten.Blocks;

/// <summary>
/// A portable barrel that stores liquid metal. Poured into from a canal tap (or a
/// crucible), it renders a glowing fill level; once full and hardened the metal can
/// be chiselled out. Sneak + right-click picks the barrel up with its contents.
/// </summary>
[BlockRegister]
public partial class BlockMoltenBarrel : Block, IExBlockDefProvider {
  private MeshData? _barrelBaseMesh;

  // Item list for the pour interaction help, cached on load. The chisel list comes from
  // MoltenChisel.ChiselHelp.
  private ItemStack[] _smeltedCrucibles = [];

  #region Code-first definition

  // Fill geometry and capacity, read at runtime from the block's own attributes (authored file or
  // injected def alike) so the values live once, in the def.
  public int MaxUnits => Attributes?["maxUnits"].AsInt(800) ?? 800;
  public int FillStart => Attributes?["fillStart"].AsInt(2) ?? 2;
  public int FillHeight => Attributes?["fillHeight"].AsInt(8) ?? 8;
  public JsonObject? FillQuadsByLevel => Attributes?["fillQuadsByLevel"];

  /// <summary>The molten barrel blocktype: a portable metal vessel that stores liquid metal and can be
  /// carried in a backpack. The two <c>construction</c> variants behave identically and differ only in
  /// shape and craft - <c>plated</c> is fabricated from plates, fire clay and nails, <c>cast</c> is a
  /// sand-cast <c>cast-barrel</c> blank lined with fire clay (see docs/design/processes/casting.md).
  /// Single-code <c>molten-barrel</c> worlds are remapped to <c>-plated</c> by
  /// <see cref="BlockMigrations.BarrelConstructionMigration"/>.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "molten-barrel", "molten/barrel")
        .Class<BlockMoltenBarrel>()
        .EntityClass("iwex.BlockEntityMoltenBarrel")
        .VariantGroup("construction", "plated", "cast")
        .MaxStackSize(1)
        .StorageFlags(2)
        .Material(EnumBlockMaterial.Metal)
        .MetalSounds()
        .CreativeCommon("*")
        .HeldTpIdleAnimation("holdbothhandslarge")
        .Attribute("maxUnits", 800)
        .Attribute("fillHeight", 8)
        .Attribute("fillStart", 2)
        .Attribute(
          "fillQuadsByLevel",
          new[]
          {
            new
            {
              x1 = 4,
              z1 = 4,
              x2 = 12,
              z2 = 12,
            },
          }
        )
        .Behavior("Lockable")
        .Behavior("UnstableFalling")
        .ShapeByType("*-plated", "iwex:molten/barrel-plated")
        .ShapeByType("*-cast", "iwex:molten/barrel-cast")
        .NonSolid()
        .TpHandTransform(-0.8, -1, -0.55, 20, 14, -90, 0.75),
    ];

  #endregion

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);

    // Cache all smelted crucibles for the pour interaction help.
    _smeltedCrucibles = MoltenMetal.SmeltedCrucibleStacks(api.World);
  }

  /// <summary>
  /// Emits incandescent block light scaled to the stored metal's temperature. The block entity owns the
  /// threshold and scaling (<see cref="BlockEntityMoltenBarrel.GlowLightLevel"/>) and re-lights the block
  /// when that level changes. Held and inventory barrels have no position, fall back to base and glow
  /// through <see cref="OnBeforeRender"/> instead.
  /// </summary>
  public override byte[] GetLightHsv(
    IBlockAccessor blockAccessor,
    BlockPos pos,
    ItemStack? stack = null
  ) {
    if (
      pos != null
      && blockAccessor.GetBlockEntity(pos) is BlockEntityMoltenBarrel be
    ) {
      byte val = be.GlowLightLevel;
      if (val > 0)
        return [8, 7, val];
    }
    return base.GetLightHsv(blockAccessor, pos, stack);
  }

  #region Held / inventory rendering
  public override void OnBeforeRender(
    ICoreClientAPI capi,
    ItemStack itemstack,
    EnumItemRenderTarget target,
    ref ItemRenderInfo renderinfo
  ) {
    base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

    var (metal, units) = MoltenContents.Read(
      itemstack,
      MoltenContents.BarrelUnitsKey,
      capi.World
    );
    if (units <= 0 || metal?.Collectible == null)
      return;

    int maxUnits = MaxUnits;
    float fillRatio =
      maxUnits > 0 ? GameMath.Clamp((float)units / maxUnits, 0f, 1f) : 0f;
    float temp = metal.Collectible.GetTemperature(capi.World, metal);
    int glow = (int)GameMath.Clamp((temp - 550f) / 2f, 0f, 255f);

    var cache = GetMeshRefCache(capi);
    int fillStep = (int)GameMath.Clamp(fillRatio * 16f, 0f, 16f);
    string key = $"{metal.Collectible.Code}|{fillStep}|{glow / 16}";
    if (!cache.TryGetValue(key, out var meshRef)) {
      MeshData mesh = GenMeshWithContent(capi, metal, fillRatio, glow);
      meshRef = cache[key] = capi.Render.UploadMultiTextureMesh(mesh);
    }
    renderinfo.ModelRef = meshRef;
  }

  private Dictionary<string, MultiTextureMeshRef> GetMeshRefCache(
    ICoreClientAPI capi
  ) {
    string cacheKey = "moltenBarrelMeshRefs:" + Code;
    if (
      capi.ObjectCache.TryGetValue(cacheKey, out var existing)
      && existing is Dictionary<string, MultiTextureMeshRef> dict
    )
      return dict;
    var created = new Dictionary<string, MultiTextureMeshRef>();
    capi.ObjectCache[cacheKey] = created;
    return created;
  }

  private MeshData GenMeshWithContent(
    ICoreClientAPI capi,
    ItemStack metal,
    float fillRatio,
    int glow
  ) {
    _barrelBaseMesh ??= TesselateBaseMesh(capi);
    MeshData combined = _barrelBaseMesh.Clone();

    Cuboidf[] boxes = FillQuads.BoxesFrom(
      FillQuadsByLevel,
      new Cuboidf(4f, 0f, 4f, 12f, 16f, 12f)
    );
    float yLevel = FillStart / 16f + fillRatio * FillHeight / 16f;

    var tex = metal.Item?.FirstTexture ?? metal.Block?.FirstTextureInventory;
    if (tex == null)
      return combined;
    capi.BlockTextureAtlas.GetOrInsertTexture(
      tex,
      out _,
      out TextureAtlasPosition texPos,
      0.005f
    );
    if (texPos == null)
      return combined;

    foreach (Cuboidf box in boxes) {
      MeshData quad = QuadMeshUtil.GetQuad();
      quad.Rgba = new byte[16];
      quad.Rgba.Fill(byte.MaxValue);
      quad.Flags = new int[4];
      for (int i = 0; i < 4; i++)
        quad.Flags[i] = glow & VertexFlags.GlowLevelBitMask;

      quad.Uv =
      [
        texPos.x1,
        texPos.y1,
        texPos.x2,
        texPos.y1,
        texPos.x2,
        texPos.y2,
        texPos.x1,
        texPos.y2,
      ];
      quad.TextureIds = [texPos.atlasTextureId];
      quad.TextureIndices = [0];
      quad.TextureIndicesCount = 1;

      float[] matrix = new Matrixf()
        .Translate((box.X1 + box.X2) / 32f, yLevel, (box.Z1 + box.Z2) / 32f)
        .RotateX((float)Math.PI / 2f)
        .Scale((box.X2 - box.X1) / 32f, (box.Z2 - box.Z1) / 32f, 1f)
        .Values;
      quad.MatrixTransform(matrix);
      combined.AddMeshData(quad);
    }

    return combined;
  }

  private MeshData TesselateBaseMesh(ICoreClientAPI capi) {
    capi.Tesselator.TesselateBlock(this, out MeshData mesh);
    return mesh;
  }

  public override void OnUnloaded(ICoreAPI api) {
    if (api is ICoreClientAPI capi) {
      string cacheKey = "moltenBarrelMeshRefs:" + Code;
      if (
        capi.ObjectCache.TryGetValue(cacheKey, out var existing)
        && existing is Dictionary<string, MultiTextureMeshRef> dict
      ) {
        foreach (var meshRef in dict.Values)
          meshRef.Dispose();
        capi.ObjectCache.Remove(cacheKey);
      }
    }
    base.OnUnloaded(api);
  }
  #endregion

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is not BlockEntityMoltenBarrel be
    )
      return base.OnBlockInteractStart(world, byPlayer, blockSel);

    var heldItem = byPlayer
      .InventoryManager
      .ActiveHotbarSlot
      ?.Itemstack
      ?.Collectible;
    if (heldItem?.Tool == EnumTool.Chisel) {
      // A chisel in hand resolves here either way: chip the hardened metal out (no tool wear, 10 units
      // per bit), or do nothing while the metal is not hardened.
      var outcome = MoltenChisel.TryChisel(
        world,
        byPlayer,
        blockSel.Position,
        be,
        ExSounds.AnvilHit,
        damageChisel: false,
        yOffset: 0.5
      );
      return outcome != ChiselOutcome.NotChiseling;
    }

    if (byPlayer.Entity.Controls.ShiftKey) {
      if (world.Side == EnumAppSide.Client)
        return true;

      var stack = new ItemStack(this);
      MoltenContents.Write(
        stack,
        MoltenContents.BarrelUnitsKey,
        be.MetalContent,
        be.CurrentUnitAmount
      );
      if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
        world.SpawnItemEntity(
          stack,
          blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5)
        );
      world.BlockAccessor.SetBlock(0, blockSel.Position);
      return true;
    }

    return be.OnPlayerInteract(byPlayer);
  }

  public override void OnBlockPlaced(
    IWorldAccessor world,
    BlockPos blockPos,
    ItemStack byItemStack
  ) {
    base.OnBlockPlaced(world, blockPos, byItemStack);

    if (byItemStack == null)
      return;

    if (
      world.BlockAccessor.GetBlockEntity(blockPos) is BlockEntityMoltenBarrel be
    ) {
      // Assign fields directly instead of FromTreeAttributes: the base
      // deserializer rebuilds Pos from posx/posy/posz, which this partial tree
      // lacks, corrupting the block entity position to (0,0,0) on reload.
      (be.MetalContent, be.CurrentUnitAmount) = MoltenContents.Read(
        byItemStack,
        MoltenContents.BarrelUnitsKey,
        world
      );
      be.MarkDirty(true);
    }
  }

  public override void GetHeldItemInfo(
    ItemSlot inSlot,
    StringBuilder dsc,
    IWorldAccessor world,
    bool withDebugInfo
  ) {
    base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

    if (inSlot.Itemstack?.Attributes?["blockEntityAttributes"] == null)
      return;

    var (metalContent, currentUnits) = MoltenContents.Read(
      inSlot.Itemstack,
      MoltenContents.BarrelUnitsKey,
      world
    );
    int maxUnits = MaxUnits;

    if (currentUnits <= 0) {
      dsc.AppendLine(Lang.Get(IwexLang.MoltenbarrelInfoEmpty, maxUnits));
      return;
    }

    if (metalContent == null) {
      dsc.AppendLine(
        Lang.Get(IwexLang.MoltenbarrelInfoUnits, currentUnits, maxUnits)
      );
      return;
    }

    string state = Lang.Get(
      MoltenMetal.StateOf(world, metalContent) switch {
        MoltenState.Liquid => "iwex:metalstate-liquid",
        MoltenState.Hardened => "iwex:metalstate-hardened",
        _ => "iwex:metalstate-soft",
      }
    );

    dsc.AppendLine(
      Lang.Get(
        "iwex:moltenbarrel-info-content",
        currentUnits,
        maxUnits,
        MoltenMetal.DisplayName(metalContent.Collectible.Code.ToString()),
        state,
        MoltenMetal.FormatTemperature(
          MoltenMetal.GetTemperature(world, metalContent)
        )
      )
    );
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) {
    var interactions =
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [];

    var be =
      world.BlockAccessor.GetBlockEntity(selection.Position)
      as BlockEntityMoltenBarrel;
    bool isHardened = be?.IsHardened ?? false;
    bool isFull = be?.IsFull ?? false;

    var result = new List<WorldInteraction>(interactions)
    {
      new()
      {
        ActionLangCode = "iwex:blockhelp-barrel-pickup",
        MouseButton = EnumMouseButton.Right,
        HotKeyCode = "sneak",
      },
    };

    if (!isHardened && !isFull) {
      result.Add(
        new WorldInteraction {
          ActionLangCode = "iwex:blockhelp-barrel-pour",
          MouseButton = EnumMouseButton.Right,
          Itemstacks = _smeltedCrucibles,
        }
      );
    }

    if (isHardened)
      result.Add(
        MoltenChisel.ChiselHelp(world, "iwex:blockhelp-barrel-chisel")
      );

    return result.ToArray();
  }

  public override bool CanBePlacedInto(ItemStack stack, ItemSlot slot) {
    return slot.Inventory?.ClassName == "backpack";
  }

  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1
  ) {
    // Molten metal hits the ground and sizzles when a still-liquid barrel breaks.
    if (
      world.Side == EnumAppSide.Server
      && world.BlockAccessor.GetBlockEntity(pos) is BlockEntityMoltenBarrel be
      && be.MetalContent != null
      && be.CurrentUnitAmount > 0
      && !be.IsHardened
    )
      world.PlaySoundAt(
        ExSounds.Sizzle,
        pos.X + 0.5,
        pos.Y + 0.5,
        pos.Z + 0.5,
        null,
        true,
        24f
      );

    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) {
    var drops = new List<ItemStack> { new ItemStack(this) };

    if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityMoltenBarrel be)
      drops.AddRange(be.GetMetalDrops());

    return drops.ToArray();
  }
}
