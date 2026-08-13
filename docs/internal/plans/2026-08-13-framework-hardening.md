# Framework hardening — make exlib a library a stranger can adopt

**Status** live, started 2026-08-13. **F0, F1 and F2 are done** and verified on the full gate
(`scripts/exmod.ps1 test all` — 15 targets, 3,917 tests, green across 1.20/1.21/1.22). F3–F8 are
unblocked and not started. The mod merge waits on an owner ruling (see *Blocked on the owner*).

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

- [x] **F0.1** Restore `src/SteelmakingExpanded/SteelmakingExpanded.csproj` and
      `test/SteelmakingExpanded.Tests/SteelmakingExpanded.Tests.csproj` from HEAD. The lost
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

## F2 — the product wrapper *(cheapest, highest leverage)* — **F2.1–F2.2 done 2026-08-13**

None of this is an API change. Together it is roughly a day and it changes exlib's category.

- [x] **F2.1** `GenerateDocumentationFile` on, in `src/Directory.Build.props`. All five mods now emit an
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
- [ ] **F2.3** Ship `ExpandedLib.Generators`. It is `IncludeBuildOutput=false` and consumed only as an
      in-repo analyzer `ProjectReference`, so the wiki's canonical config recipe produces `CS0103`
      outside the monorepo with no diagnostic. Either an analyzer NuGet package or the dll under
      `analyzers/` in the exlib-testing zip, with the `<Analyzer Include>` snippet documented.
- [ ] **F2.4** Make the test harness runnable outside this repo. Five public helpers resolve paths by
      walking up for a file literally named `VintageStory.sln` and throw otherwise, which kills the
      block-code table, the definition goldens and the handbook sync for every outside consumer. Add an
      injectable repo root checked before the probe, widened to any `*.sln`/`.git` marker.
- [ ] **F2.5** Write the 0.8.0 CHANGELOG entry. The newest entry is 0.7.0 (2026-06-21) while 208 files and
      **six entirely new public subsystems** — `Processes`, `Definitions`, `Metals`, `Materials`,
      `Fluids`, `Heat` — landed after it. Gate it: the release step fails if modinfo's version has no
      changelog entry.

---

## F3 — the wiki teaches a framework that no longer exists

This repo guards its goldens, its lang keys, its code literals and its released block codes with tests.
The public API documentation is the one authored artifact with **no guard at all**, and it has drifted
accordingly.

- [ ] **F3.1** Fix the outright errors:
      - `Block-Networks.md` — the first example declares `public override` against a `protected virtual`
        member (CS0507), and declares `AllowedOrientations` abstract where it is virtual with a
        definitions-derived default.
      - `Source-Generators.md` — delete the `ExAttributeGenerator` section; that generator was deleted in
        `f4ddcea`. Document `ExLangKeyGenerator` in its place, including the `AdditionalFiles` a consuming
        csproj needs.
      - `Getting-Started.md` — drop the `partial` advice that routed readers to the deleted generator;
        correct the claim that exlib "is compiled into lpex.dll" (it ships as its own zip, referenced
        `Private=false`); add the `GamePath` fallback to the "consuming outside this repo" snippets.
      - `Config-System.md` — rewrite around the shared sectioned document rather than per-mod files, and
        drop the documented `Edit` method that does not exist on `ExConfigRegister<T>`.
- [ ] **F3.2** Correct the retired inheritance model. `Block-Networks.md` and `Production-Machines.md`
      both teach spending the base-class slot on being a graph node — the exact workaround the membership
      behaviour was built to remove. Front them with the three axes: form = base class, process and
      membership = behaviours.
- [ ] **F3.3** Settle the preference-wiring order. It is stated in three places and two of them
      contradict the library's own code.
- [ ] **F3.4** **The guard.** A doc-parity test in `ExpandedLib.Testing`: extract every C# fenced block
      from `docs/wiki/*.md`, reflect each member named against the assembly, fail on a mismatch. This is
      the same idiom already used for goldens, lang keys and released codes — extended to the one surface
      a third party is asked to build against. F3.1–F3.3 do not stay fixed without it.

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

`test/ExpandedLib.Testing` holds ten validators whose own doc comments enumerate the exact silent
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

- [ ] **F6.1** **A migration whose source code is still live silently rewrites the player's world.**
      `BlockMigrationModSystem` skips a source only when it resolves to nothing; it never checks the source
      is *dead*. The destination is checked properly. This is the one item on the list that corrupts a save
      rather than wasting time, and it is unrecoverable. Reject, with a named error, any remap whose source
      is still a registered block; add a `Force` opt-in for the deliberate case.
- [ ] **F6.2** **A mistyped network type throws out of chunk load.** exlib guards the blank-string case
      with a comment stating the reason verbatim — *"one bad declaration would take a world down"* — and a
      typo hits the same path unguarded. It only fires when the node is isolated, so it is intermittent and
      position-dependent. Make it `TryCreateNetwork`: log an error naming the block code, the position, the
      requested type and the registered types, and register no node.

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

## Blocked on the owner

**The mod merge.** The 2026-08-13 design reading confirms the tier pairing `{iwex+lpex}` and
`{smex+hpex}` is supported by four written rulings — material-gated power, rolling feedstock, fuel, and
machine class. It also surfaces two things that need an explicit ruling before any code moves:

1. **The corpus's stated unit of closure is the mod, not the pair.** [STATE.md](STATE.md)'s placement
   rule — *"an iwex-only player gets a complete early-19th-century loop … and iwex recipes never reach
   into lpex"* — is cited by name in about fifteen entity pages. Merging iwex and lpex deletes that rule's
   subject. That is a legitimate ruling to make (it would dissolve the duplication the rule forces — iwex's
   own pipe tier, its own spur and bevel gears) but it is a **new ruling, not a restatement**, and it needs
   to land on a design page before the code follows.
2. **Which mod id and ModDB listing survives** the merged upper mod. smex has the shipped history and the
   audience; ppex/lpex has the older listing. Code cannot settle this.

Two pieces of merge-adjacent work are **owed regardless of the ruling** and can proceed now:

- **De-`lpex:` the smex layouts.** Already ruled on the design side — *"the layout must not name `lpex:`
  fittings, or a smex machine becomes unbuildable without lpex's cast tier."* It is the prerequisite for
  smex-side independence under any packaging.
- **Decouple asset domain from mod id.** `EntityRegistry.RegisterAll` passes `mod.Info.ModID` to every
  provider, and `<AssetDomain>` is one MSBuild scalar. The repo already breaks the 1:1 convention — iwex
  ships the foreign `game:` domain through `ShipGameLangOverride`. Letting a provider declare its own
  domain makes any future merge a packaging change rather than a re-coding of every block, and it removes
  the twelve magic cross-assembly class strings on its own. Useful whether or not the merge happens.

---

## Not in this plan

Content and gameplay work continues to be sequenced by [NEXT.md](NEXT.md). The shear block remains the
forming line's last step. The player-facing defects found in the same pass — the twin-tub blower
reporting "no axle turning" while the axle turns, the four machines with no readout, the cast mold that
can delete metal with its warning suppressed, the silent RCC construction failure — belong with the
content plan, not here; they are recorded in STATE.md's *Other known defects*.
