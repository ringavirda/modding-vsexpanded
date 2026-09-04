# Lime Kiln - quicklime, cement, concrete

**Status** deferred   **Would live in** Industrial Homestead
**Deferred by** the metalworking-only cut - [scope.md](../../scope.md), which owns the decision. The
specification survives only in git history and on this page - see
[What exists today](#what-exists-today).

**Owns**

- The archived design: what the kiln was, and the "recipe modes, not separate machines" ruling that shaped it.
- The deferral evidence [scope.md](../../scope.md) does not carry: the whole lime chain is already in vanilla,
  and the spine's flux is raw crushed limestone, not quicklime.
- That this is the only item in the deferral tree with no downstream dependent anywhere, including inside
  Homestead.

**Depends on**

[scope.md](../../scope.md) - the cut and the rule · [beehive coke oven](../../machines/coke-oven.md) - the
machine whose relationship to vanilla this one would have copied · [burden](../../items/burden.md) - the
flux fraction and grade bands · [heat balance](../../mechanics/heat-balance.md) - the shared furnace model a
shaft kiln would have run on · [casting-bed](../../machines/casting-bed.md) - the slag-brick side, which
draws the same cement/concrete line from the other direction.

---

## What it is

A **continuous coke-fired shaft kiln**: limestone charged at the top, coke burned through it, quicklime drawn
from the bottom. The reaction is calcination - CaCO₃ → CaO + CO₂, around 900 °C. Slaking quicklime with
water gives lime putty; grinding quicklime with a pozzolan (ground slag) gives hydraulic cement, and from
cement, concrete.

It sits beside the iron tier historically: the same coke, the same shaft-furnace shape as a cupola, often the
same works.

---

## Why it is deferred

Nothing in the spine needs quicklime ([scope.md](../../scope.md)). The blast furnace fluxes on limestone, and
smex already ships a mortar recipe.

### 1. The spine's flux is raw crushed limestone, and that is metallurgically correct

`assets/iiex/config/materialroles.json:3`

```
{ "role": "flux", "code": "game:lime" }
```

`game:lime` is vanilla crushed lime - raw limestone, uncalcined. The burdenmaker tests membership of that
role directly (`BlockEntityBurdenmaker.cs:164-166`, via `MaterialRoleRegistry.IsRole(Roles.Flux, …)`,
`src/ExpandedLib/Materials/MaterialRoleDef.cs`), and the flux fraction it stamps grades a
[burden](../../items/burden.md).

A blast furnace is charged with raw limestone; calcination happens inside the furnace as part of the burn, and
the CaO produced there fluxes the slag. A furnace fed pre-burned quicklime pays twice for the same heat.

### 2. Mortar already ships, without lime the mod ever made

`src/SteelIndustryExpanded/Recipes/Barrel/MortarRecipeDefinitions.cs:15-41` - a barrel recipe:

| in | qty |
|---|---|
| `game:slakedlimeportion` | 1 L |
| `iiex:powderedslag` | 8 |
| **out** `game:mortar` | 4 |

The one building-material payoff from lime already ships, off vanilla slaked lime, and it is slag cement.

### 3. The entire lime chain is vanilla, so the kiln is a scale upgrade and nothing more

| step | how vanilla does it | where |
|---|---|---|
| crushed lime → quicklime | `combustibleProps`: `meltingPoint: 825`, `meltingDuration: 20`, `smeltedRatio: 2`, `smeltingType: "cook"` - i.e. any firepit or kiln, no container | `.game/1.22/assets/survival/itemtypes/resource/crushed/lime.json:29-36` |
| quicklime → slaked lime | barrel: 1 L water + 4 quicklime → 4 L `slakedlimeportion` | `.game/1.22/assets/survival/recipes/barrel/slakedlime.json` |
| slaked lime + slag → mortar | the smex recipe above | `MortarRecipeDefinitions.cs` |

A player can already burn lime, slake it and build with it. A lime kiln would deliver the same items in bulk,
which is the [beehive coke oven](../../machines/coke-oven.md)'s relationship to vanilla coking. That machine
earned its place because coke is the single fuel behind the entire iron tier; quicklime is behind nothing.

---

## What exists today

No block, no code, no asset.

```
$ grep -rniE "kiln|quicklime" src/ --include=*.cs
(0 results)
```

The only `kiln` matches anywhere under `src/` are build residue in stale compiled output:
`src/SteelIndustryExpanded/bin/Debug/net7.0/…/SteelmakingExpanded.dll` and its `net8.0` twin contain an
anonymous-type field named `beehivekiln` (a vanilla clay-firing attribute) that no current source file
produces.

No lang key, no handbook entry, no shape, no config key, no recipe.

### The specification survives only in git

The row was removed from the old iiex design monolith at the cut and the monolith deleted; git history holds
the only other copy:

```
$ git show 791b43b:docs/design/iiex.md   # line 98
| **Lime kiln / cement** | — | — (coke-fired shaft) | limestone + coke → **quicklime**;
quicklime + ground slag → **cement / concrete** | **recipe modes / barrel mixes, not separate
machines** — quicklime is a continuous-shaft-kiln output on the same heat balance; cement is a
barrel/recipe payoff | *(planned)* |
```

---

## The design as it stands

Everything that was ever decided, from the row above:

| Question | Answer as it stood |
|---|---|
| Block form | Not a separate machine. "Recipe modes / barrel mixes" - quicklime is an output of a continuous shaft kiln on the same heat balance, cement is a barrel/recipe payoff |
| Heat model | the shared furnace [heat balance](../../mechanics/heat-balance.md) - no new model |
| Fuel | coke-fired shaft (no blast, no tuyere listed) |
| Inputs → outputs | limestone + coke → quicklime; quicklime + ground slag → cement / concrete |
| Status when cut | *(planned)*, never started |

Calcination at ~900 °C is below the iron melt lines the iiex heat model is tuned around, so a shaft kiln is a
furnace-core variant: no new FSM, no new blast demand, a melting point and a recipe. The suite already ships a
continuous shaft furnace in the [cupola](../../machines/cupola.md); a Homestead build starts from that, not
from a blank block.

Cement and concrete are the payoff, not the machine - barrel mixes, mirroring the shipped mortar recipe. The
slag-brick design draws the same line from the other side: slag brick is cheap bulk building material, and
cement/concrete stay deferred to the homestead mod.

---

## What it would unblock

Nothing. This is the only entry in the deferral tree with no dependent:

| Candidate consumer | Verdict |
|---|---|
| Blast furnace / cupola flux | No - the role is bound to raw `game:lime`, and pre-calcined flux is metallurgically wrong (above) |
| Mortar for the mods' own brickwork | No - already ships off vanilla slaked lime |
| elex's electrolyte via the lead chamber process | No - its inputs are sulfur and saltpetre, both vanilla ([chemistry](chemistry.md)) |
| The gasworks, the still, oil, climate control | No - none of them takes lime |
| Concrete as a building material | Only itself, and that is a building-mod ambition rather than a Homestead one |

[oil](oil.md) by contrast has a consumer in elex. The kiln is a leaf: if Homestead is ever built to a budget,
this is the row to cut first.

---

## Gotchas

1. Do not add quicklime as a second flux. Flux membership is a role, not a recipe ingredient
   (`materialroles.json:3`; `BlockEntityBurdenmaker.cs:164-166`), and burden grade bands are computed from
   the resulting flux fraction ([burden](../../items/burden.md)). A second flux item does not add an option -
   it changes how every existing burden grades, including burden already stamped and stored.

2. Vanilla already smelts crushed lime at 825 °C in any fire. A kiln block that also converts lime must not
   create a second, competing path to the same item. The [coke oven](../../machines/coke-oven.md) hit this
   and solved it by refusing to reuse `game:cokeovendoor`, so vanilla's own coking cannot fire inside the
   mod's chambers ([coke-oven](../../machines/coke-oven.md) § Owns). Copy that decision.

3. CO₂ has no medium and should not get one. `assets/exlib/config/liquids.json` declares four codes, and R1
   gives a run exactly one medium ([conventions.md](../../conventions.md)). Calcination gas is flavour;
   venting it would mean a `LiquidDef`, a producer and a consumer for a stream nothing burns.

4. Cement → concrete stops at mortar, on this page and on the slag-brick side. A concrete block set is a
   construction mod.

---

## Open

- If it is ever built: furnace-core subclass, or pure recipe mode. The archived row says "recipe modes… not
  separate machines", but also "continuous-shaft-kiln output on the same heat balance", which reads like a
  block. Those are not the same answer and nobody has picked one.
- Whether Homestead wants it at all, given it unblocks nothing. It is period flavour next to a gasworks, and
  flavour is a legitimate reason in a domestic mod, but the choice is made on that basis rather than
  inherited as a to-do.
