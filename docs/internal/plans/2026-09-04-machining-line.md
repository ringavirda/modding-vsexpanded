# Machining line - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: use `superpowers:subagent-driven-development`
> (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking. No review workflow per task; the implementer runs the
> suite, the session owner reads the diff.

**Status** ready 2026-09-04 (roadmap Phase 2, items 6-17). Runs after Phase 1's walk. Owner rulings
baked in: every station ships at once; the shear takes the window; products land in the window's
output slot, never on the floor.

**Goal:** one `iiex:machinetool-{type}-{orientation}` blocktype with a `type` variant over the eight
stations - shear, drill press, horizontal bore, lathe, nail cutter, planer, riveter, shaper - sharing one
block, one block entity, one window and one hold-to-operate mechanic, so that a survival player can turn
a roll set on the lathe, bore a cast pipe segment, and cut a gear on a shaper driven by a vanilla axle.

**Architecture:** exlib gains the three generic pieces (a held-interaction tracker, a station window
driven by a spec, tool wear); iiex replaces `BlockShear`/`BlockFastenerBench` and their block entities
with `BlockMachineTool`/`BlockEntityMachineTool` over a per-type table (shape, footprint, cell roles,
tool kind, hold or auto). Jobs stay `ProcessJob` rows in `config/processjobs/`, chosen in the window
when an input matches more than one. Blanks are sand patterns; intermediates and tools are items.

**Tech stack:** C# multi-targeted net7/net8/net10 for VS 1.20/1.21/1.22, xUnit + NSubstitute on the
exlib harness (`TestWorld`, `TestBlocks`), python3 for `infra/tools/convert-shape.py`.

**Spec:** [machining-line.md](../../design/mechanics/machining-line.md) .
[machines.txt](../../../workbench/machines.txt) . [process-extension.md](../../design/mechanics/process-extension.md)
. [tooling-wear.md](../../design/mechanics/tooling-wear.md) . [rolling-mill.md](../../design/machines/rolling-mill.md)
section the movable roller. Code facts: [../research/](../research/README.md) - `station-base-and-jobs`,
`station-window-and-hold`, `footprints-shapes-defs`, `blanks-tooling-consumers`, `mill-flatwide-cells`.

## Global constraints

- **No commits.** Every "commit" step is a worklog line in `docs/internal/worklog/2026-09.md`, newest
  first, commit-message sized. The owner commits.
- **Style** ([CONTRIBUTING.md](../../../CONTRIBUTING.md)): two-space indent, braces on the same line,
  80 columns, file-scoped namespaces, one public type per file; comments describe, never narrate; no
  em-dash, no emoji, no first person. `scripts/exmod.sh format` only on a clean tree.
- **Gate per task:** `dotnet test VintageStory.sln -c Debug` green on 1.22. **Gate for the plan:**
  `scripts/exmod.sh test all` green on all nine targets. Any API member used must exist in
  `.game/1.20` and `.game/1.21` (grep the vsapi xml docs there).
- **Goldens** re-blessed by path (`EXLIB_WRITE_GOLDENS=iiex/blocktypes/machining/machinetool`), never
  wholesale; block codes regenerated with `EXLIB_WRITE_BLOCKCODES=1`; handbook with
  `EXLIB_WRITE_HANDBOOK=1`.
- **Lang:** every new key in `mods/iiex/assets/iiex/lang/{en,ru,uk}.json` (ru/uk per the localisation glossary in
  memory); literals passed to `Lang.Get("iiex:...")`, `ActionLangCode = "iiex:..."` and
  `SendIngameError("iiex-...")` are scanned by `LangCallSites`; block names need `block-machinetool-{type}-*`.
- **Codes:** `iiex:machinetool-{type}-{ns|we}`; `IiexCodePrefixTests` forbids a base code that prefixes
  another; `ReferencedCodes` must resolve every code a recipe or def names. iiex is unreleased, so
  `forming-shear` and `forming-bench` retire without a migration (`ReleasedCodes` covers ppex/smex).
- **Numbers** live on design pages; config keys below carry defaults marked *tune*.
- **Design decisions this plan fixes** (from the owner's rulings and the research): type keys
  `shear, drillpress, horizontalbore, lathe, nailcutter, planer, riveter, shaper`; the owner's Slice
  grids ("YZ slice, north is right") are transcribed with **north on the right**, i.e. mirrored against
  the code's `Slice` (which puts north on the left) - the exported shapes settle it in Task 8; a job
  whose input matches several jobs is chosen in the window's picker; the stroke of shear and drill press
  runs on the production clock, the other six run only while a player holds RMB on a hold cell.

---

## File structure

**exlib (create)**
- `mods/exlib/src/Blocks/Machines/HoldToOperate.cs` - who is holding, with expiry.
- `mods/exlib/src/Blocks/Machines/StationWindowSpec.cs` - what a station window shows.
- `mods/exlib/src/Blocks/Machines/IStationWindowHost.cs` - what the window reads from a block entity.
- `mods/exlib/src/Blocks/Machines/GuiDialogMachineStation.cs` - the shared window (client).

**exlib (modify)**
- `mods/exlib/src/Blocks/Structures/IFillerInteractionTarget.cs` - cancel forward (default member).
- `mods/exlib/src/Blocks/Structures/BlockStructureFiller.cs` - forwards `OnBlockInteractCancel`.
- `mods/exlib/src/Blocks/Structures/StructureFootprint.cs`, `FillerLayoutBuilder.cs`,
  `StructureFillers.cs`, `BlockEntityStructureFiller.cs`, `mods/exlib/src/Definitions/ExBlockDef.cs`
  - a per-cell `role` string.
- `mods/exlib/src/Blocks/Machines/BlockEntityMachineStation.cs` - protected dialog access, client
  refresh hook.
- `mods/exlib/src/Processes/MachineTool.cs`, `ItemDie.cs` - durability and wear; `ItemDie.JobsFor`.

**iiex (create)**
- `mods/iiex/src/BlockStructures/Machining/MachineToolTypes.cs` - the per-type table.
- `mods/iiex/src/BlockStructures/Machining/MachineToolFeed.cs` - verdict and decision.
- `mods/iiex/src/BlockStructures/Machining/Blocks/BlockMachineTool.cs`
- `mods/iiex/src/BlockStructures/Machining/BlockEntities/BlockEntityMachineTool.cs`
- `mods/iiex/src/BlockStructures/Casting/CastBlankItemDefinitions.cs` - blanks.
- `mods/iiex/src/Items/MachinedItemDefinitions.cs` - intermediates.
- `mods/iiex/src/Items/MachineCutterItemDefinitions.cs`, `DrillBitItemDefinitions.cs`.
- `mods/iiex/assets/iiex/config/processjobs/{lathe,horizontalbore,shaper,planer,drillpress}.json`
- `mods/iiex/assets/iiex/shapes/machining/{type}.json` (eight exports), `mods/iiex/assets/iiex/shapes/item/...` (blanks,
  intermediates, tools).
- `mods/iiex/docs/handbook/13-machineshop.html` + `mods/iiex/assets/iiex/config/handbook/13-machineshop.json`.

**iiex (delete)**
- `BlockStructures/Forming/Blocks/BlockShear.cs`, `BlockFastenerBench.cs`;
  `Forming/BlockEntities/BlockEntityShear.cs`, `BlockEntityFastenerBench.cs`; `Forming/ShearFeed.cs`,
  `Forming/BenchFeed.cs`; goldens `goldens/iiex/blocktypes/forming/{shear,bench}.json`; tests
  `Blocks/Forming/ShearStationTests.cs`, `FastenerBenchTests.cs` (rewritten under `Blocks/Machining/`).

**iiex (modify)**
- `Forming/BlockEntities/BlockEntityMpBench.cs` - stays the stroke base; gains a virtual
  `OnIdleTick` call from its production tick.
- `Recipes/Grid/FormingRecipeDefinitions.cs` - outputs repointed; new machine recipes.
- `IiexRecipeConfig.cs` - cost rows repointed and added.
- `BlockStructures/Forming/RollSetSpec.cs`, `RollSetItemDefinitions.cs`, `Blocks/BlockRollingMill.cs`,
  `BlockEntities/BlockEntityRollingMill.cs`, `Forming/MillFeed.cs`, `IiexConfig.cs` - the i1 cells.
- `infra/tools/convert-shape.py` - `drill` clip one-shot; nothing else.

---

### Task 1: HoldToOperate, the cancel forward and filler-cell roles (exlib)

**Files:**
- Create: `mods/exlib/src/Blocks/Machines/HoldToOperate.cs`
- Modify: `mods/exlib/src/Blocks/Structures/IFillerInteractionTarget.cs`,
  `mods/exlib/src/Blocks/Structures/BlockStructureFiller.cs` (beside its `OnBlockInteractStop`
  override), `mods/exlib/src/Blocks/Structures/StructureFootprint.cs` (`FillerCellSpec`),
  `mods/exlib/src/Blocks/Structures/FillerLayoutBuilder.cs`,
  `mods/exlib/src/Blocks/Structures/StructureFillers.cs` (`ReadOffsets`),
  `mods/exlib/src/Blocks/Structures/BlockEntityStructureFiller.cs`,
  `mods/exlib/src/Definitions/ExBlockDef.cs` (the filler serialiser next to `collisionBoxes`)
- Test: `mods/exlib/tests/Machines/HoldToOperateTests.cs`,
  `mods/exlib/tests/Structures/FillerCellRoleTests.cs`

**Interfaces:**
- Produces `HoldToOperate { void Step(IPlayer, long nowMs); void End(IPlayer); bool IsHeld(long nowMs);
  void Clear(); const int StaleMs = 1000 }`.
- Produces `IFillerInteractionTarget.OnFillerInteractCancel(float secondsUsed, IWorldAccessor world,
  IPlayer byPlayer, BlockSelection principalSel, BlockPos clickedCell, EnumItemUseCancelReason reason)
  => true` as a default interface member (no existing implementer changes).
- Produces `FillerCellSpec.Role` (string?, last positional parameter, default null),
  `FillerLayoutBuilder.Role(char symbol, string role)` (annotates an already registered glyph; throws on
  an unknown one), serialised as `"role": "..."`, read back into `BlockEntityStructureFiller.Role`
  (persisted like `PortFace`).

- [ ] **Step 1: write the failing tests**

```csharp
// mods/exlib/tests/Machines/HoldToOperateTests.cs
namespace ExpandedLib.Tests;

public class HoldToOperateTests {
  private static IPlayer Player(string uid) {
    IPlayer p = Substitute.For<IPlayer>();
    p.PlayerUID.Returns(uid);
    return p;
  }

  [Fact]
  public void A_stepping_player_holds_until_the_steps_stop() {
    var hold = new HoldToOperate();
    IPlayer p = Player("p1");
    Assert.False(hold.IsHeld(0));
    hold.Step(p, 1000);
    Assert.True(hold.IsHeld(1000 + HoldToOperate.StaleMs));
    Assert.False(hold.IsHeld(1001 + HoldToOperate.StaleMs));
  }

  [Fact]
  public void End_releases_at_once_and_a_second_holder_keeps_it_held() {
    var hold = new HoldToOperate();
    IPlayer a = Player("a"), b = Player("b");
    hold.Step(a, 0);
    hold.Step(b, 0);
    hold.End(a);
    Assert.True(hold.IsHeld(10));
    hold.End(b);
    Assert.False(hold.IsHeld(10));
  }
}
```

```csharp
// mods/exlib/tests/Structures/FillerCellRoleTests.cs
namespace ExpandedLib.Tests;

public class FillerCellRoleTests {
  [Fact]
  public void A_role_survives_the_def_round_trip() {
    IReadOnlyList<FillerCellSpec> cells = StructureFootprint.Layout(f => f
      .Solid('I').Role('I', "window hold").Origin(-1, 0).Face(0, "I O"));
    ExBlockDef def = ExBlockDef.Create("t", "x").FillerOffsets(cells);
    JsonObject offsets = new(def.ToJson()["fillerOffsets"]);
    IReadOnlyList<FillerCellSpec> back = StructureFillers.ReadOffsets(offsets);
    Assert.Equal("window hold", Assert.Single(back).Role);
  }

  [Fact]
  public void Annotating_an_unregistered_glyph_throws() {
    Assert.Throws<InvalidOperationException>(() =>
      StructureFootprint.Layout(f => f.Role('Q', "hold").Origin(0, 0).Face(0, "O")));
  }
}
```

- [ ] **Step 2: run them** - `dotnet test mods/exlib/tests --filter "HoldToOperateTests|FillerCellRoleTests"`.
      Expected: compile failure (`HoldToOperate`, `Role` missing).

- [ ] **Step 3: implement**

```csharp
// mods/exlib/src/Blocks/Machines/HoldToOperate.cs
namespace ExpandedLib.Blocks.Machines;

/// <summary>
/// The players holding right-click on a machine's hold cell. A holder is live
/// while its last step is at most <see cref="StaleMs"/> old; the block's
/// interact-step hook refreshes it every tick, so a released mouse or a lost
/// player expires by itself. Server-side state, never persisted.
/// </summary>
public sealed class HoldToOperate {
  public const int StaleMs = 1000;
  private readonly Dictionary<string, long> _lastStepMs = new();

  public void Step(IPlayer player, long nowMs) =>
    _lastStepMs[player.PlayerUID] = nowMs;

  public void End(IPlayer player) => _lastStepMs.Remove(player.PlayerUID);

  public bool IsHeld(long nowMs) {
    foreach (string uid in _lastStepMs
        .Where(kv => nowMs - kv.Value > StaleMs).Select(kv => kv.Key).ToList()) {
      _lastStepMs.Remove(uid);
    }
    return _lastStepMs.Count > 0;
  }

  public void Clear() => _lastStepMs.Clear();
}
```

`IFillerInteractionTarget`: add the default member exactly as in Interfaces. `BlockStructureFiller`:
override `OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer,
BlockSelection blockSel, EnumItemUseCancelReason cancelReason)`; resolve the principal as the existing
`OnBlockInteractStop` override does and return
`target.OnFillerInteractCancel(secondsUsed, world, byPlayer, Repoint(blockSel, principal), blockSel.Position, cancelReason)`;
otherwise `base.OnBlockInteractCancel(...)`.

`FillerCellSpec`: append `string? Role = null`. `FillerLayoutBuilder.Role(char symbol, string role)`:
look the glyph up in the builder's registry (the same dictionary `Solid`/`Slab`/`Host`/`Port` write),
throw `InvalidOperationException($"glyph '{symbol}' is not registered")` when absent, otherwise store the
role and copy it into every cell that glyph produces in `Build`. Serialiser: after `allowAttach`, emit
`role` when non-null. `StructureFillers.ReadOffsets`: read `role` (string, null when absent).
`BlockEntityStructureFiller`: `public string? Role { get; private set; }`, written by the same path that
sets `PortFace` from the cell spec, persisted under `"role"` in `ToTreeAttributes`/`FromTreeAttributes`.

- [ ] **Step 4: run the two test classes; expected PASS. Run the whole exlib suite; expected green
      (the golden serialiser only adds a key when a role exists, so no golden changes).**
- [ ] **Step 5: worklog line** - "exlib: HoldToOperate, filler cancel forward, per-cell roles".

---

### Task 2: the shared station window (exlib)

**Files:**
- Create: `mods/exlib/src/Blocks/Machines/StationWindowSpec.cs`,
  `mods/exlib/src/Blocks/Machines/IStationWindowHost.cs`,
  `mods/exlib/src/Blocks/Machines/GuiDialogMachineStation.cs`
- Modify: `mods/exlib/src/Blocks/Machines/BlockEntityMachineStation.cs` (the private `_dialog`
  field becomes `protected GuiDialogBlockEntity? Dialog { get; private set; }`; add
  `protected virtual void OnClientStateChanged()` invoked at the end of `FromTreeAttributes` when
  `Api?.Side == EnumAppSide.Client`), `mods/exlib/src/Processes/ItemDie.cs` (`JobsFor`)
- Test: `mods/exlib/tests/Machines/StationWindowSpecTests.cs`,
  `mods/exlib/tests/Processes/ItemDieJobsForTests.cs`

**Interfaces:**
- Produces
  `record StationWindowSpec(string TitleKey, int ToolSlot, int InputSlot, int OutputSlot, string? ToolLabelKey, string? InputLabelKey, string? OutputLabelKey, bool ShowJobPicker, bool ShowProgress)`
  with `ToolSlot = -1` meaning no tool slot and `bool HasToolSlot => ToolSlot >= 0`.
- Produces `interface IStationWindowHost { StationWindowSpec WindowSpec { get; } IReadOnlyList<StationChoice> Choices(); string? SelectedChoice { get; } int SelectChoicePacketId { get; } float Progress { get; } }`
  and `readonly record struct StationChoice(string Code, string Name)`.
- Produces `GuiDialogMachineStation(string title, InventoryBase inventory, BlockPos pos, ICoreClientAPI capi, IStationWindowHost host) : GuiDialogBlockEntity`.
- Produces `ItemDie.JobsFor(ItemStack? die, string machine, string input) : IReadOnlyList<ProcessJob>`
  - every job on the die whose `Machine` equals `machine` (ordinal-ignore-case) and whose input matches.

- [ ] **Step 1: failing tests**

```csharp
// mods/exlib/tests/Machines/StationWindowSpecTests.cs
public class StationWindowSpecTests {
  [Fact]
  public void A_spec_without_a_tool_slot_says_so() {
    var spec = new StationWindowSpec("exlib:station-title", ToolSlot: -1, InputSlot: 0,
      OutputSlot: 1, null, null, null, ShowJobPicker: false, ShowProgress: true);
    Assert.False(spec.HasToolSlot);
  }
}

// mods/exlib/tests/Processes/ItemDieJobsForTests.cs
public class ItemDieJobsForTests {
  [Fact]
  public void Only_jobs_for_the_named_machine_and_input_come_back() {
    var world = new TestWorld();
    ItemStack die = DieStack(world, machine: "lathe",
      ("t:blank", "t:a"), ("t:blank", "t:b"), ("t:other", "t:c"));
    IReadOnlyList<ProcessJob> jobs = ItemDie.JobsFor(die, "lathe", "t:blank");
    Assert.Equal(new[] { "t:a", "t:b" }, jobs.Select(j => j.Output));
    Assert.Empty(ItemDie.JobsFor(die, "shaper", "t:blank"));
  }
  // DieStack: build the item exactly as FastenerBenchTests.Die does today, from
  // JToken.FromObject(ItemDie.Job(machine, input, output)) merged into one machinejob set.
}
```

- [ ] **Step 2: run; expected compile failure.**
- [ ] **Step 3: implement.** `StationWindowSpec` as declared. `ItemDie.JobsFor`: parse with the
      existing `TryParse`, filter `set.Machine` and `job.Matches(input, null, null)`.
      `GuiDialogMachineStation` mirrors `GuiDialogDesignTable`'s structure:

```csharp
// mods/exlib/src/Blocks/Machines/GuiDialogMachineStation.cs
namespace ExpandedLib.Blocks.Machines;

/// <summary>
/// The window every machine station opens: a tool slot, an input slot, an
/// output slot, a picker for the job when the input matches more than one, and
/// a progress bar. What it shows comes from the host's
/// <see cref="StationWindowSpec"/>; the picker sends the host's own packet id.
/// </summary>
public class GuiDialogMachineStation : GuiDialogBlockEntity {
  private const string PickerKey = "jobpicker";
  private const string ProgressKey = "progress";
  private readonly IStationWindowHost _host;
  private readonly ICoreClientAPI _capi;
  private readonly BlockPos _pos;
  private readonly string _title;

  public GuiDialogMachineStation(string title, InventoryBase inventory, BlockPos pos,
      ICoreClientAPI capi, IStationWindowHost host) : base(title, inventory, pos, capi) {
    _host = host; _capi = capi; _pos = pos; _title = title;
    if (IsDuplicate) { return; }
    Inventory.SlotModified += OnSlotModified;
    Compose();
  }

  private void Compose() {
    StationWindowSpec spec = _host.WindowSpec;
    ElementBounds bg = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
    ElementBounds dialog = ElementStdBounds.AutosizedMainDialog
      .WithAlignment(EnumDialogArea.RightMiddle)
      .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);
    bg.BothSizing = ElementSizing.FitToChildren;
    ElementBounds tool = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 30, 1, 1);
    ElementBounds input = ElementStdBounds.SlotGrid(EnumDialogArea.None, 60, 30, 1, 1);
    ElementBounds output = ElementStdBounds.SlotGrid(EnumDialogArea.None, 180, 30, 1, 1);
    ElementBounds picker = ElementBounds.Fixed(0, 90, 230, 25);
    ElementBounds progress = ElementBounds.Fixed(0, 125, 230, 12);
    GuiComposer c = _capi.Gui.CreateCompo("machinestation" + _pos, dialog)
      .AddShadedDialogBG(bg, true).AddDialogTitleBar(_title, CloseIconPressed)
      .BeginChildElements(bg);
    if (spec.HasToolSlot) {
      c.AddItemSlotGrid(Inventory, DoSendPacket, 1, new[] { spec.ToolSlot }, tool, "tool");
    }
    c.AddItemSlotGrid(Inventory, DoSendPacket, 1, new[] { spec.InputSlot }, input, "input")
     .AddItemSlotGrid(Inventory, DoSendPacket, 1, new[] { spec.OutputSlot }, output, "output");
    if (spec.ShowJobPicker) {
      IReadOnlyList<StationChoice> choices = _host.Choices();
      string[] codes = choices.Select(ch => ch.Code).ToArray();
      string[] names = choices.Select(ch => ch.Name).ToArray();
      int selected = Math.Max(0, Array.IndexOf(codes, _host.SelectedChoice));
      c.AddDropDown(codes, names, selected, OnChoiceSelected, picker, PickerKey);
    }
    if (spec.ShowProgress) {
      c.AddStatbar(progress, GuiStyle.FoodBarColor, ProgressKey);
    }
    SingleComposer = c.EndChildElements().Compose();
  }

  private void OnChoiceSelected(string code, bool selected) =>
    _capi.Network.SendBlockEntityPacket(_pos, _host.SelectChoicePacketId,
      SerializerUtil.Serialize(code));

  private void OnSlotModified(int slotId) { if (SingleComposer != null) { Compose(); } }

  public override void OnRenderGUI(float deltaTime) {
    base.OnRenderGUI(deltaTime);
    SingleComposer?.GetStatbar(ProgressKey)?.SetValue(_host.Progress * 100f);
  }

  public override void Dispose() {
    Inventory.SlotModified -= OnSlotModified;
    base.Dispose();
  }
}
```

Adapt element names to what `GuiDialogDesignTable` and `GuiDialogWorkbench` use in this tree (bounds
helpers, `GetStatbar`, statbar range 0-100 via `SetValues(value, 0, 100)` if `SetValue` is absent).
The composer is client-only and is verified in game (Task 14); nothing headless can compose it.

- [ ] **Step 4: run the two test classes; expected PASS; exlib suite green.**
- [ ] **Step 5: worklog line** - "exlib: shared station window (spec + dialog), ItemDie.JobsFor".

---

### Task 3: tool wear (exlib)

**Files:**
- Modify: `mods/exlib/src/Processes/MachineTool.cs`, `mods/exlib/src/Processes/ItemDie.cs`
- Test: `mods/exlib/tests/Processes/MachineToolWearTests.cs`

**Interfaces:**
- `MachineTool.Itemtype(string domain, string code, IReadOnlyDictionary<string,int> tiers, string shape = "game:item/ingot", string variantGroup = "type", IReadOnlyDictionary<string,int>? durability = null)` - emits `durabilityByType["*-{type}"]` when given.
- `ItemDie.Itemtype(..., IReadOnlyDictionary<string,int>? durability = null)` likewise.
- `static bool MachineTool.Wear(ItemSlot slot, IWorldAccessor world, EntityAgent? byEntity, int points = 1)` - damages the stack; returns false when the tool broke (slot emptied).

- [ ] **Step 1: failing test**

```csharp
public class MachineToolWearTests {
  [Fact]
  public void Wear_counts_down_and_breaks_the_tool_at_zero() {
    var world = new TestWorld();
    Item tool = world.RegisterItem("t:cutter-iron");
    tool.Durability = 2;
    var slot = new DummySlot(new ItemStack(tool));
    Assert.True(MachineTool.Wear(slot, world, null));
    Assert.Equal(1, slot.Itemstack!.Attributes.GetInt("durability"));
    Assert.False(MachineTool.Wear(slot, world, null));
    Assert.Null(slot.Itemstack);
  }

  [Fact]
  public void The_itemtype_emits_durability_by_type() {
    ExItemDef def = MachineTool.Itemtype("t", "cutter",
      new Dictionary<string, int> { ["iron"] = 1 }, variantGroup: "metal",
      durability: new Dictionary<string, int> { ["iron"] = 200 });
    Assert.Equal(200, (int)def.ToJson()["durabilityByType"]!["*-iron"]!);
  }
}
```

- [ ] **Step 2: run; expected compile failure / FAIL.**
- [ ] **Step 3: implement.** `Wear`:
      `slot.Itemstack?.Collectible.DamageItem(world, byEntity, slot, points); return slot.Itemstack != null;`
      (vanilla `DamageItem` removes the stack at zero and plays the break sound; if the headless world
      rejects the sound call, guard the call with `world.Side == EnumAppSide.Server` the way vanilla
      does). `Itemtype`: after `attributesByType`, emit `RawByType("durabilityByType", $"*-{type}", value)`
      per entry.
- [ ] **Step 4: run; PASS; exlib suite green.**
- [ ] **Step 5: worklog line** - "exlib: MachineTool/ItemDie durability + Wear".

---

### Task 4: the machine-tool table and definitions (iiex)

**Files:**
- Create: `mods/iiex/src/BlockStructures/Machining/MachineToolTypes.cs`,
  `mods/iiex/src/BlockStructures/Machining/Blocks/BlockMachineTool.cs` (definitions only
  in this task; interaction comes in Task 6)
- Delete: `Forming/Blocks/BlockShear.cs`, `Forming/Blocks/BlockFastenerBench.cs` (their BEs go in Task 5),
  `mods/iiex/tests/goldens/iiex/blocktypes/forming/shear.json`, `bench.json`
- Modify: `mods/iiex/src/Recipes/Grid/FormingRecipeDefinitions.cs` (outputs
  `iiex:machinetool-shear-ns`, `-nailcutter-ns`, `-riveter-ns`), `mods/iiex/src/IiexRecipeConfig.cs`
  (rows `shear-grid`, `nailcutter-grid`, `riveter-grid` now match `iiex:machinetool-{type}-*`),
  `mods/iiex/assets/iiex/lang/{en,ru,uk}.json` (`block-machinetool-{type}-*` for all eight; drop `block-forming-shear-*`,
  `block-forming-nailcutter-*`, `block-forming-riveter-*`)
- Test: `mods/iiex/tests/Definitions/MachineToolDefinitionTests.cs`; regenerate
  `mods/iiex/src/Generated/IiexBlocks.g.cs`

**Interfaces:**
- Produces `enum ToolKind { Blade, Cutter, DrillBit, Die }` and
  `sealed record MachineToolType(string Key, string Shape, IReadOnlyList<FillerCellSpec> Footprint, string PrincipalRoles, ToolKind ToolKind, bool RequiresHold, bool Staged)`
  with `static MachineToolTypes.All : IReadOnlyList<MachineToolType>` and `static MachineToolType For(string? key)`
  (throws on an unknown key).
- Produces the def `iiex:machinetool-{type}-{ns|we}` with `shapeByType` `iiex:machining/{type}` at
  rotateY 0 / 90 and `fillerOffsetsByType` per type.

- [ ] **Step 1: failing tests**

```csharp
public class MachineToolDefinitionTests {
  private static ExBlockDef Def() => BlockMachineTool.Definitions("iiex").Single();

  [Theory]
  [InlineData("shear", 4)] [InlineData("drillpress", 7)] [InlineData("horizontalbore", 4)]
  [InlineData("lathe", 7)] [InlineData("nailcutter", 3)] [InlineData("planer", 15)]
  [InlineData("riveter", 4)] [InlineData("shaper", 3)]
  public void Every_type_declares_its_footprint(string type, int cells) {
    JsonObject offsets = new(Def().ToJson()["attributesByType"]![$"*-{type}-*"]!["fillerOffsets"]);
    Assert.Equal(cells, StructureFillers.ReadOffsets(offsets).Count);
  }

  [Theory]
  [InlineData("lathe", 0, 1, -1, "hold")] [InlineData("horizontalbore", 0, 0, 1, "hold")]
  [InlineData("nailcutter", 0, 1, 0, "window hold")] [InlineData("shear", -1, 0, 0, "window")]
  public void Roles_sit_on_the_cells_the_layout_names(string type, int x, int y, int z, string role) {
    JsonObject offsets = new(Def().ToJson()["attributesByType"]![$"*-{type}-*"]!["fillerOffsets"]);
    FillerCellSpec cell = StructureFillers.ReadOffsets(offsets)
      .Single(c => c.X == x && c.Y == y && c.Z == z);
    Assert.Equal(role, cell.Role);
  }

  [Theory] [InlineData("ns", 0)] [InlineData("we", 90)]
  public void The_mesh_and_the_footprint_turn_together(string o, int angle) {
    foreach (MachineToolType m in MachineToolTypes.All) {
      int shape = (int)Def().ToJson()["shapeByType"]![$"*-{m.Key}-{o}"]!["rotateY"]!;
      Block block = TestBlocks.Configure(new BlockMachineTool(),
        $"iiex:machinetool-{m.Key}-{o}", 1, ("type", m.Key), ("orientation", o));
      Assert.Equal(angle, shape);
      Assert.Equal(angle, ((BlockMachineTool)block).StructureAngle);
    }
  }
}
```

- [ ] **Step 2: run; expected compile failure.**
- [ ] **Step 3: implement the table** (roles from the transcription in
      `research/2026-09-04-footprints-shapes-defs.md` section 2, with the Slice grids mirrored so north is on
      the right as the owner's file says):

```csharp
// mods/iiex/src/BlockStructures/Machining/MachineToolTypes.cs
namespace IronIndustryExpanded.BlockStructures.Machining;

public enum ToolKind { Blade, Cutter, DrillBit, Die }

/// <summary>One station of the machine-tool family: its shape, footprint, cell
/// roles ("window", "hold", "drive-ns", "drive-we", space separated), tooling
/// kind, whether the stroke needs a held right-click, and whether its jobs are
/// staged crops.</summary>
public sealed record MachineToolType(string Key, string Shape,
  IReadOnlyList<FillerCellSpec> Footprint, string PrincipalRoles, ToolKind ToolKind,
  bool RequiresHold, bool Staged);

public static class MachineToolTypes {
  private static IReadOnlyList<FillerCellSpec> Layout(Action<FillerLayoutBuilder> a) =>
    StructureFootprint.Layout(a);

  public static readonly MachineToolType Shear = new("shear", "iiex:machining/shear",
    Layout(f => f.Solid('I').Role('I', "window").Slab('_', BlockFacing.DOWN).Origin(-1, 1)
      .Face(0, "_ _ _ / I O #")), "", ToolKind.Blade, RequiresHold: false, Staged: true);

  public static readonly MachineToolType DrillPress = new("drillpress",
    "iiex:machining/drillpress",
    Layout(f => f.Slab('I', BlockFacing.NORTH).Role('I', "window").Solid('M')
      .Role('M', "drive-ns").Origin(-1, 2).Slice(0, "# # # / # # I / M O I")),
    "", ToolKind.DrillBit, false, false);

  public static readonly MachineToolType HorizontalBore = new("horizontalbore",
    "iiex:machining/horizontalbore",
    Layout(f => f.Solid('m').Role('m', "drive-we").Slab('i', BlockFacing.NORTH)
      .Role('i', "hold").Solid('I').Role('I', "window").Origin(-1, 0)
      .Layer(0, "m O # / . i .").Layer(1, "# I # / . . .")),
    "", ToolKind.Cutter, true, false);

  public static readonly MachineToolType Lathe = new("lathe", "iiex:machining/lathe",
    Layout(f => f.Slab('_', BlockFacing.SOUTH).Slab('i', BlockFacing.SOUTH).Role('i', "hold")
      .Solid('I').Role('I', "window").Solid('m').Role('m', "drive-we").Origin(-1, -1)
      .Layer(0, "_ _ _ / # O m").Layer(1, ". i . / I I #")),
    "", ToolKind.Cutter, true, false);

  public static readonly MachineToolType NailCutter = new("nailcutter",
    "iiex:machining/nailcutter",
    Layout(f => f.Solid('I').Role('I', "window hold").Solid('m').Role('m', "drive-we")
      .Origin(-1, 1).Slice(0, "m I / # O")),
    "", ToolKind.Die, true, false);

  public static readonly MachineToolType Planer = new("planer", "iiex:machining/planer",
    Layout(f => f.Solid('I').Role('I', "window").Slab('i', BlockFacing.EAST).Role('i', "hold")
      .Slab('_', BlockFacing.SOUTH).Slab('-', BlockFacing.NORTH).Solid('M')
      .Role('M', "drive-ns").Origin(-2, -1)
      .Layer(0, ". # # M . / I I O # # / . . . - .")
      .Layer(1, ". . i _ . / . . i # . / . . i - .")
      .Layer(2, ". . . _ . / . . . # . / . . . - .")),
    "window", ToolKind.Cutter, true, false);

  public static readonly MachineToolType Riveter = new("riveter", "iiex:machining/riveter",
    Layout(f => f.Solid('I').Role('I', "window hold").Solid('M').Role('M', "drive-ns")
      .Origin(-1, 1).Face(0, "# M # / I O #")),
    "", ToolKind.Die, true, false);

  public static readonly MachineToolType Shaper = new("shaper", "iiex:machining/shaper",
    Layout(f => f.Solid('I').Role('I', "window hold").Solid('m').Role('m', "drive-we")
      .Origin(-2, 1).Slice(0, "# # I / . m O")),
    "", ToolKind.Cutter, true, false);

  public static readonly IReadOnlyList<MachineToolType> All =
    new[] { Shear, DrillPress, HorizontalBore, Lathe, NailCutter, Planer, Riveter, Shaper };

  public static MachineToolType For(string? key) =>
    All.FirstOrDefault(m => m.Key == key)
    ?? throw new InvalidOperationException($"no machine tool type '{key}'");
}
```

`BlockMachineTool.Definitions(domain)`: the sketch in `research/2026-09-04-footprints-shapes-defs.md`
section "Recommended def shape", with `Create(domain, "machinetool", "machining/machinetool")`,
`.VariantGroup("type", All.Select(m => m.Key))`, `.VariantGroup("orientation", "ns", "we")`,
`.NetworkOriented()`, one `ShapeByType` pair and one `FillerOffsetsByType` per type,
`.CreativeCommon("*-ns")`, `.EntityBehavior("Animatable")`, `.SolidNonOpaque()`,
`.Handbook("machinetool-*")`. `StructureAngle => Variant?["orientation"] == "we" ? 90 : 0`. Keep the
`FillerOffsets` and placement-triad members from `BlockShear` that the BE will need; drop `BlockShear`
and `BlockFastenerBench` and their goldens. Expected cell counts: the shear's four (three slabs + I),
the drill press's seven, the bore's four, the lathe's seven, the nail cutter's three, the planer's
fifteen, the riveter's four, the shaper's three - if a count differs, recount the grid before touching
the test.

- [ ] **Step 4: bless the golden** - `EXLIB_WRITE_GOLDENS=iiex/blocktypes/machining/machinetool dotnet test mods/iiex/tests --filter Regenerate_goldens_when_requested`;
      `EXLIB_WRITE_BLOCKCODES=1 dotnet test ...` for `IiexBlocks.g.cs`; run the definition tests; PASS.
- [ ] **Step 5: repoint recipes, cost rows and names; run the iiex suite.** Expected failures only in
      the deleted stations' tests (they go in Task 5) and in `IiexRecipeOutputTests` until outputs are
      repointed. Green after.
- [ ] **Step 6: worklog line** - "iiex: machinetool blocktype over eight stations, forming-shear/bench retired".

---

### Task 5: the block entity and the feed decision (iiex)

**Files:**
- Create: `mods/iiex/src/BlockStructures/Machining/MachineToolFeed.cs`,
  `mods/iiex/src/BlockStructures/Machining/BlockEntities/BlockEntityMachineTool.cs`
- Delete: `Forming/BlockEntities/BlockEntityShear.cs`, `BlockEntityFastenerBench.cs`, `Forming/ShearFeed.cs`,
  `Forming/BenchFeed.cs`, `mods/iiex/tests/.../Blocks/Forming/ShearStationTests.cs`, `FastenerBenchTests.cs`
- Modify: `Forming/BlockEntities/BlockEntityMpBench.cs` - the production tick calls
  `protected virtual void OnIdleTick()` when not stroking (default no-op)
- Test: `mods/iiex/tests/Blocks/Machining/MachineToolStationTests.cs`

**Interfaces:**
- `enum MachineToolVerdict { Ok, NoTool, NoJob, Spent, ToolTooSoft, NotTurning, NotEnoughDrive, OutputFull, NotHeld }`
- `readonly record struct MachineToolDecision(MachineToolVerdict Verdict, ProcessJob? Job, float RequiredTorque)` with `bool Accepted => Verdict == MachineToolVerdict.Ok`
- `MachineToolFeed.Decide(bool hasTool, int toolTier, ProcessJob? job, WorkPiece? piece, float tempC, float rollingTempC, float coldMultiplier, float availableTorque, float speed, bool outputFree, bool heldOk)`
  - the `ShearFeed.Decide` order, then `OutputFull`, then `NotHeld`.
- `BlockEntityMachineTool : BlockEntityMpBench, IStationWindowHost` with slots `ToolSlot = 0, InputSlot = 1, OutputSlot = 2`,
  `SelectChoicePacket = FirstMachinePacketId`, `MachineKey => Type.Key`, `Type => MachineToolTypes.For(Block?.Variant?["type"])`,
  `IReadOnlyList<ProcessJob> CandidateJobs()`, `ProcessJob? SelectedJob()`, `MachineToolDecision TryBeginStroke()`,
  `void HoldStep(IPlayer)`, `void HoldEnd(IPlayer)`, `bool IsHeld`.

- [ ] **Step 1: failing tests** (copy the fixture shape of the deleted `ShearStationTests`:
      `TestWorld`, `RegisterNetwork("mpenergy", ...)`, `TestBlocks.Configure(new BlockMachineTool(), $"iiex:machinetool-{type}-ns", 1, ("type", type), ("orientation", "ns"))`,
      `world.Place`, `world.Initialize(be)`, a `TestMachineTool : BlockEntityMachineTool` that injects
      `Jobs`, and the `Turning`/`Stopped`/`Advance` helpers).

```csharp
public class MachineToolStationTests {
  [Fact]
  public void A_shear_crop_lands_in_the_output_slot_and_the_remainder_stays_in_the_input() {
    (TestWorld world, TestMachineTool be) = Station("shear", Registry("shear", count: 4));
    be.Inventory[BlockEntityMachineTool.ToolSlot].Itemstack = Blades(world, tier: 1);
    be.Inventory[BlockEntityMachineTool.InputSlot].Itemstack = Stock(world);
    Turning(world, be.Pos);
    Assert.Equal(MachineToolVerdict.Ok, be.TryBeginStroke().Verdict);
    Advance(be, 2.5f);
    ItemStack output = be.Inventory[BlockEntityMachineTool.OutputSlot].Itemstack!;
    Assert.Equal("game:rod-iron", output.Collectible.Code.ToString());
    ItemStack remainder = be.Inventory[BlockEntityMachineTool.InputSlot].Itemstack!;
    Assert.Equal(1, WorkPiece.FromStack(remainder)!.Cropped);
    Assert.Empty(world.Drops);
  }

  [Fact]
  public void A_held_station_advances_only_while_held() {
    (TestWorld world, TestMachineTool be) = Station("lathe", Registry("lathe", count: 1, seconds: 2));
    be.Inventory[0].Itemstack = Cutter(world, tier: 1);
    be.Inventory[1].Itemstack = world.Stack("t:blank");
    Turning(world, be.Pos);
    Assert.Equal(MachineToolVerdict.NotHeld, be.TryBeginStroke().Verdict);
    be.HoldStep(Player("p"));
    Assert.Equal(MachineToolVerdict.Ok, be.TryBeginStroke().Verdict);
    Advance(be, 1f);
    be.HoldEnd(Player("p"));
    Advance(be, 5f);
    Assert.True(be.IsStroking);
    be.HoldStep(Player("p"));
    Advance(be, 1.5f);
    Assert.NotNull(be.Inventory[2].Itemstack);
  }

  [Fact]
  public void Two_matching_jobs_need_a_choice_and_the_choice_is_honoured() {
    (TestWorld world, TestMachineTool be) = Station("lathe",
      Registry("lathe", ("t:blank", "t:a"), ("t:blank", "t:b")));
    be.Inventory[0].Itemstack = Cutter(world, 1);
    be.Inventory[1].Itemstack = world.Stack("t:blank");
    Turning(world, be.Pos);
    be.HoldStep(Player("p"));
    Assert.Equal(MachineToolVerdict.NoJob, be.TryBeginStroke().Verdict);
    be.OnReceivedClientPacket(Player("p"), BlockEntityMachineTool.SelectChoicePacket,
      SerializerUtil.Serialize("t:b"));
    Assert.Equal("t:b", be.TryBeginStroke().Job!.Output);
  }

  [Fact]
  public void A_die_station_reads_its_jobs_off_the_die_and_refuses_another_machines_die() {
    (TestWorld world, TestMachineTool be) = Station("nailcutter", Registry("nailcutter"));
    be.Inventory[0].Itemstack = Die(world, "nailcutter", "iiex:nailplate", "game:metalnailsandstrips-iron", 4);
    be.Inventory[1].Itemstack = world.Stack("iiex:nailplate");
    Turning(world, be.Pos);
    be.HoldStep(Player("p"));
    Assert.Equal(MachineToolVerdict.Ok, be.TryBeginStroke().Verdict);
    Advance(be, 2.5f);
    Assert.Equal(4, be.Inventory[2].Itemstack!.StackSize);
    be.Inventory[0].Itemstack = Die(world, "riveter", "iiex:nailplate", "iiex:rivet", 2);
    be.Inventory[1].Itemstack = world.Stack("iiex:nailplate");
    Assert.Equal(MachineToolVerdict.NoJob, be.TryBeginStroke().Verdict);
  }

  [Fact]
  public void A_full_output_slot_stops_the_stroke_from_starting() {
    (TestWorld world, TestMachineTool be) = Station("shear", Registry("shear", count: 4));
    be.Inventory[0].Itemstack = Blades(world, 1);
    be.Inventory[1].Itemstack = Stock(world);
    be.Inventory[2].Itemstack = world.Stack("iiex:rivet", 64);
    Turning(world, be.Pos);
    Assert.Equal(MachineToolVerdict.OutputFull, be.TryBeginStroke().Verdict);
  }

  [Fact]
  public void The_tool_wears_one_point_per_stroke_and_a_broken_tool_refuses_the_next() {
    (TestWorld world, TestMachineTool be) = Station("shear", Registry("shear", count: 4));
    be.Inventory[0].Itemstack = Blades(world, 1, durability: 1);
    be.Inventory[1].Itemstack = Stock(world);
    Turning(world, be.Pos);
    be.TryBeginStroke();
    Advance(be, 2.5f);
    Assert.Null(be.Inventory[0].Itemstack);
    Assert.Equal(MachineToolVerdict.NoTool, be.TryBeginStroke().Verdict);
  }
}
```

- [ ] **Step 2: run; expected compile failure.**
- [ ] **Step 3: implement.** `MachineToolFeed.Decide` generalises `ShearFeed.Decide` (same order:
      no tool -> no job -> spent -> tier -> turning -> drive) and appends `OutputFull` and `NotHeld`.
      The block entity:

```csharp
public sealed class BlockEntityMachineTool : BlockEntityMpBench, IStationWindowHost {
  public const int ToolSlot = 0, InputSlot = 1, OutputSlot = 2;
  public const int SelectChoicePacket = FirstMachinePacketId;
  private readonly HoldToOperate _hold = new();
  private string? _selectedChoice;
  private ProcessJob? _job;
  private float _tempC, _requiredTorque, _strokeSeconds;

  public MachineToolType Type => MachineToolTypes.For(Block?.Variant?["type"]);
  public string MachineKey => Type.Key;
  public ProcessJobRegistry Jobs { get; protected set; } = ProcessJobRegistry.Shared;
  public override string InventoryClassName => "machinetool";
  protected override MachineSlotSpec[] SlotSpecs => new[] {
    MachineSlotSpec.Input(AcceptsTool, "#6A5A3A"), MachineSlotSpec.AnyInput(),
    MachineSlotSpec.Output() };

  private bool AcceptsTool(ItemStack? s) =>
    Type.ToolKind == ToolKind.Die ? ItemDie.IsDie(s) : MachineTool.IsTool(s);
  private ItemStack? Tool => Inventory[ToolSlot].Itemstack;
  private ItemStack? Input => Inventory[InputSlot].Itemstack;
  public bool IsHeld => _hold.IsHeld(Api.World.ElapsedMilliseconds);

  public IReadOnlyList<ProcessJob> CandidateJobs() {
    if (Input == null) { return Array.Empty<ProcessJob>(); }
    string code = Input.Collectible.Code.ToShortString();
    if (Type.ToolKind == ToolKind.Die) { return ItemDie.JobsFor(Tool, MachineKey, code); }
    WorkPiece? piece = WorkPiece.FromStack(Input);
    return Jobs.Jobs(MachineKey)
      .Where(j => j.Matches(code, piece?.Thickness, piece?.Family)).ToList();
  }

  public ProcessJob? SelectedJob() {
    IReadOnlyList<ProcessJob> c = CandidateJobs();
    if (c.Count == 0) { return null; }
    if (c.Count == 1) { return c[0]; }
    return c.FirstOrDefault(j => j.Output == _selectedChoice);
  }

  public MachineToolDecision TryBeginStroke() {
    if (IsStroking) { return new(MachineToolVerdict.NotTurning, null, 0f); }
    ProcessJob? job = SelectedJob();
    WorkPiece? piece = WorkPiece.FromStack(Input);
    float tempC = Input == null ? 0f : Input.Collectible.GetTemperature(Api.World, Input);
    int tier = Type.ToolKind == ToolKind.Die ? 0 : MachineTool.TierOf(Tool);
    bool hasTool = Type.ToolKind == ToolKind.Die ? ItemDie.IsDie(Tool) : tier >= 0;
    MachineToolDecision d = MachineToolFeed.Decide(hasTool, tier, job, piece, tempC,
      IiexValues.RollingTempC, IiexValues.ShearColdMultiplier, AvailableTorque, Speed,
      OutputHasRoom(job), !Type.RequiresHold || IsHeld);
    if (!d.Accepted) { return d; }
    _job = job; _tempC = tempC; _requiredTorque = d.RequiredTorque;
    _strokeSeconds = job!.Seconds;
    BeginStroke(job.Seconds);
    return d;
  }

  public override bool AdvanceStroke(float dt) =>
    (!Type.RequiresHold || IsHeld) && base.AdvanceStroke(dt);

  protected override void OnIdleTick() { if (!Type.RequiresHold) { TryBeginStroke(); } }

  protected override void CompleteStroke() {
    ProcessJob? job = _job; ItemStack? input = Input;
    if (job == null || input == null) { AbandonStroke(); return; }
    ItemStack product = Resolve(job.Output, job.Stage == null ? job.Count : 1)!;
    if (Type.Staged) {
      Inventory[InputSlot].Itemstack = WorkPiece.FromStack(input)!.Crop(1).ToStack(input);
    } else {
      Inventory[InputSlot].TakeOut(1);
    }
    product.Collectible.SetTemperature(Api.World, product, _tempC);
    Inventory[OutputSlot].Itemstack = Merge(Inventory[OutputSlot].Itemstack, product);
    MachineTool.Wear(Inventory[ToolSlot], Api.World, null);
    Inventory[InputSlot].MarkDirty(); Inventory[OutputSlot].MarkDirty(); MarkDirty(true);
  }

  public override float LoadTorque(float speed) => IsStroking ? _requiredTorque : 0f;

  protected override bool OnStationPacket(IPlayer player, int packetid, byte[] data) {
    if (packetid != SelectChoicePacket) { return false; }
    _selectedChoice = SerializerUtil.Deserialize<string>(data);
    MarkDirty(true);
    return true;
  }

  public void HoldStep(IPlayer p) => _hold.Step(p, Api.World.ElapsedMilliseconds);
  public void HoldEnd(IPlayer p) => _hold.End(p);

  public StationWindowSpec WindowSpec => new("iiex:machinetool-title", ToolSlot, InputSlot,
    OutputSlot, "iiex:machinetool-tool", "iiex:machinetool-input", "iiex:machinetool-output",
    ShowJobPicker: true, ShowProgress: true);
  public IReadOnlyList<StationChoice> Choices() => CandidateJobs()
    .Select(j => new StationChoice(j.Output, Lang.Get(NameKey(j.Output)))).ToList();
  public string? SelectedChoice => _selectedChoice;
  public int SelectChoicePacketId => SelectChoicePacket;
  public float Progress => _strokeSeconds <= 0f ? 0f : 1f - Remaining / _strokeSeconds;
  protected override GuiDialogBlockEntity? CreateDialog(ICoreClientAPI capi) =>
    new GuiDialogMachineStation(Lang.Get("iiex:machinetool-title"), Inventory, Pos, capi, this);
}
```

`OutputHasRoom(job)`: the output slot is empty, or holds the job's output collectible below its max
stack. `Merge` adds to an existing stack. `WorkPiece.Crop(1)` is the existing tally; `ToStack(input)`
writes it back onto a clone of the input with its heat. Persist `_selectedChoice` (`"mtChoice"`),
`_tempC` (`"mtTempC"`), `_requiredTorque` (`"mtTorque"`), `_strokeSeconds` (`"mtSeconds"`) beside
`benchRemaining`; the hold is not persisted. `NameKey(code)` is vanilla's `item-`/`block-` name key
for a code. `OnIdleTick` is the new hook in `BlockEntityMpBench` called by its production tick when
`!IsStroking`.

- [ ] **Step 4: run the six tests; PASS; iiex suite green** (`ShippedCropTableTests` keeps working;
      `ProcessExtensionGuards.No_process_machine_names_a_product_in_code` must still pass - the BE names
      no product).
- [ ] **Step 5: worklog line** - "iiex: BlockEntityMachineTool: one stroke engine for eight stations, window slots, hold gate, job choice, wear".

---

### Task 6: interaction routing, window and hold cells, lang (iiex)

**Files:**
- Modify: `BlockStructures/Machining/Blocks/BlockMachineTool.cs` (interaction), `mods/iiex/assets/iiex/lang/{en,ru,uk}.json`
- Test: `mods/iiex/tests/Blocks/Machining/MachineToolInteractionTests.cs`

**Interfaces:** `BlockMachineTool : BlockNetworkNode, IExBlockDefProvider, IFillerHost, IFillerInteractionTarget`
with `string RolesAt(IWorldAccessor world, BlockPos principal, BlockPos clickedCell)` (the principal's
`PrincipalRoles`, else the filler entity's `Role`, else empty).

- [ ] **Step 1: failing tests** - with the Task 5 fixture and a filler placed by the block's own
      placement path: (a) a click on a `window` cell calls `ToggleWindow` on the client side (assert via a
      test subclass flag) and returns true; (b) a hold cell: `OnFillerInteractStart` returns true and
      `OnFillerInteractStep` keeps the entity held (`IsHeld`), `OnFillerInteractStop` releases it, and
      `OnFillerInteractCancel` releases it too; (c) a wrench mid-stroke abandons the stroke and keeps the
      input; (d) a tool in hand on any cell fits it into the tool slot (the old shortcut), a die on a
      die station likewise.
- [ ] **Step 2: run; FAIL.**
- [ ] **Step 3: implement.** `HandleInteract(world, player, principalSel, clickedCell)` in this order:
      wrench and stroking -> `AbandonStroke`, error `iiex-machinetool-freed`; tool or die in hand ->
      fit into the tool slot (refused mid-stroke: `iiex-machinetool-busy`); roles contain `window` ->
      client: `be.ToggleWindow(player)`; return true; roles contain `hold` -> server: `be.HoldStep(player)`
      then `be.TryBeginStroke()`, report a refusing verdict through `SendIngameError("iiex-machinetool-{verdict}")`
      once per hold start; return true (so Step follows). `OnFillerInteractStep` and the principal's own
      `OnBlockInteractStep`: if roles contain `hold` -> `HoldStep`; return true. Stop and Cancel -> `HoldEnd`.
      `GetFillerInteractionHelp` per role: `iiex:machinetool-help-window`, `-hold`, `-fittool`, `-free`.
      Lang keys (en/ru/uk): `block-machinetool-{type}-*` x8, `iiex:machinetool-title`, `-tool`,
      `-input`, `-output`, `-help-window`, `-help-hold`, `-help-fittool`, `-help-free`,
      `game:ingameerror-iiex-machinetool-{notool,nojob,spent,tooltoosoft,notturning,notenoughdrive,outputfull,notheld,busy,freed}`,
      `iiex:machinetool-info-tool` ("Tool: {0}"), `-info-notool`, `-info-working` ("Working - {0} s left"),
      `-info-hold` ("Hold right-click on the working cell to run it"). `GetBlockInfo` prints tool, job
      choice, working/hold lines.
- [ ] **Step 4: run; PASS; `IiexLangCoverageTests` and `LangParityTests` green.**
- [ ] **Step 5: worklog line.**

---

### Task 7: the drive cells (iiex, one spike then the implementation)

**Files:**
- Modify: `MachineToolTypes.cs` (drive-cell hosting), `BlockMachineTool.cs` (connector faces),
  `mods/exlib/src/Blocks/Structures/FillerLayoutBuilder.cs` only if the spike needs a second `Port`
  overload
- Test: `mods/iiex/tests/Blocks/Machining/MachineToolDriveTests.cs`

**Spike first (one step, no product code):** stand a `lathe` in the Task 5 fixture with a cast shaft
node (`BlockCastIronShaft`) placed against the `m` cell's east face, and assert
`world.NetworkAt(shaftPos) == world.NetworkAt(be.Pos)`. Try (a) `Port('m', BlockFacing.EAST, "mpenergy")`
plus a second port glyph for west, and (b) `Host('m', FillerBehaviorSpec.Of<BEBehaviorNetworkMember>(properties: new { networkType = "mpenergy", passThrough = true }))`.
Keep whichever couples; record the result in the worklog line and in
`docs/design/mechanics/multiblock.md`'s connector-versus-node table.

- [ ] **Step 1: failing test** - the coupling assertion above for `lathe` (east/west) and `riveter`
      (north/south), at both orientations; plus: with the principal's own axis faces no longer connectors
      (the drive moved to the cell), a shaft against the principal does **not** couple.
- [ ] **Step 2: run; FAIL.**
- [ ] **Step 3: implement** per the spike; `BlockMachineTool.HasConnectorAt` answers the drive cell's
      faces, rotated by `StructureAngle`, and nothing else. The shaper's `m` cell additionally hosts
      `FillerBehaviorSpec.Of<BEBehaviorMPFillerPort>("east")` and `("west")` so a vanilla axle turns it:
      the shaper's `Speed` reads the vanilla port's `Speed` when the mpenergy run has no state. One extra
      test: a shaper on a vanilla axle only (`BEBehaviorMPFillerPort.Speed` faked to 1) strokes; a lathe on
      a vanilla axle only does not.
- [ ] **Step 4: run; PASS; suite green; re-bless the machinetool golden (the cells gained
      behaviours or ports).**
- [ ] **Step 5: worklog line** - name the mechanism the spike chose.

---

### Task 8: shape exports and the art reconciliation list

**Files:**
- Create: `mods/iiex/assets/iiex/shapes/machining/{shear,drillpress,horizontalbore,lathe,nailcutter,planer,riveter,shaper}.json`
- Modify: `infra/tools/convert-shape.py` (`ONESHOT_CLIPS` gains `"drill"`)
- Delete: `mods/iiex/assets/iiex/shapes/forming/{shear,nailcutter,riveter}.json` after the new files exist
- Test: `Every_shape_reference_resolves_to_a_shipped_file` already covers the paths;
  `mods/iiex/tests/Invariants/MachineToolShapeExtentsTests.cs`

- [ ] **Step 1: failing test** - for each type, load the runtime shape, measure its extent in cells
      (the `ShapeExtents` helper in `mods/exlib/testing/ShapeExtents.cs`), and assert it lies inside
      the footprint's bounding box **plus the overhang the owner accepts**, expressed as one allowance
      per type in the test's table. Seed the table from the measured extents in
      `research/2026-09-04-footprints-shapes-defs.md` section 3 and mark each row that exceeds its footprint:
      drill press (3.6 tall, 2.7 deep against one column), lathe (over x by 4-6 voxels, y by 8, south
      slab by 3.5), planer (1.5 cells above y=2, 9 voxels past x=2), bore (5 voxels into z=-1), shaper
      (6 voxels into z=-2).
- [ ] **Step 2: run; FAIL (the shapes are not exported).**
- [ ] **Step 3: export** - 
      `python3 infra/tools/convert-shape.py machines/mpenergy/machine-mp-megablock-cutter mods/iiex/assets/iiex/shapes/machining/shear.json`
      and the same for drillpress, horizontalboring->horizontalbore, lathe, nailcutter, planer, riveter,
      shaper; `--check` first; add `drill` to `ONESHOT_CLIPS` so the drill press's one-shot clip does not
      loop; the `iron`/`iron2` keys remap by key (plate -> tarnished / sheet) - accept it.
- [ ] **Step 4: put the reconciliation in front of the owner** - the rows that exceed their footprint
      need either the art trimmed or the footprint grown; the test carries the allowance the owner picks.
      Until ruled, the allowance equals the measured overhang so the suite is green and the mismatch is
      recorded, not hidden.
- [ ] **Step 5: run; PASS; worklog line listing the overhang rulings still owed.**

---

### Task 9: cast blanks (iiex)

**Files:**
- Create: `mods/iiex/src/BlockStructures/Casting/CastBlankItemDefinitions.cs`,
  `mods/iiex/assets/iiex/shapes/item/castblank-{cylinder,cylinderheavy,gearsmall,gearlarge,shaft,rollers,pipepart}.json`
- Modify: `PatternItemDefinitions.cs` (`Molds` rows and `PatternShapes` rows), `DiagramItemDefinitions.cs`
  (the item-diagram types gain seven), lang x3 (`item-castblank-*`, `item-pattern-cast{...}-*`,
  `item-diagram-item-cast{...}`), the storage-rack occupancy catalogue
- Test: `mods/iiex/tests/Definitions/CastBlankTests.cs`

**Interfaces:** items `iiex:castblank-{type}` (one itemtype, `type` group), patterns
`iiex:pattern-cast{cylinder,cylinderheavy,gearsmall,gearlarge,shaft,rollers,pipepart}-{wood}`;
`CastBlankItemDefinitions.Units[type]` in units.

- [ ] **Step 1: compute the masses** - a script step, recorded in the worklog: for each drawn blank
      under `workbench/shapes/items/sandcast/blanks/`, sum the volume of its named root's cubes in
      voxels, multiply by 2.5 u/vx^3, round to the nearest 5. The roller blank is two rollers; the pipe
      part has no drawing - take the cast straight segment's shell volume as the blank and flag it for art.
- [ ] **Step 2: failing tests** - each blank has a pattern whose mold `Capacity` equals its units,
      `Output` names the blank, and `Size` is `LongCell` for `rollers` (16 voxels long) and `Cell` for the
      rest; every blank resolves through `ReferencedCodes`; every pattern type has a diagram.
- [ ] **Step 3: implement** - `Molds` rows via the existing `Mold(shape, capacity, cavity, output)`
      helper; fillings: reuse `cell-filling-{axle,cylinder,gearblanklarge,gearblanksmall}` where they exist
      (axle -> shaft) and flag `cylinderheavy`, `rollers` and `pipepart` as NEEDS-ART, using the plain
      `cell-filling-base` until drawn; blank item shapes exported from the editables; occupancy rows for
      the rack (1 cell each, rollers 2).
- [ ] **Step 4: bless goldens by path (`iiex/itemtypes/pattern`, `iiex/itemtypes/castblank`,
      `iiex/itemtypes/diagram`, `iiex/recipes/grid/pattern`); run; PASS.**
- [ ] **Step 5: worklog line with the computed masses.**

---

### Task 10: machined intermediates and the tools (iiex)

**Files:**
- Create: `mods/iiex/src/Items/MachinedItemDefinitions.cs`,
  `mods/iiex/src/Items/MachineCutterItemDefinitions.cs`,
  `mods/iiex/src/Items/DrillBitItemDefinitions.cs`, shapes exported from
  `items/machined/*` and `items/smithed/item-forged-machinecutter`, `item-forged-machinedrill`
- Modify: `Recipes/Grid/FormingRecipeDefinitions.cs` (tool recipes), `IiexRecipeConfig.cs` (rows),
  `ShearBladeItemDefinitions.cs` and `BenchDieItemDefinitions.cs` (durability), lang x3
- Test: `mods/iiex/tests/Definitions/MachinedItemTests.cs`

**Interfaces:** `iiex:machined-{cylinder,cylinderheavy,cylinderbored,gearcone,gearfaced,geardrilled}`;
`iiex:machinecutter-{iron,steel}` (tiers 1/2), `iiex:drillbit-{iron,steel}`; durability config keys
`MachineCutterDurabilityIron = 120`, `...Steel = 400`, `DrillBitDurabilityIron = 80`, `...Steel = 300`,
`ShearBladeDurabilityIron = 200`, `...Steel = 600`, `DieDurability = 150` (*tune*) in `IiexConfig`.

- [ ] **Step 1: failing tests** - each machined item has a shape file; the cutter and bit itemtypes
      carry `machinetool` tiers and `durabilityByType`; the recipes `PP,PP,_H` (cutter: 4 plates of the
      metal + hammer) and `_P_,_P_,_H_` (bit: 2 plates + hammer) output the right codes; every code
      resolves.
- [ ] **Step 2: run; FAIL.**
- [ ] **Step 3: implement** with `MachineTool.Itemtype(domain, "machinecutter", tiers, "iiex:item/machinecutter", "metal", durability)`
      and the same for the bit; `ExItemDef` for the intermediates (`MaterialDensity(7200)`,
      `materialUnits` attribute: cylinder = blank units, bored = the same, gear intermediates = the
      large blank's units, mass conserved).
- [ ] **Step 4: bless by path; PASS. Step 5: worklog.**

---

### Task 11: job tables and their guard (iiex)

**Files:**
- Create: `mods/iiex/assets/iiex/config/processjobs/{lathe,horizontalbore,shaper,planer,drillpress}.json`
- Test: `mods/iiex/tests/Definitions/ShippedJobTableTests.cs` (generalises
  `ShippedCropTableTests` over every file), `mods/iiex/tests/Invariants/JobCodesResolveTests.cs`

The roster (`minTorque` and `seconds` are *tune* values; `minTier` 1 = iron tooling):

| file | input -> output x count | minTier |
|---|---|---|
| lathe | `iiex:castblank-rollers` -> `iiex:rollset-flat` x1 . -> `iiex:rollset-grooved` x1 . -> `iiex:rollset-flatwide` x1 (three rows, chosen in the window) | 1 |
| lathe | `iiex:castblank-shaft` -> `iiex:mpenergy-shaft-ns` x2 | 1 |
| lathe | `iiex:castblank-cylinder` -> `iiex:machined-cylinder`; `castblank-cylinderheavy` -> `machined-cylinderheavy`; `castblank-gearlarge` -> `machined-gearcone` | 1 |
| horizontalbore | `iiex:machined-cylinderheavy` -> `iiex:machined-cylinderbored`; `iiex:castblank-pipepart` -> each of `iiex:pipe-cast-{straight,bend,tjunction,xjunction}-ns` x1 (four rows, chosen in the window) | 1 |
| shaper | `iiex:castblank-gearsmall` -> `iiex:spurgear` x1; `iiex:machined-geardrilled` -> `iiex:gear-iron` x2; `iiex:machined-gearcone` -> `iiex:bevelgear` x1 | 1 |
| planer | `iiex:castblank-gearlarge` -> `iiex:machined-gearfaced` | 1 |
| drillpress | `iiex:machined-gearfaced` -> `iiex:machined-geardrilled` | 1 |

- [ ] **Step 1: failing tests** - every file parses through `ProcessJobSet.TryParse` with its
      `machine` equal to a `MachineToolTypes` key; every input and output resolves against the union
      catalogue the way `ReferencedCodes` resolves recipe codes; the lathe's roller row set has three
      outputs for one input (the picker case) and the bore's pipe-part row set four.
- [ ] **Step 2: run; FAIL. Step 3: write the JSON (schema 1, the fields
      `input, output, count, minTorque, minTier, seconds`). Step 4: run; PASS. Step 5: worklog.**

---

### Task 12: the mill's raise-and-lower cells (iiex)

The change set is fully specified in `research/2026-09-04-mill-flatwide-cells.md` section "Minimal change set";
this task executes it.

**Files:** `Forming/RollSetSpec.cs` (`Adjustable`), `Forming/RollSetItemDefinitions.cs` (flatwide
`adjustable: true`), `Forming/BlockEntities/BlockEntityRollingMill.cs` (`WideGap`, `TryStepWideGap`,
`WideGapIndex`, persisted `rmWideGap`, block info lines), `Forming/Blocks/BlockRollingMill.cs` (the two
raise/lower cells `(0,1,0)` and `(-2,1,0)` get role `hold` through `Role('i', "hold")` on the existing
stand-row glyph, `OnFillerInteractStep` steps the gap every `RollingWideGapHoldSeconds`, sneak lowers
and plain raises, `Feed` uses `WideGapIndex` when the set is adjustable), `Forming/MillFeed.cs`
(`FeedVerdict.GapOffLadder`), `IiexConfig.cs` (`RollingWideGapMin 1.0`, `Max 3.5`, `Step 0.5`,
`HoldSeconds 0.5`), lang x3 (`rollingmill-help-raise/-lower`, `rollingmill-info-rollset/-gap`,
`ingameerror-iiex-rollingmill-gaplocked/-gapoffladder`), handbook clause in `10-formingshop`.
**Test:** `mods/iiex/tests/Blocks/Forming/RollingMillWideGapTests.cs` with the eight
cases the research names (clamp, refused while rolling, persists, feed uses the stored gap, 4.0 at 3.0
won't bite, off-ladder refused, locked set refused, cells resolve at both facings) plus the
`ShippedRollSetTests` addition (every adjustable rung on the step grid).

- [ ] Steps as in the research file, test first, run, implement, run, worklog; re-bless
      `iiex/itemtypes/rollset`.

---

### Task 13: recipes, cost rows, handbook, locales (iiex)

**Files:** `Recipes/Grid/FormingRecipeDefinitions.cs` (five new machine recipes), `IiexRecipeConfig.cs`
(`{type}-grid` rows), `mods/iiex/docs/handbook/13-machineshop.html` + `mods/iiex/assets/iiex/config/handbook/13-machineshop.json`,
lang x3 (handbook text key, `handbook-machineshop-title`).

Recipes (plates are heavy cast plates `V`, rods `R`, spur gear `G`, hammer `H`; costs are the owner's to
retune in the cost catalogue): drill press `_R_,VGV,VHV`; horizontal bore `VRV,VGV,VHV` with `G` x2;
lathe `VRV,VGV,RHR` with `G` x2; planer `VVV,VGV,RHR` with `G` x2; shaper `_R_,VGV,_H_`.

- [ ] **Step 1: failing tests** - `IiexRecipeOutputTests` sees five new outputs; a cost row exists
      per type; the handbook parity test wants the page.
- [ ] **Step 2-4:** write recipes and rows; write the page in the voice of `10-formingshop.html`
      (the five verbs, the bootstrap gear, the roll set on the lathe, cast pipe on the bore, the picker,
      hold-to-operate, tool wear); `EXLIB_WRITE_HANDBOOK=1`; ru/uk by hand; bless `iiex/recipes/grid/forming`.
- [ ] **Step 5: worklog.**

---

### Task 14: the gate

Survival, in game, by the owner (the walkthrough conventions of
[2026-09-04-phase1-walkthrough.md](2026-09-04-phase1-walkthrough.md) apply):

- [ ] Cast a roller blank in the long cell, turn a flat set on the lathe (window: blank in, cutter in,
      choose "Flat Roll Set", hold RMB on the turret cell), fit it to the mill and roll a bar.
- [ ] Cast a pipe-part, bore a straight cast segment (choose it in the picker), place it, raise the
      Bessemer vessel's stage that wanted it.
- [ ] Cut a pinion on a shaper standing on a vanilla axle; cut a bevel from a turned cone; turn a
      shaft.
- [ ] Every station's window opens from its window cell, every held station stops the moment RMB is
      released, the drill press and the shear run unattended, a worn cutter breaks and the station
      reports "no tool".
- [ ] `scripts/exmod.sh test all` green on all nine targets.

## Self-review notes

- Spec coverage: roster, bootstrap loop (the existing chisel routes stay as the hand route; the shaper
  on a vanilla axle is Task 7), tooling (Task 3, 10), window (Task 2, 6), hold (Task 1, 5, 6),
  footprints (Task 4), exports (Task 8), blanks (Task 9), job tables (Task 11), the wide roller (Task 12),
  handbook (Task 13). Not covered, by design: the planer's frame and bed jobs and the keyed shaft (no
  consumer), the bending roller, scale and swarf.
- Names used across tasks: `HoldToOperate`, `StationWindowSpec`, `IStationWindowHost`, `StationChoice`,
  `GuiDialogMachineStation`, `MachineToolType`, `MachineToolTypes`, `ToolKind`, `MachineToolVerdict`,
  `MachineToolDecision`, `MachineToolFeed`, `BlockMachineTool`, `BlockEntityMachineTool`,
  `FillerCellSpec.Role`, `FillerLayoutBuilder.Role`, `ItemDie.JobsFor`, `MachineTool.Wear`,
  `SelectChoicePacket` / `SelectChoicePacketId`.
