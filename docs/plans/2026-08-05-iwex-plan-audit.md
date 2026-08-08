# Design docs vs plan — open findings (iwex)

Open findings from checking `docs/design/**` against
[`2026-08-04-iwex-u2-u10-expansion.md`](2026-08-04-iwex-u2-u10-expansion.md). They target the
unstarted units (U4.4 onward, U6–U11); the shipped units (U2, U3, U4.1–4.3, U5) are not covered.
Re-verify each finding against source at unit start — quoted line numbers predate the shipped work.

The rule applied: `docs/design/**` is authoritative for *decisions*; the plan is authoritative for
*sequencing*. When they disagree about a decision, the doc wins.

| kind | meaning |
|---|---|
| `CONTRADICTS` | the plan does the opposite of a settled decision |
| `MISSING_TASK` | a settled decision no task implements |
| `FALSELY_OPEN` | the plan calls it open; a doc settles it |
| `STALE_IN_PLAN` | the plan cites a number/name the docs have moved |
| `DOC_STALE` | reversed — the plan is right and the *doc* is behind |

46 findings: 1 blocking, 16 high, 22 medium, 7 low.

---

## Blocking (1)

### `MISSING_TASK` · puddling · The firebox block's recipe composition is settled (cast or wrought iron rods + refractory brick + a diagram); until it is written the block has no craft path and is creative-only.

- **Task:** U6.11 Step 3/Step 5
- **Doc:** [machines/firebox.md](../design/machines/firebox.md):245
- **Doc says:** 1. **Recipe.** The *composition* is settled (above); the quantities are not, so the block has **no craft path** and is creative-only today.
- **Plan says:** U6.11 Step 5 adds grid recipes for "the four puddling blocks" — puddlingcore, puddlinghearth, puddlingchargedoor, puddlingchimneycap — and nothing anywhere in U2–U10 adds a recipe for iwex:furnace-firebox.
- **Why it matters:** The firebox is a required cell in both reverberatory layouts now, not an optional coal pile: test/IronworkingExpanded.Tests/goldens/iwex/blocktypes/furnace/puddlingcore.json declares `iwex:furnace-firebox-*-*` at (-5,1,0) and heatingcore.json declares two. Verified there is no recipe: nothing under src/IronworkingExpanded/Recipes/ outputs it and there is no goldens/iwex/recipes/grid/firebox.json. So U6's own Gate — "A player builds a puddling furnace from craftable blocks … loads the firebox with coke" — cannot pass, and U6.1's whole point (make both layouts buildable by a player) is undone by a missing fifth block. docs/design/machines/reheat-furnace.md:431 lists the same gap explicitly ("No recipe for core, hearth, charge door or **firebox**"). Note U9.4 Step 3 does exactly this diligence for the coke oven's chargelid ("the one shipped block in the coke-oven layout with no recipe at all … without it the layout cannot be built") — U6 needs the same step.

---

## High (16)

### `CONTRADICTS` · burden · All three vanilla coal grades (lignite, bituminous, anthracite) feed the bulk coke oven and produce one coke item; the mod does not distinguish them.

- **Task:** U9.1 Step 2 (and U9.2 Step 1, which only tests bituminous)
- **Doc:** [processes/coking.md](../design/processes/coking.md):63
- **Doc says:** | Coal rank chemistry — only certain bituminous coals coke at all | all three vanilla coal grades feed the same oven; one coke item out |
- **Plan says:** U9.1 Step 2 puts `iwex:furnace-firebox` in the chamber cells and endorses its fuel filter: 'BEBehaviorFirebox already accepts bituminous/anthracite/charcoal/coke and refuses lignite (BEBehaviorFirebox.cs:55-82), which is exactly the input filter a coke oven wants.' The U9 preamble scopes the machine to 'bulk game:coke from bituminous coal'.
- **Why it matters:** BEBehaviorFirebox.cs:69 declares `private const string Excluded = "lignite"` and :76 refuses any stack whose path contains it, before the accept list is consulted — so with the firebox as chamber substrate a lignite-mining player can never charge the oven at all, silently, with the refusal coming from a reverberatory-fuel filter that was never designed for a retort. coking.md:123 restates the rule ('The mod does not distinguish them'), and coking.md § Open 3 notes that anthracite historically does *not* coke — so the inherited filter is close to inverted from the one axis the doc left open. If the plan means to narrow the input set it is a design change that has to be written into coking.md, not inherited from a filter whose doc-comment (BEBehaviorFirebox.cs:49-52) is explicitly about reverberatory firing.

### `MISSING_TASK` · crucible · The pot is clayformed from clay-fire exactly the way vanilla's crucible is — i.e. through a `raw` variant that a pit kiln fires to `burned`.

- **Task:** U9.6 Steps 3 and 6
- **Doc:** [machines/crucible-furnace.md](../design/machines/crucible-furnace.md):345
- **Doc says:** So: **clayformed from `clay-fire`, exactly as vanilla's own crucible is**, as a distinct item with its own heat allowance and a 3-firing life.
- **Plan says:** U9.6 Produces: 'iwex:steelcrucible-{burned|smelted} as a BlockSmeltingContainer/BlockSmeltedContainer pair'; Step 3 'Define the two variants with classByType exactly as vanilla does'; Step 6 'Add the clayforming recipe from clay-fire'.
- **Why it matters:** Vanilla's crucible has three type states (raw/burned/smelted) and its clayforming recipe outputs `crucible-raw`, which fires to `crucible-burned` via combustibleProps (meltingPoint 600, smeltingType 'fire', .game/1.20/assets/survival/blocktypes/clay/crucible.json). classByType names only two states because `raw` needs no special class — the plan read the classByType map as the variant list. With no `raw` variant the clayforming recipe U9.6 Step 6 adds has nothing to output, and there is no firing step at all, so the pot is unobtainable in survival and U9's gate ('charge a fireclay pot', no creative mode) cannot be walked.

### `CONTRADICTS` · crucible · The anvil cold-crushing route conserves mass exactly: a blister ingot yields 3 chunks (25 u) + 5 bits (5 u) = 100 u. The ~9% loss belongs to the melt in the pot, not to the anvil.

- **Task:** U9.7 Step 2
- **Doc:** [machines/crucible-furnace.md](../design/machines/crucible-furnace.md):18
- **Doc says:** **cold**-worked blister ingot on the anvil with a helve hammer → **3 chunks (25 u) + 5 bits (5 u)** = 100 u exactly. **Hot**-worked keeps vanilla behaviour → shear steel
- **Plan says:** U9.7 Step 2: 'pick a voxel count that makes units-per-voxel exact for 110 u in / 100 u out' — i.e. it puts a 10% loss into the crushing step.
- **Why it matters:** U9.7 Step 1 correctly demands 'the total is exactly 3x25 + 5x5 = 100' and models the step on PigBreaking's conservation guard; Step 2 then contradicts its own Step 1 by asking for a 110→100 ratio. Meanwhile U9.10 Step 7 applies the ~9% melt loss again ('110 u blister -> 100 u crucible steel per pot'), so the loss is charged twice. The plan's own trap ('mass conservation across the blister fork … 110 u in / 100 u out per pot, and 3 chunks + 5 bits = 100 u exactly on the anvil') states the correct split, so Step 2 is the outlier and the end-to-end mass assertion the gate demands will not close.

### `MISSING_TASK` · crucible · Crucible steel is the best edge in the game; the tool preset catalogue has exactly three entries (brittle/standard/good) and an unknown preset silently falls back to `standard`.

- **Task:** U9.8 Step 2
- **Doc:** [items/alloys.md](../design/items/alloys.md):220
- **Doc says:** **Tool presets** — flat, never `*byType`; every generated tool of a metal shares one stat block (`MetalToolEmitter.cs:67-73`). Preset `none` emits no tools at all (`:80-84`); an unknown preset falls back to `standard` (`:86-91`).
- **Plan says:** U9.8 Step 2: 'Author the JSON with isAlloy:true …, meltingPoint 1600, tools.preset above bessemersteel's good.' No task anywhere in U9 adds a fourth preset to MetalToolEmitter.
- **Why it matters:** Verified in src/ExpandedLib/Metals/MetalToolEmitter.cs:67-73 — the Presets dictionary holds exactly brittle(150)/standard(1000)/good(2600), and ResolveStats falls back to Presets["standard"] for anything unrecognised. Naming a preset 'above good' therefore ships crucible-steel tools at durability 1000 — worse than Bessemer steel's 2600 — for the metal alloys.md:101 calls 'the best edge in the game', with every test green. The fix is either a new preset entry (a task that does not exist) or explicit MetalToolSpec.Durability/AttackPower/MiningTier overrides. Secondary: anchoring to bessemersteel's `good` is anchoring to a value alloys.md Gotcha #1 (:318) and blown-iron.md's landing order Change 3 both settle must be deleted.

### `MISSING_TASK` · fasteners · lpex's (and hpex's) boiler must require rivets with no nail path, while iwex machines accept either fastener.

- **Task:** U8.7 Step 1 (and Produces)
- **Doc:** [State.md](STATE.md):634
- **Doc says:** So it stops being a tier gate: **iwex machines accept either, lpex's boiler requires rivets with no nail path.** Merely structural = substitutable; must-hold-pressure = not.
- **Plan says:** U8.7 **Produces**: "every iwex bill retargeted onto it while lpex's boiler stages stay on the rivet-only helper" — but no task, and no file in U8.7's Modify list, touches BlockBoilerCornish.cs or BlockBoilerLancashire.cs. There is no rivet-only helper to "stay on".
- **Why it matters:** Verified in src/: BlockBoilerCornish.cs:134,141,146 and BlockBoilerLancashire.cs:149,156,161 all call `.RequireMetalNails(domain, n)` today — exactly as docs/design/items/fasteners.md § Consumer census records (:159, :161). U8.7 Step 1 makes "no boiler stage accepts metalnailsandstrips under any wildcard" the first test and the anchor of the whole substitution rule; against unmodified boiler code it fails red on day one and nothing in the plan fixes it. Either the plan needs a RequireRivets helper plus a retarget of six boiler stages across two mods, or the settled half of the substitution rule (the half that gives rivets any meaning) never lands.

### `CONTRADICTS` · fasteners · One stock piece at one stage yields different products depending on the section class / roll set it was rolled on — the rod fork is a per-rod player choice, not a stage lookup.

- **Task:** U8.3 (Produces, Steps 1-5)
- **Doc:** [items/fasteners.md](../design/items/fasteners.md):130
- **Doc says:** One `rolledrod` at 100 u, **the same four feeds either way**, and the player chooses per rod:
- **Plan says:** U8.3 Produces `Crop(string Form, int StageKey, string OutputCode, int Count)` with `Crop? At(string form, float thickness)` — a table keyed on (form, stage) only, with no section class.
- **Why it matters:** The settled fork puts two different products at the same form and the same stage: fasteners.md:134-135 has rolledrod at gap 1.0 → 4 rods (grooved) or 1 nailplate (flat), and fabrication.md:178 has the shingled bar at flat 2.0 → beam @200 while fasteners.md:97 has the same bar at grooved 2.0 → rolledrod @100. A (form, stage) key collides both pairs and silently answers one branch of a two-branch choice. Note the plan already got this right once: U7.5 produces `StockProducts.At(string form, SectionClass section, float stage)` with the section class in the key and the beam row present — U8.3 re-creates the same table under a new name (ShearCrops), re-deletes RollSetSpec.Outputs/OutputAt that U7.5 already deleted, and loses the section axis on the way.

### `CONTRADICTS` · fasteners · The nail bench stays 1 × 1 with an overhanging shaft under SolidNonOpaque; the drawn asset's megablock filename does not justify giving it a footprint.

- **Task:** U8.5 Step 3 (and U8.11 Step 5)
- **Doc:** [machines/nail-machine.md](../design/machines/nail-machine.md):46
- **Doc says:** Either keep 1 × 1 and accept the overhang, or rename the asset. Do not add a footprint to justify the filename.
- **Plan says:** U8.5 Step 3: "Author the 2x3x3 footprint from the measured art (Base x 0..16 z 0..27, MachineCasing y -6..27, NailTray z 0..29 — the tray pokes into the +Z neighbour cell)", and U8.11 Step 5 rewrites the doc to match the art.
- **Why it matters:** This is the exact failure shape this file exists to catch: the doc examined this very drawing (it measures the same overhang, x -2→2 and z 8→24), reasoned about it, and ruled explicitly against deriving a footprint from it — the mill's `SolidNonOpaque` (BlockRollingMill.cs:62-63) already covers an overhanging shape. The plan reasons from the art instead and treats the question as decided, then schedules doc surgery to make the doc agree. nail-machine.md § Open also still lists "Footprint vs the drawn asset" as undecided, so at minimum this is an open question the plan closes unilaterally in favour of the more expensive option. The cost is not cosmetic: a footprint drags in fillers, drive-bus cells, placement/break triads and the "cheap by design, six in a row on one shaft" property nail-machine.md:30 makes load-bearing.

### `CONTRADICTS` · fasteners · The `ItemDie` tooling contract survives for lpex's steam hammer — the stamping/plate die is a settled lpex die row, and the hammer must not invent a parallel die format.

- **Task:** U8.11 Steps 1-2
- **Doc:** [processes/stamping.md](../design/processes/stamping.md):202
- **Doc says:** **The `ItemDie` spec does not exist either.** It is the [heading machine](../design/machines/heading-machine.md)'s to define, and this machine is its **second** consumer — so the hammer must not invent a parallel die format.
- **Plan says:** U8.11 Files: "docs/design/items/dies.md (strike the iwex/lpex/hpex die rows and the ItemDie contract)"; Step 2: "Strike every die row from dies.md and fasteners.md"; Step 1 deletes heading-machine.md, the contract's owner, replacing it with a rivet-machine.md that has "no die catalogue".
- **Why it matters:** State.md's 2026-07-30 settlement removes only the fastener dies: ":642 No dies. Two machines with one job each need no swappable tooling, so the `ItemDie` family leaves iwex entirely" plus "the hpex ball die goes too". It does not touch state.md:603 ("steam hammer + stamping dies | lpex | shingled slab and blanking"), and dies.md:92 still carries the settled `lpex` stamping row (`boilerplate` 600 u → 3 × game:metalplate). "Strike every die row" plus deleting the only page that owns the contract erases lpex's tooling model as collateral damage and leaves stamping.md pointing at a deleted owner — the precise condition its own gotcha warns produces a parallel, incompatible die format later.

### `CONTRADICTS` · forming · The cast stock forms are three distinct StockForms named castbillet / castbloom / castslab, separate from the wrought bloom/slab, and they must be added as StockForms before anything can accept them.

- **Task:** U7.6 Step 4
- **Doc:** [machines/steel-roll-sets.md](../design/machines/steel-roll-sets.md):216
- **Doc says:** **the three cast forms do not exist.** `castbillet` / `castbloom` / `castslab` must be added as `StockForm`s first (build item 22)
- **Plan says:** U7.6 Step 4: 'extend the byType entry to `new { materialUnits = units, stockForm = form }` so cast stock is rollable at all. The form strings are billet/bloom/slab and must match StockForm.All keys after U7.3.'
- **Why it matters:** CastStockItemDefinitions.Forms (verified, :74-78) uses the bare strings billet/bloom/slab. U7.3 Step 4 adds no cast forms, so after U7 StockForm.All is {bloom, slab, rod}. Writing stockForm="bloom" on caststock-bloom makes a 4x4x25 / 1000 u cast bloom resolve to the wrought StockForm.Bloom (3x3x18 / 400 u) — wrong geometry, wrong mass, and the iwex flat/grooved sets accept "bloom" so an iron-tier set would bite it, which steel-roll-sets.md:96 and :154 forbid (castbillet runs the grooved barrel on a steel set; castbloom/castslab go to the wide train). "billet" resolves to nothing at all, so caststock-billet stays unrollable and the step's stated purpose fails silently.

### `CONTRADICTS` · forming · The narrow flat schedule terminates at 9 x 1 x 18, which crops into exactly two 9 x 1 x 9 game:metalplate — so the bar's terminal width must be at least 9.

- **Task:** U7.3 Step 3
- **Doc:** [items/rolled-parts.md](../design/items/rolled-parts.md):108
- **Doc says:** | `shingledbar` | flat | 1.0 | 9 × 1 × 18 | ×2 | 9 × 1 × 9 | `game:metalplate` | exact, vanilla geometry |
- **Plan says:** U7.3 Step 3: 'Keep MaxWidth: no narrow schedule reaches it (the bar tops out at 9.0 wide, the rod at 4.0), so U7 does not have to settle rolling.md Open #1'
- **Why it matters:** StockForm.Bloom ships MaxWidth = 8f (verified StockForm.cs:48; rolling-mill.md:295 tabulates it as 8 with 'Width at 1.0 = 7.60 / 0.5 = 8.00 (capped)'). Under the section law the bar reaches V/(L0·t) = 162/(18·1.0) = 9.0 at the 1.0 gap — above the cap the plan proposes to keep. Capped at 8 the stage is 8 x 1 x 20.25 and the two exact vanilla-geometry plates do not fall out, which breaks the settled crop and U7's own gate ('reproduces CutPlate (9 × 1 × 18)'). rolling.md:418 states the constraint directly: 'the bar's plate stage needs ≥ 9'. Separately, rolling-mill.md:522 (build item 4) and roll-sets.md:361 both record 'MaxWidth moves to the roll set' as settled by wide-hall, while rolling.md:372 reopens it ('One of the two has to give') — so the plan's premise for deferring is the one part that is genuinely open, but its factual claim that no narrow schedule reaches the cap is false either way.

### `CONTRADICTS` · forming · The stock ladder is exactly five forms, and the stock item list is emitted one-per-StockForm so the two cannot diverge; rolledrod is a rolled product, not a stock form.

- **Task:** U7.3 Step 4 (with U7.4 Step 5)
- **Doc:** [items/stock.md](../design/items/stock.md):96
- **Doc says:** Only `stock-bloom` and `stock-slab` exist. They are emitted one per `StockForm` (`StockItemDefinitions.cs:30`), so the item list and `StockForm.All` cannot diverge
- **Plan says:** U7.3 Step 4: 'add Rod = 2×2×10 — which is what makes the fork work' (a sixth StockForm), while U7.4 separately creates iwex:rolledrod as its own itemtype.
- **Why it matters:** StockItemDefinitions.Definitions is `StockForm.All.Values.Select(...)` (verified :30), so adding Rod auto-emits a second rod item, iwex:stock-rod, alongside U7.4's iwex:rolledrod — two items with the same 2x2x10 / 100 u identity, and only the auto-emitted one matches the sets' `accepts ["bloom","rod"]`. It also throws at emit time: `Units[form.Name]` (StockItemDefinitions.cs:44) has no "rod" row, and rolling-mill.md:379-381 warns 'You must also author (or generate) the stage shapes, or RolledStockStagesTests fails'. stock.md:9 fixes the ladder at five forms — shingledbar, shingledslab, castbillet, castbloom, castslab — and rolled-parts.md:80 files rolledrod in the product catalogue. The plan never reconciles 'the rod must be a name a roll set can accept' with 'a StockForm mints a stock item'.

### `MISSING_TASK` · forming · Build item 6: the form table is replaced — bloom/slab become shingledbar 3 x 3 x 18 and shingledslab 8 x 3 x 20.

- **Task:** none (promised by U7 Shared-files and U6.11 Step 2; absent from U7.1-U7.10)
- **Doc:** [machines/rolling-mill.md](../design/machines/rolling-mill.md):524
- **Doc says:** | 6 | `StockForm`: `bloom` / `slab` → **`shingledbar` 3 × 3 × 18** and **`shingledslab` 8 × 3 × 20** | masses belong to [stock](../design/items/stock.md) |
- **Plan says:** The rename is deferred to U7 twice and then never performed. U6.11 Step 2: 'Do not rename StockForm.Bloom to Bar … that rename belongs to U7.3/U7.4'. U7's Shared-files note: 'U7.2/U7.3/U7.4 rewrite … the form table (including the bloom→bar rename U6 deliberately does not do)'. But U7.3 Step 4 changes only the dimensions, Step 5 keeps accepts ["bloom","rod"], and U7.6 Step 4 pins the strings billet/bloom/slab.
- **Why it matters:** The rename is the direct cause of the caststock collision above: keeping the wrought form named 'bloom' is what makes caststock-bloom's stockForm ambiguous. It also leaves the lang rows wrong — stock.md:135 settles that item-stock-bloom 'Wrought Bloom' becomes 'Shingled Bar', and U7.3 Step 10 fixes only item-stock-slab's 'Cast Slab' string without renaming either item. Every downstream doc (stock.md, rolling.md:146-147, rolled-parts.md's crop table) names the forms shingledbar / shingledslab; after U7 the code would still say bloom / slab, so a reader diffing docs against code re-derives the same confusion this file exists to stop.

### `CONTRADICTS` · hearth · The iron and slag pools share one crucible volume — slag floating on top eats space iron could occupy, so iron capacity shrinks until the slag is flushed.

- **Task:** U4.3 Step 6 / U4.4 Step 4
- **Doc:** [layered-charge.md](../design/layered-charge.md) § The hearth; detail in [machines/blast-furnace-cold.md](../design/machines/blast-furnace-cold.md):417
- **Doc says:** 2. **The two taps compete for one volume.** Slag floats on iron in the same layered cell, so neglecting the slag tap lets slag build on top and eat the space iron could occupy — iron capacity shrinks until it is flushed. Real furnaces have exactly this problem
- **Plan says:** U4.3 Step 6 adds two independent fixed budgets — `HearthIronBands` (the slag-spout level) and `HearthSlagBands` (the remaining head) — and U4.4 Step 4 caps each cell separately: "Overflow beyond `MaxUnitCapacity` is what `LiquidCapacityReached` now reports."
- **Why it matters:** Two per-cell `MaxUnitCapacity` values are two independent volumes, which is the opposite of the settled model: with fixed budgets, neglecting the slag tap costs the player nothing in iron capacity and the 'operating rhythm for free' the design pays for the whole two-tap layout to get simply never materialises. It also silently deletes the reason the crucible is layered at all (doc:576 'crucible — iron under slag'), leaving two unrelated pools in one block. U4.3 Step 2 correctly cites the layering requirement and recommends route (a), but Step 6 then sizes the two cells as if they were separate tanks.

### `MISSING_TASK` · hearth · The slag-channel height is the pool's overflow level: with the slag tap open the crucible cannot hold more than 10 bands and iron rising to the cinder notch runs out of it; plugging the slag tap is what lets the hearth accumulate past that.

- **Task:** none
- **Doc:** [layered-charge.md](../design/layered-charge.md) § The hearth; detail in [machines/blast-furnace-cold.md](../design/machines/blast-furnace-cold.md):417
- **Doc says:** 1. **The slag channel height is the overflow level.** With the slag tap open the crucible cannot hold more than 10 bands; plugged, it accumulates. So *plugging is what lets the hearth fill*, and the cap is a spout the player can look at.
- **Plan says:** nothing — no U4 task couples the crucible cap to the slag tap's plug state; U4.4 Step 4 makes a full pool report `LiquidCapacityReached` (furnace stalls) and U4.7 gives the plug only a flow gate
- **Why it matters:** This and doc:599-600 ("**The slag spout height *is* the iron pool cap.** Iron can only rise to the cinder notch before it starts running out of it — a real operational failure. The cap is a spout you can see, not a constant.") are the payoff the whole tap-shape adoption exists to buy — 'Two behaviours fall out of the drawing, needing no code' (doc:619). U4.6 lands the geometry that encodes the notch height and U4.7 lands the plug, but nothing in U4 reads either: an open slag tap does not lower the cap and a plugged one does not raise it. The behaviour would then be permanently absent while U4's gate goes green, and the drawn Y 10-11 slag channel becomes decoration.

### `MISSING_TASK` · puddling · Two puddle balls (2 × 200 u) make one 400 u shingled bar under the helve, with mass conserved exactly; the balls are piled on the anvil one at a time before the blow.

- **Task:** U6.11 Step 4
- **Doc:** [processes/shingling.md](../design/processes/shingling.md):100
- **Doc says:** **Mass is conserved exactly across the step.** 2 × 200 = 400 and 6 × 200 = 1200; nothing is minted and nothing is burned, because the puddling split already took the cinder out
- **Plan says:** U6.11 Produces "One helve smithing recipe: piled wrought balls → wrought stock" and Step 4 says to copy PigRecipeDefinitions.cs verbatim in structure — but no step makes two balls into one anvil work item.
- **Why it matters:** PigRecipeDefinitions is a one-item→one-item anvil route; shingling.md § Open 4 says so in as many words ("The pig-breaking patch is the closest precedent and it accumulates nothing"), and § The loop step 2 settles the intended path ("**Pile** balls on the anvil … RMB, one ball at a time — the same voxel accumulation as stacking ingots for a vanilla plate"). Copying the pig template unmodified yields a recipe that eats one 200 u ball and produces a 400 u bar — 200 u minted, breaking R6, the exact invariant U6.11 Step 3 claims to protect. That test (`2 × BallUnits == Units["bloom"]`) compares two constants and would stay green while the recipe leaks mass. The pile mechanism is genuinely open (which of the two paths), but that a bar costs two balls is settled, and U6.11 has no step for it while the U6 Gate asserts "Two balls under the helve make a shingled bar".

### `DOC_STALE` · puddling · (stale) The puddling furnace's only uncraftable parts are four iwex blocks; the fuel cell is vanilla coal in an air cell and a vanilla grating sits under it.

- **Task:** U6.11 Step 5
- **Doc:** [machines/puddling-furnace.md](../design/machines/puddling-furnace.md):143
- **Doc says:** The bricks, slabs, grating and firebox door are all vanilla and craftable already; only the four `iwex` blocks are missing.
- **Plan says:** U6.11 Step 5 crafts exactly those four blocks — it inherits the doc's count verbatim and so misses the fifth, now-required iwex block.
- **Why it matters:** The layout was redrawn when the firebox became a block (docs/design/machines/firebox.md § What it breaks: the `Firebox` role moved "from `c` (`@(air|coalpile)`) to `F` (`iwex:furnace-firebox`)" and "`G` … leaves the puddling and reheat layouts"). The shipped golden confirms it: 91 offsets, zero gratings, zero `@(air|coalpile)`, one required `iwex:furnace-firebox-*-*`, 6 fillers. puddling-furnace.md § Structure still bills 94 cells with `G` × 1 and `c | @(air|coalpile)` (lines 58, 67, 74), and the § Numbers ShaftBox/Origin rows are read off that stale drawing. The plan catches the cell and filler counts but not this Construction sentence — and the missed firebox recipe (finding 1) is the direct consequence of trusting it. No U6 task updates this page's Structure or Construction sections.

---

## Medium (22)

### `DOC_STALE` · burden · The coke oven's chamber charge is vanilla `game:coalpile`, relying on BlockEntityCoalPile's own coking — marked 'Settled 2026-08-03'.

- **Task:** U9.1 Step 2
- **Doc:** [machines/coke-oven.md](../design/machines/coke-oven.md):62
- **Doc says:** **The charge is vanilla `game:coalpile`, and that is the whole point.** Coking is *already implemented on the pile itself* — `BlockEntityCoalPile` carries the conversion and manages the quantity change from coal to coke.
- **Plan says:** U9.1 Step 2: the `c` cells hold `iwex:furnace-firebox-*-*` marked CellRole.Firebox, not ExCodes.CoalBed — 'This retires the page's coal-pile decision.'
- **Why it matters:** The plan is right and the doc is stale. The same-dated firebox cutover moved both reverberatory furnaces off the coal pile (BlockPuddlingFurnaceCore.cs:110 and BlockHeatingFurnaceCore.cs:72 now legend `F` -> IwexBlocks.FurnaceFirebox.Any; BlockPuddlingFurnaceCore.cs:104 records 'This was `c` -> `@(air|coalpile)` until 2026-08-03'), and the shaft furnaces moved to IwexCodes.ChargeShaft. The danger is the Settled banner at :55: an implementer who reads coke-oven.md alone will rebuild a coal-pile dependency, and VanillaCodes.cs:310's own warning is that `@(air|coalpile)` completes a structure with no fuel cell built at all. U9.1 Step 2 already instructs recording the reversal in the page — that edit is the fix and must not be skipped.

### `DOC_STALE` · burden · Every fire in the suite is a vanilla `game:coalpile` in a declared cell; nothing else burns (fuels.md is the page that owns the fuel taxonomy and the charge substrate).

- **Task:** none
- **Doc:** [items/fuels.md](../design/items/fuels.md):127
- **Doc says:** Every fire in the suite is a vanilla `game:coalpile` sitting in a declared cell. Nothing else burns.
- **Plan says:** nothing — fuels.md is named in no task anywhere in the plan; U9.1 Step 2's substrate reasoning is checked against coke-oven.md only.
- **Why it matters:** Five of the eight rows in the table under that sentence are already wrong: the puddling and reheat furnaces now legend `F` -> IwexBlocks.FurnaceFirebox.Any (BlockPuddlingFurnaceCore.cs:110, BlockHeatingFurnaceCore.cs:72) and the cold blast furnace, cupola and hot blast furnace now legend `c` -> IwexCodes.ChargeShaft (`*:@(air|coalpile|furnace-chargepile)`, BlockBlastFurnaceCoreCold.cs:101, BlockCupolaFurnaceCore.cs:79, BlockBlastFurnaceCoreHot.cs:87). Gotcha 1 at :265 ('Nothing burns coke as coke. Roles.Fuel has two call sites, both in the mixer') is dead the same way. This is the page the coke oven's input filter belongs to, so U9.1's substrate decision is being taken against a description of the world before the firebox cutover — the exact failure shape this file exists to catch, one page upstream.

### `DOC_STALE` · crosscut · R10's sibling invariant R9 still states mass is derived from geometry and never picked; the 2026-08-04 ruling (recorded in cast-parts.md and bending.md) makes the density rule an art-sizing guide with mass declared, and the plan follows the ruling.

- **Task:** U7.2 Step 4, U7.4 Step 5, U9.11 Step 2, amendment § Three further rulings
- **Doc:** [conventions.md](../design/conventions.md):65
- **Doc says:** - **R9 — Mass is derived from the shape.** *(Added 2026-07-29.)* **1 voxel³ = 2.5 units.** Every mass in the   suite is the density rule applied to a drawn shape, not a picked number
- **Plan says:** "Mass is the item def's materialUnits read off the collectible, not re-derived from voxels (ruling 1: the density rule is a sizing guide, mass is declared)"; "Both are 600 u, not the 540 the density rule computes"
- **Why it matters:** cast-parts.md:83-84 ("The density rule sizes art *plausibly*; the mass is declared so the economy divides") and bending.md:9-10 ("`castshell` and `castwheelsection` are pinned at exactly the **600 u**") carry the newer ruling, and the parent plan restates it at :1428 ("that only settled that the number is declared, not derived"). R9 and state.md:21-27 ("masses should be generated, not written … Drift then becomes impossible") still assert the absolute, and R9 is a named invariant other docs cite by number — so the next reader who reasons from conventions.md will call the shipped 600 u a defect and "fix" it to 540. No task in the plan amends R9 or state.md § The structural fix; U8.11 and U10.7 are the only doc-surgery tasks and neither touches conventions.md.

### `CONTRADICTS` · crosscut · Within a family either every member lives in the `type` variant under one shared code or none does — and the fourteen blocks in iwex's `furnace/` folder all share the single code `iwex:furnace` + `type(...)`.

- **Task:** U9.6 Produces / Files / Step 5
- **Doc:** [mechanics/naming.md](../design/mechanics/naming.md):201
- **Doc says:** > Within a family, **either every member lives in the `type` variant under one shared code, or none does.** > A half-and-half family is what produces the collision.
- **Plan says:** "Produces: iwex:steelcrucible-{burned|smelted}" with its blocktype golden created at `goldens/iwex/blocktypes/furnace/steelcrucible.json`; Step 5 then treats the code as an open choice ("Either name it so the prefix matches, or extend both caches")
- **Why it matters:** The pot is filed in the furnace folder (its golden path says so), so N1 makes its code `furnace-steelcrucible` — i.e. `iwex:furnace` + `type(steelcrucible)`, matching the 14 parts naming.md:245 records as done 2026-08-03. As written it is both an N1 break (rendered `steelcrucible/burned` ≠ `furnace/steelcrucible`) and the half-and-half family N7 forbids: `iwex:furnace*`, the wildcard every furnace layout legend uses, silently stops covering a block sitting in the furnace family's own folder. Step 5's "decide the code deliberately" reads as an open question when naming.md has already answered it; if the `crucible-` prefix must match vanilla's pour cache, extend the cache — the code is not the free variable.

### `MISSING_TASK` · crucible · The pot charge is 110 u blister steel plus a thin slag cover; the slag is an input, sourced from the blast furnace's own iwex:slag, closing a loop.

- **Task:** none
- **Doc:** [machines/crucible-furnace.md](../design/machines/crucible-furnace.md):17
- **Doc says:** | **Charge** | **110 u blister steel → 100 u crucible steel** per pot (~9 % melt loss), plus a thin slag cover |
- **Plan says:** nothing — U9.10 Step 7 charges only '110 u blister -> 100 u crucible steel per pot'; U9.10 Step 8 treats slag purely as an output concern ('confirm the inherited BfMaxMoltenSlag path is genuinely unused').
- **Why it matters:** The slag flux cover is half of the settled charge and the reason iwex:slag stays useful after the blast furnace (crucible-furnace.md:463: 'Slag is still an *input* here (the thin flux cover), sourced from the blast furnace's own `iwex:slag` — a closed loop worth keeping'). Worse, U9.5 Step 4 and U9.10 Step 4 both pin FillingSlag/Slag1..4 as elements that must exist in the shipped shape — so the plan writes a test asserting the presence of drawn geometry that nothing will ever fill or show.

### `MISSING_TASK` · crucible · The pot ships with two clayforming recipes — a one-pot and a four-pot — mirroring vanilla's crucible.json / fourcrucible.json.

- **Task:** U9.6 Step 6
- **Doc:** [machines/crucible-furnace.md](../design/machines/crucible-furnace.md):14
- **Doc says:** | **Pot shape** | one pot and four pots, mirroring vanilla's `crucible.json` / `fourcrucible.json` |
- **Plan says:** U9.6 Step 6: 'Add the clayforming recipe from clay-fire' — singular, one recipe.
- **Why it matters:** `fourcrucible` exists in vanilla only as a clayforming recipe (.game/1.20/assets/survival/recipes/clayforming/fourcrucible.json — there is no fourcrucible shape or blocktype), and its output is `crucible-raw` with stacksize 4. That is exactly the ergonomics this machine needs: 4 fixed pots per furnace × a 3-firing life means a constant pot supply, and the settled row is what stops that being 4 separate clayforming sessions. Note it also collides with U9.6 Step 3's 'MaxStackSize must be 1' — a stacksize-4 output needs the counter kept somewhere other than a non-stacking stack, and the plan never confronts that.

### `CONTRADICTS` · crucible · alloys.md is canonical for the metal ladder; materials.md's Materials / Alloy compositions tables are superseded by it.

- **Task:** U9.8 Step 3
- **Doc:** [items/alloys.md](../design/items/alloys.md):356
- **Doc says:** Its **Unit economy** table is contradicted wholesale by the [density rule](../design/mechanics/density-rule.md), and its **Materials** / **Alloy compositions** tables are superseded by this page.
- **Plan says:** U9.8 Step 3: 'Record in docs/design/materials.md that this raises vanilla's tool ceiling deliberately and that it gates nothing … That is the settled ruling and it needs a home outside the machine page.'
- **Why it matters:** The plan sends the crucible-steel ladder ruling into the one table alloys.md declares superseded (and whose own header still falsely claims to be the single source of truth — the exact drift alloys.md Gotcha #8 exists to record). alloys.md:101 already carries the Crucible steel row with `Registry code: (none)` and `Status: designed`; no task in U9 updates it, so after U9.8 ships the canonical ladder still says the metal does not exist while a superseded table carries the ruling. The ruling's home is the alloys.md row.

### `DOC_STALE` · crucible · Stack height is player-built and counted by a world walk from the layout's stack base — the plan is right and this Open bullet is the stale text.

- **Task:** U9.9 Step 5 / U9.11 Step 5
- **Doc:** [machines/crucible-furnace.md](../design/machines/crucible-furnace.md):457
- **Doc says:** Its course count comes from **the layout**, not a world scan — height is fixed per structure (see § Structure).
- **Plan says:** U9.9 Step 5: 'Author only the minimum stack in the layout - the base course - and leave the rest to the player, per the settled the core owns the walk rule.' U9.11 Step 5 pins the cold-complete state.
- **Why it matters:** § Structure's dated block ('The stack is player-built and counted — settled 2026-08-02', :247-256) reverses this, and the settled box at the top says it wins over the body. The § Open bullet at :457 and the follow-on bullet at :458 ('With height fixed by design rather than chosen by the player') are pre-ruling text that survived. So an implementer reading § Open will build a fixed-height layout and permanently destroy the build-complexity-buys-capability trade the machine exists for — the exact failure the plan's own trap warns about.

### `CONTRADICTS` · fasteners · All three mpenergy benches (shear, nail, rivet/heading) are 1 × 1 blocks on mpenergy with cast-iron shafting.

- **Task:** U8.4 Steps 2/4, U8.6 Step 4
- **Doc:** [machines/shear.md](../design/machines/shear.md):47, [machines/heading-machine.md](../design/machines/heading-machine.md):42
- **Doc says:** **Footprint: 1 × 1 each, and all three sit on `mpenergy` with cast-iron shafting** — not vanilla MP, and not
- **Plan says:** U8.4 Step 4 authors a 3x2x2 footprint for the shear and U8.6 Step 4 a 6x3x2 footprint for the rivet machine ("six cells along the shaft axis... five drive-bus cells"), each derived from the measured bounding box of an editable shape.
- **Why it matters:** The footprint is stated as a design decision — shear.md:47 and heading-machine.md:42 both carry it, and the build census repeats "Rivet machine — 1 × 1 `mpenergy` bench". A six-cell-long bench cannot be the "hall of identical machines in a row on one overhead line shaft" the design is built around (nail-machine.md:30), and every mechanical consequence the plan cites (drive-bus cells, generalising BlockRollingMillAxle, filler-vs-graph-node) exists only because the footprint was enlarged. The docs are genuinely stale about the art existing (shear.md:61 "Nothing is drawn" is wrong — machine-mp-megablock-cutter.json is on disk), but "art exists" is not the same decision as "the footprint changes", and the plan conflates the two.

### `MISSING_TASK` · fasteners · StockPile.Place lives in exlib because the reheat hearth's bed and the rack must share one implementation — the rack is its second consumer.

- **Task:** U8.9 (all steps) / U7.1
- **Doc:** [machines/stock-rack.md](../design/machines/stock-rack.md):7
- **Doc says:** the requirement that pile placement become **`StockPile.Place` in exlib**, shared with the reheat hearth's bed — the rack is that code's second consumer and the reason it should not be a furnace detail;
- **Plan says:** U8.9 is titled "StockPile.Place in exlib — the pile-composition code the rack and the reheat hearth share", but no step and no file in U8.9 or anywhere else in U8 (or U7.1, the only task that touches BlockEntityHeatingHearth) migrates the hearth's bed onto it. Only U8.10's rack consumes it.
- **Why it matters:** Built with one consumer, StockPile is a rack detail that happens to sit in exlib — exactly the inversion of the doc's argument, and the hearth keeps BlockEntityHeatingHearth.OnTesselation's fixed three-row bed (stock-rack.md:65 calls this "the same job the reheat hearth's bed should be doing (build item 10)"). stock-rack.md § Open 1 names build item 10 as the shared prerequisite; nothing in U7 or U8 carries it, so the second consumer never arrives and the shared-code justification is never tested.

### `CONTRADICTS` · fasteners · A `beam` is 200 u (4.5 × 2 × 9), rolled at narrow flat 2.0 and cropped in half; every crop conserves mass exactly.

- **Task:** U8.3 Step 1
- **Doc:** [processes/fabrication.md](../design/processes/fabrication.md):178
- **Doc says:** | **`beam`** | 4.5 × 2 × 9 | **200 u** | narrow `flat` 2.0, cropped in half ([rolling](../design/processes/rolling.md)) |
- **Plan says:** U8.3 Step 1: "item-rolled-beam.json `CutPlate1..2` are two 9x1x9 elements so a beam-stage piece crops to 2 plates".
- **Why it matters:** A `game:metalplate` is 200 u (fabrication.md:179, stamping.md:114), so "a beam-stage piece → 2 plates" is 200 u in and 400 u out — a 2× mint, against the no-minting rule stamping.md:158 states as the ceiling every conversion inherits. The correct settled row is the one U7.5 Step 2 already writes: shingledbar at **flat 1.0** (400 u) → 2 × game:metalplate @200, with the beam being a separate 2.0-stage product (2 × beam @200). U8.3's wording either names the wrong input or the wrong stage; taken literally it breaks the mass ledger, and U8.3's own Step 2 conservation test would then be written around the wrong row.

### `MISSING_TASK` · fasteners · Every iwex machine bill accepts either fastener — the substitution rule is repo-wide for iwex, not a sample of bills.

- **Task:** U8.7 Step 4
- **Doc:** [State.md](STATE.md):634
- **Doc says:** So it stops being a tier gate: **iwex machines accept either, lpex's boiler requires rivets with no nail path.** Merely structural = substitutable; must-hold-pressure = not.
- **Plan says:** U8.7 Step 4 retargets exactly six sites: the four pipe segments, "the tall hopper (FurnaceRecipeDefinitions.cs:60)" and the plated molten barrel — while Produces claims "every iwex bill retargeted onto it".
- **Why it matters:** Verified in src/: iwex has nine grid nail sites, not six — FurnaceRecipeDefinitions.cs:30 (blast furnace core, Nails(4)), :79 (tall hopper, Nails(1)), :100 (twin-tub blower, Nails(1)), MoltenRecipeDefinitions.cs:34, OreProcessingRecipeDefinitions.cs:36 (now removed) and the four pipe segments. The blast furnace core and the twin-tub blower are left nail-only. The plan's list was copied from fasteners.md § Consumer census, whose table is explicitly labelled "Representative and largest bills:" (fasteners.md:154) against a total of 21 grid sites (:150) — a representative sample read as an exhaustive one. The stale `FurnaceRecipeDefinitions.cs:60` line reference comes from the same doc table (fasteners.md:164) and points at nothing today.

### `DOC_STALE` · fasteners · fabrication.md still asserts that fabrication is gated on lpex end to end because the rivet die is lpex's, so an iwex-only player can fabricate nothing.

- **Task:** U8.11 (Files list)
- **Doc:** [processes/fabrication.md](../design/processes/fabrication.md):153
- **Doc says:** **Steps 4 and 5 are both lpex.** An iwex-only player can fabricate nothing that needs a rivet or a curve —
- **Plan says:** U8.6 builds the rivet machine and the rivet item in iwex (correct per state.md:627 and :629, settled 2026-07-30), but U8.11's doc-surgery file list does not include docs/design/processes/fabrication.md at all.
- **Why it matters:** The plan is right and the doc is stale, but the doc is left stale. fabrication.md:35, :146 and :180 all route the rivet through "the heading machine + lpex's rivet die" — the page the plan deletes in U8.11 Step 1 — and its § Gotchas (:298) still concludes "Fabrication is gated on lpex end to end", which stops being true the moment rivets are iwex's. Since fabrication.md owns the bill grammar (`beam` + plate + rivets) that the rivet item's only real consumer chain depends on, leaving it pointing at a deleted page and an obsolete mod assignment is how the next implementer re-derives the wrong tier gate.

### `CONTRADICTS` · forming · Only the two narrow (bar and rod) schedules are drawn; the slab's editable file carries the as-shingled stage alone, and no wide stage of any stock has art.

- **Task:** U7.3 Steps 8-9
- **Doc:** [processes/rolling.md](../design/processes/rolling.md):391
- **Doc says:** * **`item-shingled-slab.json` draws only the as-shingled stage.** Both narrow schedules are fully drawn
- **Plan says:** U7.3 Step 8: 'Since the stages are now drawn (ruling 1: no redraws), write scripts/export-stock-stages.py to lift each named element out of the editable family file … and emit assets/iwex/shapes/forming/stock-{form}-{key}.json for keys 300/275/250/225/200/175/150/125/100. Delete the ten existing generated files'
- **Why it matters:** Verified by parsing assets/editable/shapes/item-shingled-slab.json: it has exactly one top-level element, ShingledSlab1 — confirming the doc. So eight of the nine slab keys have no source element, while the plan deletes the five existing stock-slab-* files outright. StockItemDefinitions still emits stock-slab (StockForm.Slab stays in All), so IwexDefinitionGoldenTests.Every_shape_reference_resolves_to_a_shipped_file (the test U7.4 Step 9 leans on) and the rewritten RolledStockStagesTests over the nine-gap list have nothing to resolve or measure for the slab. The plan's own U7.3 Step 1 'measure the art first' inventory lists only item-shingled-bar / item-rolled-beam / item-rolled-rod and never mentions the slab, so the gap is invisible until the script runs.

### `DOC_STALE` · forming · The plan is right and items/dies.md is the wrong doc: State.md § Fasteners (settled 2026-07-30) deletes the ItemDie family and the bolt outright.

- **Task:** U8.11 (plan is correct; no U7 task needed)
- **Doc:** [items/dies.md](../design/items/dies.md):95
- **Doc says:** **Two benches and one hammer, five dies, four mods, and exactly one spec.** That is the shape the design is aiming at, and it is worth stating plainly because no single page has held all five rows before
- **Plan says:** U8.11 is 'Doc surgery: strike the die family, the bolt and the 1x1x1 bench claims'; U7 correctly builds no die and no bolt.
- **Why it matters:** State.md:642 rules '**No dies.** Two machines with one job each need no swappable tooling, so the `ItemDie` family leaves iwex entirely — and **bolts are dropped**', and state.md:647 already logs 'Doc surgery still owed: … `items/dies.md` + `items/fasteners.md` need the die family and bolts struck.' dies.md still presents five settled dies, a blocking Open #1 ('What is Bench's value set? blocking the first die item'), and a bolt route off the 25 u rod — all dead design. Anyone in the forming cluster reading dies.md as authoritative will invent work U7/U8 must not do, and will read the rod's consumer list wrongly (it is the rivet machine, iwex, not a bolt die).

### `DOC_STALE` · forming · The plan is right and rolled-parts.md § Assets is stale: the nailplate and the rod's four flat stages are drawn.

- **Task:** U7.4 Step 1 (plan is correct)
- **Doc:** [items/rolled-parts.md](../design/items/rolled-parts.md):190
- **Doc says:** | `nailplate` | — | — | **missing**, and so are all four flat stages of the rod (1.75 / 1.5 / 1.25 / 1.0) |
- **Plan says:** The plan records: 'The art landed and was renamed since the docs were written. item-rolled-nailplate.json exists (NailPlate1 4×1×10) and item-rolled-rod.json now carries Flattened175/150/125 plus NailPlate — the complete flat branch.'
- **Why it matters:** Verified by parsing the files: item-rolled-nailplate.json = [NailPlate1]; item-rolled-rod.json = [RolledRod200, Grooved175, Grooved150, Grooved125, CutRivetRod1, Flattened175, Flattened150, Flattened125, NailPlate]. rolled-parts.md:198 further claims 'the fork has art on the round branch and none at all on the flat one' — also false now. The same § also names files that no longer exist (:186 item-rod-nail.json, :185 item-rod-rolled.json; the real names are item-rolled-rivetrod.json and item-rolled-rod.json), so anyone wiring U7.4's four product shapes off this table wires nothing. This is exactly the 'implementer reasons from the stale side' failure, inverted.

### `FALSELY_OPEN` · furnaces · The crucible's cap is set by the drawn slag-channel floor at 10/16 — it is derived from the shapes, never chosen as a constant.

- **Task:** U4.1 / U4.4 (via plan :136 and :754)
- **Doc:** [machines/blast-furnace-cold.md](../design/machines/blast-furnace-cold.md):417
- **Doc says:** So **cap the pool by band height, never by a constant** — and the shapes already choose it: the slag channel floor at 10/16 is the overflow level.
- **Plan says:** Biggest risks (:136): 'The crucible band height: layered-charge.md offers both 10 bands (3 cells × 10 × 640 = 19 200 u) and 2 bands (3 840 u) and never reconciles them.' The plan repeats it and instructs 'U4 must pick one and write down which'.
- **Why it matters:** The doc does reconcile it, twice and with a mechanism: § Sizing says the shapes choose it, and § 'Two behaviours fall out of the drawing' (:621-622) states 'The slag channel height is the overflow level. With the slag tap open the crucible cannot hold more than 10 bands; plugged, it accumulates.' The 2-band figure sits in the 'Numbers to derive' table (:911) explicitly labelled 'a starting point'. Treating this as an open pick invites the implementer to ship 2 bands and lose the settled 'the cap is a spout you can see, not a constant' behaviour — the same class of decision the taps' drawn geometry exists to encode.

### `FALSELY_OPEN` · hearth · Opening a tap consumes the clay plug — there is no refund; that is what makes blowing in cost one plug.

- **Task:** U4.7 Step 1 / U4.7 Produces
- **Doc:** [machines/blast-furnace-cold.md](../design/machines/blast-furnace-cold.md):422
- **Doc says:** | **Opening** | break the plug out; the element disappears, the clay is consumed |
- **Plan says:** U4.7 Step 1 case (2): "...opens it and consumes nothing (the plug is destroyed, not recovered) or refunds `TapUnplugClayRefund` — pick per the design ... and pin whichever"; U4.7 **Produces** adds a config key `TapUnplugClayRefund`
- **Why it matters:** The plan quotes the design in the same sentence and then reopens it, and its Produces line commits to shipping a `TapUnplugClayRefund` key the settled model has no use for. A refund directly undercuts the doc's stated purpose (doc:655-658: "**This gives tapping a cost the free right-click toggle never had.** ... the plug is both the historical verb and the consumable that makes tapping a decision rather than a reflex. It also completes the blow-in sequence ... so blowing in costs one plug"). An implementer told to 'pick whichever' will reasonably mirror `CanalUnsealClayRefund`, which U4.7 Step 8 explicitly holds up as the model.

### `MISSING_TASK` · hearth · The crucible renders on the same fill-level idiom as the charge pile — one visible band per material, denser at the bottom (iron under slag).

- **Task:** none
- **Doc:** [layered-charge.md](../design/layered-charge.md) § The hearth
- **Doc says:** Same fill-level idiom, one band per material, denser at the bottom:  * **charge pile** — coke under burden * **crucible** — iron under slag
- **Plan says:** U4.1 Step 3 gives the block `.Shape("game:block/basic/cube")` — a full opaque cube; no U4 task adds a fill-level renderer, fill quads or band geometry, and U4.4 Step 8 mentions only glow staleness
- **Why it matters:** Every other molten container in the suite renders its level (`fillStart` / `fillHeight` / `fillQuadsByLevel`, molten-canal.md § Fill geometry), and the doc ties the visible level directly to the operating decision it wants — 'the cap is a spout you can look at' (doc:623) only reads if the pool level is visible against the notch. As planned the hearth is an opaque cube whose only feedback is block light, so a player cannot see the pool rise toward the cinder notch and the 'competing volumes' rhythm has no display even if it is built.

### `DOC_STALE` · hearth · The doc asserts the blast furnace's per-tick hand-down to the tap is a hard-coded `min(20, pool)` that "contradicts the settled 50 u/s rule".

- **Task:** U4.4 Step 5
- **Doc:** [mechanics/molten-network.md](../design/mechanics/molten-network.md):208
- **Doc says:** | blast-furnace per-tick hand-down | `min(20, pool)` | `BlockEntityBlastFurnace.cs:346, 382` | **contradicts the settled 50 u/s rule** |
- **Plan says:** U4.4 Step 5: "Keep every arithmetic detail: `Math.Min(IwexValues.TapDrainPerTick, pooled)`" — the plan treats the rate as a live config key and preserves it
- **Why it matters:** Verified in source: `IwexConfig.cs:306` declares `public int TapDrainPerTick { get; set; } = 50;` and it is read at `BlockEntityShaftFurnace.cs:360` and `:397`. So the rate is (a) config, not hard-coded, (b) 50, not 20, and (c) already conforms to the settled one-number 50 u/s rule rather than contradicting it — and the cited file does not exist. molten-network.md § Open (:282) repeats the same wrong claim inside the throughput-unification list. An implementer reading the doc during U4.4 would either 'fix' a rate that is already right or count the furnace tap as an outstanding violation that has already been closed.

### `DOC_STALE` · puddling · (stale) The puddling furnace is a BlockEntityBlastFurnace subclass that inherits its product pools, so a furnace reaching Melting accumulates undrainable molten pig iron and snuffs itself.

- **Task:** U6.8 (and U6.6 Step 6, which corrects only the B8 temperature line)
- **Doc:** [machines/puddling-furnace.md](../design/machines/puddling-furnace.md):388
- **Doc says:** The *other half* is still open: the inherited molten pools. A puddling furnace that ever *does* reach `Melting` accumulates molten pig iron it can never drain, hits `LiquidCapacityReached` … and snuffs itself
- **Plan says:** The plan records the page as stale — the class is on the BlockEntityFireboxFurnace branch inheriting none of the molten members — but no U6 task corrects the page, and U6.8 makes the furnace reach Melting for the first time with no pool handling at all.
- **Why it matters:** Verified at src/IronworkingExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingFurnace.cs:31 — `public class BlockEntityPuddlingFurnace : BlockEntityFireboxFurnace`, whose class doc says it "overrides none of the core's molten-product members and inherits their truthful defaults". This is the same failure shape as the hearth-crucible incident, inverted: the page asserts an inheritance the code does not have. An implementer of U6.8 ("the 9-pig charge becomes a bath") who reads Open #7 would either plumb a drain that cannot exist or treat the melt as a known self-extinguishing dead end. The page's § Numbers FSM-bindings table (lines 256–263: MeltingPoint / MaxFuelBurnTime / MeltStartDelay / MeltIntervalSec / BlastMixRequiredToFire / AcceptedFamilies / product pools, all cited to BlockEntityBlastFurnace.cs) and its Depends-on line at :24 are stale in the same way, and B8's first cause there ("BlastMixRequiredToFire is inherited from the blast furnace") was superseded by the sealed derived MinChargeToIgnite the plan consumes in U6.3.

### `DOC_STALE` · puddling · (stale) Natural draught saturates at 0.85, so the reheat furnace's T_process ceilings at ≈1486 °C and thirty courses still give a very hot furnace.

- **Task:** U6.5 Steps 1-2
- **Doc:** [machines/reheat-furnace.md](../design/machines/reheat-furnace.md):36
- **Doc says:** With draught saturating at `natural = 0.85`, `T_in` tops out near 1906 °C; … `T_process` ceilings at **≈1486 °C — just below iron's 1538**. Build thirty courses and you still only get a very hot furnace.
- **Plan says:** U6.5 builds the settled peaking curve — `natural(courses) = base + gain·√courses − friction·courses²`, peak near 9 courses — and Step 2 pins `natural(30) < natural(0)`, i.e. thirty courses is worse than a bare flue.
- **Why it matters:** docs/design/machines/crucible-furnace.md:71 heads the replacement "### The draught curve *peaks* — it does not clamp *(settled 2026-08-02)*", tabulates the peak at 0.747 (:90) and `natural(30) = 0.184` — "catastrophically worse than no chimney at all" (:93) — and puts the reheat furnace at "roughly 1090-1370 °C across its entire buildable span" (:102). So 0.85 is unreachable and ≈1486 is not this machine's ceiling. reheat-furnace.md is where the reheat implementer looks: U7.1 sizes the soak against "the furnace's process temperature" and this page is its § Incoming design block. Worse, U6.5's own StackDraughtTests asserts the exact opposite of the thirty-course sentence, so the page reads as evidence of a bug. U6.5 has no doc-sync step at all (U6.6 Step 6 fixes only puddling-furnace.md § Gotchas), so this line survives the unit that invalidates it. Note crucible-furnace.md carries the same superseded sentence in its transfer-loss section (another cluster's doc).

---

## Low (7)

### `DOC_STALE` · crosscut · The orientation inventory still lists a SideWord scheme spelled in vanilla words for the tall hopper and the cowper intake; both now spell letters, and no SideWord scheme exists in the built registry.

- **Task:** U10.1 Step 4 and U10.5 Step 7; U10.7 Step 5 corrects only :85-97 and :149-156
- **Doc:** [mechanics/orientation-schemes.md](../design/mechanics/orientation-schemes.md):41
- **Doc says:** | **SideWord** | `north east south west` | tall hopper, cowper intake, *and vanilla's coke-oven door* |
- **Plan says:** Assigns only the twelve built schemes (Face / FaceAll / Axis / AxisFlat / DirectedAxis(Flat) / PipeBend / PipeTee / PipeCross / Canal*) and pins `iwex:hopper-tall-e` in the surviving `multiblockFacings`
- **Why it matters:** Verified in the generated tables: `iwex:hopper-tall` and `smex:cowperstoveheatsink`/`cowperstove` all declare `side(n|e|s|w)`, and `src/ExpandedLib/Helpers/ExOrientations.cs:127-202` declares twelve schemes with no SideWord among them. U10.7 is the plan's designated doc-surgery task for this page and its step list stops short of the inventory table at :35-48 and the Status line at :3 ("nothing built beyond the partial support"), which the plan already flags as false. Leaving the inventory uncorrected keeps the page teaching two vocabularies after the merge that removed one.

### `DOC_STALE` · crosscut · `EXLIB_WRITE_GOLDENS` is documented as taking the value "1"; it now also takes a comma-separated list of `domain/path` fragments, which is what every blessing step in the plan depends on.

- **Task:** U10.1 Step 6, U10.5 Step 7 (correct form) vs U4.5 Step 5, U8.3 Step 8, U8.4 Step 9 (repo-relative form)
- **Doc:** [mechanics/recipes-config.md](../design/mechanics/recipes-config.md):182
- **Doc says:** | `EXLIB_WRITE_GOLDENS` | `"1"` | `DefinitionGoldens.cs:160-161` | env var that opts into golden regeneration |
- **Plan says:** "Re-bless the 41 goldens with a scoped list … never `=1`" and "a path filter, never =1 (DefinitionGoldens.cs:152-163)"
- **Why it matters:** Verified at `DefinitionGoldens.WriteAll` — the filter is matched with `relative.Contains(f)` where `relative` is `def.Location.Domain + "/" + def.Location.Path`, and the doc-comment says so explicitly ("the variable also takes a comma-separated list of paths"). Two consequences: (1) recipes-config.md, which owns the goldens harness, still tells a fresh implementer the only value is `1` — the domain-wide bless the plan's own Biggest-risks §5 calls the largest laundering hazard in a dirty tree; (2) four plan steps spell the filter as `test/IronworkingExpanded.Tests/goldens/iwex/...`, which is never a substring of `iwex/blocktypes/...` and therefore blesses nothing. Normalise every step to the `domain/path` form the code matches on.

### `CONTRADICTS` · crosscut · An abstract base belongs at the machine-folder root, not inside a per-kind subfolder; `Blocks/` (and by the same reading `BlockEntities/`) is for the concrete variants — the shipped shape is `Furnaces/BlockEntityFurnaceCore.cs` at the root.

- **Task:** U8.2 Files / Produces
- **Doc:** [conventions.md](../design/conventions.md):172
- **Doc says:** - An abstract base belongs at the **machine-folder root**, beside its block-entity counterpart —   `Furnaces/BlockFurnaceCoreBase.cs`, not `Furnaces/Blocks/`. `Blocks/` is for the concrete variants.
- **Plan says:** Create `src/IronworkingExpanded/BlockStructures/Forming/BlockEntities/BlockEntityMpBench.cs` — `public abstract class BlockEntityMpBench : BlockEntityNetworkNode, IMpEnergyConsumer`, the shared base of all three benches
- **Why it matters:** `BlockEntityMpBench` is the family root for the shear, nail machine and rivet machine, i.e. the exact counterpart of `BlockEntityFurnaceCore`, which sits at `Furnaces/` root; the plan files it one level down in `BlockEntities/` alongside the concrete leaves. Weaker than the other findings because the doc's example names `Blocks/` explicitly and the repo already keeps the abstract branch class `BlockEntityFireboxFurnace` inside `Furnaces/BlockEntities/` — so this is a family-root-vs-branch distinction. Worth one decision now rather than a folder move after four files import it.

### `CONTRADICTS` · crosscut · `-` is a segment boundary between family, member and variant groups, never a word break; compound words are squashed.

- **Task:** U6.10 Produces / Step 3
- **Doc:** [mechanics/naming.md](../design/mechanics/naming.md):173
- **Doc says:** The separator carries meaning: every `-` in a code is a boundary between a family, a member and the variant groups. **Compound words are squashed, not hyphenated** — `rollingmill`, `sandcastingbed`,
- **Plan says:** "Produces: `iwex:puddled-ironball` at 200 u", with its shape at `assets/iwex/shapes/item/puddled-ironball.json` (no `puddled/` folder)
- **Why it matters:** The `-` here separates an adjective from a compound noun rather than a family from a member, and the shape path confirms it — the segment is not a folder, which is the test N5 offers (`hopper-tall` is legal "because `hopper` is a family and `tall` is its member, not because 'hopper tall' is two words"). Defensible as family `puddled` + member `ironball` if that is the intent, but then N2 wants the asset filed under a `puddled/` folder and nothing else joins the family. Cheapest resolutions: `puddledironball`, or file it in a real family the way `castplate-heavy` does.

### `DOC_STALE` · crucible · This furnace's damper is the bottom bypass at the flue base, not a cap at the stack top — the plan is right and the Structure/Operation tables are the stale text.

- **Task:** U9.9 Step 4
- **Doc:** [machines/crucible-furnace.md](../design/machines/crucible-furnace.md):245
- **Doc says:** | **damper** | stack top | the one air control a natural-draught furnace has |
- **Plan says:** U9.9 Step 4: 'draw the damper at the flue base with CellRole.Damper'.
- **Why it matters:** The dated settled subsection on the same page says the opposite at :274 — 'The puddling cap and this furnace's bottom damper are the same mechanic at opposite ends of the flue — top cap versus bottom bypass' — and the Owns bullet (:183) puts the flue at the bottom. The Operation table repeats 'stack top' at :365. The plan's placement is also the only workable one: a damper on the player-built stack top is outside the footprint, and a BlockEntityFurnacePart cannot resolve a core more than ComponentScanAbove = 1 away — the same trap U9.3 Step 2 flags for the coke-oven lid. Two body tables need correcting to match the settled box.

### `DOC_STALE` · fasteners · fasteners.md still states the fastener-by-tier rule — nails and bolts are the iron tier's, rivets are the steam tier's, arriving with lpex's boiler.

- **Task:** U8.11 Step 2
- **Doc:** [items/fasteners.md](../design/items/fasteners.md):60
- **Doc says:** **Nails and bolts are the iron tier's fastener; rivets are the steam tier's.** The reason is physical, not a placement convenience:
- **Plan says:** U8 correctly implements state.md's 2026-07-30 replacement (rivets are iwex; the tier gate becomes a substitution rule), but U8.11's fasteners.md instruction is only "strike the bolt, the die column, and the stale asset table naming item-rod-rolled.json / item-rod-nail.json".
- **Why it matters:** The doc-surgery step names the bolt, the dies and the asset table but not the two sections that carry the superseded decision — § Owns bullet 3 (:13-14) and § The tier rule (:58-72, "the rivet arrives with the first thing that holds pressure — lpex's boiler — and not one step earlier") — nor Gotcha 4 (:253-256), which still declares the bench a heading machine and "`rivetrod` a dead name" on the strength of the 2026-07-29 draft. After U8.11 as written, the page's headline design rule still contradicts the code U8.6 ships, which is how this class of drift restarts.

### `MISSING_TASK` · forming · The flat roll art is drawn to the shipped schedule (2.0/1.5/1.0/0.5), not the settled 2.5/2.0/1.5/1.0; the grooved art is already correct.

- **Task:** none
- **Doc:** [items/roll-sets.md](../design/items/roll-sets.md):224
- **Doc says:** | `assets/editable/shapes/item-rollers-flat.json` | four 4-voxel barrel segments named `20Gap`/`15Gap`/`10Gap`/`05Gap`, up-and-down roll pairs | iwex `flat` | drawn to the **shipped** schedule, not the settled one |
- **Plan says:** U7.3 Step 5 rewrites the flat set to gaps [2.5,2.0,1.5,1.0] and cites the same file as its authority for barrelWidth 4.0; the plan's ruling 1 is 'no redraws' and no U7 task touches the flat roll art.
- **Why it matters:** Verified by parsing assets/editable/shapes/item-finished-rollers-flat.json (the renamed file): its segments are still 20Gap/15Gap/10Gap/05Gap Down+Up, while item-finished-rollers-grooved.json is 25/20/15/10 — the doc's measurement still holds after the rename. rolling-mill.md:134 tabulates the settled flat schedule as '2.5 / 2.0 / 1.5 / 1.0, barrel 4'. The plan is right to take barrel 4 from that art (roll-sets.md:244 confirms every segment is 4 voxels) but the gap grooves it draws are the ones being deleted. Latent only because the sets still ship game:item/ingot — but U7.10 makes both sets craftable, so the two survival items remain visually identical in the hand (roll-sets.md:317: 'a set is visually indistinguishable from any other set … Mis-fitting … is silent'), and if the art is ever wired the flat set will draw a 0.5 groove that no longer exists.
