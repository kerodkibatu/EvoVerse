using Raylib_cs;
using System.Collections.Generic;
using System.Numerics;

namespace EvoVerse;

public struct Morphogen
{
    public string ID { get; }
    public int Range { get; }

    public Morphogen(string id, int range = 0)
    {
        ID = id;
        Range = range;
    }
}

public static class MorphogenManager
{
    public static HashSet<string> Morphogens { get; } = new();
    private static Dictionary<string, Dictionary<Hex, float>> morphogenStrengthMap = new();

    public static void RegisterMorphogen(string morphogenID)
    {
        Morphogens.Add(morphogenID);
        morphogenStrengthMap[morphogenID] = new Dictionary<Hex, float>();
    }

    public static void UnregisterMorphogen(string morphogenID)
    {
        Morphogens.Remove(morphogenID);
        morphogenStrengthMap.Remove(morphogenID);
    }

    public static void Update()
    {
        foreach (var key in morphogenStrengthMap.Keys.ToList())
        {
            morphogenStrengthMap[key].Clear();
        }
    }

    public static void Emit(string morphogenID, Hex hex, int range = 0)
    {
        if (!Morphogens.Contains(morphogenID))
            throw new System.Exception($"Morphogen with ID {morphogenID} not found");

        IEnumerable<Hex> hexesInRange = range == 0
            ? [hex]
            : Hex.GetHexesInRange(hex, range);

        if (!morphogenStrengthMap.TryGetValue(morphogenID, out var strengthDict))
        {
            strengthDict = [];
            morphogenStrengthMap[morphogenID] = strengthDict;
        }

        foreach (var h in hexesInRange)
        {
            float strength = CalculateStrength(hex, h, range);
            strengthDict.TryGetValue(h, out float current);
            strengthDict[h] = current + strength;
        }
    }

    private static float CalculateStrength(Hex sourceHex, Hex targetHex, int range)
    {
        if (range == 0)
            return 1f;

        int distance = sourceHex.Distance(targetHex);
        // Adjust the strength calculation to account for the new range definition
        return distance <= range ? 1f - (distance / (float)(range + 1)) : 0f;
    }

    public static float GetStrengthAtHex(Hex hex, string morphogenID)
    {
        if (!Morphogens.Contains(morphogenID))
            return 0f;

        if (morphogenStrengthMap.TryGetValue(morphogenID, out var strengthDict) &&
            strengthDict.TryGetValue(hex, out float strength))
        {
            return System.Math.Clamp(strength, 0f, 1f);
        }

        return 0f;
    }

    /// <summary>
    /// Calculates the gradient direction of a specific morphogen at a given hex.
    /// </summary>
    /// <param name="hex">The center hex to sample the gradient at</param>
    /// <param name="morphogenID">The ID of the morphogen to sample</param>
    /// <param name="samplingDistance">Optional: The distance to sample around the hex (default: 1)</param>
    /// <param name="towardHigher">Optional: If true, points toward higher concentration; if false, toward lower (default: true)</param>
    /// <returns>A hex pointing in the direction of concentration gradient, or the original hex if no gradient</returns>
    public static Hex GetGradientAtHex(Hex hex, string morphogenID, int samplingDistance = 1, bool towardHigher = true)
    {
        if (!Morphogens.Contains(morphogenID))
            return hex;

        // Get the concentration at the center hex
        float centerConcentration = GetStrengthAtHex(hex, morphogenID);

        // Array to store concentration differences for each direction
        float[] directionStrengths = new float[6];

        // Sample all neighbors within the sweep distance
        for (int i = 0; i < 6; i++)
        {
            float totalDiff = 0f;
            int sampleCount = 0;

            // Sample hexes in the direction for the given sweep distance
            for (int d = 1; d <= samplingDistance; d++)
            {
                Hex sampleHex = hex;
                for (int j = 0; j < d; j++)
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

                totalDiff += diff;
                sampleCount++;
            }

            // Average the difference for this direction
            directionStrengths[i] = sampleCount > 0 ? totalDiff / sampleCount : 0f;
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
    public static Vector2 GetGradientVectorAtHex(Hex hex, string morphogenID, int samplingDistance = 1, bool towardHigher = true)
    {
        if (!Morphogens.Contains(morphogenID))
            return Vector2.Zero;

        // Initialize gradient vector
        Vector2 gradientVector = Vector2.Zero;

        // Sample all neighbors at the sampling distance
        for (int i = 0; i < 6; i++)
        {
            // Initialize total concentration and count for averaging
            float totalConcentration = 0f;
            int sampleCount = 0;

            // Sweep area in this direction
            for (int d = 1; d <= samplingDistance; d++)
            {
                Hex sampleHex = hex.Neighbor(i).Neighbor(i); // Get the hex in this direction at the current distance
                float sampleConcentration = GetStrengthAtHex(sampleHex, morphogenID);

                // Accumulate concentration
                totalConcentration += sampleConcentration;
                sampleCount++;
            }

            // Calculate average concentration for this direction
            float averageConcentration = sampleCount > 0 ? totalConcentration / sampleCount : 0f;

            // Get the concentration at the center hex
            float centerConcentration = GetStrengthAtHex(hex, morphogenID);

            // Calculate difference (gradient) in this direction
            float diff = averageConcentration - centerConcentration;

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

    public static IEnumerable<Hex> GetAffectedHexes()
    {
        var seen = new HashSet<Hex>();

        foreach (var kvp in morphogenStrengthMap)
        {
            foreach (var hex in kvp.Value.Keys)
            {
                if (seen.Add(hex))
                {
                    yield return hex;
                }
            }
        }
    }

}