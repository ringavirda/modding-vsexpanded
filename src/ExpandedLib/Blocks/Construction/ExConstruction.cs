// Legacy-only port of the vanilla 1.22 right-click construction subsystem
// (RightClickConstruction + ConstructionStage + ConstructionIngredient), which does not exist in
// 1.20/1.21. Mirrors the vanilla logic the mega-blocks rely on - staged material consumption, shape
// SelectiveElements per stage, and salvage drops - minus the interaction-help tooltip
// (IInteractableWithHelp is 1.22-only) and the place sounds (version-divergent PlaySoundAt overloads).
// Not compiled on 1.22, where the mods use the vanilla classes through ExRightClickConstructable.
#if !GAME_GE_1_22
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Construction;

/// <summary>One construction stage: shape elements it adds/removes and the materials it needs.</summary>
public class ExConstructionStage {
  public string[]? AddElements;
  public string[]? RemoveElements;
  public ExConstructionIngredient[]? RequireStacks;
  public string ActionLangCode = "rollers-construct";
}

/// <summary>A required material for a stage; <see cref="StoreWildCard"/> records the chosen
/// variant (e.g. metal) so later stages and drops resolve to the same one.</summary>
public class ExConstructionIngredient : CraftingRecipeIngredient {
  public string? StoreWildCard;

  // Base Clone() is non-virtual in the legacy API, so this hides it with a derived-typed clone:
  // CloneTo<T>() copies the base fields, then StoreWildCard is carried over.
  public new ExConstructionIngredient Clone() {
    var c = CloneTo<ExConstructionIngredient>();
    c.StoreWildCard = StoreWildCard;
    return c;
  }
}

/// <summary>Port of vanilla <c>RightClickConstruction</c>: the per-block construction state and logic.</summary>
public class ExRightClickConstruction {
  public ExConstructionStage[] Stages = Array.Empty<ExConstructionStage>();
  public int CurrentCompletedStage;
  public Dictionary<string, string> StoredWildCards = new();

  private ICoreAPI api = null!;
  private string codeForErrorLogging = "";

  public void LateInit(
    ExConstructionStage[] stages,
    ICoreAPI api,
    string codeForErrorLogging
  ) {
    Stages = stages ?? Array.Empty<ExConstructionStage>();
    this.api = api;
    this.codeForErrorLogging = codeForErrorLogging;
  }

  public bool OnInteract(EntityAgent byEntity, ItemSlot handslot) {
    if (CurrentCompletedStage >= Stages.Length - 1)
      return false;
    if (!TryConsumeIngredients(byEntity, handslot))
      return false;
    if (CurrentCompletedStage < Stages.Length - 1)
      CurrentCompletedStage++;
    return true;
  }

  /// <summary>The shape elements completed so far, as "elem/*" selectors for the tesselator.</summary>
  public string[] getShapeElements() {
    var set = new HashSet<string>();
    for (int i = 0; i <= CurrentCompletedStage; i++) {
      var stage = Stages[i];
      if (stage.AddElements != null)
        foreach (var e in stage.AddElements)
          set.Add(e + "/*");
      if (stage.RemoveElements != null)
        foreach (var e in stage.RemoveElements)
          set.Remove(e + "/*");
    }
    return new List<string>(set).ToArray();
  }

  public ItemStack[] GetDrops(float dropRatio = 1f, Random? rnd = null) {
    if (CurrentCompletedStage < 1)
      return Array.Empty<ItemStack>();

    // Refunds every completed stage, 0..CurrentCompletedStage inclusive: the bound is `<=`, not `<`,
    // so the last built stage's materials are recovered too.
    var list = new List<ItemStack>();
    for (int i = 0; i <= CurrentCompletedStage; i++) {
      var stage = Stages[i];
      if (stage.RequireStacks == null)
        continue;
      foreach (var ingredient in stage.RequireStacks) {
        foreach (var wc in StoredWildCards)
          ingredient.FillPlaceHolder(wc.Key, wc.Value);

        var resolved = ingredient;
        if (ingredient.StoreWildCard != null) {
          resolved = (ExConstructionIngredient)ingredient.Clone();
          resolved.Code.Path = resolved.Code.Path.Replace(
            "*",
            StoredWildCards[ingredient.StoreWildCard]
          );
        }

        if (
          resolved.Resolve(
            api.World,
            $"Drop stack for construction stage {i} on {codeForErrorLogging}"
          )
        ) {
          var stack = resolved.ResolvedItemStack.Clone();
          stack.StackSize = GameMath.RoundRandom(
            rnd,
            stack.StackSize * dropRatio
          );
          list.Add(stack);
        }
      }
    }
    return list.ToArray();
  }

  private bool TryConsumeIngredients(EntityAgent byEntity, ItemSlot handslot) {
    var player = (byEntity as EntityPlayer)?.Player;
    if (player == null)
      return false;

    var stage = Stages[CurrentCompletedStage + 1];
    if (stage.RequireStacks == null)
      return true;

    var hotbar = player.InventoryManager.GetHotbarInventory();
    var toTake = new List<KeyValuePair<ItemSlot, int>>();
    var remaining = new List<ExConstructionIngredient>();
    foreach (var ing in stage.RequireStacks)
      remaining.Add((ExConstructionIngredient)ing.Clone());

    var newWildcards = new Dictionary<string, string>();
    bool creativeInstant =
      player.WorldData.CurrentGameMode == EnumGameMode.Creative
      && byEntity.Controls.CtrlKey;

    foreach (var ing in remaining) {
      foreach (var wc in StoredWildCards)
        ing.FillPlaceHolder(wc.Key, wc.Value);
      // Legacy API uses IsWildCard rather than 1.22's MatchingType enum: a non-wildcard
      // (exact-stack) ingredient that fails to resolve is a hard failure.
      if (
        !ing.Resolve(
          api.World,
          $"Require stack for construction stage on {codeForErrorLogging}"
        ) && !ing.IsWildCard
      )
        return false;
    }

    foreach (var slot in hotbar) {
      if (slot.Empty)
        continue;
      if (remaining.Count == 0)
        break;
      for (int k = 0; k < remaining.Count; k++) {
        var ing = remaining[k];
        if (
          !creativeInstant && ing.SatisfiesAsIngredient(slot.Itemstack, false)
        ) {
          int num = Math.Min(ing.Quantity, slot.Itemstack.StackSize);
          toTake.Add(new KeyValuePair<ItemSlot, int>(slot, num));
          ing.Quantity -= num;
          if (ing.Quantity <= 0) {
            remaining.RemoveAt(k);
            k--;
            if (ing.StoreWildCard != null)
              newWildcards[ing.StoreWildCard] = slot.Itemstack
                .Collectible
                .Variant[ing.StoreWildCard];
          }
        } else if (creativeInstant && ing.StoreWildCard != null) {
          newWildcards["wood"] = "oak";
          newWildcards["metal"] = "iron";
        }
      }
    }

    if (!creativeInstant && remaining.Count > 0) {
      var ing = remaining[0];
      if (player is IClientPlayer && player.Entity.Api is ICoreClientAPI capi)
        capi.TriggerIngameError(
          this,
          "missingstack",
          Lang.Get(
            "ingameerror-missingstack",
            ing.Quantity,
            ing.IsWildCard
              ? Lang.Get(ing.Name ?? "")
              : ing.ResolvedItemStack.GetName()
          )
        );
      return false;
    }

    foreach (var wc in newWildcards)
      StoredWildCards[wc.Key] = wc.Value;

    if (!creativeInstant)
      foreach (var take in toTake) {
        take.Key.TakeOut(take.Value);
        take.Key.MarkDirty();
      }
    return true;
  }

  public void ToTreeAttributes(ITreeAttribute tree) {
    var wildcards = new TreeAttribute();
    foreach (var wc in StoredWildCards)
      wildcards[wc.Key] = new StringAttribute(wc.Value);
    tree["wildcards"] = wildcards;
    tree.SetInt("currentStage", CurrentCompletedStage);
  }

  public void FromTreeAttributes(ITreeAttribute tree) {
    StoredWildCards.Clear();
    if (tree["wildcards"] is TreeAttribute wildcards)
      foreach (var entry in wildcards)
        if (entry.Value is StringAttribute s)
          StoredWildCards[entry.Key] = s.value;
    CurrentCompletedStage = tree.GetInt("currentStage", 0);
  }

  /// <summary>The build-material hover help for the next stage (right-click to add the listed stacks),
  /// or null when construction is complete. Stands in for <c>IInteractableWithHelp</c>, which the legacy
  /// game versions do not have.</summary>
  public WorldInteraction[]? GetInteractionHelp() {
    if (CurrentCompletedStage + 1 >= Stages.Length)
      return null;
    var stage = Stages[CurrentCompletedStage + 1];
    if (stage.RequireStacks == null)
      return null;

    var list = new List<WorldInteraction>();
    foreach (var ingredient in stage.RequireStacks) {
      foreach (var wc in StoredWildCards)
        ingredient.FillPlaceHolder(wc.Key, wc.Value);
      if (
        !ingredient.Resolve(
          api.World,
          $"Interaction help on {codeForErrorLogging}"
        )
      )
        return null;

      var stacks = new List<ItemStack>();
      foreach (var collectible in api.World.Collectibles) {
        var stack = new ItemStack(collectible, 1);
        if (ingredient.SatisfiesAsIngredient(stack, false)) {
          stack.StackSize = ingredient.Quantity;
          stacks.Add(stack);
        }
      }
      var matching = stacks.ToArray();
      list.Add(
        new WorldInteraction {
          ActionLangCode = stage.ActionLangCode,
          Itemstacks = matching,
          GetMatchingStacks = (wi, bs, es) => matching,
          MouseButton = EnumMouseButton.Right,
        }
      );
    }
    if (stage.RequireStacks.Length == 0)
      list.Add(
        new WorldInteraction {
          ActionLangCode = stage.ActionLangCode,
          MouseButton = EnumMouseButton.Right,
        }
      );
    return list.ToArray();
  }
}
#endif
