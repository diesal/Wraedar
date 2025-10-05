using DieselExileTools;
using GameHelper;
using GameHelper.RemoteEnums;
using GameHelper.RemoteEnums.Entity;
using GameHelper.RemoteObjects;
using GameHelper.RemoteObjects.Components;
using GameHelper.RemoteObjects.States.InGameStateObjects;
using ImGuiNET;
using System;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using static DieselExileTools.DXT;
using SColor = System.Drawing.Color;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;



namespace Wraedar;

public sealed class IconRenderer(Plugin plugin) : PluginModule(plugin) {

    private SVector3 _playerPosition;
    public static readonly HashSet<string> RogueExilesByName = new HashSet<string>{
        "Taua, the Ruthless",
        "Raok, the Bloodthirsty",
        "Bronnach, the Manhunter",
        "Adrienne, the Malignant Rose",
        "Vasa, of the Death Akhara",
        "Ciara, the Curse Weaver",
        "Nyassa, the Flaming Hand",
        "Hesperia, the Arcane Tempest",
        "Ulfred, the Afflicted",
        "Drusian, the Artillerist",
        "Sondar, the Stormbinder",
        "Doran, the Deft",
    };

    //--| Initialise |--------------------------------------------------------------------------------------------------
    public void Initialise() {


    }

    //--| Render |------------------------------------------------------------------------------------------------------
    public void Render() { 
        RenderIcons();
    }

    //--| Render Icons |------------------------------------------------------------------------------------------------
    public void RenderIcons() {
        if (!Settings.Icons.Enabled) return;

        var currentAreaInstance = Core.States.InGameStateObject.CurrentAreaInstance;
        if (!currentAreaInstance.Player.TryGetComponent<Render>(out var playerRender)) {
            DXT.Log("IconRenderer: Could not get player Render component", false);
            return;
        }
        _playerPosition = new SVector3(playerRender.GridPosition.X, playerRender.GridPosition.Y, playerRender.TerrainHeight);

        foreach (var e in currentAreaInstance.AwakeEntities) { 
            var entity = e.Value;
            if (entity == null) continue;
            if (!Settings.Icons.DrawCached && !entity.IsValid) continue;
            if (!entity.TryGetComponent<Render>(out var entityRender)) continue;          

            var entityState = entity.EntityState;
            var entityType = entity.EntityType;
            var entitySubtype = entity.EntitySubtype;

            var customPathIconSettings = Settings.Icons.CustomPathIcons.FirstOrDefault(settings => entity.Path.StartsWith(settings.Path, StringComparison.Ordinal));
            if (customPathIconSettings != null) {
                RenderIcon_Custom(entity, customPathIconSettings);
                continue;
            }

            if (entity.Path.StartsWith("Metadata/Monsters/MarakethSanctumTrial/Hazards/", StringComparison.Ordinal)) {
                RenderIcon_Trap(entity);
                continue;
            }
            if (entity.Path.StartsWith("Metadata/Terrain/Leagues/Sanctum/Objects/SanctumMote")) {
                RenderIcon_Currency(entity, GetIconSettings(IconTypes.SanctumMote));
                continue;
            }
            if (entityState == EntityStates.Useless) continue;

            if (entity.EntityType == EntityTypes.NPC) {
                RenderIcon_Friendly(entity, GetIconSettings(IconTypes.NPC));
                continue;
            }
            if (entityState == EntityStates.MonsterFriendly) {
                RenderIcon_Friendly(entity);
                continue;
            }
            if (entitySubtype == EntitySubtypes.PlayerSelf) {
                RenderIcon_Friendly(entity, GetIconSettings(IconTypes.LocalPlayer));
                continue;
            }
            if (entitySubtype == EntitySubtypes.PlayerOther) {
                RenderIcon_Friendly(entity, GetIconSettings(IconTypes.OtherPlayer));
                continue;
            }
            if (entity.Path.StartsWith("Metadata/Monsters/LeagueDelirium/DoodadDaemons", StringComparison.Ordinal)) {
                if (entity.Path.Contains("ShardPack", StringComparison.OrdinalIgnoreCase)) {
                    RenderIcon_Standard(entity, GetIconSettings(IconTypes.FracturingMirror));
                }
                continue;
            }
            if (entityType == EntityTypes.Monster) {
                RenderIcon_Monster(entity);
                continue;
            }
            if (entityType == EntityTypes.Chest) {
                RenderIcon_Chest(entity);
                continue;
            }
        }
    }

    public void RenderIcon_Friendly(Entity entity, IconSettings? iconSettings = null) {
        //if (!entity.TryGetComponent<ObjectMagicProperties>(out var omp) && omp.Mods != null && omp.Mods.Any(mod => mod.name.Contains("MonsterConvertsOnDeath_"))) return;
        if (iconSettings == null) { 
            if (entity.Path.Contains("Totem"))
                iconSettings = GetIconSettings(IconTypes.TotemGeneric);
            else
                iconSettings = GetIconSettings(IconTypes.Minion);
            if (iconSettings == null) return;

        }
        if (!iconSettings.Draw) return;

        // build label
        string? nameLabel = iconSettings.DrawName ? GetNameLabel(entity) : null;
        string? healthLabel = iconSettings.DrawHealth ? GetHealthLabel(entity) : null;
        string? label = null;
        if (nameLabel != null || healthLabel != null) {
            if (healthLabel == null) label = nameLabel;
            else if (nameLabel == null) label = healthLabel;
            else label = $"{nameLabel} ({healthLabel})";
        }
        bool hidden = false;
        if (entity.TryGetComponent<Buffs>(out var buffsComponent) && buffsComponent.StatusEffects.Keys.Any(k => k.Contains("hidden_monster", StringComparison.OrdinalIgnoreCase))) {
            hidden = true;
        }
        // get color
        var iconColor = hidden ? iconSettings.HiddenTint : iconSettings.Tint;
        // get index 
        var iconIndex = iconSettings.Index;
        if (iconSettings.AnimateLife) {
            iconIndex = GetLifeIconIndex(entity, iconSettings.Index, 8);
        }

        RenderAtlasIcon(entity, iconSettings.Size, iconIndex, iconColor, label);
    }
    public void RenderIcon_Trap(Entity entity, IconSettings? iconSettings = null) {
        //if (!entity.TryGetComponent<ObjectMagicProperties>(out var omp) && omp.Mods != null && omp.Mods.Any(mod => mod.name.Contains("MonsterConvertsOnDeath_"))) return;
        if (iconSettings == null) {
            if (entity.Path.Contains("GroundSpike"))
                iconSettings = GetIconSettings(IconTypes.GroundSpike);
            if (iconSettings == null) return;
        }
        if (!iconSettings.Draw) return;

        var armed = false;
        var up = false;
        if (!entity.TryGetComponent<StateMachine>(out var smc)) return;
        if (smc.States.Count > 3) {
            if (smc.States[2].Value == 1) up = true; 
            if (smc.States[3].Value == 1) armed = true;
        }
        // get color
        var iconColor = up ? iconSettings.Tint : armed ? iconSettings.ArmingTint : iconSettings.HiddenTint;

        RenderAtlasIcon(entity, iconSettings.Size, iconSettings.Index, iconColor);
    }
    public void RenderIcon_Currency(Entity entity, IconSettings? iconSettings = null) {
        //if (!entity.TryGetComponent<ObjectMagicProperties>(out var omp) && omp.Mods != null && omp.Mods.Any(mod => mod.name.Contains("MonsterConvertsOnDeath_"))) return;
        if (iconSettings == null) {
            if (iconSettings == null) return;
        }
        if (!iconSettings.Draw) return;

        RenderAtlasIcon(entity, iconSettings.Size, iconSettings.Index, iconSettings.Tint);
    }
    public void RenderIcon_Chest(Entity entity, IconSettings? iconSettings = null) {
        if (!entity.TryGetComponent<ObjectMagicProperties>(out var omp)) return;
        if (!entity.TryGetComponent<Chest>(out var chestComponent)) return;
        if (chestComponent.IsOpened) return;

        if (iconSettings == null) {
            if (entity.Path.Contains("BreachChest")) {
                if (entity.Path.Contains("Large")) {
                    iconSettings = GetIconSettings(IconTypes.BreachChestLarge);
                } else {
                    iconSettings = GetIconSettings(IconTypes.BreachChestNormal);
                }
            }
            else if (entity.Path.StartsWith("Metadata/Chests/LeaguesExpedition/")) {
                if (omp.Rarity == Rarity.Normal)
                    iconSettings = GetIconSettings(IconTypes.ExpeditionChestWhite);
                else if (omp.Rarity == Rarity.Magic)
                    iconSettings = GetIconSettings(IconTypes.ExpeditionChestMagic);
                else if (omp.Rarity == Rarity.Rare)
                    iconSettings = GetIconSettings(IconTypes.ExpeditionChestRare);
                //else if (omp.Rarity == Rarity.Unique)
                //    iconSettings = GetIconSettings(IconTypes.ExpeditionChestUnique);
            }
            else if (entity.Path.StartsWith("Metadata/Chests/LeagueSanctum/", StringComparison.Ordinal))
                iconSettings = GetIconSettings(IconTypes.SanctumChest);
            else if (entity.Path.StartsWith("Metadata/Chests/GraveyardBooty/", StringComparison.Ordinal))
                iconSettings = GetIconSettings(IconTypes.PirateChest);
            else if (entity.Path.StartsWith("Metadata/Chests/AbyssChest/", StringComparison.Ordinal))
                iconSettings = GetIconSettings(IconTypes.AbyssChest);
            else if (omp.Rarity == Rarity.Normal)
                iconSettings = GetIconSettings(IconTypes.ChestWhite);
            else if (omp.Rarity == Rarity.Magic)
                iconSettings = GetIconSettings(IconTypes.ChestMagic);
            else if (omp.Rarity == Rarity.Rare)
                iconSettings = GetIconSettings(IconTypes.ChestRare);
            else if (omp.Rarity == Rarity.Unique)
                iconSettings = GetIconSettings(IconTypes.ChestUnique);
            else
                iconSettings = GetIconSettings(IconTypes.UnknownChest);
            if (iconSettings == null) return;
        }
        if (!iconSettings.Draw) return;

        RenderAtlasIcon(entity, iconSettings.Size, iconSettings.Index, iconSettings.Tint);
    }
    public void RenderIcon_Custom(Entity entity, IconSettings iconSettings) {
        if (iconSettings.Check_IsAlive && entity.EntityState == EntityStates.Useless) return;
        if (iconSettings.Check_IsOpened && entity.TryGetComponent<Chest>(out var chestComponent) && chestComponent.IsOpened) return;
        if (!iconSettings.Draw) return;

        // build label
        string? nameLabel = iconSettings.DrawName ? GetNameLabel(entity) : null;
        string? healthLabel = iconSettings.DrawHealth ? GetHealthLabel(entity) : null;
        string? label = null;
        if (nameLabel != null || healthLabel != null) {
            if (healthLabel == null) label = nameLabel;
            else if (nameLabel == null) label = healthLabel;
            else label = $"{nameLabel} ({healthLabel})";
        }
        bool hidden = false;
        if (entity.TryGetComponent<Buffs>(out var buffsComponent) && buffsComponent.StatusEffects.Keys.Any(k => k.Contains("hidden_monster", StringComparison.OrdinalIgnoreCase))) {
            hidden = true;
        }

        // get color
        var iconColor = hidden ? iconSettings.HiddenTint : iconSettings.Tint;

        // index 
        var iconIndex = iconSettings.Index;
        if (iconSettings.AnimateLife) {
            iconIndex = GetLifeIconIndex(entity, iconSettings.Index, 8);
        }

        RenderAtlasIcon(entity, iconSettings.Size, iconIndex, iconColor, label);
    }
    public void RenderIcon_Standard(Entity entity, IconSettings? iconSettings) {
        if ( iconSettings == null || !iconSettings.Draw) return;

        // build label
        string? nameLabel = iconSettings.DrawName ? GetNameLabel(entity) : null;
        string? healthLabel = iconSettings.DrawHealth ? GetHealthLabel(entity) : null;
        string? label = null;
        if (nameLabel != null || healthLabel != null) {
            if (healthLabel == null) label = nameLabel;
            else if (nameLabel == null) label = healthLabel;
            else label = $"{nameLabel} ({healthLabel})";
        }

        // get color
        var iconColor = iconSettings.Tint;

        // index 
        var iconIndex = iconSettings.Index;

        RenderAtlasIcon(entity, iconSettings.Size, iconIndex, iconColor, label);
    }
    public void RenderIcon_Monster(Entity entity, IconSettings? iconSettings=null) {
        if (!entity.TryGetComponent<ObjectMagicProperties>(out var omp)) return;

        if (iconSettings == null) {
            if (entity.EntitySubtype == EntitySubtypes.PinnacleBoss)
                iconSettings = GetIconSettings(IconTypes.PinnacleBoss);
            else if (omp.Rarity == Rarity.Normal)
                iconSettings = GetIconSettings(IconTypes.NormalMonster);
            else if (omp.Rarity == Rarity.Magic)
                iconSettings = GetIconSettings(IconTypes.MagicMonster);
            else if (omp.Rarity == Rarity.Rare)
                iconSettings = GetIconSettings(IconTypes.RareMonster);
            else if (omp.Rarity == Rarity.Unique)
                iconSettings = GetIconSettings(IconTypes.UniqueMonster);
            else return;
            if (iconSettings == null) return;
        }
        if (!iconSettings.Draw) return;

        // build label
        string? nameLabel = iconSettings.DrawName ? GetNameLabel(entity) : null;
        string? healthLabel = iconSettings.DrawHealth ? GetHealthLabel(entity) : null;
        string? label = null;
        if (nameLabel != null || healthLabel != null) {
            if (healthLabel == null) label = nameLabel;
            else if (nameLabel == null) label = healthLabel;
            else label = $"{nameLabel} ({healthLabel})";
        }
        bool hidden = false;
        if(entity.TryGetComponent<Buffs>(out var buffsComponent) && buffsComponent.StatusEffects.Keys.Any(k => k.Contains("hidden_monster", StringComparison.OrdinalIgnoreCase))) {
            hidden = true;
        }

        // get color
        var iconColor = hidden ? iconSettings.HiddenTint : iconSettings.Tint;

        // index 
        var iconIndex = iconSettings.Index;
        if (iconSettings.AnimateLife) {
            iconIndex = GetLifeIconIndex(entity, iconSettings.Index, 8);
        }

        RenderAtlasIcon(entity, iconSettings.Size, iconIndex, iconColor, label);
    }

    public void RenderAtlasIcon(Entity entity, int iconSize, int iconIndex, SColor iconColor, string? label=null) {
        var iconRect = GetIconRect(entity, iconSize);
        if (iconRect == null) return;

        plugin.DrawImage(plugin.IconAtlas.TextureId, iconRect.Value, plugin.IconAtlas.GetIconUVRect(iconIndex), iconColor);
        if (!string.IsNullOrEmpty(label)) {
            var textSize = ImGui.CalcTextSize(label);
            SVector2 textPos = new(iconRect.Value.Left + (iconRect.Value.Width / 2), iconRect.Value.Bottom + (textSize.Y/2) - 2);
            var textRect = Plugin.GetCenteredRect(textPos, textSize);
            Plugin.DrawRectColoredText(textRect, label);
        }
    }

    public int GetLifeIconIndex(Entity entity, int baseIconIndex, int steps) {
        if (!entity.TryGetComponent<Life>(out var lifeComponent)) return baseIconIndex;
        float hpPCT = Math.Clamp((float)lifeComponent.Health.Current / lifeComponent.Health.Unreserved, 0f, 1f);
        int offset = (int)((1f - hpPCT) * steps);
        offset = Math.Clamp(offset, 0, steps - 1);
        return baseIconIndex + offset;
    }
    public string? GetHealthLabel(Entity entity) {
        if (!entity.TryGetComponent<Life>(out var lifeComponent)) return null;
        return $"{lifeComponent.Health.CurrentInPercent()}%";
    }
    public string? GetNameLabel(Entity entity) {
        if (!entity.TryGetComponent<Player>(out var playerComponent)) return null;
        return playerComponent.Name;
    }
    public DXTRect? GetIconRect(Entity entity, int iconSize) {
        if (!entity.TryGetComponent<Render>(out var entityRender)) return null;
        var gridPosX = entityRender.GridPosition.X - _playerPosition.X;
        var gridPosY = entityRender.GridPosition.Y - _playerPosition.Y;
        var worldPosZ = entityRender.TerrainHeight - _playerPosition.Z;
        var iconMapPos = DXT.GridToMap(gridPosX, gridPosY, worldPosZ);
        float halfSize = iconSize / 2;

        if (Settings.Icons.PixelPerfect)
            return new DXTRect(new(MathF.Round(iconMapPos.X - halfSize), MathF.Round(iconMapPos.Y - halfSize)), iconSize, iconSize);
        else
            return new DXTRect(new(iconMapPos.X - halfSize, iconMapPos.Y - halfSize), iconSize, iconSize);
    }
    public IconSettings? GetIconSettings(IconTypes type) {
        return Settings.Icons.IconSettingsByType.TryGetValue(type, out var settings) ? settings : null;
    }


}
