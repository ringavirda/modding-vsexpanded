using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using PipesAndPowerExpanded.BlockNetworkPipe.Blocks;
using Vintagestory.API.Common;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

[BlockRegister]
public partial class BlockHopperTall : BlockPipe, IExBlockDefProvider
{
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "hopper-tall", "furnaces/hopper-tall")
        .Class<BlockHopperTall>()
        .EntityClass("iwex.BlockEntityHopperTall")
        .Material(EnumBlockMaterial.Ceramic)
        .MaxStackSize(1)
        .FillerOffsets(
          StructureFootprint.Layout(f =>
            f.Origin(0, 1)
              .Slice(
                0,
                """
                #
                0
                """
              )
          )
        )
        .SolidNonOpaque(),
    ];
}
