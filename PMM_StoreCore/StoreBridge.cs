using System.Collections;
using System.Reflection;
using System.Text.Json;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using static StoreApi.Store;

namespace PMM_StoreCore;

// One entry of the store tree: a category (Item == null) or an item.
internal sealed class Node
{
    public string Key = "";
    public string Name = "";
    public Dictionary<string, string>? Item;
    public JsonElement Element;
    public bool RootItems;   // virtual category with the items that sit at the top of cs2-store.json

    public bool IsItem => Item != null;
    public string Uid => Item?["uniqueid"] ?? "";
    public string Type => Item?["type"] ?? "";
}

// Everything cs2-store does not give through IStoreApi, by reflection on the loaded cs2-store assembly
// (namespace Store, v33). Tree filtering is the same as Store.Menu.BuildMenu / AddItems.
internal sealed class StoreBridge
{
    private const BindingFlags S = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags I = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    public const string RootKey = "\u0001root";

    private readonly ILogger _log;
    public Assembly Asm { get; }

    private readonly PropertyInfo _instance;            // Store.Store.Instance
    private readonly MethodInfo _getItemName;          // Item.GetItemName(player, item)
    private readonly MethodInfo _isHidden;             // Item.IsHidden(item)
    private readonly MethodInfo? _playerHasAny;        // Item.PlayerHasAny(player, JsonElement)
    private readonly MethodInfo _checkFlag;            // MenuBase.CheckFlag(player, string?, bool)
    private readonly MethodInfo? _sellingPrice;        // MenuBase.GetSellingPrice(item, Store_Item)
    private readonly MethodInfo? _inspect;             // MenuBase.InspectAction(player, item, type)
    private readonly PropertyInfo? _cfg;               // Config_Config.Config
    private readonly object? _modules;                 // ItemModuleManager.Modules
    private readonly PropertyInfo? _hideTrails;        // Item_Trail.HideTrailPlayerList

    private StoreBridge(ILogger log, Assembly asm, PropertyInfo instance, MethodInfo getItemName, MethodInfo isHidden,
        MethodInfo? playerHasAny, MethodInfo checkFlag, MethodInfo? sellingPrice, MethodInfo? inspect,
        PropertyInfo? cfg, object? modules, PropertyInfo? hideTrails)
    {
        _log = log;
        Asm = asm;
        _instance = instance;
        _getItemName = getItemName;
        _isHidden = isHidden;
        _playerHasAny = playerHasAny;
        _checkFlag = checkFlag;
        _sellingPrice = sellingPrice;
        _inspect = inspect;
        _cfg = cfg;
        _modules = modules;
        _hideTrails = hideTrails;
    }

    public static StoreBridge? Create(ILogger log, out string why)
    {
        why = "";
        var asm = AppDomain.CurrentDomain.GetAssemblies().LastOrDefault(a => a.GetName().Name == "cs2-store");
        if (asm == null)
        {
            why = "cs2-store assembly is not loaded";
            return null;
        }

        var tStore = asm.GetType("Store.Store");
        var tItem = asm.GetType("Store.Item");
        var tMenuBase = asm.GetType("Store.MenuBase");
        if (tStore == null || tItem == null || tMenuBase == null)
        {
            why = "Store.Store / Store.Item / Store.MenuBase not found (cs2-store version changed?)";
            return null;
        }

        var instance = tStore.GetProperty("Instance", S);
        var dict = typeof(Dictionary<string, string>);
        var getItemName = tItem.GetMethod("GetItemName", S, null, new[] { typeof(CCSPlayerController), dict }, null);
        var isHidden = tItem.GetMethod("IsHidden", S, null, new[] { dict }, null);
        var playerHasAny = tItem.GetMethod("PlayerHasAny", S, null, new[] { typeof(CCSPlayerController), typeof(JsonElement) }, null);
        var checkFlag = tMenuBase.GetMethod("CheckFlag", S, null, new[] { typeof(CCSPlayerController), typeof(string), typeof(bool) }, null);
        var sellingPrice = tMenuBase.GetMethod("GetSellingPrice", S, null, new[] { dict, typeof(Store_Item) }, null);
        var inspect = tMenuBase.GetMethod("InspectAction", S, null, new[] { typeof(CCSPlayerController), dict, typeof(string) }, null);
        if (instance == null || getItemName == null || isHidden == null || checkFlag == null)
        {
            why = "Store.Instance / Item.GetItemName / Item.IsHidden / MenuBase.CheckFlag not found (cs2-store version changed?)";
            return null;
        }

        var cfg = asm.GetType("Store.Config_Config")?.GetProperty("Config", S);
        var modules = asm.GetType("Store.ItemModuleManager")?.GetField("Modules", S)?.GetValue(null);
        var hideTrails = asm.GetType("Store.Item_Trail")?.GetProperty("HideTrailPlayerList", S);

        return new StoreBridge(log, asm, instance, getItemName, isHidden, playerHasAny, checkFlag, sellingPrice, inspect,
            cfg, modules, hideTrails);
    }

    // ---------------- store data ----------------

    private object? StoreObj => _instance.GetValue(null);

    private Dictionary<string, Dictionary<string, string>>? Items
        => Plugin.Get(StoreObj, "Items") as Dictionary<string, Dictionary<string, string>>;

    public JsonElement Root
    {
        get
        {
            var cfg = Plugin.Get(StoreObj, "Config");
            return Plugin.Get(cfg, "Items") is JsonElement e && e.ValueKind == JsonValueKind.Object ? e : default;
        }
    }

    private IStringLocalizer? Localizer => Plugin.Get(StoreObj, "Localizer") as IStringLocalizer;

    private object? Cfg => _cfg?.GetValue(null);

    public bool SellingEnabled => Plugin.Get(Plugin.Get(Cfg, "Menu"), "EnableSelling") as bool? ?? true;

    public string? CommandAlias(string name)
        => (Plugin.Get(Plugin.Get(Cfg, "Commands"), name) as IEnumerable<string>)?.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

    public bool HasType(string type)
        => (_modules as IDictionary)?.Contains(type) == true && (Items?.Values.Any(i => i.TryGetValue("type", out var t) && t == type) ?? false);

    public bool Equipable(string type)
        => (_modules as IDictionary)?[type] is IItemModule m && m.Equipable;

    public bool TrailsHidden(CCSPlayerController player)
        => _hideTrails?.GetValue(null) is IEnumerable set &&
           set.Cast<object>().OfType<CCSPlayerController>().Any(p => p.IsValid && p.Slot == player.Slot);

    public bool CanReadTrails => _hideTrails != null;

    public Store_Item? PlayerItem(CCSPlayerController player, string type, string uid)
        => (Plugin.Get(StoreObj, "GlobalStorePlayerItems") as IEnumerable<Store_Item>)?
            .FirstOrDefault(p => p.SteamID == player.SteamID && p.Type == type && p.UniqueId == uid);

    public int SellingPrice(Dictionary<string, string> item, Store_Item owned)
    {
        try
        {
            if (_sellingPrice?.Invoke(null, new object[] { item, owned }) is int v) return v;
        }
        catch (Exception e)
        {
            _log.LogDebug(e, "GetSellingPrice failed");
        }
        return 0;
    }

    public bool Inspecting(CCSPlayerController player)
        => Plugin.Get(StoreObj, "InspectList") is IDictionary d &&
           d.Values.Cast<object>().OfType<CCSPlayerController>().Any(p => p.IsValid && p.Slot == player.Slot);

    public bool Inspect(CCSPlayerController player, Dictionary<string, string> item)
    {
        if (_inspect == null || Inspecting(player)) return false;
        _inspect.Invoke(null, new object[] { player, item, item["type"] });
        return true;
    }

    public static bool CanInspect(string type) => type is "playerskin" or "customweapon";

    // ---------------- names ----------------

    // Category key: our translation in the player's language, then cs2-store lang/*.json, then ours in English.
    public string CategoryName(CCSPlayerController player, string key, StoreTranslations? tr)
    {
        var culture = StoreTranslations.Culture(player);
        string? ours = tr?.GetExact(culture, key);
        if (ours != null) return ours;
        try
        {
            var loc = Localizer;
            if (loc != null)
            {
                string s = loc.ForPlayer(player, key);
                if (!string.IsNullOrEmpty(s) && s != key) return s;
            }
        }
        catch
        {
            // ignore
        }
        return tr?.Get(culture, key) ?? key;
    }

    public string ItemName(CCSPlayerController player, Dictionary<string, string> item, StoreTranslations? tr)
    {
        string raw = item.TryGetValue("name", out var n) ? n : item.GetValueOrDefault("uniqueid", "");
        string name = raw;
        try { name = _getItemName.Invoke(null, new object[] { player, item }) as string ?? raw; }
        catch { }
        if (name != raw) return name;
        return tr?.GetExact(StoreTranslations.Culture(player), raw) ?? raw;
    }

    // ---------------- tree ----------------

    private bool CheckFlag(CCSPlayerController player, string? flag, bool trueIfNull)
    {
        try { return _checkFlag.Invoke(null, new object?[] { player, flag, trueIfNull }) as bool? ?? false; }
        catch { return false; }
    }

    // cs2-store: MenuBase.CheckFlag(player, item, sell)
    public bool ItemFlag(CCSPlayerController player, Dictionary<string, string> item, bool sell)
    {
        item.TryGetValue("flag", out var flag);
        return CheckFlag(player, flag, !sell);
    }

    private bool IsHidden(Dictionary<string, string> item)
    {
        try { return _isHidden.Invoke(null, new object[] { item }) as bool? ?? false; }
        catch { return false; }
    }

    private bool PlayerHasAny(CCSPlayerController player, JsonElement element)
    {
        if (_playerHasAny == null) return true;
        try { return _playerHasAny.Invoke(null, new object[] { player, element }) as bool? ?? true; }
        catch { return true; }
    }

    private static bool TeamMatches(CCSPlayerController player, JsonElement team)
    {
        string t = team.ToString();
        // "team": "CounterTerrorist" (category) or "team": "3" (item)
        if (int.TryParse(t, out int num)) return num < 1 || num > 3 || player.TeamNum == num;
        return player.Team.ToString() == t;
    }

    // Store.Menu.AddItems: which item the player sees.
    private Dictionary<string, string>? VisibleItem(CCSPlayerController player, string uid, bool inventory, StoreApi.IStoreApi api)
    {
        var items = Items;
        if (items == null || !items.TryGetValue(uid, out var item)) return null;
        if (item.TryGetValue("enable", out var enable) && enable != "true") return null;
        if (!ItemFlag(player, item, false)) return null;
        bool has = api.Item_PlayerHas(player, item["type"], item["uniqueid"], false);
        if (inventory && !has) return null;
        if (has) return item;
        return !inventory && !IsHidden(item) ? item : null;
    }

    // Store.Menu.BuildMenu: children of one category.
    public List<Node> Children(CCSPlayerController player, JsonElement element, bool inventory, StoreApi.IStoreApi api,
        StoreTranslations? tr, bool itemsOnly = false)
    {
        var list = new List<Node>();
        if (element.ValueKind != JsonValueKind.Object) return list;
        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name is "flag" or "team") continue;
            var v = prop.Value;
            if (v.ValueKind != JsonValueKind.Object) continue;
            if (v.TryGetProperty("flag", out var flag) && !CheckFlag(player, flag.ToString(), true)) continue;
            if (v.TryGetProperty("team", out var team) && !TeamMatches(player, team)) continue;

            if (v.TryGetProperty("uniqueid", out var uid))
            {
                var item = VisibleItem(player, uid.ToString(), inventory, api);
                if (item != null)
                    list.Add(new Node { Key = prop.Name, Item = item, Element = v, Name = ItemName(player, item, tr) });
                continue;
            }
            if (itemsOnly) continue;
            if (inventory && !PlayerHasAny(player, v)) continue;
            list.Add(new Node { Key = prop.Name, Element = v, Name = CategoryName(player, prop.Name, tr) });
        }
        return list;
    }

    public int CountItems(CCSPlayerController player, Node node, bool inventory, StoreApi.IStoreApi api, int depth = 0)
    {
        if (node.IsItem) return 1;
        if (depth > 8) return 0;
        int n = 0;
        foreach (var c in Children(player, node.Element, inventory, api, null, node.RootItems))
            n += c.IsItem ? 1 : CountItems(player, c, inventory, api, depth + 1);
        return n;
    }

    public Dictionary<string, string>? FirstItem(JsonElement element, int depth = 0)
    {
        if (element.ValueKind != JsonValueKind.Object || depth > 8) return null;
        var items = Items;
        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Object) continue;
            if (prop.Value.TryGetProperty("uniqueid", out var uid))
            {
                if (items != null && items.TryGetValue(uid.ToString(), out var item)) return item;
                continue;
            }
            var inner = FirstItem(prop.Value, depth + 1);
            if (inner != null) return inner;
        }
        return null;
    }

    public Dictionary<string, string>? FindItem(string uid)
        => Items is { } items && items.TryGetValue(uid, out var item) ? item : null;
}
