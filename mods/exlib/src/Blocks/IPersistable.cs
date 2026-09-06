using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Blocks;

/// <summary>
/// A value that writes itself into a sub-tree; a <see cref="PersistAttribute"/> member of this type is
/// stored under its key as a nested tree, the same shape <c>ExBlockState.Tree</c> gives a hand-declared
/// field with its own multi-attribute layout.
/// </summary>
public interface IPersistable {
  /// <summary>Writes this value's attributes into <paramref name="tree"/>, a tree private to this member.</summary>
  void ToTree(ITreeAttribute tree);

  /// <summary>Reads this value's attributes back out of <paramref name="tree"/>, mutating in place.</summary>
  void FromTree(ITreeAttribute tree, IWorldAccessor world);
}
