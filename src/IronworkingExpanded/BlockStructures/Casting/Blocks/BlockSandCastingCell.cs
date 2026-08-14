using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Casting.BlockEntities;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Casting.Blocks;

/// <summary>
/// The 1×1 sand casting cell: a hollow brick shell placed empty, rammed with sand and impressed with a
/// wooden pattern, then filled from a canal on its launder face. Wires the code-first definition and the
/// hosted <see cref="ExpandedLib.Blocks.Structures.BEBehaviorMoltenCell"/>, and routes right-clicks to
/// <see cref="BlockEntitySandCastingCell.OnInteract"/>.
/// </summary>
[BlockRegister]
public partial class BlockSandCastingCell : Block, IExBlockDefProvider {
  #region Code-first definition

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Cell(domain)];

  private static ExBlockDef Cell(string domain) =>
    ExBlockDef
      .Create(domain, "casting-sandcell", "casting/sandcell")
      .Class<BlockSandCastingCell>()
      .EntityClass<BlockEntitySandCastingCell>()
      .Material(EnumBlockMaterial.Ceramic)
      .MiningTier(0)
      .Resistance(3.5f)
      .MaxStackSize(16)
      .Behavior("ExOrientable")
      // The cast is held on a drain-fitting molten cell, which is not registered on the molten network.
      // The entity pulls into it from the launder face and overrides its capacity from the impressed
      // pattern.
      .EntityBehavior<BEBehaviorMoltenCell>(
        new JObject { ["capacity"] = 200, ["drainFitting"] = true }
      )
      // The canal's 8-colour brick group, fire first so it is the default variant, plus the horizontal
      // facing. Declaration order brick-then-side puts the variants in that order in the block code.
      .VariantGroup(
        "brick",
        "fire",
        "black",
        "brown",
        "cream",
        "gray",
        "orange",
        "red",
        "tan"
      )
      .SideVariant()
      .ShapeSpunPerOrientation("iwex:casting/sandcastingcell")
      // Brick colour is a tint overlay over the running-bond base (the canal/bed pattern). The filling
      // shapes draw the rammed sand against the `andesite` key, so that key maps to the green-sand
      // texture.
      .Texture(
        "fire1",
        "game:block/clay/brick/four/running/cream1",
        "game:block/clay/brick/four/running/{brick}1"
      )
      .Texture("burned", "game:block/clay/vessel/sides/burned")
      .Texture("andesite", GreenSandItemDefinitions.Texture)
      .CreativeTab("general", "*-n")
      .CreativeTab("iwex", "*-n")
      .SingleSelectionBox(0f, 0f, 0f, 1f, 1f, 1f)
      .SingleCollisionBox(0f, 0f, 0f, 1f, 0.875f, 1f)
      .SideSolid(false)
      .SideOpaque(false)
      .Sound("place", "game:block/ceramicplace")
      .Sound("break", "game:block/ceramic")
      .Sound("hit", "game:block/ceramic")
      .Sound("walk", "game:walk/stone");

  #endregion

  #region Interaction

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
        is BlockEntitySandCastingCell cell
      && cell.OnInteract(byPlayer)
    )
      return true;
    return base.OnBlockInteractStart(world, byPlayer, blockSel);
  }

  #endregion
}
