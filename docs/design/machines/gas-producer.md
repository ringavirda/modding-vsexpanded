# Gas producer (Siemens producer)

**Status** designed - nothing exists. No block, no block entity, no shape, no recipe, no config key, no
lang key, no medium. `grep -ri "producer gas\|gasproducer"` over `src/`, `assets/` and `lang/` returns
zero hits in code - every hit is prose in `docs/design/`
**Mod** smex (`SteelmakingExpanded`)

**Owns**

* the machine itself: what a gas producer is in this suite, its inputs, its two consumers, and the
  **coke + limited air + steam → producer gas** conversion as a design;
* the scope carve-out as it applies to this machine - why producer gas stays in the metalworking line while
  coal gas went to Industrial Homestead, and the fact that the boundary is tar;
* the reason the producer is built even though blast-furnace top gas is free, and the lignite carve-in;
* the producer-gas medium requirement: that it must be a fourth `LiquidDef` entry, what it must declare,
  and the three shipped hard-coded medium strings that a fourth gas has to be threaded through;
* the proposed numbers, every one marked as a proposal and anchored to a shipped number;
* where a producer would hook into existing code, which shipped machine is its structural template, and the
  build list.

**Does not own — cited only, never restated**

| Fact | Owner |
|---|---|
| `T_process = T_in − T_loss`, the coke/air/preheat terms, the melt-speed factor, the `Idle → Firing → Melting` FSM and every `Bf*` key | [heat balance](../mechanics/heat-balance.md) |
| the graph substrate, one-medium-per-run, `MaxVolume`, gas pressure, leaks, vents, bursts, merge/split, the tick order, `MachinePorts` | [pipe network](../mechanics/pipe-network.md) |
| the cast tier's 5 atm rating, the fittings, the joint families | [cast pipes](cast-pipes.md), [pipe network](../mechanics/pipe-network.md) |
| where coke comes from and why the beehive oven recovers nothing | [coke oven](coke-oven.md) |
| where steam comes from, its rate and its pressure | [Cornish boiler](boiler-cornish.md) |
| where blast air comes from | [twin-tub blower](twin-tub-blower.md); smex's engine air blower |
| the cowper's regenerator model, its cycle and its `Cowper*` keys | [cowper](cowper.md) |
| the open hearth as a machine - its bath, its scrap, its product | [open hearth](open-hearth.md) |
| the filler footprint system, layout DSL, origin-is-the-negation | [multiblock](../mechanics/multiblock.md) |
| code-first defs, RCC stages, goldens, the recipe-cost catalogue | [recipes & config](../mechanics/recipes-config.md) |
| the medium-agnostic bulk buffer, and why it cannot be built today | [fluid tank](fluid-tank.md) |
| R1 single medium · R2 declared recovery · R5 gate efficiency · R7 nothing hidden | `../conventions.md:27-54` |

**Depends on** [heat balance](../mechanics/heat-balance.md) · [pipe network](../mechanics/pipe-network.md) ·
[coke oven](coke-oven.md) · [Cornish boiler](boiler-cornish.md) · [cowper](cowper.md) ·
[fluid tank](fluid-tank.md) · [multiblock](../mechanics/multiblock.md) ·
[recipes & config](../mechanics/recipes-config.md) · `../overview.md:115-121` ·
`../STATE.md:665-669`

---

## Role

The open hearth has no fuel without it. Every other fired machine in the suite burns a solid charge read out
of coal piles in its shaft ([heat balance](../mechanics/heat-balance.md)); a regenerative open-hearth bath is
fired by a flame played over the metal, and that flame has to arrive down a pipe.

A Siemens producer is a small shaft furnace fed coke and a deliberately insufficient blast, with steam
injected into it. Too little air to burn the carbon through, so the bed gives up carbon monoxide and, from
the steam by the water-gas reaction `C + H₂O → CO + H₂`, hydrogen. That mixture is producer gas.

### Why this survived the scope cut and coal gas did not

The metalworking-only cut deferred the whole gasworks - retort house, gas main, gasholder, tar, benzene,
ammonia, gas lighting - to Industrial Homestead (`../overview.md:101-113`). Producer gas is carved back in
(`../overview.md:115-121`), and the line is drawn on tar:

| | **Producer gas** *(stays)* | **Coal gas** *(deferred)* |
|---|---|---|
| Made by | blowing air + steam through hot coke | carbonising coal in retorts |
| Character | lean fuel gas, ~1/10 the heating value | rich, and a chemical feedstock |
| By-products | ash | tar, ammonia, benzene - a chemistry mod |
| Stored? | allowed, at the price that stored gas goes cold ([gas-system](../mechanics/gas-system.md)) | yes - a gasholder, because lighting demand is intermittent |
| Why it belongs here | smex's open hearth burns it | its consumers are domestic |

`../STATE.md:665-669` records the consequence: the metalworking line never gets coal-tar pitch, and therefore
never gets graphite electrodes without Homestead, because the gas process it kept produces none. The producer
is the tar-free half of gasmaking, as the [beehive coke oven](coke-oven.md) is the by-product-free half of
coking.

### It makes the steam tier structural rather than decorative

The gas system - the burner that replaces a firebox, the flare, buffering and the gasholder, the tier arc,
the two gas grades, and the coke-where-carbon-is-a-reagent / gas-where-fire-is-heat boundary - is owned by
[gas-system](../mechanics/gas-system.md) (settled 2026-08-05). This page keeps only what is the producer's
own.

#### Why the producer gets built even though blast-furnace gas is free

The blast furnace runs intermittently and the gas chain must not: steam engines, boilers and heating furnaces
need a supply that does not stop when a campaign ends or a furnace is being relined. The producer is
baseload; BF top gas is opportunistic surplus.

It also gives lignite a job. A producer gasifies slack, dust and poor coal that cannot be burnt on a grate at
all, which matters because lignite was ruled out of the coke oven on 2026-08-05 (only bituminous cokes).
Without the producer, lignite is nearly dead content.

Producer gas needs steam, and steam needs a boiler, so the chain reads: no boiler → no producer gas → no
open hearth → no low-nitrogen steel → no pressure parts. That is the only place in the suite where the steam
tier is load-bearing for a metallurgical product rather than merely a better prime mover (contrast the power
progression at `../STATE.md:611-618`, where steam only replaces the waterwheel).

### Its second consumer: the cowpers

A stove is fired on blast-furnace top gas, and where top gas is short a producer makes up the difference; the
same gas fires the open hearth's regenerators. Both are "burn a lean gas under a checkerwork" - the
[open hearth](open-hearth.md)'s chambers are the cowper idiom applied to both gas and air.

The shipped cowper cannot burn anything. `BlockEntityCowperStove.OnProductionTick` soaks sensible heat out of
whatever gas arrives, reading the run's temperature and moving the regenerator toward it
(`BlockEntityCowperStove.cs:172-188`); its only fuel-ish input is a vanilla coal pile placed under the stove,
which multiplies the soak rate (`:120-139`). Feeding it producer gas today would deliver the gas's
temperature and throw away its calorific value. See [Open](#open).

---

## Structure *(proposed — nothing is authored)*

No layout exists. `docs/internal/workbench/layouts.md` has no producer entry, and no `MultiblockLayout` in `src/`
mentions one. The cells below become hard-coded structure-local offsets the moment the block entity is
written.

The structural template is the cowper stove: a 3 × 3 column of refractory brick, 7 layers tall, anchored by a
port block that is also the multiblock origin, with an interior column of behaviour-carrying cells
(`BlockCowperStoveIntake.cs:49-115`, 59 declared cells).

| Cell role | Why the producer needs it | Where it is declared on the shipped core |
|---|---|---|
| fuel column (the bed) | coke is charged and burns here; it is what the heat balance reads as the charge | `ShaftBox` - the bounds of the layout's `CellRole.Chargeable` / `CellRole.Firebox` cells |
| air inlet, low | the limited blast - the whole mechanic is that it is short | `CellRole.Tuyere`, marked on the layout |
| steam inlet, low | injected into the bed with the air | no member exists; a new port |
| gas offtake, high | producer gas leaves the top of the bed | `CellRole.GasOutlet`, marked on the layout |
| centre | ignition whoosh, fire ambience, extinguish hiss | `ShaftCentre`, `:237` |
| ash / poke door | R2 declared recovery (`../conventions.md:30-37`) | no member exists |

Two of those cells have no member on the shared core. A producer is the first machine in the suite that takes
a gas reagent (steam) into its fire and emits a gas product that is not exhaust, so `BlockEntityFurnaceCore`
grows two new overridable offset lists or the producer resolves them itself the way the cowper resolves its
passthrough at `GetGlobalPos(0, 1, 2)` (`BlockEntityCowperStove.cs:145-148`).

It should be small and built in banks, not enlarged - the same "multiply, don't enlarge" rule the
[cupola](cupola.md) and the crucible furnace follow (`../STATE.md:704`, § D9). One producer per hearth is the
historically correct coupling and it keeps the layout at cowper scale.

---

## Assets

None. No shape, no texture, no animation, no handbook page, no lang key.

| Asset | Path | State |
|---|---|---|
| producer shape | — | nothing in `assets/editable/shapes/` or `assets/smex/shapes/` matches `gas`/`producer` |
| producer-gas medium | `assets/smex/config/liquids.json` | does not exist; `assets/smex/config/` holds only `handbook/` and `metals/` |
| handbook page | `docs/smex/handbook/` | missing - stops at `04-bessemer.html` |
| build reference | `assets/editable/refs/` | missing - only `rivetsnails/` and `rolling/` exist |

The one free asset is refractory brick. The producer is brickwork exactly as the cowper is
(`BlockCowperStoveIntake.cs:51`, `game:refractorybricks-good-tier*`), so the machine needs one port shape and
nothing else.

---

## Construction

There is no recipe. That is a blocker, and it is the same blocker the whole planned half of smex has.

`SmexRecipeConfig.Defaults()` lists every grid and RCC recipe smex ships - converter ×3, cowper ×2, engine
air blower, smokestack, two hoppers, and the converter RCC (`SmexRecipeConfig.cs:45-70`). There is no
producer entry, and no open-hearth entry. A producer needs one row added there plus a
`Recipes/Grid/GasProducerRecipeDefinitions.cs` in the shipped layout
([recipes & config](../mechanics/recipes-config.md)).

The cowper's recipe is the template - *"pipe wrapped in refractory brick, which is exactly what a
regenerative stove is"* (`CowperStoveRecipeDefinitions.cs:9-11`): tier-2 refractory brick + 2 nails + 1 pipe
+ hammer, in a 3 × 3 grid (`:21-27`). A producer is a brick shaft with a steam connection and a gas offtake;
the same bill plus a second pipe is the right order of magnitude.

Inherited trap: the cowper's layout hard-codes lpex fittings. `BlockCowperStoveIntake.cs:53-54` declares
`lpex:pipe-outlet*` and `lpex:pipe-passthrough-*` as required structure cells, so a smex machine cannot be
completed out of the iwex tier. This is the same defect the Lancashire boiler has against the rolled tier
([boiler-lancashire](boiler-lancashire.md), and [pipe network](../mechanics/pipe-network.md) § joints). A
producer's layout must name a fitting family, not a domain, or it inherits the bug on day one.

---

## Operation *(proposed)*

### Inputs → outputs

```
coke (solid charge)  ─┐
limited air  ─────────┼──▶  [ GAS PRODUCER ]  ──▶  producer gas, hot, on a pipe run
steam  ───────────────┘                        └─▶  ash (R2 declared recovery)
                                                    │
                                      ┌─────────────┴─────────────┐
                                      ▼                           ▼
                              OPEN HEARTH (the bath)      COWPERS (the stoves)
```

### Player verbs

| Verb | Where | Effect |
|---|---|---|
| charge coke | the fuel column | as every fired machine - coal piles in the shaft ([heat balance](../mechanics/heat-balance.md)) |
| light it | the charge | the shared `Idle → Firing` transition (`BlockEntityFurnaceCore.cs:559-561`) |
| plumb air in | the low port | a pipe run carrying `Air`, per [pipe network](../mechanics/pipe-network.md) |
| plumb steam in | the low port | a tap off the boiler's steam main ([Cornish boiler](boiler-cornish.md)) |
| plumb gas out | the high port | a pipe run that must reach the hearth and nothing else |
| read state | block info | R7 nothing is hidden (`../conventions.md:48-54`) - bed temperature, gas rate, steam/air supply fractions |

### States

Three, and they are all supply states rather than a new FSM - the shared `Idle / Firing / Melting` machine
already covers the fire ([heat balance](../mechanics/heat-balance.md)):

| State | Condition | Consequence |
|---|---|---|
| banked | no air or no steam | gas output stops; the hearth downstream cools - the coupled loop the design wants |
| gasifying | air and steam supplied, bed lit | gas produced at rate, at bed temperature |
| burning through | air supplied without steam | design decision - a producer over-blown on dry air is just a small furnace making exhaust. See [Open](#open) |

It must never be a net energy source. The bed is coke; the gas carries that coke's energy minus losses. R2's
declared-recovery rule (`../conventions.md:30-37`) applies to gas exactly as it does to metal: the page that
ships this machine must publish "per N units of coke → M litres of gas at T °C", and the number must be a
loss.

---

## Numbers

Every value below is proposed - nothing is in config. The anchor column is a shipped number the proposal is
scaled against, so a retune of the anchor moves the proposal with it.

### Rates

| Key *(proposed)* | Value | Anchor (file:line) | What it does |
|---|---|---|---|
| `ProducerGasPerSecond` | 24 L/s | `SmexConfig.cs:139` - `CowperIntakeVolume` = 24 L/s per intake; `BlockEntityFurnaceCore.cs:208` - `ExhaustVolumePerTick` = 24 | gas injected into the offtake run per second. One producer = one cowper intake = one furnace gas outlet. Deliberately the tier's unit rate |
| `ProducerAirPerSecond` | 8 L/s | `SmexConfig.cs:148` - `BessemerBlastPerSecond` = 8.0 | the limited blast. It must sit far below the furnace's `TuyereDrawFor(mix)` demand - being air-starved is the process, not a fault |
| `ProducerSteamPerSecond` | 8 L/s | `LpexConfig.cs:152` - `CornishBoilerSteamPerSecond` = 32; `:172` - `WattEngineSteamRate` = 30 | one quarter of a Cornish boiler's output, so a works can run a producer and an engine off one boiler |
| `ProducerGasTempFactor` | 0.8 | `BlockEntityFurnaceCore.cs:211` - `ExhaustTempFactor` = 0.8 | offtake temperature = bed temperature × this. Producer gas is fed hot on purpose |
| `ProducerMaxOutputPressure` | 1.0 atm | `ExlibConfig.cs:32` - `LitresPerPipe` = 30; `IwexConfig.cs:163` - plated burst 2.5 | a fuel main is not a blast main. Keeping the choke at 1 atm means the gas main never bursts and never needs the cast tier |

### The medium

A fourth `LiquidDef` has to exist. `ExLiquids.Load` re-seeds the four built-ins then overlays every domain's
`config/liquids.json` (`ExLiquids.cs:74-97`), so this is one JSON entry in smex, no code:

| Field | Proposed | Constraint (file:line) |
|---|---|---|
| `code` | `"ProducerGas"` | must equal the network `MediumType` string verbatim - `LiquidDef.cs:17-18` |
| `phase` | `gas` | `LiquidPhase.Gas`; gas and liquid never mix, `ExLiquids.cs:110-113` |
| `priority` | 30 | merge dominance. Shipped: Air 0, Steam 10, Exhaust 20 (`ExLiquids.cs:46-59`, `assets/exlib/config/liquids.json`). Above Exhaust so a producer main joined to anything else reads as producer gas rather than silently relabelling to Exhaust |
| `condensesTo` / `boilPointC` | none | producer gas has no phase partner in this model |

### hard-coded — the three medium strings a fourth gas must pass

| Site | Literal | file:line | Consequence for producer gas |
|---|---|---|---|
| furnace blast test | `pipe.Medium == "Air"` | `BlockEntityFurnaceCore.cs:515` | a producer-gas run plumbed into any tuyere supplies zero blast - `blastSupplied` never accumulates, so the furnace slides to natural draught and then starves out |
| furnace disruption test | `pipe.Medium == "Exhaust"` | `:512-513` | exhaust at a tuyere counts a disruption toward extinguish (`:575`); producer gas at a tuyere counts nothing at all - silently useless, not an error |
| furnace outlet | `outlet.TryProduce(..., "Exhaust")` | `:445-448` | the offtake cannot be reused as-is; a producer emits its own code |
| cowper outlet | `outlet2.TryProduce(..., "Exhaust")` | `BlockEntityCowperStove.cs:206-210` | the stove re-emits spent gas as Exhaust at 40 % of intake temperature |
| cowper air label | `string inGasType = "Air"` | `BlockEntityCowperStove.cs:143` | the stove's default label when its passthrough is empty |

None of these is a bug today - they are the shipped three-gas vocabulary. Each one is a place a fourth gas is
invisible, and a producer that silently does nothing is the worst failure mode this machine has.

### Cited — owned elsewhere

Run capacity (`nodes × LitresPerPipe`), gas pressure as a volume ratio, the 1-atm leak clamp, the burst
grace and the vent strategy: [pipe network](../mechanics/pipe-network.md) § 3, § 5, § 6. `T_in`, `T_loss`,
`fuelFactor`, `airFactor` and every `Bf*` key: [heat balance](../mechanics/heat-balance.md). Steam pressure,
the boiler's choke and `SteamExpansionFactor`: [Cornish boiler](boiler-cornish.md). The cowper's
`CowperMaxTemperature` 1240 °C and its four soak/cool rates: `SmexConfig.cs:119-140`, owned by
[cowper](cowper.md).

---

## Drops

Undefined - nothing is built. The two shipped patterns to choose between:

| Pattern | Example | Fits a producer? |
|---|---|---|
| plain multiblock: break the anchor, the brick cells are ordinary blocks the player recovers by hand | cowper stove, smokestack | yes - a producer is brickwork with two ports |
| RCC with a salvage ratio | Bessemer vessel, `RccBrokenDropsRatio` = 0.8 (`SmexConfig.cs:250`) | only if the producer becomes a right-click construction |

The ash is a drop question, not a decoration question. R2 requires the recovery to be declared
(`../conventions.md:30-37`); a producer that eats coke and returns nothing is the same silent-loss shape R2
forbids.

---

## Code — where it will hook in

| Work | Where | Note |
|---|---|---|
| `BlockGasProducer` + `BlockEntityGasProducer` | `src/SteelmakingExpanded/BlockStructures/GasProducer/` | the shipped folder shape: `Blocks/` + `BlockEntities/` beside `CowperStove/`, `Converter/`, `SmokeStack/` |
| subclass the fired core | `BlockEntityFurnaceCore` (`BlockEntityFurnaceCore.cs`) | the closest existing subclass is `BlockEntityHeatingFurnace` - it already overrides `RequiresBlast => false`, draws neither a tuyere nor an outlet glyph (so `CellRole.Tuyere` / `GasOutlet` answer empty), and reads a plain-fuel firebox rather than a burden. A producer is that, plus two gas ports - which for it means marking the two roles rather than un-emptying two arrays |
| the charge read | override `ReadChargeMix` | the reheat furnace's version returns `new BurdenMix(0f, 0f, count)` - "a firebox is all coke and nothing else" (`BlockEntityHeatingFurnace.cs:63-87`). A producer bed is the same |
| emit the gas | `IPipeNode.TryProduce(volume, temp, medium)` | exactly the furnace's outlet call (`BlockEntityFurnaceCore.cs:445-449`) with a different code |
| draw air + steam | `be.ConnectedNetwork<PipeNetwork>(face)` then `TryConsumeGas` | `MachinePorts.cs:15`; the live example is the cowper's exhaust intake (`BlockEntityCowperStove.cs:107-115`) and the engine's steam draw (`BlockEntityEngine.cs:286`) |
| the port block | implement `INetworkConnector` on the anchor | `BlockCowperStoveIntake.cs:134-142` is 9 lines: `NetworkType => "pipe"` plus a rotated `HasConnectorAt` |
| register the medium | `assets/smex/config/liquids.json` | file does not exist; the loader picks it up with no code (`ExLiquids.cs:74-97`) |
| config keys | `SmexConfig.cs` - a new `#region Gas producer` | beside `#region Cowper stove` (`:119-140`) |
| recipe + cost entry | `Recipes/Grid/GasProducerRecipeDefinitions.cs` + a row in `SmexRecipeConfig.Defaults()` (`:45-70`) | both are required; the catalogue is hand-maintained |
| do not register a new network type | — | `"gas"` is a dead network type - the unified pipe network absorbed gas and water, and only `"pipe"` / `"molten"` / `"mpenergy"` are ever registered ([pipe network](../mechanics/pipe-network.md) Gotcha 5) |

### Tests it will need

The shipped per-mod suites live in `test/SteelmakingExpanded.Tests/`. The two that matter here have no
equivalent anywhere yet: a rate assertion (`../STATE.md:133-134` records that *"no test asserts any pump's
rate as a number"*, which is how smex's blower ships at 43.2 L/s against a documented 14.4) and a
medium-isolation test that a producer main joined to an air main is caught rather than silently blended.

---

## Gotchas

* Two gases always mix, so "the gas main" is only a convention. `Compatible` returns true for any two gases
  (`ExLiquids.cs:106-115`) and the merged run takes the higher-priority label (`:118-119`). A player who
  joins the producer main to the blast main gets one pool of relabelled gas - no warning, no particle, no
  refusal. The only thing that keeps fuel gas out of a tuyere is that the player routed the pipes properly.
  This is why the proposed priority is 30: at least the label tells the truth. Owned by
  [pipe network](../mechanics/pipe-network.md) § 2; recorded here because the producer is the machine it
  bites.
* A pipe run is already storage, whatever the buffering rules price: capacity is
  `Nodes.Count × LitresPerPipe` = 30 L per pipe ([pipe network](../mechanics/pipe-network.md) § 3), so a
  300-block main is a 9000 L holder built out of ordinary pipe - and one that keeps the gas hot, unlike a
  holder. Any pricing of storage has to account for it.
* A hot gas run never cools. `ApplyPassiveCooling` requires `pass.Consumers == 0`, but every
  `BlockEntityPipe` counts as a consumer, so any run containing one pipe never passively cools
  ([pipe network](../mechanics/pipe-network.md) Gotcha 1). The design wants the gas hot, so this accident is
  currently in its favour, and any fix to that gotcha silently changes the producer's delivered temperature.
* The cowper's stranded-gas latch is live and the producer would meet it. With both an exhaust connection
  and gas in the passthrough the stove latches to `cowperstove-status-exhaustmix` and vents the stranded
  volume rather than charging (`BlockEntityCowperStove.cs:162-171`). A stove fed producer gas and air is
  exactly that condition.
* Producer gas at a tuyere is silently inert, not refused - `pipe.Medium == "Air"` fails, and unlike
  Exhaust it does not even count a disruption (`BlockEntityFurnaceCore.cs:512-516`). The player sees a
  furnace that will not get hot and no message anywhere. R7 says nothing is hidden
  (`../conventions.md:48-54`); this hides.

---

## Open

| # | Question | Notes |
|---|---|---|
| **1** | **It has no consumer.** The [open hearth](open-hearth.md) is designed and unbuilt | a producer with nothing to feed ships dead. Build order: open hearth first, or both together |
| **2** | **Does the cowper learn to burn gas, or does the producer only ever feed the hearth?** | The shipped stove soaks sensible heat only (`BlockEntityCowperStove.cs:172-188`); firing it on a fuel gas needs a combustion term that does not exist. Cheapest answer: the producer's offtake is hot enough that the existing soak path is a legitimate if lossy cowper feed, and a real combustion mode waits for the open hearth to need one |
| **3** | **The gasholder is designed and unbuilt** | storage is allowed and priced - stored gas goes cold ([gas-system](../mechanics/gas-system.md)); the holder is the [fluid tank](fluid-tank.md)'s sibling, and neither block exists |
| **4** | **What does producer gas do to the heat balance?** | `fuelFrac` is read off the solid charge (`BlockEntityFurnaceCore.cs:750-771`, owned by [heat balance](../mechanics/heat-balance.md)); there is no gas-fuel term in the model at all. The open hearth needs one, and it is the producer's requirement to raise |
| **5** | **What happens with air but no steam?** | Either the producer degrades to a small exhaust-maker (R5: gate efficiency, not possibility - `../conventions.md:43-45`) or it refuses to gasify. R5 argues for the first |
| **6** | **Air source.** Does it share the blast main, take its own blower, or draw natural draught like the reverberatory furnaces (`RequiresBlast => false`, `BlockEntityHeatingFurnace.cs:47`)? | sharing the blast main is dangerous: one pool, and the producer's low draw would sit on the same run as the furnace's high-pressure demand |
| **7** | **Ash / R2 recovery** | undeclared |
| **8** | **Recipe, cost-catalogue row, handbook page, lang keys** | none exist |
| **9** | **The layout must not name `lpex:` fittings** | or a smex machine becomes unbuildable without lpex's cast tier, repeating `BlockCowperStoveIntake.cs:53-54` |
