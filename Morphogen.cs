using Raylib_cs;
using System.Collections.Generic;
using System.Numerics;

namespace EvoVerse;

public class Morphogen
{
    public string ID { get; }
    public int Range { get; }

    public Morphogen(string id, int range = 0)
    {
        ID = id;
        Range = range;
    }
}

public class MorphogenManager
{
    private readonly Dictionary<string, Morphogen> _morphogenDict = new();
    private readonly List<(Morphogen, Hex)> _sourcesPool = new();
    
    public HashSet<Morphogen> Morphogens { get; } = new();
    public List<(Morphogen, Hex)> Sources { get; } = new();

    public void RegisterMorphogen(Morphogen morphogen)
    {
        Morphogens.Add(morphogen);
        _morphogenDict[morphogen.ID] = morphogen;
    }

    public void UnregisterMorphogen(Morphogen morphogen)
    {
        Morphogens.Remove(morphogen);
        _morphogenDict.Remove(morphogen.ID);
    }

    public void Update()
    {
        // Recycle sources for next frame
        foreach (var item in Sources)
        {
            _sourcesPool.Add(item);
        }
        Sources.Clear();
    }

    public Morphogen? GetMorphogen(string morphogenID)
    {
        return _morphogenDict.TryGetValue(morphogenID, out var morphogen) ? morphogen : null;
    }

    public void Emit(string morphogenID, Hex hex)
    {
        if (!_morphogenDict.TryGetValue(morphogenID, out var morphogen))
            throw new Exception($"Morphogen with ID {morphogenID} not found");

        if (_sourcesPool.Count > 0)
        {
            var last = _sourcesPool[^1];
            _sourcesPool.RemoveAt(_sourcesPool.Count - 1);
            last.Item1 = morphogen;
            last.Item2 = hex;
            Sources.Add(last);
        }
        else
        {
            Sources.Add((morphogen, hex));
        }
    }

    public float GetStrengthAtHex(Hex hex, string morphogenID)
    {
        if (!_morphogenDict.TryGetValue(morphogenID, out var targetMorphogen))
            return 0f;

        float totalStrength = 0f;
        
        foreach (var (sourceMorphogen, sourceHex) in Sources)
        {
            if (sourceMorphogen != targetMorphogen) continue;
            
            int distance = sourceHex.Distance(hex);
            int range = sourceMorphogen.Range;

            if (distance == 0)
                return 1f;
            if (distance <= range)
                totalStrength += 1 - MathF.Log10(distance + 1) / MathF.Log10(range + 1);
        }

        return Math.Clamp(totalStrength, 0f, 1f);
    }

    /// <summary>
    /// Calculates the gradient direction of a specific morphogen at a given hex.
    /// </summary>
    /// <param name="hex">The center hex to sample the gradient at</param>
    /// <param name="morphogenID">The ID of the morphogen to sample</param>
    /// <param name="samplingDistance">Optional: The distance to sample around the hex (default: 1)</param>
    /// <param name="towardHigher">Optional: If true, points toward higher concentration; if false, toward lower (default: true)</param>
    /// <returns>A hex pointing in the direction of concentration gradient, or the original hex if no gradient</returns>
    public Hex GetGradientAtHex(Hex hex, string morphogenID, int samplingDistance = 1, bool towardHigher = true)
    {

        if (!_morphogenDict.TryGetValue(morphogenID, out _))
            return hex;
            
        // Get the concentration at the center hex
        float centerConcentration = GetStrengthAtHex(hex, morphogenID);
        
        // Array to store concentration differences for each direction
        float[] directionStrengths = new float[6];
        
        // Sample all neighbors at the sampling distance
        for (int i = 0; i < 6; i++)
        {
            // Get the hex in this direction at the sampling distance
            Hex sampleHex = hex;
            for (int d = 0; d < samplingDistance; d++)
            {
                sampleHex = sampleHex.Neighbor(i);
            }
            
            // Get concentration at this sample point
            float sampleConcentration = GetStrengthAtHex(sampleHex, morphogenID);
            
            // Calculate difference (gradient) in this direction
            float diff = sampleConcentration - centerConcentration;
            
            // Invert the difference if we want to point toward lower concentration
            if (!towardHigher)
                diff = -diff;
                
            directionStrengths[i] = diff;
        }
        
        // Find the direction with the maximum strength
        int strongestDirection = -1;
        float maxStrength = float.Epsilon; // Use epsilon to ignore very small differences
        
        for (int i = 0; i < 6; i++)
        {
            if (directionStrengths[i] > maxStrength)
            {
                maxStrength = directionStrengths[i];
                strongestDirection = i;
            }
        }
        
        if (strongestDirection == -1)
            return hex;

        return hex.Neighbor(strongestDirection);
    }

    /// <summary>
    /// Calculates the gradient direction of a specific morphogen at a given hex.
    /// </summary>
    /// <param name="hex">The center hex to sample the gradient at</param>
    /// <param name="morphogenID">The ID of the morphogen to sample</param>
    /// <param name="samplingDistance">Optional: The distance to sample around the hex (default: 1)</param>
    /// <param name="towardHigher">Optional: If true, points toward higher concentration; if false, toward lower (default: true)</param>
    /// <returns>A normalized Vector2 pointing in the direction of concentration gradient, or Vector2.Zero if no gradient</returns>
    public Vector2 GetGradientVectorAtHex(Hex hex, string morphogenID, int samplingDistance = 1, bool towardHigher = true)
    {
        if (!_morphogenDict.TryGetValue(morphogenID, out var targetMorphogen))
            return Vector2.Zero;
            
        // Get the concentration at the center hex
        float centerConcentration = GetStrengthAtHex(hex, morphogenID);
        
        // Initialize gradient vector
        Vector2 gradientVector = Vector2.Zero;
        
        // Sample all neighbors at the sampling distance
        for (int i = 0; i < 6; i++)
        {
            // Get the hex in this direction at the sampling distance
            Hex sampleHex = hex;
            for (int d = 0; d < samplingDistance; d++)
            {
                sampleHex = sampleHex.Neighbor(i);
            }
            
            // Get concentration at this sample point
            float sampleConcentration = GetStrengthAtHex(sampleHex, morphogenID);
            
            // Calculate difference (gradient) in this direction
            float diff = sampleConcentration - centerConcentration;
            
            // Invert the difference if we want to point toward lower concentration
            if (!towardHigher)
                diff = -diff;
            
            // If there's a difference, add a vector in this direction proportional to the difference
            if (Math.Abs(diff) > float.Epsilon)
            {
                // Get the vector pointing in this direction (convert hex direction to angle)
                float angle = (float)(i * Math.PI / 3); // 60 degrees per direction
                Vector2 directionVector = new(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                );
                
                // Add to the gradient vector, weighted by the concentration difference
                gradientVector += directionVector * diff;
            }
        }
        
        // Normalize the gradient vector (if it's not zero)
        if (gradientVector != Vector2.Zero)
        {
            return Vector2.Normalize(gradientVector);
        }
        
        return Vector2.Zero;
    }

    public IEnumerable<Hex> GetAffectedHexes()
    {
        var seen = new HashSet<Hex>();
        foreach (var (sourceMorphogen, sourceHex) in Sources)
        {
            foreach (var hex in Hex.GetHexesInRange(sourceHex, sourceMorphogen.Range))
            {
                if (seen.Add(hex))
                    yield return hex;
            }
        }
    }
}