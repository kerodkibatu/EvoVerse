// In SimulationData.cs or similar
namespace EvoVerse;

public enum SimulationState
{
    Editing, // Manual cell placement/removal enabled
    Paused,  // Simulation logic frozen, can step manually
    Running  // Simulation logic updates automatically
}