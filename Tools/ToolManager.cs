using Raylib_cs;

namespace EvoVerse.Tools;

public class ToolManager
{
    public List<ITool> Tools { get; } = [];
    public ITool? ActiveTool { get; set; }

    public void HandleInput(Hex hoveredHex, WorldGrid world)
    {
        ActiveTool?.HandleInput(hoveredHex, world);
    }

    public void DrawOptions(ToolContext ctx)
    {
        ActiveTool?.DrawOptions(ctx);
    }

    public void DrawPreview(Hex hoveredHex, WorldGrid world, ToolContext ctx)
    {
        ActiveTool?.DrawPreview(hoveredHex, world, ctx);
    }

    public void HandleNumberKeys()
    {
        for (int i = 0; i < Math.Min(Tools.Count, 9); i++)
        {
            var key = (KeyboardKey)((int)KeyboardKey.One + i);
            var kpKey = (KeyboardKey)((int)KeyboardKey.Kp1 + i);
            if (Raylib.IsKeyPressed(key) || Raylib.IsKeyPressed(kpKey))
            {
                ActiveTool = Tools[i];
                return;
            }
        }
    }
}
