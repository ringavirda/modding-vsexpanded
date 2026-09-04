# Roasting (calcining)

**Status** designed - nothing is built. No item, no furnace mode, no recipe, no config key, no test, no lang
string. The only thing in `src/` that names a roasted ore is a foreign mod's item code, registered by a
compat shim
**Mods** iiex - it would live entirely on the reheat furnace and the burdenmaker; no other mod participates

**Owns** - the facts this page is canonical for:

* the roasting loop as designed, step by step, with the build status of every step;
* the evidence that it is absent - what a repo-wide search does and does not find;
* what roasting is for in this mod's economy: a fuel lever whose throughput effect is emergent and
  second-order, and which the design forbids giving a direct one;
* the two rules that keep it from being a trap - roasted ore must still bloom, and the burdenmaker accepts
  both - and the deadlock each one avoids;
* the substrate problem: the mod's ore is an oxide, so two of roasting's three real mechanisms have nothing
  to act on here;
* the exact seam in the heat balance where roasting would attach (`BfChargeLossFull`), and why that is the
  honest place rather than a bonus multiplier;
* the `roasted-crushed-iron` compat registration - what it is, and what it is not.

**Does not own** - cited only, never restated:
[reheat furnace](../machines/reheat-furnace.md) - the machine that would host it: the multiblock, the firebox
charge override, the hearth rows, and roasting mode as its Open #8 ·
[burdenmaker](../machines/burdenmaker.md) - the hopper that would accept it, `MaterialRoleRegistry`, and the
1:1 ore → burden conversion ·
[metal recovery](../mechanics/metal-recovery.md) - the recovery ladder (50 / 85 / 92 %), the anchor
derivation, and the guard-rail invariant ·
[heat balance](../mechanics/heat-balance.md) - `T_process`, every heat-in and heat-out term, the FSM ·
[cold blast furnace](../machines/blast-furnace-cold.md) - what a burden does once charged ·
[ironmaking](ironmaking.md) - the loop this would insert a step into ·
[coking](coking.md) - the fuel it is supposed to save ·
[burden](../items/burden.md) - the flux bands ·
[recoverability](../mechanics/recoverability.md) - the ≤ 32 / ≤ 48 hearth invariant that constrains the
machine · [conventions](../conventions.md) § Shared simulation model - which tier moves which number, and
the fuel-saving placeholder

---

## What it is

**Calcining**: heat ore in air, below its melting point, until it stops giving anything up. It is not
smelting - nothing is reduced and no metal appears. Three things happen:

| Mechanism | Real effect | Has a substrate in this game? |
|---|---|---|
| Free and combined water driven off | limonite is ≈ 14 % water by mass; carrying it into a furnace costs the heat to boil it | no - see below |
| CO₂ driven off from carbonates | siderite is FeCO₃; the CO₂ is 38 % of the mineral | no |
| Sulphur driven off from sulphides | pyrite roasts to oxide + SO₂; sulphur makes iron red-short | no |
| Porosity - the ore cracks and opens up | reduction gas reaches the interior, so reduction is faster and more complete | yes, and it is metal-blind |

The payoff is less coke and a faster melt (historically 10–20 % fuel), plus a modest recovery bump, because
more reducible ore leaves less unreduced FeO in the slag
([conventions](../conventions.md) § Shared simulation model).

### The substrate problem — the mod's ore is an oxide

Vanilla's `crushed-iron` is textured as hematite
(`.game/1.20/assets/survival/itemtypes/resource/crushed/crushed.json:26`) - Fe₂O₃, an anhydrous oxide. There
is no water in it, no carbonate, and no sulphide anywhere in the vanilla iron chain. Vanilla's crushed-ore
family has one iron entry (`:6`), and even the mod-gated compat codes are all oxides - `crushed-hematite`,
`crushed-magnetite`, `crushed-ore-limonite` (`Compat/IronOreCompat.cs:39-49`).

So of the four mechanisms above, only porosity has anything to act on.

Settled 2026-08-05: porosity alone - the mod works with vanilla ores only. No limonite, no siderite, no new
ore item. Roasting is a flat, metal-blind fuel-and-recovery bonus acting on porosity, and it ships with the
ruled recovery ladder unchanged - 8.5 → 9.2 u/nugget ([metal recovery](../mechanics/metal-recovery.md)).

The water/carbonate half is not modelled because vanilla supplies no substrate for it. Open: making roasting
(and the burden grades) react sensibly to ores this mod did not ship is a compatibility concern to take up
later, and the natural home for anything limonite-shaped if a third-party ore ever supplies one. Do not
pre-build the variety axis for it.

Roasting is also not a sulphide process here: there is no sulphide iron ore in vanilla or in the mod, and the
mod adds no sulphur chemistry (sulfur and acids are on the Homestead side of the cut -
[scope](../scope.md)).

---

## The loop

Designed. Every step after the first is unbuilt.

| # | Step | Machine | Player verb | Out | Built? |
|---|---|---|---|---|---|
| 1 | crush ore | vanilla | crush | `game:crushed-iron` | vanilla |
| 2 | charge the hearth | [reheat furnace](../machines/reheat-furnace.md) | RMB the hearth row with crushed ore | — | no - the hearth accepts stock only, matched by code prefix (`HeatingHearthLayout.cs:64-79`) |
| 3 | fire the firebox | same | place fuel, light it | flame drawn over the hearth | yes - the shell burns and holds heat |
| 4 | roast | same | wait | `iiex:crushedore-roasted` | no - no roasting mode, no timer, no product |
| 5 | draw | same | RMB the hearth row | roasted ore | no |
| 6 | combine | [burdenmaker](../machines/burdenmaker.md) | load the wide hopper, as raw ore | burden, carrying the benefit somehow | no - nothing distinguishes it |
| 7 | melt | [blast furnace](../machines/blast-furnace-cold.md) | — | less coke per unit of pig | no - no term exists |

### The evidence that it is absent

| Search | Result |
|---|---|
| `iiex:crushedore-roasted` in `src/` or `assets/` | no hits anywhere in the repo |
| a roasting mode, timer, temperature or state on any furnace | none - `BlockEntityHeatingFurnace.cs:24-27` says so in its own class doc: *"neither is the roasting mode that will share this machine"* |
| a `Roast*` config key | none in `IiexConfig.cs` |
| a lang key for a roasted ore | none in `assets/iiex/lang/en.json` |
| a test | none |
| anything in `src/` naming a roasted ore | the compat row (`Compat/IronOreCompat.cs:41` - see § Gotchas) and the burdenmaker's own help text and doc-comments, which already offer *"crushed or roasted iron ore"* (`BlockBurdenmaker.cs:198`) |
| design mentions | [conventions](../conventions.md) § Shared simulation model (the model), two furnace doc-comments (`BlockHeatingFurnaceCore.cs:15`, `BlockEntityHeatingFurnace.cs:26`), and the fettle route it would displace (`FettleRecipeDefinitions.cs:22-24`) |

---

## Inputs and outputs

| | | Status |
|---|---|---|
| **In** | crushed iron ore - any item in `Roles.IronOre` (`assets/iiex/config/materialroles.json:11`, prefix `crushed-iron`, plus the mod-gated codes at `IronOreCompat.cs:39-49`) | the role exists |
| **In** | firebox fuel - the reverberatory firebox takes coke, bituminous, anthracite and charcoal and refuses lignite (`BEBehaviorFirebox.IsFuel`; the override is [reheat furnace](../machines/reheat-furnace.md)'s) | live |
| **Out** | `iiex:crushedore-roasted` - no new art: vanilla's generic crushed-ore shape, retextured | does not exist |
| **Out** | must also carry `combustibleProps` / `smeltedStack` so it still blooms | does not exist - and see the warning below |
| **Out** *(second job)* | roasted oxide as fettle - "bull dog", the historically standard British fettling (`FettleItemDefinitions.cs:74`, `FettleRecipeDefinitions.cs:22-24`) | the hand-prepared grid recipe is the interim route |

### The "never a trap" guarantee is shipped by the wrong mod

The rule is that roasted ore must still smelt in a vanilla bloomery, via the same
`combustibleProps`/`smeltedStack` trick smex already applies to `crushed-iron`. That trick works, but it
lives in smex, not iiex:

```
assets/siex/patches/vanilla/crushed.json:3-10
  addmerge /combustiblePropsByType/*-iron
    meltingPoint 1482 · meltingDuration 30 · smeltedRatio 20 · smeltedStack game:ironbloom
```

With iiex alone, crushed iron ore is not bloomable at all and the guarantee the design leans on is not
present. If roasting lands in iiex, the patch (or an equivalent) has to land in iiex too, or the trap the
rule exists to prevent is moved one mod upstream.

---

## Numbers

The ladder is [metal recovery](../mechanics/metal-recovery.md)'s and is cited, not restated; the fuel-saving
figure is a stated placeholder.

| Quantity | Value | Owner / source | Status |
|---|---|---|---|
| bloomery recovery (the anchor everything is measured against) | 50 % → 5 u/nugget | [metal recovery](../mechanics/metal-recovery.md) | vanilla, untouched |
| blast furnace on raw ore | ~85 % → 8.5 u/nugget | [metal recovery](../mechanics/metal-recovery.md) - `BfIronPerOreUnit` | live |
| blast furnace on roasted ore | ~92 % → 9.2 u/nugget | [metal recovery](../mechanics/metal-recovery.md) | designed - no key exists |
| fuel saving | 10–20 % | [conventions](../conventions.md) § Shared simulation model | placeholder, historical |
| roasting's primary axis | fuel down | same | settled |
| roasting's secondary axis | small recovery up | same | settled |
| roasting's forbidden axis | throughput | same | settled |
| the decisive number | *coke spent roasting < coke saved downstream* | same | undecided - and it is the whole feature |

### The throughput question, answered honestly

Roasting is not a throughput lever: throughput is listed under `deliberately not` for the roasting row.
Recovery is a bounded resource spent once - on the blast furnace's gap over the bloomery - and every tier
after that has to compete on axes with no ceiling
([conventions](../conventions.md) § Shared simulation model).

Roasting does have a throughput effect, and it arrives by itself:

```
less dead mass in the charge  →  less heat spent on it  →  higher T_process
      →  larger margin over the melt point  →  higher melt-speed factor  →  more pig per second
```

That is [heat balance](../mechanics/heat-balance.md)'s melt-speed term, not a roasting bonus. The rule:
give roasting no direct throughput number; let the heat balance hand it whatever throughput falls out.
Roasting acts on the charge, hot blast acts on the furnace (`T_in`) - different terms of the same equation,
so they stack without either becoming redundant, and hot blast is much the larger of the two.

### The seam it attaches to already exists

There is exactly one heat-out term that means what the cold charge costs:

| Key | Value | file:line | Doc |
|---|---|---|---|
| `BfChargeLossFull` | 310 °C | `IiexConfig.cs:300` | *"Heat loss (°C) from cold charge mass with the furnace loaded to its capacity (`ChargeCapacityUnits`) … it is linear: half-loaded pays half."* |

Roasted ore should reduce that term and nothing else. It is the physically correct place - roasting removes
mass the furnace would otherwise have to heat and decompose - and it needs no new mechanic, since the model
already scales that loss by how full the shaft is. It produces the fuel saving, the faster melt and nothing
else. The term is owned by [heat balance](../mechanics/heat-balance.md); the claim that it is roasting's
correct attachment point is this page's. The recovery half has its own seam: `BfIronPerOreUnit` is live, and
the roasted rung is one key beside it ([metal recovery](../mechanics/metal-recovery.md)).

---

## Why it is like this

1. Roasted ore is its own item, not a burden attribute. Roasting precedes combining, so it is a pipeline
   stage; by the time a burden exists the ore has been proportioned away into a composition and there is
   nowhere to hang a per-ore flag ([conventions](../conventions.md) § Shared simulation model).

2. It must still bloom, or it is a trap. A player who roasts a stack and then decides to make a bloom must
   not discover that they have destroyed their own ore. Hence the `combustibleProps` route - and hence the
   warning above that the patch currently lives in the wrong mod.

3. The burdenmaker accepts both, and roasting is never mandatory. Making roasted ore compulsory would gate
   iron behind cast iron: the reheat furnace needs cast plate to build, and it is the machine that roasts, so
   the player would need iron to make iron. A deadlock, avoided by one word: *optional*.

4. The machine already exists and needed no invention. A calciner is a reverberatory furnace: flame drawn
   across a bed of material, no contact between fuel and charge, no blast. That is the
   [reheat furnace](../machines/reheat-furnace.md)'s geometry, which is why the same building serves both
   (`BlockHeatingFurnaceCore.cs:15`). The shared furnace core already supports a non-melting machine - a
   furnace with no pool overrides nothing and inherits truthful defaults.

5. One furnace, two roasts. The same machine's other roasted product is fettle - roasted tap cinder and mill
   scale, the "bull dog" that lines a puddling hearth. Today that is a hand-prepared grid recipe explicitly
   marked interim: *"Once the heating furnace can roast, roasting should become the better route, leaving
   this hand-prepared craft as the fallback"* (`FettleRecipeDefinitions.cs:22-24`). Roasting mode pays for
   itself twice, and the second payoff closes a waste loop.

---

## Gotchas

1. A roasted ore is already accepted by the burdenmaker today, and it does nothing.
   `Compat/IronOreCompat.cs:41` registers `roasted-crushed-iron` into `Roles.IronOre` - but only when
   IndustrialStory is loaded (`:37`), and it is that mod's item. A player with IndustrialStory installed can
   feed roasted ore into the burdenmaker right now, it converts 1:1 like any other ore, and the heat balance
   never learns it was roasted. The registration is correct as compat and is not an implementation of this
   page.

2. The host machine cannot complete its structure. The reheat furnace is a shell whose multiblock, part
   blocks and hearth rows are built and tested but which cannot currently be completed in game
   ([reheat furnace](../machines/reheat-furnace.md) § Status). Roasting mode has nowhere to run until that is
   fixed.

3. The hearth recognises stock, not ore. `HeatingHearthLayout.StockOf` matches a fixed set of code prefixes -
   `stock-shingledbar`, `stock-shingledslab`, `castbillet`, `castbloom`, `castslab` (`HeatingHearthLayout.cs:64-79`) - and
   `TryLoad` refuses anything else (`BlockEntityHeatingHearth.cs:57-69`). Roasting needs a second contents
   model on the same hearth, or a different volume entirely. The hearth's 3 × 2 footprint is also where the
   ≤ 32 / ≤ 48 handling limits come from ([recoverability](../mechanics/recoverability.md)), so it is not a
   free surface to redesign.

4. The firebox does not enforce coke. `BEBehaviorFirebox.IsFuel` takes coke, bituminous, anthracite or
   charcoal, so the decisive number - *coke spent roasting vs coke saved* - is really a fuel cost, and a
   roast can run on the cheap coals a shaft never sees.

5. The smex bloomery patch also raises crushed ore's stack size to 128
   (`assets/siex/patches/vanilla/crushed.json:14-20`). A roasted-ore item has to pick a stack size against
   that, and whichever side it picks changes how many hand-loads an ore hopper takes.

---

## Open

1. The whole feature. In build order: the item (`iiex:crushedore-roasted` + a retexture + the
   `combustibleProps` route), a second hearth contents model, a roasting mode on the shared core, the
   `BfChargeLossFull` reduction and the roasted recovery key, and a cost - the fuel a roast burns. Five
   pieces, none started.

2. The decisive number is unpicked. *Coke spent roasting < coke saved downstream.* [coking](coking.md)
   supplies the demand side - ≈ 11.6 coke per cast pig - so there is something to size a roasting cost
   against. Nobody has.

3. Sulphur has no home. It is the third real mechanism, it is the one with the clearest downstream
   consequence (red-shortness, which the material tiers would use), and its chemistry is on the Homestead
   side of the cut ([scope](../scope.md)). Whether a metallurgical sulphur - an ore property and a material
   penalty, with no chemical plant - is in scope is unasked.

4. The fettle displacement. `FettleRecipeDefinitions.cs:22-24` promises that roasting becomes the better
   route and the hand craft becomes the fallback. Nothing states the exchange rate, and a fettle item page
   does not exist yet.
