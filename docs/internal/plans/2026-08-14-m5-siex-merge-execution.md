# M.5 — the `siex` merge, execution plan

**Status** ⛔ **EXECUTED 2026-08-14.** Kept as the record of what was done and what the plan got wrong.
The merge is built: `siex` 0.9.9 is one assembly, one domain, one suite; `src/HighPressureExpanded`,
`assets/hpex`, `docs/hpex` and `test/HighPressureExpanded.Tests` are gone. Gate **9 targets / 3,990**
green across 1.20/1.21/1.22.

**Three things this plan got wrong, all caught by the guards rather than by reading:**

1. ⛔⛔ **It never listed the migration the merge actually needed.** M.5e's table covered retargeting the
   four existing migrations and said nothing about the 38 released `smex:` codes whose blocks *stayed*.
   They had been passing only because the stale `("smex", …)` row in `ReleasedCodeCoverageTests.Domains`
   injected smex as a live domain - trap 3, in the flesh. Deleting that row exposed them and
   `SmexToSiexMigration` was written to answer it.
2. ⛔ **"M.5 owes exactly one alias" was wrong: it owes six.** The claim rested on the converter, cowper,
   air-blower and heat-sink families being wholly recorded debt; they are not, and
   `ReleasedEntityClassTests` named all five extras.
3. ⛔ **The config section was a silent data-loss case nobody had listed.** A section is keyed by mod id,
   so renaming `smex` to `siex` orphans every value a player had tuned, with no error. `FoldLegacy`
   covers a legacy FILE, not a legacy SECTION. Fixed by adding `LegacySectionIds` to exlib
   (`ExConfigDocument.FoldLegacySections`), tested and mutation-checked.

★ **And one instruction that became obsolete:** M.5d said to drop the pre-merge config migration rows
because siex starts at 0.9.9. With the section carry-over above, a carried section brings its own
`ConfigVersion`, so those rows still fire for a player coming from smex 0.9.3. They were kept.

Parent plan: [2026-08-13-framework-hardening.md](2026-08-13-framework-hardening.md) stage **M**.
Rulings M1–M6: [STATE.md](STATE.md). Entry point: [NEXT.md](NEXT.md).
**Read [the M.4 record](2026-08-14-m4-iiex-merge-execution.md) first** — M.5 is the same shape, already
walked once, and its four load-bearing traps are restated below.

**Goal.** `smex` + `hpex` become one assembly and one domain, **`siex`** (Steel Industry Expanded).
`iiex` (M.4) and `exlib` are unchanged. This is the second and last merge; after it the mod set is
final: `exlib`, `iiex`, `siex`.

⛔⛔ **The constraint every task is measured against (M3): a merge collapses NO tier.** Same rule that
governed M.4. Nothing in the steel loop is deduplicated away because two mods happened to ship it.

---

## What makes M.5 different from M.4

⛔⛔ **`smex` HAS SHIPPED** — 0.9.8, 35 blocktypes, **351 concrete codes**, with real player worlds
behind it. In M.4 only `ppex` had shipped and both merging halves (`iwex`, `lpex`) were development-only,
so a wrong left-hand side cost nothing. Here every `smex:` left-hand side is a live contract. `hpex` has
never shipped, so its side is free.

★ **The surface is much smaller.** 41 + 13 = **54** `.cs` files (M.4 merged 204). Suites are 243 + 58
tests (M.4 merged 1854). And the collision survey is clean:

| Check | Result |
|---|---|
| Type-name collisions | **none** |
| File-path collisions | `AssemblyInfo.cs`, `InternalsVisibleTo.cs` only |
| Config property collisions | `ConfigVersion`, `RecipeLevel`, **`RccBrokenDropsRatio`** |
| Recipe catalogue key collisions | **none** (M.4 had four, and they were silent) |

⛔ **`RccBrokenDropsRatio` is the one real content decision.** Both configs carry it and both mods
register it with `ExRccSettings.RegisterBrokenDropsRatio(Mod.Info.ModID, …)`, keyed by the broken
block's `Code.Domain`. One domain after the merge means **one value** — check whether smex's and hpex's
defaults differ before collapsing them, and if they do, that is an owner call.

**Survivor: `src/SteelmakingExpanded` → `src/SteelIndustryExpanded`, absorbing hpex.** smex is 3× the
file count and hpex already declares a runtime dependency on it (`modinfo.json`), so absorbing upward
deletes an edge. ⛔ Neither may set `<ShipGameLangOverride>` — `iiex` owns it and exactly one mod may.

★ **No compile reference runs between them.** hpex's csproj references only `exlib` and `iiex`; its
dependency on smex is a **modinfo runtime** declaration (and its *test* project references smex, for the
coverage guard). So M.4's `<Private>false</Private>` transitive-copy hazard does not arise between these
two — but it still does for the merged project's references to `exlib` and `iiex`. Verify the same way:
**stage the output and count ModSystem-bearing dlls**, never by reading a csproj.

---

## The four traps, restated for `siex`

Each finished green in M.4 and was still wrong. Each has a counterpart here.

1. **A per-mod discriminator becomes a bug the moment the mods merge.** `PipeMigration` gated three
   branches on `Code.Domain` and both pipe tiers carried the same `type` variants, so a mechanical
   rewrite claimed every released segment code **twice**. Before renaming anything, grep smex and hpex
   for `Code.Domain`, `Domain ==` and `Domain !=` and decide, per site, whether the domain is doing
   *scoping* (keep it) or *discrimination* (replace it with the thing that actually varies).
   ⛔ It shows up in tests too: `RolledJointTests` was parameterised by domain as a tier proxy, so half
   its rows became literal duplicates asserting nothing.
2. **`ExRecipeProfiles` and `ExConfigProfiles` key on the mod id and REPLACE silently.** Two config
   sections under one assembly leave whichever registered first unreachable, with nothing to show for
   it. `SmexConfig` + `HpexConfig` must merge into one `SiexConfig` (40 + 29 properties, three
   collisions above), and `SmexRecipeConfig` + `HpexRecipeConfig` into one `SiexRecipeConfig`.
   ★ The catalogue keys do **not** collide here, unlike M.4 — but confirm that after any renaming, since
   a collection initialiser assigns through the indexer and one set would overwrite the other with no
   duplicate-key error and no failing test.
3. **A stale row in `ReleasedCodeCoverageTests.Domains` makes the contract pass VACUOUSLY.**
   `DefinitionCodes.ForDomain` **injects** the domain rather than filtering by it, so a leftover row
   synthesises phantom live blocks and the whole contract passes over a world that will never exist.
   Verified during M.4 in the dangerous direction: leaving a stale row in keeps all six tests green.
   **Delete the `smex` and `hpex` rows and add one `siex` row together, in one change.**
4. **Block-entity class strings live in the SAVE** — see below; this one is already half-done.

---

## The block-entity class contract — read this before touching the ModSystems

⛔⛔ A class string is stored per block entity in the **save**, appears in no definition, and nothing
migrates it. `BlockMigrationModSystem` reads the old state off a **live** block entity
(`ba.GetBlockEntity(pos)`), so an unregistered class means the game never constructs it, `oldState`
arrives null, and the block migrates **with its contents gone**. No exception, no log line, and every
code-level guard green.

★ **Most of the work is already done** *(2026-08-14, found while scouting this stage)*. smex 0.9.8
shipped 19 `entityClass` values and none were registered anywhere. The 11 that relocated to `iiex` are
now aliased in `IronIndustryExpandedModSystem.LegacyEntityClasses`; the eight remaining are
converter / cowper / air-blower / heat-sink / tiered-tap-and-tuyere, all recorded in
`ReleasedCodeDebt`, so their blocks reach no live block either and an alias cannot save state for a
block that never arrives.

★★ **What M.5 still owes: exactly one alias.** Renaming the domain changes every *retained* class key
from `smex.X` to `siex.X`. Of the retained classes only **`smex.BlockEntitySmokeStack`** backs blocks
that migrate, so it needs a `("smex.BlockEntitySmokeStack", typeof(BlockEntitySmokeStack))` row in the
merged mod's `LegacyEntityClasses`.

★ **And you will not have to remember.** `ReleasedEntityClassTests` resolves a shipped class either
through an alias or through `{domain}.{TypeName}` still being live. The moment the domain row becomes
`siex`, `smex.BlockEntitySmokeStack` matches neither and the guard fails, naming it and the blocktype it
came from. Let it drive this step rather than pre-empting it.

---

## Order

M.5a → M.5b → M.5c may not be reordered. M.5d–M.5h are within the same commit as M.5c.

### M.5a — identity, all knobs in ONE commit

- Folder `src/SteelmakingExpanded` → `src/SteelIndustryExpanded`; csproj renamed with it.
- `<AssetDomain>siex</AssetDomain>`, explicit `<AssemblyName>siex</AssemblyName>` and
  `<RootNamespace>SteelIndustryExpanded</RootNamespace>`. ⛔ Neither mod sets the last two today, so the
  **folder name** silently controls both the dll name and the namespace `ExLangKeyGenerator` emits
  `{Domain}Lang` into.
- `[assembly: ExDomain("siex")]` in `AssemblyInfo.cs`; `InternalsVisibleTo("SteelIndustryExpanded.Tests")`.
- `modinfo.json`: modid `siex`, name **Steel Industry Expanded**, version **≥ smex's 0.9.9** (smex has
  shipped 0.9.8, and `ModinfoTests` asserts source > released). Dependencies: `game`, `exlib`, `iiex` —
  the `smex` row goes.
- ⛔ `$(AssetDomainAbsorbed1)` was never needed in M.4 (every code relocated in one pass) and is
  probably not needed here either; it exists if this merge has to be staged instead.

### M.5b — move hpex in, rename namespaces

`SteelmakingExpanded` and `HighPressureExpanded` → `SteelIndustryExpanded`. Only `AssemblyInfo.cs` and
`InternalsVisibleTo.cs` collide, and no type name does, so the moves are mechanical.
⛔ Do the identifier rename and the **domain-literal** rename as two separate passes, and exclude every
`BlockMigrations/` file from the second: a left-hand side is historical and frozen, and a wrong one
matches nothing, silently. M.4's passes are in the scratch history; rebuild them rather than reusing a
half-remembered regex.

### M.5c — one ModSystem, not two

⛔ If both survive, each calls `EntityRegistry.RegisterAll` over the **whole merged assembly** and every
class registers twice. Definition registration is idempotent and both compute the same `Mod.Info.ModID`,
so the goldens look right and the Harmony guard skips — it works by coincidence.
Carry both `LegacyEntityClasses` tables forward into one, plus the smokestack row above.

### M.5d — one config

`SiexConfig` (section `siex`, `Manageable = true`) and `SiexRecipeConfig`. Legacy file names: the union
of both. ⛔ Migrations arrays: siex starts at ≥ 0.9.9, so any row below that can never fire — drop them
rather than shipping dead rows (M.4 emptied its array for exactly this reason and said so in the doc
comment).

### M.5e — migrations, retarget in place, one file at a time

⛔⛔ **Never a global find-replace.** The LEFT of every pair is historical and frozen; the RIGHT tracks
the live code. A wrong LEFT matches nothing and does nothing. A wrong RIGHT is dropped by
`BlockMigrationModSystem.BuildRemapTable` with only a `Logger.Warning` — server log, never CI.

| File | Edit |
|---|---|
| `SteelmakingExpanded/BlockMigrations/BessemerToConverterMigration.cs` | RIGHT sides → `siex` |
| `SteelmakingExpanded/BlockMigrations/BrickVariantMigration.cs` | RIGHT sides → `siex` |
| `SteelmakingExpanded/BlockMigrations/CeramicMoldRemoval.cs` | an `IBlockRemoval`; its literals are historical `smex:` codes — **check before touching** |
| `HighPressureExpanded/BlockMigrations/HpexExtractionMigration.cs` | `LegacyDomains ["lpex","ppex"]` stay frozen; `newDomain` hpex → **`siex`** |
| `IronIndustryExpanded/BlockMigrations/SmexToIiexMigration.cs` | ⛔ **NO EDIT** — `smex` is its frozen LEFT and `iiex` its live RIGHT; neither moves |

⛔ **Mutation-check each retarget**: revert one `newDomain` and confirm red. If it stays green a stale
`Domains` row is synthesising phantom blocks — go back to trap 3.

### M.5f — the coverage guard's domain anchors

`test/HighPressureExpanded.Tests/Migrations/ReleasedCodeCoverageTests.cs` — collapse `("hpex", …)` and
`("smex", …)` into one `("siex", <merged assembly>)`. Same edit in `ReleasedEntityClassTests.Mods`.
See trap 3 for why both rows must go in one change.

### M.5g — suites and build lists

Merge `test/HighPressureExpanded.Tests` into `test/SteelmakingExpanded.Tests` →
`test/SteelIndustryExpanded.Tests`. ⛔ **Prove no coverage was lost by diffing distinct test-method
names across the merge**, not by comparing test counts: duplicate classes that collapse look identical
to deleted checks in a raw count. (M.4: 1139 → 1139, and the two removed were deliberate.)

**Suite membership is hardcoded in three places and only one fails loudly:**
`VintageStory.sln` · `scripts/exmod.ps1` (`$projects`) · `dist/CakeBuild/Program.cs` (`ProjectFolders`).
A stale name fails the build; a **dropped** name means that suite silently never runs. The gate goes
from 12 targets to **9** (3 suites × 3 versions).

⛔ The sln will end up with duplicate project rows under the old GUIDs, as it did in M.4 — strip the
retired GUID's `Project(...)`/`EndProject` block **and** its configuration and nesting lines.

### M.5h — assets and docs

- ⛔⛔ **Merge the lang files by programmatic JSON key-union, never a directory move.** All three locale
  filenames collide; a directory move leaves the locales mutually consistent while losing a whole mod's
  strings, and `LangParityTests` passes over it. Assert the merged key count.
- ⛔ **Two keys collapse and the last one silently wins.** In M.4 `game:tabname-*` merged and the tab
  ended up named after the wrong half. Check `game:tabname-siex` explicitly and write all three locales.
- ⛔ **Sweep for orphaned command keys.** M.4 found four `command-steam-*` strings naming a command that
  no longer exists. Nothing guards these.
- ⛔ **The handbook pages collide on number.** `HandbookSync.ByNumber` assigns by indexer, so pages drop
  out of the parity guard with the suite green and no in-game symptom. Renumber during the move; the
  number is also the in-game ordering, so the order is an owner call.
- `assets/{smex,hpex}` → `assets/siex`; `docs/{smex,hpex}` → `docs/siex`, handbook html renumbered to
  match. `docs/siex/moddb.html` is an authoring merge of the two published pages.

---

## Verification

Full gate every stage: `./scripts/exmod.ps1 test all` — **9 targets** after the merge, green across
1.20/1.21/1.22.

⛔ **Do not use `dotnet test --no-build` after editing sources.** It silently tests a stale binary; a
count that does not move is the tell. ⛔ And do not run parallel `dotnet build` invocations across TFMs
— they race on `obj/` and leave a broken build that `--no-build` then hides. `dotnet build-server
shutdown` clears a stuck `VBCSCompiler` lock.

⛔ **A green run is not proof.** Mutation-check every guard you rely on, in both directions, and sweep
for `MUTANT` before diagnosing any red.

★ **Staged-output check, which no test can do:** publish each mod and assert exactly one
ModSystem-bearing dll per folder. A second one makes Vintage Story refuse to load the mod entirely.

---

## Carried forward, not part of M.5

- **M.6** wants `ReleasedCodeCoverageTests` (and now `ReleasedEntityClassTests`) in a
  `test/Integration.Tests`. ⛔ That directory was **deleted 2026-08-14** as an orphan — zero tracked
  files, in neither the solution nor the suite list — so M.6 now *creates* it. After M.5 the siex suite
  is the only one seeing all three mods, so the guards can also simply stay there.
- **The plan triage.** The iwex-era plans (`2026-08-04-iwex-u2-u10-expansion`, `-completion`,
  `iwex-bringup`, the two 2026-08-05 findings files) carry reading-note banners but their task content
  is unreviewed against what shipped. ⛔ It is a **triage, not a rename**: `iwex` in those files means
  both the assembly (now `iiex`) and a *scope* — "finish iwex", "an iwex-only player" — and the second
  sense no longer refers to anything, since ruling **M2** retired per-mod closure for per-loop. Doing it
  after M.5 means doing it once.
- **B25's recorded divergences** (`ReleasedCodeDebt.KnownUnmigrated`, 68 smex + 4 ppex). The converter
  and cowper families are being **completely remade** in the steel mod — do not migrate them.
  `ppex:mpfluidpump` is port-pending from the `0.9-support` branch.
