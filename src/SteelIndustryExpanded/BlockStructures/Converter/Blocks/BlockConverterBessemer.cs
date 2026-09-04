using System;
using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockNetworkMolten;
using SteelIndustryExpanded.BlockStructures.Converter.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace SteelIndustryExpanded.BlockStructures.Converter.Blocks;

/// <summary>
/// The 3×3×3 converter vessel block. Construction is driven by the RightClickConstructable
/// block-entity behavior; this block scatters any solidified charge when broken with an iron-tier
/// pickaxe. As an <see cref="IFillerInteractionTarget"/> it also lets a small hardened residue be
/// chiselled out of the hatch footprint cell (<c>chiselOffset</c>) with a chisel and hammer instead
/// of breaking the whole vessel.
/// </summary>
[BlockRegister]
public partial class BlockConverterBessemer
  : Block,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider {
  /// <summary>The blocktype base code the vessel registers under. Shared by <see cref="Definitions"/>,
  /// the control block's converter lookup and the animator cache key, so the three cannot drift apart.
  /// The multiblock, recipe and migration tables spell the code literally.</summary>
  public const string BaseCode = "converterbessemer";

  #region Code-first definition

  // The 3x3x3 footprint and the chisel-hatch offset, read at runtime from the block's own attributes.
  public JsonObject? FillerOffsets => Attributes?["fillerOffsets"];
  public JsonObject? ChiselOffset => Attributes?["chiselOffset"];

  /// <summary>The Bessemer converter vessel blocktype. A control-spawned 3x3x3 mega-block: raised
  /// through a 7-stage right-click construction, it never drops itself and reserves its whole cube with
  /// invisible fillers. Absent from creative, since the control block spawns it.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, BaseCode, "converter/bessemer")
        .Class<BlockConverterBessemer>()
        .EntityClass<BlockEntityConverterBessemer>()
        .Material(EnumBlockMaterial.Metal)
        .MetalSounds()
        .MiningTier(4)
        .Resistance(45.0f)
        .MaxStackSize(1)
        .NoDrops()
        .Attribute(
          "chiselOffset",
          new
          {
            x = 0,
            y = 1,
            z = 0,
          }
        )
        // The full 3x3x3 cube (x/y/z in -1..1) around the principal, minus the origin (the vessel cell) and
        // the upper-rear-left (-1,1,0) gap. Drawn as three floor plans (rows +Z, cols +X, top-left = (-1,-1)).
        .FillerOffsets(
          StructureFootprint.Layout(f =>
            f.Origin(-1, -1)
              .Layer(
                -1,
                """
                # # #
                # # #
                # # #
                """
              )
              .Layer(
                0,
                """
                # # #
                # O #
                # # #
                """
              )
              .Layer(
                1,
                """
                # # #
                . # #
                # # #
                """
              )
          )
        )
        .Behavior("ExOrientable")
        .Behavior("BlockEntityInteract")
        .EntityBehavior("Animatable")
        .Construction(c =>
          c.Stage(s => s.AddElements("Root/GearShaft"))
            .Stage(s =>
              s.RequireMetalPlate(domain, 24)
                .RequireMetalNails(domain, 24)
                .RequireMetalRod(domain, 12)
                .AddElements("Root/BottomIron")
            )
            .Stage(s =>
              s.RequireMetalPlate(domain, 4)
                // The trailing-star wildcard the three gas-intake recipes use, rather than one
                // orientation: a segment carries the orientation it was placed at, so an exact code
                // would refuse a player holding the same pipe the other way round.
                .Require(
                  "iiex:pipe-cast-straight*",
                  3,
                  $"{domain}:rcc-ingredient-pipe",
                  "block"
                )
                .RequireMetalNails(domain, 6)
                .AddElements("Root/GasIntake")
            )
            .Stage(s =>
              s.Require("refractorybrick-fired-tier3", 60)
                .Require("game:clay-fire", 48)
                .AddElements("Root/BottomRefractory")
            )
            .Stage(s =>
              s.Require("refractorybrick-fired-tier3", 24)
                .Require("game:clay-fire", 24)
                .AddElements("Root/UpRefractory")
            )
            .Stage(s =>
              s.RequireMetalPlate(domain, 12)
                .RequireMetalNails(domain, 12)
                .RequireMetalRod(domain, 6)
                .AddElements("Root/UpIron")
            )
            .Stage(s =>
              s.Require("game:clay-fire", 12).AddElements("Root/InputLining")
            )
        )
        .SideVariant()
        .ShapeSpunPerOrientation("siex:converter/bessemer", 0)
        .ShapeSelectiveElements("Root/GearShaft/*")
        .SingleSelectionBox(-0.5f, -0.5f, -0.5f, 1.5f, 2f, 1.5f)
        .SingleCollisionBox(-0.5f, -0.5f, -0.5f, 1.5f, 2f, 1.5f)
        .NonSolid(),
    ];

  #endregion

  // Right-click construction and its build prompts are routed to the RightClickConstructable
  // block-entity behaviour by the "BlockEntityInteract" block behaviour declared above.

  // The mining tier that breaking the vessel requires is enforced by the block definition; this
  // override only scatters whatever solidified charge it held.
  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) {
    ItemStack? solidifiedDrops = null;
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityConverterBessemer be
    )
      solidifiedDrops = be.CollectBreakDrops();

    // base.OnBlockBroken drives the RightClickConstructable behaviour, which resolves each completed
    // stage's ingredients back into drops by expanding wildcard codes (metalplate-*) against the
    // values captured at build time. A vessel saved without those values makes vanilla GetDrops
    // throw, and the exception would otherwise escape to the client. The guard degrades that state
    // to no construction drops and still removes the block.
    try {
      base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    } catch (Exception e) {
      world.Logger.Warning(
        "[siex] Bessemer converter at {0} could not drop its construction "
          + "materials (likely built before the recipe fix); removing it "
          + "anyway. {1}",
        pos,
        e
      );
      if (world.BlockAccessor.GetBlock(pos) == this)
        world.BlockAccessor.SetBlock(0, pos);
    }

    if (solidifiedDrops != null && world.Side == EnumAppSide.Server)
      world.SpawnItemEntity(solidifiedDrops, pos.ToVec3d().Add(0.5, 0.5, 0.5));
  }

  public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos) {
    // Runs on every removal path (a player break, an explosion, a worldedit delete), unlike
    // OnBlockBroken, so the reserved 3x3x3 filler volume is never left behind.
    int fillerAngle = ExOrientation.AngleFromSide(Variant["side"]);
    StructureFillers.RemoveFillers(
      world,
      pos,
      StructureFillers.FootprintCells(this, pos, fillerAngle)
    );
    base.OnBlockRemoved(world, pos);
  }

  // The vessel is control-spawned and never placed from an item, so it must not drop itself: the
  // construction materials come from the RightClickConstructable behaviour and the solidified charge
  // is spawned by OnBlockBroken above. The declared empty drop list is not always honoured for a
  // variant block, which can be handed its own code as a fallback drop at registration, so the empty
  // list is enforced here. Block behaviours could contribute drops through base.GetDrops; the
  // converter has none.
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) => [];

  #region Chisel-out interaction (IFillerInteractionTarget)

  // Every footprint cell forwards interaction to this principal block. Only the hatch cell at
  // chiselOffset takes the chisel-out; every other cell falls through to construction handling.
  public bool OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) {
    if (
      IsChiselCell(principalSel.Position, clickedCell)
      && TryChiselOut(world, byPlayer, principalSel.Position)
    )
      return true;
    return OnBlockInteractStart(world, byPlayer, principalSel);
  }

  public bool OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => OnBlockInteractStep(secondsUsed, world, byPlayer, principalSel);

  public void OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => OnBlockInteractStop(secondsUsed, world, byPlayer, principalSel);

  public WorldInteraction[] GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) {
    WorldInteraction[] baseHelp =
      GetPlacedBlockInteractionHelp(world, principalSel, forPlayer) ?? [];

    // The chisel hint shows only on the hatch cell, and only once the residue is small + hardened.
    if (
      IsChiselCell(principalSel.Position, clickedCell)
      && world.BlockAccessor.GetBlockEntity(principalSel.Position)
        is BlockEntityConverterBessemer be
      && be.CanChiselOut()
    )
      return
      [
        .. baseHelp,
        MoltenChisel.ChiselHelp(world, "siex:blockhelp-bessemer-chiselresidue"),
      ];

    return baseHelp;
  }

  private bool IsChiselCell(BlockPos principalPos, BlockPos clickedCell) {
    BlockPos chiselCell = ExOrientation.WorldPosFromAttr(
      principalPos,
      ChiselOffset,
      new Vec3i(),
      ExOrientation.AngleFromSide(Variant["side"])
    );
    return clickedCell.Equals(chiselCell);
  }

  // A chisel in hand and a hammer in the off-hand chip a hardened residue out of the vessel through
  // the shared MoltenChisel sequence. Returns true when the click is owned here (chiselled, or
  // claimed but not ready with a "too hot"/"too full" message) so it is not forwarded to construction.
  private bool TryChiselOut(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos principalPos
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(principalPos)
      is not BlockEntityConverterBessemer be
    )
      return false;

    return MoltenChisel.TryChisel(
        world,
        byPlayer,
        principalPos,
        be,
        ExSounds.StoneCrush
      ) != ChiselOutcome.NotChiseling;
  }

  #endregion

#if !GAME_GE_1_22
  // Legacy lacks the vanilla IInteractableWithHelp path, so surface the construction help here.
  public override Vintagestory.API.Client.WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) =>
    ExpandedLib.Blocks.Construction.ExRightClickConstructable.AppendConstructionHelp(
      world,
      selection,
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer)
    );
#endif
}
