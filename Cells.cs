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

    // Common properties
    // Position now has a protected set, accessible internally for movement
    public Hex Position { get; protected set; }
    public abstract CellType Type { get; }
    public Genome Genome { get; set; }

    // Time-related properties for animations and lifecycle
    protected float CreationTime { get; private set; }
    protected float LastDivisionTime { get; private set; }

    // Cell division properties
    protected virtual float DivisionCooldown => 3.0f;
    protected const float DivisionMaturityThreshold = 1.0f;
    // Increased randomness slightly to stagger division/movement
    protected const float DivisionRandomness = 0.1f; // Example: 10% chance to skip division even if ready

    // Cell rendering properties
    protected const int VertexCount = 12;
    protected const float AttachmentStrength = 0.11f;
    protected const float PulsationAmount = 0.03f;
    protected const float PulsationSpeed = 0.7f;

    // Constructor
    protected Cell(Hex position)
    {
        Position = position;
        CreationTime = (float)Raylib.GetTime();
        LastDivisionTime = CreationTime - (Random.Shared.NextSingle() * DivisionCooldown); // Stagger initial division
        Genome = CreateDefaultGenome();
    }

    protected virtual Genome CreateDefaultGenome()
    {
        var genome = new Genome();
        
        // Add genes for basic cell behavior
        var divisionGene = new Gene("Division", 0.5f);
        divisionGene.MorphogenSensitivity[MorphogenType.Activator] = 0.2f;
        divisionGene.MorphogenSensitivity[MorphogenType.Inhibitor] = -0.3f;
        genome.AddGene(divisionGene);

        var differentiationGene = new Gene("Differentiation", 0.0f);
        differentiationGene.MorphogenSensitivity[MorphogenType.Differentiation] = 0.5f;
        genome.AddGene(differentiationGene);

        var adhesionGene = new Gene("Adhesion", 0.5f);
        adhesionGene.MorphogenSensitivity[MorphogenType.Adhesion] = 0.3f;
        genome.AddGene(adhesionGene);

        return genome;
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
        // ... (CalculateCellShape implementation remains the same) ...
        // Get the center position in screen coordinates
        Vector2 center = layout.HexToPixel(Position);

        // Calculate vertices for a basic cell shape (circle-like polygon)
        Vector2[] vertices = new Vector2[VertexCount];

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
                        neighborData.Add((neighbor, direction, distance*0.9f));
                        connections[neighbor] = new List<int>();
                    }
                }
            }
        }

        // Time-based animation for subtle membrane movement
        float time = (float)Raylib.GetTime() - CreationTime;

        // Generate the cell shape vertices
        for (int i = 0; i < VertexCount; i++)
        {
            // Base angle for this vertex
            float angle = i * MathF.PI * 2.0f / VertexCount;

            // Calculate the base radius with a slight pulsation
            float pulsation = 1.0f + MathF.Sin(time * PulsationSpeed + angle * 5) * PulsationAmount;
            float vertexRadius = cellRadius * pulsation;

            // Calculate basic vertex position
            Vector2 basePos = new(
                MathF.Cos(angle) * vertexRadius,
                MathF.Sin(angle) * vertexRadius
            );

            // Adjust vertex position based on neighbors
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
    public virtual Cell? Update(WorldGrid grid)
    {
        // Update gene expression based on local morphogen levels
        var localMorphogenLevels = grid.GetLocalMorphogenLevels(Position);
        Genome.UpdateExpression(localMorphogenLevels);

        // Produce morphogens based on gene expression
        foreach (var gene in Genome.Genes.Values)
        {
            foreach (var (morphogen, production) in gene.MorphogenProduction)
            {
                if (production > 0)
                {
                    grid.AddMorphogen(morphogen, Position, production * gene.ExpressionLevel);
                }
            }
        }

        // Attempt movement based on adhesion gene
        if (Genome.Genes.TryGetValue("Adhesion", out var adhesionGene))
        {
            if (adhesionGene.ExpressionLevel < 0.3f)
            {
                TryMoveRandomly(grid);
            }
        }

        // Check for division based on division gene
        if (Genome.Genes.TryGetValue("Division", out var divisionGene))
        {
            if (divisionGene.ExpressionLevel > 0.7f)
            {
                return CheckForDivision(grid);
            }
        }

        return null;
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
    protected virtual Cell? CheckForDivision(WorldGrid grid)
    {
        float currentTime = (float)Raylib.GetTime();
        float age = currentTime - CreationTime;
        float timeSinceLastDivision = currentTime - LastDivisionTime;

        // Check cooldowns and maturity
        if (age < DivisionMaturityThreshold || timeSinceLastDivision < DivisionCooldown)
        {
            return null; // Not ready
        }

        // Apply randomness factor
        if (Random.Shared.NextSingle() < DivisionRandomness) // Use <= if you want 0 randomness to mean never skip
        {
             return null; // Skip division this tick based on chance
        }

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

                 // Reset division timer for the parent cell
                 LastDivisionTime = currentTime;

                 return newCell; // Return the newly created cell
            }
        }

        return null; // No suitable empty neighbor found or target became occupied
    }

    /// <summary>
    /// Gets a list of adjacent hexes that are within bounds and not occupied.
    /// </summary>
    protected List<Hex> GetEmptyNeighborHexes(WorldGrid grid)
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
    public static Cell CreateCell(CellType type, Hex position)
    {
        // ... (implementation remains the same) ...
        return type switch
        {
            CellType.Stem => new StemCell(position),
            CellType.Flesh => new FleshCell(position),
            CellType.Skin => new SkinCell(position),
            _ => throw new ArgumentException($"Unknown cell type: {type}")
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

    protected BasicCell(Hex position) : base(position)
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
        var offspring = Cell.CreateCell(this.Type, targetPosition);
        offspring.Genome = Genome.Clone();
        offspring.Genome.Mutate();
        return offspring;
    }
}

public class StemCell(Hex position) : BasicCell(position)
{
    // Color constants
    protected override Color MainColor => new(50, 200, 180, 230); // Vibrant teal
    protected override Color NucleusColor => new(30, 140, 120, 240); // Deep teal
    protected override Color MembraneColor => new(80, 220, 200, 220); // Bright cyan
    protected override float NucleusRadiusRatio => 0.3f;
    protected override float MembraneThickness => 5.0f;
    
    // Stem cells divide faster than other cells
    protected override float DivisionCooldown => 2.0f; // Quicker division

    public override CellType Type => CellType.Stem;
}

public class FleshCell(Hex position) : BasicCell(position)
{
    // Color constants
    protected override Color MainColor => new(220, 80, 60, 230); // Warm coral
    protected override Color NucleusColor => new(160, 40, 30, 240); // Deep red
    protected override Color MembraneColor => new(240, 120, 90, 220); // Soft orange
    protected override float NucleusRadiusRatio => 0.14f;
    protected override float MembraneThickness => 3.0f;
    
    // Flesh cells divide at medium speed
    protected override float DivisionCooldown => 4.0f;

    public override CellType Type => CellType.Flesh;
}

public class SkinCell(Hex position) : BasicCell(position)
{
    // Color constants
    protected override Color MainColor => new(240, 220, 180, 230); // Soft beige
    protected override Color NucleusColor => new(180, 160, 120, 240); // Deep taupe
    protected override Color MembraneColor => new(250, 230, 200, 220); // Light tan
    protected override float NucleusRadiusRatio => 0.2f;
    protected override float MembraneThickness => 4.0f;
    
    // Skin cells divide slower than other cells
    protected override float DivisionCooldown => 6.0f;

    public override CellType Type => CellType.Skin;
}