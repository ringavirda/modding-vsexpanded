// Shims that re-expose current-version VintageStory API members on the older game APIs the mods
// can also target. Guarded by !GAME_GE_1_22 (game version < 1.22), so they compile only where the
// real members are absent and the primary build sees none of this. (Bump the threshold alongside
// the manifest in src/Directory.Build.props if the member set the mods rely on shifts versions.)
//
// Named "Legacy" rather than "Compat" on purpose: "compat" already denotes the inter-mod
// compatibility patches under assets/.../patches/compat/ (e.g. ExpandedMetals). This file
// is about backwards game-version compatibility, a different concern.
//
// Each member below maps a 1.22 name/shape onto the equivalent on the older surface,
// verified by decompiling the 1.20/1.21 VintagestoryAPI.dll:
//   * GridRecipe.ResolvedIngredients (1.22 property)        -> resolvedIngredients (field)
//   * CraftingRecipeIngredient.ResolvedItemStack (1.22)     -> ResolvedItemstack (old casing)
//   * EvolvingNatFloat is a struct in 1.22 (so EvolvingNatFloat? exposes Nullable's
//     HasValue/Value); on the old APIs it is a class, so re-expose those names.
//   * MultiblockStructure.TransformedOffsets became public in 1.21; in 1.20 it is a
//     private field populated by InitForUse() and must be read reflectively.
#if !GAME_GE_1_22
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Legacy;

/// <summary>
/// C# 14 extension members that fill in API members missing from the pre-1.22 game surface,
/// so the mod code (written against 1.22) compiles unchanged on the legacy target frameworks.
/// </summary>
public static class LegacyApi
{
  extension(GridRecipe recipe)
  {
    /// <summary>1.22 renamed the <c>resolvedIngredients</c> field to this property.</summary>
    public CraftingRecipeIngredient[] ResolvedIngredients =>
      recipe.resolvedIngredients;
  }

  extension(CraftingRecipeIngredient ingredient)
  {
    /// <summary>1.22 re-cased <c>ResolvedItemstack</c> to <c>ResolvedItemStack</c>.</summary>
    public ItemStack ResolvedItemStack
    {
      get => ingredient.ResolvedItemstack;
      set => ingredient.ResolvedItemstack = value;
    }
  }

  extension(EvolvingNatFloat? evolve)
  {
    /// <summary>On the old (class) <see cref="EvolvingNatFloat"/>, mirror the
    /// <see cref="System.Nullable{T}"/> surface the 1.22 (struct) form provides.</summary>
    public bool HasValue => evolve != null;

    /// <summary>The non-null value; matches Nullable's accessor on 1.22.</summary>
    public EvolvingNatFloat Value => evolve!;
  }

  extension(MultiblockStructure structure)
  {
    /// <summary>On the 1.20.0 and 1.21.0 floors the transformed-offsets list is a private field
    /// (only valid after <c>InitForUse()</c> has run); read it reflectively. It became public in
    /// 1.22 (and later 1.20.x/1.21.x patches) - hence the whole-legacy <c>!GAME_GE_1_22</c> guard
    /// on this file, and the Public+NonPublic lookup so the read still works when a mod built
    /// against the floor runs on a patched build where the field is public.</summary>
    public List<BlockOffsetAndNumber>? TransformedOffsets =>
      (List<BlockOffsetAndNumber>?)TransformedOffsetsField.GetValue(structure);
  }

  private static readonly FieldInfo TransformedOffsetsField =
    typeof(MultiblockStructure).GetField(
      "TransformedOffsets",
      BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance
    )!;
}
#endif
