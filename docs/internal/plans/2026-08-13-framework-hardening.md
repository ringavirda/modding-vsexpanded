# Framework hardening — make exlib a library a stranger can adopt

**Status** live, started 2026-08-13. **F0, F1, all of F2, all of F3, F6.2, M.0, M.1, M.2, M.3, M.4 and
M.5 are done**, each verified on the full gate (`scripts/exmod.ps1 test all` — now **9 targets, 3,990
tests** after both merges collapsed a suite each, green across 1.20/1.21/1.22).
Open: F1.4, F4, F5, F6.1, F7, F8, and **M.8** of stage M.

★★ **The merge is ruled** (M1–M6 in [STATE.md](STATE.md), 2026-08-13): five mods become **`iiex`** and
**`siex`**, full domain consolidation, pipe tier onto a variant, `heavyplate` absorbs `castplate`, and
the code-first builders become supported API. Stage **M** below carries it, and its order is forced.

**Goal:** close the gap between what exlib does and what a consumer of the shipped zip can find, use and
rely on — without changing the capability surface. Every stage here is packaging, diagnostics,
documentation or a contract statement. No new features.

**Why now:** extensibility is already a stated product target (E1–E4, [STATE.md](STATE.md)) and the
contract layer landed 2026-08-12. The layer exists; the wrapper does not. A third party today gets a
138 KB dll with no XML docs, no version identity, a wiki whose flagship example does not compile, and a
documented source generator that was deleted in `f4ddcea`. That is cheap to fix now and gets more
expensive with every consumer.

**Evidence:** the 2026-08-13 architecture pass. Two reports, both grounded in the tree and in the
vendored source at `.compat/vintagestory`:
*Five Mods, One Program* (repo state) and *Adopting exlib* (framework review).

---

## Ordering rule

F0 first and alone — nothing else can be validated by running anything until the tree builds. After
that the stages are independent and can land in any order, but they are listed by payoff per hour.

---

## F0 — the tree does not build *(blocking)* — **done 2026-08-13**

The two modified csprojs in `git status` are an accidental revert to a pre-split revision, not work in
progress.

- [x] **F0.1** Restore `mods/siex/src/SteelmakingExpanded.csproj` and
      `mods/siex/tests/SteelmakingExpanded.Tests.csproj` from HEAD. The lost
      `<ProjectReference>`s to iwex and lpex are the 36 CS0246s; the lost `<AssetDomain>smex</AssetDomain>`
      is worse — a release cut from this tree ships smex with **zero assets** and drops out of the
      shipped-asset guard at the same time.
- [x] **F0.2** Verify: full build clean, `scripts/exmod.ps1 test all` green. That is the baseline every
      later stage is measured against.

---

## F1 — the release identity is wrong *(the recurrence)* — **done 2026-08-13**

Every source modinfo version is **below** what `dist/Releases/` already holds. This is the
release-stamped-below-its-migrations failure recurring, and the guard written after the first
occurrence was deleted.

| Mod | Source | Shipped |
|---|---|---|
| exlib | 0.7.0 | 0.7.2 |
| lpex | 0.6.4 | 0.6.8 (as ppex) |
| smex | 0.9.5 | 0.9.8 |

- [x] **F1.1** Bump the three source versions above their released counterparts — exlib 0.7.0 → **0.7.3**,
      lpex 0.6.4 → **0.6.9** (continuing ppex's line; a rename does not reset it, because a pre-rename
      config file is folded into the renamed mod's section still carrying the version that wrote it), smex
      0.9.5 → **0.9.9**. iwex and hpex stay at 0.1.0 — neither has ever been published.
- [x] **F1.2** Raise the dependency floors that named the old versions. A modinfo dependency is a
      **minimum**, and exlib 0.7.2 is in the wild — `"exlib": "0.7.0"` let a player pair these mods with an
      exlib predating six of the subsystems they compile against. `ModinfoTests` already asserted this and
      the bump is what made it bite.
- [x] **F1.3** `No_mod_s_source_version_is_at_or_below_a_version_it_has_already_released` in
      `ModinfoTests`. Released versions are read from the zip names under `dist/Releases/` rather than
      restated, so recording a release needs no edit to the test; `ppex` maps to `lpex`. Corpus asserted
      non-empty, since an empty map is the one way this guard could pass while the regression is live.
      Mutation-checked: restoring smex to 0.9.5 fails it.

⛔ **A claim from the audit that did not survive checking, recorded so it is not re-raised.** The report
said iwex ships "two config migrations above its own version, dead on arrival". The gate is
`CompareVersions(m.ToVersion, current) <= 0`, so a migration above the running version is **inert, not
dead** — iwex's 0.2.0 and 0.3.0 rows are pre-staged and fire on the bump that reaches them. That is
legitimate authoring in a mod that has never shipped.

- [ ] **F1.4** The real invariant underneath it is *a migration must be reachable*: for any mod that
      **has** been released, no migration's `ToVersion` may exceed the source version, because config files
      stamped at the released version already exist in the wild. That check needs to reflect over the mod
      assemblies, so it belongs in the integration suite the F0 notes want standing up — not in
      `ExpandedLib.Tests`, which sees only exlib.

---

## F2 — the product wrapper *(cheapest, highest leverage)* — **done 2026-08-13**

None of this is an API change. Together it is roughly a day and it changes exlib's category.

- [x] **F2.1** `GenerateDocumentationFile` on, in `mods/Directory.Build.props`. All five mods now emit an
      `.xml` beside their dll on all three TFMs; the mod zips pick it up for free (they copy the whole
      publish directory) and the exlib-testing bundle names it explicitly. `exlib.xml` is 8,860 lines.
      CS1591 (undocumented member), CS1573 (no `<param>` tag — this codebase documents in `<summary>`
      prose by design) and CS0419 (ambiguous cref across an overload set) are suppressed.

      ★★ **CS1574 is deliberately NOT suppressed on the current build**, and it paid for itself
      immediately: turning the doc file on surfaced **19 crefs pointing at nothing** — six in exlib,
      thirteen across the content mods — every one a doc statement that had gone stale against the code.
      All fixed. The best of them: `GreenSandItemDefinitions` cited
      `CastingCellLogic.SandIsReturned`, a member that does not exist; the real one is `AfterShakeOut`.
      Suppressed on the *legacy* targets only, alongside the existing CS8625, because a cref naming a
      1.22 API cannot resolve on 1.20/1.21 and no single cref works across both.

      ⛔ **The property block belongs in `Directory.Build.props`, keyed on `Exists(modinfo.json)` — not
      on `$(AssetDomain)`.** Properties in that file are evaluated *before* the csproj, so a property
      conditioned on a csproj-set value is silently inert: always false, and nothing errors. The first
      attempt did exactly that and produced no `.xml` with no diagnostic. (The `$(AssetDomain)`-keyed
      *ItemGroups* lower in the same file are fine — items are evaluated after every property.)
      `modinfo.json` is present for exactly the five mod projects and absent from the generator and every
      test project, so it is both the right marker and one available that early.
- [x] **F2.2** Real `<Version>`, read out of each project's own `modinfo.json` at evaluation time so the
      two cannot drift and a release still edits one file. `FileVersion` carries the full version;
      `AssemblyVersion` stays `major.minor.0.0`, because a binding identity that changed on every patch
      would break a dependent mod's assembly reference over a bugfix.
- [x] **F2.3** Generators shipped. *Done 2026-08-13.* `ExpandedLib.Generators.dll` goes into the
      `exlib-testing` bundle under `analyzers/`, with the `<Analyzer Include>` and `<AdditionalFiles>`
      snippets in the bundle README. The zip is the existing dev-library channel, so this needs no NuGet
      publishing infrastructure. Chosen over a package deliberately: the generator's API still moves, and
      the bundle already carries the harness a consumer needs alongside it.
- [x] **F2.4** Harness portable. *Done 2026-08-13.* `DefinitionGoldens` now accepts
      `RepoRootOverride` or `EXLIB_REPO_ROOT`, and its probe matches any `.sln`/`.slnx`/`.git` instead of
      a file literally named `VintageStory.sln`. Every path-relative helper - goldens, block-code table,
      handbook sync - previously threw outside this repository, which is most of what the bundle is for.

      ⛔ Fifteen test files in this repo carry their own copy of the old probe. They work, because they
      only ever run here, but each hardcodes the solution name. Collapsing them onto the harness helper is
      a follow-on, not part of this stage.


- [x] **F2.5** CHANGELOG caught up. *Done 2026-08-13.* A `0.7.3` entry covering the six public
      subsystems that landed between 0.7.0 and 0.7.2 with no entry at all - code-first definitions, the
      process-extension contract, the metal/material/liquid/heat catalogues - plus this stage's own
      packaging and the two fixes. ⛔ The release-gate step that fails when modinfo's version has no
      changelog entry is **not** built; without it this drifts again.

---

## F3 — the wiki teaches a framework that no longer exists

This repo guards its goldens, its lang keys, its code literals and its released block codes with tests.
The public API documentation is the one authored artifact with **no guard at all**, and it has drifted
accordingly.

- [x] **F3.1** Fix the outright errors. *Done 2026-08-14.* The first two were found by F3.4 rather than
      by hand — the guard was written first and then pointed at the wiki. The other two were found by
      reading the library, because the guard cannot see either.
      - [x] `Block-Networks.md` — the `public override` against a `protected virtual` member (CS0507) is
        `protected` now, and the members list no longer declares `AllowedOrientations` and
        `GetFallbackOrientation` abstract: both are virtual, the first with a definitions-derived
        default. The node example was rewritten around that default rather than hand-writing the
        orientation table, which is what the code-first defs removed.
      - [x] `Source-Generators.md` — the `ExAttributeGenerator` section is gone and `ExLangKeyGenerator`
        documented in its place, with the `AdditionalFiles` item a consuming csproj needs and the note
        that only the primary domain feeds it. The page also states the analyzer-only packaging.
      - [x] `Getting-Started.md` — the `partial` advice is gone (with the sample class's own `partial`),
        the "compiled into `lpex.dll`" claim is replaced by what actually happens, and the outside-this-repo
        route now names `exlib.dll` rather than `ExpandedLib.dll` and defines `$(GamePath)` instead of
        assuming it. Three defects the plan had not predicted came out of the same read, and all three
        are worse than the ones it had: **the `ProjectReference` snippet was missing `<Private>false</Private>`**,
        which makes the game refuse to load the reader's mod entirely and name no cause; the snippet
        does not compile (`CommandRegistry` is in `.Commands`, not `.Entities`); and the dependency floor
        was pinned at `0.7.0`, which a minimum-not-a-pin dependency lets a player satisfy with an exlib
        predating two documented breaking changes. Added: the `[assembly: ExDomain]` section, scoped to
        the cross-assembly path it actually governs.
      - [x] `Config-System.md` — rewritten around the shared sectioned document, which the page had
        never mentioned at all: it taught per-mod files, and the file name it taught as current
        (`lpex_values.json`) is now a *legacy* name folded into the `lpex` section of `ex_values.json`.
        The fabricated `Edit` is gone, and with it three more wrong behaviours the audit surfaced —
        the write-back is **server-only**, an out-of-range value is **reset, not clamped**, and
        `LegacyFileNames` **folds** rather than renames.
        ⛔ F3.4 caught **none** of this, as predicted and then some: the `Edit` is written as a bare
        declaration inside a fence whose `class ExConfigRegister<TConfig>` line the guard's
        `ClassWithBase` regex cannot parse (no room for the type parameter), so the declaration rules
        return before reaching it. Every other defect was prose or a JSON literal. The guard narrows the
        surface; it does not close it.
- [x] **F3.2** Correct the retired inheritance model. *Done 2026-08-14.* Both pages are now fronted
      with the three axes — form owns the base-class slot, process and membership are behaviours — and
      each names the escape its own reader needs: `BEBehaviorNetworkMember` for a block whose form is
      already spoken for, the production behaviour for the same case on the process axis.
      `BlockEntityNetworkNode` is described as what it is, a host for that behaviour, so the two routes
      visibly join.
- [x] **F3.3** Settle the preference-wiring order. *Done 2026-08-14.* ★★ **The disputed constraint does
      not exist in either direction, so the settlement is to delete the claim rather than pick a winner** —
      picking one would have shipped a third wrong statement. `ExPreferences.LoadConfig` never reads the
      registered set and `PreferenceRegistry.RegisterAll` never reads the config, so their relative order
      is free; exlib itself does the opposite of what the wiki instructed, which is what the contradiction
      actually was.

      The real constraint is a different one nobody had written down: register preferences **before the
      mod's own `CommandRegistry.RegisterAll`**, because a preference sub-command resolves its definition
      **once, at registration time** (`MeasureSubCommand`), so a command built first holds a throwaway
      fallback — or, for a consumer writing `Find(key)!`, throws at world load. `Registries.md` also told
      consumers to call `LoadConfig` themselves, which exlib already does; that is now stated as
      not-yours-to-call, alongside `ApplyForPlayer`, which exlib runs on `LevelFinalize`.

      ⛔ Only one of the three statements was in the wiki. The other two were **source XML docs** —
      `PreferenceRegistry`'s own summary carried both the wrong order and a claim the `.exmod` command
      "builds a sub-command per registered preference", which nothing implements, and
      `ExpandedLibModSystem` asserted the shipped order with a **false reason**. Fixed in place; see F3.5.

- [x] **F3.5** Close the drift at its source. *Added and done 2026-08-14.* The wiki's two worst config
      errors were lifted verbatim from stale XML docs in exlib, so fixing only the wiki would have
      regenerated them on the next read. Corrected: `ExConfigRegister`'s class summary, `LegacyFileNames`,
      `Load` and `Sanitize` docs; `PreferenceRegistry`'s summary; `ExpandedLibModSystem`'s class doc
      (`exmod.json` → `exmod_preferences.json`) and its false inline reason; `IExConfigAccess.Range`
      (documented `"0 to 1"`, formatter emits `"0..1"`/`"0+"`); and lpex's README, which listed a
      `Preferences/` directory that moved to exlib. ★ This is the difference between fixing a page and
      fixing a page's cause.
- [x] **F3.4** **The guard.** *Done 2026-08-14.* `WikiParity` in `ExpandedLib.Testing`, driven by
      `WikiParityTests`. It checks three claims, each chosen because it is unambiguous and a reader acts
      on it: a member written against a type exlib owns must exist; an `Ex`-prefixed identifier written
      as code must name a type; and a declaration's shape must match the base — `public override` against
      a `protected` member is CS0507, and `abstract` against an implemented member tells a consumer to
      write something they need not. **129 symbols checked** across 15 pages.

      ★ **It found all three of F3.1's predicted defects on its first run**, plus the members-list drift
      the plan had recorded only for `AllowedOrientations`: `GetFallbackOrientation` was declared abstract
      there too. Mutation-checked in both directions — reinstating the deleted generator name and widening
      an override back to `public` each fail it.

      ⛔ Two things learned building it. A declaration list is where a doc drifts furthest from the code,
      and it has no `class X : Base` line to resolve against, so the base is taken from the section's own
      heading (*"Key `BlockNetworkNode` members to know"*). And the generators **cannot be reflected** —
      the project sets `IncludeBuildOutput=false` and ships no runtime assembly — so their names are read
      out of `mods/exlib/generators/*.cs` instead of allowed by hand, which is what keeps a deleted
      generator failing.

---

## F4 — the two missing pages, and the boundary

- [ ] **F4.1** `Lifecycle.md`. `AssetsLoaded`, `AssetsFinalize`, `StartPre` and `ExecuteOrder` appear
      **zero times** in 2,623 lines of wiki, yet that is where exlib does its most consequential work, and
      exactly one `ExecuteOrder` override exists in all of `src/`. A table of phase → what exlib has done
      by then → what you may call there, one row per public entry point.
- [ ] **F4.2** `Code-First-Definitions.md`. `assets/` holds zero `blocktypes/` and zero `itemtypes/`
      across all five domains; 102 files implement the provider interfaces and 175 defs ship through them.
      The wiki mentions `ExBlockDef` once, in an aside on the testing page. Promote it from
      `docs/design/mechanics/recipes-config.md` §§1–2 and route Getting-Started through it.
- [ ] **F4.3** Publish the API boundary. 226 public types, 6 internal, zero markers separating contract
      from incidental. One page naming which namespaces are supported, `[EditorBrowsable(Never)]` on the
      rest, and a stated one-release `[Obsolete]` rule before any public member is removed. The library
      has **zero** `[Obsolete]` in its history; vsapi carries 130.
- [ ] **F4.4** `samples/HelloExpanded/` — one registered block with a definition provider, one block
      entity hosting the production behaviour, one lang entry, one config value. There is currently no
      template, no sample and no minimal working machine anywhere in the repo or either shipped zip, and
      Getting-Started ends at a call that produces nothing a player can see.

---

## F5 — diagnostics: turn on the checks that are already written

`mods/exlib/testing` holds ten validators whose own doc comments enumerate the exact silent
failures — *"fails silently in game"*, *"the block can never be placed, silently"*, *"neither throws nor
logs"*. Every one is opt-in, headless, and documented nowhere.

- [ ] **F5.1** `ExlibChecks.All(api)` invoked at `AssetsFinalize`, on by default and config-suppressible,
      plus an on-demand `/exmod verify`. Roughly half the silent-failure list becomes one log line naming
      the fix, with no new messages authored.
- [ ] **F5.2** Log a summary line per catalogue at `AssetsFinalize`: files read, entries loaded, errors.
      Today a file dropped in `config/stageladder/` instead of `config/stageladders/` is unbroken silence,
      and "exlib found none of your content" is indistinguishable from "exlib is not installed". Vanilla's
      patch loader prints a count with an error tally; we print nothing.
- [ ] **F5.3** Thread the asset location into the catalogue loaders that drop it. *"Skipping material role
      def with no role"* names no file, and three mods ship that filename.
- [ ] **F5.4** The catalogue deserializer uses lenient binding, so a misspelled key is not a warning at
      all — the property silently takes its default. Make unknown members an error naming the file and the
      key.

---

## F6 — the two hazards

⛔⛔ **F6.1 was already guarded — the audit was wrong, and this is the third such claim.** The report said
a migration naming a still-live source code "silently rewrites the player's world" with nothing catching
it. `ReleasedCodeCoverageTests.No_migration_claims_a_code_that_is_still_alive` has covered exactly this
since before the audit, and its own comment states the same reasoning the report presented as new —
including that `BuildRemapTable`'s only source-side guard (`GetBlock(oldCode) != null`) passes for a live
block. The code reading is accurate; the conclusion that nothing catches it was not.

- [ ] **F6.1** What is genuinely left is narrower: that guard is a **test over our five mods**, so it does
      not cover a third party using the migration framework — which is offered as public API. A runtime
      check needs a way to tell a live registered block from a missing-block placeholder, and vsapi exposes
      none (`GetBlock` resolves both, and `BlockId == 0` is the repo's only proxy). Worth doing when the
      integration suite lands (M.6), scoped to third-party assemblies.
- [x] **F6.2 A mistyped network type no longer throws out of chunk load.** *Done 2026-08-13.* exlib
      guarded the blank-string case with a comment stating the reason verbatim — *"one bad declaration
      would take a world down"* — and a typo hit the same path unguarded. `AddNode` now uses
      `TryCreateNetwork` and, on a miss, logs an error naming the block code, the position, the requested
      type and the registered types, then adds no node. Mutation-checked.

      ★ Only an **isolated** node reaches the factory — one placed against an existing run joins that
      network instead — so the crash was intermittent and position-dependent. That is why the fix is worth
      more than the one-line diff suggests.

      The throwing `CreateNetwork` is kept for the two internal callers (fracture split, root rebuild),
      which take the type off a live network instance: there, a miss is an exlib invariant violation
      rather than a mod declaring a bad type, and failing fast is correct.

---

## F7 — one library, not four

- [ ] **F7.1** Generalise the contributor hook. `MaterialRoleRegistry.RegisterContributor` is re-invoked
      by its loader after the clear — the correct pattern, applied to **one catalogue of six**. The other
      five erase a C# contribution silently at `AssetsFinalize`, which is why the wiki's documented C#
      extension route produces no items and no error. One contributor contract, invoked by every loader
      after its clear.
- [ ] **F7.2** A naming law in [conventions.md](../../design/conventions.md), then applied: `*Registry` is
      a keyed catalogue, `*Loader` reads assets into one, `Contribute` merges, `Register` declares.
      Currently there are five names for "a keyed catalogue", four verbs for "add an entry", three meanings
      of `Load` and four error conventions.
- [ ] **F7.3** Split `Helpers/`. Content-neutral utilities stay; the family catalogues (`ExSounds`,
      `ExParticles`, `ExBlockNames`, `ExMoldGate`, `ExMoldDrops`) move somewhere marked *ours, not yours* —
      a stranger currently cannot tell which half is API.

---

## F8 — the authoring API gaps

Each is independently useful; none blocks the others.

- [ ] **F8.1** `Raw` → `RootKey` (with an `[Obsolete]` alias, once F4.3's rule exists). `Attribute` writes
      `attributes.{key}`; `Raw` writes a top-level key the game can never read. Both take
      `(string, object)`, both compile, and the goldens pass either way because they pin emitted JSON, not
      reachability. **This already shipped here** — a whole stage-ladder contract was authored with `.Raw`
      and every feed was refused at runtime with no build, golden or log signal. Then warn on emit when a
      root key is not in the known schema, which `vsapi/docs/json-docs/` enumerates.
- [ ] **F8.2** Finish `ExItemDef`. 30 methods against the block builder's 82, missing `Behavior`,
      `Handbook`, `SkipVariants`, `AttributeByType`, `TextureByType` and a positional transform overload —
      so item defs collapse into anonymous-object soup and **the C# comes out longer than the JSON it
      replaced**.
- [ ] **F8.3** Move `BlockCodeEmitter` into `ExpandedLib.Generators` so the typed code table falls out of
      `dotnet build`. Today it is emitted by a *test* assembly under an env var, so authoring one machine's
      layout requires standing up an xUnit project — and 72 of 72 `OutputBlock` calls pass a raw literal
      anyway.
- [ ] **F8.4** Validate the multiblock layout's origin the way the filler DSL beside it already validates
      its own. `Origin` must be the negation of the anchor's grid position and nothing checks it; get it
      wrong and the structure never completes at any angle with no message anywhere.
- [ ] **F8.5** Lift `ExBlockState` into the four machine bases. It is the correct answer to the
      "field missing from `ToTreeAttributes` reads zero on the client" bug class, and across four content
      mods **41 block entities hand-write the tree pair and exactly one uses it** — because no base a
      machine actually derives from owns one.
- [ ] **F8.6** Enum overloads for `renderpass`, `faceCullMode`, `drawtype`. The same builder takes enums
      for `Material` and `MineTool`, so a newcomer infers a rule that does not hold.
- [ ] **F8.7** A duplicate definition code silently overwrites, last-writer-wins, ordered by load — the
      exact doctrine the process registries argue against on their own class comment.

---

## M — the merge *(ruled 2026-08-13; see M1–M6 in [STATE.md](STATE.md))*

Five mods become two: **`iiex`** (Iron Industry Expanded, absorbing iwex + lpex) and **`siex`** (Steel
Industry Expanded, absorbing smex + hpex). `exlib` is unchanged. Full domain consolidation — every block
code moves — with the pipe tier moving onto a `tier` variant group so M3's bootstrap rung survives it.

⛔⛔ **The constraint every task below is measured against (M3): a merge collapses no tier.** The plated
pipe family and the iron gears are the early loop's bootstrap rung. It is tempting to read two
near-identical pipe families as duplication and delete one — that would delete the progression. Both
tiers ship, separated by the variant, with distinct display names.

### Order is forced here, unlike the F stages

- [x] **M.0 The tier goes into the asset path, not just the variant grammar.** *Added and done
      2026-08-14, as M.4's hard prerequisite.* M.2 put the tier on the **code** and fixed the handbook
      `groupBy`; it did not touch the **asset Location**, which is what `ExDefinitions` actually keys on.
      `ExBlockDef.Location` is `{domain}:blocktypes/{assetName}.json` and `BlockPipe.Common` passed the
      same `assetName` for every tier, so under one domain the plated and cast tiers collided on **six
      blocktype Locations and one recipe Location**, last-writer-wins and unlogged — a whole tier deleted
      by the first green build, which is M3 executed silently.

      ★★ **Written as a failing assertion first** (`PipeTierLocationTests`, in exlib so it holds for any
      tier pair a consumer adds): two tiers in one domain share no definition Location, no segment shape,
      and exlib pins no shape to a content mod's domain. All three failed, naming the six Locations and
      six shape paths, before anything was changed.

      Built: `BlockPipe.Asset(tier, leaf)` → `pipe/{tier}/{leaf}`, used by both the segment factories and
      `BlockPipePassthrough`; segment art moved to `assets/{iwex,lpex,hpex}/shapes/pipe/{plated,cast,rolled}/`;
      the two recipe files split to `pipes-plated` / `pipes-cast`. All three tiers' goldens and the
      generated block-code tables regenerated, never hand-edited.

      ⛔ **Two of the six shape "collisions" were the design, not the bug.** The passthroughs deliberately
      share one brick mesh across every tier and differ only by the sheet texture, so they keep one shape —
      but it was written `iwex:pipe/passthrough`, a **content domain hardcoded inside exlib**, which
      resolves to nothing the moment iwex is renamed. That art now lives in `mods/exlib/assets/exlib/shapes/pipe/`
      and the literal is gone. A blanket "move every pipe shape into a per-tier folder" would have been
      wrong here.

      ⛔ **Still open for M.4, and a naive rename sweep misses it:** `BlockPipePassthrough.Sheet` returns
      `iwex:block/metal/castiron`, and `mods/siex/assets/siex/shapes/pipe/rolled/*.json` name the same texture. The
      texture is referenced by ~40 files across three mods, so it was left alone deliberately; the four
      **hpex** references are outside both merging trees and a per-file sweep over "the two merging asset
      trees" will not see them.

      ★ A premise that did not survive: the scouting said no guard resolves a `shapeByType` target to a
      file on disk. `DefinitionAssets.MissingShapes` has done exactly that all along and is wired into all
      five suites (as they then were) — it resolves the **shape's own** domain, so the cross-domain move was verified rather
      than assumed. Mutation-checked by hiding one moved file.

**M.1 Decouple asset domain from mod id.** `EntityRegistry.RegisterAll` passed `mod.Info.ModID` to every
provider and `<AssetDomain>` is a single MSBuild scalar. **Do this first**: it is what lets one assembly
carry two domains during the transition, so the rename can land mod by mod instead of as one atomic cut.
The repo already proves a mod can ship a foreign domain — iwex ships `game:` via `ShipGameLangOverride`.

- [x] **M.1a Class keys resolve from the type's own assembly.** *Done 2026-08-13.*
      `[assembly: ExDomain("<modid>")]` on each of the five mods; `EntityRegistry.DomainOf` prefers it,
      falls back to the modid recorded by `RegisterAll`, then to the caller's domain. An assembly-level
      attribute rather than a registration call because it carries **no load-order dependency** — a key
      resolves before the owning mod's `Start` has run.

      All **twelve** hand-typed cross-assembly strings are gone, replaced by
      `Class<T>()` / `EntityClass<T>()` / `EntityBehavior<T>(props)` and a new
      `FillerBehaviorSpec.Of<T>(face, properties)`. Two additions were needed to make that possible: the
      typed `EntityBehavior<T>(JObject)` overload, and the filler factory.

      ★★ **Every golden passed unchanged**, which is the parity proof: the typed calls emit byte-identical
      JSON to the strings they replaced, so this is a pure authoring-safety change with no content diff.
      Guards added and both mutation-checked — reverting `KeyFor` to the caller's domain fails one,
      deleting a mod's `ExDomain` fails the other. Verified on the full gate, 15 targets, 3,921 tests.

      ⛔ `BlockPipePassthrough` carried a six-line comment explaining that it pinned literals *because*
      the typed overloads keyed off the definition's domain, and noting that the block half of that
      failure is never logged. That comment was an accurate bug report; it is now deleted along with the
      workaround.

      ⛔ The repo enforces its comment style by test (`CommentStyleGuards`): doc comments ≤16 lines, no
      `<b>`/`<i>`, no marker glyphs in code. Rationale belongs here and on design pages, not in the
      source. Three violations were written and corrected.
- [x] **M.1b A definition provider can declare its own domain.** *Done 2026-08-13.* `[ExDefDomain("…")]`
      on a provider class overrides the domain its `Definitions(string)` factory is handed; absent, the
      registering mod's domain still applies, so nothing existing changes. This is what lets an absorbing
      assembly keep emitting the absorbed mod's codes while the relocation lands, instead of the merge
      being one atomic cut. It is also the general form of what `MetalFamilyEmitter` already did ad hoc.
      Mutation-checked.
- [x] **M.1c A project can pack several asset trees.** *Done 2026-08-13.* `$(AssetDomainAbsorbed1)` /
      `$(AssetDomainAbsorbed2)` alongside the primary `$(AssetDomain)`, with `ShipGameLangOverride` now
      one more glob of the same shape rather than its own block. Verified end to end: lpex built with
      `-p:AssetDomainAbsorbed1=smex` packs both trees with nesting intact.

      ⛔ **The globs live in `mods/Directory.Build.targets`, and they are deliberately repetitive.** The
      glob path needs `$(AssetDomain)`, which the csproj sets *after* `Directory.Build.props` is
      evaluated — the same trap as F2.1. Three shorter spellings were tried and all three fail while
      looking correct:
      - `@(domains->'…\%(Identity)\**\*')` in an `Include` — a value produced by an item transform is
        never wildcard-expanded, so a literal `**\*` reaches the copy and it fails on a missing file.
      - that transform built into a property first — item references are **not expanded in properties
        during evaluation at all**; the property just holds the `@(…)` text (confirmed with
        `-getProperty`).
      - a batched target with an unqualified `%(RecursiveDir)` in `Link` — the metadata resolved against
        whatever else was in scope, which silently re-linked the project's **own** `modicon.png` and
        `modinfo.json` under `assets/<domain>/` and packed no asset files at all.

      ★ Only the **primary** domain feeds `ExLangKeyGenerator`. A packed foreign tree is either an
      override of someone else's keys or an absorbed mod's own, and neither should emit typed constants
      into this mod's `{Domain}Lang` class.
- [x] **M.2 The pipe `tier` variant** (M4). *Done 2026-08-14.* `tier` is declared **first**
      (`pipe-{tier}-{type}-{orient}`) on the four segments of all three tiers and on both valves; the
      bricks are untouched. `RegisterBurst`/`RegisterThroughput`/`RegisterJoint` are keyed on
      `BlockPipe.Tier` — the block's own variant — with the untiered block falling back to the same
      defaults every fitting relied on before, so nothing behavioural moved. Each tier now renders its
      own name: **Plated / Cast / Rolled Piping**, ru and uk alongside (draft, pending the owner's read).
      `rolled-pipe.md`, `cast-pipes.md` and `pipe-network.md` § 5 rewritten. Verified on the full gate,
      15 targets, **4,017 tests**.

      ★★ **The scouting's central claim held, and it was already guarded.** Tier-first keeps every
      `*-straight-ns` selector matching, as `WildcardUtil.fastMatch`'s backtracking implies — but the
      proof is not the reading: `EmittedBlocktypeShapeTests` has asserted since before this stage that
      every emitted variant resolves a shape, and its own doc comment names *"inserting a variant group
      ahead of an existing one"* as the hazard. Mutation-checked by declaring `tier` last **and
      re-blessing the golden**, which is the realistic mistake; it still failed.

      ⛔ **Two corrections to the scouting.** The four `Handbook("pipe-straight-*")` selectors must go to
      **`pipe-{tier}-straight-*`**, not the scouted `pipe-*-straight-*`. An undomained groupBy selector
      is qualified with the grouping block's own domain before matching
      (`SlideshowItemstackTextComponent.cs:108-115`), so the three tiers group separately **today only
      because they are three domains**; `pipe-*-straight-*` would merge them the moment the merge lands,
      which is M3's exact prohibition. And `PipeMigration` *did* need an edit: the rows built from live
      blocks follow the tier through `Code.Clone()` as scouted, but its three **legacy** blocks
      (`gaspipe-blower`, `gaspipe-heated`, `gaspipe-intake`) name `iwex:pipe-straight-*` as a literal
      target and had to be retargeted by hand.

      ★ **A new guard, because the one surface I moved had none.** `groupBy` selectors are start-anchored
      and an unmatched one is silent — the same failure class as `shapeByType`, which was guarded, on a
      sibling key that was not. `Every_handbook_group_selector_matches_a_shipped_code` resolves each
      selector the way the game does and matches it against every shipped code. It caught a real stale
      golden during this stage.
- [x] **M.3 De-`lpex:` the smex layouts — FOLDED INTO M.4** *(2026-08-14)*. Its premise did not survive
      checking: smex hard-depends on lpex, so `BlockCowperStoveIntake.cs:53-54` always resolves and no
      shipped machine is unbuildable. What remained was a code move the merge performs anyway — and
      doing it early is not free, because there is no neutral target. A selector cannot span domains, so
      pointing the cowper at the only non-`lpex:` passthrough (iwex's **plated** one) makes a steel-loop
      machine cheaper to build, and the outlet has no non-`lpex:` home at all: giving iwex one means a
      block, a recipe, lang and a handbook page whose only consumer is a steel-loop machine.

      ★ Under **M2** the loops are nested and a siex layout naming an iiex code is exactly what is
      expected, so `lpex:pipe-cast-passthrough-*` → `iiex:pipe-cast-passthrough-*` is a `CodeRelocation`
      row in M.4 and nothing else. B23's setting is picked up there too. The design ruling this stage
      cited ([gas-producer](../../design/machines/gas-producer.md) Open 9) is corrected in place.
- [x] **M.4 The `iiex` merge — DONE 2026-08-14.** iwex + lpex into one assembly and one domain. Every `iwex:` and `lpex:`
      code relocates through `CodeRelocation`, which has already executed two cross-domain moves. The
      released `ppex:` codes get one more hop. Update `ReleasedCodes` and the coverage guard.

      ⚠️ **The passthrough collision is closed at the CODE level and STILL OPEN at the asset-Location
      level** *(correction 2026-08-14; this row previously read ✅ CLOSED and that was wrong)*. They carry
      the `tier` variant as built — `iwex:pipe-plated-passthrough-{brick}-*` and
      `lpex:pipe-cast-passthrough-{brick}-*`, distinct display names — and M.2's migration surgery below
      is real. But **`ExDefinitions` keys on `ExBlockDef.Location`, not on the code**, and
      `Location => {domain}:blocktypes/{assetName}.json` carries **no tier**: `BlockPipe.Common` passes
      the same `assetName` (`pipe/straight`) for every tier and adds `tier` afterwards as a
      *variant group*. Under one domain the two tiers' **six blocktype Locations and one recipe
      Location collapse to one each**, last-writer-wins, ordered by load, with no error (F8.7) — which
      is M3's exact prohibition, executed silently. The shape art collides the same way at
      `mods/iiex/assets/iiex/shapes/pipe/*.json`.

      ⛔ **So M.4 has a hard prerequisite: put the tier into the asset name and the shape folder**
      (stage **M.0** below) before any domain moves. It is an exlib change and it regenerates all three
      tiers' goldens, hpex included.

      The migration surgery M.2 did was two changes, both still correct:
      `PipeMigration`'s brick branch rebuilds its old smex code from the variants instead of from the
      whole live path, and `PpexRenameMigration` moved the two passthrough rows onto `CodeRelocation`'s
      rename overload — without which **120 released `ppex:` codes lose their migration**, which is what
      the mutation check showed.

      ⛔⛔ **The passthroughs collide, and they collide silently.** Found 2026-08-14 while scouting M.3.
      iwex and lpex **both** call `BlockPipePassthrough.Passthroughs(domain)` — iwex from
      `PlatedPipeDefinitions`, lpex from `CastPipeDefinitions` — so both ship
      `pipe-{passthrough|passthroughbend}-{brick}-{orient}` off the same factory, at the same asset
      path, with the same shape. The **only** difference is the metal sheet texture: corroded iron for
      iwex, `iwex:block/metal/castiron` for lpex. Under one domain the two defs land on one
      `ExDefinitions` key and the loser is dropped **last-writer-wins, ordered by load, with no error**
      (F8.7). Unlike the segments this is not resolved by construction: M.2 deliberately left the
      passthroughs untiered, because they bear no pressure and because `PipeMigration`'s brick branch
      rebuilds its old code as `"gas" + path` from the whole live path.

      ★ **It is an owner question, not an engineering one.** M3 says a merge collapses no tier, and the
      pair *is* a plated/cast pair — but expressed as a texture, with identical mechanics
      (`BurstPressure => float.MaxValue`, one shape, same flanged joint, same recipe shape). Either the
      cosmetic distinction is progression and the passthroughs need the `tier` variant too (plus the
      migration surgery M.2 avoided), or it is duplication and the two collapse to one blocktype (plus a
      migration for whichever code retires). Decide before M.4, because after the merge one of them is
      already gone and nothing said so.
- [x] **M.5 The `siex` merge.** *Done 2026-08-14.* smex + hpex are **`siex` 0.9.9**: one assembly, one
      domain, one ModSystem, one config section, one suite. M1 is complete and the mod set is closed at
      `exlib`/`iiex`/`siex`. Record, including the three things its plan got wrong:
      [2026-08-14-m5-siex-merge-execution.md](2026-08-14-m5-siex-merge-execution.md).

      ⛔⛔ **All three misses were caught by a guard, not by review** — the four traps above are real and
      the documents did not save us from them. The stale `Domains` row (trap 3) was actively hiding **38
      unmigrated released codes**; the block-entity aliases (trap 4) cost six, not the one predicted; and
      a case nobody had listed — **a config section is keyed by the mod id**, so the rename silently reset
      every player value — was fixed in exlib with `LegacySectionIds`.
- [x] **M.6 The cross-mod resolution checks.** *Done 2026-08-14.* The half that mattered is built; the
      `test/Integration.Tests` half was **declined**, and both decisions are recorded here.

      ★★ **`ReferencedCodes` (exlib.Testing) + `CrossModReferenceTests` (siex suite), 4 tests.** It
      collects every code the mods *point at* rather than register — recipe outputs and ingredients across
      all three recipe shapes, RCC `requireStacks`, and every stack a definition body names — then
      resolves each against the union catalogue of the three mods. **928 references, 530 of them into a
      mod domain**; the rest are `game:` codes this harness holds no registry for and deliberately skips.

      ⛔⛔ **It found B23 on its first run** — `iiex:pipe-straight-ns-{metal}`, a code naming nothing, which
      made the Bessemer vessel unbuildable in **every** game mode. Fixed to `iiex:pipe-cast-straight*`.
      It also found five bare drop codes on the slag blocks (`slag-path-free` and kin) that parse as
      `game:` and so named vanilla blocks that do not exist; all five now carry `iiex:`.

      ★ **Stacks are found by shape, not by key** — any object with a `code` string beside a `type` of
      `item`/`block`. Keying on attribute names would silently stop covering an attribute the moment one
      was added, which is the exact failure the check exists to catch. It cost nothing: `variantgroups`
      and filler `behaviors` also carry a `code`, and neither has that `type`, so the shape test separates
      them cleanly.

      ⛔ **Vanilla codes are out of scope, and that is a decision rather than an omission.** 398 of the 928
      are `game:`. Judging them needs a per-version manifest built from `.game/<slug>/assets` — the mods
      ship against 1.20/1.21/1.22 and a code added in 1.21 is a real defect for 1.20 — which is its own
      unit of work. `A_large_minority_of_references_is_ours…` states both counts so a skip cannot read as
      a pass.

      ⛔ **A bare *wildcard* is not judged either.** The metal families emit `metalplate-*`, `rod-*` and
      `metalnailsandstrips-*` into the mods' own domains alongside vanilla's, so an unqualified wildcard is
      a net over both registries; only exact bare codes are reported.

      ★ **`test/Integration.Tests` declined.** Both its premises had expired: the directory was deleted
      2026-08-14 as an orphan, and `SteelIndustryExpanded.Tests` is already the only suite that sees all
      three mods, so it is where a cross-mod guard belongs. A fourth project would add a build target and a
      suite-list row to hold tests that are correctly placed today.
- [x] **M.7 Re-justify the entity pages** that cite the retired per-mod closure rule. *Done 2026-08-14.*
      ★ It was **12 pages, not ~15**, and the citation was not prose but the `**Mod**` header itself.
      Every one now names a live mod. ⛔ Four of them carried **M.4 rename damage** that made them
      nonsense rather than merely stale — `dies.md` read *"iiex (nail, bolt), iiex (rivet, stamping)"* and
      `rolled-parts.md` *"iiex owns the narrow products; iiex owns the wide ones"*, both being two
      different mods that the blanket rename collapsed onto one name.

      The forming-line parenthetical is **settled and written down** in both `rolled-parts.md` and
      `stock.md`: iiex owns the forming line — the mill, the wide hall and the bending roller — and siex
      owns the cast forms and the steel roll sets.
- [ ] **M.8 `heavyplate` absorbs `castplate`** (M5): one item, metal axis `{castiron, wrought, steel}`,
      three routes (sand cast, rolled from a wrought or steel slab, planed from a larger plate). `castplate-heavy`
      is live and shipped, so it needs an item migration. Rewrite `rolled-parts.md`'s
      "`heavyplate` is not `castplate`" section and D2's second clause.

---

## Not in this plan

Content and gameplay work continues to be sequenced by [NEXT.md](NEXT.md). The shear block remains the
forming line's last step. The player-facing defects found in the same pass — the twin-tub blower
reporting "no axle turning" while the axle turns, the four machines with no readout, the cast mold that
can delete metal with its warning suppressed, the silent RCC construction failure — belong with the
content plan, not here; they are recorded in STATE.md's *Other known defects*.
