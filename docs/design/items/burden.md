# Burden — the ore-bearing charge

**Status** live (2026-08-07)   **Mod** iiex (`IronIndustryExpanded`); the material-role registry it leans
on is exlib

**Owns** - the facts this page is canonical for:

* the burden item `iiex:burden` - its stack size, density, texture, combustible props and def;
* the `BurdenMix` stamp: which attributes are written, that they are stored as parts and read as
  fractions, and what an unstamped stack means;
* the grade bands `IiexConfig.BurdenProfiles` and the classifier `Burden.ProfileLangKey` - three bands
  on flux, why they tile, and why `offspec` is unreachable in a stock config;
* the two identity predicates `Burden.Is` (a stack) and `Burden.IsCode` (a column's material string),
  and why both must exist;
* what burn-out does to a stamp.

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| Where burden is made, and the ore : flux ratio as a machine | [burdenmaker](../machines/burdenmaker.md) |
| Why coke is charged separately, the column model, band order | [layered-charge](../layered-charge.md) |
| The tall hopper's tank, the one-material rule, the drip | [tall-hopper](../machines/tall-hopper.md) |
| What the coke fraction does - `T_in`, blast pressure, tuyere draw | [heat-balance](../mechanics/heat-balance.md) |
| Iron yielded per burden unit, per-cycle consumption, pool caps | [blast-furnace-cold](../machines/blast-furnace-cold.md) · [cupola](../machines/cupola.md) |
| The `fuel` / `flux` / `scrap` / `ironore` roles and what a fuel is | [fuels](fuels.md) |
| The extinguish residue and the salvage the player digs out | [ironmaking](../processes/ironmaking.md) |
| `1 vx³ = 2.5 u` | [density-rule](../mechanics/density-rule.md) |

**Depends on** [fuels](fuels.md) · [burdenmaker](../machines/burdenmaker.md) ·
[layered-charge](../layered-charge.md) · [blast-furnace-cold](../machines/blast-furnace-cold.md) ·
[heat-balance](../mechanics/heat-balance.md) · [recipes-config](../mechanics/recipes-config.md)

---

## Role

Burden is ore and flux, and nothing else. It is one item that carries the proportion it was made at as
stack attributes, so the ratio is decided once - at the [burdenmaker](../machines/burdenmaker.md) - and
travels with the item through splitting, stacking, a hopper tank, a charge column and a burn-out.

Coke is not in it. Fuel is charged as its own bands at the furnace
([layered-charge](../layered-charge.md)); the coke layers are the ventilation slits a premixed charge does
not have.

That leaves burden with one quality: flux. Everything else about how a heat goes is decided by how the
player charges, not by what they charged.

---

## The item

| | |
|---|---|
| Code | `iiex:burden` - the only burden item (`ItemBurden.cs:32-33`) |
| Stack | 128 (`ItemBurden.cs:41`) |
| Density | `MaterialDensity(300)` (`:42`) |
| Shape | `game:item/resource/crushed/normal`, texture code `#quartz` (`:43-44`) |
| Texture | `game:block/coal/orecoalmix` (`:33`) |
| Combustible | `burnTemperature: 600` / `burnDuration: 1500` (`:47`) |
| Held | `holdbothhands` idle + ready (`:45-46`) |
| Golden | `test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/burden.json` |

It cannot be placed as a pile by hand. `ItemBurden` is a plain `Item`. What stands in a shaft is
`iiex:furnace-chargepile`, a block the furnace owns and places
([layered-charge](../layered-charge.md)); the player fills it through the tall hopper, never by hand.

### What a stack carries

| Attribute | Type | Written by | Read by | file:line |
|---|---|---|---|---|
| `iron` | float | `Burden.Write` | `Burden.Read` | `Burden.cs:31`, `:75`, `:86` |
| `flux` | float | " | " | `Burden.cs:32`, `:76`, `:86` |
| `fuel` | float | " | " | `Burden.cs:33`, `:77`, `:86` |

`fuel` survives on the struct and is not dead. The burdenmaker always writes it as `0f`
(`BlockEntityBurdenmaker.cs:358`), but the field carries a legacy unstamped segment's assumed
carbon through burn-out (`BlockEntityShaftFurnace.cs:1276`, `:1285-1290`) and is what the shaft's own
composition read fills in for fuel bands. A stamped fuel part cannot move the grade - the bands are
flux-only.

There is no legacy `coke` attribute fallback: the burdenmaker is the only stamp writer in existence, and a
second stamping convention with no writer would only ever be satisfied by accident.

Parts in, fractions out. `BurdenMix` stores what it was given and divides on every read
(`Burden.cs:19-21`), so the stamp survives stack splitting: a half-stack carries the same parts and
therefore the same proportions. `HasContent` is `Sum > 0.0001f` (`:17`), so a stamp of all zeros is
indistinguishable from no stamp at all.

---

## Grades

### The bands — `IiexConfig.BurdenProfiles` (`IiexConfig.cs:902`)

| Order | Key | MinFlux | MaxFlux |
|---|---|---|---|
| 1 | `underfluxed` | — (0) | 0.03 |
| 2 | `standard` | 0.03 | 0.08 |
| 3 | `overfluxed` | 0.08 | — (1) |

`BurdenProfile` is `Key` + `MinFlux` + `MaxFlux` and nothing else (`IiexConfig.cs:930`).

There is no iron bound and there will not be one. Burden is ore and flux only, so
`IronFrac ≡ 1 − FluxFrac`; an iron bound would be a second knob for one quantity, and it fails silently:
edit the two out of agreement and a band simply becomes unreachable.

Caution: the bands are inclusive on both ends and scanned in list order (`Burden.cs:110`, `:117-118`),
so a boundary value belongs to the earlier band: 0.03 reads `underfluxed`, 0.08 reads `standard`. Do not
sort the list - ordering is the tie-break.

Because the three bands tile 0..1 with no gap, `offspec` is unreachable unless a player edits a hole
into their config; the classifier keeps returning it as the honest answer for that case
(`Burden.cs:113-114`).

Two statuses are returned directly rather than matched:

| Status | Condition | file:line |
|---|---|---|
| `empty` | `!mix.HasContent` | `Burden.cs:106-107` |
| `offspec` | no band matched (edited config only) | `Burden.cs:114` |

Lang keys: `iiex:burden-profile-{key}` (`assets/iiex/lang/en.json:251-255`) and the composition line
`iiex:burden-composition` (`:250`), which prints two numbers - iron and flux (`ItemBurden.cs:95-101`).

The same classifier names the grade at both ends: the burdenmaker's readout previews it before the
gate opens (`BlockEntityBurdenmaker.cs:421-426`) and the held-item tooltip prints it afterwards
(`ItemBurden.cs:102`), so "right" at the machine and "right" in the hand are one answer.

---

## How a furnace reads a shaft

`BlockEntityShaftFurnace.ReadChargeMix` (`:360-412`) walks the columns once and answers four things:

| Output | Rule |
|---|---|
| `totalMix` | everything counts - every segment's units, recognised or not |
| `mix` | volume-weighted: each segment's fractions × its units, so a mixed column reads its true average |
| `rejectedCount` | units of anything `IsChargeCode` refuses |
| `isFull` | `totalMix >= ChargeCapacityUnits` - the furnace's own geometry, not a tunable total |

Unrecognised units are counted and rejected, not skipped. Skipping them makes a shaft that reads
not full, refuses to light, and tells the player nothing. Counting them keeps fullness family-blind
(which is what the tall hopper and the HUD both assume) while `ConversionBlocked` still says a furnace
that will render nothing is burning, not melting.

There are two charge seams and they must agree. `IsChargeItem(ItemStack)` is what a hopper asks;
`IsChargeCode(string)` is what a column answers, because a `ChargeSegment` holds units of a substance,
never a stack. Override one and not the other and the same material is taken through one route and refused
through the other with nothing failing - a scrap-only cupola override that dropped its inherited
`|| IsFuelCode` stopped the cupola counting its own coke, and 192 of 193 iiex tests stayed green over it.

Composition is read per material, not per band (`BlockEntityShaftFurnace.Accumulate`, `:442-476`):

* a fuel segment contributes `units × CarbonPerUnit` - 1.0 for coke, 0.5 for charcoal, so a shaft
  charged with charcoal reads leaner and runs cooler for it. Counting the band instead would make charcoal
  a pure speed buff;
* a stamped burden segment contributes its own iron and flux shares;
* an unstamped segment is read at `BfDefaultFluxFrac` (0.05) with `iron = 1 − fuel − flux` and no fuel.

Burn-out rewrites the column (`BlockEntityShaftFurnace.cs:1242-1292`). Fuel bands lose units by a
height-interpolated retention (`BfBurnoutFuelRetainedBottom` … `Top`); burden keeps its ore and flux
verbatim and has its stamped `Fuel` scaled by the same factor; unstamped charge is stamped on the way
out, so a dead furnace cannot relight off its own salvage. That salvage is
[ironmaking](../processes/ironmaking.md)'s.

---

## Code

| Type / member | file:line | Role |
|---|---|---|
| `ItemBurden : Item, IExItemDefProvider` | `Items/ItemBurden.cs:21` | one class, one itemtype |
| ↳ `Definitions` / `Def` | `:32-33` / `:37-75` | the def; kept as a factory so the next burden-like item is a call, not a copy |
| ↳ `GetHeldItemInfo` | `:77-103` | composition + grade, in hand |
| `BurdenMix` (readonly record struct) | `Items/Burden.cs:14` | `Sum` `:16`, `HasContent` `:17`, the three fractions `:19-21` |
| `Burden` (static) | `Items/Burden.cs:29` | the whole API |
| ↳ `Is` | `:55-56` | stack identity |
| ↳ `IsCode` | `:69` | column identity - a segment is a material string |
| ↳ `Write` / `Read` | `:72-78` / `:81-87` | the stamp |
| ↳ `ProfileLangKey` | `:104-115` | the classifier |
| `BurdenProfile` | `IiexConfig.cs:930` | `Key` + two flux bounds |
| `IiexConfig.BurdenProfiles` | `IiexConfig.cs:902` | the band list; retunable live through `ModConfig/ex_values.json` |
| Tests | `test/IronIndustryExpanded.Tests/Materials/BurdenProfileTests.cs` | the three bands, the boundaries, config retuning, and that a stamped fuel part cannot move the grade |

### Every writer and reader of the stamp

| Caller | What it does | file:line |
|---|---|---|
| [burdenmaker](../machines/burdenmaker.md) | the only producer - stamps normalised `(ore/total, flux/total, 0)` on drain | `BlockEntityBurdenmaker.cs:358` |
| " | previews the same mix before the gate opens | `:421-426` |
| charge pile | writes the segment's mix back onto a stack the player digs out | `BlockEntityChargePile.cs:188`, `:256` |
| tall hopper | one grade at a time - `Burden.Read(stack).Equals(Burden.Read(_tank))` | `BlockEntityHopperTall.cs:177` |
| shaft furnace | the column read, and the burn-out re-stamp | `BlockEntityShaftFurnace.cs:360-417`, `:1242-1292` |
| furnace core | charge identity (`IsChargeItem` / `IsChargeCode`) | `BlockEntityFurnaceCore.cs` |
| the item itself | the held-item tooltip | `ItemBurden.cs:88-102` |

---

## Gotchas

1. Two batches at the same ratio read the same grade and merge. The burdenmaker is the only stamp
   writer and it normalises on drain (`BlockEntityBurdenmaker.cs:358`), so equal ratios always produce
   equal stamps. Pinned rather than assumed:
   `Two_batches_at_the_same_ratio_read_the_same_grade_and_merge`.

2. A shaft of the wrong material still lights, still burns and still burns out. Only the conversion
   is gated: real mass is burning in there, and that is why `rejectedCount` exists at all. The player is
   told through the shaft-charge HUD line and nothing else.

3. The grade is a naming layer, not a mechanic. Nothing in the tree refuses a charge for being
   under-fluxed, and nothing reads `FluxFrac` except the classifier and the composition line. The heat
   balance reads carbon at the raceway and never asks for a grade. See § Open 1.

4. `Burden.Is` and `Burden.IsCode` must move together. They are adjacent in one file for
   that reason (`Burden.cs:53-69`): one is asked of a stack and one of a column segment, and a change that
   reaches only one of them produces a machine that accepts what it will not burn.

5. `BurdenProfiles` is live config. Retuning bands changes what the machine and the tooltip say and
   nothing that either does.

---

## Open

1. Flux is inert. It is burden's only quality and it drives nothing: no yield, no slag volume, no
   melt point, no refusal. The cheapest place to make it mean something is the one the player can trivially
   fix - whether an under-fluxed charge should slag badly, or simply be refused. Undecided, and it is the
   single largest gap between what this item says and what it is.

2. `iiex:burden` has no handbook page of its own. `docs/iiex/handbook/01-orehandling.html` covers
   preparing it and charging with it, but the stamp and the bands are documented only in tooltips.

3. Roasted ore is promised and not delivered. `iiex:burdenmaker-help-addore` offers "crushed or roasted
   iron ore"; `materialroles.json` grants `ironore` to the `crushed-iron` path prefix only. Whether roasted
   ore is a distinct input with its own flux requirement or a better-yielding substitute is
   [roasting](../processes/roasting.md)'s call - see [burdenmaker § Open](../machines/burdenmaker.md).

4. The burden economy has not been re-checked since the charge scale changed. The settled anchor is
   a full cold shaft ≈ one full casting bed; the shaft is now sized by geometry (`ChargeCapacityUnits`,
   32 items a block) rather than by a threshold, and the yield figures are still marked placeholder
   upstream - see [blast-furnace-cold](../machines/blast-furnace-cold.md) § Open and
   `OreRecoveryGuardRailTests`.
