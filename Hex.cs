using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace EvoVerse
{
    /// <summary>
    /// Represents a Hexagon coordinate using the axial coordinate system (q, r).
    /// See https://www.redblobgames.com/grids/hexagons/ for a great reference.
    /// </summary>
    public readonly struct Hex(int q, int r) : IEquatable<Hex>
    {
        public readonly int Q = q;
        public readonly int R = r;

        public override bool Equals(object? obj) => obj is Hex hex && Equals(hex);

        public bool Equals(Hex other) => Q == other.Q && R == other.R;

        public override int GetHashCode() => HashCode.Combine(Q, R);

        public static bool operator ==(Hex left, Hex right) => left.Equals(right);

        public static bool operator !=(Hex left, Hex right) => !(left == right);

        public override string ToString() => $"Hex({Q}, {R})";

        // Basic Hex arithmetic (can be added later as needed)
        public static Hex operator +(Hex a, Hex b) => new(a.Q + b.Q, a.R + b.R);
        public static Hex operator -(Hex a, Hex b) => new(a.Q - b.Q, a.R - b.R);
        public static Hex operator *(Hex a, int k) => new(a.Q * k, a.R * k);
        public static Hex operator /(Hex a, int k) => new(a.Q / k, a.R / k);

        public static Hex Add(Hex a, Hex b) => a + b;
        public static Hex Subtract(Hex a, Hex b) => a - b;
        public static Hex Scale(Hex a, int k) => a * k;
        public static Hex Divide(Hex a, int k) => a / k;

        // Implict conversion from Hex to Vector2
        public static implicit operator Vector2(Hex hex) => new(hex.Q, hex.R);
        public static implicit operator Hex(Vector2 v) => new((int)v.X, (int)v.Y);
        public static implicit operator (int, int)(Hex hex) => (hex.Q, hex.R);
        public static implicit operator Hex((int, int) tuple) => new(tuple.Item1, tuple.Item2);


        private static readonly Hex[] directions =
        {
            (1, 0), (1, -1), (0, -1),
            (-1, 0), (-1, 1), (0, 1)
        };

        public Hex Direction(int direction) => directions[direction % 6];

        public Hex Neighbor(int direction) => Add(this, Direction(direction));

        public IEnumerable<Hex> Neighbors()
        {
            for (int i = 0; i < 6; i++)
            {
                yield return Neighbor(i);
            }
        }

        /// <summary>
        /// Calculates the distance from this hex to the origin (0,0).
        /// Uses the standard hex grid distance formula.
        /// </summary>
        public int Length()
        {
            // Since s = -q - r, the distance is max(|q|, |r|, |s|)
            return (Math.Abs(Q) + Math.Abs(R) + Math.Abs(-Q - R)) / 2;
        }

        public int Distance(Hex other)
        {
            return (Math.Abs(Q - other.Q) + Math.Abs(R - other.R) + Math.Abs(-Q - R - (-other.Q - other.R))) / 2;
        }

        public static IEnumerable<Hex> GetHexesInRange(Hex hex, int range)
        {
            for (int q = -range + hex.Q; q <= range + hex.Q; q++)
            {
                for (int r = Math.Max(-range + hex.R, -q - range + hex.Q); r <= Math.Min(range + hex.R, -q + range + hex.Q); r++)
                {
                    yield return new Hex(q, r);
                }
            }
        }

        public static int Distance(Hex sourceHex, Hex hex)
        {
            return sourceHex.Distance(hex);
        }
    }

    /// <summary>
    /// Represents the orientation of the hex grid (pointy-top or flat-top).
    /// Stores forward and inverse matrices for conversion calculations.
    /// </summary>
    public readonly struct Orientation
    {
        public readonly float F0, F1, F2, F3; // Forward matrix elements
        public readonly float B0, B1, B2, B3; // Inverse matrix elements
        public readonly float StartAngle; // Angle for the first corner (0.5 for pointy-top, 0.0 for flat-top)

        public Orientation(float f0, float f1, float f2, float f3, float b0, float b1, float b2, float b3, float startAngle)
        {
            F0 = f0; F1 = f1; F2 = f2; F3 = f3;
            B0 = b0; B1 = b1; B2 = b2; B3 = b3;
            StartAngle = startAngle;
        }
    }

    /// <summary>
    /// Defines the layout parameters for converting between Hex and pixel coordinates.
    /// </summary>
    public readonly struct HexLayout
    {
        public readonly Orientation Orientation;
        public readonly Vector2 Size; // Size of the hex (width, height)
        public readonly Vector2 Origin; // Pixel offset for the center of Hex(0, 0)

        public HexLayout(Orientation orientation, Vector2 size, Vector2 origin)
        {
            Orientation = orientation;
            Size = size;
            Origin = origin;
        }

        // Predefined orientations
        public static readonly Orientation Pointy = new(
            MathF.Sqrt(3.0f), MathF.Sqrt(3.0f) / 2.0f, 0.0f, 3.0f / 2.0f, // Forward matrix
            MathF.Sqrt(3.0f) / 3.0f, -1.0f / 3.0f, 0.0f, 2.0f / 3.0f,    // Inverse matrix
            0.5f // Start angle (in multiples of 60 degrees)
        );

        public static readonly Orientation Flat = new(
            3.0f / 2.0f, 0.0f, MathF.Sqrt(3.0f) / 2.0f, MathF.Sqrt(3.0f), // Forward matrix
            2.0f / 3.0f, 0.0f, -1.0f / 3.0f, MathF.Sqrt(3.0f) / 3.0f,   // Inverse matrix
            0.0f // Start angle
        );

        /// <summary>
        /// Converts a Hex coordinate to its corresponding pixel center.
        /// </summary>
        public Vector2 HexToPixel(Hex h)
        {
            float x = (Orientation.F0 * h.Q + Orientation.F1 * h.R) * Size.X;
            float y = (Orientation.F2 * h.Q + Orientation.F3 * h.R) * Size.Y;
            return new Vector2(x + Origin.X, y + Origin.Y);
        }

        public bool IsInView(Hex h)
        {
            Vector2 pixel = HexToPixel(h);
            return pixel.X >= -Size.X && pixel.X <= Raylib.GetScreenWidth() + Size.X &&
                   pixel.Y >= -Size.Y && pixel.Y <= Raylib.GetScreenHeight() + Size.Y;
        }

        /// <summary>
        /// Converts a pixel coordinate to its corresponding (fractional) Hex coordinate.
        /// </summary>
        public HexF PixelToFractionalHex(Vector2 p)
        {
            Vector2 pt = new((p.X - Origin.X) / Size.X, (p.Y - Origin.Y) / Size.Y);
            float q = Orientation.B0 * pt.X + Orientation.B1 * pt.Y;
            float r = Orientation.B2 * pt.X + Orientation.B3 * pt.Y;
            return new HexF(q, r);
        }

        /// <summary>
        /// Calculates the pixel offset for a specific corner of a hexagon relative to its center.
        /// </summary>
        /// <param name="corner">Corner index (0-5).</param>
        public Vector2 HexCornerOffset(int corner)
        {
            float angle = 2.0f * MathF.PI * (Orientation.StartAngle + corner) / 6.0f;
            return new Vector2(Size.X * MathF.Cos(angle), Size.Y * MathF.Sin(angle));
        }

        /// <summary>
        /// Gets the pixel coordinates of all 6 corners for a given hexagon.
        /// </summary>
        public Vector2[] PolygonCorners(Hex h)
        {
            Vector2 center = HexToPixel(h);
            Vector2[] corners = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                Vector2 offset = HexCornerOffset(i);
                corners[i] = new Vector2(center.X + offset.X, center.Y + offset.Y);
            }
            return corners;
        }
    }

    /// <summary>
    /// Represents a fractional Hex coordinate, used for intermediate calculations like pixel-to-hex conversion.
    /// </summary>
    public readonly struct HexF
    {
        public readonly float Q;
        public readonly float R;
        public float S => -Q - R;

        public HexF(float q, float r)
        {
            Q = q;
            R = r;
        }

        /// <summary>
        /// Rounds fractional hex coordinates to the nearest integer Hex coordinate.
        /// </summary>
        public Hex Round()
        {
            int q = (int)MathF.Round(Q);
            int r = (int)MathF.Round(R);
            int s = (int)MathF.Round(S);

            float qDiff = MathF.Abs(q - Q);
            float rDiff = MathF.Abs(r - R);
            float sDiff = MathF.Abs(s - S);

            // Reset the coordinate with the largest rounding difference
            if (qDiff > rDiff && qDiff > sDiff)
            {
                q = -r - s;
            }
            else if (rDiff > sDiff)
            {
                r = -q - s;
            }
            // else: s = -q - r; // s is already derived correctly

            return new Hex(q, r);
        }
    }
}