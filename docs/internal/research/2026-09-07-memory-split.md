# Research snapshot - the assistant memory store split by subject

**Written** 2026-09-07 against the memory store at
`~/.claude/projects/-home-fallen-src-modding-vsexpanded/memory/` (243 files: the index and 242 notes).
**Covers** where each note goes when the repository becomes exlib, exmods and the workspace
(`docs/internal/plans/2026-09-06-the-split.md`, Task 2), and which notes the owner's requested prune
removes.
**Purpose** the mechanical input for that task; applied once, then this file is history.

Destinations: EXLIB (the framework repo's store), EXMODS (the family repo's store), PARENT (the
workspace store), ALL (copied into every store), DROP (deleted: completed-work records git history
carries, stale-path notes the layout note supersedes, rulings later rulings overturned).

## Overrides

The table below was produced by a reader over every note; these rows are decided by the driver
against the code and take precedence over the table:

| Note | Destination | Why |
|---|---|---|
| smex-moltenchisel-generalization.md | EXLIB | `MoltenChisel` and `BEBehaviorMoltenCell` live in exlib's industry module |
| smex-molten-renderer-onexchanged-reinit.md | EXMODS | `MoltenRenderer` lives in iiex |
| smex-moltenrenderer-temperature-staleness.md | EXMODS | same |
| smex-blocknumber-validation.md | EXLIB | the validation is in `BlockEntityMultiblockStructure` |
| smex-dynamic-block-light.md | EXMODS | the technique is demonstrated only in family blocks |
| item-piles-generic-placement.md | EXLIB | the placement system is exlib's, its two consumers are family |
| m4-iiex-merge-done.md, m5-siex-merge-done.md | PARENT | merge traps that inform the split |
| msbuild-node-reuse-leak.md | ALL | a machine trap every root hits |
| feedback-convenience-first-and-logical-layout.md | ALL | the directive outlives its examples |
| vsmc-model-creator-paths-and-wsl.md | EXMODS | absolute paths inside it are re-pointed at the exmods root when copied |

## The table

```
MEMORY.md	PARENT	index; rebuilt per store
adding-a-variant-group-moves-the-code.md	EXMODS	block-code wildcard trap specific to family blocktype variants
audit-over-reports-verify-before-acting.md	ALL	verify-a-guard-before-acting is a general working-style trap
backlog-rows-go-stale-and-audit-was-1-22-only.md	EXLIB	multiversion API-check technique + IPlayer mock seam in the testing harness
banks-throughput-only-layout-is-the-players.md	EXMODS	family design ruling on plant layout (docs/design/mechanics)
bessemer-scrap-heat-cost-was-display-only.md	EXMODS	converter (siex) heat-balance bug
blastfurnace-door-angle-sentinel.md	EXMODS	blast furnace door specific trap
blister-crush-fork-is-vanillas-refusal.md	EXMODS	iiex crucible-chain mechanic
blow-in-is-a-stored-bit.md	EXMODS	shaft furnace ignition mechanic
boiler-feedwater-injector-design.md	EXMODS	family machine design, not yet built
breach-has-three-gates.md	EXLIB	exlib base-class trap (BlockEntityMultiblockStructure.StopsProductionOnStructureLost)
builders-never-git-stash.md	ALL	git-safety rule for any agent working this tree
cast-stock-forms-built.md	EXMODS	iiex/siex cast-stock split ruling
charge-capacity-is-geometry.md	EXMODS	furnace heat-balance bug/ruling
charge-units-are-metal-units.md	EXMODS	family design ruling (charge/metal unit currency)
claude-tools-repo-and-vsshape.md	PARENT	external tools repo/process note, not repo content
coke-oven-vanilla-process-scaled.md	EXMODS	family design ruling
config-section-is-keyed-by-mod-id.md	EXLIB	exlib config-system trap (ex_values.json section keys)
content-waits-on-infrastructure.md	PARENT	roadmap/prioritization ruling for family content
convert-shape-unwraps-root.md	EXLIB	shape tooling bug (generator/tool, framework-owned)
cornish-boiler-megablock-built-uncommitted.md	EXMODS	family machine build status
crucible-furnace-stands-losses-not-draught.md	EXMODS	furnace mechanic
crucible-furnace-works-preheat-outside-smeltcycle.md	EXMODS	furnace mechanic
crucible-is-pool-only.md	EXMODS	furnace mechanic
crucible-pot-needs-three-variants.md	EXMODS	furnace block variants
definition-location-is-the-real-key.md	EXLIB	ExDefinitions framework keying rule
diagram-crafting-system.md	EXMODS	family gameplay system (design table)
doc-citation-drift-is-systemic.md	ALL	doc-citation hygiene applies to docs in every root
docs-drift-source-is-the-xml-comment.md	ALL	doc-drift fix discipline applies everywhere
docs-internal-and-extensibility-target.md	PARENT	planning-location process + product-scope ruling
editable-shape-textures-are-unprefixed.md	EXMODS	workbench editable/ art convention
electricalprogressive-power-parity.md	EXMODS	family balance vs another mod's generator
engine-ladder-plan.md	EXMODS	family engine roster plan
entity-class-strings-live-in-the-save.md	EXLIB	generic BE class-string save/migration trap, now guarded
exlib-behavior-capable-fillers.md	EXLIB	exlib filler-cell framework capability
exlib-config-system.md	EXLIB	exlib config framework
exlib-framework-implementation-2026-09-05.md	DROP	completed-work status log; landed items now in git history
exlib-generic-framework-assessment-2026-09-05.md	DROP	rulings resolved/overturned by exlib-framework-implementation-2026-09-05
exlib-github-wiki.md	EXLIB	exlib wiki generation pipeline
exlib-recipe-cost-framework.md	EXLIB	exlib recipe-cost framework
exlib-repo-split-not-yet.md	DROP	superseded by repo-split-and-publishing-plan-2026-09-06.md
exlib-testing-distribution.md	EXLIB	exlib testing-kit distribution
exmod-cli-lifecycle.md	EXLIB	build/CLI plumbing (dispatcher + scripts)
exmod-filter-flag-does-not-work.md	EXLIB	CLI tooling bug fix, reusable PS trap
fastener-benches-built.md	EXMODS	iiex machine build
feedback-blowers-animated-inlet-openings.md	EXMODS	art rule for blower shapes (family machines)
feedback-comments-describe-not-narrate.md	ALL	comment-style rule
feedback-convenience-first-and-logical-layout.md	ALL	general engineering-philosophy directive (organize logically, unify systems)
feedback-drop-adversarial-review.md	ALL	review-workflow rule
feedback-neutral-repo-voice.md	ALL	repo-voice/format rule
feedback-no-commits.md	ALL	git-ownership rule
feedback-optimize-our-time.md	ALL	general working-style directive
filler-accounting-needs-def-level.md	EXLIB	exlib StructureRig harness trap (definition-level accounting)
fillquadsbylevel-is-a-footprint.md	EXLIB	SurfaceRenderer framework gotcha
firebox-capacity-was-a-bounding-box.md	EXMODS	coke oven fix (family)
fixture-stub-block-hides-production.md	EXLIB	generic test-fixture trap
format-gate-needs-clean-tree-never-run-csharpier-alone.md	ALL	format-gate rule
forming-line-settled.md	EXMODS	family design ruling (rolling/forming)
framework-composition-and-a0.md	EXLIB	exlib composition axes
fuel-is-carbon-not-bands.md	EXMODS	furnace fuel ruling
furnace-air-and-readout.md	EXMODS	furnace readout/air ramp mechanic
furnace-core-cube-faces-and-editable-textures.md	EXMODS	furnace art convention
furnace-tap-facing-and-mirrored-drawings.md	EXMODS	furnace tap facing bug
gametime-away-catchup.md	EXLIB	generic exlib away-catchup mechanism
gas-system-owns-the-fuel-gas-rules.md	EXMODS	family gas-system design ownership
getbehavior-reads-collectiblebehaviors.md	EXLIB	VS engine/testing gotcha, generic
golden-write-now-scopeable.md	EXLIB	exlib testing harness feature
handbook-sync-pipeline.md	EXLIB	exlib handbook-sync framework
harness-megablock-structurerig.md	EXLIB	exlib StructureRig testing harness
hearth-flue-role-dynamic-chimney.md	EXMODS	furnace design detail
hearth-is-live-molten-cells.md	EXMODS	furnace mechanic
iiex-siex-are-new-mods-not-an-update.md	EXMODS	family mod-set scoping ruling
iron-chutes-approved-and-drawn.md	EXMODS	iiex art/plan
item-piles-generic-placement.md	EXLIB	stated generic exlib system (industry module) shared by hearth/rack
iwex-bringup-one-mod-at-a-time.md	PARENT	roadmap/playtest sequencing ruling
iwex-chill-and-recoverability.md	EXMODS	furnace mechanic
iwex-hearth-design-leads-code.md	EXMODS	furnace design-process note
iwex-ignition-is-positional.md	EXMODS	furnace mechanic
iwex-mixer-rotor-axis-sign.md	EXLIB	generic MP rendering rule (structure-angle-keyed sign)
iwex-twintub-blower.md	EXMODS	iron-tier machine
killed-agent-leaves-no-finally.md	ALL	agent/workflow trap
ladle-tier2-brick-lining.md	EXMODS	iiex machine
lang-call-sites-were-unguarded.md	EXLIB	generic LangCallSites guard across all domains
length-is-art-crops-are-config.md	EXMODS	forming-line ruling
m4-iiex-merge-done.md	PARENT	merge/split traps directly relevant to the coming exlib/exmods split
m5-siex-merge-done.md	PARENT	merge/split traps directly relevant to the coming exlib/exmods split
machine-shapes-contain-non-machine-groups.md	EXMODS	family machine-shape measurement convention
machining-line-is-two-families.md	EXMODS	machining-line ruling
machining-line-settled.md	EXMODS	machining-line design ruling
megablock-structureangle-and-shape-spin-must-be-paired.md	EXLIB	structures-base rendering trap
mid-gap-crop-ruled.md	EXMODS	forming-line ruling
mill-admits-feedstock-at-the-deck.md	EXMODS	mill mechanic
mill-never-refuses-on-length.md	EXMODS	mill design ruling
mill-outputs-need-form-plus-gap-key.md	EXMODS	mill/stage-ladder ruling
mod-architecture-and-split.md	PARENT	M1-M6 mod-set rulings, directly informs the exlib/exmods boundary
mod-thesis-hands-on-factory.md	EXMODS	family design philosophy (core loop)
moddb-api-and-release-checks.md	EXMODS	family mod ModDB/release process
molten-canal-conveys-not-levels.md	EXMODS	siex molten-canal mechanic
molten-pedestal-chisel-hierarchy-trap.md	EXMODS	family (BlockNetworkMolten) class-hierarchy bug, not exlib base
mp-filler-port-geared-ratio.md	EXLIB	exlib MP network base trap
mp-machines-speed-and-torque.md	EXLIB	exlib MP framework rule
mp-render-and-anim-phase-lock.md	EXLIB	exlib MP renderer base trap
mp-shaft-must-cross-face-centre.md	EXMODS	shape-authoring rule (art convention)
msbuild-node-reuse-leak.md	EXLIB	MSBuild/build-plumbing environment trap
msbuild-props-evaluate-before-csproj.md	EXLIB	MSBuild/build-plumbing trap
multiblock-oriented-parts.md	EXLIB	exlib structures-base layout feature
multiversion-legacy-support.md	EXLIB	exlib multi-version/runtime-floor framework
no-or-across-item-codes.md	EXMODS	recipe-authoring engine fact, applied in family recipes
one-modsystem-dll-per-mod-folder.md	EXLIB	exlib module-system engine finding (IExModule)
ore-crusher-decided-and-drafted.md	EXMODS	iiex machine, art in flight
parallel-reviewers-race-on-mutation-checks.md	ALL	agent/review-workflow trap
patching-game-dll-kills-logger.md	EXLIB	engine fact tied to exlib's vendored-source tooling
pipe-fittings-sketches.md	EXMODS	family pipe-fitting art
pipe-test-fixture-has-no-block-entities.md	EXLIB	exlib testing-harness trap (PipeTestWorld)
pipe-throughput-bore-and-cooling.md	EXMODS	family pipe-tier design ruling
pipe-tier-is-a-variant-declared-first.md	EXMODS	iiex pipe-tier variant mechanic
pipe-tier-shapes-and-joints.md	EXMODS	family pipe art/joint convention
pipe-volume-used-as-a-rate.md	EXLIB	exlib pipe-network base trap (Volume vs rate)
ppex-cycle-anim-must-repeat.md	EXLIB	exlib animation base trap
ppex-engine-submachine-anim-sync.md	EXLIB	exlib engine/submachine rendering base
ppex-engine-submachine-orientation-snap.md	EXLIB	exlib engine/submachine base mechanism
ppex-engine-submachine-plus180-frame.md	EXLIB	exlib engine/submachine base convention
ppex-fluidpump-intake-transfer.md	EXMODS	family machine (fluid pump)
ppex-handbook-hk-tag-and-unit-refresh.md	EXLIB	exlib handbook framework fact
ppex-litres-steam-spec.md	EXMODS	family steam-system design ruling
ppex-manual-fluid-pump.md	EXMODS	family machine
ppex-megablock-animator-single-cell-light.md	EXLIB	exlib megablock renderer base rule
ppex-mpgenerator-constant-power.md	EXLIB	exlib MP generator base mechanism
ppex-networknode-orientation-variant-key.md	EXLIB	exlib BlockNetworkNode base convention
ppex-overpressure-merge-split-clamp.md	EXLIB	exlib gas-network base trap
ppex-particles-central.md	EXLIB	shared exlib catalogues (ExParticles/ExSounds/ExOrientation)
ppex-pipe-single-medium.md	EXLIB	exlib pipe-network base design
ppex-pipe-throughput-display.md	EXLIB	exlib pipe-network readout mechanism
ppex-pipe-uniform-temperature.md	EXLIB	exlib pipe-network base design
ppex-pressure-valve-overflow.md	EXMODS	family block behavior (pressure valve)
ppex-rcc-construction-drops.md	EXLIB	vanilla RCC framework gotcha
ppex-steam-machine-immersion.md	EXMODS	family sound/particle immersion layer
ppex-submachine-output-absolute-power.md	EXLIB	exlib submachine base mechanism
ppex-valve-inline-sever.md	EXMODS	family block behavior (pipe valve)
process-routes-and-half-steps.md	EXMODS	forming-line/StageLadder ruling
pump-blower-set-sketches.md	EXMODS	family art sketches
raceway-rate-model-ruled.md	EXMODS	furnace rate-model ruling
read-design-docs-before-deciding.md	ALL	general process rule (search docs before deciding)
recipe-provider-layout.md	EXMODS	code-layout convention, examples are family mod namespaces
referenced-codes-guard-exists.md	EXLIB	exlib cross-mod code-reference guard
release-version-below-migrations.md	EXLIB	config-migration versioning trap (framework mechanism)
released-codes-manifest-was-two-releases-stale.md	EXLIB	ReleasedCodes generator behavior fix (framework tooling)
repo-layout-2026-09-05.md	PARENT	master path-translation note for the restructure itself
repo-on-wsl-and-the-eol-flip.md	DROP	completed action; CRLF fix landed in commit bd428603, git history carries it
repo-split-and-publishing-plan-2026-09-06.md	PARENT	restructure index entry, authoritative copy lives in the repo
research-map-2026-09-04.md	PARENT	research-map index for the roadmap
restructure-progress-2026-09-07.md	PARENT	restructure execution-state tracker
roadmap-2026-09-04.md	PARENT	plan of record for the roadmap
rolled-catalogue-built-b3c-closed.md	EXMODS	forming-line ruling, unreachable-route debt note
rolled-rod-is-vanilla-rod.md	EXMODS	family design ruling
rolling-mill-was-unusable-stockform.md	EXMODS	mill/WorkPiece bug (family)
ru-uk-localization-conventions.md	EXMODS	localization glossary specific to the family mods
run-tests-sh-stale-binary-trap.md	EXLIB	test-infra trap (run-tests.sh)
scope-metalworking-only.md	EXMODS	family mod content-scope ruling
section-class-missing-from-code.md	EXMODS	forming-line gap
selectiveelements-per-element-matching.md	EXLIB	VS shape/rendering engine fact
shape-child-coords-are-relative.md	EXLIB	VS shape engine fact underlying framework rendering
shape-clone-shares-its-faces.md	EXLIB	Shape.Clone() API bug, ExMeshCache framework
shape-design-workflow.md	EXMODS	family shape-design workflow (workbench)
shape-joints-not-parts-eight-defects.md	EXMODS	family shape-art defect rules
shape-ring-plates-radial-x-defect.md	EXMODS	family shape-art defect rule
shape-tools-were-not-looking.md	EXLIB	shape-conversion tool bug (generator/tool)
shear-art-is-filed-as-cutter.md	EXMODS	family shear art
shear-built-and-machine-layouts-arrived.md	EXMODS	family machine build + owner layouts
shipped-asset-invariant-guards.md	EXLIB	exlib repo-wide asset invariant guards
smex-animatable-flat-lighting-tradeoff.md	EXMODS	gas valve specific accepted tradeoff (family)
smex-animator-null-getblockinfo-nre.md	EXLIB	generic animUtil/animator null trap
smex-animator-onexchanged-reinit.md	EXLIB	generic animated-block wrench-rotation trap
smex-beehivekilndoor-getdrops-nre.md	EXMODS	specific family block subclass fix
smex-blastfurnace-orphaned-be-savereport.md	EXMODS	blast-furnace player bug report (family)
smex-blockentityattributes-pos-trap.md	EXLIB	generic BlockEntity.FromTreeAttributes gotcha
smex-blocknumber-validation.md	EXLIB	multiblock blockNumbers validation rule (framework-level)
smex-closed-valve-stale-pool-burst.md	EXMODS	family block bug (valve/network pool)
smex-converter-relief-and-createstack-bug.md	EXMODS	bessemer converter relief pass (family)
smex-cowper-stranded-air-mixlatch.md	EXMODS	cowper stove bug (family)
smex-dynamic-block-light.md	EXLIB	generic BE-driven block-light technique
smex-dynamic-connection-broken-rewalk.md	EXLIB	generic network base trap (IsConnectionBroken re-walk)
smex-em-nugget-crushing-compat.md	EXMODS	EM-compat fix specific to family content
smex-error-naming-conventions.md	EXMODS	code convention, examples are family mod error/lang keys
smex-held-mold-quad-double-sided.md	EXMODS	tool-mold rendering fix (family item)
smex-hopper-burden-tank.md	EXMODS	reinforced hopper (family block)
smex-hopper-container-packet-desync.md	EXLIB	generic GuiDialogBlockEntity/BlockEntityContainer pattern
smex-machine-ports-connector-adjacent-cell.md	EXLIB	INetworkConnector base-class contract
smex-megablock-filler-system.md	EXLIB	reusable exlib filler-block system
smex-megablock-surface-renderer-rotation.md	EXLIB	SurfaceRenderer framework base rule
smex-mold-disable-config-command.md	EXMODS	specific /exmod molds command for family content
smex-molten-live-cooldown-and-fitting-chisel.md	EXMODS	molten cooldown/chisel mechanic (family, siex)
smex-molten-per-cell.md	EXMODS	siex molten-canal design
smex-molten-renderer-onexchanged-reinit.md	EXMODS	molten-canal renderer bug (family)
smex-moltenchisel-generalization.md	EXMODS	molten-endpoint chiselling helper (family, siex-owned)
smex-moltenrenderer-temperature-staleness.md	EXMODS	MoltenRenderer temperature staleness (family molten system)
smex-mp-axissign-per-axis.md	EXLIB	BEBehaviorMPBase base-class contract
smex-multiblock-currentangle-normalize.md	EXLIB	MultiblockStructure base-class contract
smex-multiblock-gas-node-registration.md	EXLIB	multiblock-structure BE gas-node base contract
smex-multiblock-highlight-crash.md	EXLIB	MultiblockStructure framework fix
smex-multiblock-projection-behavior.md	EXLIB	shared multiblock projection base class
smex-ontesselation-stale-openfaces.md	EXMODS	molten-canal rendering bug (family)
smex-passthrough-bend-and-multiblock-regex.md	EXMODS	gas passthrough/cowper blocks (family)
smex-recipe-conflicts.md	EXMODS	brick-wrapped-pipe family recipe collision
smex-recipe-output-default-variant.md	EXLIB	generic VS recipe-authoring engine fact
smex-rightclickconstructable-rendering.md	EXLIB	generic vanilla RCC rendering requirement
smex-rightclickconstructable-storewildcard.md	EXLIB	generic vanilla RCC wildcard requirement
smex-toolmold-own-domain.md	EXMODS	specific family tool-mold blocks
smex-wrench-rotation-multiblock-be.md	EXLIB	generic network-block wrench-rotation contract
smithing-pattern-lands-transposed.md	EXMODS	family smithing-chain bug (ItemPig/ItemPuddledBall)
solid-drop-launders-into-vanilla-iron.md	EXMODS	metal solidDrop bug (family cast/pig iron)
spec-attribute-is-not-save-data.md	EXLIB	exlib SpecSchema/ladder framework rule
stock-stage-art-generated-not-drawn.md	EXMODS	family rolled-stock art generator
storage-rack-built-length-capacity.md	EXMODS	family storage-rack design
submachineside-is-dead-word-vs-letter.md	EXMODS	BlockEngine.SubmachineSide bug (family engine)
tap-plug-and-drawn-tap-shapes.md	EXMODS	furnace tap art/mechanic
tesselateshape-arg-is-not-a-cache-key.md	EXLIB	exlib ExMeshCache framework fact
test-dirty-precondition-hardening.md	EXLIB	generic test-suite discipline
test-style-regions.md	EXLIB	test-code style convention (harness)
tooling-wear-and-idle-draw.md	EXMODS	family tooling-wear/idle-draw ruling
tree-string-array-not-json.md	EXLIB	exlib ExTree framework helper
twintub-saturating-curve.md	EXMODS	blower mechanic ruling (family)
two-round-model-and-the-skip-hole.md	EXMODS	forming-line WorkPiece ruling
u10-closed-connector-check-built.md	EXMODS	furnace-plan closure (U2-U10)
u6-puddling-done-b8-closed.md	EXMODS	puddling furnace, U6 closure
u7-reheat-soak-done.md	EXMODS	reheat furnace, U7 closure
u8-done-forming-shop-discoverable.md	DROP	completed-work record (handbook page landed); no lasting rule beyond that
u9-1-coke-oven-stands.md	EXMODS	coke oven, U9.1-4 closure
u9-closed-gate-scenario-and-stale-steps.md	EXMODS	crucible-steel chain closure (U9)
vanilla-source-vendored.md	EXLIB	exlib vendored-source infra
vs-1226-iplayer-unmockable.md	EXLIB	testing-harness engine fact
vs-chat-vtml-angle-bracket-truncation.md	EXLIB	generic VTML/chat-output engine fact
vs-object-loader-injection.md	EXLIB	exlib code-first asset-injection mechanism
vs-shape-uv-rect-equals-face-size.md	EXLIB	VS shape engine fact
vsmc-model-creator-paths-and-wsl.md	EXMODS	shape-editing tool setup for family art workflow
waterwheel-clobbers-its-geared-ratio.md	EXLIB	vanilla compat bug affecting exlib MP nodes
watt-engine-farey-proportions.md	EXMODS	family engine art research
watt-engine-improved-shape-inflight.md	EXMODS	family engine art, C# owed
wiki-parity-guard-exists.md	EXLIB	exlib wiki-parity guard
wildcard-star-inside-alternation-is-regex.md	EXMODS	examples/tests are family (iiex) multiblock legends
workbench-art-arrived-grid-unjustified.md	EXMODS	family workbench build
xunit-generic-helper-breaks-discovery.md	EXLIB	generic xunit/test-discovery trap
yield-per-band-and-pig-375.md	EXMODS	family design ruling (BF yield/pig)
```
