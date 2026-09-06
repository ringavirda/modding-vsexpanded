# Research snapshot - ease audit

**Written** 2026-09-06 by a read-only research agent, against the working tree of branch
`ironmaking-expanded` (commit `7886760b` plus uncommitted wiki edits in flight - `Testing-Harness.md`
was mid-edit by another builder and was read once, not re-checked).
**Covers** exlib convenience plan Task V7: for every wiki page that documents a capability, the line
count of the first snippet a reader needs for a visible result, its rung, and whether the page states
shorter rungs when they exist; three cross-cutting ease facts against source.
**Purpose** grounding for closing Task V7 of `docs/internal/plans/2026-09-05-exlib-convenience-plan.md`
and for any V9+ tasks it spawns.

Line numbers were right on 2026-09-06 and drift; cite by heading/symbol when reusing. Nothing here is a
decision - decisions belong on the plan page. "Inherent" verdicts are this agent's own judgment calls,
not rulings.

---

## Method

Line counts are non-blank lines inside the fence (comments count as code lines; the fence delimiters
do not). For each page, "the first snippet" is the first fenced example under the heading that
documents the page's capability that a reader could actually use to see a result - not a bare
class/interface shape shown only as a reference (those are marked and skipped past). Where getting a
result genuinely needs two fences in sequence (declare, then use), both are named and both counts
given; the table's "lines" column is the fence that most directly produces the result, per capability
in the task brief.

Rung labels follow the convenience plan's three rungs: **default** (zero-config, nothing to write),
**declarative** (data/attribute, no imperative code), **explicit** (the full imperative API).

Skipped per the brief: `Home.md` (index, no capability), `Supported-API.md` and
`Testing-API-Reference.md` (both are member-surface reference dumps with no before/after usage
pattern - `TestWorld`'s listing at `Testing-API-Reference.md:24-83` is the class shape, not a worked
example; the worked example lives on `Testing-Harness.md` instead). `Source-Generators.md` **is**
included: `Source-Generators.md:29-37` is a usage snippet (an `[ExConfigRegister]` class), not just a
reference. `Lifecycle.md` has no fences and documents ordering, not a capability with a visible result
- skipped.

## The table

Sorted by line count of the representative fence, descending.

| Page | Capability | Rung | Lines | States shorter rungs? |
|---|---|---|---|---|
| Getting-Started | a registered block | declarative | 27 | Partially - no shorter form exists to state (declarative is already shortest), but the shown snippet bundles block+BE registration with *both* client and server command registration, none of which a bare registered block needs |
| Code-First-Definitions | a block defined without JSON | explicit | 21 | No - the page's first usage snippet is a real shipped block (mpenergy transmission) with filler offsets, two behaviors and a construction stage; no smaller example precedes it |
| Testing-Harness | a passing test | explicit | 20 | No - "A first test" (`Testing-Harness.md:170-194`) is a three-node network-merge test; nothing smaller precedes it |
| Commands | a running command (one you add) | explicit | 17 | No - the built-in `/exmod` commands are stated as needing 0 lines (a table, `Commands.md:12-19`), which is good, but the "add your own" example includes description text, a privilege check and a full handler where the interface needs only `ParentName` + `Register` |
| Block-Networks | a network node on a graph | explicit | 15 | Partially - `RegisterNetworkType` (8 lines, `Block-Networks.md:49-58`) is shown first as its own smaller step, but the node-block definition that actually gives you a node carries a 3-line explanatory comment inline with the code |
| Registries | a registered block | declarative | 13 | No - the fence stacks four attribute spellings (bare, custom code, `PrefixModId=false`, entity register) where the true minimal is the first two lines; a markdown table already carries the six-attribute list two sections up and could carry the variants too |
| Multiblock-Structures | a placed multiblock | declarative | 13 | Yes - explicitly names three rungs in prose ("Zero-config"/"Declarative"/"the explicit API", `Multiblock-Structures.md:166-196`) before the JSON snippet |
| Helpers-and-Renderers | rotation math (`ExOrientation`) | n/a | 13 | No usage rung exists at all - the first fence (`Helpers-and-Renderers.md:12-26`) is the static class's member list, not a call; there is no "here is one call, here is what it does" example anywhere before it |
| Migrations-and-Healing | a migration applied | explicit | 10 | No shorter rung exists (a migration is an interface implementation by nature); close to inherent |
| Production-Machines | a machine with a production tick | explicit | 9 | No - a 14-line behavior-hosting pattern (`Production-Machines.md:37-54`) is shown *before* the self-contained 9-line "Minimal machine" (`:72-83`); the harder form leads |
| Recipe-Costs | a recipe cost profile applied | explicit | 9 | No shorter rung shown or evidently possible - `RecipeProfile` needs five required lambdas |
| Extending-Processes | a job table (no C#) | declarative | 9 | Partially - the page states plainly no C# is needed, but the one shown example carries two job entries where one would demonstrate the shape |
| Source-Generators | a config value via the generator | declarative | 7 | Yes - leads with the attribute-tagged class, the shortest input form |
| Config-System | a config value read | declarative + explicit | 5 (+18 to declare) | Yes - "Three rungs" is not named here as it is on Block-Entities, but the page does show declare (18 lines) then a 5-line read/write block, in that order, which is the only order that makes sense for this capability |
| Checks | a check result | default | 2 | Yes, and unusually well - rung 1 (automatic, on world load) is stated as needing 0 lines before rung 2 (`/exmod verify`, 2 lines) even appears |
| Block-Entities | a saved field | default | 1 | Yes, best on the whole wiki - "Three rungs" (`Block-Entities.md:67-84`) leads with `[Persist]` (1 line) before `Persisted`/`DeclareState` and before hand-written |
| Construction | RCC behaviour wired | declarative | 1 | Yes - the one-line JSON behaviour entry is the whole of it; the public C# surface shown after is reference, not required |

17 pages audited (of the 22 in the wiki minus `_Footer`/`_Sidebar`, `Home`, `Supported-API`,
`Testing-API-Reference`; `Lifecycle` excluded for having no capability snippet).

## Verdicts and proposed tasks, entries over eight lines

All of these are documentation-only fixes (reorder or trim an existing wiki example); none needs a new
exlib capability, so none of them is a V-numbered code task. They are listed in the plan's task shape
for consistency, with `Files:` naming only the wiki page.

### Getting-Started (27 lines) - NOT inherent
**Files:** `mods/exlib/wiki/Getting-Started.md` (the "3. Register your content" fence, `:108-141`).
**Fix:** split into two fences - block+BE registration alone (`[BlockRegister]`/`[BlockEntityRegister]`
+ a `Start` override calling `EntityRegistry.RegisterAll`, about 8 lines) as the answer to "a registered
block", then a second, explicitly optional fence for command registration on both sides, linked to
[Commands](Commands). No test; no interface; **effort S**.

### Code-First-Definitions (21 lines) - not inherent, but a real example is legitimately long
**Files:** `mods/exlib/wiki/Code-First-Definitions.md` (`:71-93`).
**Fix:** lead with a 4-5 line minimal definition (`ExBlockDef.Create(domain, code).Class<T>()` and
nothing else) before the real mpenergy transmission example, which stays as the "what a full one looks
like" case immediately after. No test; no interface; **effort S**.

### Testing-Harness (20 lines) - largely inherent, ordering can still improve
**Files:** `mods/exlib/wiki/Testing-Harness.md` ("A first test", currently `:170-194` - a builder is
mid-edit on this page; re-check the line numbers before touching it).
**Fix:** an even smaller smoke test (place one block, `Assert.Equal` on `world.GetBlock`) ahead of the
three-node merge test, which then demonstrates the graph behaviour the smoke test can't. **Effort S**,
but coordinate timing with whoever is editing this page now.

### Commands (17 lines) - not inherent
**Files:** `mods/exlib/wiki/Commands.md` (`:28-47`).
**Fix:** trim the shown example to `ParentName` + `Register` + a one-line `HandleWith`, dropping the
description/privilege lines into a following paragraph as "you'll usually also want". **Effort S**.

### Block-Networks (15 lines) - near-inherent
**Files:** `mods/exlib/wiki/Block-Networks.md` (`:78-95`).
**Fix:** move the 3-line orientation-derivation comment out of the code fence and into prose above it,
and consider showing a node with one variant group before the full `type`x`orientation` pair. **Effort S**.

### Registries (13 lines) - not inherent
**Files:** `mods/exlib/wiki/Registries.md` (`:34-46`).
**Fix:** show the plain `[BlockRegister]` + class (2 lines) as the capability's answer; move the other
three spellings (custom code, `PrefixModId=false`, `[BlockEntityRegister]`) into the existing attribute
table or a labeled list below the minimal example. **Effort S**.

### Multiblock-Structures (13 lines) - inherent
A multiblock layout is a shape by nature; you cannot draw a shape in fewer lines than the shape has
distinct rows without losing the example's point. The page already leads with the right framing (three
rungs stated in prose, zero-config and declarative named before the explicit API); no further action.

### Helpers-and-Renderers (13 lines) - not inherent, and this is a real gap
**Files:** `mods/exlib/wiki/Helpers-and-Renderers.md` (`:12-26`, the `ExOrientation` section, and
by the same pattern every other helper section on this page - `ExMeshCache`, `ExHighlightSlots`,
`ExParticles`, `ExSounds`, `ExInventory` and beyond).
**Fix:** this page never shows one line of actual usage, anywhere, for any of its helpers - every
section is a static class's member list. Add a one-to-three line call-site example to each section
(e.g. `int angle = ExOrientation.AngleFromSide(block.Variant["side"]); var target =
ExOrientation.GlobalPos(pos, 1, 0, 0, angle);`). No interface, no test, no source change - docs only,
but touches every section on the page. **Effort M** (breadth, not depth).

### Migrations-and-Healing (10 lines) - inherent
A migration is an interface implementation with at least one remap; nothing shorter says anything.
Trimming the explanatory comment (3 lines) would get it to 7, which is a small win, not a missing rung.

### Production-Machines (9 lines) - not inherent, an ordering problem
**Files:** `mods/exlib/wiki/Production-Machines.md`.
**Fix:** move "Minimal machine" (currently `:72-83`) to appear directly after the
`BlockEntityProductionMachine` class shape, ahead of the `BlockEntityRollingMill`
behavior-hosting example (currently `:37-54`), which is real but not self-contained (it calls
`owner.OnPassTick`, defined nowhere in the snippet) and reads as harder than it is because it comes
first. **Effort S**.

### Recipe-Costs (9 lines) - inherent
`RecipeProfile` has five `required` members; the object-initializer form shown is already the
shortest legal spelling.

### Extending-Processes (9 lines) - not quite inherent
**Files:** `mods/exlib/wiki/Extending-Processes.md` (`:34-44`).
**Fix:** drop the second (staged) job from the example array, leaving one whole-item job; the staged
form gets its own follow-on example where the "staged job crops" distinction is explained just below.
**Effort S.**

## Three cross-cutting facts

**1. `using` lines a machine mod needs.** `samples/HelloExpanded/src/*.cs` imports six distinct
`ExpandedLib.*` namespaces across the whole sample (`Blocks`, `Config`, `Definitions`, `Helpers`,
`Machines`, `Registries` - `grep -h "^using ExpandedLib" samples/HelloExpanded/src/*.cs | sort -u`).
`mods/iiex/src`'s 239 non-empty `.cs` files average **2.39** `ExpandedLib.*` using-lines each (572
total). The sample is proportionately heavier per file than iiex's average, which is expected - it is
one file per concept, written to be read, not a production machine's file that only touches the one or
two namespaces its own job needs.

**2. Calls a `ModSystem` makes to register everything.**
`samples/HelloExpanded/src/HelloExpandedModSystem.cs` makes **4** calls across its three lifecycle
methods: `HelloValues.Load(api)` and `EntityRegistry.RegisterAll(...)` in `Start`, then
`CommandRegistry.RegisterAll(...)` once each in `StartClientSide` and `StartServerSide` (repeated,
not shared - a `IExSubCommand` registers per side). `Registries.md` documents `EntityRegistry.RegisterAll`
(`:52-64`) and `CommandRegistry.RegisterAll` (`:100-103`) but only ever shows **one** call to the
latter, with the parenthetical "Register them from `Start` (or the side-specific start methods)"
(`Registries.md:104`) - it never states that a mod with both client and server sub-commands needs the
call twice, once per side. A reader who registers commands only in `Start` gets no error; a
server-only sub-command still finds its parent, and only a client-side one silently never appears
client-side. This is a real gap between the sample (4 calls) and what Registries.md's own snippets
show (implies 3).

**3. Do the five most-visited pages' first snippets compile against today's API?** Checked by name
against source signatures (not by running `dotnet`, per the brief):
- **Getting-Started** - `EntityRegistry.RegisterAll(ICoreAPI, Mod, Assembly?)` and
  `CommandRegistry.RegisterAll(ICoreAPI, Mod, Assembly?)` match `mods/exlib/src/Registries/Entities/EntityRegistry.cs:27`
  and `mods/exlib/src/Registries/Commands/CommandRegistry.cs:25` exactly, including the default
  `asm = null`.
- **Block-Entities** - `[Persist] private float _tempC;` matches `PersistAttribute(string? key = null)`
  at `mods/exlib/src/Blocks/PersistAttribute.cs:11`; `s.Float("temp", () => _tempC, v => _tempC = v)`
  matches `ExBlockState.Float(string, Func<float>, Action<float>)` at
  `mods/exlib/src/Blocks/ExBlockState.cs:65`.
- **Config-System** - the shown `IiexConfig` properties (`BoilerWaterIntakeFillFraction`,
  `PumpWaterPerSecond`, `RecipeLevel`) are the real property names and defaults in
  `mods/iiex/src/IiexConfig.cs:807,1018,1125` (the real class has hundreds more properties; the
  wiki's trimmed subset is a subset, not a divergence).
- **Multiblock-Structures** - the JSON shape (`origin`, `legend`, `layers`, `core`) matches the doc
  comment on `JsonMultiblockLayout` verbatim: `mods/exlib/src/Structures/JsonMultiblockLayout.cs:13-15`.
- **Testing-Harness** - `world.RegisterNetwork(string, Func<BlockNetworkModSystem, BlockNetwork>)`,
  `world.Place(BlockPos, Block, BlockEntity?)`, `world.AddNode(BlockPos, string)`,
  `world.NetworkAt(BlockPos)` and `TestNetworkBlock.Create(string, string, int, string?)` all match
  their declarations in `mods/exlib/testing/World/TestWorld.cs` and
  `mods/exlib/testing/Doubles/TestNetworkBlock.cs:36`.

No drift found on any of the five: every call shape reads right against today's source by name. This
does not prove the snippets compile (no `dotnet build` was run, per the brief) - it proves no renamed
member, no changed parameter order, no dropped default was missed by eye.

## Proposed tasks, ranked by payoff over effort

1. **Helpers-and-Renderers usage examples** (effort M) - the only page with *zero* usage snippets
   anywhere; every other flagged page has a usage example, just not the shortest one. Highest payoff:
   this is the one page where a modder cannot see what calling the helper even looks like.
2. **Registries: lead with the 2-line registration** (effort S) - the gap between what's shown (13
   lines, four variants at once) and what's needed (2 lines) is the widest ratio on the table.
3. **Getting-Started: split block registration from command registration** (effort S) - the very
   first page a new modder reads; its first working example is 3x longer than the capability needs.
4. **Production-Machines: reorder minimal-before-hosting** (effort S) - pure reorder, no new prose,
   and it currently shows a non-self-contained snippet before a self-contained one.
5. **Commands: trim the custom sub-command example** (effort S).
6. **Code-First-Definitions: add a minimal example ahead of the real one** (effort S).
7. **Extending-Processes: one job in the example, not two** (effort S).
8. **Registries.md: state that command registration runs once per side** (effort S, prose only) - this
   is cross-cutting fact 2; closing it prevents the exact silent client-side gap described above.
9. **Block-Networks: move the orientation comment out of the fence** (effort S, cosmetic).
10. **Testing-Harness: a one-block smoke test ahead of the merge test** (effort S) - lowest urgency
    only because another builder is mid-edit on this page right now; do it after that lands.

Migrations-and-Healing, Recipe-Costs and Multiblock-Structures are inherent-length and need no task.

**Note:** `mods/exlib/wiki/Testing-Harness.md` was read once at the start of this audit per
instruction and not re-checked; its line numbers above may already have moved.
