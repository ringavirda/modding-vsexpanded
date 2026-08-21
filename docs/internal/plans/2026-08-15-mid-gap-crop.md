# The mid-gap crop — the ruling, built

**Status** live, written 2026-08-15, nothing started. Independent of everything else in flight: it touches
no form, no item, no content row and none of the [item-piles](2026-08-15-item-piles.md) work. Runnable any
time.

> **For agentic workers:** REQUIRED SUB-SKILL: use `superpowers:subagent-driven-development` or
> `superpowers:executing-plans`. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal** Make *a crop may yield stock* real, guarded and safe to publish — so the two declared cast routes
that need it (`castbillet` grooved 2.25 ×6, `castbloom` wide 3.0 ×5) become authorable content rather than
a blocked mechanism.

**Architecture** The capability already exists by construction and nothing proves it, which on a published
contract is the same as not having it. `WorkPiece.FromStack` falls back to the item type's `stockForm`, so
a stack of `iiex:stock-X` is already a fresh work piece with `Cropped = 0`. Three changes: pin that with a
test, carry heat across a crop so the yielded piece is workable, and refuse at load the one declaration
that is always a duplicator.

**Tech Stack** C# / .NET, Vintage Story API, xUnit + NSubstitute.

**Spec** The ruling is recorded across
[process-extension.md](../../design/mechanics/process-extension.md) § *What a count means*,
[shear.md](../../design/machines/shear.md) § *Open*, and
[rolled-parts.md](../../design/items/rolled-parts.md) § *What is reachable today*. Task 4 writes it up in
one place.

## Global Constraints

- ⛔ **Never commit.** Leave changes in the working tree; record in `docs/internal/worklog/2026-08.md`.
- Test entry point is `scripts/exmod.ps1 test` / `scripts/exmod.sh test`. `scripts/run-tests.sh` does not
  exist.
- The gate is 9 targets across 1.20 / 1.21 / 1.22; run `scripts/exmod.ps1 test all` before calling a task
  done.
- Comments describe, never narrate. Neutral repo voice.
- ⛔ **`FeedVerdict.PartCropped` is not touched by this plan.** It refuses the *remainder*, which is
  correct: the cut-off piece is a whole piece, not a remainder. Weakening it re-opens metal-from-nothing.
- ⛔ **No content rows.** The two cast crop rows are siex's and land in the siex pass (owner ruling,
  2026-08-15: focus on iiex). This plan ships the mechanism and nothing that uses it.

---

## File structure

| File | Responsibility |
|---|---|
| `src/IronIndustryExpanded/BlockStructures/Forming/BlockEntities/BlockEntityShear.cs` | carries the input's heat onto what the stroke yields |
| `src/ExpandedLib/Processes/ProcessJob.cs` | refuses a staged job whose output is its own input |
| `src/IronIndustryExpanded/BlockStructures/Forming/WorkPiece.cs` | names the fresh-stack read path for what it is |
| `test/IronIndustryExpanded.Tests/Blocks/Forming/ShearStationTests.cs` | heat carried; a stock-yielding crop is rollable |
| `test/ExpandedLib.Tests/Processes/ProcessJobTests.cs` | the duplicator refused at load |

---

## Task 1: Heat carried across a crop

`CompleteStroke` ejects `Resolve(job.Output, …)`, which builds a bare stack with no temperature — so a rod
cropped off a bar at rolling heat lands cold. The mill already gets this right in both `Admit` and
`ClaimFinishedPiece`; the shear never did. It is cosmetic for a finished product and load-bearing the
moment a crop yields stock, because a piece that has to be rolled on must arrive hot.

The remainder needs nothing: `Remainder` hands back the same stack, so its temperature survives.

**Files:**
- Modify: `src/IronIndustryExpanded/BlockStructures/Forming/BlockEntities/BlockEntityShear.cs` —
  `CompleteStroke`, and a new private helper beside `Resolve`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Forming/ShearStationTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: no public surface; the observable change is that a stroke's output carries the input's
  temperature.

- [ ] **Step 1: Read the existing stroke test**

Open `ShearStationTests.cs` and find the test that drives a full stroke and inspects the ejected product —
it already builds the world through `Shear(Registry(...))`, loads stock, and advances the stroke. The new
tests copy its arrangement exactly; only the assertions below are new. Note the helper it uses to read
ejected stacks out of `TestWorld` and use the same one.

- [ ] **Step 2: Write the failing tests**

```csharp
  #region Heat

  // A cropped piece is as hot as the piece it came off. Cosmetic for a product; the whole game for a
  // crop that yields stock, since the pieces have to go back through the rolls.
  [Fact]
  public void A_crop_hands_its_product_out_at_the_pieces_own_heat() {
    // arrange exactly as the existing stroke test does, with the input at rolling heat
    // (set the stock stack's temperature before loading it), then drive the stroke to completion.
    // assert:
    Assert.Equal(1100f, TemperatureOf(product), 1);
  }

  [Fact]
  public void The_remainder_keeps_the_heat_it_had() {
    Assert.Equal(1100f, TemperatureOf(remainder), 1);
  }

  #endregion
```

Add the reader beside the class's other private helpers:

```csharp
  private static float TemperatureOf(ItemStack stack) =>
    stack.Collectible.GetTemperature(World, stack);
```

- [ ] **Step 3: Run them and confirm they fail**

Run: `scripts/exmod.ps1 test iiex`
Expected: `A_crop_hands_its_product_out_at_the_pieces_own_heat` FAILS reading ambient (20) against 1100.
`The_remainder_keeps_the_heat_it_had` should already PASS — it is the premise, written as an assertion, and
a failure there means `Remainder` is not returning the same stack and this task's diagnosis is wrong.

- [ ] **Step 4: Carry the heat**

In `BlockEntityShear`, add beside `Resolve`:

```csharp
  /// <summary>
  /// Puts <paramref name="from"/>'s heat onto <paramref name="to"/>. What leaves the blades is as hot as
  /// what went under them - a crop parts metal and does not cool it - which matters most where the yield
  /// is stock rather than a product, since that has to go back through the rolls.
  /// </summary>
  private ItemStack? CarryHeat(ItemStack from, ItemStack? to) {
    if (to != null && Api != null)
      to.Collectible.SetTemperature(
        Api.World,
        to,
        from.Collectible.GetTemperature(Api.World, from)
      );
    return to;
  }
```

and wrap the product in `CompleteStroke`:

```csharp
    // A staged job hands one product per stroke; a whole-item job hands the lot and keeps nothing back.
    Eject(CarryHeat(input, Resolve(job.Output, job.Stage == null ? job.Count : 1)));
    Eject(Remainder(input, job));
```

- [ ] **Step 5: Run the tests and confirm they pass**

Run: `scripts/exmod.ps1 test iiex`
Expected: PASS.

- [ ] **Step 6: Mutation-check**

Revert the `CarryHeat` wrap and confirm the product test fails again. Restore. A test that survives that
is asserting ambient against ambient.

- [ ] **Step 7: Run the full gate and record**

Run: `scripts/exmod.ps1 test all` — 9 targets green. Append to the worklog. Do not commit.

---

## Task 2: A staged job whose output is its own input is refused at load

`count` is deliberately the modder's own number and nothing checks it against geometry — that is settled
and stays. But one declaration is a duplicator whatever the number: a staged job yielding its own input.
Length is not modelled, so the yielded piece is a *fresh, full-mass* piece of the same stock at the same
stage, and the remainder is still there beside it. There is no reading of that row which is not free metal.

It belongs at load with the rest of `ProcessJobSet`'s validation, so a bad table fails on the file rather
than by a machine quietly minting metal.

**Files:**
- Modify: `src/ExpandedLib/Processes/ProcessJob.cs` — `ProcessJobSet.TryParse`, after the `count` check
- Test: `test/ExpandedLib.Tests/Processes/ProcessJobTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `TryParse` returns false with an error naming the input, for a staged self-yielding job.

- [ ] **Step 1: Write the failing tests**

```csharp
  #region Self-yield

  [Fact]
  public void A_staged_job_yielding_its_own_input_is_refused() {
    Assert.False(
      ProcessJobSet.TryParse(
        Json("""
        { "schema": 1, "machine": "shear", "jobs": [
          { "input": "iiex:stock-shingledbar", "stage": 2.0, "family": "flat",
            "output": "iiex:stock-shingledbar", "count": 4 } ] }
        """),
        out _,
        out string? error
      )
    );
    Assert.Contains("iiex:stock-shingledbar", error);
  }

  [Fact]
  public void The_refusal_ignores_case_the_way_matching_does() {
    Assert.False(
      ProcessJobSet.TryParse(
        Json("""
        { "schema": 1, "machine": "shear", "jobs": [
          { "input": "iiex:stock-shingledbar", "stage": 2.0, "family": "flat",
            "output": "IIEX:STOCK-SHINGLEDBAR", "count": 4 } ] }
        """),
        out _,
        out _
      )
    );
  }

  // A whole-item job consumes its input, so naming the same code is a no-op recipe rather than a
  // duplicator - odd, but not this check's business.
  [Fact]
  public void A_whole_item_job_naming_its_own_input_is_allowed() {
    Assert.True(
      ProcessJobSet.TryParse(
        Json("""
        { "schema": 1, "machine": "shear", "jobs": [
          { "input": "game:metalplate-iron", "output": "game:metalplate-iron", "count": 1 } ] }
        """),
        out _,
        out _
      )
    );
  }

  // The capability this whole unit exists for: a crop that yields a DIFFERENT stock item is fine.
  [Fact]
  public void A_staged_job_yielding_a_different_stock_item_is_allowed() {
    Assert.True(
      ProcessJobSet.TryParse(
        Json("""
        { "schema": 1, "machine": "shear", "jobs": [
          { "input": "iiex:caststock-billet", "stage": 2.25, "family": "grooved",
            "output": "iiex:stock-rod", "count": 6 } ] }
        """),
        out _,
        out _
      )
    );
  }

  #endregion
```

Match the file's existing `Json(...)` helper; if it has none, add
`private static JsonObject Json(string raw) => new(JToken.Parse(raw));`.

- [ ] **Step 2: Run them and confirm they fail**

Run: `scripts/exmod.ps1 test exlib`
Expected: the two refusal tests FAIL (they parse cleanly today); the two "allowed" tests PASS.

- [ ] **Step 3: Add the check**

In `ProcessJobSet.TryParse`, directly after the `count < 1` block and before `float stage = …`, read the
stage first so the check can see it:

```csharp
      float stage = jobNode["stage"].AsFloat(-1f);
      // A staged job leaves its input on the deck and yields a fresh, full-mass piece beside it. Length is
      // not modelled, so a row yielding its own input mints metal however its count is written - there is
      // no reading of it that does not. A whole-item job consumes its input and is left alone.
      if (
        stage > 0f
        && string.Equals(input, output, StringComparison.OrdinalIgnoreCase)
      ) {
        error =
          $"{machine}: the staged job on '{input}' yields '{input}' again, which mints metal - "
          + "a crop's output must differ from what it is cut from";
        return false;
      }
```

and delete the later duplicate `float stage = …` line so the local is declared once.

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `scripts/exmod.ps1 test exlib`
Expected: PASS, all four.

- [ ] **Step 5: Confirm the shipped tables still load**

Run: `scripts/exmod.ps1 test all`
Expected: PASS. `ShippedCropTableTests` (iiex) and `ShippedCastCropTableTests` (siex) parse the real
files; a failure there means a shipped row is self-yielding and is a find, not a regression to work
around.

- [ ] **Step 6: Record**

Append to the worklog. Do not commit.

---

## Task 3: Pin that a crop may yield stock

The mechanism works by construction and nothing says so. On a schema that is the public API, an unguarded
capability is one refactor away from being removed by someone who cannot see it is load-bearing.

**Files:**
- Modify: `src/IronIndustryExpanded/BlockStructures/Forming/WorkPiece.cs` — `FromStack` /
  `FromLegacyStack`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Forming/ShearStationTests.cs`

**Interfaces:**
- Consumes: `CarryHeat` (Task 1).
- Produces: `WorkPiece.FromFreshOrLegacyStack` replaces the private `FromLegacyStack`; no public change.

- [ ] **Step 1: Write the failing test**

```csharp
  #region A crop may yield stock

  // The ruling, as an assertion. A crop whose output names a stock item ejects a piece the mill takes
  // back: fresh, uncropped, at that form's own base gauge. This is what makes the two declared cast
  // routes authorable - a billet cropped mid-gap yields six pieces that each still want a pass.
  [Fact]
  public void A_crop_that_yields_stock_ejects_a_fresh_rollable_piece() {
    // arrange as the existing stroke test, but with the registry's output set to a stock item:
    //   Registry(...) with ProductCode replaced by "iiex:stock-rod", and world.RegisterItem for it.
    // drive the stroke to completion, then:
    WorkPiece? yielded = WorkPiece.FromStack(product);

    Assert.NotNull(yielded);
    Assert.Equal("rod", yielded!.Form.Name);
    Assert.Equal(StockForm.Rod.BaseThickness, yielded.Thickness);
    // Zero is what makes it rollable: PartCropped refuses the remainder, never the piece cut off it.
    Assert.Equal(0, yielded.Cropped);
    Assert.False(yielded.IsPartCropped);
  }

  [Fact]
  public void The_remainder_of_that_same_crop_is_still_refused_by_the_mill() {
    WorkPiece? left = WorkPiece.FromStack(remainder);
    Assert.True(left!.IsPartCropped);
  }

  #endregion
```

- [ ] **Step 2: Run and confirm**

Run: `scripts/exmod.ps1 test iiex`
Expected: both PASS **first time**. That is the point — they document a capability rather than drive one.
If either fails, the ruling does not hold in code and the rest of this task is wrong; stop and report
before changing anything.

- [ ] **Step 3: Name the path that makes it work**

A brand-new stock stack reaches `Fresh(form)` through a method called `FromLegacyStack`, so the branch
this ruling depends on reads as dead migration code. Rename and re-comment; behaviour is unchanged.

```csharp
    if (tree[ThicknessKey] is not FloatAttribute thickness)
      return FromFreshOrLegacyStack(tree, form!);
```

```csharp
  // No gauge on the stack, which is two different stacks. One is a piece from the per-side era, read as
  // its least-worked side - the gauge it could always be fed at - starting its round over; nothing there
  // carried a target gap, so nothing is lost. The other is a stack that has never been rolled: an item
  // straight off the crafting grid, or a piece a shear cropped off a larger one, which is what lets a
  // crop's output be stock at all. Both are the same answer, and for the fresh one it is the whole
  // mechanism rather than a fallback.
  private static WorkPiece FromFreshOrLegacyStack(
    ITreeAttribute tree,
    StockForm form
  ) {
```

- [ ] **Step 4: Run the gate**

Run: `scripts/exmod.ps1 test all`
Expected: PASS, 9 targets. A rename with no behaviour change; anything red is a missed call site.

- [ ] **Step 5: Record**

Append to the worklog. Do not commit.

---

## Task 4: Write the ruling down in one place

It is currently spread across three pages, and one of them contradicts the built code.

**Files:**
- Modify: `docs/design/mechanics/process-extension.md` — § *What a count means*
- Modify: `docs/design/machines/shear.md` — the **Status** header
- Modify: `docs/design/items/rolled-parts.md` — the two blocked rows

- [ ] **Step 1: State the rule in `process-extension.md`**

Add to § *What a count means*: a staged job's `output` may name a **stock item**, in which case each
stroke yields a fresh work piece of that item's form at its base gauge, `Cropped = 0`, and the mill takes
it back. `PartCropped` still refuses the remainder, which is what keeps the tally one `int`. And the one
refusal: a staged job may not yield its own input.

- [ ] **Step 2: Fix `shear.md`'s stale header**

Its **Status** still says *"the crop table itself is still empty and deliberately so"* and calls that B3c.
B3c closed 2026-08-14 and both tables ship. Rewrite the header to what is true, and add the yield-stock
rule to its *Owns* list.

- [ ] **Step 3: Re-state the two blocked rows in `rolled-parts.md`**

The `castbillet` 2.25 and `castbloom` 3.0 rows say they are blocked on "the mid-gap crop". The mechanism
is no longer the blocker — authoring is. Change *Blocked on* to name what is actually missing: a stock
form and route for the piece each crop yields, both siex's, in the siex pass.

- [ ] **Step 4: Update `NEXT.md`**

Under *What is actually next*, the mid-gap crop row moves from an open ruling to a landed mechanism with
its content queued. Follow the file's own maintenance rule at the bottom.

- [ ] **Step 5: Record**

Append the whole unit to `docs/internal/worklog/2026-08.md`. Do not commit.

---

## Self-review

**Spec coverage.** The ruling has three parts — output may be stock, the piece is fresh, the remainder
stays refused — and Task 3 asserts all three. Heat is Task 1. The duplicator is Task 2. Task 4 records it.

**Deliberately not covered.** No `FeedVerdict` change: the mill's 48-voxel refusal was proposed and
**struck by the owner** on 2026-08-15 — the mill takes what its JSON declares and length is not its
business; the gate is the heating furnace's own size, which is
[item-piles](2026-08-15-item-piles.md)'s subject. No new forms, items or crop rows.

**Type consistency.** `CarryHeat`, `FromFreshOrLegacyStack`, `WorkPiece.Cropped`, `IsPartCropped`,
`StockForm.Rod.BaseThickness` are spelled the same in every task and match the current source.

**One risk worth stating.** Task 3's tests are expected to pass on first run, which is the opposite of the
usual red-green order. That is correct for a test whose job is to *pin* an existing capability, but it
means the test could be vacuous. Task 3 Step 2 handles the false-negative direction; to close the other,
temporarily change `FromStack`'s collectible-attribute fallback to `return null` and confirm
`A_crop_that_yields_stock_ejects_a_fresh_rollable_piece` fails. Restore.
