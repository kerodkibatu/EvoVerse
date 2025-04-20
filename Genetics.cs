using System;
using System.Collections.Generic;
using System.Linq;
using Raylib_cs;

namespace EvoVerse
{
    /// <summary>
    /// Represents a morphogen type that can diffuse through the environment
    /// </summary>
    public enum MorphogenType
    {
        Activator,      // Promotes cell growth and division
        Inhibitor,      // Inhibits cell growth and division
        Differentiation, // Controls cell type differentiation
        Positional,     // Provides positional information
        Adhesion        // Controls cell adhesion properties
    }

    /// <summary>
    /// Represents a gene that can be expressed in a cell
    /// </summary>
    public class Gene
    {
        public string Name { get; }
        public float ExpressionLevel { get; set; }
        public float BaseExpression { get; set; }
        public Dictionary<MorphogenType, float> MorphogenSensitivity { get; }
        public Dictionary<MorphogenType, float> MorphogenProduction { get; }

        public Gene(string name, float baseExpression)
        {
            Name = name;
            BaseExpression = baseExpression;
            ExpressionLevel = baseExpression;
            MorphogenSensitivity = new Dictionary<MorphogenType, float>();
            MorphogenProduction = new Dictionary<MorphogenType, float>();
        }

        public void UpdateExpression(Dictionary<MorphogenType, float> localMorphogenLevels)
        {
            float totalEffect = 0f;
            foreach (var (morphogen, level) in localMorphogenLevels)
            {
                if (MorphogenSensitivity.TryGetValue(morphogen, out float sensitivity))
                {
                    totalEffect += level * sensitivity;
                }
            }
            ExpressionLevel = Math.Clamp(BaseExpression + totalEffect, 0f, 1f);
        }
    }

    /// <summary>
    /// Represents the genome of a cell, containing all its genes
    /// </summary>
    public class Genome
    {
        public Dictionary<string, Gene> Genes { get; }
        public float MutationRate { get; set; }

        public Genome(float mutationRate = 0.01f)
        {
            Genes = new Dictionary<string, Gene>();
            MutationRate = mutationRate;
        }

        public void AddGene(Gene gene)
        {
            Genes[gene.Name] = gene;
        }

        public void UpdateExpression(Dictionary<MorphogenType, float> localMorphogenLevels)
        {
            foreach (var gene in Genes.Values)
            {
                gene.UpdateExpression(localMorphogenLevels);
            }
        }

        public Genome Clone()
        {
            var clone = new Genome(MutationRate);
            foreach (var gene in Genes.Values)
            {
                var geneClone = new Gene(gene.Name, gene.BaseExpression);
                foreach (var (morphogen, sensitivity) in gene.MorphogenSensitivity)
                {
                    geneClone.MorphogenSensitivity[morphogen] = sensitivity;
                }
                foreach (var (morphogen, production) in gene.MorphogenProduction)
                {
                    geneClone.MorphogenProduction[morphogen] = production;
                }
                clone.AddGene(geneClone);
            }
            return clone;
        }

        public void Mutate()
        {
            foreach (var gene in Genes.Values)
            {
                if (Random.Shared.NextSingle() < MutationRate)
                {
                    // Mutate base expression
                    gene.BaseExpression += (Random.Shared.NextSingle() - 0.5f) * 0.1f;
                    gene.BaseExpression = Math.Clamp(gene.BaseExpression, 0f, 1f);

                    // Mutate morphogen sensitivities
                    foreach (var morphogen in Enum.GetValues<MorphogenType>())
                    {
                        if (Random.Shared.NextSingle() < MutationRate)
                        {
                            if (!gene.MorphogenSensitivity.ContainsKey(morphogen))
                                gene.MorphogenSensitivity[morphogen] = 0f;
                            gene.MorphogenSensitivity[morphogen] += (Random.Shared.NextSingle() - 0.5f) * 0.1f;
                        }
                    }

                    // Mutate morphogen production
                    foreach (var morphogen in Enum.GetValues<MorphogenType>())
                    {
                        if (Random.Shared.NextSingle() < MutationRate)
                        {
                            if (!gene.MorphogenProduction.ContainsKey(morphogen))
                                gene.MorphogenProduction[morphogen] = 0f;
                            gene.MorphogenProduction[morphogen] += (Random.Shared.NextSingle() - 0.5f) * 0.1f;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Represents the morphogen field in the environment
    /// </summary>
    public class MorphogenField
    {
        private readonly Dictionary<MorphogenType, float[,]> _fields;
        private readonly int _size;
        private readonly float _diffusionRate;
        private readonly float _decayRate;

        public MorphogenField(int size, float diffusionRate = 0.1f, float decayRate = 0.05f)
        {
            _size = size;
            _diffusionRate = diffusionRate;
            _decayRate = decayRate;
            _fields = new Dictionary<MorphogenType, float[,]>();

            foreach (MorphogenType type in Enum.GetValues<MorphogenType>())
            {
                _fields[type] = new float[size, size];
            }
        }

        public void Update()
        {
            foreach (var field in _fields.Values)
            {
                Diffuse(field);
                Decay(field);
            }
        }

        private void Diffuse(float[,] field)
        {
            float[,] newField = new float[_size, _size];
            Array.Copy(field, newField, field.Length);

            for (int x = 1; x < _size - 1; x++)
            {
                for (int y = 1; y < _size - 1; y++)
                {
                    float sum = 0f;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            sum += field[x + dx, y + dy];
                        }
                    }
                    newField[x, y] = field[x, y] * (1 - _diffusionRate) + (sum / 9) * _diffusionRate;
                }
            }

            Array.Copy(newField, field, field.Length);
        }

        private void Decay(float[,] field)
        {
            for (int x = 0; x < _size; x++)
            {
                for (int y = 0; y < _size; y++)
                {
                    field[x, y] *= (1 - _decayRate);
                }
            }
        }

        public void AddMorphogen(MorphogenType type, int x, int y, float amount)
        {
            if (x >= 0 && x < _size && y >= 0 && y < _size)
            {
                _fields[type][x, y] += amount;
            }
        }

        public float GetMorphogenLevel(MorphogenType type, int x, int y)
        {
            if (x >= 0 && x < _size && y >= 0 && y < _size)
            {
                return _fields[type][x, y];
            }
            return 0f;
        }

        public Dictionary<MorphogenType, float> GetLocalMorphogenLevels(int x, int y)
        {
            var levels = new Dictionary<MorphogenType, float>();
            foreach (var type in Enum.GetValues<MorphogenType>())
            {
                levels[type] = GetMorphogenLevel(type, x, y);
            }
            return levels;
        }
    }
} 