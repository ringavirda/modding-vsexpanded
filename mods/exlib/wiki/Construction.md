# Construction (Right-Click Constructable)

`Blocks/Construction/` wraps Vintage Story's right-click-construction (RCC) flow so it behaves
consistently across game versions and so broken constructions scatter a configurable fraction of
their build materials.

## `ExRightClickConstructable`

Per-block construction state and logic. On **1.22** it is a thin subclass of vanilla
`BEBehaviorRightClickConstructable`; on **1.20 / 1.21** it is a full reimplementation. Either way
you register it under the behaviour name `"ExRightClickConstructable"` (no mod prefix - exlib owns
that JSON name on all versions), so your block JSON is version-agnostic.

```jsonc
"entityBehaviors": [ { "name": "ExRightClickConstructable", "properties": { /* stages */ } } ]
```

Public surface:

```csharp
// Available on all versions:
public ItemStack[] GetConstructionDrops(float ratio, Random rand);   // materials scattered at `ratio` (0..1)
public WorldInteraction[]? GetConstructionInteractionHelp();         // next-stage build-material hover help

// 1.20 / 1.21 only (the reimplementation exposes extra state):
public bool IsComplete { get; }
public CompositeShape shape { get; }
public event Action<CompositeShape>? OnShapeChanged;
public static WorldInteraction[] AppendConstructionHelp(IWorldAccessor world, BlockSelection selection, WorldInteraction[] baseHelp);
```

`GetConstructionDrops(ratio, rand)` returns the materials this block would scatter at the given
fraction of consumed stacks, summed across **every completed stage** - use it to salvage a
partially built or finished construction when it is broken.

> **Rendering caveat.** The RCC behaviour draws no mesh of its own. A constructable block needs a
> companion animator with an always-on idle animation to be visible - without it the block is
> invisible mid-construction.

> **Wildcard ingredients.** Stages with wildcard `requireStacks` ingredients must set
> `storeWildCard`, or breaking the block NREs inside vanilla `GetDrops`.

## Construction gates production

`ExRightClickConstructable` publishes `IProductionReadiness` (see [Production
Machines](Production-Machines)): `IsReadyToProduce` is `IsComplete` and `StopsProductionWhenNotReady`
is `true`. A host that carries the behaviour and hosts a production tick gets this for free - declare
the stages and the tick already waits for them, with no gate to write by hand.

```jsonc
"entityBehaviors": [
  { "name": "ExRightClickConstructable", "properties": { "stages": [ /* ... */ ], "gatesProduction": false } }
]
```

A machine that must keep ticking while unfinished (rendering a break, holding a status message) sets
`gatesProduction: false`, either in raw JSON or through the definition builder:

```csharp
.Construction(c => c.Stage(s => s.AddElements("frame")).GatesProduction(false))
```

With the opt-out, `IsReadyToProduce` is always `true` and `StopsProductionWhenNotReady` is `false` -
the behaviour publishes nothing that blocks or stops the tick, while `IsComplete` still answers
whatever else reads it directly (rendering, `GetBlockInfo`).

## `ExRccSettings`

A small registry letting each mod expose a **player-tunable salvage fraction** for its broken RCC
mega-blocks, resolved at break time by the block's `Code.Domain`.

```csharp
public static class ExRccSettings
{
    public static void RegisterBrokenDropsRatio(string domain, Func<float> ratio);
    public static float? BrokenDropsRatio(string domain);   // null if the domain didn't register
}
```

Register at startup, wiring the getter to your [config](Config-System) so players can tune it live:

```csharp
ExRccSettings.RegisterBrokenDropsRatio("iiex", () => IiexValues.BoilerSalvageRatio);
```

Then in your block's `OnBlockBroken`, scatter `GetConstructionDrops(ratio, rand)` where
`ratio = ExRccSettings.BrokenDropsRatio(Code.Domain) ?? defaultRatio`.

> **RCC drops come from `OnBlockBroken`, not `GetDrops`.** Vanilla RCC scatters its build
> materials from its own `OnBlockBroken`, and `Block.GetDrops` returns nothing. A custom salvage
> ratio for, say, a burst boiler needs to call back into the protected `rcc.GetDrops(ratio, rand)`
> via reflection. Note also that a mega-block's frame self-drop is controlled by overriding
> `GetDrops` to return `[]` on the controller - JSON `drops: []` is **not** honoured for variant
> blocks.

## Related pages

- [Config System](Config-System) - back the salvage ratio with a live-editable value.
- [Recipe Costs](Recipe-Costs) - RCC stage costs are also adjustable per cost profile.
