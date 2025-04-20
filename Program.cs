using Raylib_cs;
using RL = Raylib_cs.Raylib;
using System.Numerics;
using EvoVerse;
using System.Collections.Generic;
using System;
using System.Linq;
using ImGuiNET;
using rlImGui_cs;

// --- Configuration ---
const int ScreenWidth = 1280;
const int ScreenHeight = 720;
const string WindowTitle = "EvoVerse Hex Grid";
const int TargetFps = 60;
const int MapRadius = 20;
const float MinZoom = 10f;
const float MaxZoom = 300f;

// --- Colors ---
Color BackgroundColor = new(220, 248, 255, 255); // AliceBlue
Color HoverOutlineColor = new(100, 110, 120, 255);
Color BorderColor = new(150, 150, 150, 40); // Semi-transparent gray for border hexes

// --- Cell Count Tracking ---
const int MaxCellCountHistory = 1000; // Keep last 1000 data points
List<int> CellCountHistory = new();

// --- Grid Setup ---
HexLayout Layout;
WorldGrid WorldGrid;
Hex HoveredHex = new(0, 0);
Vector2 HexSize = new(30, 30);
Vector2 GridOrigin = new(ScreenWidth / 2f, ScreenHeight / 2f);
Vector2 mousePos;
CellType selectedCellType = CellType.Flesh; // Default selected cell type
int brushSize = 0; // Radius of cells to place (0 = single cell)

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
    WorldGrid = new WorldGrid(Layout, MapRadius);
    WorldGrid.ClearAndReset(); // Start with a stem cell
    RL.SetTargetFPS(TargetFps);

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
        // Update morphogen field
        WorldGrid.Update();
        
        // Update cells (movement, division)
        UpdateCellDivision();
        
        // Update cell count history after each step
        int currentCellCount = WorldGrid.GetAllOccupiedHexes().Count();
        CellCountHistory.Add(currentCellCount);
        
        // Keep only the last MaxCellCountHistory points
        if (CellCountHistory.Count > MaxCellCountHistory)
        {
            CellCountHistory.RemoveAt(0);
        }
    }

    // --- Input Handling (Camera, Cell Placement) ---
    if (!isInputCapturedByUI)
    {
        HoveredHex = Layout.PixelToFractionalHex(mousePos).Round();
        HandleCameraControls();
        
        HandleCellStateChanges();
    }
    else
    {
        HoveredHex = new Hex(int.MaxValue, int.MaxValue); // Don't show hover if UI has focus
    }
}

void UpdateCellDivision()
{
    if (RL.GetFrameTime() > 0.2f) return;

    var allCellsSnapshot = WorldGrid.GetAllCells().ToList();
    var newCellsFromDivision = new List<Cell>();

    foreach (var cell in allCellsSnapshot)
    {
        Cell? currentCellInGrid = WorldGrid.GetCell(cell.Position);
        if (currentCellInGrid == null || currentCellInGrid.Id != cell.Id)
        {
            continue;
        }

        Cell? newCell = cell.Update(WorldGrid);
        if (newCell != null)
        {
            newCellsFromDivision.Add(newCell);
        }
    }

    foreach (var newCell in newCellsFromDivision.DistinctBy(c => c.Position))
    {
        if (WorldGrid.IsWithinBounds(newCell.Position) && !WorldGrid.IsOccupied(newCell.Position))
        {
            WorldGrid.AddCell(newCell);
        }
    }
}

bool IsInputCapturedByUI()
{
    return ImGui.GetIO().WantCaptureMouse || ImGui.GetIO().WantCaptureKeyboard;
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
    WorldGrid.UpdateLayout(Layout);
}

void HandleCellStateChanges()
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
        if (WorldGrid.IsWithinBounds(HoveredHex))
        {
            Cell? hoveredCell = WorldGrid.GetCell(HoveredHex);
            if (hoveredCell != null && hoveredCell.Type != CellType.None)
            {
                selectedCellType = hoveredCell.Type;
            }
        }
    }
}

void PlaceCellIfValid(CellType cellType)
{
    if (!WorldGrid.IsWithinBounds(HoveredHex)) return;

    var hexesToModify = new List<Hex> { HoveredHex };
    
    // If brush size > 0, add neighbors within radius
    if (brushSize > 0)
    {
        for (int q = -brushSize; q <= brushSize; q++)
        {
            for (int r = Math.Max(-brushSize, -q - brushSize); r <= Math.Min(brushSize, -q + brushSize); r++)
            {
                var hex = HoveredHex + new Hex(q, r);
                if (WorldGrid.IsWithinBounds(hex))
                {
                    hexesToModify.Add(hex);
                }
            }
        }
    }

    foreach (var hex in hexesToModify)
    {
        bool canPlace = false;
        Cell? existingCell = WorldGrid.GetCell(hex);

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
            WorldGrid.PlaceCell(hex, cellType);
        }
    }
}

void DrawHexOutline(Hex hex, Color color, float borderWidth = 2f, float sizeMultiplier = 1.0f)
{
    if (sizeMultiplier <= 0) return;

    // Create a temporary layout with adjusted size for the outline
    var tempLayout = new HexLayout(
        WorldGrid.Layout.Orientation,
        WorldGrid.Layout.Size * sizeMultiplier,
        WorldGrid.Layout.Origin
    );
    
    Vector2[] borderCorners = tempLayout.PolygonCorners(hex);
    if (borderCorners.Length >= 2)
    {
        for (int i = 0; i < borderCorners.Length; i++)
        {
            RL.DrawLineEx(borderCorners[i], borderCorners[(i + 1) % borderCorners.Length], borderWidth, color);
        }
    }
}

void Draw()
{
    RL.ClearBackground(BackgroundColor);

    // --- 1. Draw Grid Lines ---
    foreach (Hex hex in WorldGrid.GetHexesInRadius().Where(h => WorldGrid.Layout.IsInView(h)))
    {
        if (WorldGrid.GetCellType(hex) == CellType.None)
        {
            DrawHexOutline(hex, BorderColor);
        }
    }

    // --- 2. Draw Cells ---
    var allCells = WorldGrid.GetAllCells().ToArray();
    foreach (Cell cell in allCells.Where(c => WorldGrid.Layout.IsInView(c.Position)))
    {
        var neighbors = allCells.Where(n => cell.Position.Distance(n.Position) <= 1).ToList();
        cell.Draw(WorldGrid.Layout, HexSize.X * 0.75f, neighbors);
    }

    // --- 3. Draw Hover Outline ---
    bool isHoverValid = !IsInputCapturedByUI() && WorldGrid.IsWithinBounds(HoveredHex);
    if (isHoverValid)
    {
        // Draw brush preview if in editing mode and brush size > 0
        if (brushSize > 0)
        {
            // Draw faint outlines for all hexes in brush radius
            for (int q = -brushSize; q <= brushSize; q++)
            {
                for (int r = Math.Max(-brushSize, -q - brushSize); r <= Math.Min(brushSize, -q + brushSize); r++)
                {
                    var hex = HoveredHex + new Hex(q, r);
                    if (WorldGrid.IsWithinBounds(hex))
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
            // Regular single hex hover outline
            DrawHexOutline(HoveredHex, HoverOutlineColor, 5f);
        }
    }

    // --- 4. Draw UI ---
    rlImGui.Begin();
    DrawInfoPanel(isHoverValid, 0.25f); // Use 25% of the screen width by default
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
        Cell? hoveredCell = WorldGrid.GetCell(HoveredHex);
        ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Hover Cell ID");
        ImGui.TableNextColumn(); ImGui.Text($"{(hoveredCell != null ? hoveredCell.Id.ToString().Substring(0, 8) : "None")}");

        ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Hover Type");
        ImGui.TableNextColumn(); ImGui.Text($"{(hoveredCell != null ? hoveredCell.Type.ToString() : "None")}");

        ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Total Cells");
        ImGui.TableNextColumn(); ImGui.Text($"{WorldGrid.GetAllOccupiedHexes().Count()}");

        ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Map Radius");
        ImGui.TableNextColumn(); ImGui.Text($"{WorldGrid.MapRadius}");

        ImGui.EndTable();
    }
    ImGui.Separator();

    ImGui.Text("Cell Type (Editing Mode)");
    ImGui.BeginDisabled(CurrentSimulationState != SimulationState.Editing);
    foreach (CellType cellType in Enum.GetValues(typeof(CellType)))
    {
        if (cellType == CellType.None) continue;
        if (ImGui.RadioButton(cellType.ToString(), selectedCellType == cellType))
        {
            selectedCellType = cellType;
        }
    }
    ImGui.EndDisabled();
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
    Vector2 window_pos = new Vector2(work_pos.X + work_size.X * 0.5f, work_pos.Y + work_size.Y - padding);
    Vector2 window_pos_pivot = new Vector2(0.5f, 1.0f);

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
        string editModeLabel = CurrentSimulationState == SimulationState.Editing ? "Simulate" : "Edit";
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
                WorldGrid.ClearAndReset(); // Reset grid when returning to edit mode
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
    RL.CloseWindow();
}