using Raylib_cs;
using System.Collections.Generic;

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

            if (distance == 0 && range == 0)
                return 1f;
            
            if (distance <= range)
                totalStrength += 1 - MathF.Log10(distance + 1) / MathF.Log10(range + 1);
        }

        return Math.Clamp(totalStrength, 0f, 1f);
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