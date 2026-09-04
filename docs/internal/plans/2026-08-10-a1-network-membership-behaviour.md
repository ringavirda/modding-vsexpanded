# A1 — Network membership as a behaviour: Implementation Plan

**Status** DONE 2026-08-12, as stage A1 of the framework-composition arc
([2026-08-10-framework-composition-staging.md](2026-08-10-framework-composition-staging.md)). Kept for the
reasoning behind the behaviour split, not as work to do.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move network membership off the `BlockNetworkNode` base class into a `BlockEntityBehavior`, so the graph walk resolves a node by behaviour and one block entity can belong to several networks at once.

**Architecture:** A new `BEBehaviorNetworkMember` carries what membership actually is — network type, connector faces, graph registration and network-state persistence. `BlockNetworkModSystem`'s two block-type tests become behaviour lookups. `BlockNetworkNode` keeps orientation, box caching, placement and drops, and loses only membership. `INetworkConnector` narrows to its original meaning: a *port*, a face another network may couple to on a block that is not a graph member.

**Tech Stack:** C# 12, .NET 7/8/10 multi-target, Vintage Story 1.20–1.22 API, xUnit + NSubstitute, csharpier.

## Global Constraints

- **Do not commit.** The user owns git history. Leave every task's work in the working tree and record what changed and why in `docs/internal/worklog/2026-08.md` (newest entry first). Where the task template below says "record", that is what it means.
- **exlib is published** (0.7.1 on ModDB). Persisted node state must survive a world loaded with the old format. Migration goes through `BlockMigrationModSystem`, the existing seam.
- **Three targets must build and pass:** `exmod test all` covers 1.22/net10.0, 1.21/net8.0, 1.20/net7.0. A change that compiles only on the newest API is not done.
- **Formatting is enforced:** run `scripts/exmod.ps1 format` before finishing a task; `-Check` needs a clean tree so it cannot run mid-task.
- **Comment style is enforced by a test:** `CommentStyleGuards` fails any doc comment over 16 lines or with more than 3 `<para>` blocks. Move rationale into `docs/design/` and cite it.
- **Spelling:** docs use UK spelling (`behaviour`, `neighbour`); code identifiers use US spelling to match the vanilla API (`BEBehaviorNetworkMember`, matching `BlockEntityBehavior`).
- **Test count floors** live in `scripts/test-floors.txt`. Raise the floor when a task adds tests; never lower it to make a run pass.
- **Design of record:** `docs/design/mechanics/framework-composition.md`. If implementation contradicts it, update the page in the same task.

---

## File Structure

| File | Responsibility |
|---|---|
| `src/ExpandedLib/Blocks/Networks/BEBehaviorNetworkMember.cs` | **create** — membership: type, faces, graph register/unregister, network-state persistence |
| `src/ExpandedLib/Blocks/Networks/NetworkMembership.cs` | **create** — the type-keyed accessor `GetMember(be, networkType)`, since `GetBehavior<T>()` returns only the first match |
| `src/ExpandedLib/Blocks/Networks/BlockNetworkModSystem.cs` | **modify** — `GetConnectedNeighbors` (`:351`) and the fracture walk (`:242`) resolve by behaviour; unloaded-chunk handling |
| `src/ExpandedLib/Blocks/Networks/BlockEntityNetworkNode.cs` | **modify** — becomes a thin shim that owns one member behaviour; keeps its public surface |
| `src/ExpandedLib/Networks/INetworkConnector.cs` | **modify** — doc narrowed to "port, not member" |
| `src/ExpandedLib/Blocks/Migrations/NetworkMembershipMigration.cs` | **create** — old per-node tree keys to the behaviour's subtree |
| `test/ExpandedLib.Testing/TestWorld.cs` | **modify** — `PlaceNode` helper that places a block *and* a member-bearing block entity |
| `test/ExpandedLib.Testing/Doubles/TestNetworkBlock.cs` | **modify** — gains a member-bearing block entity double |
| `test/ExpandedLib.Tests/Networks/NetworkMembershipTests.cs` | **create** — the behaviour's own contract |
| `test/ExpandedLib.Tests/Networks/MultiMembershipTests.cs` | **create** — several memberships on one block entity |

---

## Task 1: The membership accessor

Nothing depends on ordering here, and it is the smallest piece that can fail a review on its own: `GetBehavior<T>()` returning the first match is the reason a bespoke accessor exists, and that reason should be pinned by a test before anything uses it.

**Files:**
- Create: `src/ExpandedLib/Blocks/Networks/NetworkMembership.cs`
- Test: `test/ExpandedLib.Tests/Networks/NetworkMembershipTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `NetworkMembership.MembersOf(BlockEntity) -> IEnumerable<BEBehaviorNetworkMember>`, `NetworkMembership.MemberOf(BlockEntity, string networkType) -> BEBehaviorNetworkMember?`. Tasks 3–6 use both. `BEBehaviorNetworkMember` itself lands in Task 2; for this task it is declared minimally and filled in there.

- [ ] **Step 1: Write the failing test**

Create `test/ExpandedLib.Tests/Networks/NetworkMembershipTests.cs`:

```csharp
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Testing;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The type-keyed membership accessor. Vanilla's <c>GetBehavior&lt;T&gt;()</c> returns the FIRST
/// match, so a block entity on two networks needs an accessor that selects by network type.
/// </summary>
public class NetworkMembershipTests {
  [Fact]
  public void MemberOf_selects_by_network_type_not_by_clr_type() {
    var w = new TestWorld();
    var be = TestMemberBlockEntity.With(w, new BlockPos(0, 0, 0), "pipe", "molten");

    Assert.Equal("pipe", NetworkMembership.MemberOf(be, "pipe")?.NetworkType);
    Assert.Equal("molten", NetworkMembership.MemberOf(be, "molten")?.NetworkType);
    Assert.Null(NetworkMembership.MemberOf(be, "mpenergy"));
  }

  [Fact]
  public void MembersOf_returns_every_membership() {
    var w = new TestWorld();
    var be = TestMemberBlockEntity.With(w, new BlockPos(0, 0, 0), "pipe", "molten");

    Assert.Equal(2, NetworkMembership.MembersOf(be).Count());
  }

  [Fact]
  public void A_block_entity_with_no_membership_answers_empty_rather_than_throwing() {
    var w = new TestWorld();
    var be = TestMemberBlockEntity.With(w, new BlockPos(0, 0, 0));

    Assert.Empty(NetworkMembership.MembersOf(be));
    Assert.Null(NetworkMembership.MemberOf(be, "pipe"));
  }
}
```

`TestMemberBlockEntity` is created in Step 3 of this task.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/ExpandedLib.Tests/ExpandedLib.Tests.csproj -f net8.0 -p:Legacy=true --filter NetworkMembershipTests`
Expected: FAIL — `NetworkMembership` and `TestMemberBlockEntity` do not exist (compile error).

- [ ] **Step 3: Write the accessor and the test double**

Create `src/ExpandedLib/Blocks/Networks/NetworkMembership.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Blocks.Networks;

/// <summary>
/// Selects a block entity's network memberships by network type. Vanilla's
/// <see cref="BlockEntity.GetBehavior{T}"/> returns the first behaviour of a CLR type, which cannot
/// distinguish a pipe membership from a molten one on the same block entity.
/// </summary>
public static class NetworkMembership {
  /// <summary>Every membership on <paramref name="be"/>; empty when it is not on any network.</summary>
  public static IEnumerable<BEBehaviorNetworkMember> MembersOf(BlockEntity? be) =>
    be?.Behaviors.OfType<BEBehaviorNetworkMember>() ?? [];

  /// <summary>
  /// The membership for <paramref name="networkType"/>, or <c>null</c>. A block entity carries at
  /// most one membership per network type; two would be two nodes at one position.
  /// </summary>
  public static BEBehaviorNetworkMember? MemberOf(
    BlockEntity? be,
    string networkType
  ) =>
    MembersOf(be)
      .FirstOrDefault(m =>
        string.Equals(m.NetworkType, networkType, System.StringComparison.Ordinal)
      );
}
```

Create the minimal `BEBehaviorNetworkMember` it needs — Task 2 fills it in. In `src/ExpandedLib/Blocks/Networks/BEBehaviorNetworkMember.cs`:

```csharp
using Vintagestory.API.Common;

namespace ExpandedLib.Blocks.Networks;

/// <summary>
/// One network membership held by a block entity: the network it belongs to, the faces it couples
/// on, and its registration with <see cref="BlockNetworkModSystem"/>. A block entity carries one of
/// these per network it is on, which is what lets a converter be on pipe, molten and mechanical at
/// once. See docs/design/mechanics/framework-composition.md.
/// </summary>
public class BEBehaviorNetworkMember(BlockEntity blockentity)
  : BlockEntityBehavior(blockentity) {
  /// <summary>The network this membership joins, e.g. <c>"pipe"</c>.</summary>
  public string NetworkType { get; protected set; } = "";
}
```

Create `test/ExpandedLib.Testing/Doubles/TestMemberBlockEntity.cs`:

```csharp
using ExpandedLib.Blocks.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Testing;

/// <summary>
/// A bare block entity carrying a chosen set of network memberships, for testing the accessor and
/// the walk without a concrete machine.
/// </summary>
public sealed class TestMemberBlockEntity : BlockEntity {
  /// <summary>Places a block at <paramref name="pos"/> with one membership per network type.</summary>
  public static TestMemberBlockEntity With(
    TestWorld world,
    BlockPos pos,
    params string[] networkTypes
  ) {
    var be = new TestMemberBlockEntity();
    foreach (string type in networkTypes)
      be.Behaviors.Add(new TestNetworkMember(be, type));
    world.Place(pos, TestNetworkBlock.Create("test", "ns", id: 900), be);
    return be;
  }

  private sealed class TestNetworkMember : BEBehaviorNetworkMember {
    public TestNetworkMember(BlockEntity be, string networkType)
      : base(be) => NetworkType = networkType;
  }
}
```

`NetworkType` needs a settable-from-subclass setter, which the `protected set` above provides.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/ExpandedLib.Tests/ExpandedLib.Tests.csproj -f net8.0 -p:Legacy=true --filter NetworkMembershipTests`
Expected: PASS, 3 tests.

- [ ] **Step 5: Format, full suite, record**

```
scripts/exmod.ps1 format
scripts/exmod.ps1 test all
```
Expected: all 15 targets pass. Raise `ExpandedLib.Tests` in `scripts/test-floors.txt` by 3. Add a worklog line naming the accessor and why `GetBehavior<T>()` is insufficient. Do not commit.

---

## Task 2: The membership behaviour carries registration and persistence

**Files:**
- Modify: `src/ExpandedLib/Blocks/Networks/BEBehaviorNetworkMember.cs`
- Modify: `src/ExpandedLib/Blocks/Networks/BlockEntityNetworkNode.cs:20-70`
- Test: `test/ExpandedLib.Tests/Networks/NetworkMembershipTests.cs`

**Interfaces:**
- Consumes: `NetworkMembership.MemberOf` (Task 1).
- Produces: `BEBehaviorNetworkMember.NetworkType`, `.HasConnectorAt(BlockFacing) -> bool`, `.IsConnectionBroken() -> bool`, `.OnNetworkUpdate(object?)`, and the behaviour registering itself with `BlockNetworkModSystem` on `Initialize` / unregistering on `OnBlockRemoved`. Task 3's walk reads `NetworkType`, `HasConnectorAt` and `IsConnectionBroken`.

The behaviour must reproduce exactly what `BlockEntityNetworkNode` does today (`:20-46`): resolve the mod system, `AddNode` server-side when no network is present, restore `_savedNetworkState` captured *before* `base.Initialize` (because `AddNode` can broadcast null state and clear it), and `RemoveNode` on removal.

- [ ] **Step 1: Write the failing test**

Append to `NetworkMembershipTests.cs`:

```csharp
  [Fact]
  public void A_membership_registers_its_position_as_a_graph_node() {
    var w = new TestWorld();
    w.RegisterNetwork("test", sys => new StubNetwork(sys));
    var pos = new BlockPos(0, 0, 0);

    w.PlaceNode(pos, "test", "ns");

    Assert.NotNull(w.NetworkAt(pos));
  }

  [Fact]
  public void Removing_the_block_unregisters_the_node() {
    var w = new TestWorld();
    w.RegisterNetwork("test", sys => new StubNetwork(sys));
    var pos = new BlockPos(0, 0, 0);
    w.PlaceNode(pos, "test", "ns");

    w.GetBlockEntity(pos)!.OnBlockRemoved();

    Assert.Null(w.NetworkAt(pos));
  }
```

`TestWorld.PlaceNode` lands in Task 3 Step 3; write it here if Task 3 has not run.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/ExpandedLib.Tests/ExpandedLib.Tests.csproj -f net8.0 -p:Legacy=true --filter NetworkMembershipTests`
Expected: FAIL — `PlaceNode` does not exist, or the network is null because the behaviour does not register.

- [ ] **Step 3: Move registration into the behaviour**

Replace the body of `BEBehaviorNetworkMember` with registration lifted verbatim from `BlockEntityNetworkNode.Initialize` (`:20-40`) and `OnBlockRemoved` (`:42-46`):

```csharp
public override void Initialize(ICoreAPI api, JsonObject properties) {
  base.Initialize(api, properties);
  if (properties?["networkType"].Exists == true)
    NetworkType = properties["networkType"].AsString(NetworkType);

  NetworkSystem = api.ModLoader.GetModSystem<BlockNetworkModSystem>();
  if (api.Side != EnumAppSide.Server)
    return;

  if (NetworkSystem.GetNetworkAt(Blockentity.Pos) == null)
    NetworkSystem.AddNode(api.World.BlockAccessor, Blockentity.Pos, NetworkType);

  if (
    _savedNetworkState != null
    && NetworkSystem.GetNetworkAt(Blockentity.Pos) is BlockNetwork network
  ) {
    network.RestoreState(_savedNetworkState);
    network.BroadcastUpdate(api.World.BlockAccessor);
  }
}

public override void OnBlockRemoved() {
  base.OnBlockRemoved();
  if (Blockentity.Api?.Side == EnumAppSide.Server)
    NetworkSystem?.RemoveNode(Blockentity.Api.World.BlockAccessor, Blockentity.Pos);
}
```

⛔ The ordering trap: `FromTreeAttributes` runs before `Initialize`, so `_savedNetworkState` must be read in the behaviour's own `FromTreeAttributes` and consumed in `Initialize`. `BlockEntityStructureFiller` hit this exact problem and worked around it per-class (`:58-64`, `:130-136`); solve it here once.

Then reduce `BlockEntityNetworkNode` to owning one member behaviour, keeping its public surface (`NetworkType`, `Orientation`, `PossibleOrientations`, `HasConnectorAt`, `IsConnectionBroken`, `OnNetworkUpdate`) delegating to it, so no concrete block entity changes yet.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/ExpandedLib.Tests/ExpandedLib.Tests.csproj -f net8.0 -p:Legacy=true --filter Network`
Expected: PASS, including the pre-existing `NetworkGraphTests` — the shim means nothing else has moved yet.

- [ ] **Step 5: Format, full suite, record**

```
scripts/exmod.ps1 format
scripts/exmod.ps1 test all
```
Expected: all 15 targets pass. This is the parity gate: if any existing network test fails here, the shim is not faithful — fix the shim, do not change the test.

---

## Task 3: The test harness can place a member-bearing node

The walk cannot flip until fixtures can produce block entities. 109 of 130 `Place` calls already pass one; 21 place a bare block and those are the ones that would silently stop being nodes.

**Files:**
- Modify: `test/ExpandedLib.Testing/TestWorld.cs:129-138`
- Modify: `test/ExpandedLib.Testing/Doubles/TestNetworkBlock.cs`
- Modify: `test/ExpandedLib.Tests/Networks/NetworkGraphTests.cs:21-34` (`BuildLine`)

**Interfaces:**
- Consumes: `BEBehaviorNetworkMember` (Task 2).
- Produces: `TestWorld.PlaceNode(BlockPos pos, string networkType, string orientation, int id = 1) -> TestWorld` — places the block, creates a block entity carrying one membership, and registers it. Tasks 4–6 build every fixture on it.

- [ ] **Step 1: Write the failing test**

Append to `NetworkGraphTests.cs`:

```csharp
  [Fact]
  public void PlaceNode_creates_a_block_entity_carrying_its_membership() {
    var w = NewWorld();
    var pos = new BlockPos(0, 0, 0);

    w.PlaceNode(pos, "test", "ns");

    Assert.NotNull(w.GetBlockEntity(pos));
    Assert.Equal("test", NetworkMembership.MemberOf(w.GetBlockEntity(pos), "test")?.NetworkType);
  }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/ExpandedLib.Tests/ExpandedLib.Tests.csproj -f net8.0 -p:Legacy=true --filter PlaceNode_creates`
Expected: FAIL — `PlaceNode` does not exist.

- [ ] **Step 3: Add `PlaceNode` and migrate `BuildLine`**

In `TestWorld.cs`, beside `Place`:

```csharp
/// <summary>
/// Places a network node: the block, a block entity carrying one membership for
/// <paramref name="networkType"/>, and its graph registration. Prefer this over
/// <see cref="Place"/> for anything the graph must walk - a bare block is no longer a node.
/// </summary>
public TestWorld PlaceNode(
  BlockPos pos,
  string networkType,
  string orientation,
  int id = 1
) {
  var be = TestMemberBlockEntity.With(this, pos, networkType);
  Place(pos, TestNetworkBlock.Create(networkType, orientation, id), be);
  AddNode(pos, networkType);
  return this;
}
```

Rewrite `BuildLine` to call `PlaceNode` per cell instead of `Place` + a separate `AddNode` loop.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/ExpandedLib.Tests/ExpandedLib.Tests.csproj -f net8.0 -p:Legacy=true --filter NetworkGraphTests`
Expected: PASS, all of them — the walk still reads the block, so nothing has regressed.

- [ ] **Step 5: Migrate the remaining bare placements**

Find them:

```
grep -rn "\.Place(" test --include=*.cs | grep -v ", be" | grep -iE "network|pipe|canal|node"
```

Convert each network-node placement to `PlaceNode`. A `Place` of a non-node block is correct and stays.

- [ ] **Step 6: Format, full suite, record**

```
scripts/exmod.ps1 format
scripts/exmod.ps1 test all
```
Expected: all 15 targets pass, with the same test counts as Task 2 plus one.

---

## Task 4: The walk resolves a node by behaviour

The flip. Both block-type tests go.

**Files:**
- Modify: `src/ExpandedLib/Blocks/Networks/BlockNetworkModSystem.cs:242`, `:351-377`
- Test: `test/ExpandedLib.Tests/Networks/NetworkGraphTests.cs`

**Interfaces:**
- Consumes: `NetworkMembership.MemberOf` (Task 1), `BEBehaviorNetworkMember.HasConnectorAt` / `.IsConnectionBroken` (Task 2), `TestWorld.PlaceNode` (Task 3).
- Produces: a walk that never mentions `BlockNetworkNode`. Task 5 relies on this to make a filler a node.

- [ ] **Step 1: Write the failing test**

```csharp
  [Fact]
  public void A_block_that_is_not_a_BlockNetworkNode_is_still_a_node_when_it_carries_a_membership() {
    // The point of A1: membership is a property of the block entity, not a kind of block.
    var w = NewWorld();
    var pos = new BlockPos(0, 0, 0);
    var plainBlock = TestBlocks.Configure(new Block(), "test:plain", 77);
    var be = TestMemberBlockEntity.With(w, pos, "test");
    w.Place(pos, plainBlock, be);

    w.AddNode(pos, "test");

    Assert.NotNull(w.NetworkAt(pos));
  }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/ExpandedLib.Tests/ExpandedLib.Tests.csproj -f net8.0 -p:Legacy=true --filter is_still_a_node`
Expected: FAIL — `GetConnectedNeighbors` returns nothing because `world.GetBlock(pos) is not BlockNetworkNode`.

- [ ] **Step 3: Resolve by behaviour**

In `GetConnectedNeighbors` (`:351`), replace the block-type test and the `IsConnectionBroken` lookup:

```csharp
BlockEntity? be = world.GetBlockEntity(pos);
if (NetworkMembership.MemberOf(be, networkType) is not { } member)
  yield break;

if (member.IsNetworkEndPoint || member.IsConnectionBroken())
  yield break;

foreach (var face in BlockFacing.ALLFACES) {
  if (!member.HasConnectorAt(world, pos, face))
    continue;
  BlockPos neighborPos = pos.AddCopy(face);
  if (IsValidNetworkNeighbour(world, member, world.GetBlock(neighborPos), neighborPos, face))
    yield return neighborPos;
}
```

Change `IsValidNetworkNeighbour`'s first parameter from `BlockNetworkNode sourceNode` to
`BEBehaviorNetworkMember source`, reading `source.NetworkType` and `source.AcceptsNeighbour(...)`.
Leave the neighbour side alone: it already resolves through `INetworkConnector`, which is correct
and stays.

Apply the same substitution at `:242`, the fracture walk's root check.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/ExpandedLib.Tests/ExpandedLib.Tests.csproj -f net8.0 -p:Legacy=true --filter NetworkGraphTests`
Expected: PASS, all.

- [ ] **Step 5: Format, full suite, record**

```
scripts/exmod.ps1 format
scripts/exmod.ps1 test all
```
Expected: all 15 targets pass. ⛔ A failure in `lpex`/`hpex`/`iwex` network tests here means a real node stopped being one — most likely a block entity that never got a membership. Fix by giving it one, not by restoring the type test.

---

## Task 5: A filler is a graph node

The proof case, and the payoff that justifies A1. It retires `docs/design/mechanics/multiblock.md` § "A filler can NEVER be a graph node".

**Files:**
- Modify: `src/ExpandedLib/Blocks/Structures/BlockEntityStructureFiller.cs`
- Modify: `src/ExpandedLib/Blocks/Structures/StructureFillers.cs` (footprint declaration accepts a membership)
- Modify: `docs/design/mechanics/multiblock.md` § "A filler can NEVER be a graph node"
- Test: `test/ExpandedLib.Tests/Networks/FillerNodeTests.cs` (create)

**Interfaces:**
- Consumes: everything from Tasks 1–4.
- Produces: a filler cell that appears in `BlockNetwork.Nodes`. Task 6 tests rediscovery across such a cell.

- [ ] **Step 1: Write the failing test**

```csharp
  [Fact]
  public void A_filler_cell_bridges_two_nodes_on_opposite_sides_of_itself() {
    // Retires multiblock.md § "A filler can NEVER be a graph node".
    var w = NewWorld();
    w.PlaceNode(new BlockPos(0, 0, 0), "test", "ns");
    w.PlaceFillerNode(new BlockPos(0, 0, 1), "test", "ns");
    w.PlaceNode(new BlockPos(0, 0, 2), "test", "ns");

    var net = w.NetworkAt(new BlockPos(0, 0, 0));

    Assert.NotNull(net);
    Assert.Equal(3, net!.Nodes.Count);
    Assert.Same(net, w.NetworkAt(new BlockPos(0, 0, 2)));
  }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/ExpandedLib.Tests/ExpandedLib.Tests.csproj -f net8.0 -p:Legacy=true --filter FillerNodeTests`
Expected: FAIL — `PlaceFillerNode` does not exist; the filler is not a node.

- [ ] **Step 3: Let a footprint cell declare a membership**

`FillerBehaviorSpec` already carries `(Code, Face?, Properties?)` and `ApplyHostedBehaviors` already instantiates behaviours by class code (`BlockEntityStructureFiller.cs:94-137`). A membership needs no new mechanism: declare `exlib.BEBehaviorNetworkMember` with `{ "networkType": "pipe" }` in the cell's `behaviors` array. Confirm `ApplyHostedBehaviors` calls `Initialize` with the properties — if it does, this task is a doc and test change plus `PlaceFillerNode` in the harness.

⛔ Re-read `BlockEntityStructureFiller.cs:58-64` and `:204-209` first. The filler replays `_savedTree` client-side and re-runs `ApplyHostedBehaviors` from `FromTreeAttributes` when hosted behaviours arrive after `Initialize`. A membership added this way inherits both paths; verify the server does not restore a stale `NetworkId` over a live one.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/ExpandedLib.Tests/ExpandedLib.Tests.csproj -f net8.0 -p:Legacy=true --filter FillerNodeTests`
Expected: PASS.

- [ ] **Step 5: Update the design page**

Replace the retirement banner in `multiblock.md` with the new rule, and delete the table of can/cannot. Note that `BlockRollingMillAxle` is now redundant but is **not** removed in A1 — that is content work, and removing a placed block needs a migration.

- [ ] **Step 6: Format, full suite, record**

---

## Task 6: An unloaded chunk suspends discovery instead of ending the network

The walk currently treats an unloaded chunk as the end of the run, which phantom-fractures it. Vanilla returns `missingChunkPos` and re-discovers on chunk load.

**Files:**
- Modify: `src/ExpandedLib/Blocks/Networks/BlockNetworkModSystem.cs` (walk + a chunk-load hook)
- Test: `test/ExpandedLib.Tests/Networks/NetworkGraphTests.cs`

**Interfaces:**
- Consumes: Task 4's walk.
- Produces: nothing later depends on it; this is the last behavioural change.

- [ ] **Step 1: Write the failing test**

```csharp
  [Fact]
  public void An_unloaded_chunk_does_not_split_the_network() {
    var w = NewWorld();
    w.PlaceNode(new BlockPos(0, 0, 0), "test", "ns");
    w.PlaceNode(new BlockPos(0, 0, 1), "test", "ns");
    var net = w.NetworkAt(new BlockPos(0, 0, 0));

    w.UnloadChunkAt(new BlockPos(0, 0, 1));
    w.RemoveNode(new BlockPos(0, 0, 0));

    // The far cell is unreachable, not absent: it must not be dropped from the graph.
    Assert.NotNull(w.NetworkAt(new BlockPos(0, 0, 1)));
  }
```

`TestWorld.UnloadChunkAt` must make `GetChunkAtBlockPos` return null for that cell while leaving the block in place; add it in Step 3.

- [ ] **Step 2: Run test to verify it fails**

Expected: FAIL — the far node is dropped.

- [ ] **Step 3: Adopt vanilla's handling**

Mirror `BEBehaviorMPBase.spreadTo` (`:518-527`) and `MechanicalPowerMod.Event_ChunkDirty` (`:325-345`): when the block entity is absent **and** `GetChunkAtBlockPos(pos) == null`, report the missing chunk rather than ending the walk, and re-run discovery when that chunk loads. `IBlockAccessor.GetBlock` returns the *air* block for an unloaded chunk and never null (`IBlockAccessor.cs:257-262`), which is exactly why the current code cannot tell the two cases apart.

- [ ] **Step 4–5: Verify, format, full suite, record**

---

## Task 7: Migrate persisted state and narrow `INetworkConnector`

**Files:**
- Create: `src/ExpandedLib/Blocks/Migrations/NetworkMembershipMigration.cs`
- Modify: `src/ExpandedLib/Networks/INetworkConnector.cs` (doc only)
- Modify: `docs/design/mechanics/framework-composition.md` (status → live)
- Test: `test/ExpandedLib.Tests/Blocks/NetworkMembershipMigrationTests.cs` (create)

**Interfaces:**
- Consumes: Tasks 1–6.
- Produces: nothing.

- [ ] **Step 1: Write the failing test**

A world saved in the old format has `networkType`, `orientation`, `possibleOrientations` and the network-state subtree written flat on the block entity by `BlockEntityNetworkNode.ToTreeAttributes` (`:47-57`). After migration the same values must be readable through the behaviour. Write a test that builds an old-format `TreeAttribute`, runs `FromTreeAttributes`, and asserts the membership resolves and the network state restores.

- [ ] **Step 2: Run to verify it fails**

- [ ] **Step 3: Read both formats**

The behaviour's `FromTreeAttributes` reads its own subtree, falling back to the flat keys when the subtree is absent. Register the migration with `BlockMigrationModSystem` so a re-save writes the new shape. Keep the fallback for one published version, then delete it.

⛔ `PossibleOrientations` is currently persisted by serialising a `string[]` through `System.Text.Json` into a tree string (`BlockEntityNetworkNode.cs:50-55`, `:66-69`). Migrate it to `tree.SetStringArray` / `GetStringArray` in this task rather than carrying the JSON-in-a-string forward.

- [ ] **Step 4: Verify, then narrow the interface doc**

`INetworkConnector`'s summary currently says it is implemented by `BlockNetworkNode` "and by structure blocks that expose a fixed port". Rewrite it to say a connector is a **port only** — a face another network may couple to on a block that is not a graph member — and that membership is `BEBehaviorNetworkMember`.

- [ ] **Step 5: Full suite across all versions, update the design page, record**

```
scripts/exmod.ps1 format
scripts/exmod.ps1 test all
```
Set `framework-composition.md`'s status for the membership axis to live, and write the worklog entry covering A1 end to end.

---

## Self-Review

**Spec coverage.** Design § "Seam 2" → Tasks 4, 5. § "Membership is addressed by network type" → Tasks 1, 2. § "Consequences: retires the filler invariant" → Task 5. § "`INetworkConnector` narrowed to a port" → Task 7. § "The rule" is stated, not implemented. **Gap accepted:** the design also names `BlockRollingMillAxle` as retired; Task 5 Step 5 explicitly defers it as content work needing its own migration.

**Placeholder scan.** Task 5 Step 3 and Tasks 6–7 describe intent with citations rather than final code, because each depends on a fact the implementer must confirm first (whether `ApplyHostedBehaviors` forwards properties; how `TestWorld` models chunks; the exact old-format tree). Each names the file, line and the vanilla precedent to copy. Tasks 1–4, the core, carry complete code.

**Type consistency.** `BEBehaviorNetworkMember` and `NetworkMembership.MemberOf`/`MembersOf` are used identically in Tasks 1–7. `TestWorld.PlaceNode` is defined in Task 3 and used in Tasks 4–6; `TestMemberBlockEntity.With` is defined in Task 1 and used in Tasks 3–4. Task 2 forward-references `PlaceNode` and says to write it there if Task 3 has not run — the one ordering coupling, called out in place.

**Known risk.** Task 4 is the irreversible step: after it, a block entity without a membership is not a node. The parity gate at the end of Task 2 and the fixture migration in Task 3 exist to make that flip safe, and Task 4 Step 5 says explicitly not to restore the type test when something breaks.
