# Climate Control — radiators, cooling coils, ice

**Status** deferred   **Would live in** **Industrial Homestead** (the planned domestic/chemical mod)
**Deferred by** the metalworking-only cut - [scope.md](../../scope.md), which owns the decision and the
rule it applies. The specification survives from the archived iiex spec (git history) and is restated below.

**Owns**

- The room heat-balance design - the formula, the both-signs emitter model, the insulation term, and every
  finding recorded against it (the emergent heat pump, the vanilla-cellar result, the crop patch).
- Why this page is not the phase-change page. Climate control is deferred; the mechanism it runs on is a
  carve-out that stays ([scope.md](../../scope.md)) and ships today inside the boiler and the steam
  condenser. The deferral is the room model and the refrigerant, not the physics.
- The asymmetry between the two halves - heating is deferred purely on subject matter and has no blocker at
  all; cooling is additionally downstream of the deferred gasworks, because its refrigerant is a gasworks
  by-product.
- The model conflict a builder would hit on day one: a refrigeration loop needs two temperatures and a pipe
  run has exactly one.

**Depends on**

[scope.md](../../scope.md) - the cut, the carve-outs, the rule · [conventions.md](../../conventions.md)
- the phase-change/distillation model that stays, and R1/R7 · the archived iiex spec (git history) ·
[heat balance](../../mechanics/heat-balance.md) - the furnace-scale twin of the room balance ·
[pipe network](../../mechanics/pipe-network.md) - one medium per run, uniform run temperature, where phase
change is allowed · [Cornish boiler](../../machines/boiler-cornish.md):233-236, `:493-494` - the live consumer
of the phase-change model · [fluid tank](../../machines/fluid-tank.md) - the other block that came out of the
same archived section and was carved back in.

---

## What it is

The 1850-1885 industrialisation of the home, heat side and cold side, which historically arrived together
because one gasworks supplied a settlement's "light, heat, cold, colour and fuel".

| Half | Period machine | How it works |
|---|---|---|
| Heating | cast-iron radiators on a low-pressure steam or hot-water circuit | boiler → radiator → condensate returned |
| Cooling | Carré absorption refrigeration on ammonia | heat-driven, no engine - which is why it suits a pre-engine tier at all; Linde's compression machine is the later industrial upgrade |

Both ends drive one number: an enclosed room's temperature offset from ambient. Ice was Linde's actual
product, and the cold room is the domestic payoff.

---

## Why it is deferred

[scope.md](../../scope.md) - one row, one reason: *a room heat-balance for a home, not a shop*. It fails
the rule ("a foundry or a rolling mill would not work without it").

The two halves are not equally deferred:

| Half | Deferred because | Would it be blocked if the rule were lifted? |
|---|---|---|
| Heating - radiator, exhaust wall | subject matter only | No. It runs off a boiler the suite already ships, on the water/steam media it already declares |
| Cooling - coil, evaporator, ice | subject matter and its refrigerant | Yes. Ammonia is a by-product of the deferred [gasworks](gasworks.md)' ammoniacal liquor, so cooling cannot be lifted out of Homestead on its own |

The heating half's lack of a blocker is not an argument for building it; it will always look cheap, and it is
still out.

### The mechanism is not deferred — only this application of it

[scope.md](../../scope.md) keeps the general **heat a liquid → boil off in ascending boiling-point
order → condense each at a cooler stage** model, because the boiler runs its simplest case. That carve-out is
why the archived spec could say refrigeration "reuses the **condenser** block (steam→water generalised to
ammonia gas→liquid)", and it is still true of the shipped code:

| The generalisation | Where it already lives |
|---|---|
| A medium declares what it condenses to, and below what temperature | `LiquidDef.CondensesTo` / `CondenseBelowC`, `src/ExpandedLib/Fluids/LiquidDef.cs:26-34` |
| …and what it boils into, and above what temperature | `LiquidDef.VaporisesTo` / `BoilPointC`, `:36-43` |
| Passive, temperature-gated phase change either way | `IMediumTaxonomy.TryCondensation` / `TryVaporisation`, `src/ExpandedLib/Fluids/IMediumTaxonomy.cs:40-64`; implemented `ExLiquids.cs:158-196` |
| Nothing hardcodes steam - the boiler asks the catalogue | [Cornish boiler](../../machines/boiler-cornish.md):233-236, `:493-494` |
| The catalogue is data, overlaid per domain at `AssetsFinalize` | `ExLiquids.cs:8-19` |

The refrigeration loop is a `liquids.json` entry and a room model away, not a physics engine away. The
deferral is a decision, not a technical verdict.

---

## What exists today

**Nothing.** A source sweep over `src/` finds zero matches:

```
$ grep -rniE "radiator|refriger|ammonia|greenhouse|climatecontrol|coolingcoil" src/ --include=*.cs
(0 results)
```

No lang key, no handbook page, no shape, no config key. This is consistent with
[scope.md](../../scope.md)'s finding that the cut is real in the tree.

Adjacent infrastructure built for other reasons would be reused verbatim:

| Piece | State | File |
|---|---|---|
| The phase-change catalogue and taxonomy | live | `src/ExpandedLib/Fluids/LiquidDef.cs`, `IMediumTaxonomy.cs`, `ExLiquids.cs` |
| The steam condenser - a connector, not a graph node, bridging two runs and condensing between them | live | `src/IronIndustryExpanded/BlockNetworkPipe/BlockEntities/BlockEntitySteamCondenser.cs:16-21`, 1000 ms server tick at `:42` |
| The exhaust passthrough + chimney vent, which the archived spec listed as the "+heat" waste-heat emitter | live | `src/ExpandedLib/Blocks/Networks/BlockPipePassthrough.cs`; `src/ExpandedLib/Blocks/Networks/ChimneyVent.cs` |
| Reading ambient temperature from the world | live, twice, and only as ambient | `BlockEntityFurnaceCore.cs`, `BlockEntityTwinTubMPBlower.cs` (both `GetClimateAt`) |
| The medium catalogue | four entries only - Air, Steam, Exhaust, Water | `assets/exlib/config/liquids.json` |

The heating half's cheapest emitter is already a shipped block: the exhaust passthrough exists and vents into
a chimney. What does not exist is a room for it to warm. The gap is the room model, not the plumbing.

---

## The design as it stands

Recorded so it is not redone. All of this comes from the archived iiex spec (git history).

### The balance

```
T_room = T_ambient + Σ(heater ΔT) − Σ(cooler ΔT)     — bounded by insulation & leak to ambient
```

One shared room behavior hosts both signs. Insulation (sealed walls, glass) sets how well the room holds the
offset - the mirror of a furnace's `T_loss` ([heat balance](../../mechanics/heat-balance.md)), so build
quality gates efficiency rather than possibility (R5). Block-info shows current/target temperature and the
contributing emitters, per R7 ([conventions.md](../../conventions.md)).

### The emitters

| Emitter | Sign | Chain | Delivers |
|---|---|---|---|
| Cast radiator (block, chainable) | + heat | boiler → radiator → condensate returned | winter warmth; warm cellars; crop heating beyond vanilla's +5 |
| Exhaust passthrough wall *(both parts live)* | + heat | stove/firepit → passthrough → chimney | waste-heat room warming |
| Cooling coil / evaporator (block, chainable) | − heat | ammonia loop (Carré absorption cycle), cold side | refrigerated cold storage; crop cooling; ice |

### The five findings that make the design worth keeping

1. **The hot side must vent outside the cooled room.** The loop's condenser (and, in the absorption cycle,
   its absorber) reject heat somewhere, so a fridge warms its surroundings. Route that rejected heat into a
   room to be warmed and the result is a heat pump, emergent from the shared balance with no special case.
   That is why both signs share one behavior.
2. **Cold storage rides vanilla directly, with no patch.** A cooled sealed room is a vanilla cellar held
   below the biome/season floor → the slowest food perish anywhere, any season. Confirmed against the game:
   an enclosed room's temperature already drives the cellar perish rate.
3. **Crops are the opposite case and need a patch.** Vanilla crops read outdoor temperature plus a fixed
   +5 °C greenhouse bonus, and that bonus is warming-only - vanilla has no crop cooling. So the crop
   temperature check must be patched to read the room ΔT in both directions.
   Caution: this is the one part of the design that is a Harmony patch on vanilla farming behaviour, a
   different risk class from everything else here.
4. **Charge once, top up for leaks.** The ammonia loop is closed - a one-time charge plus top-ups, modelled
   on the electrolysis acid fill, which is the same argument [scope.md](../../scope.md) later used to
   downgrade elex's acid blocker.
5. **Ammonia is toxic** and belongs in the danger layer alongside boiler steam - historically why
   refrigeration later moved off ammonia.

---

## What it would unblock

Nothing in the metalworking line - not a wall, not even a degraded path. No shipped or planned machine in
`exlib → iiex → iiex → smex → hpex` reads a room temperature; the only two `GetClimateAt` calls in the tree
read ambient for a furnace's loss term and a blower's intake, and neither would change.

Inside Homestead the picture inverts:

| Waiting on it | Severity |
|---|---|
| The [gasworks](gasworks.md)' ammonia stream having any consumer at all | high - the refrigeration loop is the reason ammonia is recovered from the ammoniacal liquor. Without climate control, ammonia is a fertiliser precursor and nothing else |
| Cold storage / ice as a player payoff | high - it is Homestead's headline domestic feature |
| Waste-heat reuse from a foundry | flavour only; see [Open](#open) |

The gasworks and climate control were merged into one add-on precisely so the ammonia link would be internal.
Splitting them again re-creates the cross-add-on dependency that merge removed.

---

## Gotchas

1. **A refrigeration loop needs two temperatures; a pipe run has one.** The pipe network keeps a single
   network-wide temperature with no spatial gradient ([pipe network](../../mechanics/pipe-network.md)), and
   an absorption cycle is defined by the difference between its evaporator and its condenser. The fix is the
   shipped idiom, not a model change: the loop is two runs bridged by a connector, exactly as the steam
   condenser bridges a steam line and a water line without joining them
   (`BlockEntitySteamCondenser.cs:16-21`). Build it as one run and it cannot work.

2. **Ammonia has no medium, and R1 means it needs one.** `assets/exlib/config/liquids.json` declares four
   codes; a run carries exactly one medium at a time ([conventions.md](../../conventions.md)). Ammonia
   needs a `LiquidDef` with both a `CondensesTo`/`CondenseBelowC` pair and its gas-phase mirror, and it is a
   liquid that mixes only with itself while its vapour joins the mixable gas family
   (`src/ExpandedLib/Fluids/LiquidPhase.cs:9-13`) - so the two sides of the loop are two different media,
   which is the second reason they cannot share a run.

3. **The closed loop is a lifecycle the network does not have.** Every run in the suite today is open -
   produced into by something, drawn from by something else. A charge-once-and-recirculate loop has no
   producer and no consumer, so nothing ticks it and nothing refills it. The charge has to live somewhere
   that persists (a block's own tree, as the [fluid tank](../../machines/fluid-tank.md) would), not in the
   network state.

4. **A "room" is a vanilla concept the suite has never touched.** All ambient reads in the tree go through
   `GetClimateAt` at a single position. Room detection, sealing and volume are vanilla systems the mods have
   no experience with, and the insulation term depends entirely on them.

5. **The crop patch is the risky half, not the heat maths.** Patching vanilla's crop temperature check to be
   bidirectional touches farming balance across the whole game, and unlike a radiator it cannot be scoped to
   the player's own build.

6. **Do not "just add a radiator" because the steam is already there.** A radiator with no room model is a
   decorative block that consumes steam and returns condensate for no effect - state the player cannot see
   the point of, which R7 ([conventions.md](../../conventions.md)) is about.

---

## Open

- **Where the room heat-balance would live if it were ever built.** It is the room-scale twin of the furnace
  balance ([heat balance](../../mechanics/heat-balance.md)), and exlib already owns the shared `T_process`
  law - so the behavior is arguably library code even though every consumer is domestic. Nothing to decide
  until Homestead exists, but the answer is not obviously "Homestead".
- **Whether waste-heat reuse is secretly in scope.** A foundry produces enormous waste heat and the
  passthrough + chimney vent already ship; warming a shop off a furnace's exhaust is the one climate-control
  use with a metalworking flavour. It still fails the rule ([scope.md](../../scope.md)) - a rolling mill
  works fine with a cold floor.
- **Whether Homestead gets a doc at all.** Several pages point into a mod with no file, and this deferred
  tree is the nearest thing it has.
- **Ammonia's medium ownership.** If it is ever declared, it belongs in Homestead's own
  `config/liquids.json` overlay rather than exlib's baseline - the loader supports per-domain overlay
  already (`ExLiquids.cs:8-19`), so this is a convention question, not a capability one.
