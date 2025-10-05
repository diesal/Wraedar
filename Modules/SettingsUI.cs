
using DieselExileTools;
using GameHelper;
using ImGuiNET;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text.RegularExpressions;
using SColor = System.Drawing.Color;
using SVector2 = System.Numerics.Vector2;

namespace Wraedar;

public sealed class SettingsUI(Plugin plugin) : PluginModule(plugin) {

    private int SelectedFileIndex = -1;
    private List<String> PinDirFiles = [];
    private static int selectedAreaIndex = 0;
    private static readonly List<string> _pinGroupKeys = new();

    private static SColor _fileStatusColor;
    private static string? _fileStatusText;

    private static string? _loadedFilename;
    private static string? _loadedFilePath;
    private static Dictionary<string, List<Pin>>? _loadedPins;
    private static bool _pinDirty = false;

    private static int _selectTileIndex = -1;

    //--| Initialise |--------------------------------------------------------------------------------------------------
    public void Initialise() {
        InitializeIcons();
        RefreshFilesAndSelection();
    }

    //--| Draw |-------------------------------------------------------------------------------------------------------
    public void Draw() {
        //DXT.Button.Draw("SettingsWindowOpen", ref Settings.SettingsWindowOpen, new DXT.Button.Options {
        //    Label = "Settings Widnow",
        //    Width = 120,
        //    Height = 20,
        //});
        PushStrippedStyles();



        DXT.Panel.Begin("SettingsPanelOpen", new() { Width = 0 }); {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, controlSpacing);

            Draw_GeneralSettings("GeneralSettingsPanel");

            ImGui.PopStyleVar();
            ImGui.Dummy(new SVector2(0, 0));
            DXT.Panel.End("SettingsPanelOpen");
        }
        ImGui.Dummy(new SVector2(0, 3)); // panel spacing

        if (DXT.CollapsingPanel.Begin("Map.SettingsPanelOpen", ref Settings.Map.SettingsPanelCollapsed, new() { Label = "Map Settings", Width = 0 })) {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, controlSpacing);

            Render_MapSettings("Map.SettingsPanelOpen");

            ImGui.PopStyleVar();
            ImGui.Dummy(new SVector2(0, 0));
            DXT.CollapsingPanel.End("Map.SettingsPanelOpen");
        }
        ImGui.Dummy(new SVector2(0, 3)); // panel spacing

        if (DXT.CollapsingPanel.Begin("Pin.SettingsPanelOpen", ref Settings.Pin.SettingsPanelCollapsed, new() { Label = "Pin Settings", Width = 0 })) {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, controlSpacing);
        
            Render_PinSettings("Pin.SettingsPanelOpen");
        
            ImGui.PopStyleVar();
            ImGui.Dummy(new SVector2(0, 0));
            DXT.CollapsingPanel.End("Pin.SettingsPanelOpen");
        }
        ImGui.Dummy(new SVector2(0, 3)); // panel spacing

        if (DXT.CollapsingPanel.Begin("Icons.SettingsPanelOpen", ref Settings.Icons.SettingsPanelCollapsed, new() { Label = "Icon Settings", Width = 0 })) {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, controlSpacing);
        
            Draw_IconSettings("Icons.SettingsPanelOpen");
        
            ImGui.PopStyleVar();
            ImGui.Dummy(new SVector2(0, 0));
            DXT.CollapsingPanel.End("Icons.SettingsPanelOpen");
        }
        ImGui.Dummy(new SVector2(0, 3)); // panel spacing

        Draw_IconsSettings();

        PopStrippedStyles();
    }


    private static void PushStrippedStyles() {

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, SVector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, SVector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, SVector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, SVector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 10);

        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, SColor.FromArgb(0, 0, 0, 20).ToImGui());
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, DXT.Colors.Button.ToImGui());
        //ImGui.PushStyleColor(ImGuiCol.WindowBg, SColor.Transparent.ToImGui());
        ImGui.PushStyleColor(ImGuiCol.FrameBg, SColor.Transparent.ToImGui());
        ImGui.PushStyleColor(ImGuiCol.Border, SColor.Transparent.ToImGui());
        ImGui.PushStyleColor(ImGuiCol.Text, DXT.Colors.Text.ToImGui());
    }
    private static void PopStrippedStyles() {
        ImGui.PopStyleVar(8);
        ImGui.PopStyleColor(5);
    }

    //--| Render |-----------------------------------------------------------------------------------------------------
    public void Render() {

        //RenderSettingsWindow();
        RenderPinWindow();
    }

    private static readonly SVector2 controlSpacing = new(3,3);
    private static readonly SVector2 conSize = new(120,20);
    private static readonly int controlWidth = (int)conSize.X;
    private static readonly int controlHeight = (int)conSize.Y;
    private static readonly int controlx2Width = controlWidth + (int)controlSpacing.X + controlWidth;
    private static readonly int controlx3Width = controlx2Width + (int)controlSpacing.X + controlWidth;
    private static readonly int controlx4Width = controlx3Width + (int)controlSpacing.X + controlWidth;
    private static readonly int controlx5Width = controlx4Width + (int)controlSpacing.X + controlWidth;
    private static readonly int controlx6Width = controlx5Width + (int)controlSpacing.X + controlWidth;
    private static readonly int checkboxLabelWidth = controlWidth - (int)controlSpacing.X - controlHeight;

    private static readonly DXT.Label.Options checkboxLabelOptions = new() { Width = checkboxLabelWidth, Height = controlHeight };
    private static readonly DXT.ColorSelect.Options colorSelectOptions = new() { Width = controlHeight, Height = controlHeight };

    private int SettingWindowHeight = 100;
    private readonly DXT.Window.Options SettingWindowOptions = new() {
        Title = $"{DXT.PluginName}: Settings",
        Resizable = true,
        MinWidth = 623
    };
    public void RenderSettingsWindow() {
        SettingWindowOptions.LockHeight = SettingWindowHeight;
        if (DXT.Window.Begin($"{DXT.PluginName}SettingWindow", ref Settings.SettingsWindowOpen, SettingWindowOptions)) {
            var windowPosition = ImGui.GetCursorScreenPos();
            ImGui.Indent(3);

            //if (DXT.CollapsingPanel.Begin("SettingsPanelOpen", ref Settings.SettingsPanelCollapsed, new() { Label = "Settings", Width = -3 })) {
            //    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, controlSpacing);
            //
            //    Render_GeneralSettings("GeneralSettingsPanel");
            //
            //    ImGui.PopStyleVar();
            //    ImGui.Dummy(new SVector2(0, 0));
            //    DXT.CollapsingPanel.End("SettingsPanelOpen");
            //}
            //ImGui.Dummy(new SVector2(0, 3)); // panel spacing

            //if (DXT.CollapsingPanel.Begin("Map.SettingsPanelOpen", ref Settings.Map.SettingsPanelCollapsed, new() { Label = "Map Settings", Width = -3 })) {
            //    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, controlSpacing);
            //
            //    Render_MapSettings("Map.SettingsPanelOpen");
            //
            //    ImGui.PopStyleVar();
            //    ImGui.Dummy(new SVector2(0, 0));
            //    DXT.CollapsingPanel.End("Map.SettingsPanelOpen");
            //}
            //ImGui.Dummy(new SVector2(0, 3)); // panel spacing

            //if (DXT.CollapsingPanel.Begin("Pin.SettingsPanelOpen", ref Settings.Pin.SettingsPanelCollapsed, new() { Label = "Pin Settings", Width = -3 })) {
            //    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, controlSpacing);
            //
            //    Render_PinSettings("Pin.SettingsPanelOpen");
            //
            //    ImGui.PopStyleVar();
            //    ImGui.Dummy(new SVector2(0, 0));
            //    DXT.CollapsingPanel.End("Pin.SettingsPanelOpen");
            //}
            //ImGui.Dummy(new SVector2(0, 3)); // panel spacing
            //
            //if (DXT.CollapsingPanel.Begin("Icons.SettingsPanelOpen", ref Settings.Icons.SettingsPanelCollapsed, new() { Label = "Icon Settings", Width = -3 })) {
            //    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, controlSpacing);
            //
            //    Render_IconSettings("Icons.SettingsPanelOpen");
            //
            //    ImGui.PopStyleVar();
            //    ImGui.Dummy(new SVector2(0, 0));
            //    DXT.CollapsingPanel.End("Icons.SettingsPanelOpen");
            //}
            //ImGui.Dummy(new SVector2(0, 3)); // panel spacing
            //
            //RenderIconsSettings();

            ImGui.Unindent(3);
            SettingWindowHeight = SettingWindowOptions.TitleBarHeight + (int)( ImGui.GetCursorScreenPos().Y - windowPosition.Y );
            DXT.Window.End();
        }
    }

    private int PinWindowHeight = 100;
    private readonly DXT.Window.Options PinWindowOptions = new() {
        Title = $"{DXT.PluginName}: Pin Editor",
        Resizable = true,
        MinWidth = 700
    };
    public void RenderPinWindow() {

        PinWindowOptions.LockHeight = PinWindowHeight;
        if (DXT.Window.Begin($"{DXT.PluginName}PinWindow", ref Settings.Pin.EditorWindowOpen, PinWindowOptions)) {
            var windowPosition = ImGui.GetCursorScreenPos();
            ImGui.Indent(3);

            var panelID = "Pin.EditorMainPanel";
            DXT.Panel.Begin(panelID, new() { Width = -3 });
            {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, controlSpacing);

                RenderPinEditor_MainSettings(panelID);

                ImGui.PopStyleVar();
                ImGui.Dummy(new SVector2(0, 0));
                DXT.CollapsingPanel.End(panelID);
            }
            ImGui.Dummy(new SVector2(0, 3)); // panel spacing

            panelID = "EditorCurrentAreaPanel";
            if (DXT.CollapsingPanel.Begin(panelID, ref Settings.Pin.EditorCurrentAreaPanelCollapsed, new() { Label = $"Current Area: <{Plugin.AreaManager.AreaID}> Pins", Width = -3 })) {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, controlSpacing);

                RenderPinEditor_CurrentAreaSettings(panelID);

                ImGui.PopStyleVar();
                ImGui.Dummy(new SVector2(0, 0));
                DXT.CollapsingPanel.End(panelID);
            }
            ImGui.Dummy(new SVector2(0, 3)); // panel spacing

            // --| COMMON AREA PANEL |-----------------------------------------------------------------------------------------
            panelID = "EditorCommonAreasPanel";
            if (DXT.CollapsingPanel.Begin(panelID, ref Settings.Pin.EditorCommonAreasPanelCollapsed, new() { Label = "Common Pins", Width = -3 })) {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, controlSpacing);

                RenderPinEditor_CommonSettings(panelID);

                ImGui.PopStyleVar();
                ImGui.Dummy(new SVector2(0, 0));
                DXT.CollapsingPanel.End(panelID);
            }
            ImGui.Dummy(new SVector2(0, 3)); // panel spacing

            // --| SELECTED AREAS PANEL |-----------------------------------------------------------------------------------------
            panelID = "EditorSelectedAreaPanel";
            if (DXT.CollapsingPanel.Begin(panelID, ref Settings.Pin.EditorSelectedAreaPanelCollapsed, new() { Label = "Selected Pins", Width = -3 })) {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, controlSpacing);

                RenderPinEditor_SelectedSettings(panelID);

                ImGui.PopStyleVar();
                ImGui.Dummy(new SVector2(0, 0));
                DXT.CollapsingPanel.End(panelID);
            }
            ImGui.Dummy(new SVector2(0, 3)); // panel spacing

            ImGui.Unindent(3);
            PinWindowHeight = PinWindowOptions.TitleBarHeight + (int)( ImGui.GetCursorScreenPos().Y - windowPosition.Y );
            DXT.Window.End();
        }
    }

    //--| General Settings |-----------------------------------------------------------------------------------------------------------------------
    private void Draw_GeneralSettings(string panelID) {

        DXT.Checkbox.Draw("DrawOverPoePanels", ref Settings.DrawOverPanels, new() {
            Height = controlHeight,
            Tooltip = new("Draw over poe panels, only working for some panels missing game offsets for others")
        });
        ImGui.SameLine();
        DXT.Label.Draw("Over Panels", checkboxLabelOptions);

        ImGui.SameLine();
        DXT.Checkbox.Draw("DrawPoeBackground", ref Settings.DrawWhenPOEForegroundOnly, new() {
            Height = controlHeight,
            Tooltip = new("Only draw when POE is the active window (focused).")
        });
        ImGui.SameLine();
        DXT.Label.Draw("Focus only", checkboxLabelOptions);

        ImGui.SameLine();
        DXT.Checkbox.Draw("CenterOffset", ref Settings.DXTSettings.LargeMapCenterFix, new() {
            Height = controlHeight,
            Tooltip = new("Fix LargeMap centering",
            "you can check in game if you need this by opening the debug monitor\n"
          + "and checking the map center is exactly half your poe game window size")
        });
        ImGui.SameLine();
        DXT.Label.Draw("Map Fix", checkboxLabelOptions);

        //ImGui.SameLine();
        //DXT.Slider.Draw("MapScaleModifier", ref Settings.DXTSettings.MapScaleModifier, new() {
        //    Width = controlWidth, Height = controlHeight, Min = 0, Max = 5, Step = .01f, ShiftStep = .001f,
        //    Tooltip = new DXT.Tooltip.Options {
        //        Lines = [
        //        new DXT.Tooltip.Title { Text = "Map Scale Multiplier" },
        //            new DXT.Tooltip.Separator{ },
        //            new DXT.Tooltip.Description { Text = "Adjust this value so your icons and map sit perfectly on the in game map" },
        //            new DXT.Tooltip.DoubleLine  { LeftText = "mousewheel", RightText = "asjust value +/- .01" },
        //            new DXT.Tooltip.DoubleLine  { LeftText = "shift+mousewheel", RightText = "asjust value +/- .001" },
        //        ]
        //    }
        //});

        //ImGui.SameLine();
        //if (DXT.Button.Draw("ResetMapScaleModifier", new DXT.Button.Options { Label = "Reset", Width = controlWidth, Height = controlHeight, Tooltip = new("Reset Map Scale Modifier, default is a known value for 3440x1440") })) {
        //    Settings.DXTSettings.MapScaleModifier = 2.127031f;
        //}

        ImGui.SameLine();
        DXT.Button.Draw("DebugToolbar", ref Settings.DXTSettings.ShowTools ,new DXT.Button.Options { Label = "Debug", Width = controlWidth, Height = controlHeight, Tooltip = new("Show the Debug Toolbar/Tools") });

    }

    //--| Map Settings |---------------------------------------------------------------------------------------------------------------------------
    private void Render_MapSettings(string panelID) {
        DXT.Checkbox.Draw("Map.Enabled", ref Settings.Map.Enabled, new() {
            Height = controlHeight,
            Tooltip = new("Draw Area Map (Maphack)")
        });
        ImGui.SameLine();
        DXT.Label.Draw("Enabled", checkboxLabelOptions);

        ImGui.SameLine();
        DXT.ColorSelect.Draw("Map.Color", "Map Color", ref Settings.Map.Color, colorSelectOptions);
        ImGui.SameLine();
        DXT.Label.Draw("Map Color", checkboxLabelOptions);
    }

    //--| Pin Settings |---------------------------------------------------------------------------------------------------------------------------
    private void Render_PinSettings(string panelID) {
        DXT.Checkbox.Draw("Pin.Enabled", ref Settings.Pin.Enabled, new() {
            Height = controlHeight,
            Tooltip = new("Enable Pin Tracking, ie. Bosses, Area Transitions, quest locations")
        });
        ImGui.SameLine();
        DXT.Label.Draw("Enabled", checkboxLabelOptions);

        ImGui.SameLine();
        DXT.Checkbox.Draw("Pin.Paths", ref Settings.Pin.DrawPaths, new() {
            Height = controlHeight,
            Tooltip = new("Draw Paths, Draw Paths to Pins")
        });
        ImGui.SameLine();
        DXT.Label.Draw("Draw Paths", checkboxLabelOptions);

        ImGui.SameLine();
        DXT.Checkbox.Draw("Pin.OverrideColorPaths", ref Settings.Pin.OverrideColorPaths, new() {
            Height = controlHeight,
            Tooltip = new("Draw paths with a unique color per path, ignoring the colors set for each Pin. ")
        });
        ImGui.SameLine();
        DXT.Label.Draw("Color Cycle", checkboxLabelOptions);

        ImGui.SameLine();
        DXT.Slider.Draw("Pin.PathThickness", ref Settings.Pin.PathThickness, new() {
            Width = controlWidth, Height = controlHeight, Min = 1, Max = 5,
            Tooltip = new("Path Thickness"),
        });

        ImGui.SameLine();
        DXT.Button.Draw("Pin.EditorWindowOpen", ref Settings.Pin.EditorWindowOpen, new() { Label = "Pin Editor", Width = controlWidth, Height = controlHeight, Tooltip = new("Pin Editor", "Edit Pins") });

    }

    private void RenderPinEditor_MainSettings(string panelID) {

        RefreshFilesAndSelection();
        UpdateFileStatus();

        DXT.Label.Draw(_fileStatusText, new() { Width = -4, Height = controlHeight, TextColor = _fileStatusColor, DrawBG = true, PadLeft = 3, Tooltip = new("Status") });

        var menuID = $"{panelID}FileMenu";
        var newFileID = "NewPinFile";
        var importID = "ImportRadarPinFile";
        var renameFileID = "RenamePinFilePopup";
        if (DXT.Button.Draw($"{panelID}FileMenuButton", new() { Label = "File Menu", Width = controlWidth, Height = controlHeight, Tooltip = new("Open File Menu") })) {
            DXT.Menu.Open(menuID, [
                new() { Label = "New", OnClick = () => {
                    DXT.TextInputModal.Open(newFileID, "NewPinFile", new() { Title = "New Pin File", Tooltip = new("Enter new Pin filename excluding .json") } );
                }},
                new() { Label = "Open Folder", OnClick = () => {
                    DXT.Log("WTFFFFFF");
                    try {
                        Process.Start(new ProcessStartInfo {
                            FileName = Plugin.PinPath,
                            UseShellExecute = true,
                            Verb = "open"
                        });
                    }
                    catch (Exception ex) {
                        DXT.Log($"Failed to open directory: {ex.Message}", false);
                    }
                }},
                new() { Label = "Rename", Enabled = _loadedPins != null, OnClick = () => {
                    var renameFilename = Path.GetFileNameWithoutExtension(_loadedFilename);
                    if (!string.IsNullOrWhiteSpace(renameFilename)) {
                        DXT.TextInputModal.Open(renameFileID, renameFilename,
                            new DXT.TextInputModal.Options {
                                Title = "Rename Pin File",
                                Tooltip = new("Enter new Pin filename excluding .json")
                            }
                        );
                    }
                }},
                new() { Label = "Save", Enabled = _loadedPins != null && !string.IsNullOrEmpty(_loadedFilePath) && !string.IsNullOrEmpty(_loadedFilename), OnClick = () => {
                    File.WriteAllText(_loadedFilePath, JsonConvert.SerializeObject(_loadedPins, Formatting.Indented));
                    DXT.Log($"Pin file saved: {_loadedFilePath}", false);
                }},
                new() { Separator = true },
                new() { Label = "Import", Tooltip=new("Import radar important_tgt_files.txt from clipboard (open your radar important_tgt_files.txt ctra+a and copy into clipboard)"), OnClick = () => {
                    DXT.TextInputModal.Open(importID, "RadarImportedPins", new DXT.TextInputModal.Options {
                        Title = "Import Radars important_tgt_file from clipboard",
                        Tooltip = new("Enter new filename excluding .json")
                    });
                }},
            ]);
        }
        DXT.Menu.Draw(menuID);

        var result = DXT.TextInputModal.Draw(newFileID);
        if (result is { ok: true, value: var newBaseFilename }) {
            if (!string.IsNullOrWhiteSpace(newBaseFilename)) {
                DXT.Deferred.Enqueue(() => {
                    string uniquePath = GetUniqueFilePath(Plugin.PinPath, newBaseFilename, ".json");
                    File.WriteAllText(uniquePath, "{}");
                    Settings.Pin.SelectedFilename = Path.GetFileName(uniquePath);
                });
            }
        }

        result = DXT.TextInputModal.Draw(importID);
        if (result is { ok: true, value: var importBaseFilename }) {
            if (!string.IsNullOrWhiteSpace(importBaseFilename)) {
                DXT.Deferred.Enqueue(() => { ImportRadarPinFromClipboard(importBaseFilename); });
            }
        }

        result = DXT.TextInputModal.Draw(renameFileID);
        if (result is { ok: true, value: var renameBaseFilename }) {
            if (!string.IsNullOrWhiteSpace(renameBaseFilename)) {
                DXT.Deferred.Enqueue(() => {
                    string uniquePath = GetUniqueFilePath(Plugin.PinPath, renameBaseFilename, ".json");
                    var oldFilePath = Path.Combine(Plugin.PinPath, _loadedFilename);
                    File.Move(oldFilePath, uniquePath);
                    Settings.Pin.SelectedFilename = Path.GetFileName(uniquePath);
                });
            }
        }

        ImGui.SameLine();
        if (DXT.Select.Draw($"{panelID}EditorSelect", ref SelectedFileIndex, new() { Width = -controlWidth - (int)controlSpacing.X - 3, Height = controlHeight, Items = PinDirFiles, Tooltip = new("Select Pin File to edit") })) {
            DXT.Deferred.Enqueue(() => {
                if (PinDirFiles.Count > 0 && SelectedFileIndex >= 0)
                    Settings.Pin.SelectedFilename = PinDirFiles[SelectedFileIndex];
                else
                    Settings.Pin.SelectedFilename = null;
                LoadSelectedPinFile();
            });
        }

        ImGui.SameLine();
        if (DXT.Button.Draw($"{panelID}SaveFile", new() { Label = "Save", Width = controlWidth, Height = controlHeight, Enabled = _loadedPins != null && !string.IsNullOrEmpty(_loadedFilePath) && !string.IsNullOrEmpty(_loadedFilename), Tooltip = new("Save Pin File") })) {
            File.WriteAllText(_loadedFilePath, JsonConvert.SerializeObject(_loadedPins, Formatting.Indented));
            DXT.Log($"Pin file saved: {_loadedFilePath}", false);
        }

        DXT.Button.Draw($"{panelID}ShowTiles", ref Settings.Pin.ShowTiles, new() { Label = "Show Tiles", Width = controlWidth, Height = controlHeight, Tooltip = new("Draw Filtered Tile paths to world, mouse over in world tilepaths to see more information about them") });

        ImGui.SameLine();
        DXT.Checkbox.Draw("IgnoreTerrainHeight", ref Settings.Pin.IgnoreTerrainHeight, new() { Height=controlHeight, Tooltip = new("Grid Alignment","Ignore terrain height when laying out tile grid?") });

        ImGui.SameLine();
        DXT.Slider.Draw($"{panelID}TileCountFilter", ref Settings.Pin.TilePositionFreqFilter, new() { Width = controlWidth, Height = controlHeight, Min = 0, Max = 10, Tooltip = new("Tile path frequency Filter", "Filter tile paths by usage count, 0 removes this filter") });

        ImGui.SameLine();
        DXT.Input.Draw($"{panelID}TileStringFilter", ref Settings.Pin.TileFilter, new() { Width = -3, Height = controlHeight, Tooltip = new("Tile path text filter") });

        var area = Core.States.InGameStateObject.CurrentAreaInstance;
        if (area != null && area.TgtTilesLocations.Count > 0) {
            var filteredTilePairs = area.TgtTilesLocations
                .Where(tile => tile.Key.Contains(Settings.Pin.TileFilter, StringComparison.OrdinalIgnoreCase) &&
                (Settings.Pin.TilePositionFreqFilter == 0 || tile.Value.Count <= Settings.Pin.TilePositionFreqFilter))
                .ToList(); // List<KeyValuePair<string, List<Vector2>>>
            Plugin.PinRenderer.EditorFilteredTiles = filteredTilePairs;
            //var filteredTileKeys = filteredTilePairs.Select(tile => tile.Key).ToList();
            //if (_selectTileIndex < 0 || _selectTileIndex >= filteredTileKeys.Count) _selectTileIndex = 0;
            //if (DXT.Select.Draw($"{panelID}TileSelecter", ref _selectTileIndex, new() { Width = -3, Height = controlHeight, Items = filteredTileKeys, Tooltip = new("Filter Tile Path", "Select Tile path to copy to clipboard") })) {
            //    var selectedTileAssetPath = filteredTileKeys[_selectTileIndex];
            //    if (!string.IsNullOrEmpty(selectedTileAssetPath)) ImGui.SetClipboardText(selectedTileAssetPath);
            //}
        }

    }
    private void RenderPinEditor_CurrentAreaSettings(string panelID) {
        if (_loadedPins == null) {
            DXT.Label.Draw("No Pin File loaded", new() { Width = controlx2Width, Height = controlHeight, TextColor = DXT.Colors.TextRed, PadLeft = 3 });
            return;
        }

        var PinGroupName = Plugin.AreaManager.AreaID;
        List<Pin>? PinGroupList;
        if (_loadedPins.Count < 1 || !_loadedPins.TryGetValue(PinGroupName, out PinGroupList) || PinGroupList == null || PinGroupList.Count < 1) {
            if (DXT.Button.Draw($"{panelID}NewPin", new() { Label = "New", Width = controlWidth, Height = controlHeight, Tooltip = new("Add a new Pin to current area") })) {
                AddPin(PinGroupName, new Pin());
            }

            ImGui.SameLine();
            if (_loadedPins.Count < 1)
                DXT.Label.Draw("No Pins in File", new() { Width = controlx2Width, Height = controlHeight, TextColor = DXT.Colors.TextYellow, PadLeft = 3 });
            else
                DXT.Label.Draw("No Pins in Area", new() { Width = controlx2Width, Height = controlHeight, TextColor = DXT.Colors.TextYellow, PadLeft = 3 });

            return;
        }

        for (int pinIndex = 0; pinIndex < PinGroupList.Count; pinIndex++) {
            RenderPin(PinGroupName, pinIndex);
        }

        ImGui.Dummy(new SVector2((int)ImGui.GetContentRegionAvail().X - controlWidth - controlSpacing.X - controlSpacing.X, 0));
        ImGui.SameLine();
        if (DXT.Button.Draw($"{panelID}NewPin", new() { Label = "New", Width = controlWidth, Height = controlHeight, Tooltip = new("Add a new Pin to current area") })) {
            AddPin(PinGroupName, new Pin());
        }
    }
    private void RenderPinEditor_CommonSettings(string panelID) {

        if (_loadedPins == null) {
            DXT.Label.Draw("No Pin File loaded", new() { Width = controlx2Width, Height = controlHeight, TextColor = DXT.Colors.TextRed, PadLeft = 3 });
            return;
        }

        var pinGroupName = "*";
        var newButtonOptions = new DXT.Button.Options { Label = "New", Width = controlWidth, Height = controlHeight, Tooltip = new("Add a new common Pin") };

        List<Pin>? pinGroupList;
        if (_loadedPins.Count < 1 || !_loadedPins.TryGetValue(pinGroupName, out pinGroupList) || pinGroupList == null || pinGroupList.Count < 1) {
            if (DXT.Button.Draw($"{panelID}NewPin", newButtonOptions)) {
                AddPin(pinGroupName, new Pin());
            }

            ImGui.SameLine();
            if (_loadedPins.Count < 1)
                DXT.Label.Draw("No Pins in File", new() { Width = controlx2Width, Height = controlHeight, TextColor = DXT.Colors.TextYellow, PadLeft = 3 });
            else
                DXT.Label.Draw("No common Pins in File", new() { Width = controlx2Width, Height = controlHeight, TextColor = DXT.Colors.TextYellow, PadLeft = 3 });

            return;
        }

        for (int PinIndex = 0; PinIndex < pinGroupList.Count; PinIndex++) {
            RenderPin(pinGroupName, PinIndex);
        }

        ImGui.Dummy(new SVector2((int)ImGui.GetContentRegionAvail().X - controlWidth - controlSpacing.X - controlSpacing.X, 0));
        ImGui.SameLine();
        if (DXT.Button.Draw($"{panelID}NewPin", newButtonOptions)) {
            AddPin(pinGroupName, new Pin());
        }

    }
    private void RenderPinEditor_SelectedSettings(string panelID) {

        if (_loadedPins == null) {
            DXT.Label.Draw("No Pin File loaded", new() { Width = controlx2Width, Height = controlHeight, TextColor = DXT.Colors.TextRed, PadLeft = 3 });
            return;
        }
        var regexTooltip = new DXT.Tooltip.Options() {
            Lines = [
            new DXT.Tooltip.Title { Text = "xxx" },
            new DXT.Tooltip.Separator(),
            new DXT.Tooltip.Description { Text = "Supports wildcards and regex-like patterns:" },
            new DXT.Tooltip.DoubleLine { LeftText = "*", RightText = "matches any sequence of characters" },
            new DXT.Tooltip.DoubleLine { LeftText = "?", RightText = "matches any single character" },
            new DXT.Tooltip.Description { Text = "Examples:" },
            new DXT.Tooltip.DoubleLine { LeftText = "Sanctum*", RightText = "matches any area starting with 'Sanctum'" },
            new DXT.Tooltip.DoubleLine { LeftText = "*Boss*", RightText = "matches any area containing 'Boss'" },
            new DXT.Tooltip.DoubleLine { LeftText = "*Town", RightText = "matches any area ending with 'Town'" },
            new DXT.Tooltip.DoubleLine { LeftText = "Act?Boss", RightText = "matches 'Act1Boss', 'Act2Boss', etc." },
            new DXT.Tooltip.Description { Text = "You can also use exact area names or regex-like patterns." },
            new DXT.Tooltip.Description { Text = "Your pattern will be matched against the area name." },
        ]
        };


        var newButtonOptions = new DXT.Button.Options { Label = "New", Width = controlWidth, Height = controlHeight, Tooltip = new("Add a new Pin") };
        regexTooltip.Lines[0] = new DXT.Tooltip.Title { Text = "Add new group of Pins" };
        var newGroupButtonOptions = new DXT.Button.Options { Label = "New Group", Width = controlWidth, Height = controlHeight, Tooltip = regexTooltip };
        var newGroupModalID = "NewPinGroup";
        regexTooltip.Lines[0] = new DXT.Tooltip.Title { Text = "New Pin Group" };
        var newGroupModalOptions = new DXT.TextInputModal.Options { Title = "New Pin Group", Tooltip = regexTooltip };
        void newGroup(string? newGroupName) {
            if (!string.IsNullOrWhiteSpace(newGroupName)) {
                DXT.Deferred.Enqueue(() => {
                    newGroupName = GetUniqueGroupName(newGroupName);
                    AddPin(newGroupName, new Pin());
                    selectedAreaIndex = RebuildPinGroupKeys(newGroupName);
                });
            }
        }

        if (_loadedPins.Count < 1) {
            if (DXT.Button.Draw($"{panelID}NewGroup", newGroupButtonOptions)) {
                DXT.TextInputModal.Open(newGroupModalID, "NewGroup", newGroupModalOptions);
            }
            var _result = DXT.TextInputModal.Draw(newGroupModalID);
            if (_result is { ok: true, value: var _newGroupName }) { newGroup(_newGroupName); }


            ImGui.SameLine();
            DXT.Label.Draw("No Pins in File", new() { Width = controlx2Width, Height = controlHeight, TextColor = DXT.Colors.TextYellow, PadLeft = 3 });

            return;
        }

        selectedAreaIndex = RebuildPinGroupKeys();
        var selectedPinGroupName = _pinGroupKeys[selectedAreaIndex];
        var selectedPinGroupList = _loadedPins[selectedPinGroupName];

        DXT.Select.Draw("AreaSelecter", ref selectedAreaIndex, new() { Width = controlx2Width, Height = controlHeight, Items = _pinGroupKeys, Tooltip = new("Select Area edit") });

        ImGui.SameLine();
        if (DXT.Button.Draw($"{panelID}NewGroup", newGroupButtonOptions)) {
            DXT.TextInputModal.Open(newGroupModalID, "NewGroup", newGroupModalOptions);
        }
        var result = DXT.TextInputModal.Draw(newGroupModalID);
        if (result is { ok: true, value: var newGroupName }) { newGroup(newGroupName); }

        ImGui.SameLine();
        if (DXT.Button.Draw($"{panelID}DeleteGroup", new() { Label = "Delete Group", Width = controlWidth, Height = controlHeight, Tooltip = new("Delete selected Pin group and its Pins, Ctrl+Click to delete") })) {
            if (ImGui.GetIO().KeyCtrl) {
                DXT.Deferred.Enqueue(() => { RemovePinGroup(selectedPinGroupName); });
            }
        }

        ImGui.SameLine();
        if (DXT.Button.Draw($"{panelID}RenameGroup", new() { Label = "Rename Group", Width = controlWidth, Height = controlHeight, Tooltip = new("Rename selected Pin group") })) {
            DXT.TextInputModal.Open("renameGroupModalID", selectedPinGroupName,
                new DXT.TextInputModal.Options { Title = "Rename Pin Group", Tooltip = new("Rename Pin group") }
            );
        }
        result = DXT.TextInputModal.Draw("renameGroupModalID");
        if (result is { ok: true, value: var renameGroupName }) {
            DXT.Deferred.Enqueue(() => RenamePinGroup(selectedPinGroupName, renameGroupName));
        }


        if (selectedPinGroupList.Count == 0) {
            if (DXT.Button.Draw($"{panelID}NewPin", newButtonOptions)) {
                DXT.Deferred.Enqueue(() => AddPin(selectedPinGroupName, new Pin()));
            }

            ImGui.SameLine();
            DXT.Label.Draw("No Pins for area", new() { Width = controlx2Width, Height = controlHeight, TextColor = DXT.Colors.TextYellow, PadLeft = 3 });
        } else {
            for (int pinIndex = 0; pinIndex < selectedPinGroupList.Count; pinIndex++) RenderPin(selectedPinGroupName, pinIndex);

            ImGui.Dummy(new SVector2((int)ImGui.GetContentRegionAvail().X - controlWidth - controlSpacing.X - controlSpacing.X, 0));
            ImGui.SameLine();
            if (DXT.Button.Draw($"{panelID}NewPin", newButtonOptions)) {
                DXT.Deferred.Enqueue(() => AddPin(selectedPinGroupName, new Pin()));
            }
        }

    }
    private void RenderPin(string pinGroupName, int pinIndex) {
        if (_loadedPins == null) return;
        if (!_loadedPins.TryGetValue(pinGroupName, out var pinGroupList)) return;
        if (pinIndex < 0 || pinIndex >= pinGroupList.Count) return;

        var pin = pinGroupList[pinIndex];
        var widgetID = $"{pinGroupName}_{pinIndex}";

        DXT.Checkbox.Draw($"{widgetID}Enabled", ref pin.Enabled, new() { Height = controlHeight, Tooltip = new("Enable Pin") });

        ImGui.SameLine();
        DXT.ColorSelect.Draw($"{widgetID}BGColor", "Pin BG Color", ref pin.BGColor, colorSelectOptions);

        ImGui.SameLine();
        DXT.ColorSelect.Draw($"{widgetID}Color", "Pin Text Color", ref pin.TextColor, colorSelectOptions);

        ImGui.SameLine();
        DXT.Slider.Draw($"{widgetID}ExpectedCount", ref pin.ExpectedCount, new() { Width = 40, Height = controlHeight, Min = 1, Max = 10, Tooltip = new("Expected Count", "Expected count of this Pin in area") });

        ImGui.SameLine();
        var availWidth = (int)ImGui.GetContentRegionAvail().X - ((int)controlSpacing.X + controlHeight + (int)controlSpacing.X);
        var nameWidth = (int)(availWidth * .32f);
        var pathWidth = availWidth - nameWidth - (int)controlSpacing.X;
        DXT.Input.Draw($"{widgetID}Label", ref pin.Label, new() { Width = nameWidth, Height = controlHeight, BackgroundColor = pin.BGColor, TextColor = pin.TextColor, Tooltip = new("Pin Label", pin.Label) });

        ImGui.SameLine();
        DXT.Label.Draw(pin.Path, new() {
            Width = pathWidth, Height = controlHeight, DrawBG = true, PadLeft = 3,
            Tooltip = new() { Lines = new() {
                new DXT.Tooltip.Title { Text = "Pin tile path" },
                new DXT.Tooltip.Separator(),
                new DXT.Tooltip.Description { Text = pin.Path },
                new DXT.Tooltip.DoubleLine { LeftText = "ctrl+leftclick:", RightText = $"Copy Path" },
                new DXT.Tooltip.DoubleLine { LeftText = "ctrl+rightclick:", RightText = $"Paste Path" },
                new DXT.Tooltip.DoubleLine { LeftText = "mousewheel:", RightText = $"increment/decrement tile number _(01-08)" },
            }}
        });
        if (ImGui.IsItemHovered()) {
            float wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0) {
                var match = TileNumberRegex.Match(pin.Path);
                if (match.Success) {
                    int number = int.Parse(match.Groups[1].Value);
                    number += Math.Sign(wheel); // +1 or -1 per wheel tick
                    number = Math.Clamp(number, 1, 8);
                    // Replace the old number in the path with the new one, keeping leading zero
                    string newNumberStr = number.ToString("D2");
                    pin.Path = TileNumberRegex.Replace(pin.Path, $"_{newNumberStr}.tdt");
                }
            }
        }
        if (DXT.Keyboard.IsKeyDown(DXT.Keyboard.Keys.ControlKey)) {
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
                CopyPath(pin.Path);
            } else if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                if (!string.IsNullOrEmpty(_copiedPath)) {
                    UpdatePinPath(pinGroupName, pinIndex, _copiedPath);
                }
            }
        }

        ImGui.SameLine();
        if (DXT.Button.Draw($"{widgetID}MenuButton", new() { Label = "M", Width = controlHeight, Height = controlHeight, Tooltip = new("Pin Menu") })) {
            DXT.Menu.Open($"{widgetID}Menu", [
                new() { Label = "Copy", OnClick = () => { CopyPin(pin); } },
                new() { Label = "Paste", Enabled = _copiedPin != null, OnClick = () => { PastePin(pin); } },
                new() { Separator = true },
                new() { Label = "Copy Path", OnClick = () => { CopyPath(pin.Path); }},
                new() { Label = "Paste Path", Enabled = !string.IsNullOrEmpty(_copiedPath),  OnClick = () => {
                    UpdatePinPath(pinGroupName, pinIndex, _copiedPath);
                }},
                new() { Separator = true },
                new() { Label = "Move Up", Enabled = pinIndex > 0, OnClick = () => {
                    SwapPin(pinGroupName, pinIndex, pinIndex - 1);
                    }
                },
                new() { Label = "Move Down", Enabled = pinIndex >= 0 && pinIndex < pinGroupList.Count - 1, OnClick = () => {
                    SwapPin(pinGroupName, pinIndex, pinIndex + 1);
                    }
                },
                new() { Separator = true },
                new() { Label = "Duplicate", OnClick = () => {

                    AddPin(pinGroupName, pin with { }, pinIndex + 1);
                }},
                new() { Label = "Delete", RightLabel="Hold Ctrl", OnClick = () => {
                    if (ImGui.GetIO().KeyCtrl) RemovePin(pinGroupName,pinIndex);
                }},
            ]);
        }
        DXT.Menu.Draw($"{widgetID}Menu");
    }

    private void AddPin(string pinGroupName, Pin pin, int? index = null) {
        if (_loadedPins == null) return;
        if (!_loadedPins.ContainsKey(pinGroupName)) _loadedPins[pinGroupName] = new List<Pin>();
        var pinList = _loadedPins[pinGroupName];
        if (index.HasValue && index.Value >= 0 && index.Value <= pinList.Count) {
            pinList.Insert(index.Value, pin);
        } else {
            pinList.Add(pin);
        }
        _pinDirty = true;
    }
    private void SwapPin(string pinGroupName, int indexA, int indexB) {
        if (_loadedPins == null) return;
        if (!_loadedPins.TryGetValue(pinGroupName, out var pinList)) return;
        if (indexA < 0 || indexB < 0 || indexA >= pinList.Count || indexB >= pinList.Count) return;
        if (indexA == indexB) return;
        // Only swap if the Pins are actually different
        if (!ReferenceEquals(pinList[indexA], pinList[indexB])) {
            (pinList[indexA], pinList[indexB]) = (pinList[indexB], pinList[indexA]);
            _pinDirty = true;
        }
    }
    private void RemovePin(string pinGroupName, int index) {
        if (_loadedPins == null) return;
        if (_loadedPins.TryGetValue(pinGroupName, out var pinList) && index >= 0 && index < pinList.Count) {
            pinList.RemoveAt(index);
            _pinDirty = true;
        }
    }
    private void UpdatePin(string pinGroupName, int index, Pin newPin) {
        if (_loadedPins == null) return;
        if (_loadedPins.TryGetValue(pinGroupName, out var pinList) && index >= 0 && index < pinList.Count) {
            pinList[index] = newPin;
            _pinDirty = true;
        }
    }
    private void UpdatePinPath(string pinGroupName, int index, string path) {
        if (_loadedPins == null) return;
        if (!_loadedPins.TryGetValue(pinGroupName, out var pinList)) return;
        if (index < 0 || index >= pinList.Count) return;
        if (pinList[index].Path != path) {
            pinList[index].Path = path;
            _pinDirty = true;
        }
    }
    private void AddPinGroup(string pinGroup) {
        if (_loadedPins == null) return;
        if (!_loadedPins.ContainsKey(pinGroup)) {
            _loadedPins[pinGroup] = new List<Pin>();
            _pinDirty = true;
        }
    }
    private void RemovePinGroup(string groupName) {
        if (_loadedPins == null) return;
        if (_loadedPins.ContainsKey(groupName)) {
            _loadedPins.Remove(groupName);
            _pinDirty = true;
        }
    }
    private bool RenamePinGroup(string? oldName, string? newName) {
        if (_loadedPins == null) return false;
        if (oldName == null || string.IsNullOrEmpty(newName)) return false;
        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase)) return false;
        if (_loadedPins.ContainsKey(oldName) && !_loadedPins.ContainsKey(newName)) {
            var pinList = _loadedPins[oldName];
            _loadedPins.Remove(oldName);
            _loadedPins[newName] = pinList;
            _pinDirty = true;
            return true;
        }
        return false;
    }
    private void CopyPin(Pin pin) => _copiedPin = pin with { };
    private Pin? _copiedPin = null;
    private void PastePin(Pin pin) {
        if (_copiedPin != null) {
            pin.PasteFrom(_copiedPin);
            _pinDirty = true;
        }
    }

    private String? _copiedPath = null;
    public void CopyPath(String path) {
        if (string.IsNullOrEmpty(path)) return;
        if (!path.StartsWith("Metadata")) return;
        _copiedPath = path;
        DXT.Log($"Copied path: {path}");
    }
    private void ImportRadarPinFromClipboard(string newBaseFilename) {
        if (string.IsNullOrWhiteSpace(newBaseFilename)) {
            DXT.Log("Import failed: Filename is empty.", false);
            return;
        }

        string json = ImGui.GetClipboardText();
        if (string.IsNullOrWhiteSpace(json)) {
            DXT.Log("Import failed: Clipboard is empty.", false);
            return;
        }

        try {
            var oldData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
            if (oldData == null) {
                DXT.Log("Import failed: Could not parse old Pin data.", false);
                return;
            }

            var newData = new Dictionary<string, List<Pin>>();
            foreach (var area in oldData) {
                var pinList = new List<Pin>();
                foreach (var kv in area.Value) {
                    pinList.Add(new Pin {
                        Path = kv.Key,
                        Label = kv.Value,
                    });
                }
                newData[area.Key] = pinList;
            }

            string uniquePath = GetUniqueFilePath(Plugin.PinPath, newBaseFilename, ".json");
            File.WriteAllText(uniquePath, JsonConvert.SerializeObject(newData, Formatting.Indented));
            Settings.Pin.SelectedFilename = Path.GetFileName(uniquePath);
            DXT.Log($"Imported Pins saved to: {uniquePath}", false);
        }
        catch (Exception ex) {
            DXT.Log($"Import failed: {ex.Message}", false);
        }
    }
    public void RefreshFilesAndSelection() {
        // Refresh file list
        PinDirFiles = Directory.GetFiles(Plugin.PinPath, "*.json", SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetFileName(f)!)
            .OrderBy(x => x)
            .ToList();
        // Track previous selection
        string? prevFile = Settings.Pin.SelectedFilename;
        int prevIndex = SelectedFileIndex;
        // If nothing is selected or the selected file is missing, select the first file (or null)
        if (string.IsNullOrEmpty(Settings.Pin.SelectedFilename) || !PinDirFiles.Contains(Settings.Pin.SelectedFilename))
            Settings.Pin.SelectedFilename = PinDirFiles.Count > 0 ? PinDirFiles[0] : null;
        // Sync index to file
        SelectedFileIndex = PinDirFiles.IndexOf(Settings.Pin.SelectedFilename ?? "");
        // If index is still out of range, clamp it and update file name
        if (SelectedFileIndex < 0 || SelectedFileIndex >= PinDirFiles.Count) {
            SelectedFileIndex = PinDirFiles.Count > 0 ? 0 : -1;
            Settings.Pin.SelectedFilename = ( SelectedFileIndex >= 0 ) ? PinDirFiles[SelectedFileIndex] : null;
        }
        // Only reload if selection changed
        if (Settings.Pin.SelectedFilename != prevFile || SelectedFileIndex != prevIndex)
            LoadSelectedPinFile();

        _loadedFilename = Settings.Pin.SelectedFilename;
        _loadedFilePath = string.IsNullOrEmpty(_loadedFilename) ? "" : Path.Combine(Plugin.PinPath, _loadedFilename);
        _loadedPins = plugin.PinRenderer.LoadedPins;
    }
    private void LoadSelectedPinFile() {
        var fileName = Settings.Pin.SelectedFilename;
        if (!string.IsNullOrEmpty(fileName)) {
            var filePath = Path.Combine(Plugin.PinPath, fileName);
            if (File.Exists(filePath)) {
                try {
                    string json = File.ReadAllText(filePath);
                    // If the file is empty, treat as empty dictionary
                    if (string.IsNullOrWhiteSpace(json)) {
                        plugin.PinRenderer.LoadedPins = new Dictionary<string, List<Pin>>();
                        DXT.Log($"Loaded empty Pin file: {filePath}", false);
                    } else {
                        plugin.PinRenderer.LoadedPins = JsonConvert.DeserializeObject<Dictionary<string, List<Pin>>>(json) ?? new Dictionary<string, List<Pin>>();
                        DXT.Log($"Loaded Pin file: {filePath}", false);
                    }
                    return;
                }
                catch (Exception ex) {
                    plugin.PinRenderer.LoadedPins = null;
                    DXT.Log($"Failed to load Pin file '{filePath}': {ex.Message}", false);
                }
            } else {
                DXT.Log($"Pin file not found: {filePath}", false);
            }
        }
        plugin.PinRenderer.LoadedPins = null;
        DXT.Log("No Pin file selected or file does not exist.", false);
    }
    private void UpdateFileStatus() {

        // status display
        if (PinDirFiles.Count < 1) {
            _fileStatusText = "No Pin files found in Pin Directory";
            _fileStatusColor = DXT.Colors.TextRed;
        } else if (string.IsNullOrEmpty(_loadedFilename)) {
            _fileStatusText = "No Pin File selected";
            _fileStatusColor = DXT.Colors.TextRed;
        } else if (_loadedPins == null) {
            if (!File.Exists(_loadedFilePath)) {
                _fileStatusText = "Selected Pin File not found";
            } else {
                _fileStatusText = "Failed to load Pin File";
            }
            _fileStatusColor = DXT.Colors.TextRed;
        } else {
            _fileStatusText = "File loaded successfully";
            _fileStatusColor = DXT.Colors.TextGreen;
        }
    }
    private int RebuildPinGroupKeys(string? selectKey = null) {
        _pinGroupKeys.Clear();
        if (_loadedPins != null)
            _pinGroupKeys.AddRange(_loadedPins.Keys);

        // Try to find the requested key
        if (selectKey != null) {
            int idx = _pinGroupKeys.IndexOf(selectKey);
            if (idx >= 0)
                return idx;
        }

        // Fallback: return current selectedAreaIndex (clamped)
        if (selectedAreaIndex >= 0 && selectedAreaIndex < _pinGroupKeys.Count)
            return selectedAreaIndex;

        // If nothing is valid, fallback to 0
        return 0;
    }
    private string GetUniqueFilePath(string directory, string baseName, string extension) {
        string fileName = baseName + extension;
        string filePath = Path.Combine(directory, fileName);
        int count = 1;
        while (File.Exists(filePath)) {
            fileName = $"{baseName} ({count}){extension}";
            filePath = Path.Combine(directory, fileName);
            count++;
        }
        return filePath;
    }
    private string GetUniqueGroupName(string newName) {
        if (_loadedPins == null) return newName;
        string groupName = newName;
        int count = 1;
        while (_loadedPins.ContainsKey(groupName)) {
            groupName = $"{newName} ({count})";
            count++;
        }
        return groupName;
    }
    private static readonly Regex TileNumberRegex = new Regex(@"_(\d{2})\.tdt", RegexOptions.Compiled);

    //--| Icon Settings |--------------------------------------------------------------------------------------------------------------------------
    private void Draw_IconSettings(string panelID) {
        DXT.Checkbox.Draw("Icons.Enabled", ref Settings.Icons.Enabled, new() {
            Height = controlHeight,
            Tooltip = new("Draw Icons on map")
        });
        ImGui.SameLine();
        DXT.Label.Draw("Enabled", checkboxLabelOptions);

        ImGui.SameLine();
        DXT.Checkbox.Draw("Icons.PixelPerfect", ref Settings.Icons.PixelPerfect, new() {
            Height = controlHeight,
            Tooltip = new("Enables pixel-perfect icons for crisp, unblurred icons.")
        });
        ImGui.SameLine();
        DXT.Label.Draw("Pixel Perfect", checkboxLabelOptions);

        ImGui.SameLine();
        DXT.Checkbox.Draw("Icons.DrawCached", ref Settings.Icons.DrawCached, new() {
            Height = controlHeight,
            Tooltip = new("Draw icons for entities you have already discovered, even if they are now outside your poe clients visible network range.")
        });
        ImGui.SameLine();
        DXT.Label.Draw("Draw Cached", checkboxLabelOptions);

    }

    private void Draw_IconsSettings() {
        var panelID = "CustomIconPanel";
        bool isCollapsed = !Settings.GetIconPanelOpen(panelID);
        if (DXT.CollapsingPanel.Begin(panelID, ref isCollapsed, new() { Label = "Custom Icons", Width = 0 })) {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, controlSpacing);

            var newButtonOptions = new DXT.Button.Options { Label = "New", Width = controlWidth, Height = controlHeight, Tooltip = new("Add a new custom Icon") };
            if (Settings.Icons.CustomPathIcons.Count < 1) {

                if (DXT.Button.Draw($"{panelID}NewPin", newButtonOptions)) Settings.NewCustomPathIcon(); 

                ImGui.SameLine();
                DXT.Label.Draw("No Custom Icons defined", new() { Width = controlx2Width, Height = controlHeight, TextColor = DXT.Colors.TextYellow, PadLeft = 3 });
            }else {

                for (int i = 0; i < Settings.Icons.CustomPathIcons.Count; i++) {
                    var iconSettings = Settings.Icons.CustomPathIcons[i];

                    DXT.Checkbox.Draw($"{panelID}_{i}_Draw", ref iconSettings.Draw, new() { Height = controlHeight, Tooltip = new("Draw Icon") });

                    ImGui.SameLine();
                    DXT.ColorSelect.Draw($"{panelID}_{i}_Tint", $"Icon Color", ref iconSettings.Tint, new() { Width = controlHeight, Height = controlHeight });

                    ImGui.SameLine();
                    DXT.ColorSelect.Draw($"{panelID}_{i}_HiddenTint", $"Icon Hidden Color", ref iconSettings.HiddenTint, new() { Width = controlHeight, Height = controlHeight });

                    ImGui.SameLine();
                    DXT.IconSelect.Draw($"{panelID}_{i}_Index", $"Icon", ref iconSettings.Index, Plugin.IconAtlas, new DXT.IconSelect.Options { IconColor = iconSettings.Tint, Width = controlHeight, Height = controlHeight });

                    ImGui.SameLine();
                    DXT.Slider.Draw($"{panelID}_{i}_Size", ref iconSettings.Size, new() { Width = 64, Height = controlHeight, Min = 0, Max = 64, Tooltip = new("Icon Size", "32x32 is icons basesize") });

                    ImGui.SameLine();
                    DXT.Checkbox.Draw($"{panelID}_{i}_Check_IsAlive", ref iconSettings.Check_IsAlive, new() { Height = controlHeight, Tooltip = new("Check if Entity is Alive") });

                    ImGui.SameLine();
                    DXT.Checkbox.Draw($"{panelID}_{i}_Check_IsOpened", ref iconSettings.Check_IsOpened, new() { Height = controlHeight, Tooltip = new("Check if Entity is Opened") });
                    //DT.Checkbox($"##CustomPathText{i}", "Draw Text", ref setting.DrawName); ImGui.SameLine();
                    ImGui.SameLine();
                    float inputTextWidth = ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Remove").X - ImGui.GetStyle().ItemSpacing.X;
                    DXT.Input.Draw($"{panelID}_{i}_Path", ref iconSettings.Path, new() { Width = -controlHeight - (int)controlSpacing.X - (int)controlSpacing.X, Height = controlHeight, Tooltip = new("Icon Path") }); ImGui.SameLine();

                    ImGui.SameLine();
                    if (DXT.Button.Draw($"{panelID}_{i}_Remove", new() { Label = "X", Width = controlHeight, Height = controlHeight, Color = DXT.Colors.ControlRed, TextColor = DXT.Colors.TextOnColor, Tooltip = new("Remove Custom Icon") })) {
                        var idx = i;
                        DXT.Deferred.Enqueue(() => {
                            Settings.RemoveCustomPathIcon(idx);
                        });
                    }
                }
                ImGui.Dummy(new SVector2((int)ImGui.GetContentRegionAvail().X - controlWidth - controlSpacing.X - controlSpacing.X, 0));
                ImGui.SameLine();
                if (DXT.Button.Draw($"{panelID}NewPin", newButtonOptions)) Settings.NewCustomPathIcon();
            }

            ImGui.PopStyleVar();
            ImGui.Dummy(new SVector2(0, 0));
            DXT.CollapsingPanel.End(panelID);
        }
        Settings.SetIconPanelOpen(panelID, !isCollapsed);
        ImGui.Dummy(new SVector2(0, 3)); // panel spacing

        foreach (var iconGroup in IconRegistry.Groups) {
            isCollapsed = !Settings.GetIconPanelOpen(iconGroup.Name);

            if (DXT.CollapsingPanel.Begin(iconGroup.Name, ref isCollapsed, new() { Label = iconGroup.Name, Width = 0 })) {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, controlSpacing);

                foreach (var icon in iconGroup) {
                    var iconSettings = Settings.GetIconSettings(icon.IconType, icon.DefaultSettings);
                    switch (icon.IconSettingsType) {
                        case IconSettingsTypes.Default:

                        DXT.Checkbox.Draw($"{icon.Name}Checkbox", ref iconSettings.Draw, new() { Height = controlHeight, Tooltip = new("Draw Icon") });
                        ImGui.SameLine();
                        DXT.ColorSelect.Draw($"{icon.Name}ColorSelect", $"Icon Color", ref iconSettings.Tint, new() { Width = controlHeight, Height = controlHeight });
                        ImGui.SameLine();
                        DXT.ColorSelect.Draw($"{icon.Name}HiddenColorSelect", $"Icon Hidden Color", ref iconSettings.HiddenTint, new() { Width=controlHeight , Height=controlHeight });
                        ImGui.SameLine();
                        DXT.IconSelect.Draw($"{icon.Name}IconSelect", $"Icon", ref iconSettings.Index, Plugin.IconAtlas, new DXT.IconSelect.Options { IconColor = iconSettings.Tint, Width = controlHeight, Height = controlHeight });
                        ImGui.SameLine();
                        DXT.Slider.Draw($"{icon.Name}IconSlider", ref iconSettings.Size, new() { Width = 64, Height = controlHeight, Min = 0, Max = 64, Tooltip = new("Icon Size", "32x32 is icons basesize") });
                        ImGui.SameLine();
                        DXT.Label.Draw($"{icon.Name}", new() { Height = controlHeight, Tooltip = new(icon.Name) });
                        break;

                        case IconSettingsTypes.Monster:

                        DXT.Checkbox.Draw($"{icon.Name}Checkbox", ref iconSettings.Draw, new() { Height = controlHeight, Tooltip = new("Draw Icon") });
                        ImGui.SameLine();
                        DXT.ColorSelect.Draw($"{icon.Name}ColorSelect", $"Icon Color", ref iconSettings.Tint, new() { Width = controlHeight, Height = controlHeight });
                        ImGui.SameLine();
                        DXT.ColorSelect.Draw($"{icon.Name}HiddenColorSelect", $"Icon Hidden Color", ref iconSettings.HiddenTint, new() { Width = controlHeight, Height = controlHeight });
                        ImGui.SameLine();
                        DXT.IconSelect.Draw($"{icon.Name}IconSelect", $"Icon", ref iconSettings.Index, Plugin.IconAtlas, new DXT.IconSelect.Options { IconColor = iconSettings.Tint, Width = controlHeight, Height = controlHeight });
                        ImGui.SameLine();
                        DXT.Checkbox.Draw($"{icon.Name}AnimateCheckbox", ref iconSettings.AnimateLife, new() { Height = controlHeight, Tooltip = new("Animate Icon Health", "use 8 sequential icons to visualise health") });
                        ImGui.SameLine();
                        DXT.Slider.Draw($"{icon.Name}IconSlider", ref iconSettings.Size, new() { Width = 64, Height = controlHeight, Min = 0, Max = 64, Tooltip = new("Icon Size", "32x32 is icons basesize") });
                        ImGui.SameLine();
                        DXT.Label.Draw($"{icon.Name}", new() { Height = controlHeight, Tooltip = new(icon.Name) });
                        break;

                        case IconSettingsTypes.Friendly:
                        DXT.Checkbox.Draw($"{icon.Name}Checkbox", ref iconSettings.Draw, new() { Height = controlHeight, Tooltip = new("Draw Icon") });
                        ImGui.SameLine();
                        DXT.ColorSelect.Draw($"{icon.Name}ColorSelect", $"Icon Color", ref iconSettings.Tint, new() { Width = controlHeight, Height = controlHeight });
                        ImGui.SameLine();
                        DXT.ColorSelect.Draw($"{icon.Name}HiddenColorSelect", $"Icon Hidden Color", ref iconSettings.HiddenTint, new() { Width = controlHeight, Height = controlHeight });
                        ImGui.SameLine();
                        DXT.IconSelect.Draw($"{icon.Name}IconSelect", $"Icon", ref iconSettings.Index, Plugin.IconAtlas, new DXT.IconSelect.Options { IconColor = iconSettings.Tint, Width = controlHeight, Height = controlHeight });
                        ImGui.SameLine();
                        DXT.Checkbox.Draw($"{icon.Name}AnimateCheckbox", ref iconSettings.AnimateLife, new() { Height = controlHeight, Tooltip = new("Animate Icon Health", "use 8 sequential icons to visualise health") });
                        ImGui.SameLine();
                        DXT.Slider.Draw($"{icon.Name}IconSlider", ref iconSettings.Size, new() { Width = 64, Height = controlHeight, Min = 0, Max = 64, Tooltip = new("Icon Size", "32x32 is icons basesize") });
                        ImGui.SameLine();
                        DXT.Checkbox.Draw($"{icon.Name}ShowName", ref iconSettings.DrawName, new() { Height = controlHeight, Tooltip = new("Draw Name") });
                        ImGui.SameLine();
                        DXT.Checkbox.Draw($"{icon.Name}ShowHealth", ref iconSettings.DrawHealth, new() { Height = controlHeight, Tooltip = new("Draw Health") });
                        ImGui.SameLine();
                        DXT.Label.Draw($"{icon.Name}", new() { Height = controlHeight, Tooltip = new(icon.Name) });
                        break;

                        case IconSettingsTypes.Chest:
                        DXT.Checkbox.Draw($"{icon.Name}Checkbox", ref iconSettings.Draw, new() { Height = controlHeight, Tooltip = new("Draw Icon") });
                        ImGui.SameLine();
                        DXT.ColorSelect.Draw($"{icon.Name}ColorSelect", $"Icon Color", ref iconSettings.Tint, new() { Width = controlHeight, Height = controlHeight });
                        ImGui.SameLine();
                        DXT.IconSelect.Draw($"{icon.Name}IconSelect", $"Icon", ref iconSettings.Index, Plugin.IconAtlas, new DXT.IconSelect.Options { IconColor = iconSettings.Tint, Width = controlHeight, Height = controlHeight });
                        ImGui.SameLine();
                        DXT.Slider.Draw($"{icon.Name}IconSlider", ref iconSettings.Size, new() { Width = 64, Height = controlHeight, Min = 0, Max = 64, Tooltip = new("Icon Size", "32x32 is icons basesize") });
                        ImGui.SameLine();
                        DXT.Label.Draw($"{icon.Name}", new() { Height = controlHeight, Tooltip = new(icon.Name) });
                        break;

                        case IconSettingsTypes.Trap:
                        DXT.Checkbox.Draw($"{icon.Name}Checkbox", ref iconSettings.Draw, new() { Height = controlHeight, Tooltip = new("Draw Icon") });
                        ImGui.SameLine();
                        DXT.ColorSelect.Draw($"{icon.Name}ColorSelect", $"Icon Color", ref iconSettings.Tint, new() { Width = controlHeight, Height = controlHeight });
                        ImGui.SameLine();
                        DXT.ColorSelect.Draw($"{icon.Name}ArmingColorSelect", $"Icon Arming Color", ref iconSettings.ArmingTint, new() { Width = controlHeight, Height = controlHeight });
                        ImGui.SameLine();
                        DXT.ColorSelect.Draw($"{icon.Name}HiddenColorSelect", $"Icon Hidden Color", ref iconSettings.HiddenTint, new() { Width = controlHeight, Height = controlHeight });
                        ImGui.SameLine();
                        DXT.IconSelect.Draw($"{icon.Name}IconSelect", $"Icon", ref iconSettings.Index, Plugin.IconAtlas, new DXT.IconSelect.Options { IconColor = iconSettings.Tint, Width = controlHeight, Height = controlHeight });
                        ImGui.SameLine();
                        DXT.Slider.Draw($"{icon.Name}IconSlider", ref iconSettings.Size, new() { Width = 64, Height = controlHeight, Min = 0, Max = 64, Tooltip = new("Icon Size", "32x32 is icons basesize") });
                        ImGui.SameLine();
                        DXT.Label.Draw($"{icon.Name}", new() { Height = controlHeight, Tooltip = new(icon.Name) });
                        break;

                    }
                }

                ImGui.PopStyleVar();
                ImGui.Dummy(new SVector2(0, 0));
                DXT.CollapsingPanel.End("SettingsPanelOpen");
            }
            Settings.SetIconPanelOpen(iconGroup.Name, !isCollapsed);
            ImGui.Dummy(new SVector2(0, 3)); // panel spacing
        }
    }

    private static class IconRegistry {
        public class IconEntry(string name) {
            public string Name { get; set; } = name;
            public IconTypes IconType { get; set; }
            public IconSettingsTypes IconSettingsType { get; set; }
            public IconSettings DefaultSettings { get; set; } = new();
        }

        public class IconGroup(string name) : List<IconEntry> // <- inherit List<IconEntry>!
        {
            public string Name { get; set; } = name;
        }

        public static readonly List<IconGroup> Groups = new();

        public static void AddGroup(IconGroup group) {
            Groups.Add(group);
        }
    }

    private static SColor _whiteTint = DXT.Color.FromRGBA(203,213,225,255);
    private static SColor _greyTint = DXT.Color.FromRGBA(100,116,139,255); // slate

    private static SColor _normalTint = DXT.Color.FromRGBA(185,28,28,255);
    private static SColor _normalHiddenTint = DXT.Color.FromRGBA(254,202,202,255);
    private static SColor _magicTint = DXT.Color.FromRGBA(2,132,199,255);
    private static SColor _magicHiddenTint = DXT.Color.FromRGBA(224,242,254,255);
    private static SColor _rareTint = DXT.Color.FromRGBA(250,204,21,255);
    private static SColor _rareHiddenTint = DXT.Color.FromRGBA(254,249,195,255);
    private static SColor _uniqueTint = DXT.Color.FromRGBA(249,115,22,255);
    private static SColor _uniqueHiddenTint = DXT.Color.FromRGBA(255,237,213,255);

    private static SColor _friendlyTint = DXT.Color.FromRGBA(132,204,22,255);
    private static SColor _friendlyHiddenTint = DXT.Color.FromRGBA(236,252,203,255);    


    private void InitializeIcons() {
        /* IconRegistry.AddGroup(new("Ingame Icons") {
            new("Shrine") {
                IconType = IconTypes.Shrine,
                IconSettingsType = IconSettingsTypes.IngameIcon,
                DefaultSettings = new() { DrawState = IngameIconDrawStates.Always, DrawName = true }
            },
            new("Breach") {
                IconType = IconTypes.Breach,
                IconSettingsType = IconSettingsTypes.IngameIcon,
                DefaultSettings = new() { DrawState = IngameIconDrawStates.Ranged }
            },
            new("Area Transition") {
                IconType = IconTypes.AreaTransition,
                IconSettingsType = IconSettingsTypes.IngameIcon,
                DefaultSettings = new() { DrawState = IngameIconDrawStates.Ranged }
            },
            new("Quest Object") {
                IconType = IconTypes.QuestObject,
                IconSettingsType = IconSettingsTypes.IngameIcon,
                DefaultSettings = new() { DrawState = IngameIconDrawStates.Ranged, DrawName = true }
            },
            new("Ritual") {
                IconType = IconTypes.Ritual,
                IconSettingsType = IconSettingsTypes.IngameIcon,
                DefaultSettings = new() { DrawState = IngameIconDrawStates.Always }
            },
            new("Waypoint") {
                IconType = IconTypes.Waypoint,
                IconSettingsType = IconSettingsTypes.IngameIcon,
                DefaultSettings = new() { DrawState = IngameIconDrawStates.Ranged }
            },
            new("Checkpoint") {
                IconType = IconTypes.Checkpoint,
                IconSettingsType = IconSettingsTypes.IngameIcon,
                DefaultSettings = new() { DrawState = IngameIconDrawStates.Ranged }
            },
            new("Ingame NPC") {
                IconType = IconTypes.IngameNPC,
                IconSettingsType = IconSettingsTypes.IngameIcon,
                DefaultSettings = new() { DrawState = IngameIconDrawStates.Ranged }
            },
        });*/
        IconRegistry.AddGroup(new("Dangerous Icons") {
            new("Drowning Orb") {
                IconType = IconTypes.DrowningOrb,
                IconSettingsType = IconSettingsTypes.Default,
                DefaultSettings = new() { Index = 25, Tint = DXT.Color.FromRGBA(213,0,249,255), HiddenTint = DXT.Color.FromRGBA(244,179,255,255) }
            },
            new("Volatile Core") {
                IconType = IconTypes.VolatileCore,
                IconSettingsType = IconSettingsTypes.Default,
                DefaultSettings = new() { Index = 25, Tint = DXT.Color.FromRGBA(213,0,249,255), HiddenTint = DXT.Color.FromRGBA(244,179,255,255) }
            },
            new("Lightning Clone") {
                IconType = IconTypes.LightningClone,
                IconSettingsType = IconSettingsTypes.Default,
                DefaultSettings = new() { Index = 25, Tint = DXT.Color.FromRGBA(213,0,249,255), HiddenTint = DXT.Color.FromRGBA(244,179,255,255) }
            },
            new("Consuming Phantasm") {
                IconType = IconTypes.ConsumingPhantasm,
                IconSettingsType = IconSettingsTypes.Default,
                DefaultSettings = new() { Index = 25, Tint = DXT.Color.FromRGBA(213,0,249,255), HiddenTint = DXT.Color.FromRGBA(244,179,255,255) }
            },
            new("Ground Spike") {
                IconType = IconTypes.GroundSpike,
                IconSettingsType = IconSettingsTypes.Trap,
                DefaultSettings = new() { Index = 52, Tint = DXT.Color.FromRGBA(220,38,38,255), ArmingTint = DXT.Color.FromRGBA(217,119,6,255), HiddenTint = DXT.Color.FromRGBA(20,128,61,255) }
            },
        });
        IconRegistry.AddGroup(new("Monster Icons") {
            new("Normal Monster") {
                IconType = IconTypes.NormalMonster,
                IconSettingsType = IconSettingsTypes.Monster,
                DefaultSettings = new() { Tint = _normalTint, HiddenTint = _normalHiddenTint }
            },
            new("Magic Monster") {
                IconType = IconTypes.MagicMonster,
                IconSettingsType = IconSettingsTypes.Monster,
                DefaultSettings = new() { Index = 1, Tint = _magicTint, HiddenTint = _magicHiddenTint }
            },
            new("Rare Monster") {
                IconType = IconTypes.RareMonster,
                IconSettingsType = IconSettingsTypes.Monster,
                DefaultSettings = new() { AnimateLife = true, Index = 16, Tint =_rareTint, HiddenTint = _rareHiddenTint }
            },
            new("Unique Monster") {
                IconType = IconTypes.UniqueMonster,
                IconSettingsType = IconSettingsTypes.Monster,
                DefaultSettings = new() { AnimateLife = true, Index = 40, Tint = _uniqueTint, HiddenTint = _uniqueHiddenTint }
            },
            new("Pinnacle Boss") {
                IconType = IconTypes.PinnacleBoss,
                IconSettingsType = IconSettingsTypes.Monster,
                DefaultSettings = new() { AnimateLife = true, Index = 40, Tint = _uniqueTint, HiddenTint = _uniqueHiddenTint }
            },
            //new("Rogue Exile") {
            //    IconType = IconTypes.RogueExile,
            //    IconSettingsType = IconSettingsTypes.Monster,
            //    DefaultSettings = new() { Index = 2, Tint = _uniqueTint, HiddenTint = _uniqueHiddenTint }
            //},
            //new("Giant Rogue Exile") {
            //    IconType = IconTypes.GiantRogueExile,
            //    IconSettingsType = IconSettingsTypes.Monster,
            //    DefaultSettings = new() { Index = 98, Tint = _uniqueTint, HiddenTint = _uniqueHiddenTint }
            //},
            new("Spirit") {
                IconType = IconTypes.Spirit,
                IconSettingsType = IconSettingsTypes.Monster,
                DefaultSettings = new() { Index = 2, Tint = DXT.Color.FromRGBA(251,191,36,255), HiddenTint = DXT.Color.FromRGBA(254,243,199,255) }
            },
            new("Fracturing Mirror") {
                IconType = IconTypes.FracturingMirror,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Index = 192, Tint = DXT.Color.FromRGBA(34,211,238,255) }
            },


        });
        IconRegistry.AddGroup(new("Friendly Icons") {
            new("Local Player") {
                IconType = IconTypes.LocalPlayer,
                IconSettingsType = IconSettingsTypes.Friendly,
                DefaultSettings = new() { Draw = false, Tint = _friendlyTint, HiddenTint = _friendlyHiddenTint }
            },
            new("Other Player") {
                IconType = IconTypes.OtherPlayer,
                IconSettingsType = IconSettingsTypes.Friendly,
                DefaultSettings = new() { Draw = false, Tint = _friendlyTint, HiddenTint = _friendlyHiddenTint }
            },
            new("NPC") {
                IconType = IconTypes.NPC,
                IconSettingsType = IconSettingsTypes.Friendly,
                DefaultSettings = new() { Draw = false, Index = 1, Tint = _friendlyTint, HiddenTint = _friendlyHiddenTint }
            },
            new("Minion") {
                IconType = IconTypes.Minion,
                IconSettingsType = IconSettingsTypes.Friendly,
                DefaultSettings = new() { Draw = false, Tint = _friendlyTint, HiddenTint = _friendlyHiddenTint }
            },
            new("Totem") {
                IconType = IconTypes.TotemGeneric,
                IconSettingsType = IconSettingsTypes.Friendly,
                DefaultSettings = new() { AnimateLife = true, Index = 16, Tint = _friendlyTint, HiddenTint = _friendlyHiddenTint }
            },
        });
        IconRegistry.AddGroup(new("Chest Icons") {
            new("Unknown Chest") {
                IconType = IconTypes.UnknownChest,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Draw = true, Index = 360, Tint = _greyTint }
            },
            new("Breakable Object") {
                IconType = IconTypes.BreakableObject,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Draw = false, Index = 360, Tint = _greyTint }
            },
            new("Chest White") {
                IconType = IconTypes.ChestWhite,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Draw = false, Index = 360, Tint = _whiteTint }
            },
            new("Chest Magic") {
                IconType = IconTypes.ChestMagic,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Index = 360, Tint = _magicTint }
            },
            new("Chest Rare") {
                IconType = IconTypes.ChestRare,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Index = 360, Tint = _rareTint }
            },
            new("Chest Unique") {
                IconType = IconTypes.ChestUnique,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Index = 360, Tint = _uniqueTint }
            },
            new("Breach Chest Normal") {
                IconType = IconTypes.BreachChestNormal,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Index = 360, Tint = DXT.Color.FromRGBA(217,70,239,255) }
            },
            new("Breach Chest Large") {
                IconType = IconTypes.BreachChestLarge,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Index = 361, Tint = DXT.Color.FromRGBA(217,70,239,255) }
            },
            new("Expedition Chest White") {
                IconType = IconTypes.ExpeditionChestWhite,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Index = 360, Tint = _whiteTint }
                },
            new("Expedition Chest Magic") {
                IconType = IconTypes.ExpeditionChestMagic,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Index = 360, Tint = _magicTint }
            },
            new("Expedition Chest Rare") {
                IconType = IconTypes.ExpeditionChestRare,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Index = 360, Tint = _rareTint }
            },
            new("Sanctum Chest") {
                IconType = IconTypes.SanctumChest,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Index = 360, Tint = DXT.Color.FromRGBA(217,70,239,255) }
            },
            new("Pirate Chest") {
                IconType = IconTypes.PirateChest,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Index = 360, Tint = _rareTint }
            },
            new("Abyss Chest") {
                IconType = IconTypes.AbyssChest,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Index = 360, Tint = DXT.Color.FromRGBA(22,163,74,255) }
            },
            new("Sanctum Mote") {
                IconType = IconTypes.SanctumMote,
                IconSettingsType = IconSettingsTypes.Chest,
                DefaultSettings = new() { Index = 360, Tint = DXT.Color.FromRGBA(217,70,239,255) }
            },
        });
    }


}

