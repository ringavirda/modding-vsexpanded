using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The puddling furnace's chimney cap - the damper that regulates its natural draught, on a control rod
/// running down the stack. Two cells deep: the cap body sits over the flue with its housing behind, so
/// the footprint runs along the structure's local +Z rather than sideways.
/// </summary>
[BlockRegister]
public partial class BlockPuddlingChimneyCap
  : BlockFilledMegastructure,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider {
  #region Code-first definition

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(
          domain,
          BlockFurnaceCoreBase.FurnaceCode,
          "furnace/puddlingchimneycap"
        )
        .Class<BlockPuddlingChimneyCap>()
        .EntityClass<BlockEntityPuddlingChimneyCap>()
        .EntityBehavior("Animatable")
        .Material(EnumBlockMaterial.Metal)
        .MaxStackSize(1)
        .Behavior("MultiblockStructure")
        .Behavior("ExOrientable")
        // `type` names the family member; every furnace part shares the code `iiex:furnace`
        // (see BlockFurnaceCoreBase.FurnaceCode).
        .VariantGroup("type", "puddlingchimneycap")
        .SideVariant()
        .ShapeByTypePerOrientation("iiex:furnace/puddlingchimneycap", 0)
        .CreativeCommon("*-n")
        // One filler at local +Z: the cap's housing. Matches the shape's extent
        // (x -2..16 = one cell wide, z -5..32 = two deep).
        .FillerOffsets(
          StructureFootprint.Layout(f =>
            f.Origin(0, 0)
              .Layer(
                0,
                """
                0
                #
                """
              )
          )
        )
        .Replaceable(400)
        .Resistance(4f)
        .LightAbsorption(0)
        .Sound("walk", "walk/metal")
        .Sound("place", "block/anvil")
        .SoundByTool(
          EnumTool.Pickaxe,
          "block/rock-hit-pickaxe",
          "block/rock-break-pickaxe"
        )
        .SolidNonOpaque(),
    ];

  #endregion

  public override int StructureAngle =>
    ExOrientation.AngleFromSide(Variant["side"]);

  #region Interaction

  private bool HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos principal
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(principal)
      is not BlockEntityPuddlingChimneyCap be
    )
      return false;
    if (
      BlockBehaviorMultiblockStructure.TryToggleProjection(
        world,
        byPlayer,
        principal
      )
    )
      return true;
    if (world.Side == EnumAppSide.Server)
      be.Toggle();
    return true;
  }

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) => HandleInteract(world, byPlayer, blockSel.Position);

  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => HandleInteract(world, byPlayer, principalSel.Position);

  bool IFillerInteractionTarget.OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => false;

  void IFillerInteractionTarget.OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) { }

  WorldInteraction[] IFillerInteractionTarget.GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) => CapHelp();

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) => CapHelp();

  private static WorldInteraction[] CapHelp() =>
    [
      new()
      {
        ActionLangCode = "iiex:chimneycap-help-toggle",
        MouseButton = EnumMouseButton.Right,
      },
    ];

  #endregion
}
