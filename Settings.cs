using DieselExileTools;
using GameHelper.Plugin;
using SColor = System.Drawing.Color;

namespace Wraedar
{
    public sealed class Settings : IPSettings
    {
        public DXTSettings DXTSettings = new();  

        public bool SettingsWindowOpen = false;
        public bool SettingsPanelCollapsed = false;

        public bool DrawOverPanels = false;
        public bool DebugWalkableTerrain = false;
        public bool DrawWhenPOEForegroundOnly = false;

        public IconsSettings Icons = new();
        public PinSettings Pin = new();
        public MapSettings Map = new();

        public IconSettings GetIconSettings(IconTypes iconType, IconSettings defaultSettings) {
            if (!Icons.IconSettingsByType.TryGetValue(iconType, out IconSettings? value)) {
                value = defaultSettings;
                Icons.IconSettingsByType[iconType] = value;
            }
            return value;
        }
        public bool GetIconPanelOpen(string category, bool defaultValue = true) {
            if (!Icons.IconPanelsOpen.ContainsKey(category)) {
                Icons.IconPanelsOpen[category] = defaultValue;
            }
            return Icons.IconPanelsOpen[category];
        }
        public void SetIconPanelOpen(string category, bool value) {
            if (Icons.IconPanelsOpen.ContainsKey(category)) {
                Icons.IconPanelsOpen[category] = value;
            } else {
                Icons.IconPanelsOpen.Add(category, value);
            }
        }
        public void NewCustomPathIcon() {
            var customIcon = new IconSettings {
                Path = "Metadata/CustomPath/ReplaceMe",
            };
            Icons.CustomPathIcons.Add(customIcon);
        }
        public void RemoveCustomPathIcon(int index) {
            if (index >= 0 && index < Icons.CustomPathIcons.Count) Icons.CustomPathIcons.RemoveAt(index);
        }

    }
    public sealed class MapSettings
    {
        public bool SettingsPanelCollapsed = false;
        public bool Enabled = true;

        public SColor Color = DXT.Color.FromRGBA(155, 155, 201, 100);
    }

    public sealed class PinSettings
    {
        public bool SettingsPanelCollapsed = false;
        public bool Enabled = true;
        public bool DrawPaths = true;
        public bool OverrideColorPaths = false;
        public int PathThickness = 1;

        public bool EditorWindowOpen = false;
        public bool EditorCurrentAreaPanelCollapsed = false;
        public bool EditorCommonAreasPanelCollapsed = false;
        public bool EditorSelectedAreaPanelCollapsed = false;
        public string? SelectedFilename;

        public bool ShowTiles = false;
        public bool IgnoreTerrainHeight = false;
        public string TileFilter = "";
        public int TilePositionFreqFilter = 1;

        public bool test1 = false;
        public bool test2 = false;
        public bool test3 = false;
        public bool test4 = false;

    }
    public sealed class IconsSettings {
        public bool SettingsPanelCollapsed = false;
        public bool Enabled = true;
        public bool PixelPerfect = true;
        public bool DrawCached = true;

        public Dictionary<IconTypes, IconSettings> IconSettingsByType { get; set; } = new();
        public List<IconSettings> CustomPathIcons { get; set; } = new List<IconSettings> { };

        public Dictionary<string, bool> IconPanelsOpen { get; set; } = new Dictionary<string, bool>();



    }
    public class IconSettings {
        public bool Draw = true;
        public bool DrawName = false;
        public bool DrawHealth = false;
        public bool AnimateLife = false;
        public int Size = 32;
        public int Index = 0;
        public SColor Tint = DXT.Color.FromHsla(255, 255, 255, 255);
        public SColor HiddenTint = DXT.Color.FromRGBA(128, 128, 128, 255);
        public SColor ArmingTint = DXT.Color.FromHsla(255, 255, 255, 255);

        // Ingame icons
        public IngameIconDrawStates DrawState = IngameIconDrawStates.Off;

        // custom path icon settings
        public string Path = "";
        public bool Check_IsAlive = false;
        public bool Check_IsOpened = false;
    }

}
