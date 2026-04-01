using System.Numerics;
using EvoVerse;

namespace EvoVerse.Tests;

public class HexTests
{
    [Fact]
    public void Arithmetic_Addition_ReturnsCorrectSum()
    {
        var a = new Hex(1, 2);
        var b = new Hex(3, -1);
        var sum = a + b;
        Assert.Equal(4, sum.Q);
        Assert.Equal(1, sum.R);
    }

    [Fact]
    public void Arithmetic_Subtraction_ReturnsCorrectDifference()
    {
        var a = new Hex(5, 3);
        var b = new Hex(2, 1);
        var diff = a - b;
        Assert.Equal(3, diff.Q);
        Assert.Equal(2, diff.R);
    }

    [Fact]
    public void Arithmetic_Multiplication_ScalesCorrectly()
    {
        var h = new Hex(2, -1);
        var scaled = h * 3;
        Assert.Equal(6, scaled.Q);
        Assert.Equal(-3, scaled.R);
    }

    [Fact]
    public void Arithmetic_Division_DividesCorrectly()
    {
        var h = new Hex(6, 4);
        var divided = h / 2;
        Assert.Equal(3, divided.Q);
        Assert.Equal(2, divided.R);
    }

    [Fact]
    public void Distance_FromOrigin_ReturnsCorrectLength()
    {
        var origin = new Hex(0, 0);
        Assert.Equal(0, origin.Length());

        var h1 = new Hex(1, 0);
        Assert.Equal(1, h1.Length());

        var h2 = new Hex(1, -1);
        Assert.Equal(1, h2.Length());

        var h3 = new Hex(2, -1);
        Assert.Equal(2, h3.Length());
    }

    [Fact]
    public void Distance_BetweenHexes_ReturnsCorrectAxialDistance()
    {
        var a = new Hex(0, 0);
        var b = new Hex(1, 0);
        Assert.Equal(1, a.Distance(b));
        Assert.Equal(1, b.Distance(a));

        var c = new Hex(2, -1);
        Assert.Equal(2, a.Distance(c));

        var d = new Hex(-1, 2);
        Assert.Equal(2, a.Distance(d));
    }

    [Fact]
    public void Neighbors_ReturnsSixNeighbors()
    {
        var center = new Hex(0, 0);
        var neighbors = center.Neighbors().ToList();
        Assert.Equal(6, neighbors.Count);

        var expected = new HashSet<Hex>
        {
            new(1, 0), new(1, -1), new(0, -1),
            new(-1, 0), new(-1, 1), new(0, 1)
        };
        Assert.True(neighbors.All(n => expected.Contains(n)));
    }

    [Fact]
    public void Direction_ReturnsCorrectHexForEachDirection()
    {
        var center = new Hex(0, 0);
        Assert.Equal(new Hex(1, 0), center.Direction(0));
        Assert.Equal(new Hex(1, -1), center.Direction(1));
        Assert.Equal(new Hex(0, -1), center.Direction(2));
        Assert.Equal(new Hex(-1, 0), center.Direction(3));
        Assert.Equal(new Hex(-1, 1), center.Direction(4));
        Assert.Equal(new Hex(0, 1), center.Direction(5));
        Assert.Equal(new Hex(1, 0), center.Direction(6));
    }

    [Fact]
    public void Neighbor_ReturnsAdjacentHex()
    {
        var center = new Hex(0, 0);
        Assert.Equal(new Hex(1, 0), center.Neighbor(0));
        Assert.Equal(new Hex(0, 1), center.Neighbor(5));
    }

    [Fact]
    public void GetHexesInRange_Range0_ReturnsSingleHex()
    {
        var center = new Hex(3, -2);
        var hexes = Hex.GetHexesInRange(center, 0).ToList();
        Assert.Single(hexes);
        Assert.Equal(center, hexes[0]);
    }

    [Fact]
    public void GetHexesInRange_Range1_ReturnsSevenHexes()
    {
        var center = new Hex(0, 0);
        var hexes = Hex.GetHexesInRange(center, 1).ToList();
        Assert.Equal(7, hexes.Count);
        Assert.Contains(center, hexes);
        Assert.Contains(new Hex(1, 0), hexes);
        Assert.Contains(new Hex(1, -1), hexes);
        Assert.Contains(new Hex(0, -1), hexes);
        Assert.Contains(new Hex(-1, 0), hexes);
        Assert.Contains(new Hex(-1, 1), hexes);
        Assert.Contains(new Hex(0, 1), hexes);
    }

    [Fact]
    public void GetHexesInRange_Range2_ReturnsNineteenHexes()
    {
        var center = new Hex(0, 0);
        var hexes = Hex.GetHexesInRange(center, 2).ToList();
        Assert.Equal(19, hexes.Count);
        Assert.Contains(center, hexes);
        Assert.Contains(new Hex(2, -1), hexes);
        Assert.Contains(new Hex(-2, 1), hexes);
    }

    [Fact]
    public void GetHexesInRange_BufferOverload_MatchesEnumerable()
    {
        var center = new Hex(1, 1);
        var fromEnumerable = Hex.GetHexesInRange(center, 1).ToList();
        var buffer = new List<Hex>();
        Hex.GetHexesInRange(center, 1, buffer);
        Assert.Equal(fromEnumerable.Count, buffer.Count);
        Assert.True(fromEnumerable.All(h => buffer.Contains(h)));
    }

    [Fact]
    public void Equality_SameCoordinates_AreEqual()
    {
        var a = new Hex(5, -3);
        var b = new Hex(5, -3);
        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentCoordinates_AreNotEqual()
    {
        var a = new Hex(1, 0);
        var b = new Hex(0, 1);
        Assert.False(a.Equals(b));
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equality_EqualsObject_ReturnsFalseForNonHex()
    {
        var h = new Hex(0, 0);
        Assert.False(h.Equals("not a hex"));
        Assert.False(h.Equals(null));
    }

    [Fact]
    public void ToString_ReturnsReadableFormat()
    {
        var h = new Hex(2, -1);
        Assert.Equal("Hex(2, -1)", h.ToString());
    }

    [Fact]
    public void ImplicitConversion_FromTuple_Works()
    {
        Hex h = (3, 4);
        Assert.Equal(3, h.Q);
        Assert.Equal(4, h.R);
    }

    [Fact]
    public void ImplicitConversion_ToVector2_Works()
    {
        var h = new Hex(2, -1);
        Vector2 v = h;
        Assert.Equal(2, v.X);
        Assert.Equal(-1, v.Y);
    }
}
