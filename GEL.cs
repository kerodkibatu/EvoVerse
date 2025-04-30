// Genetic Expression Language
using System;
using System.Collections.Generic;
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
    // List of active markers
    public List<string> ActiveMarkers { get; set; } = [];

    // List of inhibited markers
    public List<string> InhibitedMarkers { get; set; } = [];

    // Environmental conditions
    public Dictionary<int, ComparisonType> NeighborConditions { get; set; } = [];

    // Range or timer value
    public int Range { get; set; } = 0;

    // Target marker for movement genes (what to move toward or away from)
    public string? MovementTarget { get; set; }

    // Whether movement is away from the target (true) or towards it (false)
    public bool IsMovementAway { get; set; } = false;

    public void AddMarker(string marker)
    {
        if (!ActiveMarkers.Contains(marker))
        {
            ActiveMarkers.Add(marker);
        }
    }

    public void AddInhibitor(string marker)
    {
        if (!InhibitedMarkers.Contains(marker))
        {
            InhibitedMarkers.Add(marker);
        }
    }

    public void AddNeighborCondition(int count, ComparisonType comparison = ComparisonType.Equals)
    {
        NeighborConditions[count] = comparison;
    }

    public bool Evaluate(WorldGrid grid, Cell cell)
    {
        foreach (var marker in ActiveMarkers)
        {
            if (grid.GetMorphogenStrength(cell.Position, marker) == 0)
            {
                return false;
            }
        }
        foreach (var marker in InhibitedMarkers)
        {
            if (grid.GetMorphogenStrength(cell.Position, marker) > 0)
            {
                return false;
            }
        }
        // TODO: Fix this
        bool neighborConditionMet = true; // Assume true until proven otherwise
        foreach (var condition in NeighborConditions)
        {
            var A = 6 - cell.GetEmptyNeighborHexes(grid).Count;
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
        var activeStr = string.Join(" ", ActiveMarkers.Select(m => m.ToString()));
        var inhibitedStr = string.Join(" ", InhibitedMarkers.Select(m => "!" + m.ToString()));
        var markers = activeStr + (string.IsNullOrEmpty(activeStr) || string.IsNullOrEmpty(inhibitedStr) ? "" : " ") + inhibitedStr;

        var neighborStr = "";
        foreach (var condition in NeighborConditions)
        {
            neighborStr += $" n({ComparisonToString(condition.Value)}{condition.Key})";
        }

        var rangeStr = Range > 0 ? Range.ToString() : "";

        if (MovementTarget != null)
        {
            return $"[{markers}{neighborStr}]{rangeStr}{(IsMovementAway ? "<" : ">")}{MovementTarget}";
        }

        return $"[{markers}{neighborStr}]{rangeStr}";
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

        var expressions = gel.Split(["\n"], StringSplitOptions.RemoveEmptyEntries);
        return ParseGenome(expressions);
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
        var bracketMatches = Regex.Matches(conditions, @"\[(.*?)\](?:(\d+))?(?:([<>])(\w+))?");
        if (bracketMatches.Count == 0)
        {
            throw new GELParseException("No valid condition sets found. Expected format: [markers] or [markers]range or [markers]range<target");
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

            // Parse movement target if present
            if (bracketMatch.Groups[4].Success)
            {
                if (gene.OutputMarker != "MOVE")
                {
                    throw new GELParseException("Movement syntax is only allowed when output marker is MOVE");
                }
                var targetMarker = bracketMatch.Groups[4].Value;
                ValidateMarker(targetMarker);
                conditionSet.MovementTarget = targetMarker;
                conditionSet.IsMovementAway = bracketMatch.Groups[3].Value == "<";
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
                else if (marker.StartsWith("!"))
                {
                    // Parse inhibited marker
                    var markerName = marker[1..];
                    ValidateMarker(markerName);
                    conditionSet.AddInhibitor(markerName);
                }
                else
                {
                    // Parse active marker
                    ValidateMarker(marker);
                    conditionSet.AddMarker(marker);
                }
            }

            gene.ConditionSets.Add(conditionSet);
        }
    }
}