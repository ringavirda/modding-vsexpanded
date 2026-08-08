# The settled-economy landing

**Status** ruled 2026-08-07 - this page owns the together-or-nothing mass batch. One number is already
shipped (the pig); the other five are settled on paper and land as one queued code + goldens batch. Every
row cites the page that derived it.
**Mod** iwex (every item in the batch) · the parity and scenario suites it re-runs span iwex and smex

**Owns** - the facts this page is canonical for:

* the batch itself - the six masses that must land together or not at all, and each row's
  shipped-vs-pending state;
* the landing checklist - what "landed" means: code, goldens, suites, and where art must follow;
* the statement that the bloomery guard-rail is part of the batch's acceptance, not a later nicety.

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| `1 vx³ = 2.5 u` and the derivation behind every mass | [density rule](../mechanics/density-rule.md) |
| the pig's three denominations and the helve-break | [pig](pig.md) |
| the 8.5 u/nugget recovery ladder the guard-rail protects | [ironmaking](../processes/ironmaking.md) |
| the wrought ball, blooms, slabs and the shingling ladder | [stock](stock.md), [shingling](../processes/shingling.md) |
| the cast plate's pair rule (D2) and its fabricated twin | [fabrication](../processes/fabrication.md) |
| the 9-pig charge, the fettle loop and the 175 u tap-cinder remainder | [puddling](../processes/puddling.md) |
| the guard-rail's own derivation | [conventions.md](../conventions.md):576-577 |

---

## Why a batch, not six edits

The masses cite each other. 9 pigs = 3375 u puddle into 16 balls @ 200 u + 175 u cinder; 2 balls make the
400 u bar and 6 balls the 1200 u slab; the cast plate's 500 u is priced against its fabricated twin so
D2's cast-vs-fabricate choice stays a choice; and the tap-cinder count is whatever closes the fettle loop.
Retune one row alone and a ratio breaks somewhere the edit never looked, so the batch lands together or
not at all (ruled 2026-08-07).

## The batch

| Item | In code today | Settled | Geometry / derivation | State |
|---|---|---|---|---|
| **pig** | 375 u (`ItemPig.cs:39`) | 375 u | 5 × 3 × 10 = 150 vx³ × 2.5 ([pig](pig.md)) | shipped 2026-08-05 |
| **wrought ball** | no item exists | 200 u | 16 balls per 9-pig heat: 3375 → 3200 + 175 ([puddling](../processes/puddling.md)) | pending |
| **stock bloom** (`stock-bloom`) | 180 u (`StockItemDefinitions.cs:25`) | 400 u | = `shingledbar` at 3 × 3 × 18, 2 balls ([stock](stock.md)) | pending |
| **stock slab** (`stock-slab`) | 400 u (`StockItemDefinitions.cs:26`) | 1200 u | = `shingledslab` at 8 × 3 × 20 = 480 vx³, 6 balls ([stock](stock.md)) | pending |
| **cast plate** (`castplate-heavy`) | 160 u (`CastPartItemDefinitions.cs:37`) | 500 u - D2 re-affirmed 2026-08-07 | 10 × 2 × 10 = 200 vx³ × 2.5 = 500 | pending - and the art follows, see below |
| **tap cinder** | count undecided (`FettleItemDefinitions.cs:77-78`) | 3 per heat | the 175 u remainder as exactly the count that closes the fettle loop: 3 cinder → 3 fettle → the next heat ([puddling](../processes/puddling.md)) | pending |

### The cast plate, ruled 2026-08-07 - 500 u at 10 × 2 × 10

The four-way disagreement [fabrication § Numbers](../processes/fabrication.md#numbers) recorded - code 160,
runtime shape 12 × 2 × 12 (720 by the rule), fresh editable art 8 × 2 × 8 (320), settled 500 - is resolved
by re-affirming D2: 500 u is canonical and 10 × 2 × 10 is the canonical geometry. The code constant, the
cavity box, the goldens and both shapes all follow. Code and goldens land in this batch; the art redraw
to 10 × 2 × 10 is owed separately, because art is the maintainer's hand-work and never blocks the code
batch.

## The guard-rail

> **The iwex chain must never yield less iron per ore than a vanilla bloomery**
> ([conventions.md](../conventions.md):576-577).

Asserted as part of this batch's acceptance, not filed as a someday test.

## Landing checklist

1. the five pending numbers land in one change - the constants, the item defs, and every recipe or
   machine that counts them;
2. goldens re-blessed, scoped to the touched defs (`EXLIB_WRITE_GOLDENS` takes a path list);
3. the parity and scenario suites re-run green, all supported game versions;
4. the guard-rail invariant asserted - iron per ore, end of chain, ≥ the bloomery's 5 u/nugget;
5. art follows where geometry changed - only the cast plate moves geometry (10 × 2 × 10); the redraw is
   the maintainer's, tracked here so the batch is not called done while the shape still says 12 × 2 × 12.

## Open

* the wrought ball is still an item with no page and no code - this batch pins its mass, not its design
  ([stock § Open](stock.md)).
* [puddling § Open #1](../processes/puddling.md) (how many `tapcinder` items is 175 u?) is answered here -
  3 - and that page should be updated to cite this one when the batch lands.
