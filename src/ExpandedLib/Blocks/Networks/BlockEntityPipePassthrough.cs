using ExpandedLib.Registries.Entities;

namespace ExpandedLib.Blocks.Networks;

/// <summary>
/// Block entity for the pipe passthrough: a plain pipe node, used as a gas consumer by adjacent
/// machines. Registered by exlib, and every tier's passthrough binds to that one key
/// (<c>exlib.BlockEntityPipePassthrough</c>), as the pipe segments do.
/// </summary>
[BlockEntityRegister]
public class BlockEntityPipePassthrough : BlockEntityPipe { }
