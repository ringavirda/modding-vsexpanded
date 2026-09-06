using ExpandedLib.Networks;

namespace ExpandedLib.Testing;

/// <summary>
/// The smallest concrete <see cref="BlockEntityNetworkNode"/>: everything on the base class, one
/// settable network type, nothing else. <see cref="CapturingNode"/> deliberately implements
/// <c>INetworkNode</c> directly instead, so it cannot stand in here -
/// <c>BlockNetworkNode.RecalculateAndSyncOrientations</c> returns early for any block entity that is
/// not this class.
/// </summary>
public sealed class OrientableNode : BlockEntityNetworkNode {
  public override string NetworkType { get; set; } = "test";
}
