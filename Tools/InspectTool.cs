using System.Numerics;
using Raylib_cs;

namespace EvoVerse.Tools;

public class InspectTool : ITool
{
    public string Name => "Inspect";
    public string ShortcutHint => "3";

    public void HandleInput(Hex hoveredHex, WorldGrid world)
    {
        // Inspect is read-only; no placement or modification
    }

    public void DrawOptions(ToolContext ctx)
    {
        ImGuiNET.ImGui.Text("Inspect Tool");
        ImGuiNET.ImGui.TextDisabled("Hover over a hex to view cell and morphogen details.");
        ImGuiNET.ImGui.Separator();
    }

    public IReadOnlyList<(string Control, string Action)> GetHelpEntries() => [
        ("Hover", "View cell and morphogen details"),
    ];

    public void DrawPreview(Hex hoveredHex, WorldGrid world, ToolContext ctx)
    {
        if (!world.IsWithinBounds(hoveredHex)) return;

        var hoveredCell = world.GetCell(hoveredHex);
        var tooltipPos = world.Layout.HexToPixel(hoveredHex);

        string tooltipText = $"Hex: {hoveredHex}\n";
        tooltipText += $"ID: {(hoveredCell != null ? hoveredCell.Id.ToString()[..8] : "None")}\n";
        tooltipText += $"Type: {hoveredCell?.Type ?? "None"}\n";
        if (hoveredCell != null)
        {
            tooltipText += $"Clock: {hoveredCell.Clock}\n";
            if (hoveredCell.Timers.Count > 0)
            {
                tooltipText += "Timers: ";
                tooltipText += string.Join(", ", hoveredCell.Timers.Select(t => $"{t.marker}({t.time})"));
                tooltipText += "\n";
            }
        }

        var hexStates = world.GetMorphogensAtHex(hoveredHex);
        if (hexStates != null && hexStates.Count > 0)
        {
            tooltipText += "=========\n";
            tooltipText += "Morphogens:\n";
            foreach (var (morphogen, strength) in hexStates)
            {
                if (world.GetMorphogenStrength(hoveredHex, morphogen) > 0)
                    tooltipText += $"  {morphogen}: {strength:F2}\n";
            }
        }
        tooltipText += "=========\n";

        float padding = 10;
        var textSize = Raylib.MeasureTextEx(ctx.UIFont, tooltipText, 20, 1);
        var tooltipRect = new Rectangle(
            tooltipPos.X + 20,
            tooltipPos.Y - 20,
            textSize.X + padding * 2,
            textSize.Y + padding * 2);
        Raylib.DrawRectangleRec(tooltipRect, new Color(0, 0, 0, 200));
        Raylib.DrawRectangleLinesEx(tooltipRect, 1, Color.White);
        Raylib.DrawTextEx(ctx.UIFont, tooltipText, new Vector2(tooltipPos.X + 20 + padding, tooltipPos.Y - 20 + padding), 20, 1, Color.White);
    }
}
