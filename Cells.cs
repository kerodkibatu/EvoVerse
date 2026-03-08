using System.Numerics;
using Raylib_cs;
using System.Linq;
using System.Security.Cryptography;

namespace EvoVerse;

/// <summary>
/// The possible types of cells in the simulation.
/// </summary>
public enum CellType
{
    None,
    Stem,
    Flesh,
    Skin
}

/// <summary>
/// Base abstract class for all cells in the simulation.
/// </summary>
public abstract class Cell
{
    // GUID
    public Guid Id { get; private set; } = Guid.NewGuid();

    // Clock(basically the age of the cell)
    public int Clock { get; set; } = 0;

    // Timer When a timer is active, it will emit a marker at the end of the timer
    // There are multiple timers, so we need a tuple of (marker, time) and
    // when the timer is up, it will be removed from the list and emit the marker
    public List<(string marker, int time)> Timers { get; set; } = [];
    // Common properties
    // Position now has a protected set, accessible internally for movement
    public Hex Position { get; protected set; }
    public abstract CellType Type { get; }

    // Genome property
    public Genome Genome { get; protected set; } = [];

    // Time-related properties for animations and lifecycle
    protected float CreationTime { get; private set; }

    // Cell rendering properties
    protected const int VertexCount = 16;
    protected const int VertexCountPerformance = 5;
    protected const float AttachmentStrength = 0.11f;
    protected const float PulsationAmount = 0.03f;
    protected const float PulsationSpeed = 0.7f;

    // Death properties
    public bool IsDead { get; private set; } = false;

    // Constructor
    protected Cell(Hex position, Genome? genome = null)
    {
        Position = position;
        CreationTime = (float)Raylib.GetTime();
        Genome = genome ?? [];
    }

    // Chance to die
    public void Die(float probability = 1)
    {
        if (Random.Shared.NextSingle() < probability)
        {
            IsDead = true;
        }
    }

    // --- Drawing methods remain the same ---
    public void Draw(HexLayout layout, float cellRadius, ICollection<Cell> neighbors)
    {
        (Vector2 center, Vector2[] vertices) =
            CalculateCellShape(layout, cellRadius, neighbors);
        DrawFilledCell(center, vertices);
        DrawCellMembrane(vertices);
        DrawCellInternals(center, cellRadius);
    }
    protected (Vector2 center, Vector2[] vertices) CalculateCellShape(HexLayout layout, float cellRadius, ICollection<Cell> neighbors)
    {
        // Get the center position in screen coordinates
        Vector2 center = layout.HexToPixel(Position);

        // Use lower vertex count if performance mode is enabled
        int vertexCount = CONFIG.PerformanceMode ? VertexCountPerformance : VertexCount;
        Vector2[] vertices = new Vector2[vertexCount];

        // Keep track of which neighbor affects which vertices
        Dictionary<Cell, List<int>> connections = new();

        // Find neighboring cells
        List<(Cell cell, Vector2 direction, float distance)> neighborData = new();
        if (neighbors != null)
        {
            foreach (var neighbor in neighbors)
            {
                // Don't connect to self
                if (ReferenceEquals(this, neighbor))
                    continue;

                if (AreNeighbors(Position, neighbor.Position))
                {
                    // Calculate direction to neighbor (normalized)
                    Vector2 neighborPos = layout.HexToPixel(neighbor.Position);
                    Vector2 direction = neighborPos - center;
                    float distance = direction.Length();

                    // Only consider true neighbors (not overlapping or too far cells)
                    if (distance > 0 && distance < cellRadius * 3)
                    {
                        direction /= distance; // Normalize
                        neighborData.Add((neighbor, direction, distance * 0.9f));
                        connections[neighbor] = new List<int>();
                    }
                }
            }
        }

        // Generate the cell shape vertices
        for (int i = 0; i < vertexCount; i++)
        {
            // Base angle for this vertex
            float angle = i * MathF.PI * 2.0f / vertexCount;

            // If not in performance mode, calculate the base radius with a slight pulsation
            float vertexRadius = cellRadius;
            if (!CONFIG.PerformanceMode)
            {
                // Time-based animation for subtle membrane movement
                float time = (float)Raylib.GetTime() - CreationTime;
                float pulsation = 1.0f + MathF.Sin(time * PulsationSpeed + angle * 5) * PulsationAmount;
                vertexRadius *= pulsation;
            }

            // Calculate basic vertex position
            Vector2 basePos = new(
                MathF.Cos(angle) * vertexRadius,
                MathF.Sin(angle) * vertexRadius
            );

            // Adjust vertex position based on neighbors if not in performance mode
            if (!CONFIG.PerformanceMode)
            {
                Vector2 finalPos = basePos;
                Vector2 vertexDir = Vector2.Normalize(basePos); // Direction from center to vertex

                foreach (var (neighbor, neighborDir, distance) in neighborData)
                {
                    // How much this vertex is facing toward the neighbor
                    float alignment = Vector2.Dot(vertexDir, neighborDir);

                    // Only pull vertices that face the neighbor somewhat
                    if (alignment > 0.4f) // Increased threshold for more targeted attachment
                    {
                        // Pull stronger if vertex direction aligns well with neighbor direction
                        float pullStrength = alignment * AttachmentStrength;

                        // Pull the vertex toward the neighbor, but careful of overlapping too much
                        // Use a sigmoid function to control how far the vertex can extend
                        float maxPull = distance * 0.4f; // Maximum pull is 40% of the distance
                        float pull = maxPull * (2.0f / (1.0f + MathF.Exp(-pullStrength * 5)) - 1.0f);

                        // Apply the pull
                        finalPos += neighborDir * pull;
                    }
                }

                // Set final vertex position
                vertices[i] = center + finalPos;
            }
            else
            {
                // Set final vertex position without adjustment for performance mode
                vertices[i] = center + basePos;
            }
        }

        return (center, vertices);
    }
    protected abstract void DrawFilledCell(Vector2 center, Vector2[] vertices);
    protected abstract void DrawCellMembrane(Vector2[] vertices);
    protected abstract void DrawCellInternals(Vector2 center, float cellRadius);
    protected static bool AreNeighbors(Hex a, Hex b)
    {
        return a.Distance(b) == 1; // Simplified check using Hex.Distance
    }


    // --- Movement and Update Logic ---

    /// <summary>
    /// Method called by WorldGrid to update the cell's internal position state.
    /// Should only be called by trusted grid logic (like WorldGrid.MoveCell).
    /// </summary>
    internal void SetPosition(Hex newPosition)
    {
        Position = newPosition;
    }

    /// <summary>
    /// Update method called once per tick for each cell. Handles movement attempt and division check.
    /// </summary>
    /// <param name="grid">The world grid for interaction.</param>
    /// <returns>A new Cell if division occurs, otherwise null.</returns>
    public virtual List<(string,Hex,int)> Update(WorldGrid grid)
    {
        // Check if we're still valid in the grid
        if (!CellExists(grid))
            return [];
        ShouldDivide = Clock != 0;
        var emittedMarkers = EvaluateGenome(grid);
        Clock++;
        for (int i = Timers.Count - 1; i >= 0; i--)
        {
            var (marker, time) = Timers[i];
            int remaining = time - 1;
            if (remaining <= 0)
            {
                emittedMarkers.Add((marker, Position, 0));
                Timers.RemoveAt(i);
            }
            else
            {
                Timers[i] = (marker, remaining);
            }
        }
        return emittedMarkers;
    }
    public bool ShouldDivide { get; private set; } = true;
    public List<(string,Hex,int)> EvaluateGenome(WorldGrid grid)
    {
        List<(string,Hex,int)> emittedMarkers = [];
        foreach (var gene in Genome)
        {
            var activeConditions = gene.Evaluate(grid, this);
            foreach (var activeCondition in activeConditions)
            {
                // Apply the condition
                switch (gene.Function)
                {
                    case GeneFunction.Morphology:
                        // Emit the markers
                        emittedMarkers.Add((gene.OutputMarker, Position, activeCondition.Range));
                        break;
                    case GeneFunction.StartTimer:
                        if (!Timers.Any(t => t.marker == gene.OutputMarker))
                        {
                            Timers.Add((gene.OutputMarker, activeCondition.Range));
                        }
                        break;
                    case GeneFunction.Apoptosis:
                        float prob;
                        if (activeCondition.ProbabilityMorphogen != null)
                            prob = 1.0f - grid.GetMorphogenStrength(Position, activeCondition.ProbabilityMorphogen);
                        else
                            prob = activeCondition.Range > 0 ? 1.0f / activeCondition.Range : 1.0f;
                        Die(prob);
                        break;
                    case GeneFunction.NoDivision:
                        ShouldDivide = false;
                        break;
                    case GeneFunction.Specialization:
                        CellType? targetType = gene.OutputMarker switch
                        {
                            "SKIN" when Type == CellType.Stem => CellType.Skin,
                            "FLESH" when Type == CellType.Stem => CellType.Flesh,
                            _ => null
                        };
                        if (targetType.HasValue)
                            grid.PlaceCell(Position, targetType.Value, Genome);
                        break;
                    case GeneFunction.Movement:
                        // Direction is determined by the condition
                        var direction = MorphogenManager.GetGradientAtHex(
                            Position,
                            activeCondition.MovementTarget!,
                            activeCondition.Range,
                            !activeCondition.IsMovementAway);
                        grid.MoveCell(Position, Position + direction);
                        break;
                }
            }
        }
        return emittedMarkers;
    }

    public bool CellExists(WorldGrid grid)
    {
        Cell? currentCellInGrid = grid.GetCell(Position);

        return currentCellInGrid != null && currentCellInGrid.Id == Id;
    }

    /// <summary>
    /// Attempts to move the cell to a randomly chosen adjacent empty hex.
    /// </summary>
    protected virtual void TryMoveRandomly(WorldGrid grid)
    {
        // Find available empty neighbors efficiently
        var emptyNeighbors = GetEmptyNeighborHexes(grid);

        if (emptyNeighbors.Count > 0)
        {
            // Pick a random empty neighbor
            int randomIndex = Random.Shared.Next(emptyNeighbors.Count);
            Hex targetHex = emptyNeighbors[randomIndex];

            // Execute the move using the reliable grid method
            // This internally checks if the target is *still* empty and handles position update.
            grid.MoveCell(Position, targetHex);
        }
    }


    /// <summary>
    /// Checks conditions for division and returns a new cell if division occurs.
    /// </summary>
    public virtual void TryDivide(WorldGrid grid)
    {
        if (Raylib.GetFPS() < CONFIG.MinFrameRate)
            return;
        // Find empty neighbors at the *current* position
        var emptyNeighbors = GetEmptyNeighborHexes(grid);

        if (emptyNeighbors.Count > 0)
        {
            // Pick a random empty neighbor for the new cell
            int randomIndex = Random.Shared.Next(emptyNeighbors.Count);
            Hex targetHex = emptyNeighbors[randomIndex];

            // Check if the chosen target hex is *still* empty in the grid
            // (Another cell might have moved there *this tick* after our move but before this check)
            // This check is important but the primary check/resolution happens in UpdateCellDivision when adding.
            if (!grid.IsOccupied(targetHex))
            {
                // Create the offspring
                Cell newCell = CreateDivisionOffspring(targetHex);
                newCell.ShouldDivide = false;
                Clock = 0;
                grid.AddCell(newCell);
            }
        }
    }

    /// <summary>
    /// Gets a list of adjacent hexes that are within bounds and not occupied.
    /// </summary>
    public List<Hex> GetEmptyNeighborHexes(WorldGrid grid)
    {
        List<Hex> emptyNeighbors = [];
        for (int i = 0; i < 6; i++)
        {
            Hex neighborHex = Position.Neighbor(i);
            // Use WorldGrid.IsOccupied for a clear check
            if (grid.IsWithinBounds(neighborHex) && !grid.IsOccupied(neighborHex))
            {
                emptyNeighbors.Add(neighborHex);
            }
        }
        return emptyNeighbors;
    }
    // Abstract method for creating offspring remains the same
    protected abstract Cell CreateDivisionOffspring(Hex targetPosition);

    // Factory method remains the same
    public static Cell? CreateCell(CellType type, Hex position, Genome? genome = null)
    {
        return type switch
        {
            CellType.Stem => new StemCell(position, genome),
            CellType.Flesh => new FleshCell(position, genome),
            CellType.Skin => new SkinCell(position, genome),
            _ => null
        };
    }
}

public abstract class BasicCell : Cell
{
    // Shared nucleus and animation properties
    protected const float AnimationSpeedFactor = 0.5f; // Controls animation speed
    protected const float AnimationAmount = 0.01f; // Controls animation magnitude
    protected const float OffsetMagnitudeRange = 0.25f; // Offset by 25% of cell radius

    // Properties for animation and offset
    protected float offsetMagnitude;
    protected float offsetAngle;
    protected float animationPhase;

    // Abstract properties for specific cell types to define
    protected abstract float NucleusRadiusRatio { get; }
    protected abstract Color MainColor { get; }
    protected abstract Color NucleusColor { get; }
    protected abstract Color MembraneColor { get; }
    protected abstract float MembraneThickness { get; }

    protected BasicCell(Hex position, Genome? genome = null) : base(position, genome)
    {
        // Calculate stable, position-dependent offset
        int positionHash = Position.GetHashCode();
        offsetAngle = (float)(positionHash % 628) / 100.0f; // Random angle between 0 and 2π
        offsetMagnitude = (float)Random.Shared.NextDouble() * OffsetMagnitudeRange; // Random offset magnitude
        animationPhase = (float)Random.Shared.NextDouble() * 628; // Random animation phase
    }

    protected override void DrawFilledCell(Vector2 center, Vector2[] vertices)
    {
        // Sort vertices in counterclockwise order around the center
        var sortedVertices = vertices.OrderByDescending(v => Math.Atan2(v.Y - center.Y, v.X - center.X));

        // Create array of points for DrawTriangleFan
        Vector2[] points = new Vector2[sortedVertices.Count() + 2];
        points[0] = center;
        Array.Copy(sortedVertices.ToArray(), 0, points, 1, sortedVertices.Count());
        points[sortedVertices.Count() + 1] = sortedVertices.First();

        // Draw triangle fan
        Raylib.DrawTriangleFan(points, points.Length, MainColor);
    }

    protected override void DrawCellMembrane(Vector2[] vertices)
    {
        // Draw cell membrane as connected lines
        for (int i = 0; i < vertices.Length; i++)
        {
            Raylib.DrawLineEx(vertices[i], vertices[(i + 1) % vertices.Length], MembraneThickness, MembraneColor);
        }
    }

    protected override void DrawCellInternals(Vector2 center, float cellRadius)
    {
        // Calculate nucleus properties and position
        float nucleusRadius = cellRadius * NucleusRadiusRatio;
        float animationTime = (float)Raylib.GetTime() - CreationTime;
        float animationFactor = MathF.Sin(animationTime * AnimationSpeedFactor + animationPhase) * AnimationAmount;

        // Calculate nucleus offset
        Vector2 animatedOffset = new(
            MathF.Cos(offsetAngle + animationTime * AnimationSpeedFactor) * offsetMagnitude * (1.0f + animationFactor),
            MathF.Sin(offsetAngle + animationTime * AnimationSpeedFactor) * offsetMagnitude * (1.0f + animationFactor)
        );

        // Calculate nucleus position
        Vector2 nucleusCenter = new(
            center.X + (animatedOffset.X * cellRadius),
            center.Y + (animatedOffset.Y * cellRadius)
        );

        // Draw nucleus
        Raylib.DrawCircleV(nucleusCenter, nucleusRadius, NucleusColor);
    }

    // Create a new cell of the same type
    protected override Cell CreateDivisionOffspring(Hex targetPosition)
    {
        // Use the factory method to create a new cell of the same type with the same genome
        return Cell.CreateCell(Type, targetPosition, [.. Genome])!;
    }
}

public class StemCell : BasicCell
{
    // Color constants
    protected override Color MainColor => new(210, 200, 180, 150); // Washed out beige
    protected override Color NucleusColor => new(200, 190, 170, 150); // Light beige
    protected override Color MembraneColor => new(220, 210, 190, 150); // Soft beige with transparency
    protected override float NucleusRadiusRatio => 0.3f;
    protected override float MembraneThickness => 5.0f;

    public override CellType Type => CellType.Stem;
    public StemCell(Hex position, Genome? genome = null) : base(position, genome) { }
}

public class FleshCell : BasicCell
{
    // Color constants
    protected override Color MainColor => new(220, 80, 60, 150); // Warm coral
    protected override Color NucleusColor => new(160, 40, 30, 150); // Deep red
    protected override Color MembraneColor => new(240, 120, 90, 150); // Soft orange
    protected override float NucleusRadiusRatio => 0.14f;
    protected override float MembraneThickness => 3.0f;
    public FleshCell(Hex position, Genome? genome = null) : base(position, genome) { }
    public override CellType Type => CellType.Flesh;
}

public class SkinCell : BasicCell
{
    // Color constants
    protected override Color MainColor => new(0, 100, 0, 150); // Darker green
    protected override Color NucleusColor => new(0, 80, 0, 150); // Darker green variant
    protected override Color MembraneColor => new(0, 120, 0, 150); // Darker green with lighter shade
    protected override float NucleusRadiusRatio => 0.2f;
    protected override float MembraneThickness => 4.0f;

    public override CellType Type => CellType.Skin;
    public SkinCell(Hex position, Genome? genome = null) : base(position, genome) { }
}
