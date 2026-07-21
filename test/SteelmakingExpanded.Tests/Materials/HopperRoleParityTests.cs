using System;
using ExpandedLib.Materials;
using IronworkingExpanded.Compat;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using Vintagestory.API.Common;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Parity guard for both hopper families' accept-rules after they moved from hardcoded
/// <c>Collectible.Code.Path</c> equality onto <see cref="MaterialRoleRegistry"/>. The reinforced
/// hopper's typed-slot <see cref="ItemSlotBlastFurnace.CanTakeFrom"/> and the bell hopper's role
/// classification must return exactly what the old inline string checks did. Relies on the
/// module-init-seeded iwex roles (the headless stand-in for materialroles.json); read-only.
/// </summary>
public class HopperRoleParityTests
{
  private static ItemSlot Source(string code) =>
    new DummySlot(new ItemStack(new Item { Code = new AssetLocation(code) }));

  private static ItemSlotBlastFurnace Slot(string allowedType)
  {
    var inv = new InventoryBlastFurnace(8, "test", null, null);
    int i = allowedType switch
    {
      "iron" => 0,
      "coke" => 2,
      _ => 3,
    };
    return (ItemSlotBlastFurnace)inv[i];
  }

  // The reinforced hopper's original accept-rule, reproduced as the oracle (no compat mod headless).
  private static bool OldCanTake(string allowedType, string path) =>
    allowedType switch
    {
      "iron" => path.StartsWith("crushed-iron", StringComparison.Ordinal)
        || path == "blastmix",
      "coke" => path == "coke",
      "lime" => path == "lime",
      _ => false,
    };

  #region Reinforced hopper CanTakeFrom
  [Theory]
  [InlineData("iron", "game:crushed-iron")]
  [InlineData("iron", "game:crushed-iron-magnetite")]
  [InlineData("iron", "iwex:blastmix")]
  [InlineData("iron", "game:coke")]
  [InlineData("iron", "game:lime")]
  [InlineData("iron", "game:stick")]
  [InlineData("coke", "game:coke")]
  [InlineData("coke", "game:charcoal")] // fuel, but the coke slot must still reject it
  [InlineData("coke", "game:crushed-iron")]
  [InlineData("lime", "game:lime")]
  [InlineData("lime", "iwex:blastmix")]
  [InlineData("lime", "game:coke")]
  public void CanTakeFrom_matches_the_old_hardcoded_rule(
    string allowedType,
    string code
  )
  {
    string path = new AssetLocation(code).Path;
    bool old = OldCanTake(allowedType, path);
    bool @new = Slot(allowedType).CanTakeFrom(Source(code));
    Assert.Equal(old, @new);
  }
  #endregion

  #region Bell hopper role classification
  [Theory]
  [InlineData("iwex:blastmix", true)]
  [InlineData("game:lime", false)]
  [InlineData("game:coke", false)]
  public void Charge_role_matches_the_old_blastmix_check(string code, bool expected)
  {
    var stack = new ItemStack(new Item { Code = new AssetLocation(code) });
    bool old = stack.Collectible.Code.Path == "blastmix";
    Assert.Equal(expected, old); // pin the oracle
    Assert.Equal(old, MaterialRoleRegistry.IsRole(Roles.Charge, stack));
  }

  [Theory]
  [InlineData("game:lime", true)]
  [InlineData("iwex:blastmix", false)]
  [InlineData("game:coke", false)]
  public void Flux_role_matches_the_old_lime_check(string code, bool expected)
  {
    var stack = new ItemStack(new Item { Code = new AssetLocation(code) });
    bool old = stack.Collectible.Code.Path == "lime";
    Assert.Equal(expected, old);
    Assert.Equal(old, MaterialRoleRegistry.IsRole(Roles.Flux, stack));
  }
  #endregion

  #region IronOreCompat (crushed-iron prefix)
  [Theory]
  [InlineData("crushed-iron", true)]
  [InlineData("crushed-iron-magnetite", true)]
  [InlineData("crushed-hematite", false)] // mod ore, no mod enabled headless
  [InlineData("coke", false)]
  [InlineData("lime", false)]
  public void IsCrushedIronOre_matches_the_old_prefix(string path, bool expected)
  {
    Assert.Equal(
      expected,
      path.StartsWith("crushed-iron", StringComparison.Ordinal)
    );
    Assert.Equal(expected, IronOreCompat.IsCrushedIronOre(path));
  }
  #endregion
}
