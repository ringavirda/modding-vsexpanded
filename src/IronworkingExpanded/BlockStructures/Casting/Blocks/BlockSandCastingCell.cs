using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Casting.BlockEntities;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Casting.Blocks;

/// <summary>
/// The 1×1 sand casting cell block: a hollow brick shell placed empty, then rammed with sand and impressed
/// with a wooden pattern to cast a part from molten metal drawn off a canal on its launder face. The block
/// wires the code-first definition, the hosted <see cref="BEBehaviorMoltenCell"/>, and routes every
/// right-click through to <see cref="BlockEntitySandCastingCell.OnInteract"/>.
/// </summary>
[BlockRegister]
public partial class BlockSandCastingCell : Block, IExBlockDefProvider
{
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
      // The cell holds its cast on a drain-fitting molten cell (not network-registered); the entity pulls
      // into it from the launder face and overrides its capacity from the impressed pattern.
      .EntityBehavior(
        "exlib.BEBehaviorMoltenCell",
        new JObject { ["capacity"] = 200, ["drainFitting"] = true }
      )
      // The fired-brick colour (the canal's 8-state group, fire first = default so the original
      // fire-brick look survives the migration and the fire-brick recipe has a target) plus the
      // horizontal facing. Declaration order brick-then-side => code sandcastingcell-{brick}-{side}.
      .VariantGroup("brick", "fire", "black", "brown", "cream", "gray", "orange", "red", "tan")
      .SideVariant()
      .ShapeSpunPerOrientation("iwex:casting/sandcastingcell")
      // Brick colour is a tint overlay over the running-bond base (the canal/bed pattern). The `andesite`
      // key is the rammed sand the filling shapes are drawn with - named for the rock type the cell once
      // defaulted to, back when it took any sand and remapped this key per-BE. With one prepared molding
      // sand there is nothing to remap, so it simply is the green-sand texture and the shapes (which still
      // declare the historical key) resolve straight through.
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
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntitySandCastingCell cell
      && cell.OnInteract(byPlayer)
    )
      return true;
    return base.OnBlockInteractStart(world, byPlayer, blockSel);
  }

  #endregion
}
