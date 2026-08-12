# Orientation schemes

**Status** analysis, 2026-08-03 - nothing built beyond the partial support described under *What exists today*
**Mod** exlib (the registry would live there; every mod declares against it)

**Owns** - the facts this page is canonical for:

* the complete inventory of orientation spellings in the suite, and which blocks use each;
* the one rotation rule that covers all of them, and the proof it does;
* why the layout DSL cannot get this right by parsing the code string, and what it needs instead;
* the valve/canal directed-axis question and what dropping it would and would not buy.

**Does not own** - cited only: the oriented-parts feature and `multiblockFacings`
([multiblock](multiblock.md)) · the rotation convention itself, north 0° / west 90° (`ExOrientation`) ·
the layout scratchpad ([layouts-workbench.md](../../internal/workbench/layouts.md)).

---

## The problem

A multiblock layout can require a part to be placed the right way round: it reads the facing out of the block
code and rotates it with the structure. That works for a facing (`brickslabs-fire-south-free`), and the four
furnace layouts already rely on it.

Network nodes do not spell a facing. They spell the set of faces they connect, and there are at least six
different spellings for it. The spellings overlap - `ns` means one thing on a pipe and a different thing on a
valve, and nothing in the string says which. So the string cannot be parsed; the block has to declare its
scheme.

---

## The inventory

Every `VariantGroup("orientation", …)` and `side` group in the suite, grouped by the shape of its tokens:

| Scheme | Tokens | Declared by |
|---|---|---|
| **Face** (horizontal) | `n e s w` | tuyere, twin-tub blower, fluid intake, smokestack intake, molten-canal tap |
| **Face** (all six) | `n e s w u d` | pipe outlet |
| **SideWord** | `north east south west` | tall hopper, cowper intake, and vanilla's coke-oven door |
| **Axis** | `ns we ud` | pipe straight, pipe passthrough, cast-iron shaft, cast-iron bevel |
| **Axis** (horizontal) | `ns we` | flywheel, rolling mill, mill axle, canal straight/pass |
| **DirectedAxis** | `ns we ew sn` | molten canal ends (`PassOrEndOrientations`) |
| **DirectedAxis** (+vertical) | `ns we ud sn ew du` | valve, pressure valve |
| **Bend** - two adjacent faces | `nw se en ws` + `un us uw ue` + `dn ds dw de` | pipe bend (12), canal bend (4) |
| **Tee** - three faces | `uns uwe dns dwe nes esw swn wne dnu deu dsu dwu` | pipe T-junction (12), canal T (4) |
| **Cross** - four faces | `nswe nsud weud` | pipe X-junction (3), canal X (1) |

Spelling is not canonical. The bend writes `en`, not `ne`; `ws`, not `sw`. The tee writes `uns` in one family
and `dnu` in another for the same kind of arrangement. These came from whichever rotation of the shape was
authored first, and they are the reason a general parser cannot be written.

---

## One rule covers every scheme

> **Rotate each direction letter, preserving order. If the result is a declared token, use it. Otherwise use
> the unique declared token with the same face *set*.**

The algorithm needs nothing from a block except the list of tokens it declares, which the block already
writes in its `VariantGroup`.

Worked through every scheme at 90° (north → west):

| Scheme | Input | Ordered rotation | Declared? | Result |
|---|---|---|---|---|
| Face | `n` | `w` | yes | `w` |
| Axis | `we` | `sn` | no | set `{s,n}` → `ns` |
| DirectedAxis | `we` | `sn` | yes | `sn` - direction survives |
| Bend | `nw` | `ws` | yes | `ws` |
| Tee | `uwe` | `usn` | no | set `{u,s,n}` → `uns` |
| Cross | `weud` | `snud` | no | set `{s,n,u,d}` → `nsud` |

The two-step is what makes one rule enough. The ordered step preserves direction exactly where a scheme
declares both spellings; the set fallback repairs the non-canonical spellings everywhere else. Neither step
alone works: ordered-only breaks the axis and the tee, set-only cannot tell `ns` from `sn`.

The set fallback requires one token per face set within a scheme. True for every scheme above except the
directed ones, where the ordered step catches it first and the fallback is never reached.

---

## What exists today

`ExOrientation.IsOrientationToken` / `RotateOrientationToken` handle Face, SideWord and Axis, and
canonicalise. That covers pipes, passthroughs, shafts, bevels, flywheels and mills.

Bend, Tee and Cross are not recognised at all - `nw` and `uns` fail the axis-pair grammar, so a layout
pinning a bend is not orientation-checked, and the gap is silent.

The directed schemes are actively mishandled. `RotateOrientationToken("sn", 90)` returns `we`, not `ew` - it
canonicalises, because with only the string to go on it cannot know the block declares both. That is
documented and tested as a known limit, and a layout needing it must use `LegendAnyFacing`.

---

## A multiblock must not orientation-check a self-orienting node

For most nodes a layout should not pin the orientation at all; pinning one would be actively harmful.

A network node's orientation is not the player's choice.
`BlockNetworkNode.OnNeighbourBlockChange` calls `RecalculateAndSyncOrientations`, and
`BlockMoltenCanal.PickBestOrientation` picks whichever declared token best matches the connectors around it.
The node re-orients itself whenever a neighbour changes.

Two consequences:

1. **The structure could never be completed.** If the layout demands `pipe-straight-fire-ns` and the node
   decides `we` from its connections, the cell is never satisfied - and the player cannot fix it, because
   placing it "the other way round" does not stick.
2. **A complete structure could silently come apart.** Connect a pipe somewhere else in the world, the node
   re-orients, the cell stops matching - and for a furnace, incomplete means extinguish. A player would
   experience their furnace going out because they plumbed something unrelated nearby.

The dividing line is not "facing vs node token". It is: who decides the orientation?

| Orientation decided by | Examples | Orientation-check? |
|---|---|---|
| The player, fixed at placement or by wrench | slabs, stairs, coke-oven doors, tall hopper, cowper intake, furnace cores | yes - this is what the feature is for |
| The network, from a free run of neighbours | pipe straight/bend/junction, passthrough, molten canal mid-run | no - pin the block, never the orientation |
| The network, but walled in by the structure | tuyere, pipe outlet - a single-faced node embedded in a furnace shell | yes, and it is needed - see below |

The one node code a shipped layout pins today, `lpex:pipe-outlet-fire-u`, is harmless: it is vertical, so no
rotation and no recalculation can move it.

### The embedded connector is the case that must be checked

A tuyere or a gas outlet sits in the furnace wall, with the furnace on one side and the player's pipework on
the other. Placed backwards, its connector faces the furnace interior and the blast pipe joins to nothing.

The node's own logic does not prevent it. `BlockNetworkNode.ComputeValidOrientations` marks a face forbidden
only when the neighbour is a compatible network block with no connector back. A plain solid neighbour -
refractory brick - falls through to a branch that only ever adds a face as required, and only when the
current orientation already points at it. So nothing stops a tuyere facing into the brick, and nothing later
turns it round.

Unlike a free-run pipe, the required facing here is a property of the layout geometry, not of the world: the
drawing knows which side of that cell is furnace interior, and that never changes, so the check is stable.

The residual risk is narrow: a single-faced node re-picks only when it gains a network neighbour on another
face, and every other face of an embedded tuyere is brick. Reachable only by deliberately plumbing into the
furnace shell.

Two ways to express it, differing in robustness rather than difficulty:

| | |
|---|---|
| **Pin the cardinal** | Uses the oriented-parts mechanism exactly as it stands. Cheap. Breaks if the node ever legitimately re-picks |
| **Require "connector faces out"** | The layout marks the cell; the check asks the node whether its connector points away from the structure. Immune to re-orientation as long as it still faces out, which is the actual requirement |

The second is what the fiction means and what the player expects. It is also the subset test described below,
narrowed to one face.

### What a layout actually wants from a node

Not "this pipe faces north" but "there is a pipe here that connects along this axis" - a subset test, not an
equality test. The node's chosen token contains the faces the structure needs; it may contain more.

That is a different feature from oriented parts, and it needs the same registry: given a scheme's token list,
"does token `T` include face `f`" is a lookup. It also composes correctly with self-orientation - a node that
re-orients to serve more connections still satisfies a subset check.

It is not built, and nothing in the DSL expresses it today.

---

## What it needs: a declared scheme, not a parsed string

Name the schemes once, and have blocks reference them instead of listing states inline:

```csharp
.VariantGroup("orientation", ExOrientations.Axis)          // was "ns", "we", "ud"
.VariantGroup("orientation", ExOrientations.PipeBend)      // was 12 hand-typed tokens
```

That buys three things at once:

1. **The layout builder gets the token list**, so the one rule above becomes implementable. A code's scheme
   is found by looking its token up in the registry rather than guessing from its shape.
2. **The tokens stop being retyped.** The pipe bend's 12 and the T-junction's 12 are written once instead of
   once per block, and the canal's horizontal-only subsets become an explicit subset of rather than a
   coincidentally-similar list.
3. **Ambiguity becomes detectable.** Two schemes sharing a token (`ns` in `Axis` and `DirectedAxis`) is
   exactly the case that cannot be resolved from the string, and a registry can fail the build when a layout
   pins such a token without saying which scheme it means.

This is the same move [`ExCodes`](../../internal/workbench/layouts.md) made for block codes, applied to variant states.

---

## The directed-axis question

Dropping the valve's flipped variants would not by itself resolve the ambiguity - the molten canal declares
`ns we ew sn` too (`PassOrEndOrientations`), for what may be a genuine reason: a canal end points somewhere,
and which way it points is not cosmetic.

The valve's case is the one worth acting on. Its reversed spellings exist so a wrench can flip which side the
handle sits on - an appearance choice currently encoded as a distinct orientation. If the handle side can be
held somewhere other than the orientation variant (a block-entity flag driving a shape swap, or a second
variant group of its own), the valve collapses to plain `Axis` and:

* six variants become three, halving the valve's blocktype count;
* the valve stops being an exception in every rotation path that touches it;
* the flip stops being lost on rotation - today, turning a structure containing a valve canonicalises its
  direction away, a bug the layout DSL sidesteps rather than fixes.

The canal end must be settled separately, and on different grounds: whether `ns` vs `sn` on a canal end
encodes flow direction (keep it) or merely which end the lip is on (fold it into appearance, like the valve).

---

## Cost, roughly

| Step | Size |
|---|---|
| `ExOrientations` registry + the two-step rotation rule + tests | small - the rule is ~30 lines and the schemes are a table |
| Point ~18 blocks' `VariantGroup` calls at it | mechanical; no golden movement if the token lists are copied exactly |
| Teach the layout builder to resolve a token → scheme, and fail on ambiguity | small |
| Valve appearance split | real work - a variant group moves, so goldens and a block migration |

Steps 1-3 move no goldens and are independently useful. Step 4 is a separate decision.
