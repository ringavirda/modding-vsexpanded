using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using PipesAndPowerExpanded.BlockNetworkPipe.Blocks;
using Vintagestory.API.Common;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

[BlockRegister]
public partial class BlockTwinTubMpBlower : BlockPipe, IExBlockDefProvider
{
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "twintubmpblower", "furnaces/twintubmpblower")
        .Class<BlockTwinTubMpBlower>()
        .EntityClass("iwex.BlockEntityTwinTubMpBlower")
        .Material(EnumBlockMaterial.Ceramic)
        .MaxStackSize(1)
        .FillerOffsets(
          StructureFootprint.Layout(f =>
            f.Origin(0, 1)
              .Slice(
                0,
                // (y=1,z=0) is the mp connector filler. In default orientation
                // (north) it has mp connection on west face only.
                // (y=0,z=2) is pipe connector filler. In default orientation
                // (north) it has pipe connection on north face only.
                """
                ###
                0##
                """
              )
          )
        )
        .SolidNonOpaque(),
    ];
}
