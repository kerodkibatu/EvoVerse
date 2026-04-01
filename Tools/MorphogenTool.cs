using Raylib_cs;

namespace EvoVerse.Tools;

public class MorphogenTool : ITool
{
    public string Name => "Morphogen";
    public string ShortcutHint => "2";

    public MorphogenToolMode Mode { get; set; } = MorphogenToolMode.Simple;
    public bool SubtractMode { get; set; }
    public string SelectedMorphogen { get; set; } = "M0";
    public int Range { get; set; }
    public float Concentration { get; set; } = 1f;
    public Dictionary<string, float> SampledProfile { get; } = [];
    public int AdvancedRange { get; set; }

    public void HandleInput(Hex hoveredHex, WorldGrid world)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.S) && world.IsWithinBounds(hoveredHex))
        {
            var hexStates = world.GetMorphogensAtHex(hoveredHex);
            if (hexStates != null && hexStates.Count > 0)
            {
                if (Mode == MorphogenToolMode.Simple)
                {
                    var best = hexStates.MaxBy(kvp => kvp.Value);
                    SelectedMorphogen = best.Key;
                    Concentration = best.Value;
                }
                else
                {
                    SampledProfile.Clear();
                    foreach (var kvp in hexStates)
                        SampledProfile[kvp.Key] = kvp.Value;
                }
            }
        }

        bool isLeftClick = Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool isLeftClickHeld = Raylib.IsMouseButtonDown(MouseButton.Left);
        if ((isLeftClick || isLeftClickHeld) && world.IsWithinBounds(hoveredHex))
        {
            if (Mode == MorphogenToolMode.Simple)
            {
                if (MorphogenManager.Morphogens.Contains(SelectedMorphogen))
                {
                    if (SubtractMode)
                        MorphogenManager.Subtract(SelectedMorphogen, hoveredHex, Range, Concentration);
                    else
                        MorphogenManager.Emit(SelectedMorphogen, hoveredHex, Range, Concentration);
                }
            }
            else
            {
                foreach (var kvp in SampledProfile)
                {
                    if (MorphogenManager.Morphogens.Contains(kvp.Key) && kvp.Value > 0.001f)
                    {
                        if (SubtractMode)
                            MorphogenManager.Subtract(kvp.Key, hoveredHex, AdvancedRange, kvp.Value);
                        else
                            MorphogenManager.Emit(kvp.Key, hoveredHex, AdvancedRange, kvp.Value);
                    }
                }
            }
            world.UpdateMorphogens();
        }
    }

    public void DrawOptions(ToolContext ctx)
    {
        ImGuiNET.ImGui.Text("Morphogen Tool");
        if (ImGuiNET.ImGui.RadioButton("Simple Emit", Mode == MorphogenToolMode.Simple))
            Mode = MorphogenToolMode.Simple;
        ImGuiNET.ImGui.SameLine();
        if (ImGuiNET.ImGui.RadioButton("Advanced", Mode == MorphogenToolMode.Advanced))
            Mode = MorphogenToolMode.Advanced;
        bool subtract = SubtractMode;
        if (ImGuiNET.ImGui.Checkbox("Subtract (reduce morphogen)", ref subtract))
            SubtractMode = subtract;
        ImGuiNET.ImGui.Separator();

        if (Mode == MorphogenToolMode.Simple)
        {
            ImGuiNET.ImGui.Text("Morphogen");
            if (ImGuiNET.ImGui.BeginCombo("##MorphogenCombo", SelectedMorphogen))
            {
                foreach (var m in MorphogenManager.Morphogens)
                {
                    if (ImGuiNET.ImGui.Selectable(m, SelectedMorphogen == m))
                        SelectedMorphogen = m;
                }
                ImGuiNET.ImGui.EndCombo();
            }
            ImGuiNET.ImGui.Text("Range");
            int range = Range;
            if (ImGuiNET.ImGui.SliderInt("##MorphogenRange", ref range, 0, 5, range == 0 ? "Single" : $"{range}"))
                Range = range;
            ImGuiNET.ImGui.Text("Concentration");
            float conc = Concentration;
            if (ImGuiNET.ImGui.SliderFloat("##MorphogenConc", ref conc, 0f, 1f, "%.2f"))
                Concentration = conc;
            if (ImGuiNET.ImGui.IsItemHovered()) ImGuiNET.ImGui.SetTooltip("Strength of emission (0-1)");
            ImGuiNET.ImGui.TextDisabled("Sample (S) to copy from hovered hex");
        }
        else
        {
            ImGuiNET.ImGui.Text("Profile (Sample with S)");
            var toRemove = new List<string>();
            foreach (var kvp in SampledProfile)
            {
                ImGuiNET.ImGui.PushID(kvp.Key);
                float conc = kvp.Value;
                if (ImGuiNET.ImGui.SliderFloat(kvp.Key, ref conc, 0f, 1f, "%.2f"))
                    SampledProfile[kvp.Key] = conc;
                ImGuiNET.ImGui.SameLine();
                if (ImGuiNET.ImGui.SmallButton("X")) toRemove.Add(kvp.Key);
                ImGuiNET.ImGui.PopID();
            }
            foreach (var k in toRemove) SampledProfile.Remove(k);

            if (ImGuiNET.ImGui.BeginCombo("##AddMorphogen", "Add morphogen..."))
            {
                foreach (var m in MorphogenManager.Morphogens)
                {
                    if (!SampledProfile.ContainsKey(m) && ImGuiNET.ImGui.Selectable(m))
                        SampledProfile[m] = 0.5f;
                }
                ImGuiNET.ImGui.EndCombo();
            }
            ImGuiNET.ImGui.Text("Range");
            int advRange = AdvancedRange;
            if (ImGuiNET.ImGui.SliderInt("##AdvancedRange", ref advRange, 0, 5, advRange == 0 ? "Single" : $"{advRange}"))
                AdvancedRange = advRange;
            ImGuiNET.ImGui.TextDisabled("Sample (S) to copy from hovered hex");
        }
        ImGuiNET.ImGui.Separator();
    }

    public IReadOnlyList<(string Control, string Action)> GetHelpEntries() => [
        ("Left Click", SubtractMode ? "Reduce morphogen" : "Emit morphogen"),
        ("S Key", "Sample from hovered hex"),
    ];

    public void DrawPreview(Hex hoveredHex, WorldGrid world, ToolContext ctx)
    {
        if (!world.IsWithinBounds(hoveredHex)) return;

        int range = Mode == MorphogenToolMode.Simple ? Range : AdvancedRange;
        string morphogenId = Mode == MorphogenToolMode.Simple ? SelectedMorphogen : (SampledProfile.Keys.FirstOrDefault() ?? "M0");
        var color = ctx.MorphogenColors.TryGetValue(morphogenId, out var c) ? c : Raylib_cs.Color.White;
        var previewColor = new Color((byte)color.R, (byte)color.G, (byte)color.B, (byte)60);

        foreach (var hex in Hex.GetHexesInRange(hoveredHex, range))
        {
            if (world.IsWithinBounds(hex) && world.Layout.IsInView(hex, ctx.ScreenW, ctx.ScreenH))
            {
                ctx.DrawHexFill(hex, previewColor);
                ctx.DrawHexOutline(hex, previewColor, 1f);
            }
        }
    }
}
