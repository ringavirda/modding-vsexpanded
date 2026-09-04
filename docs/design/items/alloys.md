# Alloys and the metal ladder

**Status** partial - 3 metals of 16 exist as material identities (`pigiron`, `castiron`, `bessemersteel`;
`slag` is a fourth entry but not a metal). The alloying mechanic itself does not exist: there is no
[ladle](../machines/ladle.md), no ferroalloy, no window catalogue, and `MetalDef.Alloy` is read by nothing.
**Mod** exlib owns the registry and the emitters; iiex and siex own the metals themselves

**Owns** — the facts this page is canonical for:

* the metal ladder - every metal in the ferrous line, what it guarantees, what it is for, which mod owns it,
  and its build status. One row per metal, and this page is the row's home;
* D3 - alloys inherit their base's grade as a continuous pressure penalty, not a lockout - and what that
  ruling costs each alloy;
* the ferroalloys as a material family (spiegeleisen, high-carbon ferromanganese, ferrochrome, ferrosilicon) -
  what they are and what they guarantee, as distinct from where they are smelted or melted - including the
  ruling (2026-08-07) that spiegeleisen and FeMn are two distinct items, and their pinned compositions;
* the `MetalDef` contract as shipped: which fields are live, which are exercised only by tests, and which are
  declared and read by nothing - `Alloy`, `MetalAlloySpec`, `IsAlloy`, `MeltingPointOverride`, `UnitsPerBit`,
  `Media`, `GlowMinTemp`;
* the two-pass catalogue load and the convention fallbacks a metal gets for free when it ships no def;
* the generated item family - which resource forms and tools a metal opts into, the three stat presets and
  their numbers, and the smelt-back ratios;
* the audit of the four shipped metal defs against the ladder - including that `bessemersteel.json` ships
  `tools: {preset:"good"}` against [N1](../../internal/plans/STATE.md);
* every material identity, composition and ratio on the ferrous ladder -
  [materials.md](../materials.md) is a pointer page and this page is the home.

**Does not own** — cited only, never restated:

| Fact | Owner |
|---|---|
| `1 vx³ = 2.5 u`, every item mass, and the dead `materialUnits` attribute | [density rule](../mechanics/density-rule.md) |
| mixing by held proportion, the chill model, waste alloy, recarburisation as a verb | [ladle](../machines/ladle.md) |
| the blow, its carbon bands, the retype site, capacity, the cold-scrap gate | [Bessemer](../machines/bessemer.md) |
| what blown iron is and the order it must land in | [blown iron](blown-iron.md) |
| bath alloying (D6), the low-N argument, the producer-gas dependency | [open hearth](../machines/open-hearth.md) |
| blister → pot → crucible steel, the pot, the stack-height problem | [crucible furnace](../machines/crucible-furnace.md) |
| melting a ferroalloy, and why fuel contact makes the cupola right for it | [cupola](../machines/cupola.md) |
| smelting a ferroalloy - the burden family, the coke cost, the fuel-vs-throughput trade | [cold blast furnace](../machines/blast-furnace-cold.md), [burden](burden.md) |
| the chrome-steel supply chain end to end, the ball die, the bootstrap invariant | [bearings](../machines/bearings.md) |
| pig iron as an item, its mass and its 9-pig charge | [pig](pig.md), [puddling furnace](../machines/puddling-furnace.md) |
| wrought balls, blooms, shingling, cast slab/bloom/billet | [stock](stock.md), [long cell](../machines/long-cell.md) |
| the cast ↔ fabricated substitution (N3) | [cast parts](cast-parts.md), [rolled parts](rolled-parts.md) |
| pipe network pressure, burst, R1 single medium | [pipe network](../mechanics/pipe-network.md), [cast pipes](../machines/cast-pipes.md), [rolled pipe](../machines/rolled-pipe.md) |
| `IMoltenCell`, `FlowEdge`, the metal-type refusal, the no-op merge | [molten network](../mechanics/molten-network.md) |
| code-first `ExItemDef`, goldens, `ExRecipeCosts`, `/exmod` | [recipes & config](../mechanics/recipes-config.md) |
| melt-back recovery granularity and the ≤32 / ≤48 invariant | [recoverability](../mechanics/recoverability.md) |
| status of everything, D1–D9, N1–N3, the blocker list | [STATE.md](../../internal/plans/STATE.md) |

**Depends on** [density rule](../mechanics/density-rule.md) · [molten network](../mechanics/molten-network.md) ·
[recipes & config](../mechanics/recipes-config.md) · [ladle](../machines/ladle.md) ·
[Bessemer](../machines/bessemer.md) · [blown iron](blown-iron.md) ·
[open hearth](../machines/open-hearth.md) · [crucible furnace](../machines/crucible-furnace.md) ·
[cupola](../machines/cupola.md) · [cold blast furnace](../machines/blast-furnace-cold.md) ·
[bearings](../machines/bearings.md) · [STATE.md](../../internal/plans/STATE.md)

---

## Role

A metal in this suite is a promise about what may be built with it: two mild steels with identical carbon are
different materials because one may become a boiler and one may not. The ladder is not a power curve but a set
of guarantees, and a player climbs it because a machine refuses to be built from anything weaker.

There is no ppm attribute, no hardness stat and no quality tag. The grade is the material identity:
`siex:ingot-bessemersteel` and a future `siex:ingot-openhearthsteel` differ by nothing except which recipes
accept them.

Three consequences:

1. Every metal must justify its row by naming a guarantee no other metal on the ladder carries. A metal that
   guarantees nothing new is a skin, and the suite does not ship skins.
2. An alloy is not a new promise, it is a modified one. D3 (below) makes an alloy inherit its base's guarantee
   and pay a continuous penalty for a weak one, so "hadfield steel" is two products, not one.
3. Identity lives in one JSON file per metal. `assets/<domain>/config/metals/*.json` is the whole surface;
   the molten system, the molds and the item generator all read the same `MetalDef`
   (`ExpandedLib/Metals/MetalDef.cs:18`).

### Why this page carries no `section × length` / `vx³` / `mass` columns

A metal has no geometry; its forms do, and they belong to [stock](stock.md), [cast parts](cast-parts.md) and
[rolled parts](rolled-parts.md), with the rule itself owned by [density rule](../mechanics/density-rule.md).
The one geometry-adjacent number a metal owns is its density in kg/m³: vanilla tooltip flavour, not the
`2.5 u/vx³` rule - see [Numbers](#numbers).

---

## The catalogue

The ferrous ladder. "shipped" marks a metal whose `MetalDef` ships today. Scope is ferrous only
([D8](../../internal/plans/STATE.md)); non-ferrous (copper, the bronzes, pure copper) is deferred and has no row here.

| Metal | Registry code | What it guarantees | Made by | Consumed by | Mod | Status |
|---|---|---|---|---|---|---|
| **Pig iron** | `pigiron` (shipped) | nothing - a feedstock, and the ladder's only metal that promises nothing | [cold blast furnace](../machines/blast-furnace-cold.md) | [cupola](../machines/cupola.md), [puddling](../machines/puddling-furnace.md), [Bessemer](../machines/bessemer.md) | iiex | live |
| **Cast iron** | `castiron` (shipped) | it can be poured to shape, and it will shatter in tension | [cupola](../machines/cupola.md) | [cast parts](cast-parts.md); gates LP machinery | iiex | live |
| **Wrought iron** | (= vanilla `game:iron`) | it can be forged, and it passes the pressure gate | [puddling](../machines/puddling-furnace.md) → shingling | the whole forming line; boilers | iiex | metal live (vanilla), route blocked (B8) |
| **Blister steel** | (vanilla `game:ingot-blistersteel`) | carburised bar - feedstock only, uneven | vanilla cementation | shear steel, [crucible](../machines/crucible-furnace.md) | vanilla | live |
| **Shear steel** | (= vanilla `game:steel`) | the tool-grade baseline; the pre-Bessemer standard | vanilla piling + helve | vanilla tools | vanilla | live |
| **Crucible steel** | (none) | the best edge in the game - homogeneous, slag-free high-C | [crucible furnace](../machines/crucible-furnace.md) | tools, weapons, [shear](../machines/shear.md) blade sets | iiex | designed |
| **Blown iron** | (none) | nothing - it is unusable. See [blown iron](blown-iron.md) | [Bessemer](../machines/bessemer.md) blow | [ladle](../machines/ladle.md), and nothing else | smex | designed (N1) |
| **Bessemer steel** | `bessemersteel` (shipped) | structural volume, cheaply - and it is barred from pressure work | [ladle](../machines/ladle.md) recarburising blown iron | fabricated substitutes (N3), bulk machine frames | smex | live, but its def conflicts with N1 - see [Gotchas](#gotchas) |
| **Open-hearth steel** | (none) | a spec - the pressure grade, and the only base an alloy can inherit cleanly | [open hearth](../machines/open-hearth.md) | boiler plate, pressure parts, HSS feedstock | smex | designed |
| **Ingot iron** | (= vanilla `game:iron`, today) | slag-free, ~0 % C, soft - not wrought iron | Bessemer over-blow; later the arc furnace | recarburise or remelt | smex / elex | live as a retype target only |
| **Hadfield steel** | (none) | it survives impact and abrasion - the HP machinery material gate | [ladle](../machines/ladle.md) / [open-hearth](../machines/open-hearth.md) bath + ~12.5 % Mn | every hpex machine; rolled pipe | smex | designed |
| **Chrome steel** | (none) | hardness with accuracy - it holds a ground surface. Defined 2026-08-07: ~1 % C, ~1.5 % Cr | one route: the [ladle](../machines/ladle.md) + ferrochrome | [bearings](../machines/bearings.md), files | smex | designed - composition settled |
| **HSS** | (none) | it stays hard hot - needs no tempering | arc furnace + W + Cr | endgame tools | elex | deferred (D8) |
| **Spiegeleisen** | (none) | it scavenges oxygen and carries carbon - the historical fix for a blow; at a 10–15 % dose, the rail-grade route | [cold blast furnace](../machines/blast-furnace-cold.md) burden family | [ladle](../machines/ladle.md): recarburising | iiex → smex | designed |
| **High-carbon ferromanganese** | (none) | it delivers manganese in bulk - the mild-steel trim and the ladle's alloying reagent | same burden family | [ladle](../machines/ladle.md): mild trim, hadfield | iiex → smex | designed |
| **Ferrochrome** | (none) | it delivers chromium into a bath without an electric furnace | same burden family | chrome steel, later HSS | iiex → smex | designed |
| **Ferrosilicon** | (none) | a second deoxidiser, and the cheap one | same burden family | steel practice generally | iiex → smex | later |
| **Waste alloy** | (none) | it guarantees only its base mass back - the off-spec outcome | a bad [ladle](../machines/ladle.md) mix | cupola / Bessemer scrap / arc | any | designed |
| (slag) | `slag` (shipped) | not a metal - a registry entry so the molten system can carry it | every furnace | brick, fettle | iiex | live |

### D3 — an alloy inherits its base's grade, as a continuous penalty

Settled ([STATE.md § D3](../../internal/plans/STATE.md)):

> **Alloys inherit their base's properties.** Critical machinery built from a lesser base gets a **lower max
> pressure** — not a refusal.

Its consequences, per alloy:

| Alloy | On a Bessemer base | On an open-hearth base |
|---|---|---|
| Hadfield steel | builds every hpex machine, at a reduced pressure ceiling | the full HP band |
| Chrome steel | bearings that work, rated lower | the reference product |
| HSS | the one exception - open-hearth steel is HSS-exclusive feedstock | the only route |

Bessemer steel, barred from pressure work by grade, exists for structural substitution (N3,
[cast parts](cast-parts.md)); under D3 a player with only a Bessemer converter is capped on hadfield, never
blocked. That is R5 (efficiency, not possibility) applied to a material rather than to a machine.

Nothing implements D3. There is no per-material pressure rating anywhere in `src/`; the only thing resembling
one is the pipe-tier burst ladder, keyed on the mod domain, not on the metal - see [Numbers](#numbers) and
[Gotchas #6](#gotchas).

### The ferroalloys are one family, not three errands

They share a definition - an iron carrier for an element that will not reduce on its own - and therefore share
every mechanic: one burden family in the cold [blast furnace](../machines/blast-furnace-cold.md), one remelt in
the [cupola](../machines/cupola.md), one hand-drop port on the [ladle](../machines/ladle.md).

Two of the three elements do not exist as vanilla metals. The vanilla metal worldproperty lists 23 codes
including `chromium`, but no `manganese` and no `tungsten`
(`<VS>/assets/survival/worldproperties/block/metal.json`, verified). So ferromanganese and (later) HSS need new
metal identities and a reduction story, while ferrochrome could in principle lean on a vanilla one.

### Pinned identities (settled 2026-08-07) — the two reagents, and chrome steel

Spiegeleisen and high-carbon ferromanganese are two distinct items, roughly 10× apart in Mn strength and not
interchangeable: dose one where the other belongs and the arithmetic misses by an order of magnitude
([recarburising](../processes/recarburising.md) § Numbers back-solves both compositions from the design's own
stated masses). This page is where the fractions live:

| Material | Composition | Job |
|---|---|---|
| **spiegeleisen** | ~3.8 % C, low Mn - roughly a tenth of FeMn's strength | the historical Bessemer additive; a 10–15 % dose lands rail-grade steel at 0.45–0.65 % C |
| **high-carbon ferromanganese** | ~80 % Mn, high-C - its exact carbon fraction is still a design lever ([alloying](../processes/alloying.md) § Numbers) | the ladle's alloying reagent (hadfield's ~1000 u), and the mild-steel trim (~159–186 u lands ~0.2 % C) |
| **chrome steel** | ~1 % C, ~1.5 % Cr - base ferroalloy FeCr (the FeMn / FeCr / FeSi family), one route: via the [ladle](../machines/ladle.md) | a deliberately minimal row - it unblocks [bearings](../machines/bearings.md) and N2, and nothing more is pinned |

### Compositions (target fractions)

This page owns the target fractions. Tolerance windows are still open - see Open 3.

| Material | Base | Carbon | Other (target) | Character |
|---|---|---|---|---|
| Pig iron | ~96 % Fe | ~4.0 % C | Si/Mn/P/S (slag flavour) | very high C, brittle |
| Cast iron | ~97 % Fe | ~3.0 % C | — | castable, brittle |
| Wrought iron | ~99.9 % Fe | <0.1 % C | fibrous slag stringers | = vanilla iron |
| Blister steel | ~99 % Fe | ~1.0 % C | surface-carburised, uneven | shear + crucible feedstock |
| Shear steel | ~99 % Fe | ~0.8 % C | — | = vanilla `game:steel`; homogenised tool steel |
| Crucible steel | ~98.8 % Fe | ~1.2 % C | — | homogeneous high-C tool steel |
| Bessemer steel | ~99.8 % Fe | ~0.2 % C | high-N | structural mild steel |
| Open-hearth steel | ~99.8 % Fe | ~0.2 % C | low-N | pressure-grade mild steel |
| Ingot iron | ~100 % Fe | ~0 % C | — (slag-free) | over-blow / arc; not wrought iron |
| Hadfield steel | ~86.3 % Fe | ~1.2 % C | ~12.5 % Mn | austenitic, work-hardening |
| HSS | ~77.25 % Fe | ~0.75 % C | ~18 % W, ~4 % Cr | hot-hard; W-Cr (no vanadium) |

Wrought iron is puddling-only: its toughness is slag fibres worked into a pasty ball and elongated by
shingling, which no fully molten route can reproduce. An over-blown or arc-smelted melt yields ingot iron
(slag-free, ~0 % C), never wrought iron.

Non-ferrous compositions, kept here for when the non-ferrous scope opens (deferred; no catalogue rows):
tin bronze ~88 % Cu + ~12 % Sn; brass ~70 % Cu + ~30 % Zn (needs a coke cover or the zinc boils off);
bismuth bronze ~60 % Cu + ~25 % Zn + ~15 % Bi and black bronze ~84 % Cu + ~8 % Au + ~8 % Ag (both follow
the vanilla `metalalloy` ranges - verify against the installed game before pinning); converter copper
~98 % Cu (impure rod/wire stock); pure copper ~99.95 % Cu (electrolytic, low-resistance wire).

---

## Numbers

Every metal's identity is JSON; the emitters supply the rest. `<domain>Values.X` is a generated accessor over
`<domain>Config.X`, so the file:line is the config declaration.

### The four shipped metal defs

| Field | `pigiron.json` | `castiron.json` | `bessemersteel.json` | `slag.json` |
|---|---|---|---|---|
| file | `assets/iiex/config/metals/pigiron.json:1-13` | `…/castiron.json:1-16` | `assets/siex/config/metals/bessemersteel.json:1-16` | `…/slag.json:1-4` |
| `code` | `pigiron` | `castiron` | `bessemersteel` | `slag` |
| `moltenItem` | `iiex:ingot-pigiron` | `iiex:ingot-castiron` | `siex:ingot-bessemersteel` | `iiex:slag` |
| `solidDrop` | `game:metalbit-iron` | `game:metalbit-iron` | `game:metalbit-steel` | (convention) |
| `castDomain` | `iiex` | `iiex` | `smex` | — |
| `liquidThreshold` | 0.75 | 0.75 | (default 0.8) | (default) |
| `density` (kg/m³) | 7000 | 7200 | 7820 | — |
| `meltingPoint` (°C) | 1150 | 1200 | 1500 | — |
| `isAlloy` | — | — | true | — |
| `itemForms` | `[ingot]` | `[ingot, plate, bits, rod, nails]` | `[ingot, plate, rod, nails]` | — |
| `tools` | (none - a feedstock) | `preset: brittle` | `preset: good` - against N1, see Gotcha 1 | — |

`slag.json` is two fields. Everything else about slag - its solid drop, its display name, its thresholds -
comes from the convention branch (`ExpandedLib/Metals/MetalRegistry.cs:83-135`).

### Convention fallbacks — what an unregistered metal gets for free

| Fact | Convention | file:line |
|---|---|---|
| molten item for a bare token | `game:ingot-<code>` | `MetalRegistry.cs:69-76` |
| solid drop | `ingot-X` → `metalbit-X`, same domain; a non-ingot drops as itself | `:169-172` |
| display name | strip `ingot-`, capitalise | `:174-179` |
| units per solid bit | 5 | `:93` |
| media | `["molten"]` | `:31` |
| liquid threshold | 0.8 × melting point | `ExlibConfig.cs:63` |
| hardened threshold | 0.3 × melting point | `:67` |
| glow floor | 500 °C | `:70` |
| recovery fallback | `iiex:slag` | `:75` |
| melting point | deferred to the item's vanilla `combustibleProps` - never duplicated on the def | `MoltenMetal.cs:113-114` |

The catalogue loads in two passes at `AssetsFinalize`: a baseline entry derived from every metal code in every
loaded `worldproperties/block/metal` (so vanilla's 23 and any EM/mod additions are enumerable), then an overlay
of every domain's `config/metals/*.json` (`MetalCatalogueLoader.cs:41-77`, asset read `:80-105`). A def missing
`code` or `moltenItem` is skipped with a warning (`:62-74`).

### The generated item family

Opt-in per metal via `generateItemFamily` (`MetalDef.cs:81`). Emitter defaults when a field is null:
density 7870, melting point 1482 °C, texture `game:block/metal/ingot/iron`, forms
`[ingot, plate, rod, nails]` (`MetalFamilyEmitter.cs:42-49`). Every form is stamped with the metal's
`MaterialDensity` and `storageFlags 5` (`:132-133`).

| Form | Item code | Max stack | Smelt-back | file:line |
|---|---|---|---|---|
| `ingot` | `<domain>:ingot-<metal>` | 16 | 1 → 1 ingot | `:145`, `:154` |
| `plate` | `metalplate-<metal>` | 8 | 1 → 2 ingots | `:305`, `:314-318` |
| `bits` | `metalbit-<metal>` | 128 | 20 → 1 ingot | `:234`, `:244` |
| `rod` | `rod-<metal>` | 16 | 1 → 1 ingot | `:387`, `:395` |
| `nails` | `metalnailsandstrips-<metal>` | 32 | 4 → 1 ingot | `:462`, `:471` |

Tool presets are flat, never `*byType`; every generated tool of a metal shares one stat block
(`MetalToolEmitter.cs:67-73`). Preset `none` emits no tools at all (`:80-84`); an unknown preset falls back to
`standard` (`:86-91`).

| Preset | Durability | Attack | Mining tier | Mining speed | Used by |
|---|---|---|---|---|---|
| `brittle` | 150 | 2.0 | 4 | 6.0 | cast iron |
| `standard` | 1000 | 2.25 | 4 | 7.5 | nothing ships it |
| `good` | 2600 | 2.5 | 5 | 9.0 | Bessemer steel - against N1, see Gotcha 1 |

Eight tool types by default - pickaxe, axe, shovel, hammer, saw, knife, chisel, scythe
(`MetalToolEmitter.cs:131-496`). Generated items are kept off the vanilla `block/metal` worldproperty on
purpose, so a castable-but-brittle metal never leaks an anvil-forgeable `workitem-<metal>`
(`MetalToolEmitter.cs:20-24`, `MetalFamilyEmitter.cs:30-35`).

### D3's only implementation, and it is keyed on the wrong thing

| Pipe tier | Material the docs claim | Burst | Registered as | file:line |
|---|---|---|---|---|
| plated (iiex) | wrought-iron plate | 2.5 atm | domain `iiex` | `IiexConfig.cs:163`, `IronIndustryExpandedModSystem.cs:73` |
| cast (iiex) | cast iron | 5.0 atm | domain `iiex` | `IiexConfig.cs:50`, `IronIndustryExpandedModSystem.cs:60` |
| rolled (hpex) | hadfield steel | 12 atm | domain `hpex` | `SiexConfig.cs:107-115`, `SteelIndustryExpandedModSystem.cs:42` |
| (unregistered) | — | 5.0 atm | fallback | `ExpandedLib/Blocks/Networks/BlockPipe.cs:185` (`RegisterBurst`) |

This is a continuous material→pressure ladder and the shape D3 asks for, but it is keyed on `Mod.Info.ModID`,
so the rating belongs to the block's mod, not to the metal it was built from: a hadfield pipe on a Bessemer
base and one on an open-hearth base are the same block and rate identically. D3 needs the key to move onto the
metal. The pipe base classes live in exlib (`src/ExpandedLib/Blocks/Networks`); the burst registry sits with
them and stays domain-keyed.

---

## Assets

| Asset | Path | State |
|---|---|---|
| metal defs | `assets/{iiex,smex}/config/metals/*.json` | 4 files: `pigiron`, `castiron`, `slag` (iiex), `bessemersteel` (smex). No `blowniron`, `openhearthsteel`, `cruciblesteel`, `hadfieldsteel`, `chromesteel`, `ferromanganese`, `ferrochrome`, `wastealloy` |
| shapes | — | none, by design. Every generated form paints a `game:` shape (`item/ingot`, `item/plate`, `item/nugget`, `item/rod`, `item/resource/metalnailsandstrips`) and every tool a `game:item/tool/*` shape |
| textures - cast iron | `assets/iiex/textures/block/metal/castiron.png` | live, referenced by `castiron.json:10` |
| textures - cast iron alt | `assets/iiex/textures/block/metal/castiron-alt.png` | orphaned - no `.cs`, `.json` or golden in the repo references `castiron-alt` |
| textures - pig iron | `game:block/metal/tarnished/iron` | vanilla, verified present |
| textures - Bessemer steel | `game:block/metal/ingot/steel` | vanilla, verified present. It is the vanilla steel texture, so Bessemer steel is visually indistinguishable from `game:steel`, a different material on this ladder |
| lang - iiex | `assets/iiex/lang/en.json:18-39` | complete: 2 metal names, 5 form names + descriptions, 8 tool names |
| lang - smex | `assets/siex/lang/en.json:3-19` | complete: 1 metal name, 4 form names + descriptions, 8 tool names - the lang file is the most visible trace of the N1 conflict |
| lang - exlib | `exlib:metal-unknown` | the empty-code label (`MetalRegistry.cs:100`) |
| goldens | `test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/{castiron,pigiron}/`, `test/SteelIndustryExpanded.Tests/goldens/siex/itemtypes/bessemersteel/` | 13 / 1 / 12 files - exactly `forms + tools` per def, so the goldens confirm the opt-in surface |
| handbook | — | no page for the metal ladder at all. Nothing in game explains why two mild steels differ |

---

## Code

| Type / member | file:line | Notes |
|---|---|---|
| `MetalDef` | `ExpandedLib/Metals/MetalDef.cs:18` | 20 fields; the whole material-identity surface |
| `MetalToolSpec` | `:118` | preset + 3 overrides + type list |
| `MetalAlloySpec` / `MetalAlloyIngredient` | `:138` / `:145` | dead - see below |
| `MetalRegistry` | `MetalRegistry.cs:22` | two indices over one def set: by molten-item `AssetLocation` and by short code |
| `ResolveByCode` / `MoltenItemOf` | `:69` / `:79` | how the Bessemer and the furnaces name a metal |
| reader helpers | `:83-155` | registered-override-else-convention, all of them |
| `CastProductOf` | `:144` | `{metal}` substitution + domain rehoming; the only consumer of `CastDomain` (`BlockEntityCastMold.cs:265`) |
| `MetalCatalogueLoader` | `MetalCatalogueLoader.cs:21` | two-pass load; `Populate` `:41` is asset-free and unit-tested |
| `MetalFamilyEmitter` | `MetalFamilyEmitter.cs:10` | the 5 resource forms |
| `MetalToolEmitter` | `MetalToolEmitter.cs:26` | the 8 tools + 3 presets |
| `MoltenMetal.MeltingPointOf` | `MoltenMetal.cs:113` | goes straight to the collectible - see the dead-field table |

### Declared and read by nothing

Confirmed by `grep -rn` over `src/` excluding `bin/`/`obj/`:

| Field | Declared | Read by production code | Read by tests |
|---|---|---|---|
| `MetalDef.Alloy` + `MetalAlloySpec` + `MetalAlloyIngredient` | `MetalDef.cs:71`, `:138`, `:145` | no - zero call sites | JSON-binding only (`MetalCatalogueLoaderTests.cs:111-137`) |
| `MetalDef.IsAlloy` | `:57` | no | asserted only (`MetalCatalogueLoaderTests.cs:129`, `BessemerSteelMetalTests.cs:37`) |
| `MetalDef.MeltingPointOverride` | `:39` | no - `MeltingPointOf` calls `GetMeltingPoint` on the collectible directly (`MoltenMetal.cs:113-114`) | — |
| `MetalRegistry.UnitsPerBitOf` | `MetalRegistry.cs:92` | no callers | — |
| `MetalRegistry.MediaOf` (and `MetalDef.Media`) | `:115` | no callers - the bridge to `IMediumTaxonomy` was never crossed | — |
| `MetalRegistry.GlowMinTempOf` (and `MetalDef.GlowMinTemp`) | `:129` | no callers | — |

`Alloy` is the dangerous one. Wiring it up would emit vanilla `AlloyRecipe`s, and vanilla alloy recipes snap an
off-ratio mix to the nearest alloy - the exact behaviour the [ladle](../machines/ladle.md)'s
"off-spec → waste alloy" rule forbids. It must stay inert. The other five are unused surface, and
`UnitsPerBit` in particular is a live gap: melt-back granularity is hard-coded at 5 u per bit everywhere
([recoverability](../mechanics/recoverability.md)) while the per-metal override sits unread.

### Where a caller hooks in

* A new metal is one JSON file. Ship `assets/<domain>/config/metals/<code>.json` with `code` + `moltenItem`
  and it is registered; add `generateItemFamily` and the whole item family and tool set appear with no C#.
  That is how `blowniron`, `openhearthsteel`, `hadfieldsteel` and the ferroalloys should land.
* A metal that must make no tools either omits `tools` (pig iron does) or names `preset: "none"`
  (`MetalToolEmitter.cs:80-84`).
* A metal that must make no items simply omits `generateItemFamily` - which is what a metal that only ever
  exists molten (blown iron) wants for its item forms, but not for its ingot.

---

## Gotchas

1. **`bessemersteel.json` ships a full tool family, against N1.** `generateItemFamily: true`,
   `itemForms: [ingot, plate, rod, nails]` and `tools: {preset: "good"}` (`bessemersteel.json:8-15`) yield 12
   items including a tier-5, 2600-durability pickaxe - better than any vanilla steel tool the player can
   forge. [N1](../../internal/plans/STATE.md) (the blow yields blown iron, not steel) and the ladder's own rule (tools
   come from shear / crucible / HSS steel) both say this must not exist. The fix is one preset token, but it
   must not land before the [ladle](../machines/ladle.md) does - see [blown iron](blown-iron.md).

2. **Bessemer steel wears vanilla steel's texture** (`game:block/metal/ingot/steel`), so the material the
   design most wants the player to distinguish from `game:steel` is the one that looks identical to it.

3. **`castiron-alt.png` is orphaned art** - drawn, shipped, referenced nowhere.

4. **Ingot iron has no identity of its own.** The Bessemer over-blow retypes the bath to
   `MetalRegistry.MoltenItemOf("iron")` → `game:ingot-iron`
   ([Bessemer](../machines/bessemer.md#operation)), i.e. the over-blow product is vanilla iron, which is also
   wrought iron on this ladder. The compositions table insists the two are different materials ("slag-free,
   ~0 % C - not wrought iron"); in code they are the same item. Either ingot iron gets a def or the row goes.

5. **`liquidThreshold: 0.75` appears twice and is never explained.** Pig and cast iron both override the 0.8
   default (`pigiron.json:7`, `castiron.json:7`); Bessemer steel does not. The effect is that high-carbon irons
   stay pourable further below their melting point, which is physically right, but nothing says so and a new
   metal author has no rule to follow.

6. **The pipe burst ladder is the design's only material→pressure gate, and it cannot express D3** - it is
   keyed on the mod domain (`BlockPipe.RegisterBurst(Mod.Info.ModID, …)`), not on the metal. Any real
   implementation of "alloys inherit their base's grade" has to move that key.

7. **A metal's melting point lives in two unrelated places depending on whether its item exists.**
   `MetalDef.MeltingPoint` (`:104`) authors `combustibleProps` for a generated item;
   `MetalDef.MeltingPointOverride` (`:39`) overrides the read for an item that already exists, and is dead. So
   the melting point of a non-generated metal is whatever its vanilla item says, with no escape hatch that
   works.

8. **`materials.md` is a pointer page.** Its unit-economy, materials and alloy-composition tables are retired;
   this page owns every material identity, composition and ratio on the ferrous ladder, including the two
   recarburiser reagents and chrome steel (§ Pinned identities). If a fraction is not on this page or a page it
   delegates to, it is not settled.

9. **`MetalToolSpec.ToolTypes` and the `standard` preset are shipped-but-unused surface.** No def names a tool
   subset and none uses `standard`, so both paths are exercised only by unit tests.

10. **The registry is process-wide static state.** `MetalCatalogueLoader.Load` clears before repopulating
    (`:27`), which is what makes a second world load in one process clean - but it also means any test or tool
    that registers a metal without clearing leaks into the next one.

---

## Open

1. **Nine metals on the ladder have no `MetalDef`.** In dependency order the ones that unblock other work
   are: `blowniron` (must follow the ladle, see [blown iron](blown-iron.md)), `ferromanganese` and
   `spiegeleisen` (two defs - distinct reagents, settled 2026-08-07), `openhearthsteel`, `hadfieldsteel`,
   `chromesteel`. Each is one JSON file.

2. **D3 has no mechanism.** "Lower max pressure" needs somewhere to live - a per-metal rating read by
   boilers, pipes and HP machines. The pipe ladder shows the shape of the answer and shows why the current key
   is wrong ([Numbers](#numbers)). Until this exists, hadfield's whole reason to be two products is
   design-only.

3. **Where do the alloy windows live in code?** This page carries the target ratios
   (§ Compositions) but no tolerance and no code. `MetalDef.Alloy` is the wrong
   home (it snaps). A catalogue beside `config/metals/`, loaded the same way, keyed by product
   metal - decided jointly with the [ladle](../machines/ladle.md#open) and the
   [open hearth](../machines/open-hearth.md).

4. **Chrome steel's composition is settled** (2026-08-07: ~1 % C, ~1.5 % Cr, FeCr via the ladle,
   § Pinned identities). [bearings § Open 4](../machines/bearings.md#open) asks for a catalogue row, a
   `MetalRegistry` entry and a grade under D3 - the row and the composition are above; the `MetalDef` entry
   and the grade are still owed.

5. **Manganese has no reduction story.** It is not a vanilla metal, and rhodochrosite is a vanilla ore, so
   the FeMn burden family is the only proposed path. If it slips, hadfield, recarburisation and therefore the
   entire settled Bessemer route slip with it. See [burden](burden.md).

6. **Nothing teaches the ladder.** The guarantee model is the suite's central material idea and it exists only
   in design docs - no handbook page, no in-game text, no tooltip. A player meeting "this recipe wants
   open-hearth steel" has no way to learn why.

7. **Should `IsAlloy` mean anything?** It is authored on Bessemer steel and asserted by two tests, and reads as
   a promise the code never keeps. Either give it a job (a handbook grouping, a recovery rule, an
   alloy-window lookup key) or delete it with `Alloy`.
