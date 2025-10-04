using DieselExileTools;
using GameHelper;
using GameHelper.RemoteEnums;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SColor = System.Drawing.Color;
using SVector2 = System.Numerics.Vector2;

namespace Wraedar;

/// <summary>
/// Detects if the current map location is map edge or not.
/// </summary>
public class AreaManager {

    private const string textureName = "wraedar_map_terrain";
    private int terraintBytesPerRow;
    private byte[]? gridData;
    private float[][]? girdHeightData;
    private float worldToGridHeightMultiplier;

    public string AreaID { get; private set; } = "Unknown";
    public string AreaHash { get; private set; } = "Unknown";

    public int TotalRows { get; private set; } = 0;
    public nint? Texture { get; private set; }
    public SVector2 TextureSize { get; private set; } = SVector2.Zero;


    public void ClearArea () {
        Texture = null;
        TextureSize = SVector2.Zero;
        Core.Overlay.RemoveImage(textureName);

        AreaID = "Unknown";
        gridData = null;
        girdHeightData = null;
        terraintBytesPerRow = 0;
        worldToGridHeightMultiplier = 0f;
        TotalRows = 0;

    }

    public bool ChangeArea() {
        var areaInstance = Core.States.InGameStateObject.CurrentAreaInstance;
        if (areaInstance == null) {
            ClearArea();
            return false;
        }

        var newAreaID = Core.States?.InGameStateObject?.CurrentWorldInstance?.AreaDetails?.Id;
        var newAreaHash = areaInstance.AreaHash;
        if (string.IsNullOrEmpty(newAreaID)) {
            ClearArea();
            return false;
        }
        if (AreaID == newAreaID && AreaHash == newAreaHash) return false;

        if (Core.States.GameCurrentState is not (GameStateTypes.InGameState or GameStateTypes.EscapeState)) return false;

        AreaID = newAreaID;
        AreaHash = newAreaHash;

        gridData = areaInstance.GridWalkableData;
        girdHeightData = areaInstance.GridHeightData;
        terraintBytesPerRow = areaInstance.TerrainMetadata.BytesPerRow;
        worldToGridHeightMultiplier = areaInstance.WorldToGridConvertor * 2f;
        TotalRows = gridData.Length / terraintBytesPerRow;

        Texture = null;
        TextureSize = SVector2.Zero;
        Core.Overlay.RemoveImage(textureName);

        return true;
    }
    public bool GenerateMapTexture() {
        if (terraintBytesPerRow <= 0) {
            DXT.Log("TerrainMetadata.BytesPerRow is 0 or less, cannot generate map texture for current area", false);
            return false;
        }
        if (gridData == null || girdHeightData == null) return false;

        var configuration = Configuration.Default.Clone();
        configuration.PreferContiguousImageBuffers = true;

        using Image<Rgba32> image = new(configuration, terraintBytesPerRow * 2, TotalRows);
        Parallel.For(0, girdHeightData.Length, y => {
            for (var x = 1; x < girdHeightData[y].Length - 1; x++) {
                if (!IsBorder(x, y)) continue;

                var height = (int)(girdHeightData[y][x] / worldToGridHeightMultiplier);
                var imageX = x - height;
                var imageY = y - height;

                if (IsInsideMapBoundary(imageX, imageY)) {
                    image[imageX, imageY] = new Rgba32(SColor.White.ToVector4());
                }
            }
        });
        TextureSize = new(image.Width, image.Height);

        if (Math.Max(image.Width, image.Height) > 8192) {
            var (newWidth, newHeight) = (image.Width, image.Height);
            if (image.Height > image.Width) {
                newWidth = newWidth * 8192 / newHeight;
                newHeight = 8192;
            }
            else {
                newHeight = newHeight * 8192 / newWidth;
                newWidth = 8192;
            }

            var targetSize = new Size(newWidth, newHeight);
            var resizer = new ResizeProcessor(new ResizeOptions { Size = targetSize }, image.Size)
                .CreatePixelSpecificCloningProcessor(configuration, image, image.Bounds);
            resizer.Execute();
        }

        Core.Overlay.AddOrGetImagePointer(textureName, image, false, out var t);
        Texture = t;
        return true;
    }
    public Image<Rgba32>? GenerateDebugTexture() {
        if (terraintBytesPerRow <= 0) return null;
        var configuration = Configuration.Default.Clone();
        configuration.PreferContiguousImageBuffers = true;
        var image = new Image<Rgba32>(configuration, terraintBytesPerRow * 2, TotalRows);
        Parallel.For(0, girdHeightData.Length, y => {
            for (var x = 1; x < girdHeightData[y].Length - 1; x++) {
                if (!IsBorder(x, y)) continue;
                var height = (int)(girdHeightData[y][x] / worldToGridHeightMultiplier);
                var imageX = x - height;
                var imageY = y - height;
                if (IsInsideMapBoundary(imageX, imageY)) {
                    image[imageX, imageY] = new Rgba32(SColor.Red.ToVector4());
                }
            }
        });
        return image;
    }


    /// <summary>
    /// Detects if the current tile is a border.
    ///
    /// The current tile is a border if it itself is not walkable and at least one adjacent tile is walkable.
    /// At least one adjacent tile has to be walkable to avoid not just drawing all non-walkable tiles.
    /// </summary>
    /// <returns>True if the current tile is a border, false otherwise.</returns>
    public bool IsBorder(int x, int y)
    {
        var index = (y * terraintBytesPerRow) + (x / 2); // (x / 2) => since there are 2 data points in 1 byte.
        var (oneIfFirstNibbleZeroIfNot, zeroIfFirstNibbleOneIfNot) = NibbleHandler(x);
        var shiftIfSecondNibble = zeroIfFirstNibbleOneIfNot * 0x4;

        var currentTile = GetTileValueAt(index, shiftIfSecondNibble);

        // we add the extra condition if currentTile != 1 to make the border thicker.
        if (currentTile != 1 && CanWalk(currentTile))
        {
            return false;
        }

        var upTile = GetTileValueAt(index + terraintBytesPerRow, shiftIfSecondNibble);
        if (CanWalk(upTile))
        {
            return true;
        }

        var downTile = GetTileValueAt(index - terraintBytesPerRow, shiftIfSecondNibble);
        if (CanWalk(downTile))
        {
            return true;
        }

        var shiftIfFirstNibble = oneIfFirstNibbleZeroIfNot * 0x4;
        var leftTile = GetTileValueAt(index - oneIfFirstNibbleZeroIfNot, shiftIfFirstNibble);
        if (CanWalk(leftTile))
        {
            return true;
        }

        var rightTile = GetTileValueAt(index + zeroIfFirstNibbleOneIfNot, shiftIfFirstNibble);
        return CanWalk(rightTile);
    }

    /// <summary>
    /// Checks if (ImageX,ImageY) coordinate is within the width and height of the map.
    /// </summary>
    /// <param name="imageX"></param>
    /// <param name="imageY"></param>
    /// <returns>True if X,Y is within the boundary of the image. Otherwise false</returns>
    public bool IsInsideMapBoundary(int imageX, int imageY)
    {
        var width = terraintBytesPerRow * 2;
        return imageX < width && imageX >= 0 && imageY < TotalRows && imageY >= 0;
    }

    /// <summary>
    /// 0 = not walkable 1,2,3,4,5 means potentially walkable.
    /// It's potentially walkable because it also depends on entity size
    /// (e.g. if entity size is 1 then 1 or above is walkable and
    /// if entity size is 3 than 3 or above is walkable). For the purpose
    /// of generating map we will assume everything above 0 is walkable.
    /// </summary>
    /// <param name="tileValue">map tile walkable value</param>
    /// <returns></returns>
    private static bool CanWalk(int tileValue)
        => tileValue != 0;

    /// <summary>
    /// returns 1, 0 if x lies in first nibble otherwise 0, 1.
    /// </summary>
    /// <param name="x">map walkable data array index.</param>
    /// <returns></returns>
    private static (int oneIfFirstNibbleZeroIfNot, int zeroIfFirstNibbleOneIfNot) NibbleHandler(int x)
        => x % 2 == 0 ? (1, 0) : (0, 1);

    private int GetTileValueAt(int index, int shiftAmount)
    {
        var data = this.gridData.ElementAtOrDefault(index);
        return (data >> shiftAmount) & 0xF;
    }





}
