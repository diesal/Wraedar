using DieselExileTools;
using GameHelper;
using GameHelper.RemoteEnums;
using GameHelper.RemoteObjects.Components;
using SVector2 = System.Numerics.Vector2;

namespace Wraedar;

public sealed class MapRenderer(Plugin plugin) : PluginModule(plugin)
{
    public void Initialise() {
        plugin.OnAreaChange += OnAreaChange;
    }

    public void Render() {
        if (!Settings.Map.Enabled) return;
        if (Plugin.AreaManager.Texture == null) return;

        var player = Core.States.InGameStateObject.CurrentAreaInstance.Player;
        if (!player.TryGetComponent<Render>(out var playerRender)) return;
        var playerHeight = -playerRender.TerrainHeight;

        var rect = new DXTRect( new SVector2(-playerRender.GridPosition.X, -playerRender.GridPosition.Y),
            Plugin.AreaManager.TextureSize.X,
            Plugin.AreaManager.TextureSize.Y
        );

        var p1 = DXT.GridToMap(rect.Left, rect.Top, playerHeight);
        var p2 = DXT.GridToMap(rect.Right, rect.Top, playerHeight);
        var p3 = DXT.GridToMap(rect.Right, rect.Bottom, playerHeight);
        var p4 = DXT.GridToMap(rect.Left, rect.Bottom, playerHeight);

        Plugin.DrawImageQuad(Plugin.AreaManager.Texture.Value, p1, p2, p3, p4, Settings.Map.Color);
    }

    private void OnAreaChange(string areaID, string areaHash) {
        if (Core.States.GameCurrentState is not (GameStateTypes.InGameState or GameStateTypes.EscapeState)) return;

        if (plugin.AreaManager.GenerateMapTexture()) {
            DXT.Log($"MapRenderer: Generated map texture for area: {areaID}", false);
            DXT.Monitor("MapRenderer", "CurrentArea.TextureSize", plugin.AreaManager.TextureSize);
        }
        else {
            DXT.Log($"MapRenderer: Failed to generate map texture for area: {areaID}", false);
            return;
        }

    }


}
