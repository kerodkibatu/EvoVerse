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

    public class Morphogen
    {
        public string ID { get; set; }
        public int Range { get; set; }
        public Color Color { get; set; }
        public Morphogen(string id, int range = 0, Color? color = null)
        {
            ID = id;
            Range = range;
            Color = color ?? Utils.SampleHSV();
        }
    }
    public class MorphogenManager(WorldGrid worldGrid)
    {
        public HashSet<Morphogen> Morphogens { get; } = [];

        public void RegisterMorphogen(Morphogen morphogen)
        {
            Morphogens.Add(morphogen);
        }
        public void UnregisterMorphogen(Morphogen morphogen)
        {
            Morphogens.Remove(morphogen);
        }
        public List<(Morphogen, Hex)> Sources { get; } = [];
        public void Update()
        {
            Sources.Clear();
        }
        public Morphogen? GetMorphogen(string morphogenID)
        {
            return Morphogens.FirstOrDefault(m => m.ID == morphogenID);
        }
        public void Emit(string morphogenID, Hex hex)
        {
            var morphogen = GetMorphogen(morphogenID);
            if (morphogen == null)
            {
                throw new Exception($"Morphogen with ID {morphogenID} not found");
            }
            Sources.Add((morphogen, hex));
        }
        public float GetStrengthAtHex(Hex hex, string morphogenID)
        {
            float totalStrength = 0.0f;

            foreach (var (sourceMorphogen, sourceHex) in Sources)
            {
                if (sourceMorphogen.ID == morphogenID)
                {
                    int distance = Hex.Distance(sourceHex, hex);
                    int range = sourceMorphogen.Range;


                    if (distance == 0 && range == 0)
                    {
                        totalStrength += 1.0f; // Strength is 1 only at the source if range is 0
                    }
                    else if (distance <= range)
                    {
                        // Strength decreases linearly from 1 at the source to 0 at the max range
                        float strengthContribution = 1.0f - (float)distance / range;
                        totalStrength += strengthContribution;
                    }
                    // If distance > range, the contribution is 0, so no need for an else clause
                }
            }


            return Math.Clamp(totalStrength, 0.0f, 1.0f);
        }
        public IEnumerable<Hex> GetAffectedHexes()
        {
            HashSet<Hex> affectedHexes = [];
            foreach (var (sourceMorphogen, sourceHex) in Sources)
            {
                var hexesInRange = Hex.GetHexesInRange(sourceHex, sourceMorphogen.Range).Where(worldGrid.IsWithinBounds);
                foreach (var hexInRange in hexesInRange)
                {
                    affectedHexes.Add(hexInRange);
                }
            }
            return affectedHexes;
        }
    }
}