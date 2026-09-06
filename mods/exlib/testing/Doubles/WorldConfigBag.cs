using Vintagestory.API.Datastructures;

namespace ExpandedLib.Testing;

/// <summary>
/// The world-config tree a real <c>IWorldAccessor.Config</c> answers - a real
/// <see cref="TreeAttribute"/>, not a fake, so a behaviour that reads or writes a world-config key
/// round-trips exactly as it does in game. <see cref="TestWorld"/> wires <see cref="Tree"/> as
/// <c>World.Config</c>.
/// </summary>
public sealed class WorldConfigBag {
  /// <summary>The backing tree. Empty until a test or the code under test populates it.</summary>
  public ITreeAttribute Tree { get; } = new TreeAttribute();
}
