using System;
using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkMolten;
using SteelmakingExpanded.BlockStructures.Converter.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using ExpandedLib.Metals;

namespace SteelmakingExpanded.BlockStructures.Converter.Blocks;

/// <summary>
/// The 3×3×3 converter vessel block. Construction is driven by the
/// RightClickConstructable block-entity behavior; this block scatters any
/// solidified charge when broken with an iron-tier pickaxe.
/// <para>
/// Implements <see cref="IFillerInteractionTarget"/> so a small hardened residue (one that
/// solidified mid-pour) can be chiselled out of the upper-rear <c>(0,1,1)</c> footprint cell with a
/// chisel + hammer, rather than forcing the player to break the whole vessel.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockConverterBessemer
  : Block,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider
{
  /// <summary>The blocktype base code ("converterbessemer") - the single source of truth the vessel is
  /// defined under (<see cref="Definitions"/>) and later resolved/animated by, so the control block that
  /// spawns it (<c>GetConverterBlock</c>) and the animator cache key can't silently drift from the code
  /// the block registers under. The multiblock/recipe/migration tables keep it literal by design (they
  /// are hand-curated match catalogues, and typing one code among their siblings would only skew them).</summary>
  public const string BaseCode = "converterbessemer";

  #region Code-first definition

  // The 3x3x3 footprint and the chisel-hatch offset, read at runtime from the block's own attributes
  // (file or injected def alike) - replacing the generated members so the values live once, in the def.
  public JsonObject? FillerOffsets => Attributes?["fillerOffsets"];
  public JsonObject? ChiselOffset => Attributes?["chiselOffset"];

  /// <summary>The Bessemer converter vessel blocktype, authored in C# (migrated from converter/bessemer.json).
  /// A control-spawned 3x3x3 mega-block: raised through a 7-stage right-click construction, it never drops
  /// itself and reserves its whole cube with invisible fillers. Not in creative (the control block spawns it).</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, BaseCode, "converter/bessemer")
        .Class<BlockConverterBessemer>()
        .EntityClass("smex.BlockEntityConverterBessemer")
        .Material(EnumBlockMaterial.Metal)
        .MetalSounds()
        .MiningTier(4)
        .Resistance(45.0f)
        .MaxStackSize(1)
        .NoDrops()
        .Attribute("chiselOffset", new { x = 0, y = 1, z = 0 })
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
                .Require("lpex:pipe-straight-ns-{metal}", 3, type: "block")
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
        .ShapeSpunPerOrientation("smex:converter/bessemer", 0)
        .ShapeSelectiveElements("Root/GearShaft/*")
        .SingleSelectionBox(-0.5f, -0.5f, -0.5f, 1.5f, 2f, 1.5f)
        .SingleCollisionBox(-0.5f, -0.5f, -0.5f, 1.5f, 2f, 1.5f)
        .NonSolid(),
    ];

  #endregion

  // RMB construction and its build prompts are routed to the
  // RightClickConstructable block-entity behaviour by the "BlockEntityInteract"
  // block behaviour declared in the block JSON.

  // The converter is welded plate over a refractory lining - breaking it needs an iron-tier pickaxe.
  // The actual mining requirement is enforced via "requiredMiningTier" in the
  // block JSON; here we just scatter whatever solidified charge it held.
  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    ItemStack? solidifiedDrops = null;
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityConverterBessemer be
    )
      solidifiedDrops = be.CollectBreakDrops();

    // Clear the reserved 3x3x3 filler volume. Done before base.OnBlockBroken so
    // that even if the construction-drop path below throws, the fillers are gone
    // and we don't leave invisible solid cells behind.
    int fillerAngle = ExOrientation.AngleFromSide(Variant["side"]);
    var fillerCells = StructureFillers.FootprintCells(this, pos, fillerAngle);
    StructureFillers.RemoveFillers(world, pos, fillerCells);

    // base.OnBlockBroken drives the RightClickConstructable behaviour, which
    // resolves each completed stage's ingredients back into drops. That path
    // expands wildcard codes (e.g. metalplate-*) using the wildcard values
    // captured at build time; a converter raised before "storeWildCard" was
    // added to the recipe has none stored, so vanilla GetDrops throws while
    // expanding the "*" - and because it runs before the block is cleared, the
    // exception escapes to the client and crashes the game. Guard it so a
    // legacy/corrupt construction state degrades to "no construction drops"
    // and the block is still removed.
    try
    {
      base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }
    catch (Exception e)
    {
      world.Logger.Warning(
        "[smex] Bessemer converter at {0} could not drop its construction "
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

  // The vessel is control-spawned, never placed from an item, so it must NOT drop itself when broken:
  // the construction materials come from the RightClickConstructable behaviour and the solidified
  // charge is spawned by OnBlockBroken above. The JSON "drops": [] declares this, but it is not always
  // honoured for a variant block (the per-side variant can still be handed its own code as a fallback
  // drop at registration), which leaves the vessel dropping itself alongside the materials. Enforce the
  // empty drop list here so the suppression can't be bypassed. (Block behaviours can still contribute
  // their own drops via base.GetDrops; the converter has none, so an empty list is correct.)
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) => [];

  #region Chisel-out interaction (IFillerInteractionTarget)

  // Every footprint cell forwards interaction to this principal block. We single out the (0,1,1)
  // hatch cell for the chisel-out and forward everything else to the normal construction handling.
  public bool OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  )
  {
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
  )
  {
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
        MoltenChisel.ChiselHelp(world, "smex:blockhelp-bessemer-chiselresidue"),
      ];

    return baseHelp;
  }

  private bool IsChiselCell(BlockPos principalPos, BlockPos clickedCell)
  {
    BlockPos chiselCell = ExOrientation.WorldPosFromAttr(
      principalPos,
      ChiselOffset,
      new Vec3i(),
      ExOrientation.AngleFromSide(Variant["side"])
    );
    return clickedCell.Equals(chiselCell);
  }

  // Chisel in hand + hammer in the off-hand chips a hardened residue out of the vessel via the shared
  // MoltenChisel ritual. Returns true when this owns the click (chiselled, or claimed-but-not-ready with
  // a "too hot"/"too full" message), so it isn't forwarded to construction handling.
  private bool TryChiselOut(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos principalPos
  )
  {
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
