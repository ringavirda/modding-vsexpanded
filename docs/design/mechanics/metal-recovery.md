# Metal recovery

**Status** live for the blast-furnace rung (`BfIronPerOreUnit`, shipped 2026-08-07 with the burden chain);
the roasted rung is designed, not built
**Mod** iwex carries the code; the ladder itself is cross-cutting

**Owns**

* the ore-to-metal recovery ladder - every recovery fraction between rock and first metal - and the rule
  that each fraction is stated once, here, and cited everywhere else;
* the anchor derivation from vanilla's bloomery;
* which code carries each number, and the currency it must be stated in;
* the guard-rail invariant that pins the ladder's floor.

**Does not own** - cited only, never restated: what recovery is spent on per tier and why it is spent once
([conventions](../conventions.md) § Which tier moves which number); refining losses downstream of the first
melt (puddling's tap cinder, rolling's mill scale) and the by-product rule they follow
([conventions](../conventions.md) § metal recovery, R2); the roasting process and the seam it attaches to
([roasting](../processes/roasting.md)); the melt condition and production rate
([heat-balance](heat-balance.md)).

**Depends on** vanilla's own bloomery rate - nothing else.

---

## The ladder

Vanilla's 5 units per nugget is not the ore's iron content; it is a bloomery's share of it. A real
bloomery threw 40–60 % of the iron into its slag, so the anchor derives itself:

```
true content = 5 u (vanilla, per nugget) ÷ 0.50 (bloomery recovery) = 10 u per nugget
```

Every process is a recovery fraction of that 10:

| Route | Recovery | Iron per nugget | Status |
|---|---|---|---|
| Bloomery (vanilla, untouched) | 50 % | **5** | live - vanilla's own number, no patch |
| Blast furnace, raw ore | ~85 % | **8.5** | live - `BfIronPerOreUnit` |
| Blast furnace, roasted ore | ~92 % | **9.2** | designed - [roasting](../processes/roasting.md) is unbuilt; no key exists |

The bloomery still yields exactly 5; its historical loss is modelled purely by being the floor everything
else is measured against.

## What owns each number in code

| Number | Where | How it is applied |
|---|---|---|
| **8.5** | `IwexConfig.cs:470` (`BfIronPerOreUnit`) | iron units per unit of ore content in the melted burden; the shaft furnace scales it by the band's own ore share (`BlockEntityShaftFurnace.cs:174-175`) |
| **8.5 / 6** | `IwexConfig.cs:478` (`BfSlagPerOreUnit`) | slag rendered alongside the iron, holding a 6:1 iron-to-slag ratio |
| **5** | vanilla | untouched |
| **9.2** | nowhere | ships only when roasting does |

The chain is one nugget → one crushed item → one burden item (the burdenmaker is 1:1), so "per unit of ore
content" and "per nugget" are the same number. The cupola does not participate: it remelts rather than
reduces, so there is no ore share in its charge and it overrides the rate flat.

## The rules

1. **A recovery fraction is stated once - on this page.** Every other page, config comment and handbook
   entry cites it. A fraction restated is a fraction that drifts.
2. **State recovery against ore content, never per burden item or per band.** The ore share of a band is a
   separate number that moves when the burden recipe does (taking coke out of the burden lifted it from
   ~0.75 to ~0.94); a per-band restatement bakes a snapshot of it into a constant.
3. **The floor is pinned.** The iwex chain must never yield less iron per ore than a vanilla bloomery.
   `OreRecoveryGuardRailTests` derives the chain's effective per-nugget yield from the shipped constants and
   asserts it stays at or above 5 - the one test that stops a recovery rebalance quietly turning the mod
   into a downgrade.
