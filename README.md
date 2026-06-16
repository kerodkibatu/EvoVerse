# EvoVerse

A hex-grid cellular development sandbox. Cells sit on a hexagonal grid, emit and
read chemical signals (morphogens), and decide what to do (divide, specialize,
move, or die) based on a small genetic program you write yourself.

The program is written in **GEL** (Gene Expression Language), a tiny DSL I built
for this project. A genome is a text file of rules; the simulation runs them
every tick and you watch a body plan emerge from a single stem cell.

The default genome (`TEST.GEL`) grows a planaria-like organism: a stem core that
lays down a morphogen boundary, a differentiation wave that turns the interior
into flesh, a skin layer on the outside, and apoptosis that trims the edges.

![A run of TEST.GEL: a single stem cell grows into a bounded organism with a flesh interior and a skin layer](docs/rollout.gif)

*One rollout of `TEST.GEL`: a single stem cell (top) divides, lays down
morphogen gradients, and differentiates into flesh (red) bounded by skin
(green), with apoptosis trimming the edges.*

## How it works

Each cell holds the same genome (like real cells sharing one DNA). What makes
them differentiate is **context**: the morphogen concentrations and neighbors
around them. A morphogen is just a named signal that spreads from wherever it's
emitted and weakens with distance:

```
strength = 1 - (distance / (range + 1))
```

A gene maps conditions to an output. It fires when its conditions hold, and the
output is either a morphogen to emit or a built-in action (divide control,
specialize, move, die):

```
OUTPUT => [conditions] [conditions] ...
```

Conditions inside one `[...]` are AND-ed; multiple `[...]` blocks are OR-ed. So
this gene emits `M6` (range 7) from any stem cell that currently sees `M0`:

```
M6 => [is(STEM) M0]7
```

Conditions can check morphogen presence (`M1`), absence (`!M0`), concentration
(`M0(>0.5)`), cell type (`is(STEM)`), neighbor count (`n(6)`), typed neighbors
(`ns(SKIN>=2)`), and age since last division (`t(>5)`). The full grammar
(type definitions, timers, gradient-based apoptosis, movement targeting) is in
[`GEL_Rules.md`](GEL_Rules.md).

![Close-up of the first ~30 ticks: the seed cell dividing into a stem mass](docs/early-growth.gif)

*The first few dozen ticks, up close: the seed divides into a stem mass before
the boundary and differentiation genes kick in.*

## Running it

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download). Rendering is
[raylib](https://www.raylib.com/) via raylib-cs, and the UI is Dear ImGui via
RlImGui. Both restore from NuGet automatically.

```sh
git clone https://github.com/kerodkibatu/EvoVerse.git
cd EvoVerse
dotnet run
```

A window opens with the grid and a few panels (simulation controls, a cell
inspector, a morphogen sampler, and a live cell-count plot).

### Controls

| Input | Action |
|---|---|
| `Space` | Play / pause |
| `R` | Reset: reloads `TEST.GEL` and reseeds a single stem cell |
| Middle-drag | Pan the camera |
| Scroll | Zoom |
| `Alt` + hover | Inspect the hex under the cursor |
| Left-click | Use the active tool (place cell / paint morphogen / inspect) |

To run a different organism, edit `TEST.GEL` (or point it at your own rules) and
press `R`.

## Project layout

| File | What it does |
|---|---|
| `GEL.cs` | The GEL parser and gene/genome model |
| `Cells.cs` | Cell state and per-tick gene evaluation |
| `WorldGrid.cs` | The hex world, division, and the update loop |
| `Morphogen.cs` | Morphogen field, diffusion, and caching |
| `Hex.cs` | Hex grid math (axial/cube coords, pixel mapping) |
| `Program.cs` | raylib + ImGui app, rendering, tools, input |
| `Tools/` | Place-cell / morphogen-paint / inspect tools |
| `GEL_Rules.md` | Full GEL language specification |
| `EvoVerse.Tests/` | xUnit tests for the parser, hex math, and morphogen field |

```sh
dotnet test   # run the test suite
```

## Roadmap

- Per-morphogen decay rates and emission strength
- `OR` within a single condition set: `[M0 (M1|M2)]`
- Intermediate cell types for multi-stage differentiation
- Loading/saving genomes from the UI instead of editing `TEST.GEL`

## License

MIT. See [LICENSE](LICENSE).

The default genome recreates a model from third-party educational material; see
[`Reference/README.md`](Reference/README.md) for attribution notes.
