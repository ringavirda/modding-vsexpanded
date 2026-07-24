using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkPipe.BlockEntities;

namespace LowPressureExpanded.BlockNetworkPipe.BlockEntities;

/// <summary>Block entity for the gas outlet; a plain pipe node, used as a gas producer by adjacent machines.</summary>
[BlockEntityRegister]
public class BlockEntityPipeOutlet : BlockEntityPipe { }
