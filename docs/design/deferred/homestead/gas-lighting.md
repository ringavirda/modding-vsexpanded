# Gas lighting (gas lamp + gasholder)

**Status** deferred - nothing exists anywhere: no block, no shape, no lang key, no config key
**Would live in** the planned Industrial Homestead mod. The archived design is explicit that the fixture
and its fuel ship together: "Gas lighting lives entirely in this add-on"
**Deferred by** the metalworking-only cut, recorded in [scope.md](../../scope.md). Do not re-argue it here.

**Owns**

* why this is the clearest case on the deferral list - it is the only entry with zero downstream dependents;
* the design problem gas lighting has even inside Homestead: it must beat a vanilla lantern that is cheap,
  permanent and needs no fuel;
* the archived two-block design (lamp + gasholder);
* the light mechanic the suite already ships four times, which a lamp would reuse rather than invent.

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| The cut, its carve-outs, the release target | [scope.md](../../scope.md) |
| Coal gas - what makes it, why it is out of scope, the tar chain | [gasworks](gasworks.md) |
| Producer gas, and the "no gasholder" ruling for that gas | [gas-producer](../../machines/gas-producer.md) |
| The medium-agnostic storage node and why it cannot be built today | [fluid tank](../../machines/fluid-tank.md) |
| One medium per run, run capacity, leaks, open connectors | [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) |
| The acids and the sulfur question | [chemistry](chemistry.md) |
| R1 · R2 · R5 · R7 | [conventions.md](../../conventions.md) |
| "You buy operating efficiency with build complexity" | [overview.md](../../overview.md) |

**Depends on** [scope.md](../../scope.md) · [gasworks](gasworks.md) ·
[fluid tank](../../machines/fluid-tank.md) · [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) ·
the archived iiex spec (git history)

---

## What it is

The pre-electric lighting tier. Coal gas is made at a [gasworks](gasworks.md), stored in a holder against the
evening peak, distributed through street mains and house services, and burned at fixtures. Two burner
generations matter: the **Argand-type open flame** (bright for its era, sooty, the whole 19th century) and the
**Welsbach incandescent mantle** (1885), which heats a thoria/ceria mesh to incandescence instead of relying
on the flame itself.

Lighting demand is spiky - everybody lights up at dusk - while a retort house carbonises at a steady rate, so
the gasworks runs flat and the holder absorbs the difference.

## Why it is deferred

[scope.md](../../scope.md) - link, do not re-argue. The test it fails:

> "A feature earns a place in this suite if a foundry or a rolling mill would not work without it"
> ([scope.md](../../scope.md) § The rule).

A foundry works by daylight, by firelight, and by the vanilla lantern a player already has. Gas lighting fails
the test twice: the fixture is domestic, and its entire supply chain is a chemical industry that is separately
out of scope. There is no partial version - a lamp with no gasworks is an unlit block, which is why the
archived design refuses to split them.

That makes it the reference case. If a proposed scope argument would let gas lighting back in, the argument is
wrong.

## What exists today

Nothing.

```
$ grep -rniE "\blamp|lantern|gaslight|luminair" src/ --include=*.cs
(no matches)

$ grep -rniE "coal gas|gasworks|gasholder|retort|gas lamp|coal tar" assets/
(no matches — en, ru and uk alike)
```

The suite has never shipped a light fixture of any kind. Every occurrence of "light" in `src/` is incandescent
block light off hot metal - a canal, a barrel, a cast mold, a heat sink. There is no block whose purpose is to
be a lamp.

The mechanic is shipped four times over, and a gas lamp would reuse it rather than invent it:

| Shipped light source | Where | Note |
|---|---|---|
| molten canal | `mods/iiex/src/BlockNetworkMolten/Blocks/BlockMoltenCanal.cs:253-256` | per-cell glow |
| molten barrel | `.../Blocks/BlockMoltenBarrel.cs:95-102` | scaled to stored temperature |
| cast mold | `mods/iiex/src/BlockStructures/Casting/Blocks/BlockCastMold.cs:103-108` | its own comment names the idiom: `GetLightHsv` + `MarkBlockDirty`-on-change, "as the molten barrel and the canals" |
| cowper heat sink | `mods/siex/src/BlockStructures/CowperStove/Blocks/BlockHeatsink.cs:38-53` | smex's copy |
| the shared scale | `mods/exlib/src/Metals/MoltenMetal.cs:155-162` - block light 0–24, floored by `MetalGlowMinTemp` = 500 °C (`mods/exlib/src/ExlibConfig.cs:70`) | exlib owns the scale; a lamp would be a constant on it instead of a temperature function |

### What it is up against, in vanilla

| Vanilla fixture | Light | Cost | Fuel |
|---|---|---|---|
| `game:lantern` | `lightHsv: [7, 3, 18]` (`.game/1.20/assets/survival/blocktypes/metal/lantern.json:114`) | 1 metal plate (copper and up) + 2 `clearquartz` + 1 candle, one 3×2 grid recipe (`.game/1.20/assets/survival/recipes/grid/lantern.json`) | none - permanent |
| `game:chandelier` | up to `[9, 3, 24]` at `chandelier-candle8` (`.game/1.20/assets/survival/blocktypes/metal/chandelier.json:30-39`) | candles | none |

24 is the top of the block-light scale - the same 0–24 exlib uses for glowing metal. Vanilla's free-standing,
fuel-free fixtures already reach the maximum, and a lantern gets to 18 of 24 for one plate and two quartz.

## The design as it stands

The archived spec is two rows:

| Block | What it is | IO |
|---|---|---|
| **Gas lamp** | block (fixture) | pipe coal gas → light |
| **Gasholder** | "the core-iiex medium-agnostic storage node holding coal gas (telescoping bell cosmetic)" | pipe ↔ bulk gas buffer - keeps lamps lit when the gasworks idles |

Plus one framing line and one product line:

* coal gas comes from the gasworks, "never the coke oven, which makes coke only". That is the same mistake
  [coke oven](../../machines/coke-oven.md):33-38 and [gasworks](gasworks.md) both exist to prevent,
  pre-empted inside the lighting spec.
* kerosene → kerosene lanterns as items, the portable sibling of the fixed lamp. The archived design had two
  lighting products from the same add-on, one piped and one carried.

The gasholder is not a new block. The archived spec makes it the same medium-agnostic storage node as the
[fluid tank](../../machines/fluid-tank.md) with a telescoping-bell skin, and that page owns why it cannot be
built today (capacity is per-node and uniform). `fluid-tank.md:56-58` cites this archived gasholder as one of
the two reasons the tank must stay medium-agnostic.

### The design problem, and it is not the scope one

A gas lamp has to justify a gasworks, a main, a holder and a continuous fuel draw, against a fixture the
player can already craft from one plate and never think about again. R5 says gate efficiency, not possibility
([conventions.md](../../conventions.md)), so gas lighting must never block light; it can only be better. But
"better" in raw brightness is nearly foreclosed: a lantern is at 18 and the ceiling is 24.

So the only lever is coverage per player action, not brightness per block. One main lit from one works
illuminates a whole shop floor, a whole yard, a whole street - the player builds a lighting system once
instead of crafting and hanging fixtures one at a time. That is the suite's own pillar - you buy operating
efficiency with build complexity ([overview.md](../../overview.md)) - applied to light.

Gas lighting fits the suite's design language and its scope not at all. The deferral is not a judgement on the
feature's quality; the reason it is out is [scope.md](../../scope.md).

## What it would unblock

Nothing.

| Homestead line | Who outside it waits |
|---|---|
| [gasworks](gasworks.md) → coal tar → pitch | elex's graphite electrodes (degraded path - [scope.md](../../scope.md)) |
| [oil](oil.md) → petcoke | same electrode |
| [chemistry](chemistry.md) → acid, sulfur | elex, smex-copper - and both turn out to be vanilla-supplied |
| gasworks → ammonia | Homestead's own refrigeration ([climate-control](climate-control.md)) |
| gas lighting | nobody, in any mod, at any tier |

Every other deferred line has at least one waiting consumer, which is why they get severity ratings. Gas
lighting is a pure leaf: it consumes coal gas and produces photons, and no recipe, machine or material in
`exlib → iiex → iiex → smex → hpex → elex` reads either end of it.

## Gotchas

* **Every lamp is a network node.** Run capacity is `nodes × LitresPerPipe`
  ([pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md)), so a long lighting main is itself a gas holder - the
  same observation [gas-producer](../../machines/gas-producer.md):316-320 makes when it argues "never stored"
  is a decision, not a mechanism. For lighting the accident inverts: the buffering the mains give for free is
  exactly what the holder was for. Do not "fix" it without noticing that.
* **Two gases always mix, silently** (`mods/exlib/src/Fluids/ExLiquids.cs:106-115`, comment at `:114`).
  A domestic lighting main threaded through a works will pass near air and exhaust runs, and one accidental
  junction merges the pools and relabels by priority (`:118-119`). Lighting is the worst case because the
  mains are long and everywhere, unlike a producer's short lock-step feed.
* **Coal gas is carbon monoxide, and a leaking run leaks by design.** Open-connector detection and the leak
  model are live ([pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md)). A lighting network is the densest,
  longest, least-supervised pipework a player would build. R7 ([conventions.md](../../conventions.md)) says
  the leak must be visible, not a silent poison.
* **A lit/unlit lamp must read at a glance.** R7 again. A lamp that is out is visibly out, but block info
  should still say why (no gas, no pressure, valve shut), not just dark.
* **[conventions.md](../../conventions.md) § Networks lists coal gas as a pipe medium.** Capability, not
  scope; already logged at [scope.md](../../scope.md).
* **`mods/exlib/assets/exlib/config/liquids.json` declares four media** - Air, Steam, Exhaust, Water - so there is no
  medium a lamp could consume today even if the block existed. Owned by [gasworks](gasworks.md) § knots.

## Open

*(All of these are Homestead's to answer. None blocks anything in this suite - see § What it would unblock.)*

| # | Question | Notes |
|---|---|---|
| 1 | **Brightness and radius** | no number was ever proposed, anywhere. The anchors are vanilla's lantern at 18 and the 0–24 ceiling (`MoltenMetal.cs:155-162`) |
| 2 | **Flame or mantle - one tier or two?** | An Argand burner and a Welsbach mantle (1885) are ~80 years apart and materially different in output. Two tiers is historically richer; one is cheaper |
| 3 | **Continuous draw, or gate on a lit state?** | Historically the tap stayed on. Mechanically, a per-lamp continuous draw across a large main is the load model that makes the holder matter |
| 4 | **Is the gasholder the [fluid tank](../../machines/fluid-tank.md)?** | The archived spec says yes. Do not design a second storage node |
| 5 | **Do kerosene lanterns ship with it?** | the archived spec pairs them. They need [oil](oil.md), not the gasworks - a different dependency for the same product |
| 6 | **Does gas lighting stay a leaf?** | It should. The moment something in the metalworking line needs light to function, this page stops being the clean example |
