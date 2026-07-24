using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkPipe.BlockEntities;

namespace LowPressureExpanded.BlockNetworkPipe.BlockEntities;

/// <summary>Block entity for the gas passthrough; a plain pipe node, used as a gas consumer by adjacent machines.</summary>
[BlockEntityRegister]
public class BlockEntityPipePassthrough : BlockEntityPipe { }
