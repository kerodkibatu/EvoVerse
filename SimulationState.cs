using System.Text.Json;
using System.Text.Json.Serialization;

namespace EvoVerse;

public class SimulationHistoryState
{
    // Store cells with their complete state
    public Dictionary<Hex, Cell> Cells { get; set; } = new();
    
    // Store morphogen states
    public Dictionary<Hex, Dictionary<string, float>> Morphogens { get; set; } = new();
    
    // Store cell count history
    public List<int> CellCountHistory { get; set; } = new();

    // Create a deep copy of the state
    public SimulationHistoryState DeepCopy()
    {
        var copy = new SimulationHistoryState();
        
        // Deep copy cells
        foreach (var kvp in Cells)
        {
            // Create a new cell of the same type with the same genome
            var newCell = Cell.CreateCell(kvp.Value.Type, kvp.Key, kvp.Value.Genome);
            if (newCell != null)
            {
                // Copy internal state
                newCell.Clock = kvp.Value.Clock;
                newCell.Timers = new List<(string, int)>(kvp.Value.Timers);
                copy.Cells[kvp.Key] = newCell;
            }
        }
        
        // Deep copy morphogens
        foreach (var kvp in Morphogens)
        {
            copy.Morphogens[kvp.Key] = new Dictionary<string, float>(kvp.Value);
        }
        
        // Copy cell count history
        copy.CellCountHistory = new List<int>(CellCountHistory);
        
        return copy;
    }
} 