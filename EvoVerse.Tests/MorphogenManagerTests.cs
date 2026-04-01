using System.Numerics;
using EvoVerse;

namespace EvoVerse.Tests;

[Collection("MorphogenManager")]
public class MorphogenManagerTests
{
    private const string TestMorphogen = "M_Test";

    public MorphogenManagerTests()
    {
        if (!MorphogenManager.Morphogens.Contains(TestMorphogen))
            MorphogenManager.RegisterMorphogen(TestMorphogen);
    }

    private void Cleanup()
    {
        MorphogenManager.UnregisterMorphogen(TestMorphogen);
    }

    [Fact]
    public void Emit_RangeZero_SingleHexGetsConcentration()
    {
        try
        {
            MorphogenManager.Update();
            var hex = new Hex(0, 0);
            MorphogenManager.Emit(TestMorphogen, hex, 0, 0.5f);
            Assert.Equal(0.5f, MorphogenManager.GetStrengthAtHex(hex, TestMorphogen));
        }
        finally { Cleanup(); }
    }

    [Fact]
    public void Emit_RangeZero_MultipleEmitsAccumulate()
    {
        try
        {
            MorphogenManager.Update();
            var hex = new Hex(1, 1);
            MorphogenManager.Emit(TestMorphogen, hex, 0, 0.3f);
            MorphogenManager.Emit(TestMorphogen, hex, 0, 0.2f);
            Assert.Equal(0.5f, MorphogenManager.GetStrengthAtHex(hex, TestMorphogen));
        }
        finally { Cleanup(); }
    }

    [Fact]
    public void Emit_RangeOne_StrengthFallsOffWithDistance()
    {
        try
        {
            MorphogenManager.Update();
            var center = new Hex(0, 0);
            MorphogenManager.Emit(TestMorphogen, center, 1, 1f);

            float centerStrength = MorphogenManager.GetStrengthAtHex(center, TestMorphogen);
            float neighborStrength = MorphogenManager.GetStrengthAtHex(new Hex(1, 0), TestMorphogen);

            Assert.True(centerStrength > neighborStrength);
            Assert.True(centerStrength > 0);
            Assert.True(neighborStrength > 0);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public void Emit_ConcentrationParameterScalesStrength()
    {
        try
        {
            MorphogenManager.Update();
            var hex = new Hex(0, 0);
            MorphogenManager.Emit(TestMorphogen, hex, 0, 0.25f);
            Assert.Equal(0.25f, MorphogenManager.GetStrengthAtHex(hex, TestMorphogen));
        }
        finally { Cleanup(); }
    }

    [Fact]
    public void Subtract_RangeZero_ReducesStrength()
    {
        try
        {
            MorphogenManager.Update();
            var hex = new Hex(0, 0);
            MorphogenManager.Emit(TestMorphogen, hex, 0, 1f);
            MorphogenManager.Subtract(TestMorphogen, hex, 0, 0.4f);
            Assert.Equal(0.6f, MorphogenManager.GetStrengthAtHex(hex, TestMorphogen));
        }
        finally { Cleanup(); }
    }

    [Fact]
    public void Subtract_DoesNotGoBelowZero()
    {
        try
        {
            MorphogenManager.Update();
            var hex = new Hex(0, 0);
            MorphogenManager.Emit(TestMorphogen, hex, 0, 0.3f);
            MorphogenManager.Subtract(TestMorphogen, hex, 0, 1f);
            Assert.Equal(0f, MorphogenManager.GetStrengthAtHex(hex, TestMorphogen));
        }
        finally { Cleanup(); }
    }

    [Fact]
    public void GetStrengthAtHex_UnknownMorphogen_ReturnsZero()
    {
        Assert.Equal(0f, MorphogenManager.GetStrengthAtHex(new Hex(0, 0), "NonExistentMorphogen"));
    }

    [Fact]
    public void GetStrengthAtHex_ClampsToMaxOne()
    {
        try
        {
            MorphogenManager.Update();
            var hex = new Hex(0, 0);
            MorphogenManager.Emit(TestMorphogen, hex, 0, 2f);
            Assert.Equal(1f, MorphogenManager.GetStrengthAtHex(hex, TestMorphogen));
        }
        finally { Cleanup(); }
    }

    [Fact]
    public void Emit_UnregisteredMorphogen_Throws()
    {
        var ex = Assert.Throws<Exception>(() =>
            MorphogenManager.Emit("NeverRegistered", new Hex(0, 0)));
        Assert.Contains("not found", ex.Message);
    }
}
