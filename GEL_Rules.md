# Gene Expression Language (GEL) Specification

## Overview

GEL (Gene Expression Language) defines how cells read morphogen gradients and environmental signals to decide their fate. Each line in a `.GEL` file is a **gene expression** that maps conditions to an output.

## Syntax

### Expression Format

```
OUTPUT => [conditions1] [conditions2]...
```

- **OUTPUT** — the marker to release or function to trigger when any condition set matches
- **=>** — separates the output from its conditions
- **[...]** — a condition set; the gene fires if **any** condition set evaluates to true (logical OR across sets, logical AND within a set)

### Comments

```
// This is a full-line comment
M0 => [M0 M1]  // This is an inline comment
```

Everything after `//` on a line is ignored by the parser.

### Variables / Aliases

```
@core = [is(STEM) M0]
@edge = [is(STEM) !M6 !M0 M2]

SKIN  => @edge
FLESH => @core [is(STEM) M7 n(6)]
```

- Define with `@name = value`
- Reference with `@name` — the text is substituted before parsing
- Variables must be defined before they are referenced
- Undefined `@name` references produce a parse error

---

## Type Definitions

`STEM` is the only built-in cell type. All other cell types must be declared with a `TYPE` block before they can be used as specialization targets.

```
TYPE FLESH:
  color: #DC503C96
  nucleus: #A0281E96, 0.14
  membrane: #F0785A96, 3.0

TYPE SKIN:
  color: #00640096
  nucleus: #00500096, 0.2
  membrane: #00780096, 4.0

TYPE BONE:
  color: #F0F0DCC8
  nucleus: #C8C8B4B4, 0.15
  membrane: #FFFFF0C8, 6.0
```

### Syntax

```
TYPE NAME:
  key: values
```

The header line is `TYPE NAME:` (colon required). Property lines must be indented (spaces or tabs). The block ends at the first non-indented, non-empty line.

### Properties

| Property | Format | Defaults | Description |
|---|---|---|---|
| **color** | `#RRGGBB[AA]` | a=150 | Main cell fill color |
| **nucleus** | `#RRGGBB[AA] [, radius_ratio]` | a=150, ratio=0.2 | Nucleus color and size (0.0-1.0 of cell radius) |
| **membrane** | `#RRGGBB[AA] [, thickness]` | a=150, thickness=3.0 | Membrane outline color and pixel thickness |

Colors use `#RRGGBB` or `#RRGGBBAA` hex format. Alpha defaults to 150 (semi-transparent) if omitted. Only `color` is required. Nucleus and membrane inherit sensible defaults if omitted.

Types must be defined before any gene expression that references them (as specialization target, `is()`, or `ns()`). `STEM` cannot be redefined.

---

## Markers

### Morphogen Markers
Any identifier that is not a reserved function marker is treated as a morphogen name.
- **M0–M9** — conventional primary signaling morphogens
- Custom names (e.g. `Mbase`, `Mchain`, `tmSkDiv`) are also valid

### Timer Markers
Any output marker starting with `tm` starts a countdown timer:
- **tmSkDiv**, **tm0**, **tm1**, ... — released when the countdown reaches zero

---

## Condition Types

All conditions within `[...]` must be true simultaneously (AND logic).
Multiple `[...]` blocks are OR-linked — the gene fires if any block passes.

### Marker Presence

```
[M1 M2]         // M1 and M2 must both be present (strength > 0)
```

### Marker Inhibition

```
[M1 !M0 !M6]    // M1 must be present; M0 and M6 must be absent (strength == 0)
```

The `!` prefix means the marker must **not** be present.

### Concentration Thresholds

```
[M0(>0.5)]      // M0 strength must be greater than 0.5
[M1(<=0.3)]     // M1 strength must be at most 0.3
[!M2(>0.8)]     // M2 strength must NOT be greater than 0.8 (i.e., must be <= 0.8)
```

Supported comparison operators: `>`, `<`, `>=`, `<=`, `=`, `!=`

Morphogen strength ranges from `0.0` (absent) to `1.0` (at source). The decay formula is:

```
strength = 1 - (distance / (range + 1))
```

So at range 5: strength at distance 1 = 0.83, distance 3 = 0.50, distance 5 = 0.17, distance 6 = 0.

### Cell Type Self-Check — `is()`

```
[is(STEM)]       // cell must be a stem cell
[is(FLESH)]      // cell must be a flesh cell
[is(SKIN)]       // cell must be a skin cell
[is(SPIKEBASE)]  // cell must be a spike-base cell
[is(SPIKE)]      // cell must be a spike cell
[!is(SKIN)]      // cell must NOT be a skin cell
```

Any type name defined via `TYPE` or the built-in `STEM` (case-insensitive).
Checks `cell.Type` directly, does not rely on morphogens.

### Neighbor Count — `n()`

```
[n(0)]    // cell has exactly 0 neighbors (isolated)
[n(6)]    // cell is fully surrounded
[n(>3)]   // cell has more than 3 neighbors
[n(<=2)]  // cell has at most 2 neighbors
[n(<6)]   // cell has fewer than 6 neighbors
```

Counts all occupied neighboring hexes regardless of cell type. Range: 0–6 (hexagonal grid).

### Typed Neighbor Count — `ns()`

```
[ns(SKIN)]           // has at least 1 skin neighbor (default: >=1)
[ns(SKIN>=2)]        // has 2 or more skin neighbors
[ns(FLESH<=1)]       // has at most 1 flesh neighbor
[ns(STEM=0)]         // has no stem neighbors
[ns(SPIKEBASE>=1)]   // adjacent to at least one spike-base cell
[ns(SPIKE=0)]        // no spike neighbors
```

Counts only neighbors matching the specified cell type. Any type defined via `TYPE` or the built-in `STEM`.
If no comparison is given, defaults to `>=1`.

### Clock / Age — `t()`

```
[t(>5)]    // cell's clock (ticks since last division) is greater than 5
[t(10)]    // clock is exactly 10
[t(>=3)]   // clock is at least 3
```

The clock is incremented each simulation step. **It resets to 0 every time the cell divides.** This means `t()` measures age-since-last-division, not total cell age.

---

## Range / Denominator Suffix

The number immediately after `]` has different meanings depending on the gene function:

| Gene type | Meaning of `N` in `[...]N` |
|---|---|
| Morphology | Propagation range of the emitted morphogen (in hex steps) |
| StartTimer (`tm...`) | Countdown duration in simulation ticks |
| Apoptosis (`APOP`) | Death probability denominator — `Die(1 / N)` per tick |

```
M6 => [is(STEM) M0]7       // emit M6 with range 7
tmSkDiv => [n(>0)]1        // timer fires after 1 tick
APOP => [is(SPIKE)]10      // 10% death chance per tick (1/10)
APOP => [is(SPIKE)]        // 100% death (no suffix = certain death)
```

---

## Gradient-Based Apoptosis Probability — `(Morphogen)`

For APOP only, a morphogen name in parentheses after `]` makes death probability a function of that morphogen's concentration at the cell's position:

```
APOP => [is(SPIKE)](Mbase)
```

Death probability = `1 - concentration`. So:
- Concentration 0.0 (outside range) → Die(1.0) = 100% death per tick
- Concentration 0.5 (mid-range) → Die(0.5) = 50% per tick
- Concentration 1.0 (at source) → Die(0.0) = immortal

This creates a smooth probabilistic boundary instead of a hard binary `!Morphogen` cutoff. The numeric suffix and morphogen suffix are mutually exclusive on the same condition set.

```
APOP => [is(SPIKE)](Mbase)         // gradient: P(death) = 1 - Mbase
APOP => [is(SPIKE)]10              // fixed: 10% per tick
APOP => [is(SPIKE) !Mbase]         // binary: 100% when Mbase absent
```

---

## Movement Targeting

```
MOVE => [is(STEM) M0 M1]>M3    // move towards M3 gradient
MOVE => [is(STEM) M7]<M8       // move away from M8 gradient
```

- `>` after `]` means move **towards** the target marker's gradient
- `<` after `]` means move **away from** the target marker's gradient
- Movement syntax is only valid when the output marker is `MOVE`

---

## Gene Functions

| Output Marker | Function | Description |
|---|---|---|
| Any non-reserved name | **Morphology** | Emits a morphogen at the given range |
| `tm...` | **StartTimer** | Starts a relative countdown; emits the marker when it reaches 0 |
| `NODIV` | **NoDivision** | Prevents the cell from dividing this tick |
| `APOP` | **Apoptosis** | Kills the cell (probability configurable via suffix or morphogen) |
| `SKIN` | **Specialization** | Differentiates Stem → Skin |
| `FLESH` | **Specialization** | Differentiates Stem → Flesh |
| `MOVE` | **Movement** | Moves cell along a morphogen gradient |

### Specialization Transitions

| From | To | Trigger |
|---|---|---|
| Stem | Skin | `SKIN => [conditions]` |
| Stem | Flesh | `FLESH => [conditions]` |

Differentiation is **irreversible**. Each type divides into cells of the same type.

---

## Timer Behavior

Timers are stored as **relative countdowns** (ticks remaining), not absolute clock values.

- Starting a timer with `tmFoo => [conditions]5` counts down 5 ticks from activation.
- Timer countdowns run independently of the cell's Clock — **division does not reset pending timers**.
- A timer marker fires once, then is removed. The gene can re-start it next tick if conditions still hold (but the duplicate-guard `!Timers.Any(...)` prevents it from being re-added while active).
- When a cell divides, **offspring do not inherit timers** (asymmetric — only the parent cell retains them).

---

## Clock vs Timer

| | `Clock` | `Timer` |
|---|---|---|
| What it counts | Ticks since last division | Ticks remaining on countdown |
| Resets on division | Yes (parent resets to 0) | No (continues running) |
| Used in conditions | `t(>N)` | Fires marker when done |
| Inherited by offspring | No (starts at 0) | No |

---

## Complete Example — Bounded Colony

```gel
// ── Morphogen propagation ──────────────────────────────────────────────────
M0 => [M0] [is(STEM) n(0)]          // seed trigger (self-sustaining)
M1 => [M1]                          // generic persistent marker
M2 => [M2] [tmSkDiv]                // propagated + timer-seeded
M6 => [is(STEM) M0]7                // colony boundary zone (range 7)
M7 => [is(STEM) M2 !M6]1 [is(STEM) !M0 M6 M7]1  // transition wave
M9 => [is(FLESH) n(<6)]1            // skin-repair signal from exposed flesh

// ── Timers ────────────────────────────────────────────────────────────────
tmSkDiv => [n(>0)]1                 // fires 1 tick after first neighbor appears

// ── Division control ──────────────────────────────────────────────────────
NODIV => [is(STEM) !M0 !M6]         // stem stops outside growth zone
         [is(SKIN) ns(SKIN>=2)]     // interior skin stops
         [is(FLESH) !M6]            // flesh stops outside zone

// ── Specialization ────────────────────────────────────────────────────────
SKIN  => [is(STEM) !M6 !M0 M2]
FLESH => [is(STEM) !M0 M6 M7 n(6)]

// ── Apoptosis ─────────────────────────────────────────────────────────────
APOP => [is(SKIN) ns(FLESH=0) !M6]  // outer skin outside zone
```

---

## Future Features (Roadmap)

- **Decay control** — set per-morphogen decay rates
- **Emission strength** — control how strongly a morphogen is emitted per tick
- **Logical OR within a condition set** — `[M0 (M1|M2)]` for alternative markers
- **Multi-stage differentiation** — intermediate cell types beyond current 3
