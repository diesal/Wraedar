using System;
using DieselExileTools;
using GameHelper;
using GameHelper.RemoteEnums;
using GameHelper.RemoteObjects.Components;
using GameHelper.RemoteObjects.States.InGameStateObjects;
using ImGuiNET;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using SColor = System.Drawing.Color;
using SVector2 = System.Numerics.Vector2;

namespace Wraedar;


public record Pin
{
    public string Path = "";
    public int ExpectedCount = 1;
    public string Label = "";
    public bool Enabled = true;
    public SColor TextColor = DXT.Color.FromRGBA(253, 224, 71);
    public SColor BGColor = DXT.Color.FromRGBA(0, 0, 0);
    public void PasteFrom(Pin other) {
        Path = other.Path;
        ExpectedCount = other.ExpectedCount;
        Label = other.Label;
        Enabled = other.Enabled;
        TextColor = other.TextColor;
        BGColor = other.BGColor;
    }
}

public class PinTileMatch {
    public required Pin Pin { get; set; }
    public required string TileKey { get; set; }
    public required List<SVector2> TilePositions { get; set; }
}

public sealed class PinRenderer(Plugin plugin) : PluginModule(plugin)
{
    private string _currentArea = "";
    private DateTime _lastCopyTime = DateTime.MinValue;

    private SColor filterdTileBG = DXT.Palettes.Tailwind.Slate800.Color;
    private readonly SColor filterdTileBorder = DXT.Palettes.Tailwind.Slate600.Color;
    private readonly SColor filterdTileText = DXT.Palettes.Tailwind.Slate500.Color;

    public Dictionary<string, List<Pin>>? LoadedPins;
    public List<KeyValuePair<string, List<SVector2>>>? EditorFilteredTiles;
    private List<PinTileMatch> _areaPinTileMatches = new();

    private AreaInstance? _areaInstance;
    private PathFinder? _pathFinder;
    private Dictionary<string, Task<List<SVector2>>> _pinPathTasks = new();
    private Dictionary<string, List<SVector2>> _pinPathsReady = new();

    private readonly SColor[] _pathColorCycle = new SColor[] {
        DXT.Color.FromRGBA(253, 224, 71), // Yellow
        DXT.Color.FromRGBA(16, 185, 129), // Green
        DXT.Color.FromRGBA(96, 165, 250), // Blue
        DXT.Color.FromRGBA(236, 72, 153), // Pink
        DXT.Color.FromRGBA(249, 115, 22), // Orange
        DXT.Color.FromRGBA(139, 92, 246), // Purple
        DXT.Color.FromRGBA(34, 197, 94),  // Emerald
        DXT.Color.FromRGBA(14, 165, 233), // Sky
        DXT.Color.FromRGBA(234, 179, 8),  // Amber
    };

    // | Initialise |----------------------------------------------------------------------------------------------------
    public void Initialise() {
        plugin.OnAreaChange += OnAreaChange;
    }

    // | Events |----------------------------------------------------------------------------------------------------
    private void OnAreaChange(string areaID, string areaHash) {
        if (Core.States.GameCurrentState is not ( GameStateTypes.InGameState or GameStateTypes.EscapeState )) return;
        _currentArea = areaID;
        _areaInstance = Core.States.InGameStateObject.CurrentAreaInstance;
        if (_areaInstance == null) return;
        _pathFinder = new PathFinder(_areaInstance);
        UpdateMatchedPins(true);
    }

    // | Render |----------------------------------------------------------------------------------------------------
    public void Render() {
        if (_areaInstance == null) return;
        if (Settings.DebugWalkableTerrain) DebugTilesAroundPlayer(10);
        if (Settings.Pin.Enabled && LoadedPins != null && LoadedPins.Count > 0) {
            UpdateMatchedPins();
            if (_areaPinTileMatches != null && _areaPinTileMatches.Count > 0) {
                if (Settings.Pin.DrawPaths) RenderPaths();
                RenderPins();
            }
        }
        RenderEditorFilteredTiles();       
    }
    private void RenderEditorFilteredTiles() {
        if (!Settings.Pin.ShowTiles || !Settings.Pin.EditorWindowOpen || EditorFilteredTiles == null || EditorFilteredTiles.Count < 1) return;

        var player = Core.States.InGameStateObject.CurrentAreaInstance.Player;
        if (!player.TryGetComponent<Render>(out var playerRender)) return;

        var area = Core.States.InGameStateObject.CurrentAreaInstance;

        foreach (var tileEntry in EditorFilteredTiles) {
            var tilePositions = tileEntry.Value; // List<Vector2>
            foreach (var position in tilePositions) {
                // Convert grid/world position to map/screen position

                float height = 0;
                if (!Settings.Pin.IgnoreTerrainHeight &&
                    position.X < area.GridHeightData[0].Length &&
                    position.Y < area.GridHeightData.Length) {
                    height = area.GridHeightData[(int)position.Y][(int)position.X] - playerRender.TerrainHeight;
                }
                var screenPos = DXT.GridToMap(position.X - playerRender.GridPosition.X, position.Y - playerRender.GridPosition.Y, height,true); // Adjust as needed for your coordinate system
                var xy = ExtractXYFromPath(tileEntry.Key);
                var label = xy.HasValue ? $"{xy.Value.x},{xy.Value.y}" : "?,?";

                var textRect = Plugin.GetCenteredRect(screenPos, ImGui.CalcTextSize(label));
                if (textRect.Contains(ImGui.GetMousePos())) {
                    Plugin.DrawRectText(textRect, label, filterdTileText, SColor.Black, 5, filterdTileText);
                    DXT.Tooltip.Draw(new DXT.Tooltip.Options {
                        Lines = new List<DXT.Tooltip.Line> {
                        new DXT.Tooltip.Title { Text = "Tile" },
                        new DXT.Tooltip.Separator(),
                        new DXT.Tooltip.Description { Text = tileEntry.Key },
                        new DXT.Tooltip.DoubleLine { LeftText = "Leftclick:", RightText = $"copy tile path" },
                    }
                    });
                    if (DXT.Mouse.IsLeftButtonDown()) {
                        if ((DateTime.Now - _lastCopyTime).TotalMilliseconds >= 250) {
                            Plugin.SettingsUI.CopyPath(tileEntry.Key);
                            _lastCopyTime = DateTime.Now;
                        }
                    }
                }
                else {
                    Plugin.DrawRectText(textRect, label, filterdTileText, SColor.Black, 1, filterdTileBorder);
                }

                //var textRect = Plugin.DrawCenteredText(screenPos, label, filterdTileText, SColor.Black, 2, filterdTileText);
            }
        }
    }

    // | Pins |-----------------------------------------------------------------------------------------------------------
    private void RenderPins() {
        //DebugTilesAroundPlayer();
        if (_areaInstance == null) return;

        var area = Core.States.InGameStateObject.CurrentAreaInstance;
        if (area == null) return;

        var player = area.Player;
        if (!player.TryGetComponent<Render>(out var playerRender)) return;

        foreach (var entry in _areaPinTileMatches) {
            if (!entry.Pin.Enabled) continue;

            string labelSuffix = entry.TilePositions.Count > entry.Pin.ExpectedCount ? "?" : "";
            foreach (var position in entry.TilePositions) {
                float height = 0;
                if (position.X < area.GridHeightData[0].Length && position.Y < area.GridHeightData.Length)
                    height = area.GridHeightData[(int)position.Y][(int)position.X] - playerRender.TerrainHeight;

                var screenPos = DXT.GridToMap(
                    position.X - playerRender.GridPosition.X,
                    position.Y - playerRender.GridPosition.Y,
                    height, true);

                var textRect = Plugin.GetCenteredRect(screenPos, ImGui.CalcTextSize(entry.Pin.Label + labelSuffix));
                Plugin.DrawRectText(textRect, entry.Pin.Label + labelSuffix, entry.Pin.TextColor, entry.Pin.BGColor, 1, SColor.Black);
            }
        }
    }
    public void UpdateMatchedPins(bool force = false, bool allowLearn = true) {
        if (_areaInstance == null) return;
        if (_pathFinder == null) return;
        if (LoadedPins == null || LoadedPins.Count == 0) return;

        var player = _areaInstance.Player;
        if (!player.TryGetComponent<Render>(out var playerRender)) return;
        var playerPos2D = new SVector2(playerRender.GridPosition.X, playerRender.GridPosition.Y);

        bool changed = false;
        int matchIndex = 0;
        var newMatches = new List<PinTileMatch>();

        foreach (var kvp in LoadedPins) {
            if (!_currentArea.Like(kvp.Key)) continue;
            foreach (var pin in kvp.Value) {
                var tileEntry = _areaInstance.TgtTilesLocations
            .FirstOrDefault(tile => tile.Key.Equals(pin.Path, StringComparison.OrdinalIgnoreCase));
                if (tileEntry.Key == null) continue;

                var snappedTilePositions = tileEntry.Value
            .Select(pos => {
                var grid = _pathFinder.PoeGridPosToPathGridPos(pos);
                if (_pathFinder.IsTileWalkable(grid))
                    return pos;
                var walkable = _pathFinder.FindNearestWalkable(grid,8);
                return walkable.HasValue
                    ? new SVector2(walkable.Value.X * _pathFinder.GridSize, walkable.Value.Y * _pathFinder.GridSize)
                    : pos;
            })
            .ToList();

                var match = new PinTileMatch {
                    Pin = pin,
                    TileKey = tileEntry.Key,
                    TilePositions = snappedTilePositions
                };
                newMatches.Add(match);

                // Compare as we go
                if (!changed && ( matchIndex >= _areaPinTileMatches.Count
                    || _areaPinTileMatches[matchIndex].Pin.Path != match.Pin.Path
                    || _areaPinTileMatches[matchIndex].TileKey != match.TileKey
                    || _areaPinTileMatches[matchIndex].TilePositions.Count != match.TilePositions.Count )) {
                    changed = true;
                }
                matchIndex++;
            }
        }

        bool hasRenderablePins = newMatches.Any(match => match.Pin.Enabled && match.TilePositions.Count > 0);
        if (allowLearn && !hasRenderablePins && TryLearnBossPin()) {
            UpdateMatchedPins(true, false);
            return;
        }



        // If counts differ, mark as changed
        if (force || !changed && newMatches.Count != _areaPinTileMatches.Count) {
            changed = true;
        }

        if (!changed) return;

        DXT.Log($"Found {newMatches.Count} Pin matches for area {_currentArea}, updating computes", false);

        _areaPinTileMatches = newMatches;

        // Precompute paths for all Pins now
        foreach (var entry in _areaPinTileMatches) {
            if (!entry.Pin.Enabled || entry.TilePositions.Count == 0) continue;

            var targetTile = entry.TilePositions
            .OrderBy(p => (p - playerPos2D).Length()) // closest to player
            .First();

            var targetGrid = _pathFinder.PoeGridPosToPathGridPos(targetTile);
            var playerGrid = _pathFinder.PoeGridPosToPathGridPos(playerPos2D);

            foreach (var _ in _pathFinder.RunFirstScan(playerGrid, targetGrid)) { }
        }
    }

    private bool TryLearnBossPin() {
        if (!Settings.Pin.LearnMapBossArena) return false;
        if (_areaInstance == null) return false;
        if (LoadedPins == null) return false;
        if (string.IsNullOrEmpty(_currentArea)) return false;
        if (string.IsNullOrWhiteSpace(Settings.Pin.SelectedFilename)) return false;

        var matchingTiles = new List<(string Key, SVector2 Position)>();

        foreach (var tileEntry in _areaInstance.TgtTilesLocations) {
            if (string.IsNullOrEmpty(tileEntry.Key)) continue;
            if (!tileEntry.Key.Contains("boss", StringComparison.OrdinalIgnoreCase) &&
                !tileEntry.Key.Contains("arena", StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var position in tileEntry.Value) {
                matchingTiles.Add((tileEntry.Key, position));
            }
        }

        if (matchingTiles.Count == 0) return false;

        var center = new SVector2(
            (float)matchingTiles.Average(tile => tile.Position.X),
            (float)matchingTiles.Average(tile => tile.Position.Y));

        var bestTile = matchingTiles
            .OrderBy(tile => Vector2.DistanceSquared(tile.Position, center))
            .First();

        if (!LoadedPins.TryGetValue(_currentArea, out var areaPins)) {
            areaPins = new List<Pin>();
            LoadedPins[_currentArea] = areaPins;
        }

        if (areaPins.Any(pin => string.Equals(pin.Label, "Boss", StringComparison.OrdinalIgnoreCase))) {
            return false;
        }

        var existingPin = areaPins
            .FirstOrDefault(pin => string.Equals(pin.Path, bestTile.Key, StringComparison.OrdinalIgnoreCase));
        if (existingPin != null) {
            if (string.Equals(existingPin.Label, "Boss", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            var previousLabel = existingPin.Label;
            existingPin.Label = "Boss";
            if (SaveLoadedPinsToFile()) {
                DXT.Log($"Learned Boss pin for area {_currentArea}: {bestTile.Key}", false);
                return true;
            }

            existingPin.Label = previousLabel;
            return false;
        }

        var newPin = new Pin {
            Path = bestTile.Key,
            Label = "Boss",
        };

        areaPins.Add(newPin);
        if (SaveLoadedPinsToFile()) {
            DXT.Log($"Learned Boss pin for area {_currentArea}: {bestTile.Key}", false);
            return true;
        }

        areaPins.Remove(newPin);
        if (areaPins.Count == 0) {
            LoadedPins.Remove(_currentArea);
        }
        return false;
    }

    private bool SaveLoadedPinsToFile() {
        if (LoadedPins == null) return false;
        var fileName = Settings.Pin.SelectedFilename;
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        try {
            Directory.CreateDirectory(Plugin.PinPath);
            var filePath = Path.Combine(Plugin.PinPath, fileName);
            var json = JsonConvert.SerializeObject(LoadedPins, Formatting.Indented);
            File.WriteAllText(filePath, json);
            return true;
        }
        catch (Exception ex) {
            DXT.Log($"Failed to save learned Boss pin: {ex.Message}", false);
            return false;
        }
    }

    // | Pathfinding |----------------------------------------------------------------------------------------------------
    private SVector2? _lastPlayerPathGridPos = null;
    public void RenderPaths() {
        if (_pathFinder == null || _areaPinTileMatches == null) return;
        if (_areaInstance == null) return;

        var player = Core.States.InGameStateObject.CurrentAreaInstance.Player;
        if (!player.TryGetComponent<Render>(out var playerRender)) return;
        var playerPos2D = new SVector2(playerRender.GridPosition.X, playerRender.GridPosition.Y);

        var playerPathGridPos = _pathFinder.PoeGridPosToPathGridPos(playerPos2D);
        if (_lastPlayerPathGridPos.HasValue && _lastPlayerPathGridPos == playerPathGridPos) _pinPathsReady.Clear(); // Player hasn't moved grid-wise, skip pathfinding
        _lastPlayerPathGridPos = playerPathGridPos;

        var playerGrid = _pathFinder.PoeGridPosToPathGridPos(playerPos2D);

        for (int i = 0; i < _areaPinTileMatches.Count; i++) {
            var entry = _areaPinTileMatches[i];
            if (!entry.Pin.Enabled || entry.TilePositions.Count == 0) continue;

            // Skip if more matches than expected
            if (entry.TilePositions.Count > entry.Pin.ExpectedCount) continue;

            // Sort all tiles by distance to player and take up to ExpectedCount
            var pinPositions = entry.TilePositions
                .OrderBy(p => (p - playerPos2D).Length())
                .Take(entry.Pin.ExpectedCount);

            foreach (var pinPos in pinPositions) {
                var pinGridPos = _pathFinder.PoeGridPosToPathGridPos(pinPos);
                // Skip tiles that are too close to the player
                if (( pinPos - playerPos2D ).Length() < 80) continue;

                List<SVector2> path;
                try {
                    var result = _pathFinder.FindPath(playerPathGridPos, pinGridPos, true);
                    path = result?.Select(v => new SVector2(v.X, v.Y)).ToList() ?? [];
                }
                catch {
                    path = [];
                }

                if (path.Count >= 2) {
                    var pixelPath = _pathFinder.PathGridToPoeGrid(path);
                    float height = 0;

                    SColor color = entry.Pin.TextColor;
                    if (Settings.Pin.OverrideColorPaths) {
                        int colorIndex = i % _pathColorCycle.Length;
                        color = _pathColorCycle[colorIndex];
                    }

                    DrawPath(pixelPath, playerPos2D, playerRender.TerrainHeight, color);
                }
            }
        }
        //if (Settings.Map.DrawOverPoePanels) playerHeight = 0;

    }

    private void DrawPath(List<SVector2> path, SVector2 playerPos,float playerHeight, SColor color) {

        // Draw the first segment: from the actual player position to the second path point
        var secondPath = path[1];
        float secondHeight = 0;
        if (secondPath.X < _areaInstance.GridHeightData[0].Length && secondPath.Y < _areaInstance.GridHeightData.Length)
            secondHeight = _areaInstance.GridHeightData[(int)secondPath.Y][(int)secondPath.X] - playerHeight;

        var start = DXT.GridToMap(0, 0, 0, true); // Actual player position
        var end = DXT.GridToMap(secondPath.X - playerPos.X, secondPath.Y - playerPos.Y, secondHeight, true);
        Plugin.DrawLine(start, end, color, Settings.Pin.PathThickness);

        for (int i = 1; i < path.Count - 1; i++) {
            var startPath = path[i];
            var endPath = path[i + 1];
    
            float startHeight = 0;
            if (startPath.X < _areaInstance.GridHeightData[0].Length && startPath.Y < _areaInstance.GridHeightData.Length)
                startHeight = _areaInstance.GridHeightData[(int)startPath.Y][(int)startPath.X] - playerHeight;
            float endHeight = 0;
            if (endPath.X < _areaInstance.GridHeightData[0].Length && endPath.Y < _areaInstance.GridHeightData.Length)
                endHeight = _areaInstance.GridHeightData[(int)endPath.Y][(int)endPath.X] - playerHeight;
    
            start = DXT.GridToMap(startPath.X - playerPos.X, startPath.Y - playerPos.Y, startHeight, true);
            end = DXT.GridToMap(endPath.X - playerPos.X, endPath.Y - playerPos.Y, endHeight, true);
            Plugin.DrawLine(start, end, color, Settings.Pin.PathThickness);
        }
    }




    public void DebugTilesAroundPlayer(int radiusInTiles = 10) {
        var area = Core.States.InGameStateObject.CurrentAreaInstance;
        if (area == null) return;

        var player = area.Player;
        if (!player.TryGetComponent<Render>(out var playerRender)) return;

        if (_pathFinder?.Grid == null) {
            DXT.Log("[DEBUG] Tile grid not initialized, cannot debug tiles around player", false);
            return;
        }

        // Convert player grid pixel position -> tile position
        int px = (int)(playerRender.GridPosition.X / _pathFinder.GridSize);
        int py = (int)(playerRender.GridPosition.Y / _pathFinder.GridSize);

        int walkableCount = 0;

        for (int dy = -radiusInTiles; dy <= radiusInTiles; dy++) {
            for (int dx = -radiusInTiles; dx <= radiusInTiles; dx++) {
                int tx = px + dx;
                int ty = py + dy;

                // Skip out-of-bounds
                if (tx < 0 || ty < 0 || tx >= _pathFinder.Width || ty >= _pathFinder.Height)
                    continue;

                bool walkable = _pathFinder.Grid[ty][tx];
                if (walkable) walkableCount++;

                float height = 0;
                int gx = tx * _pathFinder.GridSize;
                int gy = ty * _pathFinder.GridSize;
                //if (gy < area.GridHeightData.Length && gx < area.GridHeightData[0].Length)
                //    height = area.GridHeightData[gy][gx] - playerRender.TerrainHeight;

                // Convert tile coord delta → screen position
                var screenPos = DXT.GridToMap(
                    gx - playerRender.GridPosition.X,
                    gy - playerRender.GridPosition.Y,
                    height,
                    true
                );

                // Draw circle
                var color = walkable ? SColor.LimeGreen : SColor.Red;
                plugin.DrawCircleFilled(screenPos, 3, color);
            }
        }

        DXT.Monitor("PathFinding", "walkableCount", walkableCount);
    }

    // | ......... |------------------------------------------------------------------------------------------------------

    public static (int x, int y)? ExtractXYFromPath(string path) {
        // Pattern matches ...:x:123-y:456 or ...:123-y:456
        var match = Regex.Match(path, @"(?:\:x\:|\:)(-?\d+)-y:(-?\d+)");
        if (match.Success) {
            int x = int.Parse(match.Groups[1].Value);
            int y = int.Parse(match.Groups[2].Value);
            return (x, y);
        }
        return null;
    }

}
