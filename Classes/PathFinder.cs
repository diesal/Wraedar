using DieselExileTools;
using GameHelper.RemoteObjects.States.InGameStateObjects;
using SixLabors.ImageSharp;
using System.Collections.Concurrent;
using System.Diagnostics;
using SVector2 = System.Numerics.Vector2;

namespace Wraedar;
public class PathFinder {
    public bool[][] Grid { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int GridSize { get; private set; }

    public PathFinder(AreaInstance areaInstance, int gridSize = 8) {
        if (gridSize <= 0) {
            throw new ArgumentException("Grid size must be greater than 0", nameof(gridSize));
        }

        GridSize = gridSize;

        var walkableData = areaInstance.GridWalkableData;
        int bytesPerRow = areaInstance.TerrainMetadata.BytesPerRow;
        int totalRows = walkableData.Length / bytesPerRow;

        // Pixel-space dimensions
        int pixelWidth = bytesPerRow * 2;
        int pixelHeight = totalRows;

        // Convert to grid
        Width = (int)Math.Ceiling(pixelWidth / (float)GridSize);
        Height = (int)Math.Ceiling(pixelHeight / (float)GridSize);
        Grid = new bool[Height][];

        for (int ty = 0; ty < Height; ty++) {
            Grid[ty] = new bool[Width];

            for (int tx = 0; tx < Width; tx++) {

                int walkableCount = 0;
                int totalCount = 0;

                for (int py = ty * GridSize; py < ( ty + 1 ) * GridSize && py < pixelHeight; py++) {
                    for (int px = tx * GridSize; px < ( tx + 1 ) * GridSize && px < pixelWidth; px++) {
                        int byteIndex = py * bytesPerRow + (px / 2);
                        if (byteIndex >= walkableData.Length) continue;

                        byte b = walkableData[byteIndex];
                        int shift = (px % 2 == 0) ? 4 : 0;
                        int tileValue = (b >> shift) & 0xF;

                        if (tileValue != 0) walkableCount++;
                        totalCount++;
                    }
                }

                float ratio = (float)walkableCount / totalCount;
                Grid[ty][tx] = ratio >= 0.50f; // require at least X% walkable, .50f = 50%
            }
        }
    }
    
    public DXTVector2i PoeGridPosToPathGridPos(SVector2 pixelPos) {
        return new DXTVector2i((int)( pixelPos.X / GridSize ), (int)( pixelPos.Y / GridSize ));
    }
    public List<SVector2> PathGridToPoeGrid(List<SVector2> path) {
        if (path == null) return [];
        var poeGridPath = new List<SVector2>(path.Count);
        foreach (var step in path) {
            poeGridPath.Add(step * GridSize); // or just step if already pixel-space
        }
        return poeGridPath;
    }
    public DXTVector2i? FindNearestWalkable(DXTVector2i target, int maxRadius = 2) {
        if (IsTileWalkable(target))
            return target;

        for (int r = 1; r <= maxRadius; r++) {
            for (int dy = -r; dy <= r; dy++) {
                for (int dx = -r; dx <= r; dx++) {
                    // Only check the border of the square
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                        continue;

                    int nx = target.X + dx;
                    int ny = target.Y + dy;
                    var neighbor = new DXTVector2i(nx, ny);
                    if (IsTileWalkable(neighbor))
                        return neighbor;
                }
            }
        }
        return null;
    }
    public bool IsTileWalkable(DXTVector2i tile) {
        if (tile.X < 0 || tile.X >= Width) return false;
        if (tile.Y < 0 || tile.Y >= Height) return false;
        return Grid[tile.Y][tile.X];
    }
    public IEnumerable<List<DXTVector2i>> RunFirstScan(DXTVector2i start, DXTVector2i target) {
        // Precompute distance/direction fields from a target
        if (DirectionField.ContainsKey(target)) yield break;
        if (!ExactDistanceField.TryAdd(target, new Dictionary<DXTVector2i, float>())) yield break;

        var exactDistanceField = ExactDistanceField[target];
        exactDistanceField[target] = 0;

        var backtrack = new Dictionary<DXTVector2i, DXTVector2i>();
        var queue = new PriorityQueue<DXTVector2i, float>();
        queue.Enqueue(target, 0);

        backtrack[target] = target;
        var reversePath = new List<DXTVector2i>();
        var sw = Stopwatch.StartNew();

        void TryEnqueue(DXTVector2i coord, DXTVector2i previous, float previousDistance) {
            if (!IsTileWalkable(coord)) return;
            if (backtrack.ContainsKey(coord)) return;

            backtrack[coord] = previous;
            var dist = previousDistance + coord.DistanceF(previous);
            exactDistanceField[coord] = dist;
            queue.Enqueue(coord, dist);
        }

        while (queue.TryDequeue(out var current, out var currentDistance)) {
            if (reversePath.Count == 0 && current.Equals(start)) {
                reversePath.Add(current);
                var it = current;
                while (it != target && backtrack.TryGetValue(it, out var prev)) {
                    reversePath.Add(prev);
                    it = prev;
                }
                yield return reversePath;
            }

            foreach (var neighbor in GetNeighbors(current))
                TryEnqueue(neighbor, current, currentDistance);

            if (sw.ElapsedMilliseconds > 100) {
                yield return reversePath;
                sw.Restart();
            }
        }

        // Build DirectionField
        var directionGrid = Grid
        .Select((row, y) => row.Select((_, x) => {
            var coord = new DXTVector2i(x, y);
            if (float.IsPositiveInfinity(GetExactDistance(coord, exactDistanceField))) return (byte)0;

            var neighbors = GetNeighbors(coord);
            var (bestNeighbor, bestDist) = neighbors
                .Select(n => (n, distance: GetExactDistance(n, exactDistanceField)))
                .MinBy(p => p.distance);

            if (float.IsPositiveInfinity(bestDist)) return (byte)0;

            var dirIndex = NeighborOffsets.IndexOf(bestNeighbor - coord);
            return (byte)(dirIndex + 1);
        }).ToArray())
        .ToArray();

        DirectionField[target] = directionGrid;
        ExactDistanceField.TryRemove(target, out _);
    }
    public List<DXTVector2i> FindPath(DXTVector2i start, DXTVector2i target, bool startWalkable = false) {
        // move start to nearest walkable tile
        if (startWalkable && !IsTileWalkable(start)) {
            var nearest = FindNearestWalkable(start, 1); 
            if (nearest.HasValue) start = nearest.Value;
        }

        if (DirectionField.GetValueOrDefault(target) is { } dirField) {
            if (dirField[start.Y][start.X] == 0) return null;
            var path = new List<DXTVector2i>();
            var current = start;

            while (current != target) {
                var dirIndex = dirField[current.Y][current.X];
                if (dirIndex == 0) return null;
                current += NeighborOffsets[dirIndex - 1];
                path.Add(current);
            }

            return path;
        }

        if (!ExactDistanceField.TryGetValue(target, out var exactDistanceField)) return null;
        if (float.IsPositiveInfinity(GetExactDistance(start, exactDistanceField))) return null;

        var fallbackPath = new List<DXTVector2i>();
        var pos = start;
        while (pos != target) {
            pos = GetNeighbors(pos).MinBy(n => GetExactDistance(n, exactDistanceField));
            fallbackPath.Add(pos);
        }

        return fallbackPath;
    }

    // Cached distance and direction fields
    private readonly ConcurrentDictionary<DXTVector2i, Dictionary<DXTVector2i, float>> ExactDistanceField = new();
    private readonly ConcurrentDictionary<DXTVector2i, byte[][]> DirectionField = new();
    private static readonly List<DXTVector2i> NeighborOffsets = [
        new DXTVector2i(0, 1),
        new DXTVector2i(1, 1),
        new DXTVector2i(1, 0),
        new DXTVector2i(1, -1),
        new DXTVector2i(0, -1),
        new DXTVector2i(-1, -1),
        new DXTVector2i(-1, 0),
        new DXTVector2i(-1, 1)
    ];

    private static IEnumerable<DXTVector2i> GetNeighbors(DXTVector2i tile) {
        foreach (var offset in NeighborOffsets)
            yield return tile + offset;
    }
    private static float GetExactDistance(DXTVector2i tile, Dictionary<DXTVector2i, float> dict) {
        return dict.GetValueOrDefault(tile, float.PositiveInfinity);
    }
}