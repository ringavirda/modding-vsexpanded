using ExpandedLib.Registries.Entities;

namespace ExpandedLib.Blocks.Networks;

/// <summary>
/// Block entity for the pipe passthrough; a plain pipe node, used as a gas consumer by adjacent machines.
/// <para>
/// Registered by <b>exlib</b>, and every tier's passthrough binds to that one key
/// (<c>exlib.BlockEntityPipePassthrough</c>) - the same arrangement the pipe segments already use. A
/// passthrough is a pipe run through a brick wall, so it belongs to whichever mod owns the pipe base
/// rather than to a tier.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityPipePassthrough : BlockEntityPipe { }
