# exlib testing as a generic kit - assessment and ranked recommendations

**Status** proposed 2026-09-05. Companion to
[2026-09-05-exlib-framework-assessment.md](2026-09-05-exlib-framework-assessment.md); nothing here is
ruled until the owner answers TR1-TR4 in section 6. It extends hardening items F5 and F8.3 and the
conventions in [../testing.md](../testing.md). Citations are by symbol; counts are from 2026-09-05.

**The question.** `ExpandedLib.Testing` and the three suites are part of what exlib offers other
modders. How good are they, what patterns do they prove, what do our mods and the reference mods still
need tested, and what would make this a strong generic testing kit for Vintage Story mods?

**Method.** Four read-only passes (the harness and its fidelity, exlib's own suite, the iiex and siex
suites, the eight reference mods' testable seams), the mechanical counts below, and one experiment: a
headless boot of the real dedicated server with exlib and iiex loaded (section 7).

## 1. Verdict

There is nothing else like this in the reference population. None of the eight mods under `.compat/`
has a single test; the family runs about 4,700 test cases across 72,000 lines of test code, headless,
against the real game assemblies, on three game versions, behind a coverage gate. The harness's best
ideas are patterns rather than fixtures: a machine is driven live to a condition (`RunUntil`) instead
of sampled at an offset; a megablock is stood up from its own definition and completes through its own
monitor (`StructureRig`) instead of a forced flag; networks are hammered with randomised operation
sequences and asserted never to go negative, NaN or over their burst ceiling (`PipeInvariantTests`);
every code that ever shipped is replayed against the live definitions (`ReleasedCodeCoverageTests`);
a law is scanned over every concrete subclass in the loaded assembly closure so a downstream mod's
leaf is covered without writing a test (`FurnaceBranchGuards`); the source tree itself is guarded
(lang parity, wiki parity, handbook parity, shipped JSON parses).

It is not yet a generic kit, for six reasons:

1. **It cannot load a JSON block.** `TestBlocks.Configure` builds bare `Block` instances by hand and
   `StructureRig.Around` reads an `ExBlockDef`; nothing in the harness touches an asset manager or the
   object loader. A JSON-first modder, which is most of the reference population, gets a bare block
   stand-in and no layout fidelity.
2. **The fake world is thin outside blocks, block entities and networks.** `TestWorld` wires
   `GetBlock`, `SetBlock`, `GetBlockEntity`, tick-listener capture and one shared chunk substitute;
   players, entities, inventories, the event bus beyond ticks, assets, logger, config and packet
   channels are unwired NSubstitute defaults. That is why the family reaches private members through
   `ReflectionHelpers` in 87 files (332 call sites in iiex alone) and builds `Substitute.For<IPlayer>()`
   inside one fixture.
3. **Mocking a player needs a patched game dll.** `IPlayer` ships an `internal abstract` member, so
   `infra/tools/patch-api.cs` publicises the provisioned copy; it once broke the game's logger
   (`patching-game-dll-kills-logger`). A stranger has to discover and replicate this.
4. **The most valuable patterns live in content fixtures, not the harness.** `RunUntil`/`RunLive`,
   the rig and plant vocabulary, the collection discipline for process-global statics and the
   assembly-closure law scan are all in `mods/iiex/tests/Fixtures` and `Invariants`, where a third
   party cannot reach them.
5. **The edges are repo-bound.** `RepoPaths.DomainToMod` throws for an unknown domain;
   `ReleasedCodes`, `ReleasedVersions` and `ReleasedCodeDebt` are this family's history;
   `coverage_gate.py` names our three assemblies; `BlockCodeEmitter` is a test-assembly feature behind
   an environment variable (F8.3); the dev bundle is built on demand, there is no template project and
   no NuGet; [../testing.md](../testing.md) still describes five projects including the retired smex
   and hpex suites.
6. **exlib's own third-party-facing surface is largely untested.** 132 of 225 public types have no
   reference in `mods/exlib/tests`, and whole areas a stranger would rely on have none: the `/exmod`
   command tree, preferences, right-click construction, the legacy shims, the renderers, recipe
   profiles, the config registration attributes, the migration edges (`IBlockRemoval`,
   `IItemCodeMigration`, the sweeper). No Harmony patch in exlib or iiex has a test. Packet handling is
   tested at the dispatch method, never through the wire.

One thing the harness reader missed and the experiment confirmed: every provisioned install carries
`VintagestoryServer.dll` and the full asset tree, so a real headless server boot of a mod is available
in CI with what `provision game` already downloads. That is the cheapest route to a real check for
JSON-only mods, ahead of building an asset loader.

## 2. What exists

| Piece | Size | What it gives |
|---|---|---|
| `ExpandedLib.Testing` | 34 files, 6,400 lines | `TestWorld`, `Scene`, `SceneDiagram`, `StructureRig`, `TestBlocks`, `TestLang`, `ReflectionHelpers`, `VsAssemblyResolver`, 8 doubles, 18 validators |
| exlib suite | 108 files, 20,900 lines, about 2,000 cases | 40 unit, 33 harness-scenario, 16 definition, 15 guard, 1 generator files |
| iiex suite | 190 files, 43,000 lines, about 2,400 cases | machine, scenario, invariant, migration, definition tests; the rig and plant fixtures |
| siex suite | 49 files, 8,800 lines, about 320 cases | the same shape one tier up; `ReleasedCodeCoverageTests` |
| infra | `provision game`, `.game/<slug>` cache, `coverage_gate.py`, `tests.yml`, `tests-legacy.yml` | game assemblies from the public CDN with no install, coverage floors 68/55/60, lanes for 1.20, 1.21, 1.22 |

Health signals are good: no `Skip`, no sleeps, no retries in any suite; time is always the fake clock;
assertion density about 2.5 per test; goldens rebless through one environment variable.

## 3. What blocks a stranger, ranked

**T-A JSON blocks cannot enter the harness** (finding 1). Cost: the harness serves code-first mods
only, which is us.

**T-B No player, inventory, entity, asset, config or packet fakes** (finding 2). Cost: interaction,
inventory and config code is either untestable or tested by reaching into privates.

**T-C The publicizer** (finding 3). Cost: a stranger cannot mock a player on 1.22 without our script.

**T-D Patterns stranded in content fixtures** (finding 4). Cost: the kit's best idioms are invisible
to an adopter; they are also duplicated per fixture here.

**T-E Repo-bound edges and stale docs** (finding 5). Cost: `DefinitionGoldens` and `DefinitionAssets`
cannot be pointed at a foreign mod without editing harness source; the conventions doc misleads.

**T-F Reflection as the default seam.** 332 sites in iiex, 161 in siex, 3 in exlib. The five most
reached members are `Orientation`, `Type`, `OnProductionTick`, `OnServerTick`,
`UpdateStructureRotation`: all invoked, not read, because the bases expose no internal way to drive a
tick or force a rotation. Cost: every rename breaks tests at run time, and an adopter learns the wrong
habit from our examples.

**T-G exlib's untested surface** (finding 6). Cost: the areas a third party touches first are the
ones with no proof.

**T-H Silent-race discipline is informal.** One `[CollectionDefinition]` in exlib against four bare
`[Collection("...")]` names across nine files; two in iiex; none in siex. Cost: the trap the
conventions doc itself names ("joining is what serializes") is caught by convention, not by code.

## 4. Recommendations

Ranked by payoff over effort. S is under a day, M is days, L is a week or more.

### 4.1 Make the kit generic

1. **Lift the stranded patterns into the harness** (T-D). A content-free `MachineRig` base with
   `RunUntil`/`RunLive`; a `RegistryLawScanner<TBase>` for assembly-closure laws; the drained-resource
   invariant as a template parameterised over produce, consume and tick; a `StaticStateCollection`
   helper that both writers and readers join. Effort S to M each. Seeds: `FurnaceLayoutRig`,
   `FurnaceBranchGuards`, `PipeInvariantTests`, `FurnaceConfigCollection`.
2. **Supported doubles for what tests fake by hand** (T-B, T-C). `TestPlayer` with an inventory and
   held slots; an `ItemSlot`/`InventoryBase` fixture; a mod-loader fake exposing `IsModEnabled` and the
   three aliases interestingme probes for; a `World.Config` bag; a `LoadModConfig`/`StoreModConfig`
   file fake; a logger that records. Ship the publicizer as a documented step of `provision game`,
   with the CodeView fix. Effort M.
3. **Unbind the edges** (T-E). `RepoPaths.DomainToMod` falls back to `domain == mod`;
   `ReleasedCodes`, `ReleasedVersions` and `ReleasedCodeDebt` become a per-mod registration the family
   fills; `coverage_gate.py` reads its floors from a file; `BlockCodeEmitter` moves to the generators
   (F8.3). Effort S.
4. **Template and distribution.** A `dotnet new` template (csproj with the `<Private>true</Private>`
   trap pre-solved, `ModuleInit`, one `TestWorld` test, one golden test, one guard); a CI workflow
   template around `provision game`; the dev bundle built by the release workflow; NuGet for
   `ExpandedLib.Testing` once its public surface is marked. Effort M.
5. **Rewrite [../testing.md](../testing.md) and the two wiki testing pages** for the three-project
   layout and the per-mod paths, and adopt the six exemplary files as the documented examples:
   `NetworkGraphTests`, `StructureRigTests`, `ProductionMachineTests`, `ConfigMigrationTests`,
   `ExlibDefinitionGoldenTests`, `ShippedAssetJsonTests`. Effort S.

### 4.2 Extend what can be tested

6. **A headless dedicated-server smoke lane.** Boot the Linux server archive with the mod under test in
   a scratch data path, wait for "now running", run `/exmod verify` (item 10 of the framework
   assessment), stop, and fail on any `[Error]` in `server-main.log`. Works in CI with what
   `provision game` already downloads; gives JSON-only mods a real check with no fakes. Effort M.
   Section 7 records the local attempt.
7. **Real-asset loading in process** (T-A). Drive the game's asset manager, patch loader and object
   loader against a mod's asset tree so JSON blocktypes, itemtypes, recipes and patches resolve inside
   `TestWorld`, and `StructureRig` can stand up a JSON block. Effort L. Item 6 first; this second.
8. **A Harmony fixture.** Load the vanilla assemblies once per test assembly, apply a mod's patches
   under a collection, verify application through `Harmony.GetPatchInfo`, and document the static
   constructor hazards (`BlockEntityAnvil` builds particle objects in its type initialiser). Serves the
   five reference C# mods and our own six untested patches. Effort M.
9. **`exlib-verify` as a dotnet tool** for code-free modders: patch targets exist, patch ops apply,
   recipe codes resolve, lang keys exist, handbook references resolve, `dependsOn` and `condition`
   gates name real mods and flags. Built on the validators plus item 6 or 7. Effort M after 7, L
   before it.
10. **A packet round-trip fixture.** A paired fake channel that serialises through the game's own
    serialiser, so `OnReceivedClientPacket` tests cover the wire and not only the dispatch. Effort S.

### 4.3 Close exlib's own gaps

11. **Test the third-party-facing surface** (T-G): the `/exmod` tree, preferences, right-click
    construction, recipe profiles, config registration attributes, `IBlockRemoval`,
    `IItemCodeMigration`, the sweeper, the legacy shims through the legacy lane, `HandbookUnitPatch`
    and iiex's five patches through item 8. Effort M.
12. **Replace reflection with seams** (T-F). Internal or protected-internal entry points on the bases
    for driving a tick and setting orientation and type; convert the top five reflected members first.
    Effort M; reduces 493 reflection sites across the suites to the few that pin a genuinely private
    fact.
13. **Formalise the collections** (T-H): a `[CollectionDefinition]` class for each bare name, and one
    in siex. Split `MultiblockCellRolesTests` (774 lines) and `NetworkMembershipTests` (630 lines)
    before offering them as examples. Effort S.

## 5. Where this slots in

Items 1, 3, 5 and 13 are small and independent of content; they fit around the walk. Items 2, 4 and 11
belong with the pre-release gate in the framework assessment, since the kit ships with the first
public exlib. Item 6 is the first thing to build for a JSON audience, before item 7. Items 8, 9, 10
and 12 follow the rulings below.

## 6. Rulings requested

- **TR1 Audience of the kit.** Stay a C#-first harness and add the server smoke lane (item 6), or
  also build in-process asset loading (item 7, L). Default: 6 now, 7 after the first external adopter
  asks for it.
- **TR2 The publicizer.** Ship `patch-api.cs` as part of `provision game` for third parties, with the
  logger fix and a note that it modifies only the provisioned copy. Default: yes.
- **TR3 NuGet for `ExpandedLib.Testing`.** Its surface is smaller than exlib's and could be marked
  first. Default: after the framework boundary (R1), in the same release.
- **TR4 Where lifted patterns live.** In the harness, content-free, per the homing rule in
  [../testing.md](../testing.md). Default: yes.

## 7. Evidence

### Counts

| Measure | Count |
|---|---|
| test cases at the last green run: exlib, iiex, siex | 2,019; 2,401; 324 |
| `[Fact]` and `[Theory]` methods: exlib, iiex, siex | 942; 1,482; 248 |
| lines of test code: exlib, iiex, siex | 20,892; 42,959; 8,796 |
| harness: files, lines, validators, doubles | 34; 6,372; 18; 8 |
| files referencing `TestWorld`, `TestBlocks`, `ReflectionHelpers`, `RepoPaths`, `StructureRig`, `Scene` | 132; 130; 87; 36; 32; 20 |
| `ReflectionHelpers` call sites: iiex, siex, exlib | 332; 161; 3 |
| exlib public types with no reference in its tests | 132 of 225 |
| coverage floors: exlib, iiex, siex, total | 68, 55, 60, 60 percent |
| `Skip`, `Thread.Sleep`, retries across the suites | 0, 0, 0 |
| tests in the eight reference mods | 0 |

### Untested areas of exlib with third-party relevance

Commands (all eight types), Preferences (six), Construction (`ExRightClickConstruction`,
`ExConstructionStage`, `ExRccSettings`, `ConstructedAnimator`), Legacy (`LegacyApi120`, `LegacyLinq`,
`LegacyAnimUtil`), Renderers (`SurfaceRenderer`, `ToggleAnimator`), Recipes (`ExRecipeProfiles`,
`RecipeProfile`), Config (`ExConfigRegisterAttribute`, `ExConfigRangeAttribute`, `ExConfigProfiles`,
`ExConfigFiles`), Migrations (`IBlockRemoval`, `IItemCodeMigration`, `CodeRelocation`,
`ChunkColumnSweeperModSystem`), `HandbookUnitPatch`.

### What the reference mods would need

| Mod | Testable seam | Fake needed |
|---|---|---|
| ElectricalProgressive | `Network`, `NetworkPart` graph maths | none; POCOs |
| HydrateOrDiedrate | aquifer maths in `AquiferManager`; `ConfigManager` load and `World.Config` mirror | file IO fake; `World.Config` bag |
| SmithingPlus | `IsBrokenToolHead(ItemStack)`; patched method bodies called directly | `ItemStack`; Harmony host |
| Toolsmith | `TinkeringUtility` durability functions | `ItemStack`; the static `Api` seeded |
| interestingme | drop tables with `requiresMod` gating; ProtoBuf config round-trip | mod-loader fake with four method names |
| `_im`, `em`, industrialstory | patches apply, recipes resolve, lang and handbook keys, `dependsOn` and `condition` gates | item 9 |

### The headless server experiment

Booting `.game/1.22/VintagestoryServer.dll` with `--addModPath` pointing at the built exlib and iiex
folders: the server started, reached save-game creation, then failed on `e_sqlite3`; with a system
SQLite shim it went on to discover both mods and failed again on `libSkiaSharp` while reading the mod
icons. Both are Linux native libraries absent from the local install, which is the Windows-flavoured
client package; the Linux server tarball that `provision game -Kind server` fetches in CI carries them.
The Linux server tarball (`vs_server_linux-x64_1.22.6.tar.gz`, fetched directly from the CDN into
the scratch directory) booted with both mods from `--addModPath`: 82 block, 73 item and 39 recipe
definitions injected, the healer watching 1,228 block types, "Dedicated Server now running" reached,
`/stop` on stdin honoured, exit code 0, no `[Error]` line in `server-main.log`, 91 seconds end to end
including world generation. One observation the lane surfaced on its own: on a fresh data path the
config store logged "reset MetalRecoveryFallback to defaults on upgrade to 0.7.3", so a first run
reads as an upgrade; a smoke lane asserting on notifications as well as errors would have caught that
wording. Item 6 is therefore verified feasible with the archive CI already downloads, and the boot
budget is about a minute and a half per lane.
