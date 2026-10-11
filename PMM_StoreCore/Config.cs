using CounterStrikeSharp.API.Core;

namespace PMM_StoreCore;

public class StoreCoreConfig : BasePluginConfig
{
    // false: cs2-store keeps its own menu (through PanoramaMenuManager).
    public bool Enabled { get; set; } = true;

    // PMM menu types (the player's choice in !menu) that get the store panel.
    // The panel is clicked with the mouse, so it is PanoramaMenu by default.
    // Everyone else keeps the normal cs2-store menu drawn by PMM.
    public List<string> MenuTypes { get; set; } = new() { "PanoramaMenu" };

    public string LayoutPath { get; set; } = "panorama/layout/custom_game/pmm_store.xml";

    // Center, Left, Right.
    public string Position { get; set; } = "Center";

    // Pause before the panel is drawn. Drawing in the frame of the chat command kicks with NETWORK_DISCONNECT.
    public float OpenDelay { get; set; } = 0.2f;

    // Pause before the mouse is captured.
    public float InputDelay { get; set; } = 0.2f;

    public float ToastSeconds { get; set; } = 2.5f;

    public string DateFormat { get; set; } = "dd.MM.yyyy";

    // After a skin, trail, tag ... is bought it is put on at once.
    public bool EquipAfterPurchase { get; set; } = true;

    // Amount buttons of the "Gift credits" block (up to 4).
    public List<int> GiftAmounts { get; set; } = new() { 50, 100, 250, 500 };

    // Settings tab: "hide other players' trails" switch (cs2-store HideTrails command).
    public bool ShowHideTrails { get; set; } = true;

    // Settings tab: "gift credits" block (cs2-store Gift command).
    public bool ShowGift { get; set; } = true;

    // Item type -> icon (names: tools/gen_layout.py ICONS). Weapons use the weapon icon of the item.
    public Dictionary<string, string> TypeIcons { get; set; } = new()
    {
        ["playerskin"] = "customplayer",
        ["weapon"] = "bullet",
        ["health"] = "health",
        ["armor"] = "armor",
        ["gravity"] = "parachute",
        ["speed"] = "fast",
        ["godmode"] = "shield",
        ["respawn"] = "refresh",
        ["bunnyhop"] = "exojump",
        ["open"] = "key",
        ["sound"] = "sound_3",
        ["link"] = "star",
        ["smoke"] = "smokegrenade",
        ["trail"] = "random",
        ["tracer"] = "bullet_burst",
        ["grenadetrail"] = "hegrenade",
        ["coloredskin"] = "colorwheel",
        ["customweapon"] = "loadout",
        ["wings"] = "trophy",
        ["equipment"] = "inventory",
        ["chattag"] = "clantag",
        ["scoretag"] = "clantag",
        ["namecolor"] = "colorwheel",
        ["chatcolor"] = "colorwheel",
    };

    // Category key (as in cs2-store.json) -> icon. Without an entry the icon of the first item inside is used.
    public Dictionary<string, string> CategoryIcons { get; set; } = new()
    {
        ["Features"] = "star",
        ["Tags"] = "clantag",
    };

    public string DefaultIcon { get; set; } = "star";
}
