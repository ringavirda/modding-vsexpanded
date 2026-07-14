using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using ExpandedLib.Metals;

namespace IronworkingExpanded.BlockNetworkMolten.Blocks;

/// <summary>
/// The base molten-canal block: a self-orienting node of the "molten" network that
/// carries liquid metal. Provides the orientation tables shared by every
/// straight/bend/junction canal variant and handles open-end connector updates,
/// solidified-metal drops, and spill sounds on break.
/// </summary>
[BlockRegister]
public partial class BlockMoltenCanal : BlockNetworkNode, IExBlockDefProvider
{
  public override string NetworkType => "molten";

  /// <summary>The mod id, used to build the code-first defs when deriving runtime tables from them.</summary>
  protected const string Domain = "iwex";

  private Dictionary<string, string[]>? _allowedOrientations;

  /// <summary>
  /// Derived from THIS block's own code-first defs (resolved by its runtime type), so the orientation
  /// states live once - in the variant groups - and every canal endpoint subclass (start/tap/moldpedestal)
  /// inherits the right map with no duplicated list. Cached on first read.
  /// </summary>
  public override Dictionary<string, string[]> AllowedOrientations =>
    _allowedOrientations ??= ExDefinitions.OrientationMap(
      ExDefinitions.DefinitionsOf(GetType(), Domain)
    );

  #region Code-first definition

  // The fill-geometry attributes read at runtime from the block's own attributes (file or injected def
  // alike), replacing the JSON-scanned generated members - so the values live once, in the def.
  public JsonObject? FillQuadsByLevel => Attributes?["fillQuadsByLevel"];
  public int FillStart => Attributes?["fillStart"].AsInt(14) ?? 14;
  public int FillHeight => Attributes?["fillHeight"].AsInt(1) ?? 1;

  /// <summary>The molten-canal blocktypes, authored in C# (migrated from molten/canalbrick/* and
  /// molten/canalcobblestone/*). One class backs 8 files: the 4 canal shapes (straight/bend/tjunction/
  /// xjunction) each in a fire-brick and a cobblestone skin.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain)
  {
    foreach (CanalSkin skin in CanalSkins)
      foreach (CanalTypeSpec type in CanalTypes)
        yield return CanalDef(domain, skin, type);
  }

  // Protected so the endpoint subclasses (start/moldpedestal), which share the fire-brick + cobblestone
  // skins, reuse the exact same material/sounds/texture/variant surface instead of re-declaring it.
  protected sealed record CanalSkin(
    string Folder,
    EnumBlockMaterial Material,
    bool HasPlaceSound,
    System.Action<ExBlockDef> Variants,
    System.Action<ExBlockDef> Texture
  );

  private sealed record CanalTypeSpec(
    string Type,
    int MaxStack,
    string[] Orientations,
    string CreativeSelector,
    object FillQuads,
    (string Wildcard, int? RotateY)[] Shapes
  );

  protected static readonly CanalSkin[] CanalSkins =
  [
    new(
      "canalbrick",
      EnumBlockMaterial.Ceramic,
      HasPlaceSound: true,
      Variants: d =>
        d.VariantGroup(
          "brick",
          "fire",
          "black",
          "brown",
          "cream",
          "gray",
          "orange",
          "red",
          "tan"
        ),
      Texture: d =>
        d.Texture(
          "granite1",
          "game:block/clay/brick/four/running/cream1",
          "game:block/clay/brick/four/running/{brick}1"
        )
    ),
    new(
      "canalcobblestone",
      EnumBlockMaterial.Stone,
      HasPlaceSound: false,
      Variants: d =>
        d.VariantGroupFromProperties("rock", "block/rockwithdeposit")
          .SkipVariants("*-halite-*", "*-scoria-*", "*-tuff-*", "*-travertine-*"),
      Texture: d =>
        d.Texture("granite1", "game:block/stone/cobblestone/{rock}1")
    ),
  ];

  private static readonly CanalTypeSpec[] CanalTypes =
  [
    new(
      "straight",
      8,
      ["ns", "we"],
      "*-straight-*-ns",
      new[] { new { x1 = 7, z1 = 0, x2 = 9, z2 = 16 } },
      [("*-straight-*-ns", null), ("*-straight-*-we", 90)]
    ),
    new(
      "bend",
      4,
      ["nw", "se", "en", "ws"],
      "*-bend-*-nw",
      new[]
      {
        new { x1 = 7, z1 = 0, x2 = 9, z2 = 9 },
        new { x1 = 0, z1 = 7, x2 = 7, z2 = 9 },
      },
      [
        ("*-bend-*-nw", null),
        ("*-bend-*-en", 270),
        ("*-bend-*-se", 180),
        ("*-bend-*-ws", 90),
      ]
    ),
    new(
      "tjunction",
      4,
      ["nes", "esw", "swn", "wne"],
      "*-tjunction-*-esw",
      new[]
      {
        new { x1 = 0, z1 = 7, x2 = 16, z2 = 9 },
        new { x1 = 7, z1 = 0, x2 = 9, z2 = 7 },
      },
      [
        ("*-tjunction-*-wne", null),
        ("*-tjunction-*-nes", 270),
        ("*-tjunction-*-esw", 180),
        ("*-tjunction-*-swn", 90),
      ]
    ),
    new(
      "xjunction",
      4,
      ["nswe"],
      "*-xjunction-*-nswe",
      new[]
      {
        new { x1 = 0, z1 = 7, x2 = 16, z2 = 9 },
        new { x1 = 7, z1 = 0, x2 = 9, z2 = 7 },
        new { x1 = 7, z1 = 9, x2 = 9, z2 = 16 },
      },
      [("*-xjunction-*-nswe", null)]
    ),
  ];

  private static ExBlockDef CanalDef(
    string domain,
    CanalSkin skin,
    CanalTypeSpec type
  ) =>
    CanalFamilyDef(
        domain,
        $"molten/{skin.Folder}/{type.Type}",
        skin,
        type.Type,
        type.MaxStack,
        type.CreativeSelector,
        type.Orientations,
        type.FillQuads,
        $"iwex:molten/canal/{type.Type}",
        type.Shapes
      )
      .Class<BlockMoltenCanal>()
      .EntityClass("iwex.BlockEntityMoltenCanal");

  /// <summary>
  /// Builds the surface shared by every canal-family blocktype - a canal shape OR a start/moldpedestal
  /// endpoint: the skin's material/sounds/texture/variant plus the common fill geometry, handbook grouping,
  /// Lockable behavior, orientation group, per-orientation shapes and non-solid flags. The caller binds its
  /// own <c>class</c>/<c>entityClass</c> (they differ per endpoint) and adds any extra attributes (the mold
  /// pedestal's separate mold fill). Authored once instead of copied across the start/moldpedestal defs.
  /// </summary>
  protected static ExBlockDef CanalFamilyDef(
    string domain,
    string assetName,
    CanalSkin skin,
    string type,
    int maxStack,
    string creativeSelector,
    string[] orientations,
    object fillQuads,
    string shapeBase,
    (string Wildcard, int? RotateY)[] shapes
  )
  {
    var def = ExBlockDef
      .Create(domain, "moltencanal", assetName)
      .Material(skin.Material)
      .Sound("walk", "game:walk/stone");
    if (skin.HasPlaceSound)
      def.Sound("place", "game:block/ceramicplace");
    def.SoundByTool(
        EnumTool.Pickaxe,
        "game:block/rock-hit-pickaxe",
        "game:block/rock-break-pickaxe"
      )
      .MaxStackSize(maxStack)
      .CreativeCommon(creativeSelector)
      .Attribute("fillHeight", 1)
      .Attribute("fillStart", 14)
      .Attribute("fillQuadsByLevel", fillQuads)
      .Handbook($"moltencanal-{type}-*")
      .Behavior("Lockable")
      .VariantGroup("type", type);
    skin.Variants(def);
    def.VariantGroup("orientation", orientations);
    skin.Texture(def);
    foreach ((string wildcard, int? rotateY) in shapes)
      def.ShapeByType(wildcard, shapeBase, rotateY: rotateY);
    return def.NonSolid();
  }

  #endregion

  /// <summary>A type's default orientation is the first state it lists (which matches every canal shape's
  /// fallback), so this is derived from the defs too - only the start block overrides it (it defaults
  /// south, not to its first-listed north).</summary>
  protected override string GetFallbackOrientation(string? type) =>
    type != null
    && AllowedOrientations.TryGetValue(type, out string[]? states)
    && states.Length > 0
      ? states[0]
      : "ns";

  /// <summary>
  /// Disables wrench rotation (and the hint) while the cell holds liquid metal or has solidified -
  /// drain or chip it clear first.
  /// </summary>
  protected override bool CanWrenchRotate(IWorldAccessor world, BlockPos pos)
  {
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityMoltenCanal be
      && (be.HasMoltenMetal || be.Solidified)
    )
      return false;

    return base.CanWrenchRotate(world, pos);
  }

  /// <summary>
  /// Emits incandescent block light scaled to the metal's temperature (via
  /// <see cref="BlockEntityMoltenCanal.GlowLightLevel"/>), like the cowper heat sink.
  /// </summary>
  public override byte[] GetLightHsv(
    IBlockAccessor blockAccessor,
    BlockPos pos,
    ItemStack? stack = null
  )
  {
    if (
      pos != null
      && blockAccessor.GetBlockEntity(pos) is BlockEntityMoltenCanal be
    )
    {
      byte val = be.GlowLightLevel;
      if (val > 0)
        return [8, 7, val];
    }
    return base.GetLightHsv(blockAccessor, pos, stack);
  }

  public override void OnBlockPlaced(
    IWorldAccessor world,
    BlockPos pos,
    ItemStack? byItemStack
  )
  {
    base.OnBlockPlaced(world, pos, byItemStack);
    UpdateEndConnectors(world, pos);
  }

  public override void OnNeighbourBlockChange(
    IWorldAccessor world,
    BlockPos pos,
    BlockPos neibpos
  )
  {
    base.OnNeighbourBlockChange(world, pos, neibpos);
    UpdateEndConnectors(world, pos);
  }

  protected void UpdateEndConnectors(IWorldAccessor world, BlockPos pos)
  {
    if (Orientation == null)
      return;

    var openConnectors = Orientation
      .Where(conn =>
        world.BlockAccessor.GetBlock(
          pos.AddCopy(BlockFacing.FromFirstLetter(conn))
        )
          is not BlockMoltenCanal
      )
      .Select(conn => BlockFacing.FromFirstLetter(conn))
      .ToArray();

    var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityMoltenCanal;
    be?.OpenConnectorFaces = openConnectors;
  }

  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1
  )
  {
    // Read BE state before base.OnBlockBroken → RemoveNode tears the network down.
    if (
      world.Side == EnumAppSide.Server
      && world.BlockAccessor.GetBlockEntity(pos) is BlockEntityMoltenCanal be
      && be.WouldSpillOnRemoval()
    )
      world.PlaySoundAt(ExSounds.Sizzle, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5);

    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    var drops = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

    // The network is already torn down here, so read the cached state from the BE (still alive,
    // holding the last broadcast values).
    if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityMoltenCanal be)
    {
      var solidifiedDrop = be.GetSolidifiedDrop(world);
      if (solidifiedDrop != null)
        drops = [.. drops, solidifiedDrop];

      // Recover part of the seal's fire clay when a sealed canal is broken.
      if (be.Sealed && world.GetItem(FireClayCode) is { } clay)
        drops =
        [
          .. drops,
          new ItemStack(clay, IwexValues.CanalUnsealClayRefund),
        ];
    }

    return drops;
  }

  public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
  {
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityMoltenCanal
      {
        Solidified: true
      }
    )
    {
      AssetLocation loc = CodeWithVariants(
        ["variant", "state", "orientation"],
        ["pass", "normal", "ns"]
      );
      Block? pickBlock = world.GetBlock(loc);
      return new ItemStack(pickBlock ?? this);
    }

    var drops = GetDrops(world, pos, null);
    return drops.Length > 0 ? drops[0] : new ItemStack(this);
  }

  #region Sealing (separator / valve)
  private static readonly AssetLocation FireClayCode = new("game:clay-fire");

  /// <summary>
  /// Interactions on a molten canal:
  /// <list type="bullet">
  /// <item>Any solidified canal: chisel in hand + hammer in the off-hand chips the
  /// hardened metal out, recovering bits and restoring the run to working order.</item>
  /// <item>Straight canal + <see cref="IwexValues.CanalSealClayCost"/> fire clay:
  /// seals it into a flow-blocking separator.</item>
  /// <item>Sealed straight canal + chisel: breaks the seal and refunds
  /// <see cref="IwexValues.CanalUnsealClayRefund"/> fire clay.</item>
  /// </list>
  /// </summary>
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is not BlockEntityMoltenCanal be
    )
      return base.OnBlockInteractStart(world, byPlayer, blockSel);

    ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
    ItemStack? held = activeSlot?.Itemstack;

    // A solidified canal is chipped clear with a chisel in hand + hammer in the off-hand (shared
    // chisel-out ritual). A plain click on a still-clogged cell falls through to base.
    if (be.Solidified)
    {
      var outcome = MoltenChisel.TryChisel(
        world,
        byPlayer,
        blockSel.Position,
        be,
        ExSounds.StoneCrush
      );
      return outcome != ChiselOutcome.NotChiseling
        || base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    // Sealing / unsealing only applies to straight segments.
    if (Type != "straight")
      return base.OnBlockInteractStart(world, byPlayer, blockSel);

    if (!be.Sealed)
    {
      if (!IsFireClay(held) || held!.StackSize < IwexValues.CanalSealClayCost)
        return base.OnBlockInteractStart(world, byPlayer, blockSel);

      // Only seal a fully drained section: this cell AND its connector-face neighbours must be
      // empty, so a seal never traps metal against itself.
      if (!CanSeal(world, blockSel.Position, be))
      {
        if (world.Side == EnumAppSide.Server)
          (byPlayer as IServerPlayer)?.SendIngameError("iwex-canalnotempty");
        return false;
      }

      if (world.Side == EnumAppSide.Server)
      {
        be.SetSealed(true);
        if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
          activeSlot!.TakeOut(IwexValues.CanalSealClayCost);
          activeSlot.MarkDirty();
        }
        ExSounds.Play(world.Api, blockSel.Position, ExSounds.Build, 0.8f);
      }
      return true;
    }

    // Sealed: unseal with a chisel.
    if (!MoltenChisel.IsTool(held, EnumTool.Chisel))
      return base.OnBlockInteractStart(world, byPlayer, blockSel);

    if (world.Side == EnumAppSide.Server)
    {
      be.SetSealed(false);
      if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
      {
        Item? clay = world.GetItem(FireClayCode);
        if (clay != null)
        {
          var refund = new ItemStack(clay, IwexValues.CanalUnsealClayRefund);
          if (!byPlayer.InventoryManager.TryGiveItemstack(refund))
            world.SpawnItemEntity(
              refund,
              blockSel.Position.ToVec3d().Add(0.5, 0.6, 0.5)
            );
        }
        held!.Collectible.DamageItem(world, byPlayer.Entity, activeSlot, 1);
      }
      ExSounds.Play(world.Api, blockSel.Position, ExSounds.StoneCrush, 0.8f);
    }
    return true;
  }

  /// <summary>
  /// Whether this straight canal may be sealed: the cell itself holds no metal and neither does any
  /// of its connector-face canal neighbours. Sealing only severs an already-drained section.
  /// </summary>
  private bool CanSeal(
    IWorldAccessor world,
    BlockPos pos,
    BlockEntityMoltenCanal be
  )
  {
    if (!be.IsCellEmpty)
      return false;
    if (Orientation == null)
      return true;

    foreach (char c in Orientation)
    {
      BlockPos nPos = pos.AddCopy(BlockFacing.FromFirstLetter(c));
      if (
        world.BlockAccessor.GetBlockEntity(nPos) is BlockEntityMoltenCanal nbe
        && !nbe.IsCellEmpty
      )
        return false;
    }
    return true;
  }

  private static bool IsFireClay(ItemStack? stack) =>
    stack?.Collectible?.Code is { } code
    && code.Domain == FireClayCode.Domain
    && code.Path == FireClayCode.Path;

  private static ItemStack[]? _fireClayStacks;

  /// <summary>
  /// The "chip out the solidified cell" interaction hint, or <c>null</c> when the cell at
  /// <paramref name="pos"/> can't be chiselled yet (not solidified, or not fully hardened). Exposed so
  /// endpoint subclasses that build their own interaction help (the mold pedestal) can still advertise
  /// clearing a clogged cell - the tap inherits the base help directly.
  /// </summary>
  protected WorldInteraction? ChiselClearInteraction(
    IWorldAccessor world,
    BlockPos pos
  ) =>
    world.BlockAccessor.GetBlockEntity(pos)
      is BlockEntityMoltenCanal { Solidified: true, IsHardened: true }
      ? MoltenChisel.ChiselHelp(world, "iwex:blockhelp-canal-clearsolidified")
      : null;

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  )
  {
    WorldInteraction[] baseHelp =
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [];

    var be =
      world.BlockAccessor.GetBlockEntity(selection.Position)
      as BlockEntityMoltenCanal;

    // The chip-clear hint only shows on a clogged (solidified) cell once it has fully hardened -
    // not on fittings that never latch Solidified, which can't be chiselled.
    if (be is { Solidified: true, IsHardened: true })
      return
      [
        .. baseHelp,
        MoltenChisel.ChiselHelp(world, "iwex:blockhelp-canal-clearsolidified"),
      ];

    if (Type != "straight")
      return baseHelp;

    // A sealed canal can always be unsealed (with a chisel).
    if (be is { Sealed: true })
      return
      [
        .. baseHelp,
        MoltenChisel.ChiselHelp(world, "iwex:blockhelp-canal-unseal"),
      ];

    // The seal hint only shows when sealing is actually possible: this cell and its neighbours
    // are empty. A cell holding (or sitting next to) metal - liquid or solidified - won't advertise it.
    if (be != null && CanSeal(world, selection.Position, be))
      return
      [
        .. baseHelp,
        new WorldInteraction
        {
          ActionLangCode = "iwex:blockhelp-canal-seal",
          MouseButton = EnumMouseButton.Right,
          Itemstacks =
            (_fireClayStacks ??= ResolveFireClayStacks(world)).Length > 0
              ? _fireClayStacks
              : null,
        },
      ];

    return baseHelp;
  }

  private static ItemStack[] ResolveFireClayStacks(IWorldAccessor world)
  {
    Item? clay = world.GetItem(FireClayCode);
    return clay != null
      ? [new ItemStack(clay, IwexValues.CanalSealClayCost)]
      : [];
  }
  #endregion

  protected static string[] PassOrEndOrientations(string variant) =>
    variant == "pass" ? ["ns", "we"] : ["ns", "we", "ew", "sn"];

  /// <summary>Counts how many horizontal neighbours have a canal connector facing this block.</summary>
  public int CountConnectedNeighborFaces(
    IBlockAccessor blockAccessor,
    BlockPos pos
  )
  {
    int count = 0;
    foreach (var face in BlockFacing.HORIZONTALS)
    {
      BlockPos nPos = pos.AddCopy(face);
      if (
        blockAccessor.GetBlock(nPos) is BlockMoltenCanal nCanal
        && nCanal.HasConnectorAt(face.Opposite)
      )
        count++;
    }
    return count;
  }

  /// <summary>
  /// Chooses the orientation from <paramref name="orientations"/> that best matches
  /// the canal neighbours around <paramref name="pos"/>, or <c>null</c> if none fit.
  /// </summary>
  public static string? PickBestOrientation(
    IBlockAccessor blockAccessor,
    BlockPos pos,
    string[] orientations
  )
  {
    var requiredFaces = BlockFacing
      .HORIZONTALS.Where(face =>
      {
        BlockPos nPos = pos.AddCopy(face);
        return blockAccessor.GetBlock(nPos) is BlockMoltenCanal nCanal
          && nCanal.HasConnectorAt(face.Opposite);
      })
      .Select(f => f.Code[0])
      .ToList();

    foreach (string orient in orientations)
    {
      if (requiredFaces.Count == 1 && orient.StartsWith(requiredFaces[0]))
        return orient;
      else if (
        requiredFaces.Count > 1
        && requiredFaces.TrueForAll(c => orient.Contains(c))
      )
        return orient;
    }

    return null;
  }
}
