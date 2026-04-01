using EvoVerse;

namespace EvoVerse.Tests;

[Collection("GELParser")]
public class GELParserTests
{
    [Fact]
    public void ParseGEL_EmptyString_Throws()
    {
        var ex = Assert.Throws<GELParseException>(() => GEL_Parser.ParseGEL(""));
        Assert.Contains("cannot be empty", ex.Message);

        ex = Assert.Throws<GELParseException>(() => GEL_Parser.ParseGEL("   "));
        Assert.Contains("cannot be empty", ex.Message);
    }

    [Fact]
    public void ParseGEL_ValidSimpleGenome_ParsesCorrectly()
    {
        try
        {
            var genome = GEL_Parser.ParseGEL("M0 => [M0]");
            Assert.Single(genome);
            Assert.Equal("M0", genome[0].OutputMarker);
        }
        finally
        {
            MorphogenManager.UnregisterMorphogen("M0");
        }
    }

    [Fact]
    public void ParseGEL_NODIVWithCondition_ParsesCorrectly()
    {
        var genome = GEL_Parser.ParseGEL("NODIV => [n(0)]");
        Assert.Single(genome);
        Assert.Equal("NODIV", genome[0].OutputMarker);
        Assert.Single(genome[0].ConditionSets);
        Assert.Single(genome[0].ConditionSets[0].NeighborConditions);
        Assert.True(genome[0].ConditionSets[0].NeighborConditions.ContainsKey(0));
    }

    [Fact]
    public void ParseGEL_WithVariables_SubstitutesCorrectly()
    {
        try
        {
            var gel = @"@cond = [is(STEM) M1]
M0 => @cond";
            var genome = GEL_Parser.ParseGEL(gel);
            Assert.Single(genome);
            Assert.Equal("M0", genome[0].OutputMarker);
            Assert.Single(genome[0].ConditionSets);
            Assert.Contains(genome[0].ConditionSets[0].SelfTypeChecks, t => t == CellTypeRegistry.Stem);
            Assert.Single(genome[0].ConditionSets[0].ActiveMarkers);
            Assert.Equal("M1", genome[0].ConditionSets[0].ActiveMarkers[0].MarkerName);
        }
        finally
        {
            MorphogenManager.UnregisterMorphogen("M0");
        }
    }

    [Fact]
    public void ParseGEL_UndefinedVariable_Throws()
    {
        var ex = Assert.Throws<GELParseException>(() => GEL_Parser.ParseGEL("M0 => @undefined"));
        Assert.Contains("Undefined variable", ex.Message);
        Assert.Contains("@undefined", ex.Message);
    }

    [Fact]
    public void ParseGEL_InvalidFormat_Throws()
    {
        var ex = Assert.Throws<GELParseException>(() => GEL_Parser.ParseGEL("M0"));
        Assert.Contains("=>", ex.Message);

        ex = Assert.Throws<GELParseException>(() => GEL_Parser.ParseGEL("M0 => => [M0]"));
        Assert.Contains("Invalid expression", ex.Message);
    }

    [Fact]
    public void ParseGeneExpression_WithMarkerThreshold_ParsesCorrectly()
    {
        var gene = GEL_Parser.ParseGeneExpression("M6 => [M6(<0.2)]");
        Assert.Equal("M6", gene.OutputMarker);
        Assert.Single(gene.ConditionSets);
        Assert.Single(gene.ConditionSets[0].ActiveMarkers);
        var mc = gene.ConditionSets[0].ActiveMarkers[0];
        Assert.Equal("M6", mc.MarkerName);
        Assert.Equal(ComparisonType.LessThan, mc.Comparison);
        Assert.Equal(0.2f, mc.Threshold);
    }

    [Fact]
    public void ParseGeneExpression_EmptyExpression_Throws()
    {
        Assert.Throws<GELParseException>(() => GEL_Parser.ParseGeneExpression(""));
        Assert.Throws<GELParseException>(() => GEL_Parser.ParseGeneExpression("   "));
    }

    [Fact]
    public void MarkerCondition_ToString_RoundTrip()
    {
        var mc1 = new MarkerCondition("M0");
        Assert.Equal("M0", mc1.ToString());

        var mc2 = new MarkerCondition("M6", ComparisonType.LessThan, 0.2f);
        Assert.Equal("M6(<0.2)", mc2.ToString());

        var mc3 = new MarkerCondition("M1", ComparisonType.GreaterThanOrEqual, 0.5f);
        Assert.Equal("M1(>=0.5)", mc3.ToString());
    }

    [Fact]
    public void ParseGEL_Comments_Ignored()
    {
        try
        {
            var gel = @"// This is a comment
M0 => [M0] // inline comment";
            var genome = GEL_Parser.ParseGEL(gel);
            Assert.Single(genome);
            Assert.Equal("M0", genome[0].OutputMarker);
        }
        finally
        {
            MorphogenManager.UnregisterMorphogen("M0");
        }
    }

    [Fact]
    public void ParseGEL_UnclosedBrackets_Throws()
    {
        var ex = Assert.Throws<GELParseException>(() => GEL_Parser.ParseGEL("M0 => [M0"));
        Assert.Contains("Unclosed brackets", ex.Message);
    }
}
