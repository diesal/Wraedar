
namespace Wraedar;


public enum IconTypes
{
    Unset,

    CustomPath,
    // Totems
    TotemGeneric,

    // Friendly Icons
    NPC,
    LocalPlayer,
    OtherPlayer,
    DecoyTotem,
    Minion,
    // Monsters
    NormalMonster,
    MagicMonster,
    RareMonster,
    UniqueMonster,
    PinnacleBoss,
    RogueExile,
    GiantRogueExile,
    Spirit,

    // Einhar Beasts
    VividVulture,
    BlackMorrigan,
    CraicicChimeral,
    WildBristleMatron,
    WildHellionAlpha,
    FenumalPlaguedArachnid,
    FenumusFirstOfTheNight,
    // Dangerous icons 
    DrowningOrb,
    VolatileCore,
    ConsumingPhantasm,
    LightningClone,
    // minimap icons 
    Shrine,
    Breach,
    QuestObject,
    Ritual,
    Waypoint,
    Checkpoint,
    AreaTransition,
    IngameNPC,
    IngameUncategorized,
    // Delirium 
    FracturingMirror,
    BloodBag,
    EggFodder,
    GlobSpawn,
    // Chest
    UnknownChest,
    BreakableObject,
    BreachChestNormal,
    BreachChestLarge,
    ExpeditionChestWhite,
    ExpeditionChestMagic,
    ExpeditionChestRare,
    SanctumChest,
    PirateChest,
    AbyssChest,
    ChestWhite,
    ChestMagic,
    ChestRare,
    ChestUnique,

    SanctumMote,

    // Strongbox types
    UnknownStrongbox,
    ArcanistStrongbox,
    ArmourerStrongbox,
    BlacksmithStrongbox,
    ArtisanStrongbox,
    CartographerStrongbox,
    ChemistStrongbox,
    GemcutterStrongbox,
    JewellerStrongbox,
    LargeStrongbox,
    OrnateStrongbox,
    DivinerStrongbox,
    OperativeStrongbox,
    ArcaneStrongbox,
    ResearcherStrongbox,

    // Traps
    GroundSpike,
}
public enum IconSettingsTypes
{
    Default,
    Custom,
    IngameIcon,
    Monster,
    Chest,
    Friendly,
    Trap
}
public enum IngameIconDrawStates {
    Off = 0,
    Ranged = 1,
    Always = 2
}
