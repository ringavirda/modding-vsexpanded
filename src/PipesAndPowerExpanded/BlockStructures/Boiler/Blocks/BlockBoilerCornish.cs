using ExpandedLib.Blocks.Structures;
using ExpandedLib.Registries.Entities;

namespace PipesAndPowerExpanded.BlockStructures.Boiler.Blocks;

/// <summary>
/// The Cornish boiler mega-block (iron, low-pressure entry tier). All behavior lives
/// in <see cref="BlockBoiler"/>.
/// </summary>
[BlockRegister]
public partial class BlockBoilerCornish
  : BlockBoiler,
    IFillerHost,
    IBoilerGeometry { }
