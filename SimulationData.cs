// In SimulationData.cs or similar
using Raylib_cs;

namespace EvoVerse
{

    public enum SimulationState
    {
        Editing, // Manual cell placement/removal enabled
        Paused,  // Simulation logic frozen, can step manually
        Running  // Simulation logic updates automatically
    }

    public record Morphogen(string Name, Color Color){
        public override string ToString() => Name;
    }
}