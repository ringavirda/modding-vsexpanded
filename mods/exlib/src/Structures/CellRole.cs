using System;
using System.Collections.Generic;

namespace ExpandedLib.Structures;

/// <summary>
/// What a multiblock layout cell is for, as opposed to what block may occupy it: the layout records the
/// code per cell, a role records the purpose, so a machine can ask its own drawing where its tuyeres are.
/// Roles attach to glyphs rather than codes, since one code serves several purposes in one layout
/// (<c>game:air</c> is vent shaft, flue column and tap alcove); a cell holds exactly one glyph, a glyph
/// may carry several roles, and distinct roles accumulate rather than overwrite. A role exists only where
/// code asks the layout which cells are its X cells - a block that finds its own core looks up the other
/// way, through <see cref="BlockEntityMultiblockStructure.FindAnchorOwning{T}"/>. An unmarked role is a
/// set of any size; see <see cref="IsSingle"/> and docs/design/mechanics/multiblock.md.
/// <para>
/// A cell role is a string key declared by the mod that owns the machine; exlib declares none. Equality
/// and hashing are by <see cref="Key"/> alone, so two roles minted independently from the same key, by
/// two different mods, are the same role.
/// </para>
/// </summary>
public readonly record struct CellRole(string Key) {
  // Whether each key was last minted single-cell. The owner of a key declares it once; a later
  // conflicting declaration is not diagnosed here - it is not exlib's place to police one mod's own
  // naming discipline - and simply wins, since it is the most recent word on that key.
  private static readonly Dictionary<string, bool> _single = new(
    StringComparer.Ordinal
  );

  /// <summary>
  /// Builds the role named <paramref name="key"/>, single-cell when <paramref name="single"/> is true: a
  /// layout may give it at most one cell, so a consumer may read the runtime answer as a point rather
  /// than a set (enforced by <c>MultiblockLayoutBuilder.Build()</c>). Throws
  /// <see cref="ArgumentException"/> when <paramref name="key"/> is null, empty or all whitespace.
  /// <para>
  /// A declaration, not a lookup: a caller that merely wants the <see cref="CellRole"/> value for a key
  /// someone else already declared - reading a role back out of JSON, say - must use the primary
  /// constructor (<c>new CellRole(key)</c>) instead, or it will silently reset that key's arity to a
  /// plain set the next time it runs.
  /// </para>
  /// </summary>
  public static CellRole Of(string key, bool single = false) {
    if (string.IsNullOrWhiteSpace(key))
      throw new ArgumentException("A cell role key cannot be blank.", nameof(key));
    lock (_single)
      _single[key] = single;
    return new CellRole(key);
  }

  /// <summary>
  /// Whether this role was declared <see cref="Of">single-cell</see>. A layout that declares it declares
  /// exactly one cell for it; a layout may still declare none, so the consumer handles empty.
  /// </summary>
  public bool IsSingle {
    get {
      lock (_single)
        return _single.TryGetValue(Key, out bool single) && single;
    }
  }

  /// <summary>The role's key, exactly as declared - what an emitted <c>multiblockRoles</c> table names
  /// it.</summary>
  public override string ToString() => Key;
}

/// <summary>Facts about <see cref="CellRole"/> that both the layout builder and its consumers read.</summary>
public static class CellRoles {
  /// <summary>
  /// Whether <paramref name="role"/> was declared <see cref="CellRole.IsSingle">single-cell</see>: a
  /// layout that declares it declares exactly one cell for it, so a consumer may read the answer as a
  /// point. A layout may still declare none, so the consumer handles empty.
  /// </summary>
  public static bool IsSingleCell(CellRole role) => role.IsSingle;
}
