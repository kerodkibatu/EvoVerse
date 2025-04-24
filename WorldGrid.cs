using System.Collections.Generic;
using System.Numerics;
using System;
using System.Linq;
using Raylib_cs;
using System.Security.Cryptography.X509Certificates; // Needed for GetAllCells().ToList() if not already present

namespace EvoVerse;

public class WorldGrid
{
    public MorphogenManager MorphogenManager { get; }

    private readonly Dictionary<Hex, Cell> _cells = new Dictionary<Hex, Cell>();
    private HexLayout _layout;
    public HexLayout Layout => _layout;
    public int MapRadius { get; private set; }

    public WorldGrid(HexLayout initialLayout, int mapRadius)
    {
        _layout = initialLayout;
        MapRadius = mapRadius;
        MorphogenManager = new MorphogenManager(this);
    }

    public bool IsWithinBounds(Hex hex)
    {
        return hex.Length() <= MapRadius;
    }

    public void UpdateLayout(HexLayout newLayout)
    {
        _layout = newLayout;
    }

    public void Update()
    {
        // Update morphogens
        MorphogenManager.Update();

        var cells = GetAllCells();
        // Update cells
        foreach (var cell in cells)
        {
            cell.Update(this);
            if (cell.IsDead)
            {
                _cells.Remove(cell.Position);
            }
        }
    }

    public Cell? GetCell(Hex hex)
    {
        // No bounds check here; let caller decide if that's needed.
        // Bounds check happens in IsOccupied, MoveCell, AddCell etc.
        return _cells.TryGetValue(hex, out Cell? cell) ? cell : null;
    }

    public CellType GetCellType(Hex hex)
    {
        // Bounds check is implicit via GetCell returning null if key not found
        // or can be added explicitly if desired: !IsWithinBounds(hex) ? CellType.None : ...
        Cell? cell = GetCell(hex);
        return cell?.Type ?? CellType.None;
    }

    /// <summary>
    /// Checks if a hex is within bounds and contains a cell.
    /// </summary>
    public bool IsOccupied(Hex hex)
    {
        // Combines bounds check and cell existence check
        return IsWithinBounds(hex) && _cells.ContainsKey(hex);
    }

    public void PlaceCell(Hex hex, CellType cellType)
    {
        if (!IsWithinBounds(hex)) return;

        if (cellType == CellType.None)
        {
            _cells.Remove(hex);
        }
        else
        {
            // Overwrite existing cell if present, or add new one
            _cells[hex] = Cell.CreateCell(cellType, hex);
        }
    }

    public bool AddCell(Cell cell)
    {
        if (cell == null || !IsWithinBounds(cell.Position))
        {
            return false; // Invalid cell or out of bounds
        }

        // Use IsOccupied which checks bounds and existence
        if (IsOccupied(cell.Position))
        {
            return false; // Position already occupied
        }

        // Add the cell
        _cells[cell.Position] = cell;
        return true;
    }

    /// <summary>
    /// Moves a cell from its current position to a target hex.
    /// Performs necessary checks and updates the cell's internal position.
    /// </summary>
    /// <param name="currentHex">The hex the cell is currently at.</param>
    /// <param name="targetHex">The empty hex to move the cell to.</param>
    /// <returns>True if the move was successful, false otherwise.</returns>
    public bool MoveCell(Hex currentHex, Hex targetHex)
    {
        // 1. Check bounds for both hexes
        if (!IsWithinBounds(currentHex) || !IsWithinBounds(targetHex))
            return false;

        // 2. Check if there is a cell at the source
        if (!_cells.TryGetValue(currentHex, out Cell? cellToMove))
            return false; // Source hex is empty

        // 3. Check if the target hex is actually empty
        if (_cells.ContainsKey(targetHex)) // Use ContainsKey for direct check
            return false; // Target hex is already occupied

        // --- Execute the move ---
        // 4. Remove cell from the old position in the grid
        _cells.Remove(currentHex);

        // 5. CRITICAL: Update the cell's internal position state
        cellToMove.SetPosition(targetHex);

        // 6. Add the cell to the new position in the grid
        _cells[targetHex] = cellToMove;

        return true; // Move successful
    }


    // --- Remove the old SwapCells method ---
    // public void SwapCells(Hex hex1, Hex hex2) { ... } // REMOVE THIS


    // --- Other methods remain the same ---
    public IReadOnlyCollection<Cell> GetAllCells()
    {
        // Returning Values directly is fine for read-only snapshotting
        return [.. _cells.Values];
    }

    public IEnumerable<Hex> GetAllOccupiedHexes()
    {
        return _cells.Keys;
    }

    public IEnumerable<Hex> GetHexesInRadius()
    {
        // ... (implementation remains the same) ...
        for (int q = -MapRadius; q <= MapRadius; q++)
        {
            int r1 = Math.Max(-MapRadius, -q - MapRadius);
            int r2 = Math.Min(MapRadius, -q + MapRadius);
            for (int r = r1; r <= r2; r++)
            {
                yield return new Hex(q, r);
            }
        }
    }

    /// <summary>
    /// Clears all cells and morphogens from the grid and places a single stem cell at the center.
    /// </summary>
    public void ClearAndReset()
    {
        _cells.Clear();
        MorphogenManager.Update();
        PlaceCell(new Hex(0, 0), CellType.Stem);
    }
}