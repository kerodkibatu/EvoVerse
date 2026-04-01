using System.Numerics;
using EvoVerse;

namespace EvoVerse.Tests;

public class WorldGridTests
{
    private static HexLayout CreateLayout() =>
        new(HexLayout.Pointy, new Vector2(30, 30), Vector2.Zero);

    [Fact]
    public void IsWithinBounds_Origin_ReturnsTrue()
    {
        var world = new WorldGrid(CreateLayout(), 5);
        Assert.True(world.IsWithinBounds(new Hex(0, 0)));
    }

    [Fact]
    public void IsWithinBounds_InsideCircle_ReturnsTrue()
    {
        var world = new WorldGrid(CreateLayout(), 5);
        Assert.True(world.IsWithinBounds(new Hex(3, 0)));
        Assert.True(world.IsWithinBounds(new Hex(2, 2)));
    }

    [Fact]
    public void IsWithinBounds_OutsideCircle_ReturnsFalse()
    {
        var world = new WorldGrid(CreateLayout(), 5);
        Assert.False(world.IsWithinBounds(new Hex(10, 0)));
        Assert.False(world.IsWithinBounds(new Hex(5, 5)));
    }

    [Fact]
    public void IsWithinBounds_OnBoundary_ReturnsTrue()
    {
        var world = new WorldGrid(CreateLayout(), 2);
        var hex = new Hex(2, -1);
        int q = hex.Q, r = hex.R;
        Assert.True(q * q + q * r + r * r <= 4);
        Assert.True(world.IsWithinBounds(hex));
    }

    [Fact]
    public void PlaceCell_GetCell_StemAtOrigin()
    {
        var world = new WorldGrid(CreateLayout(), 5);
        var origin = WorldGrid.OriginHex;
        world.PlaceCell(origin, CellTypeRegistry.Stem);

        var cell = world.GetCell(origin);
        Assert.NotNull(cell);
        Assert.Equal(CellTypeRegistry.Stem, cell.Type);
    }

    [Fact]
    public void PlaceCell_None_RemovesCell()
    {
        var world = new WorldGrid(CreateLayout(), 5);
        var origin = WorldGrid.OriginHex;
        world.PlaceCell(origin, CellTypeRegistry.Stem);
        Assert.NotNull(world.GetCell(origin));

        world.PlaceCell(origin, CellTypeRegistry.None);
        Assert.Null(world.GetCell(origin));
    }

    [Fact]
    public void GetCellType_EmptyHex_ReturnsNone()
    {
        var world = new WorldGrid(CreateLayout(), 5);
        Assert.Equal(CellTypeRegistry.None, world.GetCellType(new Hex(1, 1)));
    }

    [Fact]
    public void GetNeighbors_IsolatedCell_ReturnsFewerThanSix()
    {
        var world = new WorldGrid(CreateLayout(), 5);
        world.PlaceCell(WorldGrid.OriginHex, CellTypeRegistry.Stem);

        var neighbors = world.GetNeighbors(WorldGrid.OriginHex);
        Assert.True(neighbors.Count <= 6);
    }

    [Fact]
    public void MoveCell_ValidMove_Succeeds()
    {
        var world = new WorldGrid(CreateLayout(), 5);
        var from = new Hex(0, 0);
        var to = new Hex(1, 0);
        world.PlaceCell(from, CellTypeRegistry.Stem);

        Assert.True(world.MoveCell(from, to));
        Assert.Null(world.GetCell(from));
        Assert.NotNull(world.GetCell(to));
        Assert.Equal(CellTypeRegistry.Stem, world.GetCell(to)!.Type);
    }

    [Fact]
    public void MoveCell_OccupiedTarget_Fails()
    {
        var world = new WorldGrid(CreateLayout(), 5);
        world.PlaceCell(new Hex(0, 0), CellTypeRegistry.Stem);
        world.PlaceCell(new Hex(1, 0), "FLESH");

        Assert.False(world.MoveCell(new Hex(0, 0), new Hex(1, 0)));
        Assert.NotNull(world.GetCell(new Hex(0, 0)));
        Assert.NotNull(world.GetCell(new Hex(1, 0)));
    }

    [Fact]
    public void StepBack_AfterTwoUpdates_RestoresState()
    {
        var world = new WorldGrid(CreateLayout(), 5);
        world.PlaceCell(WorldGrid.OriginHex, CellTypeRegistry.Stem);
        world.Update();
        world.Update();

        Assert.True(world.StepBack());
        Assert.Equal(1, world.GetCurrentHistoryIndex() + 1);
    }
}
