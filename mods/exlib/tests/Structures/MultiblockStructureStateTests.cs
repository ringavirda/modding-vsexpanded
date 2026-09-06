using ExpandedLib.Blocks;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="BlockEntityMultiblockStructure"/> gets the same <c>Persisted</c>/<c>DeclareState</c>
/// convenience as <see cref="ExBlockEntity"/>, on top of the <c>structureComplete</c> flag it already
/// writes by hand.
/// </summary>
public class MultiblockStructureStateTests {
  private sealed class StatefulStructure : BlockEntityMultiblockStructure {
    public string? Note;

    protected override void UpdateStructureRotation() => SetStructureAngle(0);

    protected override void OnStructureCompleted() { }

    protected override void OnStructureLost() { }

    protected override string GetIncompleteMessage(int missingCount) => "";

    protected override string GetCompleteMessage() => "";

    protected override void DeclareState(ExBlockState state) =>
      state.String("note", () => Note, v => Note = v);
  }

  [Fact]
  public void A_field_declared_through_State_round_trips() {
    // ToTreeAttributes reads Block.IsMissing before it ever reaches Persisted, so the structure needs a
    // real block even though nothing here cares which one.
    var source = new StatefulStructure {
      Note = "riveted",
      Pos = new BlockPos(0, 0, 0),
      Block = TestBlocks.Configure(new Block(), "test:structure", 1),
    };
    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);

    var target = new StatefulStructure();
    target.FromTreeAttributes(tree, new TestWorld().World);

    Assert.Equal("riveted", target.Note);
  }
}
