using System.Runtime.CompilerServices;

namespace EvoVerse;

public class WorldGrid
{
    public MorphogenManager MorphogenManager { get; }
    private readonly Dictionary<Hex, Cell> _cells = new();
    public HexLayout Layout { get; private set; }
    public int MapRadius { get; private set; }
    private List<Hex>? _cachedHexesInRadius;
    private int _cachedRadius;
    private static readonly Hex OriginHex = new(0, 0);

    public WorldGrid(HexLayout initialLayout, int mapRadius)
    {
        Layout = initialLayout;
        MapRadius = mapRadius;
        MorphogenManager = new MorphogenManager();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsWithinBounds(Hex hex) => hex.Length() <= MapRadius;

    public void UpdateLayout(HexLayout newLayout) => Layout = newLayout;

    public void Update()
    {
        MorphogenManager.Update();
        
        var cellsToRemove = new List<Hex>(_cells.Count / 10); // Estimate 10% might die
        foreach (var kvp in _cells)
        {
            kvp.Value.Update(this);
            if (kvp.Value.IsDead)
                cellsToRemove.Add(kvp.Key);
        }
        
        foreach (var hex in cellsToRemove)
            _cells.Remove(hex);
    }

    public Cell? GetCell(Hex hex) => _cells.TryGetValue(hex, out var cell) ? cell : null;

    public CellType GetCellType(Hex hex) => GetCell(hex)?.Type ?? CellType.None;

    public bool IsOccupied(Hex hex) => IsWithinBounds(hex) && _cells.TryGetValue(hex, out _);

    public void PlaceCell(Hex hex, CellType cellType)
    {
        if (!IsWithinBounds(hex)) return;

        if (cellType == CellType.None)
            _cells.Remove(hex);
        else
            _cells[hex] = Cell.CreateCell(cellType, hex);
    }

    public bool AddCell(Cell cell)
    {
        if (cell == null || !IsWithinBounds(cell.Position) || IsOccupied(cell.Position))
            return false;

        _cells[cell.Position] = cell;
        return true;
    }

    public bool MoveCell(Hex currentHex, Hex targetHex)
    {
        if (!IsWithinBounds(currentHex) || !IsWithinBounds(targetHex) || 
            !_cells.TryGetValue(currentHex, out var cellToMove) || 
            _cells.ContainsKey(targetHex))
            return false;

        _cells.Remove(currentHex);
        cellToMove.SetPosition(targetHex);
        _cells[targetHex] = cellToMove;
        return true;
    }

    public IEnumerable<Cell> GetAllCells() => _cells.Values;
    public IEnumerable<Hex> GetAllOccupiedHexes() => _cells.Keys;

    public IEnumerable<Hex> GetHexesInRadius()
    {
        if (_cachedHexesInRadius == null || _cachedRadius != MapRadius)
        {
            _cachedHexesInRadius = new List<Hex>();
            for (int q = -MapRadius; q <= MapRadius; q++)
            {
                int r1 = Math.Max(-MapRadius, -q - MapRadius);
                int r2 = Math.Min(MapRadius, -q + MapRadius);
                for (int r = r1; r <= r2; r++)
                    _cachedHexesInRadius.Add(new Hex(q, r));
            }
            _cachedRadius = MapRadius;
        }
        return _cachedHexesInRadius;
    }

    public void ClearAndReset()
    {
        _cells.Clear();
        MorphogenManager.Update();
        PlaceCell(OriginHex, CellType.Stem);
    }
}