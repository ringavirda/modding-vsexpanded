# exlib convenience plan - less boilerplate in ordinary mod code

> **For agentic workers:** execute task by task with a fresh implementer per task; the gate in each
> task is the check. No review pass per task.

**Status** complete 2026-09-06 (every task landed, uncommitted). Companion to
[2026-09-05-exlib-framework-plan.md](2026-09-05-exlib-framework-plan.md); Task V1 needs that plan's
Task D5. Evidence: a read of every repeated pattern in `mods/iiex/src` and `mods/siex/src` on
2026-09-05 (counts below). Patterns with no evidence of repetition (packet switches: 0 sites; GUI
dialogs: 2 files; logger prefixes: 0) get no new surface.

**Goal:** the things every block entity, block and mod system writes by hand today are written once in
exlib, in the spirit of `ExBlockEntity`: declare, and the plumbing follows.

**Architecture:** additions to `ExpandedLib.Blocks` (state declaration by attribute) and
`ExpandedLib.Helpers` (extension methods over the game's own interfaces). Nothing here changes a
save format, a registered code or a network packet; every helper is a pure convenience over calls
that keep working unchanged.

**Tech stack:** as the framework plan.

**Spec:** the counts:

| Pattern | Files | Sites | Lines each |
|---|---|---|---|
| hand-written `ToTreeAttributes`/`FromTreeAttributes` pair | 46 | 46 | about 7 |
| `GetBlockEntity(pos) is X be` with a null guard | 66 | 151 | 2 to 4 |
| neighbour walk over `BlockFacing.ALLFACES` / `HORIZONTALS` with `AddCopy` | 7 | 8 (+26 `AddCopy`) | 4 to 6 |
| `OnBlockInteractStart` guard clauses (held tool, sneak, side) | 36 | 75 | 10 to 15 |
| `Api.Side == EnumAppSide.X` checks | many | 80 | 1 |
| `GetBlockInfo` lines through `Lang.Get` | 43 | 265 `Lang.Get` | 2 to 4 |

## Progress

- **V2 landed 2026-09-06** together with framework D5: `[Persist]`, `PersistScan`, `IPersistable`,
  the `Persisted` accessor on `ExBlockEntity` and the four machine bases, `ExBlockState.Tree`.
- **V3, V4, V5 landed 2026-09-06** (nine lanes green, exlib 2233 tests): `ExBlockAccess`
  (`BlockEntity<T>`, `TryGetBlockEntity<T>`, `Neighbour<T>`, `Neighbours<T>`), `ExSide`,
  `Interaction`/`ExInteraction.Of`, `ExInfo` (`Lang`, `LangIf`, `Measure` dispatching to
  `ExMeasure`'s per-quantity formatters). Of the nine neighbour loops and thirty-six interact
  handlers, one of each was a pure instance of the pattern and was converted; the rest do more than
  the helper models and stay.
- **V1 landed 2026-09-06** (nine lanes green; iiex 2452, siex 336 tests): 44 of 46 block entities
  declare their state (20 through `[Persist]` alone, 24 through `Persisted` calls where a default,
  a legacy key, a negation, a nested `MoltenCharge` or an always-written stack diverges from the
  primitives); `TreeKeys` in the harness and 100 tree-key goldens prove every save key and type
  unchanged. Allow-listed: `BlockEntityBurdenmaker` (vanilla `BlockEntityContainer` base) and
  `BEBehaviorFirebox` (a behaviour). Two traps the goldens caught: an override of the virtual
  `DeclareState` on the machine bases must call `base`, and intermediate classes needed the
  `ExBlockEntity` base to expose `Persisted`.
- **V6 landed 2026-09-06** with framework F3: the sample uses `[Persist]`, `ExBlockAccess.Neighbours`,
  `ExInteraction.Of` and `ExInfo.Lang`.
- **V8 landed 2026-09-06** (ten lanes green; exlib 2402): `ExBlockEntityBehavior` and
  `ExBlockEntityContainer` carry `Persisted` through the shared host; `BEBehaviorFirebox` and
  `BlockEntityBurdenmaker` converted with their save shape unchanged, so every block entity and
  behaviour in the family declares its state; `TreeKeys.AssertDeclaresBaseKeys` calls each
  `DeclareState` level non-virtually and fails when an override skipped `base`.
- **V7 landed 2026-09-06**: `docs/internal/research/2026-09-06-ease-audit.md` audited seventeen
  pages; twelve open with a snippet over eight lines; three are inherent (a layout, a migration, a
  recipe profile), nine are page order or missing examples (V10 below); the sample's mod system
  makes four registration calls and Registries.md never says the command registration runs once per
  side, which is a missing rung (V9 below). A machine mod imports six namespaces; iiex averages 2.4
  per file.
- **V9 and V10 landed 2026-09-06** (eleven lanes green; exlib 2410; smoke green): `ExModSystem`
  (config accessors found through `[ExConfigAccessor]` stamped by the generator and loaded by
  `ExConfig.LoadAll`, then entities, commands per side and preferences, then the hooks; optional
  Harmony bootstrap) makes the sample's mod system an empty class; the nine pages lead with their
  shortest snippet (Getting-Started 27 to 7, Registries 13 to 2, Commands 17 to 7, and so on) and
  every helper section has a usage snippet. The convenience plan is complete. Found on the way:
  `EntityRegistry.RegisterAll` mutates a process-global per-assembly domain cache, so a test that
  calls it over the shared test assembly must clear that entry on dispose.



## The convenience rule (owner, 2026-09-05)

Modders are lazy in the way every good engineer is: the framework must make the short path the right
path. Each helper here is the declarative or default rung of a capability the explicit API already
has; none is a new concept. The plan closes with an ease audit (Task V7) that measures every wiki
page's first snippet.

## Global constraints

Those of the framework plan, plus: a converted block entity writes exactly the tree keys it wrote
before (a key-set golden proves it); an extension method never changes which side runs a mutation.

---

### Task V1: tree-key goldens, then `ExBlockState` everywhere

**Files:**
- Create: `mods/iiex/tests/Invariants/TreeKeyGoldenTests.cs`, `mods/siex/tests/Invariants/TreeKeyGoldenTests.cs`,
  `mods/exlib/testing/TreeKeys.cs`, goldens under `mods/<mod>/tests/goldens/<domain>/treekeys/<Class>.txt`.
- Modify: the 46 block entities (list them with
  `grep -rl "override void ToTreeAttributes" mods/iiex/src mods/siex/src --include=*.cs`), each
  moving its pair into `DeclareState` on the base it already derives from (framework Task D5 gives
  `BlockEntityProductionMachine`, `BlockEntityMultiblockStructure`, `BlockEntityNetworkNode`,
  `BlockEntityMachineStation` a `State`; the 14 on bare `BlockEntity` derive from `ExBlockEntity`).

**Interfaces:**
```csharp
namespace ExpandedLib.Testing;
/// The keys a block entity writes for a default-constructed instance, for a golden of the save shape.
public static class TreeKeys {
  public static IReadOnlyList<string> Of(BlockEntity be);          // sorted keys of ToTreeAttributes
  public static void AssertGolden(BlockEntity be, string domain);  // compares with the golden file
}
```

- [ ] **Step 1: goldens first.** The golden tests enumerate every concrete block entity of the mod through the assembly closure (the `FurnaceBranchGuards` idiom), construct it through `TestWorld`, and call `TreeKeys.AssertGolden`. Bless with `EXLIB_WRITE_GOLDENS=1` BEFORE touching any block entity; read the golden set once.
- [ ] **Step 2: convert one block entity per commit-sized diff.** Rules: same keys; an enum stored as `int` stays `Int` with cast lambdas; a `BlockPos` stored as three ints stays three `Int` declarations unless `Pos` writes the identical keys; a legacy-key fallback (`BlockEntityFurnaceTap`: `plugged` from `isPouring`) is kept by reading it in `DeclareState` through `String`/`Bool` with a custom setter; `MoltenCharge.ToTree/FromTree` goes through the `Tree` primitive from framework Task D5.
- [ ] **Step 3: gate** after each batch of ten: `bash scripts/exmod.sh test latest`; the tree-key goldens must not change (if one does, the conversion is wrong, not the golden).
- [ ] **Step 4: the count.** `grep -rl "override void ToTreeAttributes" mods/iiex/src mods/siex/src` lists only the two deliberate sites named in [../testing.md](../testing.md) (projection and HUD tests gate on the flag itself) or none.

### Task V2: state by attribute

**Files:**
- Create: `mods/exlib/src/Blocks/PersistAttribute.cs`, `mods/exlib/src/Blocks/PersistScan.cs`,
  `mods/exlib/src/Blocks/IPersistable.cs`.
- Modify: `mods/exlib/src/Blocks/ExBlockEntity.cs` and the four bases from framework Task D5: the
  `State` getter runs `PersistScan.Declare(this, state)` before `DeclareState(state)`.
- Test: `mods/exlib/tests/Blocks/PersistAttributeTests.cs`.
- Wiki: `Production-Machines.md` and a new section in `Helpers-and-Renderers.md` "Declared state".

**Interfaces:**
```csharp
namespace ExpandedLib.Blocks;
/// Marks a field or auto-property of a block entity as saved state. The key defaults to the member
/// name without a leading underscore; Legacy names an older key read when the key is absent.
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class PersistAttribute(string? key = null) : Attribute {
  public string? Key { get; } = key;
  public string? Legacy { get; init; }
}
/// A value type or class that writes itself into a sub-tree; a [Persist] member of this type is
/// stored under its key as a nested tree.
public interface IPersistable {
  void ToTree(ITreeAttribute tree);
  void FromTree(ITreeAttribute tree, IWorldAccessor world);
}
public static class PersistScan {
  /// Declares every [Persist] member of the block entity's type into the state; the scan is cached
  /// per type. Supported member types: bool, int, long, float, double, string, enum (stored as int),
  /// BlockPos, ItemStack, IPersistable. Throws NotSupportedException naming the member otherwise.
  public static void Declare(BlockEntity be, ExBlockState state);
}
```

- [ ] **Step 1: tests**: each supported type round-trips; an unsupported type throws naming the member; `Legacy` reads the old key when the new one is absent and writes only the new key; a subclass inherits its base's members; the scan runs once per type (a counter on a test accessor).
- [ ] **Step 2-4:** implement with compiled delegates (`Expression` trees), gate, wiki. Convert `samples/HelloExpanded`'s block entity to `[Persist]`.

### Task V3: block-entity lookups and side checks

**Files:**
- Create: `mods/exlib/src/Helpers/ExBlockAccess.cs`, `mods/exlib/src/Helpers/ExSide.cs`.
- Test: `mods/exlib/tests/Helpers/ExBlockAccessTests.cs`, `ExSideTests.cs`.
- Wiki: `Helpers-and-Renderers.md`.

**Interfaces:**
```csharp
namespace ExpandedLib.Helpers;
public static class ExBlockAccess {
  /// The block entity of type T at pos, or null; never throws for an unloaded chunk.
  public static T? BlockEntity<T>(this IBlockAccessor accessor, BlockPos pos) where T : class;
  public static bool TryGetBlockEntity<T>(this IBlockAccessor accessor, BlockPos pos, [NotNullWhen(true)] out T? be) where T : class;
  /// The block entity of type T one step from pos in the facing's direction, or null.
  public static T? Neighbour<T>(this IBlockAccessor accessor, BlockPos pos, BlockFacing facing) where T : class;
  /// Every (facing, block entity) pair of type T around pos over the given facings (default ALLFACES).
  public static IEnumerable<(BlockFacing Facing, T Entity)> Neighbours<T>(this IBlockAccessor accessor, BlockPos pos, IEnumerable<BlockFacing>? facings = null) where T : class;
}
public static class ExSide {
  public static bool IsServer(this ICoreAPI api);
  public static bool IsClient(this ICoreAPI api);
  public static bool IsServer(this IWorldAccessor world);
  public static bool IsClient(this IWorldAccessor world);
}
```

- [ ] **Step 1: tests** on `TestWorld`: lookup hit, miss, wrong type, unloaded chunk; `Neighbours` yields only matching entities with the right facing.
- [ ] **Step 2-4:** implement, gate, wiki. Then replace the 8 hand-written neighbour walks in iiex and siex with `Neighbours<T>` (their tests are the check).

### Task V4: interaction guards

**Files:**
- Create: `mods/exlib/src/Helpers/ExInteraction.cs`.
- Test: `mods/exlib/tests/Helpers/ExInteractionTests.cs` (needs the `TestPlayer` double from the testing plan Task T2; until then, NSubstitute `IPlayer` as `ExOrientableRig` does).
- Wiki: `Helpers-and-Renderers.md`.

**Interfaces:**
```csharp
namespace ExpandedLib.Helpers;
/// Reads what a click carried; answers questions, never decides which side acts.
public readonly struct Interaction(IWorldAccessor world, IPlayer player, BlockSelection selection) {
  public ItemStack? Held { get; }                 // active hotbar stack or null
  public CollectibleObject? HeldCollectible { get; }
  public bool HeldIs(EnumTool tool);
  public bool HeldIs(AssetLocation code);         // exact code or wildcard with '*'
  public bool HandEmpty { get; }
  public bool Sneaking { get; }
  public BlockFacing Face { get; }                // selection.Face
  public bool IsServer { get; }
  public bool IsClient { get; }
}
public static class ExInteraction {
  public static Interaction Of(IWorldAccessor world, IPlayer player, BlockSelection selection);
}
```

- [ ] **Step 1: tests**: held tool, held code with wildcard, empty hand, sneak, face, side.
- [ ] **Step 2-4:** implement, gate, wiki. Then convert the interact handlers in iiex and siex whose guard block shrinks by five lines or more (the implementer lists the converted files in the task's closing note); the mods' tests are the check.

### Task V5: block-info lines

**Files:**
- Create: `mods/exlib/src/Helpers/ExInfo.cs`.
- Test: `mods/exlib/tests/Helpers/ExInfoTests.cs`.

**Interfaces:**
```csharp
namespace ExpandedLib.Helpers;
public static class ExInfo {
  /// Appends Lang.Get(key, args) as one line.
  public static StringBuilder Lang(this StringBuilder dsc, string key, params object[] args);
  /// Appends the line only when the condition holds.
  public static StringBuilder LangIf(this StringBuilder dsc, bool condition, string key, params object[] args);
  /// Appends a measured value through ExMeasure so metric and imperial readers both get their unit.
  public static StringBuilder Measure(this StringBuilder dsc, string key, float value, string unit);
}
```

- [ ] **Step 1: tests** with `TestLang` (echoes the key): line endings, condition, unit formatting through `ExMeasure`.
- [ ] **Step 2-4:** implement, gate, wiki. Adoption in the family is optional and done only where a `GetBlockInfo` body shrinks.

### Task V6: the sample uses all of it

- [ ] `samples/HelloExpanded` (framework Task F3) uses `[Persist]`, `ExBlockAccess.Neighbours`, `ExInteraction.Of` and `ExInfo.Lang`, so the Getting-Started walk shows the convenience layer rather than the raw engine calls.


### Task V7: the ease audit

- [ ] For every page in mods/exlib/wiki that documents a capability, record the line count of the
  first snippet a reader needs to get a visible result (a registered block, a config value read, a
  placed multiblock, a network node, a passing test). Write the table into
  `docs/internal/research/2026-09-XX-ease-audit.md`. Any snippet over eight lines names the missing
  rung; each becomes a task appended to this plan with the same shape as V2-V5, implemented before
  the first public release.


### Task V8: declared state for behaviours and containers (added 2026-09-06 from V1)

- [ ] `ExBlockEntityBehavior : BlockEntityBehavior` with `Persisted`, `DeclareState` and the
  `[Persist]` scan, wired the way `BlockEntityStateHost` wires the block entities; `BEBehaviorFirebox`
  converts and leaves the allow-list. `BlockEntityBurdenmaker` gets a `Persisted` host through a
  small `ExBlockEntityContainer : BlockEntityContainer` base. Both proven by the tree-key goldens.
- [ ] `BlockEntityStateHost` logs one Warning when a type overrides `DeclareState` without reaching
  the base declaration for a type that declares members (detectable: the base's declared keys are
  absent from the state after `DeclareState` returns), so the trap the goldens caught is caught
  without goldens.


### Task V9: `ExModSystem` (added 2026-09-06 from the ease audit)

The zero-line rung for registration. `public abstract class ExModSystem : ModSystem` in
`mods/exlib/src/Registries/ExModSystem.cs`:
- `Start(api)`: `ExConfig.LoadAll(api, Assembly)` (new: finds every generated `*Values` accessor in
  the assembly through an attribute the generator stamps, `[ExConfigAccessor(typeof(TConfig))]`, and
  calls its `Load`), then `EntityRegistry.RegisterAll(api, Mod, Assembly)`, then `OnStart(api)`;
- `StartServerSide(api)`: `CommandRegistry.RegisterAll`, then `OnStartServerSide(api)`;
- `StartClientSide(api)`: `PreferenceRegistry.RegisterAll`, `CommandRegistry.RegisterAll`, then
  `OnStartClientSide(api)`;
- `AssetsFinalize(api)`: `OnAssetsFinalize(api)`; `Dispose`: `ExHarmony.UnpatchAll(Mod)` when
  `PatchHarmony` (a `protected virtual bool`, default false) had patched in `Start`.
- `protected virtual Assembly Assembly => GetType().Assembly`; the hooks are empty by default.
Tests: a test ModSystem deriving it over the test assembly registers the expected classes, commands
and preferences on each side and loads a config accessor found by attribute; the sample's
`HelloExpandedModSystem` becomes a class with an empty body (or one hook) and its tests stay green;
Getting-Started's section 4 shows the empty class first and the explicit calls second; Registries.md
leads with `ExModSystem`, then the per-side rule for the explicit calls; Supported-API row;
CHANGELOG. Effort M.

### Task V10: the pages lead with the shortest rung (added 2026-09-06 from the ease audit)

The nine doc-only fixes from the audit, in its ranked order: Helpers-and-Renderers gains a worked
usage snippet per helper section; Registries leads with the two-line registration (or `ExModSystem`
after V9) and states that command registration runs once per side; Getting-Started separates block
registration from command registration; Production-Machines shows the minimal machine before the
hosted-behaviour form; Commands trims the custom sub-command example to the derive-once form;
Code-First-Definitions opens with a minimal definition; Extending-Processes shows one job before two;
Block-Networks moves the commentary out of the fence; Testing-Harness leads with the smoke test.
Every first snippet is at most eight lines or the page says why not. Effort S each, M in total.

## Order

V1 after framework D5; V2 after V1 (so the goldens cover the attribute path too); V3, V4, V5 in any
order, one at a time; V6 last. Gate after each: `bash scripts/exmod.sh test latest`.
