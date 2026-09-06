using System;
using ExpandedLib.Blocks;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="TreeKeys.AssertDeclaresBaseKeys"/>: the guard for the "call <c>base.DeclareState</c>
/// first" convention a golden alone cannot catch, since <see cref="PersistScan"/>'s <c>[Persist]</c>
/// scan contributes its keys independently of whatever <c>DeclareState</c> does.
/// </summary>
public class TreeKeysTests {
  private class BaseEntity : ExBlockEntity {
    public int BaseField;

    protected override void DeclareState(ExBlockState state) =>
      state.Int("baseKey", () => BaseField, v => BaseField = v);
  }

  private sealed class GoodDerived : BaseEntity {
    public int DerivedField;

    protected override void DeclareState(ExBlockState state) {
      base.DeclareState(state);
      state.Int("derivedKey", () => DerivedField, v => DerivedField = v);
    }
  }

  private sealed class BadDerived : BaseEntity {
    public int DerivedField;

    // Skips base.DeclareState(state): BaseField silently stops saving.
    protected override void DeclareState(ExBlockState state) =>
      state.Int("derivedKey", () => DerivedField, v => DerivedField = v);
  }

  private static void Place(BlockEntity be) {
    be.Pos = new BlockPos(0, 0, 0);
    be.Block = TestBlocks.Configure(new Block(), "test:treekeysguard", 1);
  }

  [Fact]
  public void A_subclass_that_calls_base_passes() {
    var be = new GoodDerived();
    Place(be);

    TreeKeys.AssertDeclaresBaseKeys(be);
  }

  [Fact]
  public void A_subclass_that_skips_base_fails_naming_the_type_and_the_missing_keys() {
    var be = new BadDerived();
    Place(be);

    InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
      () => TreeKeys.AssertDeclaresBaseKeys(be)
    );

    Assert.Contains("BaseEntity", ex.Message, StringComparison.Ordinal);
    Assert.Contains("baseKey", ex.Message, StringComparison.Ordinal);
  }
}
