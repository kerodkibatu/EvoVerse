namespace EvoVerse;

public enum SimulationState
{
    Paused,  // Simulation logic frozen, can step manually
    Running  // Simulation logic updates automatically
}

public enum MorphogenToolMode
{
    Simple,
    Advanced
}