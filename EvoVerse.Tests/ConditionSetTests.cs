using System.Numerics;
using EvoVerse;

namespace EvoVerse.Tests;

[Collection("ConditionSet")]
public class ConditionSetTests
{
    private const string TestMorphogen = "M_CondTest";

    private static HexLayout CreateLayout() =>
        new(HexLayout.Pointy, new Vector2(30, 30), Vector2.Zero);

    [Fact]
    public void Evaluate_SelfTypeMatch_ReturnsTrue()
    {
        try
        {
            MorphogenManager.RegisterMorphogen(TestMorphogen);
            var world = new WorldGrid(CreateLayout(), 5);
            world.PlaceCell(WorldGrid.OriginHex, CellTypeRegistry.Stem);

            var cell = world.GetCell(WorldGrid.OriginHex)!;
            var cs = new ConditionSet();
            cs.SelfTypeChecks.Add(CellTypeRegistry.Stem);

            Assert.True(cs.Evaluate(world, cell));
        }
        finally
        {
            MorphogenManager.UnregisterMorphogen(TestMorphogen);
        }
    }

    [Fact]
    public void Evaluate_SelfTypeMismatch_ReturnsFalse()
    {
        var world = new WorldGrid(CreateLayout(), 5);
        world.PlaceCell(WorldGrid.OriginHex, CellTypeRegistry.Stem);

        var cell = world.GetCell(WorldGrid.OriginHex)!;
        var cs = new ConditionSet();
        cs.SelfTypeChecks.Add("FLESH");

        Assert.False(cs.Evaluate(world, cell));
    }

    [Fact]
    public void Evaluate_ActiveMarkerPresent_ReturnsTrue()
    {
        try
        {
            MorphogenManager.RegisterMorphogen(TestMorphogen);
            MorphogenManager.Update();
            MorphogenManager.Emit(TestMorphogen, WorldGrid.OriginHex, 0, 0.5f);

            var world = new WorldGrid(CreateLayout(), 5);
            world.PlaceCell(WorldGrid.OriginHex, CellTypeRegistry.Stem);
            world.UpdateMorphogens();

            var cell = world.GetCell(WorldGrid.OriginHex)!;
            var cs = new ConditionSet();
            cs.ActiveMarkers.Add(new MarkerCondition(TestMorphogen));

            Assert.True(cs.Evaluate(world, cell));
        }
        finally
        {
            MorphogenManager.UnregisterMorphogen(TestMorphogen);
        }
    }

    [Fact]
    public void Evaluate_ActiveMarkerAbsent_ReturnsFalse()
    {
        try
        {
            MorphogenManager.RegisterMorphogen(TestMorphogen);
            MorphogenManager.Update();

            var world = new WorldGrid(CreateLayout(), 5);
            world.PlaceCell(WorldGrid.OriginHex, CellTypeRegistry.Stem);
            world.UpdateMorphogens();

            var cell = world.GetCell(WorldGrid.OriginHex)!;
            var cs = new ConditionSet();
            cs.ActiveMarkers.Add(new MarkerCondition(TestMorphogen));

            Assert.False(cs.Evaluate(world, cell));
        }
        finally
        {
            MorphogenManager.UnregisterMorphogen(TestMorphogen);
        }
    }

    [Fact]
    public void Evaluate_ClockCondition_ReturnsCorrectly()
    {
        var world = new WorldGrid(CreateLayout(), 5);
        world.PlaceCell(WorldGrid.OriginHex, CellTypeRegistry.Stem);
        var cell = world.GetCell(WorldGrid.OriginHex)!;

        var cs = new ConditionSet();
        cs.ClockConditions[2] = ComparisonType.GreaterThan;

        cell.Clock = 3;
        Assert.True(cs.Evaluate(world, cell));

        cell.Clock = 1;
        Assert.False(cs.Evaluate(world, cell));
    }

    [Fact]
    public void Evaluate_NeighborCount_IsolatedCell_MeetsZero()
    {
        var world = new WorldGrid(CreateLayout(), 5);
        world.PlaceCell(WorldGrid.OriginHex, CellTypeRegistry.Stem);

        var cell = world.GetCell(WorldGrid.OriginHex)!;
        var cs = new ConditionSet();
        cs.NeighborConditions[0] = ComparisonType.Equals;

        Assert.True(cs.Evaluate(world, cell));
    }
}
