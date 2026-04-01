// Genetic Expression Language
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace EvoVerse;

public enum GeneFunction
{
    Morphology,
    StartTimer,
    Specialization,
    NoDivision,
    Apoptosis,
    Movement
}
public enum ComparisonType
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual
}

public struct MarkerCondition
{
    public string MarkerName { get; set; }
    public ComparisonType? Comparison { get; set; }
    public float Threshold { get; set; }

    public MarkerCondition(string markerName)
    {
        MarkerName = markerName;
        Comparison = null;
        Threshold = 0;
    }

    public MarkerCondition(string markerName, ComparisonType comparison, float threshold)
    {
        MarkerName = markerName;
        Comparison = comparison;
        Threshold = threshold;
    }

    public override readonly string ToString()
    {
        if (Comparison == null)
            return MarkerName;
        var compStr = Comparison.Value switch
        {
            ComparisonType.Equals => "=",
            ComparisonType.NotEquals => "!=",
            ComparisonType.GreaterThan => ">",
            ComparisonType.LessThan => "<",
            ComparisonType.GreaterThanOrEqual => ">=",
            ComparisonType.LessThanOrEqual => "<=",
            _ => throw new ArgumentException("Invalid comparison type")
        };
        return $"{MarkerName}({compStr}{Threshold.ToString(CultureInfo.InvariantCulture)})";
    }
}

public class Gene
{
    public List<ConditionSet> ConditionSets { get; set; } = [];
    public GeneFunction Function { get; set; } = GeneFunction.Morphology;
    public string OutputMarker { get; set; } = "NODIV";

    public Gene() { }

    public Gene(string outputMarker)
    {
        OutputMarker = outputMarker;
    }

    public IEnumerable<ConditionSet> Evaluate(WorldGrid grid, Cell cell)
    {
        foreach (var conditionSet in ConditionSets)
        {
            if (conditionSet.Evaluate(grid, cell))
                yield return conditionSet;
        }
    }

    public override string ToString()
    {
        var conditionSetsStr = string.Join(" ", ConditionSets.Select(cs => cs.ToString()));
        return $"{OutputMarker} => {conditionSetsStr}";
    }
}

public class ConditionSet
{
    public List<MarkerCondition> ActiveMarkers { get; set; } = [];
    public List<MarkerCondition> InhibitedMarkers { get; set; } = [];
    public Dictionary<int, ComparisonType> NeighborConditions { get; set; } = [];
    public Dictionary<int, ComparisonType> ClockConditions { get; set; } = [];
    public int Range { get; set; } = 0;

    // Self-type conditions (string-based, case-insensitive)
    public List<string> SelfTypeChecks { get; set; } = [];
    public List<string> InhibitedSelfTypes { get; set; } = [];

    // Typed neighbor count conditions
    public List<(string CellType, int Count, ComparisonType Comparison)> TypedNeighborConditions { get; set; } = [];

    public string? MovementTarget { get; set; }
    public bool IsMovementAway { get; set; } = false;
    public string? ProbabilityMorphogen { get; set; }

    public void AddMarker(MarkerCondition marker)
    {
        if (!ActiveMarkers.Any(m => m.MarkerName == marker.MarkerName))
            ActiveMarkers.Add(marker);
    }

    public void AddInhibitor(MarkerCondition marker)
    {
        if (!InhibitedMarkers.Any(m => m.MarkerName == marker.MarkerName))
            InhibitedMarkers.Add(marker);
    }

    public void AddNeighborCondition(int count, ComparisonType comparison = ComparisonType.Equals)
    {
        NeighborConditions[count] = comparison;
    }

    public void AddClockCondition(int value, ComparisonType comparison = ComparisonType.Equals)
    {
        ClockConditions[value] = comparison;
    }

    private static bool CompareValues(float a, float b, ComparisonType comparison)
    {
        const float epsilon = 0.0001f;
        return comparison switch
        {
            ComparisonType.Equals => MathF.Abs(a - b) < epsilon,
            ComparisonType.NotEquals => MathF.Abs(a - b) >= epsilon,
            ComparisonType.GreaterThan => a > b,
            ComparisonType.LessThan => a < b,
            ComparisonType.GreaterThanOrEqual => a > b || MathF.Abs(a - b) < epsilon,
            ComparisonType.LessThanOrEqual => a < b || MathF.Abs(a - b) < epsilon,
            _ => false
        };
    }

    public bool Evaluate(WorldGrid grid, Cell cell)
    {
        // Check self-type conditions (string comparison, case-insensitive)
        foreach (var t in SelfTypeChecks)
            if (!string.Equals(cell.Type, t, StringComparison.OrdinalIgnoreCase)) return false;
        foreach (var t in InhibitedSelfTypes)
            if (string.Equals(cell.Type, t, StringComparison.OrdinalIgnoreCase)) return false;

        // Batch morphogen lookups
        Dictionary<string, float>? morphogens = null;
        if (ActiveMarkers.Count > 0 || InhibitedMarkers.Count > 0)
            morphogens = grid.GetMorphogensAtHex(cell.Position);
        float GetStrength(string name) => morphogens?.TryGetValue(name, out var s) == true ? s : 0f;

        foreach (var mc in ActiveMarkers)
        {
            var strength = GetStrength(mc.MarkerName);
            if (mc.Comparison != null)
            {
                if (!CompareValues(strength, mc.Threshold, mc.Comparison.Value))
                    return false;
            }
            else
            {
                if (strength == 0) return false;
            }
        }
        foreach (var mc in InhibitedMarkers)
        {
            var strength = GetStrength(mc.MarkerName);
            if (mc.Comparison != null)
            {
                if (CompareValues(strength, mc.Threshold, mc.Comparison.Value))
                    return false;
            }
            else
            {
                if (strength > 0) return false;
            }
        }

        // Clock conditions
        foreach (var condition in ClockConditions)
        {
            var cellClock = cell.Clock;
            var threshold = condition.Key;
            bool met = condition.Value switch
            {
                ComparisonType.Equals => cellClock == threshold,
                ComparisonType.NotEquals => cellClock != threshold,
                ComparisonType.GreaterThan => cellClock > threshold,
                ComparisonType.LessThan => cellClock < threshold,
                ComparisonType.GreaterThanOrEqual => cellClock >= threshold,
                ComparisonType.LessThanOrEqual => cellClock <= threshold,
                _ => false
            };
            if (!met) return false;
        }

        // Typed neighbor count conditions (string-based)
        foreach (var (cellType, count, comparison) in TypedNeighborConditions)
        {
            int typedCount = 0;
            for (int i = 0; i < 6; i++)
            {
                var neighborCell = grid.GetCell(cell.Position.Neighbor(i));
                if (neighborCell != null && string.Equals(neighborCell.Type, cellType, StringComparison.OrdinalIgnoreCase))
                    typedCount++;
            }
            bool met = comparison switch
            {
                ComparisonType.Equals => typedCount == count,
                ComparisonType.NotEquals => typedCount != count,
                ComparisonType.GreaterThan => typedCount > count,
                ComparisonType.LessThan => typedCount < count,
                ComparisonType.GreaterThanOrEqual => typedCount >= count,
                ComparisonType.LessThanOrEqual => typedCount <= count,
                _ => false
            };
            if (!met) return false;
        }

        // Neighbor count conditions
        int occupiedNeighborCount = NeighborConditions.Count > 0
            ? 6 - cell.GetEmptyNeighborHexes(grid).Count
            : 0;

        foreach (var condition in NeighborConditions)
        {
            var A = occupiedNeighborCount;
            var B = condition.Key;
            bool met = condition.Value switch
            {
                ComparisonType.Equals => A == B,
                ComparisonType.NotEquals => A != B,
                ComparisonType.GreaterThan => A > B,
                ComparisonType.LessThan => A < B,
                ComparisonType.GreaterThanOrEqual => A >= B,
                ComparisonType.LessThanOrEqual => A <= B,
                _ => false
            };
            if (!met) return false;
        }
        return true;
    }

    public static string ComparisonToString(ComparisonType comparison)
    {
        return comparison switch
        {
            ComparisonType.Equals => "=",
            ComparisonType.NotEquals => "!=",
            ComparisonType.GreaterThan => ">",
            ComparisonType.LessThan => "<",
            ComparisonType.GreaterThanOrEqual => ">=",
            ComparisonType.LessThanOrEqual => "<=",
            _ => throw new ArgumentException("Invalid comparison type")
        };
    }

    public override string ToString()
    {
        var parts = new List<string>();
        parts.AddRange(SelfTypeChecks.Select(t => $"is({t})"));
        parts.AddRange(InhibitedSelfTypes.Select(t => $"!is({t})"));
        parts.AddRange(ActiveMarkers.Select(m => m.ToString()));
        parts.AddRange(InhibitedMarkers.Select(m => "!" + m.ToString()));
        parts.AddRange(TypedNeighborConditions.Select(tc =>
            $"ns({tc.CellType}{ComparisonToString(tc.Comparison)}{tc.Count})"));
        var markers = string.Join(" ", parts);

        var neighborStr = "";
        foreach (var condition in NeighborConditions)
        {
            var compPrefix = condition.Value == ComparisonType.Equals ? "" : ComparisonToString(condition.Value);
            neighborStr += $" n({compPrefix}{condition.Key})";
        }

        var clockStr = "";
        foreach (var condition in ClockConditions)
        {
            var compPrefix = condition.Value == ComparisonType.Equals ? "" : ComparisonToString(condition.Value);
            clockStr += $" t({compPrefix}{condition.Key})";
        }

        var rangeStr = Range > 0 ? Range.ToString() : "";
        var probStr = ProbabilityMorphogen != null ? $"({ProbabilityMorphogen})" : "";

        if (MovementTarget != null)
            return $"[{markers}{neighborStr}{clockStr}]{rangeStr}{(IsMovementAway ? "<" : ">")}{MovementTarget}";

        return $"[{markers}{neighborStr}{clockStr}]{rangeStr}{probStr}";
    }
}

public class Genome : List<Gene>
{
    public override string ToString()
    {
        return string.Join("\n", this.Select(g => g.ToString()));
    }
}

public class GELParseException : Exception
{
    public GELParseException(string message) : base(message) { }
}

public static class GEL_Parser
{
    private static readonly HashSet<string> FunctionMarkers = ["APOP", "NODIV", "MOVE"];

    public static Genome ParseGEL(string gel)
    {
        if (string.IsNullOrWhiteSpace(gel))
            throw new GELParseException("GEL string cannot be empty");

        // Reset type registry before parsing (keeps STEM builtin)
        CellTypeRegistry.Reset();

        var expressions = PreprocessVariables(gel);
        return ParseGenome(expressions);
    }

    public static string[] PreprocessVariables(string gel)
    {
        var lines = gel.Split(["\n"], StringSplitOptions.None);
        var result = new List<string>();
        var variables = new Dictionary<string, string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var commentIndex = line.IndexOf("//");
            if (commentIndex >= 0)
                line = line[..commentIndex];

            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // TYPE block: "TYPE NAME:" followed by indented property lines
            var typeMatch = Regex.Match(trimmed, @"^TYPE\s+(\w+)\s*:\s*$", RegexOptions.IgnoreCase);
            if (typeMatch.Success)
            {
                var typeName = typeMatch.Groups[1].Value;
                var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // Consume indented lines (properties)
                while (i + 1 < lines.Length)
                {
                    var nextRaw = lines[i + 1];
                    var nextComment = nextRaw.IndexOf("//");
                    if (nextComment >= 0)
                        nextRaw = nextRaw[..nextComment];

                    // Must be indented (starts with whitespace) and non-empty
                    if (nextRaw.Length > 0 && (nextRaw[0] == ' ' || nextRaw[0] == '\t'))
                    {
                        var propLine = nextRaw.Trim();
                        if (!string.IsNullOrEmpty(propLine))
                        {
                            // Parse "key: value"
                            var colonIdx = propLine.IndexOf(':');
                            if (colonIdx > 0)
                            {
                                var key = propLine[..colonIdx].Trim();
                                var val = propLine[(colonIdx + 1)..].Trim();
                                props[key] = val;
                            }
                        }
                        i++;
                    }
                    else
                    {
                        break;
                    }
                }

                // Register the type immediately (so gene expressions can reference it)
                ParseTypeBlock(typeName, props);
                continue;
            }

            // Variable definition: @name = value
            var varMatch = Regex.Match(trimmed, @"^@(\w+)\s*=\s*(.+)$");
            if (varMatch.Success)
            {
                var name = varMatch.Groups[1].Value;
                var value = varMatch.Groups[2].Value.Trim();
                variables[name] = value;
                continue;
            }

            // Substitute @name references
            trimmed = Regex.Replace(trimmed, @"@(\w+)", match =>
            {
                var name = match.Groups[1].Value;
                if (!variables.ContainsKey(name))
                    throw new GELParseException($"Undefined variable: '@{name}'");
                return variables[name];
            });

            result.Add(trimmed);
        }

        return result.ToArray();
    }

    public static Genome ParseGenome(string[] expressions)
    {
        var genome = new Genome();
        var specializationTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // TYPE blocks are already parsed during preprocessing. Only gene expressions remain.
        foreach (var expression in expressions)
        {
            try
            {
                var gene = ParseGeneExpression(expression.Trim());
                genome.Add(gene);

                if (gene.Function == GeneFunction.Specialization)
                    specializationTargets.Add(gene.OutputMarker);

                if (gene.Function == GeneFunction.Morphology)
                    MorphogenManager.RegisterMorphogen(gene.OutputMarker);
            }
            catch (GELParseException ex)
            {
                throw new GELParseException($"Error in expression '{expression}': {ex.Message}");
            }
        }

        foreach (var target in specializationTargets)
        {
            if (!CellTypeRegistry.Exists(target))
                throw new GELParseException(
                    $"Specialization target '{target}' has no TYPE definition. Add:\nTYPE {target}:\n  color: r, g, b");
        }

        return genome;
    }

    /// <summary>
    /// Parse a YAML-like TYPE block and register it in the CellTypeRegistry.
    /// Called during preprocessing when "TYPE NAME:" header is encountered.
    /// </summary>
    private static void ParseTypeBlock(string typeName, Dictionary<string, string> props)
    {
        var name = typeName.ToUpperInvariant();

        if (name == CellTypeRegistry.Stem)
            throw new GELParseException("Cannot redefine STEM. It is a built-in type.");

        var def = new CellTypeDefinition { Name = name };

        foreach (var (key, value) in props)
        {
            var args = value.Split(',').Select(s => s.Trim()).ToArray();
            bool isHex = args[0].StartsWith('#');
            // After the color, extra args start at index 1 (hex) or 4 (decimal rgba)
            int extraStart = isHex ? 1 : 4;

            switch (key.ToLowerInvariant())
            {
                case "color":
                    def.MainColor = ParseColor(args, "color");
                    break;
                case "nucleus":
                    def.NucleusColor = ParseColor(args, "nucleus");
                    if (args.Length > extraStart && float.TryParse(args[extraStart], CultureInfo.InvariantCulture, out var ratio))
                        def.NucleusRadiusRatio = ratio;
                    break;
                case "membrane":
                    def.MembraneColor = ParseColor(args, "membrane");
                    if (args.Length > extraStart && float.TryParse(args[extraStart], CultureInfo.InvariantCulture, out var thickness))
                        def.MembraneThickness = thickness;
                    break;
                default:
                    throw new GELParseException($"Unknown TYPE property: '{key}'. Valid: color, nucleus, membrane");
            }
        }

        CellTypeRegistry.Register(def);
    }

    /// <summary>
    /// Parse a color from either hex (#RRGGBB or #RRGGBBAA) or decimal (r, g, b[, a]) format.
    /// </summary>
    private static Raylib_cs.Color ParseColor(string[] args, string propName)
    {
        var first = args[0].Trim();

        // Hex format: #RRGGBB or #RRGGBBAA
        if (first.StartsWith('#'))
        {
            var hex = first[1..];
            if (hex.Length != 6 && hex.Length != 8)
                throw new GELParseException($"TYPE {propName}: hex color must be #RRGGBB or #RRGGBBAA. Got '{first}'");
            try
            {
                byte r = Convert.ToByte(hex[0..2], 16);
                byte g = Convert.ToByte(hex[2..4], 16);
                byte b = Convert.ToByte(hex[4..6], 16);
                byte a = hex.Length == 8 ? Convert.ToByte(hex[6..8], 16) : (byte)150;
                return new Raylib_cs.Color(r, g, b, a);
            }
            catch (FormatException)
            {
                throw new GELParseException($"TYPE {propName}: invalid hex color '{first}'");
            }
        }

        // Decimal format: r, g, b[, a]
        if (args.Length < 3)
            throw new GELParseException($"TYPE {propName} requires at least 3 values (r,g,b) or a hex color (#RRGGBB). Got {args.Length}");

        if (!byte.TryParse(args[0], out var dr) || !byte.TryParse(args[1], out var dg) || !byte.TryParse(args[2], out var db))
            throw new GELParseException($"Invalid color values in {propName}. Expected integers 0-255 or hex #RRGGBB");

        byte da = 150;
        if (args.Length >= 4 && byte.TryParse(args[3], out var alpha))
            da = alpha;

        return new Raylib_cs.Color(dr, dg, db, da);
    }

    public static Gene ParseGeneExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new GELParseException("Expression cannot be empty");

        var gene = new Gene();

        if (!expression.Contains("=>"))
            throw new GELParseException("Expression must contain an output marker and conditions separated by '=>'");

        var parts = expression.Split(["=>"], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            throw new GELParseException("Invalid expression format. Expected 'output => conditions'");

        var outputMarker = parts[0].Trim();
        var rightPart = parts[1].Trim();

        ValidateOutputMarker(outputMarker);
        gene.OutputMarker = outputMarker;

        // Determine gene function.
        // If the output marker is a registered (or soon-to-be) cell type and not a function marker,
        // treat it as specialization. This allows GEL-defined types to be specialization targets.
        gene.Function = outputMarker switch
        {
            "APOP" => GeneFunction.Apoptosis,
            "NODIV" => GeneFunction.NoDivision,
            "MOVE" => GeneFunction.Movement,
            _ when outputMarker.StartsWith("tm") => GeneFunction.StartTimer,
            _ when IsSpecializationMarker(outputMarker) => GeneFunction.Specialization,
            _ => GeneFunction.Morphology
        };

        if (!string.IsNullOrWhiteSpace(rightPart))
            ParseConditions(rightPart, gene);

        return gene;
    }

    /// <summary>
    /// A marker is a specialization marker if it's a registered cell type (other than STEM)
    /// or if it matches the pattern of a TYPE that will be defined (uppercase, not a morphogen pattern).
    /// We check registry first; during parsing, types are defined before gene expressions.
    /// </summary>
    private static bool IsSpecializationMarker(string marker)
    {
        if (FunctionMarkers.Contains(marker)) return false;
        if (marker.StartsWith("tm")) return false;
        // If it's registered as a cell type, it's a specialization target
        return CellTypeRegistry.Exists(marker);
    }

    private static void ValidateOutputMarker(string marker)
    {
        if (string.IsNullOrWhiteSpace(marker))
            throw new GELParseException("Output marker cannot be empty");
    }

    private static void ValidateMarker(string marker)
    {
        if (string.IsNullOrWhiteSpace(marker))
            throw new GELParseException("Marker cannot be empty");
    }

    private static ComparisonType ParseComparison(string compStr)
    {
        return compStr switch
        {
            "<=" => ComparisonType.LessThanOrEqual,
            ">=" => ComparisonType.GreaterThanOrEqual,
            "!=" => ComparisonType.NotEquals,
            "<" => ComparisonType.LessThan,
            ">" => ComparisonType.GreaterThan,
            "=" => ComparisonType.Equals,
            _ => throw new GELParseException($"Invalid comparison operator: '{compStr}'")
        };
    }

    private static MarkerCondition ParseMarkerWithOptionalThreshold(string token)
    {
        var match = Regex.Match(token, @"^(\w+)\(([<>=!]+)([\d.]+)\)$");
        if (match.Success)
        {
            var name = match.Groups[1].Value;
            var compStr = match.Groups[2].Value;
            var valueStr = match.Groups[3].Value;

            var comparison = ParseComparison(compStr);
            if (!float.TryParse(valueStr, CultureInfo.InvariantCulture, out var threshold))
                throw new GELParseException($"Invalid threshold value: '{valueStr}'");
            return new MarkerCondition(name, comparison, threshold);
        }
        return new MarkerCondition(token);
    }

    private static void ParseConditions(string conditions, Gene gene)
    {
        if (string.IsNullOrWhiteSpace(conditions))
            throw new GELParseException("Conditions cannot be empty");

        if (conditions.Count(c => c == '[') != conditions.Count(c => c == ']'))
            throw new GELParseException("Unclosed brackets found in conditions");

        var bracketMatches = Regex.Matches(conditions, @"\[(.*?)\](?:(\d+))?(?:\((\w+)\))?(?:([<>])(\w+))?");
        if (bracketMatches.Count == 0)
            throw new GELParseException("No valid condition sets found. Expected format: [markers] or [markers]range or [markers](Morphogen)");

        foreach (Match bracketMatch in bracketMatches)
        {
            var conditionSet = new ConditionSet();
            var markers = bracketMatch.Groups[1].Value.Split([" "], StringSplitOptions.RemoveEmptyEntries);

            if (bracketMatch.Groups[2].Success)
            {
                if (!int.TryParse(bracketMatch.Groups[2].Value, out var range))
                    throw new GELParseException($"Invalid range value: '{bracketMatch.Groups[2].Value}'. Range must be a positive integer");
                conditionSet.Range = range;
            }

            if (bracketMatch.Groups[3].Success)
            {
                if (gene.OutputMarker != "APOP")
                    throw new GELParseException("Probability morphogen syntax (Morphogen) is only allowed when output marker is APOP");
                conditionSet.ProbabilityMorphogen = bracketMatch.Groups[3].Value;
            }

            if (bracketMatch.Groups[5].Success)
            {
                if (gene.OutputMarker != "MOVE")
                    throw new GELParseException("Movement syntax is only allowed when output marker is MOVE");
                var targetMarker = bracketMatch.Groups[5].Value;
                ValidateMarker(targetMarker);
                conditionSet.MovementTarget = targetMarker;
                conditionSet.IsMovementAway = bracketMatch.Groups[4].Value == "<";
                gene.Function = GeneFunction.Movement;
            }

            foreach (var marker in markers)
            {
                if (marker.StartsWith("n("))
                {
                    if (!marker.EndsWith(')'))
                        throw new GELParseException("Invalid neighbor condition. Expected format: n(count)");
                    var countStr = marker[2..^1];
                    ComparisonType comparisonType = ComparisonType.Equals;
                    if (countStr.StartsWith("<=")) comparisonType = ComparisonType.LessThanOrEqual;
                    else if (countStr.StartsWith(">=")) comparisonType = ComparisonType.GreaterThanOrEqual;
                    else if (countStr.StartsWith('<')) comparisonType = ComparisonType.LessThan;
                    else if (countStr.StartsWith('>')) comparisonType = ComparisonType.GreaterThan;
                    else if (countStr.StartsWith('!')) comparisonType = ComparisonType.NotEquals;
                    countStr = countStr.TrimStart(['!', '=', '<', '>']);
                    if (!int.TryParse(countStr, out var count) || count < 0 || count > 6)
                        throw new GELParseException($"Invalid neighbor count: '{countStr}'. Must be an integer between 0 and 6");
                    conditionSet.AddNeighborCondition(count, comparisonType);
                }
                else if (marker.StartsWith("t("))
                {
                    if (!marker.EndsWith(')'))
                        throw new GELParseException("Invalid clock condition. Expected format: t(>5)");
                    var countStr = marker[2..^1];
                    ComparisonType comparisonType = ComparisonType.Equals;
                    if (countStr.StartsWith("<=")) comparisonType = ComparisonType.LessThanOrEqual;
                    else if (countStr.StartsWith(">=")) comparisonType = ComparisonType.GreaterThanOrEqual;
                    else if (countStr.StartsWith('<')) comparisonType = ComparisonType.LessThan;
                    else if (countStr.StartsWith('>')) comparisonType = ComparisonType.GreaterThan;
                    else if (countStr.StartsWith('!')) comparisonType = ComparisonType.NotEquals;
                    countStr = countStr.TrimStart(['!', '=', '<', '>']);
                    if (!int.TryParse(countStr, out var count) || count < 0)
                        throw new GELParseException($"Invalid clock value: '{countStr}'. Must be a non-negative integer");
                    conditionSet.AddClockCondition(count, comparisonType);
                }
                else if (marker.StartsWith("ns("))
                {
                    if (!marker.EndsWith(')'))
                        throw new GELParseException("Invalid typed neighbor condition. Expected format: ns(TYPE) or ns(TYPE>=N)");
                    var inner = marker[3..^1];
                    var nsMatch = Regex.Match(inner, @"^(\w+)(?:([<>=!]+)(\d+))?$");
                    if (!nsMatch.Success)
                        throw new GELParseException($"Invalid typed neighbor condition format: '{inner}'");
                    var typeStr = nsMatch.Groups[1].Value.ToUpperInvariant();
                    // No enum validation needed - any string is a valid type name
                    var nsComp = ComparisonType.GreaterThanOrEqual;
                    var nsCount = 1;
                    if (nsMatch.Groups[2].Success)
                    {
                        nsComp = ParseComparison(nsMatch.Groups[2].Value);
                        nsCount = int.Parse(nsMatch.Groups[3].Value);
                    }
                    conditionSet.TypedNeighborConditions.Add((typeStr, nsCount, nsComp));
                }
                else if (marker.StartsWith("is(") || marker.StartsWith("!is("))
                {
                    bool negated = marker.StartsWith("!");
                    var typeStr = negated ? marker[4..^1] : marker[3..^1];
                    typeStr = typeStr.ToUpperInvariant();
                    // No enum validation - any string is valid
                    if (negated)
                        conditionSet.InhibitedSelfTypes.Add(typeStr);
                    else
                        conditionSet.SelfTypeChecks.Add(typeStr);
                }
                else if (marker.StartsWith("!"))
                {
                    var markerToken = marker[1..];
                    var mc = ParseMarkerWithOptionalThreshold(markerToken);
                    ValidateMarker(mc.MarkerName);
                    conditionSet.AddInhibitor(mc);
                }
                else
                {
                    var mc = ParseMarkerWithOptionalThreshold(marker);
                    ValidateMarker(mc.MarkerName);
                    conditionSet.AddMarker(mc);
                }
            }

            gene.ConditionSets.Add(conditionSet);
        }
    }
}
