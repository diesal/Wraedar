using Coroutine;
using DieselExileTools;
using GameHelper;
using GameHelper.CoroutineEvents;
using GameHelper.Plugin;
using GameHelper.RemoteEnums;
using GameHelper.RemoteObjects.Components;
using ImGuiNET;
using Newtonsoft.Json;
using System.Reflection.Metadata.Ecma335;
using SColor = System.Drawing.Color;
using SVector2 = System.Numerics.Vector2;


namespace Wraedar;

public sealed class Plugin : PCore<Settings> {

    #region --| Deferred Initialization |-------------------------------------------------------------------------------
    private bool _initialised = false;
    private bool _canEnable = false;
    public override void DrawUI() {
        if (GameHelper.Core.States.GameCurrentState == GameStateTypes.GameNotLoaded) return;
        // Defer OnEnable until game is loaded
        if (!_canEnable) {
            _canEnable = true;
            OnEnable(true);
        }
        if (!_initialised) return;
        if (!Enabled) return;

        Tick();
        Render();
    }
    #endregion

    public bool Enabled = false;
    public static string Name { get; } = "Wraedar";
    public static string SettingsFilename { get; } = $"{Name}.json";

    public string GameHelperPath => Path.GetFullPath(Path.Combine(DllDirectory, "..", ".."));
    public string SettingsPath => Path.Combine(GameHelperPath, "configs", SettingsFilename);
    public string PinPath => Path.Combine(DllDirectory, "PinFiles");


    private DXT.IconAtlas? _iconAtlas;
    public DXT.IconAtlas IconAtlas => _iconAtlas ??= new("Diesel_MapIcons", Path.Combine(DllDirectory,"Media", "MapIcons.png"), new SVector2(32, 32));

    private ImDrawListPtr _wraedarWindowPtr;

    public AreaManager AreaManager { get; } = new();

    //--| Modules |-----------------------------------------------------------------------------------------------------
    private SettingsUI? _settingsUI;
    public SettingsUI SettingsUI => _settingsUI ??= new SettingsUI(this);

    private MapRenderer? _mapRenderer;
    public MapRenderer MapRenderer => _mapRenderer ??= new MapRenderer(this);

    private IconRenderer? _iconRenderer;
    public IconRenderer IconRenderer => _iconRenderer ??= new IconRenderer(this);

    private PinRenderer? _pinRenderer;
    public PinRenderer PinRenderer => _pinRenderer ??= new PinRenderer(this);

    //--| OnEnable |----------------------------------------------------------------------------------------------------
    public override void OnEnable(bool isGameOpened) {
        if (isGameOpened && !_initialised) Initialise();

        onAreaChangeAC = CoroutineHandler.Start(OnAreaChangeCoroutine());

        Enabled = true;

    }

    //--| OnDisable |---------------------------------------------------------------------------------------------------
    public override void OnDisable() {
        Enabled = false;

        onAreaChangeAC = null;
    }

    //--| Initialise |--------------------------------------------------------------------------------------------------
    private void Initialise() {
        _initialised = true;

        LoadSettings();
        Extensions.Settings = Settings;

        Initialise_DXT();

        MapRenderer.Initialise();
        PinRenderer.Initialise();
        IconRenderer.Initialise();
        SettingsUI.Initialise();
    }
    private void Initialise_DXT() {
        DXT.Initialise(new DXT.Config {
            PluginName = Name,
            PluginDirectory = DllDirectory,            
            Settings = Settings.DXTSettings,
        });

        DXT.AddToolbarButtons([
            new DXT.FloatingToolbar.Button {
                Label = "Path",
                Tooltip = new("Debug walkable Terrain for pathfinding around player"),
                SetChecked = state => Settings.DebugWalkableTerrain = state,
                GetChecked = () => Settings.DebugWalkableTerrain,
            }
        ]);

        //DXT.LogHeader = (width, height) => {
        //    DXT.Button.Draw($"{Name}Friendly", ref Settings.DebugFriendlyIcon, new DXT.Button.Options { Label = "Friendly", Width = 80, Height = 22 }); ImGui.SameLine();
        //    ImGui.SameLine();
        //    if (DXT.Button.Draw($"{Name}RebuildIcons", new DXT.Button.Options { Label = "Rebuild", Width = 80, Height = 22, Tooltip = new("Rebuild Icons") })) {
        //        //IconBuilder.RebuildIcons();
        //    }
        //};
    }

    //--| Draw Settings |-----------------------------------------------------------------------------------------------
    public override void DrawSettings() {
        if (!Enabled) return;

        SettingsUI.Draw();
    }


    //--| Tick |-------------------------------------------------------------------------------------------------------
    public void Tick() {
        DXT.Tick();
    }

    //--| Render |-----------------------------------------------------------------------------------------------------
    public void Render() {

        DXT.Render();
        SettingsUI.Render();

        if (Core.States.GameCurrentState is not (GameStateTypes.InGameState or GameStateTypes.EscapeState)) return;
        if (Core.States.InGameStateObject.GameUi.SkillTreeNodesUiElements.Count > 0) return;

        ImGui.SetNextWindowPos(DXT.ActiveMapPosition);
        ImGui.SetNextWindowSize(DXT.ActiveMapSize);
        ImGui.Begin("Wraedar_background",
            ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.NoInputs |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoBackground);
        _wraedarWindowPtr = ImGui.GetWindowDrawList();

        if (DXT.IsLargeMapVisible) {

            DXT.Monitor("Wraedar", "LargeMapZoom", DXT.LargeMap.Zoom);
            DXT.Monitor("Wraedar", "LargeMapScale", DXT.ActiveMapScale);
            DXT.Monitor("Wraedar", "LargeMapCenter", DXT.ActiveMapCenter);
            DXT.Monitor("Wraedar", "LargeMapPosition", DXT.ActiveMapPosition);
            DXT.Monitor("Wraedar", "LargeMapSize", DXT.ActiveMapSize);
            var currentAreaInstance = Core.States.InGameStateObject.CurrentAreaInstance;
            if (currentAreaInstance.Player.TryGetComponent<Render>(out var playerRender)) {
                DXT.Monitor("Wraedar", "Player Height", playerRender.TerrainHeight);
                DXT.Monitor("Wraedar", "Player ClampedHeight", playerRender.TerrainHeight > 0.01f ? playerRender.TerrainHeight : 0f);
            }

            //ImGui.SetNextWindowBgAlpha(0f);
            //ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
            //ImGui.Begin("LargeMap Icon Window", ImGuiHelper.TransparentWindowFlags);
            //ImGui.PopStyleVar();

            MapRenderer.Render();
            PinRenderer.Render();
            IconRenderer.Render();
        }

        ImGui.End();
    }
    //--| Draw Methods |-----------------------------------------------------------------------------------------------

    public void DrawImageQuad(nint textureId, SVector2 p1, SVector2 p2, SVector2 p3, SVector2 p4, SColor color) {
        var uv1 = SVector2.Zero;
        var uv2 = new SVector2(1f, 0f);
        var uv3 = new SVector2(1f, 1f);
        var uv4 = new SVector2(0f, 1f);

        _wraedarWindowPtr.AddImageQuad(textureId, p1, p2, p3, p4, uv1, uv2, uv3, uv4, color.ToImGui());
    }

    public void DrawImage(nint textureId, DXTRect imagePosition, DXTRect imageUV, SColor? color=null) {
        color ??= SColor.White;
        _wraedarWindowPtr.AddImage(textureId, imagePosition.TopLeft, imagePosition.BottomRight, imageUV.TopLeft, imageUV.BottomRight, color.Value.ToImGui());
    }

    public void DrawCircle(SVector2 center, float radius, SColor? color=null, int numSegments = 12, float thickness = 1.0f) {
        color ??= SColor.White;
        _wraedarWindowPtr.AddCircle(center, radius, color.Value.ToImGui(), numSegments, thickness);
    }
    public void DrawCircleFilled(SVector2 center, float radius, SColor? color=null, int numSegments = 12) {
        color ??= SColor.White;
        _wraedarWindowPtr.AddCircleFilled(center, radius, color.Value.ToImGui(), numSegments);
    }

    public static DXTRect GetCenteredRect(SVector2 center, SVector2 size) {
        var halfBgSize = new SVector2(MathF.Round(size.X / 2), MathF.Round(size.Y / 2)); // round the result of the division
        return new DXTRect(center - halfBgSize, center + halfBgSize);
    }
    public static DXTRect GetCenteredRect(SVector2 center, float width, float height) {
        return GetCenteredRect(center, new SVector2(width, height));
    }

    public void DrawRectText(DXTRect textPos, string text, SColor? textColor = null, SColor? backgroundColor = null, float padding = 2f, SColor? borderColor = null, float borderThickness = 1f) {
        textColor ??= SColor.White;
        backgroundColor ??= SColor.Black;
        var boxRect = textPos.Expand(padding);

        _wraedarWindowPtr.AddRectFilled(boxRect.TopLeft, boxRect.BottomRight, backgroundColor.Value.ToImGui());

        if (borderColor.HasValue)
            _wraedarWindowPtr.AddRect(boxRect.TopLeft, boxRect.BottomRight, borderColor.Value.ToImGui(), 0f, ImDrawFlags.None, borderThickness);

        _wraedarWindowPtr.AddText(textPos.TopLeft, textColor.Value.ToImGui(), text);
    }

    public void DrawRectColoredText(DXTRect textPos, string text, SColor? textColor = null, SColor? backgroundColor = null, float padding = 2f, SColor? borderColor = null, float borderThickness = 1f) {
        textColor ??= SColor.White;
        backgroundColor ??= SColor.Black;
        var boxRect = textPos.Expand(padding);

        _wraedarWindowPtr.AddRectFilled(boxRect.TopLeft, boxRect.BottomRight, backgroundColor.Value.ToImGui());
        if (borderColor.HasValue)
            _wraedarWindowPtr.AddRect(boxRect.TopLeft, boxRect.BottomRight, borderColor.Value.ToImGui(), 0f, ImDrawFlags.None, borderThickness);

        var coloredText = new DXT.ColoredText(text);
        coloredText.Draw(_wraedarWindowPtr, textPos.TopLeft, textColor);
    }

    public void DrawCenteredText(SVector2 center, string text, SColor? textColor = null) {
        textColor ??= SColor.White;

        var textSize = ImGui.CalcTextSize(text);
        var textRect = GetCenteredRect(center, textSize);

        _wraedarWindowPtr.AddText(textRect.TopLeft, textColor.Value.ToImGui(), text);
    }
    public void DrawCenteredColoredText(SVector2 center, DXT.ColoredText text, SColor? defaultTextColor = null) {
        defaultTextColor ??= SColor.White;
        var textRect = GetCenteredRect(center, text.Size);
        text.Draw(_wraedarWindowPtr, textRect.TopLeft, defaultTextColor);
    }

    public void DrawLine(SVector2 p1, SVector2 p2, SColor? color = null, float thickness = 1.0f) {
        color ??= SColor.White;
        _wraedarWindowPtr.AddLine(p1, p2, color.Value.ToImGui(), thickness);
    }

    //--| Events |-----------------------------------------------------------------------------------------------------

    public event Action<string, string>? OnAreaChange;
    private ActiveCoroutine? onAreaChangeAC;
    private string _currentArea = string.Empty;
    private string _currentAreaHash = string.Empty;
    private IEnumerator<Wait> OnAreaChangeCoroutine() {
        while (true) {
            yield return new Wait(RemoteEvents.AreaChanged);

            if (!AreaManager.ChangeArea()) continue;
            DXT.Log($"Event: OnAreaChange: {AreaManager.AreaID} ({AreaManager.AreaHash})", false);
            OnAreaChange?.Invoke(AreaManager.AreaID, AreaManager.AreaHash);
        }
    }

    //--| SaveSettings |-----------------------------------------------------------------------------------------------
    public override void SaveSettings() {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
        var settingsData = JsonConvert.SerializeObject(this.Settings, Formatting.Indented);
        File.WriteAllText(SettingsPath, settingsData);
    }

    //--| LoadSettings |-----------------------------------------------------------------------------------------------
    public void LoadSettings() {
        if (File.Exists(SettingsPath)) {
            var settingsData = File.ReadAllText(SettingsPath);
            try {
                var loadedSettings = JsonConvert.DeserializeObject<Settings>(settingsData);
                if (loadedSettings != null) {
                    this.Settings = loadedSettings;
                    DXT.Log($"{Name} Settings loaded from {SettingsPath}", false);
                }
            } catch (JsonException ex) {
                DXT.Log($"{Name} Failed to load settings: {ex.Message}", false);
            }              
        }
    }








}
