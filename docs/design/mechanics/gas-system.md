# The fuel-gas system

**Status** designed - nothing built  **Mods** smex owns the sources; iiex, iiex and hpex own consumers
**Since** 2026-08-05

**Owns** - the facts this page is canonical for:

* the coke/gas boundary - which machines can convert and which cannot, and why it is chemistry
* the gas burner: one block replacing a firebox anywhere, and the boiler exception
* two grades of one medium, and what mixing does
* flaring, and why venting is not an option
* buffering, and the price stored gas pays
* the tier arc - when the transition unlocks, and how it reaches backwards

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| The gas producer machine, its bed chemistry, its config | [gas-producer](../machines/gas-producer.md) |
| Cowper stoves, the dust catcher, stove count and size | [cowper](../machines/cowper.md) |
| Pipe media, pressure, merge/split, `Compatible` | [pipe-network](pipe-network.md) |
| The open hearth and its regenerators | [open-hearth](../machines/open-hearth.md) |
| The closed top that makes capture possible | [blast-furnace-hot](../machines/blast-furnace-hot.md) |
| Boiler internals | [boiler-cornish](../machines/boiler-cornish.md) · [boiler-lancashire](../machines/boiler-lancashire.md) |
| Scope: why producer gas stays and coal gas does not | [gas-producer](../machines/gas-producer.md) § *Why this survived the scope cut* |

---

## 1. The boundary: coke is a reagent, gas is only heat

The rule is chemistry rather than balance:

| stays on solid fuel | goes to gas |
|---|---|
| **blast furnace**, **cupola** - coke is a reductant, a chemical reagent | puddling, reheat, crucible, open hearth, boilers, cowper stoves |
| **coke oven** - it makes the coke | |

Carbon cannot be piped into an ore column, so the transition can be as near-total as it likes without erasing
coke.

Historically exact: Siemens regenerative gas firing (1856–61) went onto puddling furnaces, reheating
furnaces, crucible melting, glass tanks and the open hearth - which is a gas-fired regenerative
reverberatory. A producer plus regenerators was bolted onto an existing hearth: the hearth did not change,
the firing did. Standard in large works by the 1870s–90s.

## 2. Sources - two grades of one medium

| grade | character | made by |
|---|---|---|
| **blast-furnace gas** | lean | captured at a closed furnace top, cleaned in a dust catcher |
| **producer gas** | rich | blowing air + steam through hot coke |

Lean gas delivers less: lower flame temperature, less steam from a boiler, a lower ceiling in a furnace. The
two are grades of one medium. `Compatible` returns true for any gas pair, so gases always mix; as grades,
mixing gives a weighted average instead of a priority relabel that discards the difference.

This is why the open hearth is regenerative: preheating air and gas with the exhaust is what reaches steel
temperature on lean gas. The regenerator is a consequence of the fuel.

### Why the producer gets built when BF gas is free

The blast furnace runs intermittently and the gas chain must not. Steam engines, boilers and heating furnaces
need supply that survives the end of a campaign or a reline. The producer is baseload; BF gas is opportunistic
surplus. Works ran both.

The producer is also what gives lignite a job. It gasifies slack, dust and poor coal that cannot burn on a
grate at all, which matters because lignite was ruled out of the coke oven (only bituminous cokes). Without
the producer, lignite is nearly dead content.

## 3. The burner

One piped gas-burner block replaces a firebox anywhere. It functions exactly as a firebox does and consumes
gas instead of a fuel bed. No per-machine variant, no parallel family - the same move the firebox block
itself made when it replaced `@(air|coalpile)`.

Boilers are the exception and take shape variants, because the vessel visibly differs: smex ships the gas
Cornish; hpex ships both Lancashire variants.

## 4. Flaring - surplus is burnt, never vented

A smokestack is the wrong outlet. A stack carries combustion products - gas that has already burnt. Raw
blast-furnace gas is CO-rich, toxic and explosive, so venting it unburnt wastes fuel and, in reality, kills
people.

The historical answer is the bleeder / flare: furnaces carried bleeder valves at the throat that released
surplus and ignited it, a large and very visible flame. Producer plants flared surplus the same way.

A flare visible from across the map means fuel is being wasted. A well-plumbed works barely flares; an idle or
under-consumed one burns its surplus in plain sight, so nothing has to tell the player their gas balance is
wrong.

## 5. Buffering - allowed, and priced in heat

| feed | temperature | when you want it |
|---|---|---|
| **direct** | full value - gas is fed hot on purpose | a furnace that needs the ceiling |
| **from a holder** | ambient - the sensible heat is gone | smoothing a supply that swings |

Smoothness or temperature, never both. A blast furnace's output swings with charging and tapping while its
consumers do not, so a holder keeps the steam engines running through a charge, at the cost of a cooler flame
wherever it feeds. Storage is not forbidden; it is priced.

### The holder is the tank's sibling, not a new machine

One storage family, two shape variants, behaviour forking on whether the medium is compressible:

| | **liquid tank** | **gasholder** |
|---|---|---|
| vessel | rigid | telescoping bell in a water seal |
| volume stored | fixed = vessel volume | variable - the bell rises and falls |
| pressure | from head (fill-dependent) | constant, set by the bell's weight |
| why | liquid is incompressible | gas is compressible, so a rigid vessel stores it at wildly varying pressure; rigid gas storage needs compression, a later technology |

The bell's fill is readable from across the map - the same family as the flare showing wasted gas and the slag
spout showing the iron cap. The works states its condition by its silhouette; nothing opens a GUI.

## 6. The tier arc - the closed top unlocks it, and it reaches backwards

An open-topped furnace cannot capture its gas; it burns it at the throat as the tunnel-head flame. So the
transition begins at smex, and it falls out of the two drawings the design already ships:

| | **cold blast** (iiex) | **hot blast** (smex) |
|---|---|---|
| top | open stack, 3 air cells - "my open top is my chimney" | sealed: bell hopper over a 1-cell throat |
| exhaust | none | 2 outlets, feeding cowpers and the main |

Historically exact: capture needed a closed top (Parry's cup-and-cone, 1850), decades after the cold blast
furnaces the iron tier models. The cold furnace's wasted flame is the visible argument for the closed top, and
it costs nothing because it is already drawn.

| tier | what gas does to it |
|---|---|
| **iiex** | a gas burner drops into the puddling / reheat firebox cell - the hearth untouched |
| **iiex** | smex ships the gas Cornish variant |
| **hpex** | both Lancashire variants ship |
| **smex** | gas-native from the start |

Nothing the player built becomes obsolete - the way it is fed changes.

## 7. Why gas wins, and it is not power

Raw gas is not stronger than coal per unit; BF gas especially is lean. The advantages, in the order they
mattered:

1. **Centralisation.** One producer plant, stoked in one place, piped to everything. No hand-feeding ten
   boilers and furnaces.
2. **Regeneration** - gas plus a regenerator beats any solid-fuel bed and is the only route to steel
   temperatures. The regenerator is the upgrade; the gas is what makes it possible.
3. **Control** - a gas flame is adjustable and its atmosphere can be set reducing or oxidising.
4. **Cleanliness** - no ash in the furnace, no sulphur contact with the work.
5. **Poor fuel becomes usable** - see lignite, above.

So the mod's shape is: coal is stronger per unit and demands attention; gas is weaker per unit and demands
none. Buy the plumbing and the producer, and stop stoking.

## 8. Loops this closes

```
  blast furnace ──gas──> blowing engine ──blast──> blast furnace
                   ├────> stoves ──hot blast──────────┘
                   ├────> boilers ──steam──> producer (needs steam)
                   └────> flare (visible waste)

  dust catcher ──flue dust──> back into the burden
```

The furnace powers its own blower. It also gives iiex a reason to touch iiex's output: the twin-tub blower is
the iron tier's only air source, so a gas-fired blowing engine is the steam tier's upgrade to it, bought with
plumbing rather than a bigger blower.

BF gas also breaks a circular dependency: producer gas needs steam, and steam needs a boiler. BF top gas needs
no steam, so a gas-fired boiler is what makes producer gas bootstrappable at all.

## 9. Open

* **Where the dust catcher lives** as a block, and its footprint.
* **The gas-fired blowing engine** - an iiex machine consuming gas and driving blast, replacing the twin-tub.
  Named nowhere yet.
* **Flue-dust routing** - it should go wherever fine iron-bearing material goes (the same place `millscale`
  does) rather than inventing a disposal verb.
* **Numbers.** Grade calorific values, burner consumption rates, holder capacity, and the retune the surplus
  forces: the 48 L/s exhaust figure, the 5.3× stove charge advantage, the two-stove assumption, and
  `blast-furnace-hot.md`'s exhaust arithmetic.
