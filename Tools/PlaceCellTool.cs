using Raylib_cs;
using System.Numerics;

namespace EvoVerse.Tools;

public class PlaceCellTool : ITool
{
    public string Name => "Place Cell";
    public string ShortcutHint => "1";

    public string SelectedCellType { get; set; } = CellTypeRegistry.Stem;
    public int BrushSize { get; set; }

    private static readonly Color ErasePreviewColor = new Color(150, 80, 80, 100);

    public void HandleInput(Hex hoveredHex, WorldGrid world)
    {
        bool isLeftClick = Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool isShiftHeld = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
        bool isLeftClickHeldWithShift = isShiftHeld && Raylib.IsMouseButtonDown(MouseButton.Left);

        if (isLeftClick || isLeftClickHeldWithShift)
            PlaceCellIfValid(world, hoveredHex, SelectedCellType);
        if (Raylib.IsMouseButtonDown(MouseButton.Right))
            PlaceCellIfValid(world, hoveredHex, CellTypeRegistry.None);
        if (Raylib.IsKeyPressed(KeyboardKey.S) && world.IsWithinBounds(hoveredHex))
        {
            var hoveredCell = world.GetCell(hoveredHex);
            if (hoveredCell != null && hoveredCell.Type != CellTypeRegistry.None)
                SelectedCellType = hoveredCell.Type;
        }
    }

    public void DrawOptions(ToolContext ctx)
    {
        ImGuiNET.ImGui.Text("Brush Size");
        int brushSize = BrushSize;
        if (ImGuiNET.ImGui.SliderInt("##BrushSize", ref brushSize, 0, 5, brushSize == 0 ? "Single Cell" : $"Radius: {brushSize}"))
            BrushSize = brushSize;
        if (ImGuiNET.ImGui.IsItemHovered())
            ImGuiNET.ImGui.SetTooltip("Size of the brush area when placing cells.\nRadius 0 = single cell\nRadius 1 = 7 cells\nRadius 2 = 19 cells\nRadius 3 = 37 cells");
        ImGuiNET.ImGui.Separator();

        ImGuiNET.ImGui.Text("Cell Type");
        foreach (var typeName in CellTypeRegistry.AllTypeNames)
        {
            if (ImGuiNET.ImGui.RadioButton(typeName, string.Equals(SelectedCellType, typeName, StringComparison.OrdinalIgnoreCase)))
                SelectedCellType = typeName;
        }
        ImGuiNET.ImGui.Separator();
    }

    public IReadOnlyList<(string Control, string Action)> GetHelpEntries() => [
        ("Left Click", "Place Cell"),
        ("Shift + Left Click", "Overwrite Cell"),
        ("Right Click", "Remove Cell"),
        ("S Key", "Sample Cell Type"),
    ];

    public void DrawPreview(Hex hoveredHex, WorldGrid world, ToolContext ctx)
    {
        if (!world.IsWithinBounds(hoveredHex)) return;

        int screenW = ctx.ScreenW;
        int screenH = ctx.ScreenH;
        var previewColor = BrushSize > 0 ? GetPreviewColor(ctx) : ctx.HoverOutlineColor;
        if (BrushSize > 0)
            previewColor.A = 100;

        if (BrushSize > 0)
        {
            for (int q = -BrushSize; q <= BrushSize; q++)
            {
                for (int r = Math.Max(-BrushSize, -q - BrushSize); r <= Math.Min(BrushSize, -q + BrushSize); r++)
                {
                    var hex = hoveredHex + new Hex(q, r);
                    if (world.Layout.IsInView(hex, screenW, screenH))
                    {
                        ctx.DrawHexOutline(hex, previewColor, 2f);
                        ctx.DrawHexFill(hex, previewColor);
                    }
                }
            }
        }
        else
        {
            ctx.DrawHexOutline(hoveredHex, ctx.HoverOutlineColor, 5f);
        }
    }

    private Color GetPreviewColor(ToolContext ctx)
    {
        if (string.Equals(SelectedCellType, CellTypeRegistry.None, StringComparison.OrdinalIgnoreCase))
            return ErasePreviewColor;

        // Use the registered type's main color as preview, or fallback
        var def = CellTypeRegistry.Get(SelectedCellType);
        if (def != null)
        {
            var c = def.MainColor;
            return new Color(c.R, c.G, c.B, (byte)100);
        }
        return ctx.HoverOutlineColor;
    }

    private void PlaceCellIfValid(WorldGrid world, Hex hoveredHex, string cellType)
    {
        if (!world.IsWithinBounds(hoveredHex)) return;

        var hexesToModify = new List<Hex> { hoveredHex };
        if (BrushSize > 0)
        {
            for (int q = -BrushSize; q <= BrushSize; q++)
            {
                for (int r = Math.Max(-BrushSize, -q - BrushSize); r <= Math.Min(BrushSize, -q + BrushSize); r++)
                {
                    var hex = hoveredHex + new Hex(q, r);
                    if (world.IsWithinBounds(hex))
                        hexesToModify.Add(hex);
                }
            }
        }

        bool isErase = string.Equals(cellType, CellTypeRegistry.None, StringComparison.OrdinalIgnoreCase);
        foreach (var hex in hexesToModify)
        {
            bool canPlace = false;
            var existingCell = world.GetCell(hex);

            if (isErase)
                canPlace = true;
            else if (existingCell == null)
                canPlace = true;
            else if (Raylib.IsKeyDown(KeyboardKey.LeftShift) && !string.Equals(existingCell.Type, SelectedCellType, StringComparison.OrdinalIgnoreCase))
                canPlace = true;

            if (canPlace)
                world.PlaceCell(hex, cellType);
        }
    }
}
