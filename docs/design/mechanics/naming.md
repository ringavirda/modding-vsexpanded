# Block & item naming

**Status** settled 2026-08-03 (N6 and N7 were added the same day) - iwex is converted apart from the
burdenmaker; the lpex/hpex/smex machine folders remain
**Mod** cross-cutting (an authoring convention). The enforcement lives in `ExpandedLib.Testing`.

**Owns**

* the seven rules N1–N7, each with the failure that produced it;
* the verified inventory of every block code in the suite against its asset path, and which of the
  two is wrong where they disagree;
* the one legitimate exception to N1 - the molten canal, which is two variant grammars on one code;
* why N6 exists at all: hierarchical names make an `ExRecipeCosts` hazard reachable that flat names were
  accidentally safe from;
* the rule that bounds the wave - migrate only what shipped - and where the shipped set is recorded.

**Does not own** - cited only: the orientation tokens themselves and how they rotate
([orientation-schemes](orientation-schemes.md)) · where a def, a recipe provider or an asset tree lives
([conventions.md](../conventions.md) § the per-mod project skeleton) · the cost catalogue's contents
([recipes & config](recipes-config.md)) · the layout DSL that consumes these codes
([layouts](../../internal/workbench/layouts.md)).

**Depends on** [orientation-schemes](orientation-schemes.md) - N4 is its who-decides-the-orientation table,
narrowed to a spelling question.
**Depended on by** block renames & codegen
· [layouts](../../internal/workbench/layouts.md) - a layout pins codes, so it can only be authored once the codes are final.

---

## Role

A block code is written down in seven places: the definition, the asset path, the golden, three lang files
and every layout that pins it. Nothing checks that those seven agree about anything except existence.

The rules below make one string serve as the code, the file path and the lang key, so that
knowing any one of them tells you the other two. The wave is happening now because layouts pin codes, and
the iwex bring-up needs layouts authored against names that will not move again.

This page is a convention, not a description of the tree. The inventory below is the work list.

---

## The rules

### N1 — the rendered code **is** the asset path

> A block's fully-rendered code, with `-` put back to `/`, is the path of its blocktype file.
> `iwex:slag-pathslab-free` lives at `blocktypes/slag/pathslab.json`.

Stated exactly enough to test - the asset path with `/` → `-` must equal either:

* the def's base code (`crafting/designtable` → `crafting-designtable`, `slag/pathslab` →
  `slag-pathslab`), or
* the base code plus one state of its first variant group, when that group names the member
  (`pipe/straight` → `pipe` + `straight`; `mpenergy/bevel` → `mpenergy` + `bevel`).

Both forms are legal and which one applies is not a style choice: a block whose `type` group names its
member takes the second, and one whose `type` names a sub-kind takes the first. `slag-pathslab` and
`crafting-designtable` are the first form; `pipe` + `type(straight)` is the second.

Caution: the two forms may not be mixed inside one family - see N7. `mpenergy` was briefly written that
way (shaft and bevel sharing `mpenergy`, the flywheel owning `mpenergy-flywheel`) and it is precisely the
shape that lets a family wildcard escape its family. Within a family, pick one form for every member.

One legitimate exception, and it is not the material skin it looks like. The molten canal ships two
defs per shape at `molten/canal/brick/straight` and `molten/canal/cobblestone/straight`, both with code
`molten-canal`. They are two defs rather than one because they declare different variant groups -
`brick(fire|black|…)` against `rock(from block/rockwithdeposit)` (`BlockMoltenCanal.cs:69-103`) - and an
asset path must be unique per def (`ExBlockDef.Location`, `ExBlockDef.cs:70-73`). One code, two grammars,
therefore two files, and the skin segment is the only place the distinction can live. The rendered
codes still obey N1 in shape (`molten-canal-straight-fire-ns`, `molten-canal-straight-granite-ns`), and a
wildcard covers both because both render four segments.

The skin segment is an infix, not a suffix - path `molten/canal/brick/straight` against code prefix
`molten-canal-straight`. Any N1 test must allow it explicitly; it cannot fall out of a prefix check.

A block that must keep a `type` group but has no sub-kind is forced into the second form.
`BlockNetworkNode` keys `AllowedOrientations` by `Variant["type"]`, so a node cannot drop the group; giving
it its own code as well would stutter (N3). Sharing the family code is the only spelling left - which is
why `pipe`, and now `mpenergy`, look the way they do.

### N2 — family first, member second, and the family is singular

> `pipe/straight`, not `pipes/straight`. `hopper/tall`, not `hopper-tall` filed under `furnaces/`.

The family segment is what a player and a wildcard both reach for. Singular because the code is one block,
not a collection: `iwex:pipe-straight-ns` reads correctly and `iwex:pipes-straight-ns` does not.

smex already does this right and is the model: `converter-intake`, `cowperstove-intake`,
`smokestack-intake` are three families each naming their own member, and the shared member word costs
nothing because the family disambiguates it.

### N3 — no stutter between a code and its own `type` state

> If the code and the first `type` state say the same word, the code is wrong, not the variant group.

The `type` group cannot simply be deleted. `BlockNetworkNode` reads it into `Type`
(`BlockNetworkNode.cs:45`) and `AllowedOrientations` is keyed by it (`:201`, `:712`), so a node with no
`type` has no valid orientations and cannot be placed. The fix is to widen the code to the family and
leave `type` naming the member - the grammar the pipes already use.

All five stutters are gone - `mpenergy-shaft`, `mpenergy-bevel`, `forming-rollingmill`,
`furnace-tuyere`, `furnace-twintubblower`.

### N4 — `side` when the player decides, `orientation` when the network does — and both spell letters

> A player-placed facing is a `side` group; a self-orienting network node is an `orientation` group.
> Both carry single-letter tokens - `n`, `e`, `s`, `w` for a side; `n`, `ns`, `uwe`, … for an
> orientation. The group name says who decides; it no longer says how the facing is spelled.

The test is who decides, not what the block is. A network node re-picks its own orientation whenever
a neighbour changes (`BlockNetworkNode.OnNeighbourBlockChange` → `RecalculateAndSyncOrientations`), so its
variant is an output of the world; an `ExOrientable` block's facing is an input from the player
and never moves again. Two different things take two different group names, but not two different
vocabularies.

#### The two spellings were merged on 2026-08-04, and only letters compose

`ExBlockDef.SideVariant()` declares the four states explicitly as letters, and the blocks carry
`ExOrientable` rather than vanilla's `HorizontalOrientable`.

Letters win because only letters compose. A bend is `nw`, a tee `uns`, a cross `nswe`, and there is
no word form of any of those - so a mod that keeps both spellings needs every helper to carry an
`asLetter` flag and infer it from `side.Length == 1`. It did, and the inference leaked: a rig could hold
`furnace-tuyere-n` and `furnace-irontap-west` in adjacent constants, and `BlockFacing.FromCode` returns
null for a letter, which is how `TryPourMetal` came to drain nothing forever with no error.

The two behaviours are coupled and cannot be split. Vanilla's `HorizontalOrientable` builds the
placed code with `CodeWithParts`, which keeps only the first dash-segment, so it cannot place a block
that also has a `type` or `tier` group. Respelling the states without swapping the behaviour crashes the
client; swapping the behaviour without respelling leaves the letters unproduced.

#### The one surviving word-spelled facing

A group sourced from vanilla's `abstract/horizontalorientation` worldproperty keeps the game's words,
because the states live in the game's assets. Exactly two blocks are in that position -
`slag-brickstairs` and `slag-pathstairs`, which inherit vanilla's stair grammar
(`…-up-north-free`). A repo-wide respelling hits them, and the failure is silent both ways: a wildcard
selector still matches, so only a concrete reference breaks. Two recipe outputs were rewritten to
`-up-n-free` and shipped stairs nobody could craft; `{Mod}RecipeOutputTests` now fails on that class.

Reach the spelling through `IwexBlocks.<Block>.WithSide(BlockFacing)` (or `WithHorizontalorientation`)
rather than typing it. The emitter reads the def's own states, so the caller states a direction and the
generated table picks the vocabulary.

#### Released codes are a separate question

ppex shipped its `side` groups as words (`ppex:boilercornish-north`) and smex shipped everything as
letters (`smex:blastfurnacetap-n`). A migration's old code is a historical fact, so `CodeRelocation.Remap`
takes `legacySideWords` per row and the ppex rows set it. Without it 32 released codes lose their path to
a live block and a player's boiler stops loading, with only a `Logger.Warning` to show for it.

The same change made `CowperStoveIntakeOrientationMigration` circular - it rewrote `-n` → `-north`,
so after the respelling it claimed a live code and pointed at a dead one. Deleted; see
`BrickVariantMigration`'s note for why nothing is owed.

### N5 — `-` joins segments; never a word break inside one

> `designtable`, not `design-table`. `hopper-tall` is legal - because `hopper` is a family and `tall`
> is its member, not because "hopper tall" is two words.

The separator carries meaning: every `-` in a code is a boundary between a family, a member and the
variant groups. Compound words are squashed, not hyphenated - `rollingmill`, `sandcastingbed`,
`millaxle`, exactly as vanilla writes `claybricks` and `metalplate`. Spending a `-` on a word break destroys
the boundary meaning, and it is what makes `blastfurnace-core` and `hopper-tall` unparseable as a pair -
one is a two-word name, the other is a family and a member, and they are spelled identically.

The corollary is a real finding: iwex's `hopper-tall` is spelled right and smex's `hopperbell` /
`hopperreinforced` are spelled wrong. They are one family split across two mods and two spellings.

### N6 — a path segment may only become a folder if it is not itself a block

> `slag/pathslab`, not `slag/path/slab`, because `slag/path` is a block.

`ExRecipeCosts` applies every catalogue entry in sequence - `foreach (var entry in catalogue.Values)` at
`ExRecipeCosts.cs:218` (`Apply`), and again at `:42` and `:78` for extraction - rather than taking the
first match. Two selectors hitting one recipe means the later entry overwrites the earlier's costs, in
dictionary enumeration order, which is not a contract.

Flat names were accidentally safe. `iwex:slagpath-*` could not match `slagpathslab-free`: the
character after `slagpath` is `s`, not `-`. Hierarchical names remove that accident -
`iwex:slag-path-*` matches `slag-path-slab-free` exactly as intended. The hazard is created by
adopting N1/N2, so the fix has to be structural rather than a patched selector.

Vanilla agrees: `game:claybricks`, `game:brickslab` - name suffixes, not folders.

### N7 — no base code may be a prefix of another at a `-` boundary

> Within a family, either every member lives in the `type` variant under one shared code, or none does.
> A half-and-half family is what produces the collision.

N6's code-level twin. N6 is about folders; this is about the codes
themselves, and it bites a construct N6 never sees: `SomeFamily.Code + "*"`, the idiom for "any member of
this family". That wildcard is correct exactly as long as no other block's code starts with the same
string - and `IwexCodes.Tuyere` and `IwexCodes.MoltenMetalTap` are both built that way and both fed
straight into multiblock `Legend`s.

The failure lands in a layout. Give the tuyere base code `furnace` while a charge door has
`furnace-chargedoor`, and `iwex:furnace*` accepts a charge door in a tuyere cell. The structure
completes wrong, every code involved is real, every block resolves, and nothing fails.

Like N6 this is a boundary rule, not a substring rule - `slag-path` and `slag-pathslab` are safe
because the next character is `s`, not `-`. Same accident, same reason not to lean on it.

Found live, in work done under this convention. Family 3 gave shaft and bevel the shared code
`mpenergy` while `mpenergy-flywheel` and `mpenergy-transmission` kept their own - a textbook half-and-half
family. Fixed by moving the flywheel's size into a `size` group and the transmission's gearing into
`kind`, freeing `type` to name the member: every rendered code came out byte-identical, so no golden
code, lang key, recipe or layout moved. The whole change was in how the def is authored.

Enforced by `CodePrefixCollision` + `{Iwex,Lpex,Hpex,Smex}CodePrefixTests` - one per mod, because a family
does not cross a domain.

---

## Inventory — every code against its asset path

Read off the generated tables (`src/*/Generated/*Blocks.g.cs`), which are emitted by running the
definitions, so this is what actually ships rather than what the call sites appear to say. "ok" = the base
code already equals the path with `/` → `-`.

### iwex

| Code | Asset path | Breaks | Family / when |
|---|---|---|---|
| `slag-block` `slag-bricks` `slag-brickslab` `slag-brickstairs` `slag-path` `slag-pathslab` `slag-pathstairs` | `slag/*` | ok | done 2026-08-03 |
| `crafting-designtable` | `crafting/designtable` | ok | done 2026-08-03 |
| `mpenergy` + `type(shaft\|bevel\|flywheel\|transmission)` | `mpenergy/{shaft,bevel,flywheel,transmission}` | ok | done 2026-08-03; re-cut for N7 the same day |
| `forming` + `type(rollingmill\|millaxle)` | `forming/rollingmill` · `forming/millaxle` | ok | done 2026-08-03 |
| `casting-sandbed` · `casting-sandcell` · `casting-mold` | `casting/sandbed` · `…/sandcell` · `…/mold` | ok | done 2026-08-03 |
| `molten-canal` | `molten/canal/{brick,cobblestone}/{type}` · `molten/canal/tap` | keeps the N1 skin exception | done 2026-08-03 |
| `molten-barrel` | `molten/barrel` | ok | done 2026-08-03 |
| all 14 furnace parts - one code `iwex:furnace` + `type(blastcore\|cupolacore\|heatingcore\|puddlingcore\|chargedoor\|puddlingchargedoor\|chargepile\|heatinghearth\|puddlinghearth\|puddlingchimneycap\|irontap\|slagtap\|tuyere\|twintubblower)` | `furnace/{type}` | ok | done 2026-08-03 |
| `hopper-tall` (code unchanged) | `hopper/tall` | ok | done 2026-08-03 - left the furnace folder; N4 fixed with it |
| `hearthmetal` + `metal(pigiron\|castiron)` | *(root)* | ok | done 2026-08-07 - `solidifiediron` / `solidifiedcastiron` merged into one block with the metal as a variant; migrations cover both old codes and the released `smex:solidifiediron` (`HearthMetalMigration.cs`) |
| `burdenmaker` | `ore/burdenmaker` | N1, N2 | open - shipped after the sweep with a bare code against a family folder; `ore-burdenmaker` is the conformant spelling |
| `pipe` | `pipe/{straight,bend,tjunction,xjunction}` | ok | done 2026-08-03 - all 3 mods at once |

### lpex · hpex · smex · exlib

| Code | Asset path | Breaks |
|---|---|---|
| `lpex:pipe` · `hpex:pipe` | `pipe/*` | ok - done 2026-08-03, moved atomically with iwex's |
| `lpex:steamcondenser` | `steamcondenser` | ok - done 2026-08-03, it is not a pipe, so it left the folder |
| `lpex:boilercornish` · `hpex:boilerlancashire` | `boiler/cornish` · `boiler/lancashire` | N1 |
| `lpex:enginewatt` `enginefluidpump` `enginempgenerator` · `hpex:enginecornish` | `engine/*` | N1 |
| `lpex:manualfluidpump` | `manualfluidpump` | N1 ok, N2 |
| `smex:blastfurnacecore` | `blastfurnace/core` | N1 |
| `smex:hopperbell` · `hopperreinforced` | `blastfurnace/hopperbell` · `…reinforced` | N1, N2, N5 - same family as iwex's `hopper-tall` |
| `smex:converterbessemer` `convertercontrol` `convertertransmission` · `converter` | `converter/{bessemer,control,transmission,intake}` | N1 |
| `smex:cowperstoveheatsink` · `cowperstove` | `cowperstove/{heatsink,intake}` | N1 |
| `smex:engineairblower` · `smokestack` | `engine/airblower` · `smokestack/intake` | N1 |
| `exlib:structurefiller` | `structurefiller` | ok - the only code in the suite that is already fully conformant and has no family |

### The family is the asset folder, singularised

> The family is the folder the blocks already live in, singularised.
> `casting/` → `casting-sandbed`. `ore/` → `ore-burdenmaker`. `forming/` → `forming-rollingmill`.
> `pipes/` → `pipe-straight`. `furnaces/` → `furnace-tuyere`. `molten/` → `molten-canal`.

It agrees with the block-renames plan's own proposals (`furnace-tuyere`, `forming-rollingmill`) and it is
not a material or a class name. That distinction cost one deviation already: the plan sketched
`castiron-shaft` / `castiron-bevel`, but `castiron` covers only two of the four mpenergy blocks - the
flywheel and the transmission are cast iron too and are not named for it. A modifier two-thirds of a family
carries is not a family. `mpenergy` is.

A one-block family sits at the asset root with no folder, and that is correct rather than a gap -
`exlib:structurefiller`. What Open #4 warns about is the opposite case: a block at the root that does
have siblings it should be filed with.

### Traps a rename sweep must survive — found doing `molten`, and general

The molten sweep touched 42 files across four mods. Five classes of string look exactly like the block
code and must not move.

| Looks like | Actually | Must |
|---|---|---|
| `smex:moltencanal-*`, `smex:moltenbarrel`, `smex:moltencanalstart-ns` | released codes - the migration sources | never rewrite; rewriting them silently deletes the migration |
| `moltencanals-title` / `moltencanals-text` | handbook topic keys (plural) | leave - not a block code |
| `iwex:moltenbarrel-info-{empty,content,units,units-state}` | block-info UI strings | leave |

The two a `smex:`-prefixed textual guard cannot see:

| Missed form | Where | Why the guard failed |
|---|---|---|
| `new AssetLocation("smex", "moltenbarrel")` | `BarrelConstructionMigration` | the domain is a separate argument - the string `smex:moltenbarrel` never appears |
| `("moltencanal", "molten-canal")` | `SmexToIwexMigration.Relocated` | a bare tuple of base codes; the left side is the shipped smex code |

A migration source is a historical fact, not a current code. Rewriting one leaves the migration
internally consistent and matching nothing: every released block is orphaned and no test fails, because
nothing in the suite knows what the old world actually contains except `ReleasedCodes` - and that file's
strings are `smex:`-qualified, so they survived the sweep and the two rewritten sources simply stopped
agreeing with them silently.

The check that catches it: count `smex:`-qualified occurrences before and after and assert equality
(that caught nothing here), then read every `AssetLocation(domain, code)` and every migration table by
hand. The domain-qualified form is the only one a textual guard can see.

### An unstated invariant that already holds everywhere

The orientation group is always last. Across all five generated tables there is not one def where a
variant group is declared after `side` or `orientation`. Nothing enforces it, and the released ppex codes
show it was once broken the other way (`ppex:pipe-straight-ns-iron` - metal after orientation, while
`ppex:pipe-outlet-black-d` put brick before it, in the same mod). It is free to pin as a test today; see
Open #2.

---

## Migrations — only what shipped

> A code that never escaped needs no migrator, and writing one is worse than useless: it reads like
> coverage while claiming a code nothing placed.

The shipped set is recorded as data in `test/ExpandedLib.Testing/ReleasedCodes.cs`, extracted from
`dist/Releases/1.22.0` by `scripts/gen-released-codes.py` rather than from memory - 501 concrete codes
across three domains.

Only `exlib`, `ppex` and `smex` have ever shipped. There has never been a released `iwex`, `lpex` or
`hpex` build, so every block added after those splits carries no migration debt at all. This turns
a 60-code rename wave into roughly 45 paths of real migration work, and it is why the `slag` family
needed one migration row for seven renames.

Two guards hold the contract, both in `test/HighPressureExpanded.Tests/Migrations/ReleasedCodeCoverageTests.cs`
(there because it is the only suite that references all five mods, and a migration chain crosses mods by
design):

* every released code still reaches a live block;
* no migration claims a code that is still alive - the general form of the bug where 30 live
  `lpex:pipe-*` blocks were declared as migration sources and silently converted.

---

## How each rule is enforced

| Rule | Enforced by | Strength |
|---|---|---|
| N1 | — | nothing. See Open #1 |
| N2 | — | nothing; a reading rule |
| N3 | — | nothing, but the generated table makes it visible: the summary line prints code and path together |
| N4 | — | nothing |
| N5 | — | nothing |
| N6 | `CostSelectorOverlap` + `{Mod}CostSelectorTests` | real, and mutation-proven: re-pointing `slagpathslab-grid` at `iwex:slag-path-*` fails with both selectors and the colliding codes named |
| the wave itself | `ReleasedCodeCoverageTests`, `BlockCodeEmitter` drift tests, `LangCoverage`, `CodeLiterals`, goldens | five independent nets; a rename that drops any one of code / lang / literal / golden / migration fails |

---

## Gotchas

1. A code can be a block and an item. `iwex:slag` was both; a scripted `"iwex:slag"` →
   `"iwex:slag-block"` across the block's golden also hit its `smeltedStack`, which references the
   item. Renaming a block never implies renaming the item of the same name - decide each separately and
   let the golden catch the overreach.

2. Recipe-cost catalogue keys are player config and must not be renamed with the code. They are
   persisted in the user's config file; changing a key orphans their tuning. Re-point the entry's `Match`
   selector and leave the key alone (`IwexRecipeConfig`).

3. Never regenerate goldens wholesale. `EXLIB_WRITE_GOLDENS` rewrites an entire domain, and the
   working tree carries ~110 hand-blessed uncommitted goldens. Move and patch them by hand. The scoped
   `EXLIB_WRITE_BLOCKCODES=1` is safe - it writes only `{Mod}Blocks.g.cs`.

4. Textures are not block codes. `assets/{domain}/textures/block/slag/*` stayed put through the slag
   rename. Aligning texture paths is optional and was excluded to hold the diff to blocktypes.

5. The pipe family cannot be renamed one mod at a time. `BlockPipe.Segments(domain)` is shared, so
   iwex, lpex and hpex must move in one change or the joint families disagree mid-wave.

6. Lang wildcards must be re-spelled with the code. A key like `block-slagpath-*` becomes
   `block-slag-path-*`, and `Lang.GetMatching` has no dash-stripping fallback - a near-miss produces a
   raw key on screen, in three locales. `LangCoverage` catches the missing-name case; it cannot catch a
   wildcard that matches nothing.

---

## Open

1. N1 has no test and is the rule most worth one. `DefinitionCodes` already expands defs to their
   codes and `IExDef.Location` already carries the asset path - the check is one `Assert` over
   `base code.Replace('-','/') == assetName`, plus an allow-list of one for the canal. Cheap, and it would
   convert the inventory above from a work list into a red build that shrinks as families land.

2. Pin "the orientation group is last." It already holds across all five mods (see above), so the test
   is green on the day it is written and stops the released-ppex mistake from recurring.

3. The two design-renames scheduled beside this convention are both done. `moltenmetaltap` became
   two blocktypes, `furnace-irontap` and `furnace-slagtap`, with the smex migration mapping every
   shipped tap onto the iron one (it cannot know which a given block was); and `solidifiediron` /
   `solidifiedcastiron` merged into `hearthmetal-{metal}` (2026-08-07). Neither needs re-doing in a
   family sweep.

4. `manualfluidpump` is N1-conformant by accident: it sits at the asset root with
   no family folder, so code and path trivially agree. N2 says it belongs under a family, which will
   break its N1 conformance until the code moves with the path. Do not read an "ok" in the inventory as
   "nothing to do". (`castmold` and `rollingmillaxle` were in this group and are now genuinely fixed.)

5. Whether the burdenmaker takes `ore-burdenmaker`. It is the one iwex family straggler; the code
   would move to match its `ore/` folder, and nothing shipped depends on the bare spelling.

6. Whether `type` should ever equal the member folder. The pipes make member-as-`type` work
   (`pipe/straight` + `type(straight)`), and N3 forbids member-as-code. What is not settled is whether a
   family with exactly one member should carry a `type` group at all - `tuyere` and `twintubmpblower` only
   have one because `BlockNetworkNode` requires it, which is a mechanism leaking into a name.
