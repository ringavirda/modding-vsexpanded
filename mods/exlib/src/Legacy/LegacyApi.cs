// Shims that re-expose current-version VintageStory API members on the older game APIs the mods can
// also target. Each member maps a 1.22 name or shape onto its equivalent on the older surface.
// Guarded by !GAME_GE_1_22 (game version < 1.22), so they compile only where the real members are
// absent and the primary build sees none of this. The threshold moves with the manifest in
// mods/Directory.Build.props when the member set the mods rely on shifts versions.
//
// "Legacy" here means backwards game-version compatibility; "compat" denotes the inter-mod
// compatibility patches under assets/.../patches/compat/.
#if !GAME_GE_1_22
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Legacy;

/// <summary>
/// C# 14 extension members that fill in API members missing from the pre-1.22 game surface, so mod
/// code written against 1.22 compiles unchanged on the legacy target frameworks.
/// </summary>
public static class LegacyApi {
  extension(GridRecipe recipe) {
    /// <summary>The 1.22 property name for the <c>resolvedIngredients</c> field.</summary>
    public CraftingRecipeIngredient[] ResolvedIngredients =>
      recipe.resolvedIngredients;

    /// <summary>The 1.22 overload, which takes the world explicitly; the older surface reaches the
    /// same world through the player, so the argument is redundant here. Arity separates this from
    /// the three-argument instance method it forwards to.</summary>
    public bool Matches(
      IPlayer forPlayer,
      IWorldAccessor world,
      ItemSlot[] ingredients,
      int gridWidth
    ) => recipe.Matches(forPlayer, ingredients, gridWidth);
  }

  extension(CraftingRecipeIngredient ingredient) {
    /// <summary>The 1.22 casing of <c>ResolvedItemstack</c>.</summary>
    public ItemStack ResolvedItemStack {
      get => ingredient.ResolvedItemstack;
      set => ingredient.ResolvedItemstack = value;
    }
  }

  extension(EvolvingNatFloat? evolve) {
    /// <summary><see cref="EvolvingNatFloat"/> is a class here and a struct on 1.22, so these mirror
    /// the <see cref="System.Nullable{T}"/> surface the struct form exposes.</summary>
    public bool HasValue => evolve != null;

    /// <summary>The non-null value, matching Nullable's accessor on 1.22.</summary>
    public EvolvingNatFloat Value => evolve!;
  }

  extension(MultiblockStructure structure) {
    /// <summary>The transformed-offsets list, valid only after <c>InitForUse()</c> has run. On the
    /// 1.20.0 and 1.21.0 floors it is a private field and is read reflectively; the lookup covers
    /// Public as well so the read still works on a patched build where the field is public.</summary>
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
