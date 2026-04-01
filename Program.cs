using Raylib_cs;
using RL = Raylib_cs.Raylib;
using System.Numerics;
using EvoVerse;
using EvoVerse.Tools;
using ImGuiNET;
using rlImGui_cs;

// --- Configuration ---
const int ScreenWidth = 1280;
const int ScreenHeight = 720;
const string WindowTitle = "EvoVerse Hex Grid";
const int TargetFps = 500;
const int MapRadius = 25;
const float MinZoom = 10f;
const float MaxZoom = 300f;
const float BottomBarHeight = 52f;

// --- Colors ---
Color BackgroundColor = new(220, 248, 255, 255); // AliceBlue - world area
Color OutsideWorldColor = new(180, 190, 200, 255); // Gray - outside circular world
Color HoverOutlineColor = new(100, 110, 120, 255);
Color BorderColor = new(150, 150, 150, 40); // Semi-transparent gray for border hexes

// --- Cell Type String Cache (no longer needed, Cell.Type is a string) ---

// --- Fonts ---
Font UIFont;

// --- Cell Count Tracking ---
const int MaxCellCountHistory = 1000; // Keep last 1000 data points
List<int> CellCountHistory = [];

// --- Grid Setup ---
HexLayout Layout;
WorldGrid World;
Hex HoveredHex;
Vector2 HexSize = new(30, 30);
Vector2 GridOrigin = new(ScreenWidth / 2f, ScreenHeight / 2f);
Vector2 mousePos;

// --- Morphogen Visualization ---
Dictionary<string, bool> MorphogenVisibility = new Dictionary<string, bool>();
Dictionary<string, Color> MorphogenColors = new Dictionary<string, Color>();

// --- Simulation State & Control ---
SimulationState CurrentSimulationState = SimulationState.Paused;
bool StepRequested = false;
float simulationSpeed = 10.0f; // Steps per second
float timeSinceLastStep = 0.0f;

// --- Tool System ---
ToolManager ToolManager = new();
ToolContext ToolContext = new();

// --- Main Execution ---
Run(ScreenWidth, ScreenHeight, WindowTitle);

// --- Functions ---
void Init()
{
    Layout = new HexLayout(HexLayout.Pointy, HexSize, GridOrigin);
    World = new WorldGrid(Layout, MapRadius);
    
    // Initialize all morphogens as visible by default with default colors
    foreach (var morphogen in MorphogenManager.Morphogens)
    {
        MorphogenVisibility[morphogen] = true;
        
        MorphogenColors[morphogen] = Utils.SampleHSV(0.75f);
    }
    
    World.ClearAndReset(); // Start with a stem cell

    var placeTool = new PlaceCellTool();
    var morphogenTool = new MorphogenTool();
    var inspectTool = new InspectTool();
    if (MorphogenManager.Morphogens.Count > 0 && !MorphogenManager.Morphogens.Contains(morphogenTool.SelectedMorphogen))
        morphogenTool.SelectedMorphogen = MorphogenManager.Morphogens.First();
    ToolManager.Tools.Add(placeTool);
    ToolManager.Tools.Add(morphogenTool);
    ToolManager.Tools.Add(inspectTool);
    ToolManager.ActiveTool = placeTool;

    RL.SetTargetFPS(TargetFps);

    // Load font for RayLib
    string fontPathRaylib = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "consola.ttf");
    if (!System.IO.File.Exists(fontPathRaylib)) fontPathRaylib = "C:/Windows/Fonts/Arial.ttf"; // Fallback
    
    if (System.IO.File.Exists(fontPathRaylib))
    {
        UIFont = RL.LoadFont(fontPathRaylib);
    }
    else
    {
        UIFont = RL.GetFontDefault();
        Console.WriteLine("Warning: Default font not found, using RayLib default font.");
    }

    rlImGui.Setup(true);
    try
    {
        unsafe
        {
            ImGuiIOPtr io = ImGui.GetIO();
            ImFontAtlasPtr fonts = io.Fonts;
            fonts.Clear();
            string fontPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "consola.ttf");
            if (!System.IO.File.Exists(fontPath)) fontPath = "C:/Windows/Fonts/Arial.ttf"; // Fallback
            float fontSize = 16.0f;
            if (System.IO.File.Exists(fontPath))
            {
                ImFontPtr newFont = fonts.AddFontFromFileTTF(fontPath, fontSize);
            }
            else
            {
                Console.WriteLine("Warning: Default font not found.");
            }
            fonts.Build();

            // style
            var stylePtr = ImGui.GetStyle();
            ImGUIStyle.InitStyle(ref stylePtr);
        }
        rlImGui.ReloadFonts();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading font: {ex.Message}");
    }
}

void Update()
{
    mousePos = RL.GetMousePosition();
    bool isInputCapturedByUI = ImGui.GetIO().WantCaptureMouse || ImGui.GetIO().WantCaptureKeyboard;
    HoveredHex = new Hex(int.MaxValue, int.MaxValue); // Reset hovered hex

    // Ensure all morphogens have visibility settings and colors
    foreach (var morphogen in MorphogenManager.Morphogens)
    {
        // Check for visibility settings
        if (!MorphogenVisibility.ContainsKey(morphogen))
        {
            MorphogenVisibility[morphogen] = false; // Default to invisible
        }
        
        // Check for color settings
        if (!MorphogenColors.ContainsKey(morphogen))
        {
            // Generate a unique color based on ID to keep consistency
            var hash = morphogen.GetHashCode();
            var r = (byte)((hash & 0xFF0000) >> 16);
            var g = (byte)((hash & 0x00FF00) >> 8);
            var b = (byte)(hash & 0x0000FF);
            MorphogenColors[morphogen] = new Color((byte)r, (byte)g, (byte)b, (byte)255);
        }
    }

    // --- Simulation Logic Update ---
    bool performUpdate = false;
    if (CurrentSimulationState == SimulationState.Running)
    {
        timeSinceLastStep += RL.GetFrameTime();
        float timePerStep = simulationSpeed > 0 ? 1.0f / simulationSpeed : float.MaxValue;

        if (timeSinceLastStep >= timePerStep)
        {
            performUpdate = true;
            timeSinceLastStep -= timePerStep;
        }
    }

    if (StepRequested)
    {
        performUpdate = true;
        CurrentSimulationState = SimulationState.Paused; // Step automatically pauses
        StepRequested = false;
        timeSinceLastStep = 0; // Reset timer when single-stepping
    }

    // Global shortcuts (skip when typing in text field)
    if (!ImGui.GetIO().WantTextInput)
    {
        if (RL.IsKeyPressed(KeyboardKey.Space))
        {
            CurrentSimulationState = CurrentSimulationState == SimulationState.Running ? SimulationState.Paused : SimulationState.Running;
            timeSinceLastStep = 0;
        }
        if (RL.IsKeyPressed(KeyboardKey.R))
        {
            World.ClearAndReset();
            CurrentSimulationState = SimulationState.Paused;
            timeSinceLastStep = 0;
        }
    }

    if (performUpdate)
    {
        // Update World
        World.Update();

        // Update plot
        UpdatePlot();

        // Update morphogens and cache
        World.UpdateMorphogens();
    }

    // --- Input Handling (Camera, Cell Placement) ---
    if (!isInputCapturedByUI)
    {
        HoveredHex = Layout.PixelToFractionalHex(mousePos).Round();
        HandleCameraControls();
        
        HandleUserInteraction();
    }
    else
    {
        HoveredHex = new Hex(int.MaxValue, int.MaxValue); // Don't show hover if UI has focus
    }
}

void UpdatePlot()
{
    // Update cell count history after each step
    int currentCellCount = World.GetAllOccupiedHexes().Count();
    CellCountHistory.Add(currentCellCount);

    // Keep only the last MaxCellCountHistory points
    if (CellCountHistory.Count > MaxCellCountHistory)
    {
        CellCountHistory.RemoveAt(0);
    }
}

bool IsInputCapturedByUI()
{
    return ImGui.GetIO().WantCaptureMouse ||
        ImGui.GetIO().WantCaptureKeyboard ||
        ImGui.GetIO().WantTextInput;
}

void HandleCameraControls()
{
    if (RL.IsMouseButtonDown(MouseButton.Middle))
    {
        Vector2 delta = RL.GetMouseDelta();
        GridOrigin += delta;
        UpdateGridLayout();
    }

    float wheel = RL.GetMouseWheelMove();
    if (wheel == 0) return;

    float scaleFactor = 1.0f + wheel * 0.1f;
    Vector2 newHexSize = HexSize * scaleFactor;

    newHexSize.X = Math.Clamp(newHexSize.X, MinZoom, MaxZoom);
    newHexSize.Y = Math.Clamp(newHexSize.Y, MinZoom, MaxZoom);

    if (newHexSize != HexSize)
    {
        HexSize = newHexSize;
        GridOrigin = mousePos - (mousePos - GridOrigin) * scaleFactor;
        UpdateGridLayout();
    }
}

void UpdateGridLayout()
{
    Layout = new HexLayout(Layout.Orientation, HexSize, GridOrigin);
    World.UpdateLayout(Layout);
}

void HandleUserInteraction()
{
    ToolManager.HandleInput(HoveredHex, World);
}

void DrawHexOutline(Hex hex, Color color, float borderWidth = 2f)
{
    Vector2[] borderCorners = Layout.PolygonCorners(hex);
    // Add the first corner to the end of the array to close the loop
    borderCorners = [.. borderCorners, borderCorners[0]];
    if (borderCorners.Length >= 2)
    {
        RL.DrawLineStrip(borderCorners, borderCorners.Length, color);
    }
}

void DrawHexFill(Hex hex, Color color)
{
    var center = Layout.HexToPixel(hex);
    List<Vector2> corners = [center, .. Layout.PolygonCorners(hex).OrderBy(c => -Math.Atan2(c.Y - center.Y, c.X - center.X))];
    corners.Add(corners[1]);
    RL.DrawTriangleFan([.. corners], corners.Count, color);
}

void DrawMorphogen(Hex hex)
{
    // Cache the pixel position of the hex to avoid recalculating it for each morphogen
    Vector2 hexPosition = Layout.HexToPixel(hex);
    
    var hexStates = World.GetMorphogensAtHex(hex);
    if (hexStates != null)
    {
        foreach (var (morphogen, strength) in hexStates)
        {
            if (MorphogenVisibility.TryGetValue(morphogen, out bool isVisible) && isVisible)
            {
                Color displayColor = MorphogenColors[morphogen];
                float radius = 0.8f * Layout.Size.X;
                byte alpha = (byte)(0.9f * strength * displayColor.A);
                DrawHexFill(hex, new Color(displayColor.R, displayColor.G, displayColor.B, alpha));
            }
        }
    }
}

void Draw()
{
    RL.ClearBackground(OutsideWorldColor);

    // --- Draw circular world background ---
    float worldRadiusPx = HexSize.X * MathF.Sqrt(3f) * (MapRadius + 0.6f);
    RL.DrawCircleV(GridOrigin, worldRadiusPx, BackgroundColor);

    int screenW = RL.GetScreenWidth();
    int screenH = RL.GetScreenHeight();
    var hexesInRadius = World.GetHexesInRadius();

    // --- Draw Grid Lines ---
    foreach (Hex hex in hexesInRadius)
    {
        if (World.Layout.IsInView(hex, screenW, screenH))
        {
            if (World.GetCellType(hex) == CellTypeRegistry.None)
            {
                DrawHexOutline(hex, BorderColor);
            }
        }
    }

    // --- Draw Cells ---
    foreach (Cell cell in World.GetAllCells())
    {
        if (World.Layout.IsInView(cell.Position, screenW, screenH))
        {
            var neighbors = World.GetNeighbors(cell.Position);
            cell.Draw(World.Layout, HexSize.X * 0.75f, neighbors);
        }
    }

    // --- 2. Draw Morphogens ---
    foreach (var hex in hexesInRadius)
    {
        if (World.IsWithinBounds(hex) && World.Layout.IsInView(hex, screenW, screenH))
        {
            DrawMorphogen(hex);
        }
    }

    // --- Draw Hover Preview (tool-specific) ---
    bool isHoverValid = !IsInputCapturedByUI() && World.IsWithinBounds(HoveredHex);
    if (isHoverValid)
    {
        ToolContext.Layout = World.Layout;
        ToolContext.HexSize = HexSize;
        ToolContext.ScreenW = screenW;
        ToolContext.ScreenH = screenH;
        ToolContext.MorphogenVisibility = MorphogenVisibility;
        ToolContext.MorphogenColors = MorphogenColors;
        // CellTypeStringCache removed - Cell.Type is already a string
        ToolContext.UIFont = UIFont;
        ToolContext.HoverOutlineColor = HoverOutlineColor;
        ToolContext.BorderColor = BorderColor;
        ToolContext.DrawHexOutline = DrawHexOutline;
        ToolContext.DrawHexFill = DrawHexFill;
        ToolManager.DrawPreview(HoveredHex, World, ToolContext);
    }

    if (isHoverValid && (RL.IsKeyDown(KeyboardKey.LeftAlt) || RL.IsKeyDown(KeyboardKey.RightAlt)) && ToolManager.ActiveTool is not InspectTool)
        {
            Cell? hoveredCell = World.GetCell(HoveredHex);
            Vector2 tooltipPos = Layout.HexToPixel(HoveredHex);

            string tooltipText = $"Hex: {HoveredHex}\n";
            tooltipText += $"ID: {(hoveredCell != null ? hoveredCell.Id.ToString().Substring(0, 8) : "None")}\n";
            tooltipText += $"Type: {(hoveredCell != null ? hoveredCell.Type : "None")}\n";
            if (hoveredCell != null)
            {
                tooltipText += $"Clock: {hoveredCell.Clock}\n";
            }

            var hexStates = World.GetMorphogensAtHex(HoveredHex);
            if (hexStates != null)
            {
                tooltipText += "=========\n";
                tooltipText += "Morphogens:\n";
                foreach (var (morphogen, strength) in hexStates)
                {
                    if (World.GetMorphogenStrength(HoveredHex, morphogen) > 0)
                    {
                        tooltipText += $"  {morphogen}: {strength:F2}\n";
                    }
                }
            }
            tooltipText += "=========\n";

            float padding = 10;
            Vector2 textSize = RL.MeasureTextEx(UIFont, tooltipText, 20, 1);
            Rectangle tooltipRect = new(
                tooltipPos.X + 20,
                tooltipPos.Y - 20,
                textSize.X + padding * 2,
                textSize.Y + padding * 2);
            RL.DrawRectangleRec(tooltipRect, new Color(0, 0, 0, 200));
            RL.DrawRectangleLinesEx(tooltipRect, 1, Color.White);
            RL.DrawTextEx(UIFont, tooltipText, new Vector2(tooltipPos.X + 20 + padding, tooltipPos.Y - 20 + padding), 20, 1, Color.White);
        }

    // ---  Draw UI ---
    rlImGui.Begin();
    DrawInfoPanel(isHoverValid, 0.25f);
    DrawMorphogenPanel(isHoverValid);
    DrawSimulationControls();
    rlImGui.End();
}

void DrawInfoPanel(bool isHoverValid, float xAxisRatio = 0.25f)
{
    // Ensure the ratio is between 0 and 1
    xAxisRatio = Math.Clamp(xAxisRatio, 0.1f, 0.5f);

    // Get the main viewport
    ImGuiViewportPtr viewport = ImGui.GetMainViewport();
    Vector2 workSize = viewport.WorkSize;
    float panelHeight = workSize.Y - BottomBarHeight;

    // Calculate the desired width based on the ratio
    float panelWidth = workSize.X * xAxisRatio;

    ImGui.SetNextWindowPos(new Vector2(viewport.WorkPos.X, viewport.WorkPos.Y));
    ImGui.SetNextWindowSize(new Vector2(panelWidth, panelHeight));
    ImGui.SetNextWindowSizeConstraints(
        new Vector2(200, 200),
        new Vector2(workSize.X * 0.5f, panelHeight)
    );
    
    // Enable docking and set the window to dock to the left
    ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f); // Optional: no rounding for docked window
    ImGui.Begin("Info Panel", ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoCollapse);

    // Dock the window to the left
    ImGui.DockSpace(ImGui.GetID("InfoPanelDockSpace"), new Vector2(0, 0), ImGuiDockNodeFlags.PassthruCentralNode);
    
    // Panel content (unchanged)
    ImGui.Text($"State: {CurrentSimulationState}");
    ImGui.Separator();

    if (ImGui.BeginTable("MetricTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
    {
        ImGui.TableSetupColumn("Metric", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("Value");

        ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("FPS");
        ImGui.TableNextColumn(); ImGui.Text($"{RL.GetFPS()}");

        ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Zoom");
        ImGui.TableNextColumn(); ImGui.Text($"{HexSize.X:F2}");

        if (isHoverValid)
        {
            ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Hover Hex");
            ImGui.TableNextColumn(); ImGui.Text($"{HoveredHex}");

        }
        else
        {
            ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Hover Hex");
            ImGui.TableNextColumn(); ImGui.Text("(N/A)");
        }
        Cell? hoveredCell = World.GetCell(HoveredHex);
        ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Hover Cell ID");
        ImGui.TableNextColumn(); ImGui.Text($"{(hoveredCell != null ? hoveredCell.Id.ToString().Substring(0, 8) : "None")}");

        ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Hover Type");
        ImGui.TableNextColumn(); ImGui.Text($"{(hoveredCell != null ? hoveredCell.Type : "None")}");

        ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Total Cells");
        ImGui.TableNextColumn(); ImGui.Text($"{World.GetAllOccupiedHexes().Count()}");

        ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Map Radius");
        ImGui.TableNextColumn(); ImGui.Text($"{World.MapRadius}");

        ImGui.EndTable();
    }
    ImGui.Separator();

    // Add Morphogen Visualization Section
    if (ImGui.CollapsingHeader("Morphogen Visualization"))
    {
        foreach (var morphogen in MorphogenManager.Morphogens)
        {
            bool isVisible = MorphogenVisibility[morphogen];
            if (ImGui.Checkbox($"{morphogen}", ref isVisible))
            {
                MorphogenVisibility[morphogen] = isVisible;
            }
            
            // Display color indicator next to checkbox
            ImGui.SameLine();
            
            // Get color from custom colors
            Color currentColor = MorphogenColors[morphogen];
                
            Vector4 color = new Vector4(
                currentColor.R / 255f,
                currentColor.G / 255f,
                currentColor.B / 255f,
                currentColor.A / 255f
            );
            
            // Check if color button is clicked and show color picker
            if (ImGui.ColorButton($"##{morphogen}_color", color, ImGuiColorEditFlags.NoTooltip, new Vector2(20, 20)))
            {
                ImGui.OpenPopup($"color_picker_{morphogen}");
            }
            
            // Color picker popup
            if (ImGui.BeginPopup($"color_picker_{morphogen}"))
            {
                ImGui.Text($"Edit {morphogen} Color");
                ImGui.Separator();
                
                if (ImGui.ColorPicker4($"##{morphogen}_picker", ref color,
                    ImGuiColorEditFlags.DisplayRGB | 
                    ImGuiColorEditFlags.DisplayHex |
                    ImGuiColorEditFlags.AlphaBar |
                    ImGuiColorEditFlags.InputRGB))
                {
                    // Update the custom color dictionary
                    MorphogenColors[morphogen] = new Color(
                        color.X,
                        color.Y,
                        color.Z,
                        color.W
                    );
                }
                ImGui.EndPopup();
            }
        }
    }
    ImGui.Separator();

    // Add Graphs section
    if (ImGui.CollapsingHeader("Graphs"))
    {
        if (CellCountHistory.Count > 0)
        {
            // Label
            ImGui.Text("Cell Count History");
            
            // Find min and max values for scaling
            int minCellCount = CellCountHistory.Min();
            int maxCellCount = CellCountHistory.Max();
            int range = Math.Max(1, maxCellCount - minCellCount); // Avoid division by zero
            
            // Get the draw list for custom drawing
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 graphStart = ImGui.GetCursorScreenPos();
            Vector2 graphSize = new Vector2(ImGui.GetContentRegionAvail().X, 150); // Fixed height for graph
            
            // Draw background
            drawList.AddRectFilled(graphStart, graphStart + graphSize, ImGui.GetColorU32(ImGuiCol.FrameBg));
            
            // Draw grid lines
            for (int i = 0; i <= 4; i++)
            {
                float y = graphStart.Y + graphSize.Y * (1 - (float)i / 4);
                drawList.AddLine(
                    new Vector2(graphStart.X, y),
                    new Vector2(graphStart.X + graphSize.X, y),
                    ImGui.GetColorU32(ImGuiCol.Border)
                );
                
                // Add cell count labels
                int cellCount = minCellCount + (range * i / 4);
                string label = $"{cellCount}";
                Vector2 labelSize = ImGui.CalcTextSize(label);
                ImGui.SetCursorPos(new Vector2(5, y - labelSize.Y / 2));
                ImGui.Text(label);
            }
            
            // Draw the line graph
            for (int i = 1; i < CellCountHistory.Count; i++)
            {
                float x1 = graphStart.X + graphSize.X * ((float)(i - 1) / (CellCountHistory.Count - 1));
                float y1 = graphStart.Y + graphSize.Y * (1 - (float)(CellCountHistory[i - 1] - minCellCount) / range);
                float x2 = graphStart.X + graphSize.X * ((float)i / (CellCountHistory.Count - 1));
                float y2 = graphStart.Y + graphSize.Y * (1 - (float)(CellCountHistory[i] - minCellCount) / range);
                
                drawList.AddLine(
                    new Vector2(x1, y1),
                    new Vector2(x2, y2),
                    ImGui.GetColorU32(ImGuiCol.PlotLines),
                    2.0f
                );
            }
            
            // Add current cell count text
            ImGui.SetCursorPos(new Vector2(graphSize.X - 100, graphStart.Y + 5));
            ImGui.Text($"Current: {CellCountHistory.Last()}");
            
            // Move cursor past the graph
            ImGui.SetCursorPosY(graphStart.Y + graphSize.Y + 10);
        }
        else
        {
            ImGui.Text("No cell count data available");
        }
    }

    if (ImGui.CollapsingHeader("Controls Help"))
    {
        if (ImGui.BeginTable("ControlsTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Control"); ImGui.TableSetupColumn("Action");
            ImGui.TableHeadersRow();
            ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Middle Mouse"); ImGui.TableNextColumn(); ImGui.Text("Pan Camera");
            ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Mouse Wheel"); ImGui.TableNextColumn(); ImGui.Text("Zoom Camera");
            foreach (var (control, action) in ToolManager.ActiveTool?.GetHelpEntries() ?? [])
            {
                ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text(control); ImGui.TableNextColumn(); ImGui.Text(action);
            }
            ImGui.EndTable();
        }
    }
    
    ImGui.End();
    ImGui.PopStyleVar();
}

Vector2 ComputeGradientFromCache(Hex hex, string morphogen)
{
    float center = World.GetMorphogenStrength(hex, morphogen);
    Vector2 grad = Vector2.Zero;
    for (int i = 0; i < 6; i++)
    {
        Hex n = hex.Neighbor(i);
        float s = World.GetMorphogenStrength(n, morphogen);
        float diff = s - center;
        float angle = (float)(i * Math.PI / 3);
        grad += new Vector2((float)Math.Cos(angle), -(float)Math.Sin(angle)) * diff;
    }
    return grad != Vector2.Zero ? Vector2.Normalize(grad) : Vector2.Zero;
}

float ComputeGradientMagnitude(Hex hex, string morphogen)
{
    float center = World.GetMorphogenStrength(hex, morphogen);
    float maxN = 0f, minN = 1f;
    foreach (var n in hex.Neighbors())
    {
        float s = World.GetMorphogenStrength(n, morphogen);
        if (s > maxN) maxN = s;
        if (s < minN) minN = s;
    }
    return Math.Max(maxN - center, center - minN);
}

void DrawGradientArrow(Vector2 center, float r, Vector2 direction, float magnitude, Color morphogenColor, ImDrawListPtr drawList)
{
    if (direction == Vector2.Zero || magnitude < 0.001f) return;
    float arrowLen = r * (0.3f + 0.7f * Math.Clamp(magnitude, 0f, 1f));
    Vector2 tip = center + direction * arrowLen;
    Vector2 perp = new(-direction.Y, direction.X);
    float baseOffset = arrowLen * 0.4f;
    float wing = arrowLen * 0.2f;
    Vector2 base1 = center + direction * baseOffset - perp * wing;
    Vector2 base2 = center + direction * baseOffset + perp * wing;
    uint arrowCol = ImGui.ColorConvertFloat4ToU32(new Vector4(
        morphogenColor.R / 255f, morphogenColor.G / 255f, morphogenColor.B / 255f, 0.9f));
    drawList.AddTriangleFilled(tip, base1, base2, arrowCol);
}

void DrawUnifiedGradientCompass(Vector2 center, float size)
{
    var drawList = ImGui.GetWindowDrawList();
    float r = size * 0.45f;
    uint circleCol = ImGui.GetColorU32(ImGuiCol.Border);
    uint fillCol = ImGui.GetColorU32(ImGuiCol.FrameBg);
    drawList.AddCircleFilled(center, r, fillCol);
    drawList.AddCircle(center, r, circleCol);

    foreach (var morphogen in MorphogenManager.Morphogens)
    {
        Vector2 grad = ComputeGradientFromCache(HoveredHex, morphogen);
        float mag = ComputeGradientMagnitude(HoveredHex, morphogen);
        Color c = MorphogenColors.TryGetValue(morphogen, out var mc) ? mc : Color.White;
        DrawGradientArrow(center, r, grad, mag, c, drawList);
    }
}

void DrawMorphogenPanel(bool isHoverValid)
{
    float xAxisRatio = 0.22f;
    xAxisRatio = Math.Clamp(xAxisRatio, 0.15f, 0.4f);

    ImGuiViewportPtr viewport = ImGui.GetMainViewport();
    Vector2 workPos = viewport.WorkPos;
    Vector2 workSize = viewport.WorkSize;

    float panelWidth = workSize.X * xAxisRatio;
    float panelHeight = workSize.Y - BottomBarHeight;
    Vector2 panelPos = new Vector2(workPos.X + workSize.X - panelWidth, workPos.Y);

    ImGui.SetNextWindowPos(panelPos);
    ImGui.SetNextWindowSize(new Vector2(panelWidth, panelHeight));
    ImGui.SetNextWindowSizeConstraints(
        new Vector2(180, 200),
        new Vector2(workSize.X * 0.4f, panelHeight)
    );

    ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 4.0f);
    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 8));
    if (ImGui.Begin("Morphogen Sample", ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoCollapse))
    {
        if (!isHoverValid)
        {
            ImGui.TextColored(new Vector4(0.55f, 0.55f, 0.55f, 1f), "Hover a hex to sample.");
        }
        else
        {
            ImGui.Text("Concentrations");
            if (ImGui.BeginTable("Conc", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Morphogen", ImGuiTableColumnFlags.WidthFixed, 65);
                ImGui.TableSetupColumn("Val", ImGuiTableColumnFlags.WidthFixed, 36);
                ImGui.TableSetupColumn("Bar", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                foreach (var morphogen in MorphogenManager.Morphogens)
                {
                    float strength = World.GetMorphogenStrength(HoveredHex, morphogen);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(morphogen);
                    ImGui.TableNextColumn();
                    ImGui.Text($"{strength:F2}");
                    ImGui.TableNextColumn();
                    float barWidth = Math.Max(30, ImGui.GetContentRegionAvail().X - 4);
                    Vector2 barStart = ImGui.GetCursorScreenPos();
                    Vector2 barSize = new Vector2(barWidth * Math.Clamp(strength, 0f, 1f), 10);
                    uint barColor = MorphogenColors.TryGetValue(morphogen, out var c)
                        ? ImGui.ColorConvertFloat4ToU32(new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, 0.8f))
                        : ImGui.GetColorU32(ImGuiCol.PlotHistogram);
                    ImGui.GetWindowDrawList().AddRectFilled(barStart, barStart + barSize, barColor);
                    ImGui.GetWindowDrawList().AddRect(barStart, barStart + new Vector2(barWidth, 10), ImGui.GetColorU32(ImGuiCol.Border));
                    ImGui.Dummy(new Vector2(barWidth, 10));
                }
                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.Text("Gradients");
            float compassSize = 72f;
            Vector2 compassCenter = ImGui.GetCursorScreenPos() + new Vector2(compassSize / 2 + 8, compassSize / 2 + 8);
            DrawUnifiedGradientCompass(compassCenter, compassSize);
            ImGui.Dummy(new Vector2(compassSize + 16, compassSize + 16));

            ImGui.Text("Legend");
            foreach (var morphogen in MorphogenManager.Morphogens)
            {
                ImGui.SameLine(0, 8);
                if (MorphogenColors.TryGetValue(morphogen, out var mc))
                {
                    ImGui.ColorButton($"##{morphogen}", new Vector4(mc.R / 255f, mc.G / 255f, mc.B / 255f, 1f), ImGuiColorEditFlags.NoTooltip, new Vector2(12, 12));
                }
                ImGui.SameLine();
                ImGui.Text(morphogen);
            }

            ImGui.Spacing();
            ImGui.Text("Neighbors");
            int d = 0;
            foreach (var n in HoveredHex.Neighbors())
            {
                var parts = MorphogenManager.Morphogens
                    .Select(m => (m, World.GetMorphogenStrength(n, m)))
                    .Where(x => x.Item2 > 0.001f)
                    .Select(x => $"{x.Item1}:{x.Item2:F1}").ToList();
                ImGui.Text($"  Dir {d}: {(parts.Count > 0 ? string.Join(" ", parts) : "—")}");
                d++;
            }
        }
    }
    ImGui.End();
    ImGui.PopStyleVar(2);
}

void DrawVerticalSeparator(float height)
{
    Vector2 pos = ImGui.GetCursorScreenPos();
    ImGui.Dummy(new Vector2(12, height));
    float x = pos.X + 6;
    ImGui.GetWindowDrawList().AddLine(
        new Vector2(x, pos.Y),
        new Vector2(x, pos.Y + height),
        ImGui.GetColorU32(ImGuiCol.Separator),
        1f);
}

void DrawSimulationControls()
{
    const float horizontalPadding = 24f;
    const float verticalPadding = 8f;

    ImGuiWindowFlags window_flags = ImGuiWindowFlags.NoDecoration
                                  | ImGuiWindowFlags.NoDocking
                                  | ImGuiWindowFlags.NoSavedSettings
                                  | ImGuiWindowFlags.NoFocusOnAppearing
                                  | ImGuiWindowFlags.NoScrollbar
                                  | ImGuiWindowFlags.NoNav;

    ImGuiViewportPtr viewport = ImGui.GetMainViewport();
    Vector2 work_pos = viewport.WorkPos;
    Vector2 work_size = viewport.WorkSize;
    Vector2 barPos = new Vector2(work_pos.X, work_pos.Y + work_size.Y - BottomBarHeight);
    Vector2 barSize = new Vector2(work_size.X, BottomBarHeight);

    ImGui.SetNextWindowPos(barPos, ImGuiCond.Always);
    ImGui.SetNextWindowSize(barSize, ImGuiCond.Always);

    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(horizontalPadding, verticalPadding));
    ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
    ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10f, 3f)); // Tighter padding for compact buttons
    ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.04f, 0.05f, 0.07f, 0.97f));
    ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.97f, 1f, 0.5f, 0.35f)); // Accent top border

    if (ImGui.Begin("SimulationControls", window_flags))
    {
        float buttonHeight = 28f;

        ToolContext.Layout = World.Layout;
        ToolContext.HexSize = HexSize;
        ToolContext.ScreenW = (int)work_size.X;
        ToolContext.ScreenH = (int)work_size.Y;
        ToolContext.MorphogenVisibility = MorphogenVisibility;
        ToolContext.MorphogenColors = MorphogenColors;
        // CellTypeStringCache removed - Cell.Type is already a string
        ToolContext.UIFont = UIFont;
        ToolContext.HoverOutlineColor = HoverOutlineColor;
        ToolContext.BorderColor = BorderColor;

        ToolManager.HandleNumberKeys();
        for (int i = 0; i < ToolManager.Tools.Count; i++)
        {
            var tool = ToolManager.Tools[i];
            bool selected = ToolManager.ActiveTool == tool;
            if (selected)
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.35f, 0.2f, 1f));
            if (i > 0) ImGui.SameLine(0, 2);
            if (ImGui.Button(tool.Name, new Vector2(0, buttonHeight)))
                ToolManager.ActiveTool = tool;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Right-click for options");
            Vector2 btnMin = ImGui.GetItemRectMin();
            Vector2 btnMax = ImGui.GetItemRectMax();
            if (ImGui.BeginPopupContextItem($"ToolConfig_{i}"))
            {
                ImGui.SetNextWindowPos(new Vector2(btnMin.X, btnMin.Y), ImGuiCond.Appearing, new Vector2(0, 1));
                ImGui.SetNextWindowSize(new Vector2(280, -1), ImGuiCond.FirstUseEver);
                Vector2 popupMin = ImGui.GetWindowPos();
                Vector2 popupMax = popupMin + ImGui.GetWindowSize();
                Vector2 mouse = ImGui.GetIO().MousePos;
                bool mouseOverButton = mouse.X >= btnMin.X && mouse.X <= btnMax.X && mouse.Y >= btnMin.Y && mouse.Y <= btnMax.Y;
                bool mouseOverPopup = mouse.X >= popupMin.X && mouse.X <= popupMax.X && mouse.Y >= popupMin.Y && mouse.Y <= popupMax.Y;
                if (!mouseOverButton && !mouseOverPopup)
                    ImGui.CloseCurrentPopup();
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 10));
                ImGui.Text($"{tool.Name} ({tool.ShortcutHint})");
                ImGui.Separator();
                tool.DrawOptions(ToolContext);
                ImGui.PopStyleVar();
                ImGui.EndPopup();
            }
            if (selected) ImGui.PopStyleColor();
        }
        ImGui.SameLine(0, 16);

        DrawVerticalSeparator(buttonHeight);
        ImGui.SameLine(0, 16);

        // Reset
        if (ImGui.Button("Reset", new Vector2(60, buttonHeight)))
        {
            World.ClearAndReset();
            CurrentSimulationState = SimulationState.Paused;
            timeSinceLastStep = 0;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset simulation (R)");
        ImGui.SameLine(0, 20);

        DrawVerticalSeparator(buttonHeight);
        ImGui.SameLine(0, 16);

        // Transport: Play/Pause, Step back, Step forward
        bool isRunning = CurrentSimulationState == SimulationState.Running;
        if (isRunning)
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.35f, 0.15f, 1f));
        string playPauseLabel = isRunning ? "Pause" : "Play";
        if (ImGui.Button(playPauseLabel, new Vector2(56, buttonHeight)))
        {
            CurrentSimulationState = isRunning ? SimulationState.Paused : SimulationState.Running;
            timeSinceLastStep = 0;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(isRunning ? "Pause (Space)" : "Run (Space)");
        if (isRunning) ImGui.PopStyleColor();
        ImGui.SameLine(0, 4);

        ImGui.BeginDisabled(World.GetCurrentHistoryIndex() <= 0);
        if (ImGui.Button("|<", new Vector2(36, buttonHeight)))
            World.StepBack();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Step back");
        ImGui.EndDisabled();
        ImGui.SameLine(0, 2);

        if (ImGui.Button("|>", new Vector2(36, buttonHeight)))
            StepRequested = true;
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Step forward (pauses if running)");
        ImGui.SameLine(0, 16);

        DrawVerticalSeparator(buttonHeight);
        ImGui.SameLine(0, 16);

        // Speed
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Speed");
        ImGui.SameLine(0, 6);
        ImGui.SetNextItemWidth(120);
        ImGui.SliderFloat("##Speed", ref simulationSpeed, 0.1f, 60f, "%.1f", ImGuiSliderFlags.Logarithmic);
        if (simulationSpeed < 0.1f) simulationSpeed = 0.1f;
        ImGui.SameLine(0, 16);

        DrawVerticalSeparator(buttonHeight);
        ImGui.SameLine(0, 16);

        // History
        ImGui.Text($"Step {World.GetCurrentHistoryIndex() + 1} / {World.GetHistorySize()}");
    }
    ImGui.End();

    ImGui.PopStyleColor(2);
    ImGui.PopStyleVar(4);
}

void Run(int width, int height, string title)
{
    RL.InitWindow(width, height, title);
    RL.SetWindowState(ConfigFlags.ResizableWindow);
    Init();
    while (!RL.WindowShouldClose())
    {
        Update();
        RL.BeginDrawing();
        Draw();
        RL.EndDrawing();
    }
    rlImGui.Shutdown();
    RL.UnloadFont(UIFont); // Unload font before closing
    RL.CloseWindow();
}