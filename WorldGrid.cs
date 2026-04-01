using System.Runtime.CompilerServices;

namespace EvoVerse;

public class WorldGrid
{
    private readonly Dictionary<Hex, Cell> _cells = new();
    public HexLayout Layout { get; private set; }
    public int MapRadius { get; private set; }
    private List<Hex>? _cachedHexesInRadius;
    private int _cachedRadius;
    public static Hex OriginHex => new(0, 0);

    // Add morphogen cache
    private Dictionary<Hex, Dictionary<string, float>> _morphogenCache = new();

    // Simulation history
    private List<SimulationHistoryState> _history = new();
    private int _currentHistoryIndex = -1;
    private const int MaxHistorySize = 1000; // Maximum number of states to keep in history

    public WorldGrid(HexLayout initialLayout, int mapRadius)
    {
        Layout = initialLayout;
        MapRadius = mapRadius;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsWithinBounds(Hex hex) => hex.Q * hex.Q + hex.Q * hex.R + hex.R * hex.R <= MapRadius * MapRadius;

    public void UpdateLayout(HexLayout newLayout) => Layout = newLayout;

    public void Update()
    {
        MorphogenManager.Update(); // Clear morphogens before this step's emissions
        // Save current state before updating
        SaveCurrentState();
        
        var cellsToRemove = new List<Hex>(_cells.Count / 10); // Estimate 10% might die
        var cellsToDivide = new List<Hex>(_cells.Count); // Estimate all might divide
        List<(string,Hex,int)> emittedMarkers = [];
        var snapshot = _cells.ToArray();

        // Fix: Shuffle iteration order to eliminate update-order bias (Fisher-Yates)
        for (int i = snapshot.Length - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (snapshot[i], snapshot[j]) = (snapshot[j], snapshot[i]);
        }

        for (int i = snapshot.Length - 1; i >= 0; i--)
        {
            var kvp = snapshot[i];
            emittedMarkers.AddRange(kvp.Value.Update(this));
            if (kvp.Value.IsDead)
                cellsToRemove.Add(kvp.Key);
            else if (kvp.Value.ShouldDivide)
                cellsToDivide.Add(kvp.Key);
        }
        // Emit all markers
        foreach (var (marker, hex, range) in emittedMarkers)
        {
            MorphogenManager.Emit(marker, hex, range);
        }

        foreach (var hex in cellsToRemove)
            _cells.Remove(hex);

        foreach (var hex in cellsToDivide)
            GetCell(hex)?.TryDivide(this);
    }

    // Save current state to history
    private void SaveCurrentState()
    {
        var state = new SimulationHistoryState();
        
        // Copy cells
        foreach (var kvp in _cells)
        {
            state.Cells[kvp.Key] = kvp.Value;
        }
        
        // Copy morphogens
        foreach (var kvp in _morphogenCache)
        {
            state.Morphogens[kvp.Key] = new Dictionary<string, float>(kvp.Value);
        }
        
        // Add to history
        _currentHistoryIndex++;
        
        // If we're not at the end of history, remove future states
        if (_currentHistoryIndex < _history.Count)
        {
            _history.RemoveRange(_currentHistoryIndex, _history.Count - _currentHistoryIndex);
        }
        
        _history.Add(state);
        
        // Trim history if too large
        if (_history.Count > MaxHistorySize)
        {
            _history.RemoveAt(0);
            _currentHistoryIndex--;
        }
    }

    // Restore state from history
    public bool RestoreState(int index)
    {
        if (index < 0 || index >= _history.Count)
            return false;
            
        var state = _history[index];
        
        // Clear current state
        _cells.Clear();
        _morphogenCache.Clear();
        
        // Restore cells
        foreach (var kvp in state.Cells)
        {
            _cells[kvp.Key] = kvp.Value;
        }
        
        // Restore morphogens
        foreach (var kvp in state.Morphogens)
        {
            _morphogenCache[kvp.Key] = new Dictionary<string, float>(kvp.Value);
        }
        
        _currentHistoryIndex = index;
        return true;
    }

    // Step back one state
    public bool StepBack()
    {
        if (_currentHistoryIndex <= 0)
            return false;
            
        return RestoreState(_currentHistoryIndex - 1);
    }

    // Step forward one state
    public bool StepForward()
    {
        if (_currentHistoryIndex >= _history.Count - 1)
            return false;
            
        return RestoreState(_currentHistoryIndex + 1);
    }

    // Get current history index
    public int GetCurrentHistoryIndex() => _currentHistoryIndex;
    
    // Get total history size
    public int GetHistorySize() => _history.Count;

    public Cell? GetCell(Hex hex) => _cells.TryGetValue(hex, out var cell) ? cell : null;

    public string GetCellType(Hex hex) => GetCell(hex)?.Type ?? CellTypeRegistry.None;

    public bool IsOccupied(Hex hex) => IsWithinBounds(hex) && _cells.TryGetValue(hex, out _);

    public void PlaceCell(Hex hex, string cellType, Genome? genome = null)
    {
        if (!IsWithinBounds(hex)) return;

        if (string.Equals(cellType, CellTypeRegistry.None, StringComparison.OrdinalIgnoreCase))
            _cells.Remove(hex);
        else
            _cells[hex] = new Cell(cellType, hex, genome ?? _genome);
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

    public List<Cell> GetNeighbors(Hex hex)
    {
        var list = new List<Cell>(6);
        for (int i = 0; i < 6; i++)
        {
            var c = GetCell(hex.Neighbor(i));
            if (c != null) list.Add(c);
        }
        return list;
    }

    public IEnumerable<Hex> GetHexesInRadius()
    {
        if (_cachedHexesInRadius == null || _cachedRadius != MapRadius)
        {
            int radiusSq = MapRadius * MapRadius;
            _cachedHexesInRadius = new List<Hex>();
            for (int q = -MapRadius; q <= MapRadius; q++)
            {
                int r1 = Math.Max(-MapRadius, -q - MapRadius);
                int r2 = Math.Min(MapRadius, -q + MapRadius);
                for (int r = r1; r <= r2; r++)
                {
                    var h = new Hex(q, r);
                    if (h.Q * h.Q + h.Q * h.R + h.R * h.R <= radiusSq)
                        _cachedHexesInRadius.Add(h);
                }
            }
            _cachedRadius = MapRadius;
        }
        return _cachedHexesInRadius;
    }

    private Genome _genome = [];
    public void ClearAndReset()
    {
        // Read the GEL file
        _genome = GEL_Parser.ParseGEL(File.ReadAllText("TEST.GEL"));
        Console.WriteLine(_genome);
        _cells.Clear();
        _morphogenCache.Clear();
        MorphogenManager.Update();
        PlaceCell(OriginHex, CellTypeRegistry.Stem, _genome);
    }

    // Add method to update morphogen cache
    public void UpdateMorphogens()
    {
        _morphogenCache.Clear();
        var affectedHexes = MorphogenManager.GetAffectedHexes().ToArray();
        foreach (var hex in affectedHexes)
        {
            var hexStates = new Dictionary<string, float>();
            foreach (var morphogen in MorphogenManager.Morphogens)
            {
                float strength = MorphogenManager.GetStrengthAtHex(hex, morphogen);
                if (strength > 0)
                {
                    hexStates[morphogen] = strength;
                }
            }
            if (hexStates.Count > 0)
            {
                _morphogenCache[hex] = hexStates;
            }
        }
    }

    // Add method to get morphogen strength from cache
    public float GetMorphogenStrength(Hex hex, string morphogen)
    {
        if (_morphogenCache.TryGetValue(hex, out var hexStates) &&
            hexStates.TryGetValue(morphogen, out var strength))
        {
            return strength;
        }
        return 0f;
    }

    // Add method to get all morphogens at a hex
    public Dictionary<string, float>? GetMorphogensAtHex(Hex hex)
    {
        return _morphogenCache.TryGetValue(hex, out var hexStates) ? hexStates : null;
    }
}