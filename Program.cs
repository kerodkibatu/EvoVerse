using Raylib_cs;
using RL = Raylib_cs.Raylib;
using System.Numerics;
using EvoVerse;
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

// --- Colors ---
Color BackgroundColor = new(220, 248, 255, 255); // AliceBlue
Color HoverOutlineColor = new(100, 110, 120, 255);
Color BorderColor = new(150, 150, 150, 40); // Semi-transparent gray for border hexes

// --- Cell Type String Cache ---
Dictionary<CellType, string> CellTypeStringCache = InitializeCellTypeStringCache();

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
CellType selectedCellType = CellType.Flesh; // Default selected cell type
int brushSize = 0; // Radius of cells to place (0 = single cell)

// --- Morphogen Visualization ---
Dictionary<string, bool> MorphogenVisibility = new Dictionary<string, bool>();
Dictionary<string, Color> MorphogenColors = new Dictionary<string, Color>();

// --- Simulation State & Control ---
SimulationState CurrentSimulationState = SimulationState.Editing;
bool StepRequested = false;
float simulationSpeed = 10.0f; // Steps per second
float timeSinceLastStep = 0.0f;

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
            MorphogenVisibility[morphogen] = true; // Default to visible
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

    if (performUpdate)
    {
        // Update World
        World.Update();

        // Update plot
        UpdatePlot();
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
    bool isLeftClick = RL.IsMouseButtonPressed(MouseButton.Left);
    bool isShiftHeld = RL.IsKeyDown(KeyboardKey.LeftShift) || RL.IsKeyDown(KeyboardKey.RightShift);
    bool isLeftClickHeldWithShift = isShiftHeld && RL.IsMouseButtonDown(MouseButton.Left);

    if (isLeftClick || isLeftClickHeldWithShift)
    {
        PlaceCellIfValid(selectedCellType);
    }
    if (RL.IsMouseButtonDown(MouseButton.Right))
    {
        PlaceCellIfValid(CellType.None);
    }
    if (RL.IsKeyPressed(KeyboardKey.S))
    {
        if (World.IsWithinBounds(HoveredHex))
        {
            Cell? hoveredCell = World.GetCell(HoveredHex);
            if (hoveredCell != null && hoveredCell.Type != CellType.None)
            {
                selectedCellType = hoveredCell.Type;
            }
        }
    }
}

void PlaceCellIfValid(CellType cellType)
{
    if (!World.IsWithinBounds(HoveredHex)) return;

    var hexesToModify = new List<Hex> { HoveredHex };
    
    // If brush size > 0, add neighbors within radius
    if (brushSize > 0)
    {
        for (int q = -brushSize; q <= brushSize; q++)
        {
            for (int r = Math.Max(-brushSize, -q - brushSize); r <= Math.Min(brushSize, -q + brushSize); r++)
            {
                var hex = HoveredHex + new Hex(q, r);
                if (World.IsWithinBounds(hex))
                {
                    hexesToModify.Add(hex);
                }
            }
        }
    }

    foreach (var hex in hexesToModify)
    {
        bool canPlace = false;
        Cell? existingCell = World.GetCell(hex);

        if (cellType == CellType.None)
        {
            canPlace = true;
        }
        else if (existingCell == null)
        {
            canPlace = true;
        }
        else if (RL.IsKeyDown(KeyboardKey.LeftShift) && existingCell.Type != selectedCellType)
        {
            canPlace = true;
        }

        if (canPlace)
        {
            World.PlaceCell(hex, cellType);
        }
    }
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

void DrawMorphogen(Hex hex)
{
    // Cache the pixel position of the hex to avoid recalculating it for each morphogen
    Vector2 hexPosition = Layout.HexToPixel(hex);
    
    // Use a single loop to minimize overhead
    foreach (var morphogen in MorphogenManager.Morphogens)
    {
        if (MorphogenVisibility.TryGetValue(morphogen, out bool isVisible) && isVisible)
        {
            Color displayColor = MorphogenColors[morphogen];
            float strength = MorphogenManager.GetStrengthAtHex(hex, morphogen);
            if (strength > 0) // Only draw if there's a strength
            {
                float radius = 0.5f * strength * Layout.Size.X;
                byte alpha = (byte)(strength * displayColor.A);
                RL.DrawPoly(hexPosition, 8, radius, 0, new Color(displayColor.R, displayColor.G, displayColor.B, alpha));
            }
        }
    }
}

void Draw()
{
    RL.ClearBackground(BackgroundColor);

    // --- 1. Draw Grid Lines ---
    foreach (Hex hex in World.GetHexesInRadius())
    {
        if (World.Layout.IsInView(hex) && World.IsWithinBounds(hex))
        {
            if (World.GetCellType(hex) == CellType.None)
            {
                DrawHexOutline(hex, BorderColor);
            }
        }
    }

    // --- 2. Draw Morphogens ---
    var affectedHexes = MorphogenManager.GetAffectedHexes().ToArray();
    foreach (var affectedHex in affectedHexes)
    {
        if (World.IsWithinBounds(affectedHex) && World.Layout.IsInView(affectedHex))
        {
            DrawMorphogen(affectedHex);
        }
    }

    // --- 3. Draw Cells ---
    var allCells = World.GetAllCells().ToArray();
    foreach (Cell cell in allCells)
    {
        if (World.Layout.IsInView(cell.Position))
        {
            var neighbors = new List<Cell>();
            foreach (var n in allCells)
            {
                if (cell.Position.Distance(n.Position) <= 1)
                {
                    neighbors.Add(n);
                }
            }
            cell.Draw(World.Layout, HexSize.X * 0.75f, neighbors);
        }
    }

    // --- 3. Draw Hover Outline ---
    bool isHoverValid = !IsInputCapturedByUI() && World.IsWithinBounds(HoveredHex);
    if (isHoverValid)
    {
        if (brushSize > 0)
        {
            for (int q = -brushSize; q <= brushSize; q++)
            {
                for (int r = Math.Max(-brushSize, -q - brushSize); r <= Math.Min(brushSize, -q + brushSize); r++)
                {
                    var hex = HoveredHex + new Hex(q, r);
                    if (World.Layout.IsInView(hex))
                    {
                        var previewColor = HoverOutlineColor;
                        previewColor.A = 100;
                        DrawHexOutline(hex, previewColor, 2f);
                    }
                }
            }
        }
        else
        {
            DrawHexOutline(HoveredHex, HoverOutlineColor, 5f);
        }

        if (RL.IsKeyDown(KeyboardKey.LeftAlt) || RL.IsKeyDown(KeyboardKey.RightAlt))
        {
            Cell? hoveredCell = World.GetCell(HoveredHex);
            Vector2 tooltipPos = Layout.HexToPixel(HoveredHex);

            string tooltipText = $"Hex: {HoveredHex}\n";
            tooltipText += $"ID: {(hoveredCell != null ? hoveredCell.Id.ToString().Substring(0, 8) : "None")}\n";
            tooltipText += $"Type: {(hoveredCell != null ? CellTypeStringCache[hoveredCell.Type] : "None")}\n";

            if (MorphogenManager.Morphogens.Any())
            {
                tooltipText += "Morphogens:\n";
                foreach (var morphogen in MorphogenManager.Morphogens)
                {
                    if (MorphogenVisibility[morphogen])
                    {
                        float strength = MorphogenManager.GetStrengthAtHex(HoveredHex, morphogen);
                        tooltipText += $"  {morphogen}: {strength:F2}\n";
                    }
                }
            }

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
    }

    // --- 4. Draw UI ---
    rlImGui.Begin();
    DrawInfoPanel(isHoverValid, 0.25f);
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
    
    // Calculate the desired width based on the ratio
    float panelWidth = workSize.X * xAxisRatio;
    float panelHeight = workSize.Y; // Keep full screen height

    // Set up docking
    ImGui.SetNextWindowPos(new Vector2(viewport.WorkPos.X, viewport.WorkPos.Y));
    ImGui.SetNextWindowSizeConstraints(
        new Vector2(200, panelHeight), // Minimum width of 200 pixels, fixed height
        new Vector2(workSize.X * 0.5f, panelHeight) // Maximum width of 50% screen width, fixed height
    );
    
    // Enable docking and set the window to dock to the left
    ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f); // Optional: no rounding for docked window
    ImGui.Begin("Info Panel", ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoCollapse);

    // Dock the window to the left
    ImGui.DockSpace(ImGui.GetID("InfoPanelDockSpace"), new Vector2(0, 0), ImGuiDockNodeFlags.PassthruCentralNode);
    
    // Panel content (unchanged)
    ImGui.Text($"State: {CurrentSimulationState}");
    ImGui.Separator();

    ImGui.Text("Brush Size");
    ImGui.SliderInt("##BrushSize", ref brushSize, 0, 5, brushSize == 0 ? "Single Cell" : $"Radius: {brushSize}");
    if (ImGui.IsItemHovered())
    {
        ImGui.SetTooltip("Size of the brush area when placing cells.\nRadius 0 = single cell\nRadius 1 = 7 cells\nRadius 2 = 19 cells\nRadius 3 = 37 cells");
    }
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
        ImGui.TableNextColumn(); ImGui.Text($"{(hoveredCell != null ? CellTypeStringCache[hoveredCell.Type] : "None")}");

        ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Total Cells");
        ImGui.TableNextColumn(); ImGui.Text($"{World.GetAllOccupiedHexes().Count()}");

        ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Map Radius");
        ImGui.TableNextColumn(); ImGui.Text($"{World.MapRadius}");

        ImGui.EndTable();
    }
    ImGui.Separator();

    ImGui.Text("Cell Type (Editing Mode)");
    foreach (CellType cellType in Enum.GetValues(typeof(CellType)))
    {
        if (cellType == CellType.None) continue;
        if (ImGui.RadioButton(CellTypeStringCache[cellType], selectedCellType == cellType))
        {
            selectedCellType = cellType;
        }
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
            ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Left Click"); ImGui.TableNextColumn(); ImGui.Text("Place Cell (Editing Only)");
            ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Shift + Left Click"); ImGui.TableNextColumn(); ImGui.Text("Overwrite Cell (Editing Only)");
            ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Right Click"); ImGui.TableNextColumn(); ImGui.Text("Remove Cell (Editing Only)");
            ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("S Key"); ImGui.TableNextColumn(); ImGui.Text("Sample Cell Type (Editing Only)");
            ImGui.EndTable();
        }
    }
    
    ImGui.End();
    ImGui.PopStyleVar();
}

void DrawSimulationControls()
{
    ImGuiWindowFlags window_flags = ImGuiWindowFlags.NoDecoration
                                  | ImGuiWindowFlags.NoDocking
                                  | ImGuiWindowFlags.AlwaysAutoResize
                                  | ImGuiWindowFlags.NoSavedSettings
                                  | ImGuiWindowFlags.NoFocusOnAppearing
                                  | ImGuiWindowFlags.NoNav;

    float padding = 10.0f;
    ImGuiViewportPtr viewport = ImGui.GetMainViewport();
    Vector2 work_pos = viewport.WorkPos;
    Vector2 work_size = viewport.WorkSize;
    Vector2 window_pos = new Vector2(work_pos.X + work_size.X - padding, work_pos.Y + work_size.Y - padding);
    Vector2 window_pos_pivot = new Vector2(1.0f, 1.0f); // Pivot to bottom right

    ImGui.SetNextWindowPos(window_pos, ImGuiCond.Always, window_pos_pivot);
    ImGui.SetNextWindowBgAlpha(0.6f);

    if (ImGui.Begin("SimulationControls", window_flags))
    {
        // Play/Pause Button
        string playPauseLabel = CurrentSimulationState == SimulationState.Running ? "Pause (II)" : "Play (>)";
        if (ImGui.Button(playPauseLabel, new Vector2(100, 0)))
        {
            if (CurrentSimulationState == SimulationState.Running)
            {
                CurrentSimulationState = SimulationState.Paused;
                timeSinceLastStep = 0; // Reset timer on pause
            }
            else if (CurrentSimulationState == SimulationState.Paused) // Only play if paused
            {
                CurrentSimulationState = SimulationState.Running;
                timeSinceLastStep = 0; // Reset timer on play
            }
            else if (CurrentSimulationState == SimulationState.Editing)
            {
                CurrentSimulationState = SimulationState.Running;
                timeSinceLastStep = 0; // Reset timer on play
            }
        }
        ImGui.SameLine();

        // Step Button
        ImGui.BeginDisabled(CurrentSimulationState == SimulationState.Editing); // Can step when paused or running
        if (ImGui.Button("Step (>|)", new Vector2(100, 0)))
        {
            StepRequested = true;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Execute one simulation step\n(Will pause if running)");
        ImGui.EndDisabled();
        ImGui.SameLine();

        // Edit/Simulate Toggle Button
        string editModeLabel = CurrentSimulationState == SimulationState.Editing ? "Sim" : "Edit";
        if (ImGui.Button(editModeLabel, new Vector2(80, 0)))
        {
            if (CurrentSimulationState == SimulationState.Editing)
            {
                CurrentSimulationState = SimulationState.Paused; // Start simulation in paused state
                timeSinceLastStep = 0;
            }
            else // Current state is Paused or Running
            {
                CurrentSimulationState = SimulationState.Editing;
                World.ClearAndReset(); // Reset grid when returning to edit mode
                timeSinceLastStep = 0; // Reset timer when going to edit mode
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                CurrentSimulationState == SimulationState.Editing ?
                "Switch to Simulation Mode (Paused)" :
                "Return to Editing Mode (Simulation stops)");
        ImGui.SameLine();

        // Speed Slider
        ImGui.PushItemWidth(150); // Make slider wider
        ImGui.SliderFloat("Speed (Steps/Sec)", ref simulationSpeed, 0.1f, 60.0f, "%.1f", ImGuiSliderFlags.Logarithmic);
        if (simulationSpeed < 0.1f) simulationSpeed = 0.1f; // Prevent zero/negative speed from slider interaction
        ImGui.PopItemWidth();

    }
    ImGui.End();
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

static Dictionary<CellType, string> InitializeCellTypeStringCache()
{
    var cache = new Dictionary<CellType, string>();
    foreach (CellType cellType in Enum.GetValues(typeof(CellType)))
    {
        cache[cellType] = cellType.ToString();
    }
    return cache;
}