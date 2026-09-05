# M.4 — the `iiex` merge, execution plan

**Status** EXECUTED 2026-08-14. Every stage below landed; the gate is 12 targets / 4,000 tests, green on
1.20/1.21/1.22. Kept as the record of how the merge was done and what it cost, because **M.5 (`siex`) is
the same shape** - read it before that one. What actually differed from this plan in execution, plus the
four traps that only surfaced during it, is in
[the worklog](../worklog/2026-08.md#2026-08-14---m4-iwex--lpex-are-one-mod-iiex) and summarised at the top
of [NEXT.md](NEXT.md).

Parent plan: [2026-08-13-framework-hardening.md](2026-08-13-framework-hardening.md) stage **M**.
Rulings M1–M6: [STATE.md](STATE.md). Entry point: [NEXT.md](NEXT.md).

**Goal.** `iwex` + `lpex` become one assembly and one domain, **`iiex`** (Iron Industry Expanded).
`smex` + `hpex` become `siex` later (M.5); `exlib` is unchanged.

⛔⛔ **The constraint every task is measured against (M3): a merge collapses NO tier.** The plated pipe
family and the iron gears are the early loop's deliberate bootstrap rung — a player plumbs and gears the
works before steam exists and upgrades to cast afterwards. Two near-identical pipe families are
progression, not duplication. Do not delete either.

---

## What is already done, and why it matters here

| Stage | What it removed from M.4's path |
|---|---|
| **M.1a** | `[assembly: ExDomain("<modid>")]` — a class key resolves from the type's own assembly, with no load-order dependency |
| **M.1b** | `[ExDefDomain("…")]` on a provider overrides the domain its `Definitions(string)` factory is handed |
| **M.1c** | `$(AssetDomainAbsorbed1)` / `$(AssetDomainAbsorbed2)` — one project can pack several asset trees |
| **M.2** | The pipe `tier` is a variant, declared **first** (`pipe-{tier}-{type}-{orient}`) |
| **M.0** | The tier is in the **asset path** too. Without this the merge silently deleted a pipe tier |
| **M.3b** | The released-code manifest is current (0.6.8/0.9.8) and the remaining divergence is fenced |
| **M.3c** | exlib names no content domain anywhere — lang keys, sheet texture, config fallback |

★★ **Read this before trusting any instinct about migrations.** `CodeRelocation.Remap` walks the **live**
registry and pairs each live block with an *explicitly named historical* code, rebuilding the table on
every load. So there is **no chain**: `ppex → lpex → iiex` is **one hop**, and the edit is to **retarget
`newDomain` in place**. Never add a second hop.

★★ **Only `exlib`, `ppex` and `smex` have ever been released** (confirmed against every zip in
`dist/Releases/`). `iwex:` and `lpex:` codes have never existed in a player world, so **they need no
migration at all**. That bounds the entire migration surface to the `ppex:` and `smex:` left-hand sides.

---

## Order

M.4a → M.4b → M.4c may not be reordered. M.4d–M.4g are within the same commit as M.4c.

### M.4a — identity, all knobs in ONE commit

Survivor: `src/IronIndustryExpanded` → `src/IronIndustryExpanded`, absorbing lpex. Reasons, all verified:
iwex is 3× the file count (159 vs 53 `.cs`); the dependency already runs lpex → iwex, so absorbing upward
**deletes** an edge rather than inverting one; iwex carries `<ShipGameLangOverride>true</>` which exactly
one project may set (`IronIndustryExpanded.csproj:25`, and the comment there says lpex/smex must not);
iwex's folder taxonomy is a superset of lpex's.

- `<AssetDomain>iiex</AssetDomain>` **and** `<AssetDomainAbsorbed1>lpex</AssetDomainAbsorbed1>`.
  ⛔ Both in the same commit: the absorbed glob is `Condition`-guarded on the property being non-empty
  (`mods/Directory.Build.targets:68-74`), so an unset one is **inert with no error** — and a
  `[ExDefDomain("lpex")]` def then loads with **no shape and no error**.
- Explicit `<AssemblyName>iiex</AssemblyName>` and `<RootNamespace>IronIndustryExpanded</RootNamespace>`.
  ⛔ Neither mod sets these today, so the **folder name** silently controls both the dll name and the
  namespace `ExLangKeyGenerator` emits `{Domain}Lang` into.
- `[assembly: ExDomain("iiex")]` in `AssemblyInfo.cs`.
- `modinfo.json`: modid `iiex`, version **≥ lpex's 0.6.9**. ⛔ `modinfo.json` is the version single-source
  for `AssemblyVersion`/`FileVersion` (`Directory.Build.props:169-175`), and this repo has shipped a
  release stamped **below** its own migrations before, with every migration dead.

### M.4b — one ModSystem, not two

⛔ If both survive, each calls `EntityRegistry.RegisterAll` over the **whole merged assembly** and every
class registers twice. Definition registration is idempotent so the goldens look right, and both compute
the same `Mod.Info.ModID` so the second `Harmony.HasAnyPatches` check skips — it works by coincidence.

- One class, one `EntityRegistry.RegisterAll`, one `Harmony.PatchAll`. The bodies are largely disjoint
  (iwex: network types, mold safety, pattern/roll validation — lpex: RCC salvage ratio, chimney patch,
  cast-tier pipe ratings, preference registry), so this is concatenation, not a rewrite.
- ⛔ **Carry forward the two existing `api.RegisterBlockEntityClass` aliases** at
  `IronworkingExpandedModSystem.cs:83-90`, and add `iwex.*` / `lpex.*` aliases beside them. Every
  registered class key flips to `iiex.*`, and a class string lives in the **save**, not in any definition
  — `CodeRelocation` does not touch it and no golden sees it. Bound the alias list by what shipped.

### M.4c — migrations: retarget in place, one file at a time

⛔⛔ **Never a global find-replace.** The LEFT of every pair is *historical and frozen*; the RIGHT tracks
the live code. A wrong LEFT matches nothing and does nothing, silently. A wrong RIGHT is dropped by
`BlockMigrationModSystem.BuildRemapTable` with only a `Logger.Warning` — server log, never CI.

| File | Edit |
|---|---|
| `IronIndustryExpanded/BlockMigrations/LpexRenameMigration.cs` | `newDomain` → `iiex` |
| `IronIndustryExpanded/BlockMigrations/SmexToIwexMigration.cs` | `newDomain` → `iiex`; the `RemapToDefault` targets (`iwex:furnace-chargedoor-n`, the four `BlowerFacings`) → `iiex:` |
| `IronIndustryExpanded/BlockMigrations/SmexToIwexItemMigration.cs` | live-side domain filter → `iiex` |
| `IronIndustryExpanded/BlockMigrations/PipeMigration.cs` | see below |
| `HighPressureExpanded/BlockMigrations/HpexExtractionMigration.cs` | ⛔ **NO EDIT** — `LegacyDomains ["lpex","ppex"]` are frozen LEFT sides and its `newDomain` hpex survives |
| the seven other `IronIndustryExpanded/BlockMigrations/*` | RIGHT sides only |
| `SteelmakingExpanded/BlockMigrations/*` | untouched by M.4 (smex is M.5) |

⛔⛔ **`PipeMigration`'s three branches discriminate on DOMAIN, which is exactly what the merge collapses.**
`PipeMigration.cs:67` `dom == "iwex" && SegmentTypes`, `:83` `dom == "lpex" && ValveTypes`, `:101`
`dom == "lpex" && BrickTypes`. Both tiers carry the same `SegmentTypes`, so a mechanical `dom == "iiex"`
rewrite fires the segment branch for the cast tier too and emits **every released ppex segment code twice
with different targets** — a free tier upgrade for a player's iron pipes, i.e. M3 inverted.
**Replace the domain gates with the block's own tier variant** (`BlockPipe.PlatedTier` /
`BlockPipe.CastTier`). Keep the brick branch rebuilding its old smex code from
`Variant["type"]/["brick"]/["orientation"]` and never from the path. Then retarget the six literal targets
at `:144, :151, :156` (`lpex`) and `:163, :184, :190` (`iwex`).

**Proves it:** `Every_released_block_code_still_reaches_a_live_block`,
`No_released_code_is_claimed_by_two_different_migrations` (keys on old code regardless of migration name,
so it *does* catch the double-emit), `No_migration_claims_a_code_that_is_still_alive`.
⛔ **Mutation-check each retarget**: revert one `newDomain` to `lpex` and confirm red. If it stays green a
stale `Domains` row is synthesising phantom blocks — go to M.4d.

### M.4d — the coverage guard's domain anchors

`test/HighPressureExpanded.Tests/Migrations/ReleasedCodeCoverageTests.cs:27-34` lists one row per mod
assembly. Collapse `("iwex", …)` and `("lpex", …)` into **one** `("iiex", <merged assembly>)`.

⛔⛔ **Delete both old rows in the same commit.** `DefinitionCodes.ForDomain` **injects** the domain rather
than filtering by it, so a leftover `("lpex", mergedAsm)` row synthesises phantom live `lpex:` blocks;
`LpexRenameMigration` then mints its `ppex:` rows against them and the whole contract passes **over a world
that will never exist**. Leaving one "temporarily" is the single edit that makes M.4 pass vacuously.

### M.4e — dependents

Both smex and hpex reference lpex **and** iwex explicitly, and both become the same project.

- Keep exactly **one** `<ProjectReference … ><Private>false</Private></ProjectReference>` in each;
  **delete** the second. ⛔ Do not drop the explicit one in favour of the transitive: `Private=false` does
  not propagate, and the SDK's synthesised transitive reference copies a second ModSystem-bearing dll into
  the output. Vintage Story then **refuses to load the mod at all** — *"Found multiple .dll files with
  ModSystems and/or ModInfo attributes"* — leaving it absent from the mod list. `HighPressureExpanded.csproj:93-108`
  documents this exactly, including that `DisableTransitiveProjectReferences` is **not** the fix.
- Update both `modinfo.json` dependency lists.
- ⛔ **Verify by staging the mod output and asserting exactly one ModSystem-bearing dll per folder.**
  Not by reading a csproj — this failure is green in every test run and only visible in game.

**Suite membership is hardcoded in three places and only one fails loudly:**
`VintageStory.sln:18,20,22,30,34` · `scripts/exmod.ps1:357-363` · `infra/CakeBuild/Program.cs:47-48`.
A stale name fails the build; a **dropped** name means that suite silently never runs.
Delete the `test/PipesAndPowerExpanded.Tests` orphan (csproj only, zero sources, in neither list) — it is
standing proof this already happened once, during the ppex → lpex rename.

### M.4f — assets

- ⛔⛔ **Merge the lang files by programmatic JSON key-union, never a directory move.** All three locale
  filenames collide. If `en.json` alone is overwritten `LangParityTests` catches it loudly; if all three go
  together — which is what a directory move does — the locales stay mutually consistent and the guard
  passes over a tree that lost a whole mod's strings. Assert the merged `en.json` key count.
- ⛔ **Rename on the whole token `iwex`/`lpex`, not on `iwex:`.** ~35 keys embed the domain under a `game:`
  prefix or behind a hyphen (`game:tabname-iwex`, `game:ingameerror-iwex-*`, `iwex-hearth-*`), and the
  creative-tab key is derived at runtime from the domain. Then hand-review every `game:`-prefixed key so
  nothing vanilla is caught, and leave `mods/iiex/assets/game/lang/` alone.
- ⛔ **The handbook pages collide on number.** Both trees number `00-`–`04-`, and `HandbookSync.ByNumber`
  assigns by indexer, so five of ten pages drop out of the parity guard with the suite green, the pages
  still shipping and no in-game symptom. Renumber `00`–`09` during the move — the number is also the
  in-game ordering, so it has to be decided anyway. Owner's call on the order.

### M.4g — regenerate, never hand-edit

`Generated/IiexBlocks.g.cs` via `EXLIB_WRITE_BLOCKCODES=1`; goldens via `EXLIB_WRITE_GOLDENS`.
⛔ Use the **scoped** form (`EXLIB_WRITE_GOLDENS=<path fragment>`) — bare `=1` rewrites every golden in the
domain and accepts unread any drift. Update the four hardcoded strings in the block-code drift test
(domain, class name, namespace, repo path).

### M.4h — drop the staging scaffold — NOT NEEDED

The scaffold (`$(AssetDomainAbsorbed1)` + `[ExDefDomain("lpex")]`) exists to keep an absorbed tree
shipping under its own domain while codes relocate piecemeal. Every `lpex:` code relocated in one pass, so
neither was ever set and there was nothing to drop. `assets/lpex/` and `docs/lpex/` are deleted, no golden
claims an `lpex/` path, and the mechanism stays available for M.5 if that merge has to be staged.

---

## Verification

Full gate every stage: `./scripts/exmod.ps1 test all` — 15 targets, **4,026 tests** at the time of
writing, green across 1.20/1.21/1.22.

⛔ **Do not use `dotnet test --no-build` after editing sources.** It silently tests a stale binary; a count
that does not move is the tell. The gate builds fresh, which is why it catches what per-suite runs miss.

⛔ **A green run is not proof.** Mutation-check every guard you rely on, in both directions, and sweep for
`MUTANT` before diagnosing any red — a killed process leaves no `finally`.

---

## Carried forward, not part of M.4

- **B25's 72 recorded divergences** (`ReleasedCodeDebt.KnownUnmigrated`). 40 are converter/cowper, which
  are being **completely remade in the steel mod** — do not touch. 28 (`blastfurnacetap`,
  `blastfurnace-tuyere`, `engineairblower`) are unruled. 4 are `ppex:mpfluidpump`, which is **port-pending
  from the `0.9-support` branch** (`src/PipesAndPowerExpanded/BlockStructures/MpPump/`) and becomes an
  `iiex:` target once ported.
- **`BlockPipePassthrough.Sheet`** no longer names a content domain, but
  `assets/hpex/shapes/pipe/rolled/*.json` still name `iwex:block/metal/castiron` — **outside both merging
  trees**, which is exactly where a per-file sweep over "the two mods being merged" stops looking. The
  texture stays in iwex/iiex; retarget the hpex four in M.4f.
- `assets/iwex/textures/block/metal/castiron-alt.png` is referenced by nothing (owner's call to delete).
