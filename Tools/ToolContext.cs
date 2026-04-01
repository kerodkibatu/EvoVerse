using System.Numerics;
using Raylib_cs;

namespace EvoVerse.Tools;

public class ToolContext
{
    public HexLayout Layout { get; set; }
    public Vector2 HexSize { get; set; }
    public int ScreenW { get; set; }
    public int ScreenH { get; set; }
    public Dictionary<string, bool> MorphogenVisibility { get; set; } = [];
    public Dictionary<string, Color> MorphogenColors { get; set; } = [];
    // No longer needed - Cell.Type is already a string
    public Font UIFont { get; set; }
    public Color HoverOutlineColor { get; set; }
    public Color BorderColor { get; set; }

    public Action<Hex, Color, float> DrawHexOutline { get; set; } = (_, _, _) => { };
    public Action<Hex, Color> DrawHexFill { get; set; } = (_, _) => { };
}
