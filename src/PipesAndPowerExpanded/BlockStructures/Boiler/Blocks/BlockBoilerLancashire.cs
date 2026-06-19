using ExpandedLib.Blocks.Structures;
using ExpandedLib.Registries.Entities;

namespace PipesAndPowerExpanded.BlockStructures.Boiler.Blocks;

/// <summary>
/// The Lancashire boiler mega-block (steel, high-pressure tier). All behavior lives
/// in <see cref="BlockBoiler"/>.
/// </summary>
[BlockRegister]
public partial class BlockBoilerLancashire
  : BlockBoiler,
    IFillerHost,
    IBoilerGeometry { }
