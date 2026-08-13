# Extending Processes

Our machines do not know what they make. Every one of them reads a **registry**, and a registry is
something you contribute to — from your own mod, with your own domain, without our source and without a
fork.

> **Tooling carries its own spec. The machine reads it and names no product.**

A source guard enforces it: no machine may write a product code in C#. That is what makes this page a
contract rather than a promise.

## The two shapes

Every machine's registry is one of two shapes, and which one it is follows from the process.

| Shape | Means | Machines |
|---|---|---|
| **Terminal** | one input, one job, one output — **× a count** | shear, drill, lathe, shaper, planer, nail and rivet machines, sand casting, the design table |
| **Sequence** | a ladder the work walks, carrying state between steps | the rolling mill and the bending roller, and nothing else |

The count is not cosmetic. The shear crops one rod into **four** rods, so a job that could name only one
output could not express the crop table at all.

Both are **merged catalogues**. Your file and ours land in the same registry, so adding a machine family
and adding a stock family cost the same — neither requires patching the other.

---

## Terminal: a job table

Drop a file at `assets/<yourdomain>/config/processjobs/<anything>.json`. One file per machine is the
convention; nothing enforces it.

```json
{
  "schema": 1,
  "machine": "shear",
  "jobs": [
    { "input": "yourmod:bronzestrip", "output": "yourmod:bronzerivet", "count": 6 },
    { "input": "yourmod:bronzebar", "stage": 2.0, "family": "grooved",
      "output": "yourmod:bronzerod", "count": 4, "minTorque": 0.3 }
  ]
}
```

| Field | Meaning |
|---|---|
| `machine` | which machine these jobs belong to. Required |
| `input` / `output` | item codes. Required |
| `count` | how many outputs one job yields. Defaults to 1; must be at least 1. On a **staged** job this is the whole piece's yield and one leaves per stroke; on a whole-item job they all leave at once |
| `stage` / `family` | optional, and go together: take a piece part way down a ladder, at that gauge on that branch. Omit both to take the whole item |
| `minTorque` | drive torque the machine needs for this job. 0 when it is not gated |

A second job on one input is **reported in the log and ignored** — the first declaration stands. Taking
the last writer would make the outcome depend on mod load order, which nobody can reproduce.

### A staged job crops; a whole-item job converts

Whether you gave the job a `stage` decides what happens to the input, and it is the only thing that does:

| Job | The input | `count` reads as |
|---|---|---|
| **staged** (`stage` present) | **survives**, still stock at the same gauge, with one more crop tallied against it | the whole piece's yield |
| **whole-item** (no `stage`) | **consumed** | what one conversion produces |

Your `count` is your number and nothing checks it against geometry — what a piece divides into is a design
choice, not something we can calculate for you. Two things follow that are worth knowing before you pick it:

- **The count stays live.** The piece tallies crops *taken*, so raising a `count` from 4 to 6 gives every
  piece already in a player's world the two extra crops rather than stranding it on the old number.
- **A part-worked piece cannot re-enter a sequence.** Crop a bar twice and the rolling mill will refuse it
  until it is cut out. The tally is against *that stage's* count, so a part piece carried to the next stage
  would be worth the next stage's whole count again. Finish the cut, then roll the pieces on.

---

## Sequence: a stage ladder

A ladder is one stock family's states: every gauge it can be worked to, across every machine family that
works it. Drop a file at `assets/<yourdomain>/config/stageladders/<anything>.json`.

```json
{
  "schema": 1,
  "family": "bronzebar",
  "shape": "yourmod:item/bronze-bar",
  "stages": [
    { "thickness": 2.50, "element": "Grooved250",  "acceptedBy": ["grooved", "flat"] },
    { "thickness": 2.00, "element": "Grooved200",  "acceptedBy": ["grooved"], "code": "yourmod:bronzerod" }
  ]
}
```

**`code` present means a stopping point**, and an item is generated for it. **`code` absent means a
render-only intermediate** — the same item at a different gauge, drawn from the stack's own thickness. One
field drives the item catalogue, the machine's stopping points and the held-item appearance together.

**`acceptedBy` makes the ladder a graph, not a line.** A stage several families accept is a fork: the same
piece at the same gauge continues one way on grooved rolls and another on flat ones. That is the mill's
whole point, and a line could not express it.

**One rung per gap, not per pass.** A gap does cost two trips — in, turned, and back — but the mill lands
the half-step between them by arithmetic, and you do not declare it. `3.00 → 2.75 → 2.50` is the *walk*;
`2.50` is the *rung*. Declare the half-steps and the mill will offer them as gaps of their own, which is a
barrel with twice the grooves you meant.

A gauge no rung names is drawn by scaling the family's base shape, so an undeclared half-step still looks
part-worked in the hand.

### Why `config/` and not an item attribute

Items are *generated* from stopping points, and that has to happen before the game builds itemtypes. A
ladder carried on an itemtype could not be read in time: you cannot build an itemtype from data that lives
on an itemtype.

⚠️ One consequence worth knowing: **JSON-patching our catalogue adds a route but no item**, because the
patch lands after generation. To add a stopping point, ship your own file — the merge puts it in the same
family.

---

## What gets built for you

A stage naming a `code` becomes an itemtype automatically:

- **your domain, not ours** — the owning domain comes from the code you declared;
- **defaults for everything** — a sparse declaration still yields an item that loads and is reachable: the
  family's shape drawn at the stage's `element`, a stack size, a creative-tab entry;
- **`"generate": false`** when the code already exists — wire it up, build nothing;
- **the `game:` domain is never built into.** Pointing at a vanilla item such as `game:rod-iron` wires it
  up; declare `"generate": false` to say so explicitly and keep it out of the log.

⚠️ **Lang keys cannot be generated.** An item with no `item-<code>` entry displays its raw code. Ship your
own strings — this is the one part that is not automatic.

⚠️ **A generated code is save data.** Once an item exists in someone's world the rule that produced its
code is frozen, which is why you state the code explicitly and we never compute one.

### Renames are declared, not detected

```json
{ "code": "yourmod:nailplate", "formerCodes": ["yourmod:oldnailplate"] }
```

We see only the current catalogue, so a code that vanished and one that appeared are indistinguishable
from a rename. Declare the old code and the stack migration is free.

---

## Shapes: both conventions work

`element` is optional.

- **Present** — the stage is one element of a family shape file. Right for a progression: thickness falls
  and length grows across the elements, a relationship a reader can check at a glance in one file.
- **Absent** — the whole shape file is the stage. Right for a finished product with its own model.

⚠️ `selectiveElements` matching is the engine's per-segment prefix rule, so naming an ancestor keeps more
than intended and naming an element exactly drops its children. Flat, distinctly-named top-level elements
are safe.

By convention an element drawn **off the shared origin** is not a stage — it is another machine's output,
and it belongs in that machine's registry.

---

## The C# route

JSON is the primary path and needs no dependency on us. If you would rather compute a spec at load, take
the dependency and call the same surface:

```csharp
using ExpandedLib.Processes;

// Sequence
ProcessExtensions.Shared.AddStages("bronzebar", [
    new ProcessStage(2.0f, "Bronze200", ["flat"], null),
    new ProcessStage(1.0f, "Bronze100", ["flat"], "yourmod:bronzeplate"),
], shape: "yourmod:item/bronze-bar");

// Terminal
ProcessExtensions.Shared.AddJobs("shear", [
    new ProcessJob("yourmod:strip", "yourmod:rivet", Count: 6, Stage: null, Family: null, MinTorque: 0f),
]);
```

Both return the clashes they hit, empty when the contribution was taken whole, and both throw
`ArgumentException` on a declaration the JSON route would also have refused — the code path builds the
same declaration and runs it through the same parser, so there is one set of rules rather than two.

This surface is deliberately no wider than the JSON schema. Anything expressible only in C# is a gap in
the schema, and the schema should grow instead — [open an issue](https://github.com/ringavirda/modding-vsexpanded/issues).

---

## Schema stability

Every spec carries a `schema` number, and we promise to read every form that has shipped.

| You declare | We read it as |
|---|---|
| nothing | schema 1 — the form that shipped before the field existed |
| an older schema | itself, through the fallback for that form |
| a newer schema | **refused**, with an error naming both numbers — update the library |

The alternative, freezing at release and going additive-only, was cheaper for us and worse for you: it
makes your content break on our schedule.

---

## When something does not appear

| Symptom | Cause |
|---|---|
| the machine refuses your piece | no stage its fitted tooling's family accepts, or the tooling does not accept that stock at all |
| your item shows its raw code | no `item-<code>` lang entry — ship your own strings |
| your patch added a route but no item | patches land after item generation; ship your own catalogue file instead |
| nothing at all, and the log says "invalid stage ladder" | the message names the file and the field |
| your stage was ignored | someone declared that `(thickness, family)` first; the log names the clash |
