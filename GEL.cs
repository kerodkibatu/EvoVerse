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
    // List of condition sets (each set represents a bracketed condition)
    public List<ConditionSet> ConditionSets { get; set; } = [];

    // Function or phenotype this gene affects
    public GeneFunction Function { get; set; } = GeneFunction.Morphology;

    // For output marker release
    public string OutputMarker { get; set; } = "NODIV";

    public Gene()
    {
    }

    public Gene(string outputMarker)
    {
        OutputMarker = outputMarker;
    }

    public IEnumerable<ConditionSet> Evaluate(WorldGrid grid, Cell cell)
    {
        foreach (var conditionSet in ConditionSets)
        {
            if (conditionSet.Evaluate(grid, cell))
            {
                yield return conditionSet;
            }
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
    // List of active markers (with optional thresholds)
    public List<MarkerCondition> ActiveMarkers { get; set; } = [];

    // List of inhibited markers (with optional thresholds)
    public List<MarkerCondition> InhibitedMarkers { get; set; } = [];

    // Environmental conditions
    public Dictionary<int, ComparisonType> NeighborConditions { get; set; } = [];

    // Clock/age conditions
    public Dictionary<int, ComparisonType> ClockConditions { get; set; } = [];

    // Range or timer value
    public int Range { get; set; } = 0;

    // Self-type conditions (checked directly against cell.Type)
    public List<CellType> SelfTypeChecks { get; set; } = [];
    public List<CellType> InhibitedSelfTypes { get; set; } = [];

    // Typed neighbor count conditions — ns(SKIN>=2) etc.
    public List<(CellType CellType, int Count, ComparisonType Comparison)> TypedNeighborConditions { get; set; } = [];

    // Target marker for movement genes (what to move toward or away from)
    public string? MovementTarget { get; set; }

    // Whether movement is away from the target (true) or towards it (false)
    public bool IsMovementAway { get; set; } = false;

    // Probability morphogen for APOP: Die(1 - concentration). Null = use Range-based probability.
    public string? ProbabilityMorphogen { get; set; }

    public void AddMarker(MarkerCondition marker)
    {
        if (!ActiveMarkers.Any(m => m.MarkerName == marker.MarkerName))
        {
            ActiveMarkers.Add(marker);
        }
    }

    public void AddInhibitor(MarkerCondition marker)
    {
        if (!InhibitedMarkers.Any(m => m.MarkerName == marker.MarkerName))
        {
            InhibitedMarkers.Add(marker);
        }
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
        // Check self-type conditions
        foreach (var t in SelfTypeChecks)
            if (cell.Type != t) return false;
        foreach (var t in InhibitedSelfTypes)
            if (cell.Type == t) return false;

        // Batch morphogen lookups when marker conditions exist
        Dictionary<string, float>? morphogens = null;
        if (ActiveMarkers.Count > 0 || InhibitedMarkers.Count > 0)
        {
            morphogens = grid.GetMorphogensAtHex(cell.Position);
        }
        float GetStrength(string name) => morphogens?.TryGetValue(name, out var s) == true ? s : 0f;

        // Check active markers (with optional thresholds)
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
                if (strength == 0)
                    return false;
            }
        }
        // Check inhibited markers (with optional thresholds)
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
                if (strength > 0)
                    return false;
            }
        }
        // Check clock/age conditions
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
        // Check typed neighbor count conditions (ns)
        foreach (var (cellType, count, comparison) in TypedNeighborConditions)
        {
            int typedCount = 0;
            for (int i = 0; i < 6; i++)
            {
                var neighborCell = grid.GetCell(cell.Position.Neighbor(i));
                if (neighborCell?.Type == cellType)
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

        // Compute occupied neighbor count once (used by all NeighborConditions)
        int occupiedNeighborCount = NeighborConditions.Count > 0
            ? 6 - cell.GetEmptyNeighborHexes(grid).Count
            : 0;

        bool neighborConditionMet = true; // Assume true until proven otherwise
        foreach (var condition in NeighborConditions)
        {
            var A = occupiedNeighborCount;
            var B = condition.Key;
            switch (condition.Value)
            {
                case ComparisonType.Equals:
                    if (A != B) neighborConditionMet = false;
                    break;
                case ComparisonType.NotEquals:
                    if (A == B) neighborConditionMet = false;
                    break;
                case ComparisonType.GreaterThan:
                    if (A <= B) neighborConditionMet = false;
                    break;
                case ComparisonType.LessThan:
                    if (A >= B) neighborConditionMet = false;
                    break;
                case ComparisonType.GreaterThanOrEqual:
                    if (A < B) neighborConditionMet = false;
                    break;
                case ComparisonType.LessThanOrEqual:
                    if (A > B) neighborConditionMet = false;
                    break;
                default:
                    neighborConditionMet = false;
                    break;
            }
            // If any condition fails, we can exit early
            if (!neighborConditionMet) break;
        }
        return neighborConditionMet;
    }

    public string ComparisonToString(ComparisonType comparison)
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
        parts.AddRange(SelfTypeChecks.Select(t => $"is({t.ToString().ToUpperInvariant()})"));
        parts.AddRange(InhibitedSelfTypes.Select(t => $"!is({t.ToString().ToUpperInvariant()})"));
        parts.AddRange(ActiveMarkers.Select(m => m.ToString()));
        parts.AddRange(InhibitedMarkers.Select(m => "!" + m.ToString()));
        parts.AddRange(TypedNeighborConditions.Select(tc =>
            $"ns({tc.CellType.ToString().ToUpperInvariant()}{ComparisonToString(tc.Comparison)}{tc.Count})"));
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
        {
            return $"[{markers}{neighborStr}{clockStr}]{rangeStr}{(IsMovementAway ? "<" : ">")}{MovementTarget}";
        }

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
    private static readonly HashSet<string> FunctionMarkers = ["APOP", "NODIV", "SKIN", "FLESH", "MOVE"];
    private static readonly HashSet<string> SpecializationMarkers = ["SKIN", "FLESH"];

    public static Genome ParseGEL(string gel)
    {
        if (string.IsNullOrWhiteSpace(gel))
        {
            throw new GELParseException("GEL string cannot be empty");
        }

        var expressions = PreprocessVariables(gel);
        return ParseGenome(expressions);
    }

    public static string[] PreprocessVariables(string gel)
    {
        var lines = gel.Split(["\n"], StringSplitOptions.None);
        var result = new List<string>();
        var variables = new Dictionary<string, string>();

        foreach (var rawLine in lines)
        {
            // Strip // comments
            var line = rawLine;
            var commentIndex = line.IndexOf("//");
            if (commentIndex >= 0)
                line = line[..commentIndex];

            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Check for variable definition: @name = value
            var varMatch = Regex.Match(line, @"^@(\w+)\s*=\s*(.+)$");
            if (varMatch.Success)
            {
                var name = varMatch.Groups[1].Value;
                var value = varMatch.Groups[2].Value.Trim();
                variables[name] = value;
                continue;
            }

            // Substitute @name references
            line = Regex.Replace(line, @"@(\w+)", match =>
            {
                var name = match.Groups[1].Value;
                if (!variables.ContainsKey(name))
                    throw new GELParseException($"Undefined variable: '@{name}'");
                return variables[name];
            });

            result.Add(line);
        }

        return result.ToArray();
    }

    public static Genome ParseGenome(string[] expressions)
    {
        var genome = new Genome();
        foreach (var expression in expressions)
        {
            try
            {
                var gene = ParseGeneExpression(expression.Trim());
                genome.Add(gene);

                // Register non-function markers
                if (!FunctionMarkers.Contains(gene.OutputMarker))
                {
                    MorphogenManager.RegisterMorphogen(gene.OutputMarker);
                }
            }
            catch (GELParseException ex)
            {
                throw new GELParseException($"Error in expression '{expression}': {ex.Message}");
            }
        }
        return genome;
    }

    public static Gene ParseGeneExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new GELParseException("Expression cannot be empty");
        }

        var gene = new Gene();

        if (!expression.Contains("=>"))
        {
            throw new GELParseException("Expression must contain an output marker and conditions separated by '=>'");
        }

        var parts = expression.Split(["=>"], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new GELParseException("Invalid expression format. Expected 'output => conditions'");
        }

        var outputMarker = parts[0].Trim();
        var rightPart = parts[1].Trim();

        ValidateOutputMarker(outputMarker);
        gene.OutputMarker = outputMarker;

        // Map gene function based on output marker
        gene.Function = outputMarker switch
        {
            "APOP" => GeneFunction.Apoptosis,
            "NODIV" => GeneFunction.NoDivision,
            "MOVE" => GeneFunction.Movement,
            _ when outputMarker.StartsWith("tm") => GeneFunction.StartTimer,
            _ when SpecializationMarkers.Contains(outputMarker) => GeneFunction.Specialization,
            _ => GeneFunction.Morphology
        };

        // Only parse conditions if there are any
        if (!string.IsNullOrWhiteSpace(rightPart))
        {
            ParseConditions(rightPart, gene);
        }

        return gene;
    }

    private static void ValidateOutputMarker(string marker)
    {
        if (string.IsNullOrWhiteSpace(marker))
        {
            throw new GELParseException("Output marker cannot be empty");
        }
    }

    private static void ValidateMarker(string marker)
    {
        if (string.IsNullOrWhiteSpace(marker))
        {
            throw new GELParseException("Marker cannot be empty");
        }
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
            {
                throw new GELParseException($"Invalid threshold value: '{valueStr}'");
            }
            return new MarkerCondition(name, comparison, threshold);
        }
        return new MarkerCondition(token);
    }

    private static void ParseConditions(string conditions, Gene gene)
    {
        if (string.IsNullOrWhiteSpace(conditions))
        {
            throw new GELParseException("Conditions cannot be empty");
        }

        // Check for unclosed brackets
        if (conditions.Count(c => c == '[') != conditions.Count(c => c == ']'))
        {
            throw new GELParseException("Unclosed brackets found in conditions");
        }

        // Extract content between brackets
        // Groups: [1]=content, [2]=range, [3]=probability morphogen (Mbase), [4]=movement dir, [5]=movement target
        var bracketMatches = Regex.Matches(conditions, @"\[(.*?)\](?:(\d+))?(?:\((\w+)\))?(?:([<>])(\w+))?");
        if (bracketMatches.Count == 0)
        {
            throw new GELParseException("No valid condition sets found. Expected format: [markers] or [markers]range or [markers](Morphogen)");
        }

        foreach (Match bracketMatch in bracketMatches)
        {
            var conditionSet = new ConditionSet();
            var markers = bracketMatch.Groups[1].Value.Split([" "], StringSplitOptions.RemoveEmptyEntries);

            // Parse range if present
            if (bracketMatch.Groups[2].Success)
            {
                if (!int.TryParse(bracketMatch.Groups[2].Value, out var range))
                {
                    throw new GELParseException($"Invalid range value: '{bracketMatch.Groups[2].Value}'. Range must be a positive integer");
                }
                conditionSet.Range = range;
            }

            // Parse probability morphogen if present: [conditions](Mbase) → Die(1 - concentration)
            if (bracketMatch.Groups[3].Success)
            {
                if (gene.OutputMarker != "APOP")
                {
                    throw new GELParseException("Probability morphogen syntax (Morphogen) is only allowed when output marker is APOP");
                }
                conditionSet.ProbabilityMorphogen = bracketMatch.Groups[3].Value;
            }

            // Parse movement target if present
            if (bracketMatch.Groups[5].Success)
            {
                if (gene.OutputMarker != "MOVE")
                {
                    throw new GELParseException("Movement syntax is only allowed when output marker is MOVE");
                }
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
                    // throw error if there is no closing bracket
                    if (!marker.EndsWith(')'))
                    {
                        throw new GELParseException("Invalid neighbor condition. Expected format: n(count)");
                    }
                    // Parse neighbor condition
                    var countStr = marker[2..^1];
                    ComparisonType comparisonType = ComparisonType.Equals;
                    if (countStr.StartsWith("<="))
                    {
                        comparisonType = ComparisonType.LessThanOrEqual;
                    }
                    else if (countStr.StartsWith(">="))
                    {
                        comparisonType = ComparisonType.GreaterThanOrEqual;
                    }
                    else if (countStr.StartsWith('<'))
                    {
                        comparisonType = ComparisonType.LessThan;
                    }
                    else if (countStr.StartsWith('>'))
                    {
                        comparisonType = ComparisonType.GreaterThan;
                    }
                    else if (countStr.StartsWith('!'))
                    {
                        comparisonType = ComparisonType.NotEquals;
                    }
                    countStr = countStr.TrimStart(['!', '=', '<', '>']);
                    if (!int.TryParse(countStr, out var count) || count < 0 || count > 6)
                    {
                        throw new GELParseException($"Invalid neighbor count: '{countStr}'. Must be an integer between 0 and 6");
                    }
                    conditionSet.AddNeighborCondition(count, comparisonType);
                }
                else if (marker.StartsWith("t("))
                {
                    if (!marker.EndsWith(')'))
                    {
                        throw new GELParseException("Invalid clock condition. Expected format: t(>5)");
                    }
                    // Parse clock/age condition
                    var countStr = marker[2..^1];
                    ComparisonType comparisonType = ComparisonType.Equals;
                    if (countStr.StartsWith("<="))
                    {
                        comparisonType = ComparisonType.LessThanOrEqual;
                    }
                    else if (countStr.StartsWith(">="))
                    {
                        comparisonType = ComparisonType.GreaterThanOrEqual;
                    }
                    else if (countStr.StartsWith('<'))
                    {
                        comparisonType = ComparisonType.LessThan;
                    }
                    else if (countStr.StartsWith('>'))
                    {
                        comparisonType = ComparisonType.GreaterThan;
                    }
                    else if (countStr.StartsWith('!'))
                    {
                        comparisonType = ComparisonType.NotEquals;
                    }
                    countStr = countStr.TrimStart(['!', '=', '<', '>']);
                    if (!int.TryParse(countStr, out var count) || count < 0)
                    {
                        throw new GELParseException($"Invalid clock value: '{countStr}'. Must be a non-negative integer");
                    }
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
                    var typeStr = nsMatch.Groups[1].Value;
                    if (!Enum.TryParse<CellType>(typeStr, true, out var nsCellType) || nsCellType == CellType.None)
                        throw new GELParseException($"Unknown cell type in ns() condition: '{typeStr}'. Valid types: STEM, FLESH, SKIN");
                    var nsComp = ComparisonType.GreaterThanOrEqual;
                    var nsCount = 1;
                    if (nsMatch.Groups[2].Success)
                    {
                        nsComp = ParseComparison(nsMatch.Groups[2].Value);
                        nsCount = int.Parse(nsMatch.Groups[3].Value);
                    }
                    conditionSet.TypedNeighborConditions.Add((nsCellType, nsCount, nsComp));
                }
                else if (marker.StartsWith("is(") || marker.StartsWith("!is("))
                {
                    bool negated = marker.StartsWith("!");
                    var typeStr = negated ? marker[4..^1] : marker[3..^1];
                    if (!Enum.TryParse<CellType>(typeStr, true, out var cellType) || cellType == CellType.None)
                        throw new GELParseException($"Unknown cell type in is() condition: '{typeStr}'. Valid types: STEM, FLESH, SKIN");
                    if (negated)
                        conditionSet.InhibitedSelfTypes.Add(cellType);
                    else
                        conditionSet.SelfTypeChecks.Add(cellType);
                }
                else if (marker.StartsWith("!"))
                {
                    // Parse inhibited marker (with optional threshold)
                    var markerToken = marker[1..];
                    var mc = ParseMarkerWithOptionalThreshold(markerToken);
                    ValidateMarker(mc.MarkerName);
                    conditionSet.AddInhibitor(mc);
                }
                else
                {
                    // Parse active marker (with optional threshold)
                    var mc = ParseMarkerWithOptionalThreshold(marker);
                    ValidateMarker(mc.MarkerName);
                    conditionSet.AddMarker(mc);
                }
            }

            gene.ConditionSets.Add(conditionSet);
        }
    }
}
