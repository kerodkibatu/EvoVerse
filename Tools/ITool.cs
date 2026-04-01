namespace EvoVerse.Tools;

public interface ITool
{
    string Name { get; }
    string ShortcutHint { get; }
    void HandleInput(Hex hoveredHex, WorldGrid world);
    void DrawOptions(ToolContext ctx);
    void DrawPreview(Hex hoveredHex, WorldGrid world, ToolContext ctx);
    IReadOnlyList<(string Control, string Action)> GetHelpEntries();
}
