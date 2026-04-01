using System.Numerics;
using Raylib_cs;

namespace EvoVerse;

/// <summary>
/// Visual and behavioral definition for a cell type.
/// STEM is the only built-in. All others are defined in GEL files via TYPE declarations.
/// </summary>
public class CellTypeDefinition
{
    public string Name { get; set; } = "";
    public Color MainColor { get; set; } = new(180, 180, 180, 150);
    public Color NucleusColor { get; set; } = new(150, 150, 150, 150);
    public Color MembraneColor { get; set; } = new(200, 200, 200, 150);
    public float NucleusRadiusRatio { get; set; } = 0.2f;
    public float MembraneThickness { get; set; } = 3.0f;
}

/// <summary>
/// Registry of all known cell types. STEM is always registered.
/// Custom types are registered when a GEL file is parsed.
/// </summary>
public static class CellTypeRegistry
{
    public const string None = "NONE";
    public const string Stem = "STEM";

    private static readonly Dictionary<string, CellTypeDefinition> _types = new(StringComparer.OrdinalIgnoreCase);

    static CellTypeRegistry()
    {
        RegisterBuiltins();
    }

    private static void RegisterBuiltins()
    {
        Register(new CellTypeDefinition
        {
            Name = Stem,
            MainColor = new Color(210, 200, 180, 150),
            NucleusColor = new Color(200, 190, 170, 150),
            MembraneColor = new Color(220, 210, 190, 150),
            NucleusRadiusRatio = 0.3f,
            MembraneThickness = 5.0f,
        });
    }

    public static void Register(CellTypeDefinition def)
    {
        _types[def.Name.ToUpperInvariant()] = def;
    }

    public static CellTypeDefinition? Get(string name)
    {
        return _types.GetValueOrDefault(name.ToUpperInvariant());
    }

    public static bool Exists(string name) => _types.ContainsKey(name.ToUpperInvariant());

    public static IEnumerable<string> AllTypeNames => _types.Keys;

    /// <summary>
    /// Clear all types and re-register builtins. Call before loading a new GEL file.
    /// </summary>
    public static void Reset()
    {
        _types.Clear();
        RegisterBuiltins();
    }
}

/// <summary>
/// A cell in the simulation. Type is a string identifier resolved through CellTypeRegistry.
/// </summary>
public class Cell
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public int Clock { get; set; } = 0;
    public List<(string marker, int time)> Timers { get; set; } = [];
    public Hex Position { get; protected set; }
    public string Type { get; private set; }
    public Genome Genome { get; protected set; } = [];
    public bool IsDead { get; private set; } = false;
    public bool ShouldDivide { get; set; } = true;

    // Rendering
    protected float CreationTime { get; private set; }
    private const int VertexCount = 16;
    private const int VertexCountPerformance = 5;
    private const float AttachmentStrength = 0.11f;
    private const float PulsationAmount = 0.03f;
    private const float PulsationSpeed = 0.7f;
    private const float AnimationSpeedFactor = 0.5f;
    private const float AnimationAmount = 0.01f;
    private const float OffsetMagnitudeRange = 0.25f;

    private readonly float _offsetMagnitude;
    private readonly float _offsetAngle;
    private readonly float _animationPhase;

    public Cell(string type, Hex position, Genome? genome = null)
    {
        Type = type.ToUpperInvariant();
        Position = position;
        CreationTime = (float)Raylib.GetTime();
        Genome = genome ?? [];

        int positionHash = Position.GetHashCode();
        _offsetAngle = (float)(positionHash % 628) / 100.0f;
        _offsetMagnitude = (float)Random.Shared.NextDouble() * OffsetMagnitudeRange;
        _animationPhase = (float)Random.Shared.NextDouble() * 628;
    }

    public void Die(float probability = 1)
    {
        if (Random.Shared.NextSingle() < probability)
            IsDead = true;
    }

    internal void SetPosition(Hex newPosition) => Position = newPosition;

    /// <summary>
    /// Change this cell's type (specialization). Irreversible in biology, but the engine allows it.
    /// </summary>
    internal void Specialize(string newType)
    {
        Type = newType.ToUpperInvariant();
    }

    // --- Update Logic ---

    public virtual List<(string, Hex, int)> Update(WorldGrid grid)
    {
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

    public List<(string, Hex, int)> EvaluateGenome(WorldGrid grid)
    {
        List<(string, Hex, int)> emittedMarkers = [];
        foreach (var gene in Genome)
        {
            var activeConditions = gene.Evaluate(grid, this);
            foreach (var activeCondition in activeConditions)
            {
                switch (gene.Function)
                {
                    case GeneFunction.Morphology:
                        emittedMarkers.Add((gene.OutputMarker, Position, activeCondition.Range));
                        break;
                    case GeneFunction.StartTimer:
                        if (!Timers.Any(t => t.marker == gene.OutputMarker))
                            Timers.Add((gene.OutputMarker, activeCondition.Range));
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
                        // Specialization: only stem cells can differentiate
                        if (Type == CellTypeRegistry.Stem)
                        {
                            var targetType = gene.OutputMarker.ToUpperInvariant();
                            if (CellTypeRegistry.Exists(targetType))
                                Specialize(targetType);
                        }
                        break;
                    case GeneFunction.Movement:
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

    public virtual void TryDivide(WorldGrid grid)
    {
        if (Raylib.GetFPS() < CONFIG.MinFrameRate)
            return;
        var emptyNeighbors = GetEmptyNeighborHexes(grid);
        if (emptyNeighbors.Count > 0)
        {
            int randomIndex = Random.Shared.Next(emptyNeighbors.Count);
            Hex targetHex = emptyNeighbors[randomIndex];
            if (!grid.IsOccupied(targetHex))
            {
                var newCell = new Cell(Type, targetHex, [.. Genome]);
                newCell.ShouldDivide = false;
                Clock = 0;
                grid.AddCell(newCell);
            }
        }
    }

    public List<Hex> GetEmptyNeighborHexes(WorldGrid grid)
    {
        List<Hex> emptyNeighbors = [];
        for (int i = 0; i < 6; i++)
        {
            Hex neighborHex = Position.Neighbor(i);
            if (grid.IsWithinBounds(neighborHex) && !grid.IsOccupied(neighborHex))
                emptyNeighbors.Add(neighborHex);
        }
        return emptyNeighbors;
    }

    // --- Drawing ---

    private CellTypeDefinition? _cachedDef;
    private string? _cachedDefType;

    private CellTypeDefinition GetDefinition()
    {
        if (_cachedDefType != Type)
        {
            _cachedDef = CellTypeRegistry.Get(Type);
            _cachedDefType = Type;
        }
        return _cachedDef ?? new CellTypeDefinition { Name = Type };
    }

    public void Draw(HexLayout layout, float cellRadius, ICollection<Cell> neighbors)
    {
        var def = GetDefinition();
        (Vector2 center, Vector2[] vertices) = CalculateCellShape(layout, cellRadius, neighbors);
        DrawFilledCell(center, vertices, def.MainColor);
        DrawCellMembrane(vertices, def.MembraneColor, def.MembraneThickness);
        DrawCellInternals(center, cellRadius, def.NucleusColor, def.NucleusRadiusRatio);
    }

    private (Vector2 center, Vector2[] vertices) CalculateCellShape(HexLayout layout, float cellRadius, ICollection<Cell> neighbors)
    {
        Vector2 center = layout.HexToPixel(Position);
        int vertexCount = CONFIG.PerformanceMode ? VertexCountPerformance : VertexCount;
        Vector2[] vertices = new Vector2[vertexCount];

        List<(Cell cell, Vector2 direction, float distance)> neighborData = new();
        if (neighbors != null)
        {
            foreach (var neighbor in neighbors)
            {
                if (ReferenceEquals(this, neighbor)) continue;
                if (AreNeighbors(Position, neighbor.Position))
                {
                    Vector2 neighborPos = layout.HexToPixel(neighbor.Position);
                    Vector2 direction = neighborPos - center;
                    float distance = direction.Length();
                    if (distance > 0 && distance < cellRadius * 3)
                    {
                        direction /= distance;
                        neighborData.Add((neighbor, direction, distance * 0.9f));
                    }
                }
            }
        }

        for (int i = 0; i < vertexCount; i++)
        {
            float angle = i * MathF.PI * 2.0f / vertexCount;
            float vertexRadius = cellRadius;

            if (!CONFIG.PerformanceMode)
            {
                float time = (float)Raylib.GetTime() - CreationTime;
                float pulsation = 1.0f + MathF.Sin(time * PulsationSpeed + angle * 5) * PulsationAmount;
                vertexRadius *= pulsation;
            }

            Vector2 basePos = new(
                MathF.Cos(angle) * vertexRadius,
                MathF.Sin(angle) * vertexRadius
            );

            if (!CONFIG.PerformanceMode)
            {
                Vector2 finalPos = basePos;
                Vector2 vertexDir = Vector2.Normalize(basePos);
                foreach (var (_, neighborDir, distance) in neighborData)
                {
                    float alignment = Vector2.Dot(vertexDir, neighborDir);
                    if (alignment > 0.4f)
                    {
                        float pullStrength = alignment * AttachmentStrength;
                        float maxPull = distance * 0.4f;
                        float pull = maxPull * (2.0f / (1.0f + MathF.Exp(-pullStrength * 5)) - 1.0f);
                        finalPos += neighborDir * pull;
                    }
                }
                vertices[i] = center + finalPos;
            }
            else
            {
                vertices[i] = center + basePos;
            }
        }
        return (center, vertices);
    }

    private static void DrawFilledCell(Vector2 center, Vector2[] vertices, Color color)
    {
        var sortedVertices = vertices.OrderByDescending(v => Math.Atan2(v.Y - center.Y, v.X - center.X));
        Vector2[] points = new Vector2[sortedVertices.Count() + 2];
        points[0] = center;
        Array.Copy(sortedVertices.ToArray(), 0, points, 1, sortedVertices.Count());
        points[sortedVertices.Count() + 1] = sortedVertices.First();
        Raylib.DrawTriangleFan(points, points.Length, color);
    }

    private static void DrawCellMembrane(Vector2[] vertices, Color color, float thickness)
    {
        for (int i = 0; i < vertices.Length; i++)
            Raylib.DrawLineEx(vertices[i], vertices[(i + 1) % vertices.Length], thickness, color);
    }

    private void DrawCellInternals(Vector2 center, float cellRadius, Color nucleusColor, float radiusRatio)
    {
        float nucleusRadius = cellRadius * radiusRatio;
        float animationTime = (float)Raylib.GetTime() - CreationTime;
        float animationFactor = MathF.Sin(animationTime * AnimationSpeedFactor + _animationPhase) * AnimationAmount;
        Vector2 animatedOffset = new(
            MathF.Cos(_offsetAngle + animationTime * AnimationSpeedFactor) * _offsetMagnitude * (1.0f + animationFactor),
            MathF.Sin(_offsetAngle + animationTime * AnimationSpeedFactor) * _offsetMagnitude * (1.0f + animationFactor)
        );
        Vector2 nucleusCenter = new(
            center.X + (animatedOffset.X * cellRadius),
            center.Y + (animatedOffset.Y * cellRadius)
        );
        Raylib.DrawCircleV(nucleusCenter, nucleusRadius, nucleusColor);
    }

    private static bool AreNeighbors(Hex a, Hex b) => a.Distance(b) == 1;
}
