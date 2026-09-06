using System.Collections.Generic;
using ExpandedLib.Blocks;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace HelloExpanded;

/// <summary>
/// The whole "your first block" walk: one code-first definition (vanilla shape and texture, no JSON
/// asset authored), and a sneak-click that resets its block entity's counter through
/// <see cref="ExInteraction"/> rather than a hand-rolled click guard.
/// </summary>
[BlockRegister]
public class BlockHello : Block, IExBlockDefProvider {
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "hello")
        .Class<BlockHello>()
        .EntityClass<BlockEntityHello>()
        .Material(EnumBlockMaterial.Stone)
        .Shape("game:block/basic/cube")
        .TextureAll("survival:block/stone/rock/granite*")
        .MetalSounds()
        .Resistance(3.0f)
        .MiningTier(1)
        .CreativeCommon("*")
        .SideVariant()
        .Behavior<BlockBehaviorExOrientable>(),
    ];

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is not BlockEntityHello be
    )
      return base.OnBlockInteractStart(world, byPlayer, blockSel);

    Interaction interaction = ExInteraction.Of(world, byPlayer, blockSel);
    if (!interaction.Sneaking)
      return base.OnBlockInteractStart(world, byPlayer, blockSel);

    if (interaction.IsClient)
      return true;

    be.ResetTicks();
    return true;
  }
}
