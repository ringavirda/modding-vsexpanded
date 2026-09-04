# Direct charging

**Status** designed - nothing in `src/` distinguishes a converter destination from any other, while every hop
the route needs is already live, already typed and already agreeing on metal codes. There is no mechanism to
build; there is a layout to allow.
**Mods** iiex (the furnace tap, the canal, the seal) · smex (the converter; later the
[open hearth](../machines/open-hearth.md))

**Owns** - the facts this page is canonical for:

* the claim that direct charging needs no new mechanism, backed hop by hop by the live call chain, including
  the metal-code match at both ends;
* the routing verbs - how a player chooses a destination - and the finding that a branch without a seal is a
  split, not a choice;
* the overflow relationship: what demotes the beds, what a blowing converter does to the run, and the three
  independent reasons the beds are never obsolete;
* the layout lesson - which parts of "molten metal cools in the canal, so the converter wants to sit next to
  the furnace" are simulated, by what mechanism, and which part is not;
* the process arithmetic: how long a converter takes to fill from one blast furnace, and where the chain's
  ceilings sit.

**Does not own** - cited only, never restated:
[cold blast furnace](../machines/blast-furnace-cold.md) - the tap drain path, `TapDrainPerTick`, the stack
factors, and the pool ceilings ·
[molten canal](../machines/molten-canal.md) - the canal family, the start, the tap, the pedestal, sealing and
unsealing, the end-cap rule ·
[molten network](../mechanics/molten-network.md) - `IMoltenCell`, `FlowEdge`, the distance BFS, every flow
rate and per-cell capacity, the `Sealed`/`Solidified` latches, `SoakHeat`, and the back-pressure chain ·
[Bessemer](../machines/bessemer.md) - the converter's four states, its carbon model, its capacity, its blow
length, its heat balance, and B7 / B19 ·
[casting bed](../machines/casting-bed.md) - the bed, its intake and its 20-casting capacity ·
[casting](casting.md) - the sand loop and the three casting routes ·
[puddling furnace](../machines/puddling-furnace.md) - the 9-pig charge · [cupola](../machines/cupola.md) - the
remelt family · [ladle](../machines/ladle.md) · [open hearth](../machines/open-hearth.md) ·
[heat balance](../mechanics/heat-balance.md) - `T_process` and the furnace state model ·
[density rule](../mechanics/density-rule.md) · [pig](../items/pig.md)

---

## What it is

**Hot-metal charging**: run the blast furnace's iron straight into the steelmaking vessel while it is still
liquid, instead of casting it into pigs, letting it go cold, and melting it again.

The mod abstracts away the torpedo car and the transfer ladle. The
[molten canal](../machines/molten-canal.md) is the runner - a fixed brick channel from the tap-hole to
whatever is at the other end, the pre-railway arrangement and the one that makes proximity matter.

---

## The loop

The whole route already exists as typed code, and nothing along it distinguishes a converter destination from
a casting bed. Hop by hop:

| # | Hop | Who does it | file:line |
|---|---|---|---|
| 1 | Furnace pool → tap. `min(TapDrainPerTick, pool)` units per tick, stack size `ceil(units × TapIronStackFactor)`, stamped with the furnace's own `_internalTemp` | `BlockEntityShaftFurnace.DrainIronTap` | `BlockEntityShaftFurnace.cs:1288-1325` |
| 2 | Tap → canal start. Pours to `Pos + side.Opposite`, one down; the tap refuses to open at all unless a `BlockMoltenCanalStart` is there | `BlockEntityFurnaceTap.TryPourMetal` | `BlockEntityFurnaceTap.cs`; the gate at `BlockFurnaceTap.cs` (`iiex:tap-err-nocanal`) |
| 3 | Start → run. The start is the BFS root; each edge is driven once per network tick | `MoltenNetwork.OnTick` / `FlowEdge` | see [molten network](../mechanics/molten-network.md) |
| 4 | Run → the converter's own canal tap. The converter's layout already requires `iiex:moltencanal-tap*` at its local `(1,1,2)`, plus a start and two straights | `BlockConverterControl` layout | see [Bessemer § The layout](../machines/bessemer.md) |
| 5 | Tap cell → bath. The converter drains it directly, `min(cellAmount, space)` per tick, capturing type and temperature first | `BlockEntityConverterControl.TickFilling` | `:379`, tap-closed guard `:391`, drain `:430`, temperature capture `:428`, applied `:444` |
| 6 | Bath ← identity check. The vessel accepts one metal at a time and re-seeds carbon by mass average on a pig fill | same | type gate `:412-419`, carbon reseed `:449-457` |

The two ends already agree on the code. The furnace's product token is `"pigiron"`
(`BlockEntityShaftFurnace.cs:151`) and the converter's `PigCode` resolves the same token through
`MetalRegistry` (`BlockEntityConverterControl.cs`), both landing on `iiex:ingot-pigiron`
(`assets/iiex/config/metals/pigiron.json`). Nothing has to be translated, and `FlowEdge`'s type refusal -
which would silently stop a mismatched run dead - never fires.

The work is making the converter reachable, not building direct charging. The furnace end is buildable in
survival; the converter end is not (B7 / B19, [Bessemer](../machines/bessemer.md)).

### Choosing a destination

There is no switch, no GUI and no destination setting. A player with one tap and two possible sinks has
exactly three verbs, all of which already exist:

| Verb | Where | Effect | file:line |
|---|---|---|---|
| Seal a straight canal with fire clay | any `moltencanal-straight` on the branch | severs the branch; caps every connector face visibly | `BlockMoltenCanal.cs:429-454`; the gate at `:486-507` |
| Unseal with a chisel | the sealed cell | rejoins the branch, refunds clay | `:456-479` |
| Close a fitting (Ctrl + RMB) | the converter's own canal tap | the tap drops off the graph, not merely stops delivering | `BlockEntityMoltenCanalTap.cs:49-50` |

A branch without a seal is a split, not a choice. The [casting bed](../machines/casting-bed.md) pulls from
any horizontal neighbour of its principal unconditionally, at its own rate, regardless of which cell holds
more (`BlockEntitySandCastingBed.cs:267-287`), while `FlowEdge` pushes toward whichever canal cell has less.
A T-junction with one leg to the bed and one to the converter feeds both at once. Routing is not emergent; it
is a seal, and the player must place it.

A seal cannot be thrown mid-pour: `CanSeal` requires this cell and every connector-face canal neighbour to be
empty (`BlockMoltenCanal.cs:486-507`). Switching destination means draining the branch first, and nothing
documents that anywhere a player will find it.

---

## Inputs and outputs

| In | Out |
|---|---|
| molten pig iron off the blast furnace's lower tap, at the furnace's `_internalTemp` | the converter's bath, seeded with `BessemerPigCarbonStart` carbon by mass average |
| — | nothing else changes: no new item, no new block, no new network |
| *(the slag tap is unaffected - it has its own canal and its own destination)* | |

What the route removes from the player's day, compared with the bed route:

| Bed route | Direct route |
|---|---|
| carve → pour → wait for the bed to cool → harvest 8 slots → carry pigs | nothing |
| re-melt the pigs in a [cupola](../machines/cupola.md), or puddle them | nothing |
| pay the reheat in fuel and time | the heat is never lost |

---

## Numbers

Every input is cited; the arithmetic is this page's.

### The chain's ceilings

| Stage | Ceiling | Owner |
|---|---|---|
| Furnace pool → tap | ≈ 30 u/s (`TapDrainPerTick` 50 × `TapIronStackFactor` 0.6) | [cold blast furnace](../machines/blast-furnace-cold.md) |
| Furnace sustained production | ≈ 11.3 u/s at melt-speed factor 1.0 (0.35 carbon/s × 32.3 u/carbon) | [ironmaking](ironmaking.md) § Derived |
| Canal cell → canal cell | 50 u/s (`MoltenFlowRate`) | [molten network](../mechanics/molten-network.md) |
| Converter tap cell → bath | ≤ 25 u/s - the whole tap cell, whose capacity is 25 | drain `BlockEntityConverterControl.cs:430`; capacity cited from [molten network](../mechanics/molten-network.md) |

In steady state the furnace's own production is the limit - every transport hop is faster than the make. The
converter's 25 u/s intake binds only while a standing pool is being drained down.

### How long a converter takes to fill

Assumptions: one cold blast furnace, melt-speed factor 1.0, the converter held in `Filling` throughout, no
cold scrap.

| Quantity | Arithmetic | Result |
|---|---|---|
| burst delivery, full pool standing | intake-limited at 25 u/s; the pool (`BfMaxMoltenIron` 2400) drains at 25 − 11.3 ≈ 13.7 u/s | pool empty in ≈ 175 s, ≈ 4 400 u delivered |
| fill, shipped capacity (4800 u), full pool at start | the burst above, then production rate for the remainder | ≈ 3½ min |
| fill, from a bare producing furnace | 4800 ÷ 11.3 | ≈ 7 min |
| fill, settled capacity (6000 u) | 6000 ÷ 11.3 | ≈ 9 min |
| against the blow | blow ≈ 297 s ([Bessemer § Derived](../machines/bessemer.md)) | charging and blowing are the same order of time |

### What a run costs in heat

| Mechanism | Effect of a longer run | file:line |
|---|---|---|
| **Transit time.** One `FlowEdge` per cell per 1 s network tick | an N-cell run adds ≈ N seconds of cooling before the metal arrives | [molten network § ordering](../mechanics/molten-network.md) |
| **Standing thermal mass.** Every push volume-weight-averages the two charges' temperatures | an N-cell run holds up to 50 N units of previously-poured, already-cooling metal that the new charge averages down into on arrival | `BlockEntityMoltenCanal.cs:216-221`; capacity cited from [molten network](../mechanics/molten-network.md) |
| **No conduction.** Cells exchange heat only when metal moves | a standing run cools cell by cell independently; nothing upstream keeps it warm | [molten network § Open](../mechanics/molten-network.md) |
| **The plug.** A cell below the metal's melting point latches `Solidified` and severs the graph | pig melts at 1150 °C (`assets/iiex/config/metals/pigiron.json`), so a slow, long, cold run does not merely deliver cooler metal - it stops | [molten network § thermal pass](../mechanics/molten-network.md) |
| **`SoakHeat` protects only the start.** A brim-full canal start being poured onto keeps taking heat | the rest of the run has no such protection | `BlockEntityMoltenCanal.cs:276` |

---

## Why it is like this

Hot-metal charging is a destination, not a system: the molten network is live, the converter already declares
a canal tap in its own layout, and the metal codes already match. No second transport layer is added for one
customer.

Two exits from one tap:

| Exit | Cost | Prerequisite |
|---|---|---|
| Beds | cast → cool → collect → re-melt or puddle; the reheat is paid | nothing |
| Direct | none - the heat is never lost | a converter, built and ready |

Early on there is no choice; once the steel tier is standing, the beds stop being the route and become the
overflow.

The beds are never obsolete, for three independent reasons.

1. The iron line needs solid pigs. [Puddling](../machines/puddling-furnace.md) takes nine pigs a charge
   (`PuddlingHearthLayout.cs:21`), and the [cupola](../machines/cupola.md) melts metal that can be picked
   up. Only the steel line can take liquid.
2. A busy converter backs the run up. `TickFilling` only runs in the `Filling` state
   (`BlockEntityConverterControl.cs:209`), so a converter that is blowing, pouring or full stops draining its
   input tap entirely; the tap cell fills to its 25-unit cap, the run fills behind it, and the furnace stalls
   and counts a disruption ([molten network § back-pressure](../mechanics/molten-network.md)). The overflow
   destination is a bed.
3. One bed can absorb the furnace's make. A bed pulls at 25 u/s, above the furnace's sustained ≈ 11 u/s, so a
   single overflow bed keeps up with anything but the burst of draining a full pool, which the canal buffers.

The layout lesson is emergent rather than taught: molten metal cools in the canal, so a long run to a distant
converter loses heat and can plug outright, and the converter therefore wants to sit next to the furnace -
the layout real integrated works used. Only half of that is currently simulated; see Gotcha 3, where the
incentive is a step rather than a slope.

---

## Gotchas

1. The converter end cannot be built in survival. The furnace end can - its tuyere recipe was fixed
   2026-08-07 - but the converter vessel still has an RCC stage that can never be satisfied and a gas intake
   with no craftable ingredient (B7, B19, [Bessemer](../machines/bessemer.md)). Until those close, direct
   charging cannot be exercised in survival.

2. The converter's canal tap must be toggled open even though nothing is parked under it. A canal tap
   normally drains into a parked barrel or mold; here the converter reaches into the tap's cell directly. A
   closed tap fails twice over - `TickFilling` reports `bessemer-status-filling-tapclosed`
   (`BlockEntityConverterControl.cs:391-393`) and the tap has already severed itself from the graph
   (`BlockEntityMoltenCanalTap.cs:49-50`), so its cell never fills either. The layout wildcards
   `iiex:moltencanal-tap*`, so a backwards tap also completes the structure and then never delivers
   ([Bessemer § Open #8](../machines/bessemer.md)).

3. The converter's heat balance never reads the arrival temperature. `T_in` is
   `BessemerAutothermalBase + BessemerHeatPerCarbonUnit × airFactor`, capped - a constant plus a blast term
   ([Bessemer § The heat balance](../machines/bessemer.md)). Once the blow starts, metal that crawled twenty
   cells and arrived barely liquid refines exactly as fast as metal tapped straight into the vessel. The only
   graded effect is during filling: `_charge.SetTemperature(Api.World, temp)`
   (`BlockEntityConverterControl.cs:444`) puts the bath at whatever arrived, and `UpdateSolidified` can latch
   a cold bath before the first blast tick. The proximity incentive is therefore a step - "arrives" vs
   "plugs" - not a smooth cost.

4. A stranded pool cannot be redirected. If the furnace drops below its melt point mid-charge it stops
   draining entirely, and the only way to get that metal out is to let the furnace die and chisel the frozen
   block ([cold blast furnace § Gotcha 5](../machines/blast-furnace-cold.md)). "Route it to a bed instead" is
   not available at exactly the moment a player would want it.

5. The run cannot be re-routed while it is running. Sealing requires the cell and its canal neighbours to be
   empty, so a live branch must be drained before it can be cut. In practice the switchable verb is closing
   the destination fitting, not sealing the canal.

6. Two metals cannot share a run. `FlowEdge` refuses to push into a cell holding a different metal code
   ([molten network § 6](../mechanics/molten-network.md)), so a canal that once carried slag will not take
   pig until it is empty. A shared trunk between the metal tap and the slag tap is not possible, and nothing
   warns about it.

7. Vertical drops do not flow. The flow driver walks horizontals only, while graph membership walks all faces
   ([molten network § Gotcha 4](../mechanics/molten-network.md)). A converter on a different level from the
   furnace tap is in the same network and will never receive anything - a real constraint on "put it next to
   the furnace", since the obvious layout is to put the vessel below the tap.

8. Nothing distinguishes a converter as a destination, including in the HUD. The tap's readout, the canal's
   pour tally and the furnace's ledger all report the same thing whether the run ends at a bed or a vessel.
   R7's "nothing is hidden" is satisfied only in the sense that the individual pieces each report themselves.

---

## Open

1. Unblock the converter end (B7, B19). Until then this page describes a route nobody can walk in survival.
   Both are one-token or one-recipe fixes owned by [Bessemer](../machines/bessemer.md).

2. Decide whether arrival temperature should matter. Today it does not, once the blow starts (Gotcha 3).
   Feeding the arrival temperature into `T_in` - even as a small term, or as a floor below which the first
   blow tick stalls - would turn the layout lesson from a step into a slope. It is a change to the
   [Bessemer](../machines/bessemer.md)'s heat balance, not to the canal.

3. Model the destination choice, or state that it is manual. A seal on a straight is the only real routing
   verb, it cannot be thrown while metal is moving, and an unsealed branch feeds both sinks at once. Either
   that is the design - in which case something should teach it - or the route wants a switchable fitting (a
   plain valve on the branch is the obvious candidate; the block already exists in iiex).

4. The bed rotation still has no code. *Bed count = cooling-and-clearing time ÷ pour time* is the rule that
   makes overflow provisioning a decision, and nothing models a clearing time
   ([casting bed § Open](../machines/casting-bed.md)). Direct charging makes it less pressing, not more.

5. The [open hearth](../machines/open-hearth.md) will want the same route and does not exist. Its charge is
   designed to be a mix of hot metal and cold scrap, so it needs the identical canal hop plus a scrap path -
   nothing new on this page's side.

6. The [ladle](../machines/ladle.md) sits between the converter and the long cell, not before it. Direct
   charging feeds the converter's input; the steel side still needs a ladle for mandatory recarburisation
   before anything can be cast. Neither exists.
