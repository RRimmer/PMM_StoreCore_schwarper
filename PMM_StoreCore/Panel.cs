using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using MenuManager;
using Microsoft.Extensions.Logging;
using static StoreApi.Store;

namespace PMM_StoreCore;

// Store panel (pmm_store.xml). Server -> client: dialog variables on #st-root + classes per player.
// Ids and class names: tools/gen_layout.py.
public partial class Plugin : IPmmMenuPainter
{
    // Pool sizes of pmm_store.xml: the lists scroll with the mouse wheel, there are no pages.
    private const int CatsPerPage = 24, CardsPerPage = 60, Cols = 3, GiftPerPage = 16, Amounts = 4;
    private const string RootId = "st-root";

    // Must match tools/gen_layout.py ICONS
    private static readonly HashSet<string> IconNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "health", "shield", "fast", "exojump", "parachute", "dollar_sign", "coin_stack", "star", "gift", "random",
        "clantag", "bullet", "bullet_burst", "kill", "refresh", "timer", "key", "teamplayer", "colorwheel", "loadout",
        "inventory", "sound_3", "crosshair", "shoppingcart", "leader", "trophy", "settings", "lock", "check", "warning",
        "clock", "armor_helmet", "armor", "kevlar", "healthshot", "defuser", "flashbang", "smokegrenade", "hegrenade",
        "molotov", "incgrenade", "decoy", "grenadepack", "taser", "knife", "c4", "assaultsuit", "customplayer",
        "ak47", "aug", "awp", "bizon", "cz75a", "deagle", "elite", "famas", "fiveseven", "g3sg1", "galilar", "glock",
        "hkp2000", "m249", "m4a1", "m4a1_silencer", "mac10", "mag7", "mp5sd", "mp7", "mp9", "negev", "nova", "p250",
        "p90", "revolver", "sawedoff", "scar20", "sg556", "ssg08", "tec9", "ump45", "usp_silencer", "xm1014",
    };

    private sealed class Slot
    {
        public bool Open;
        public int Token;
        public bool Captured;
        public int ToastToken;
        public string Tab = "shop";
        public readonly List<string> Path = new();
        public int Page, CatPage, GiftPage;
        public string? ItemId;
        public int GiftTarget = -1;      // UserId
        public int GiftAmount = -1;      // index in Config.GiftAmounts
        public List<Node> Cats = new();
        public List<Node> Cards = new();
        public List<CCSPlayerController> GiftPlayers = new();
        public readonly Dictionary<string, int> Pages = new(StringComparer.Ordinal);
        public readonly Dictionary<string, bool> Cls = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Vars = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> OneHot = new(StringComparer.Ordinal);

        public string PageKey => Tab + "\u0002" + string.Join("\u0001", Path);

        public void Forget()
        {
            Cls.Clear();
            Vars.Clear();
            OneHot.Clear();
            Captured = false;
        }

        public void Reset()
        {
            Open = false;
            Token++;
            Path.Clear();
            ItemId = null;
            Page = CatPage = GiftPage = 0;
            GiftTarget = GiftAmount = -1;
            Pages.Clear();
        }
    }

    private readonly record struct ItemState(bool Has, bool Using, bool Equipable, int Price, Store_Item? Owned);

    private readonly Slot[] _slots = Enumerable.Range(0, 65).Select(_ => new Slot()).ToArray();
    private static readonly PluginCapability<IPmmPaintRegistry?> PaintCapability = new("pmm:paint");
    private IPmmPaintRegistry? _registry;
    private CCSCustomHudLayout? _layout;
    private bool _layoutWarned;

    // ---------------- lifecycle ----------------

    private void PanelLoad()
    {
        RegisterListener<Listeners.OnCustomHudClicked>(OnClick);
        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            _layout = null;
            foreach (var s in _slots)
            {
                s.Open = false;
                s.Forget();
            }
        });
        RegisterListener<Listeners.OnClientDisconnect>(slot =>
        {
            if (slot is < 0 or >= 65) return;
            _slots[slot].Reset();
        });
        // Reconnect into the same slot keeps the previous player's per-player state: hide it again.
        RegisterListener<Listeners.OnClientPutInServer>(slot =>
        {
            if (slot is < 0 or >= 65) return;
            _slots[slot].Forget();
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p != null && p.IsValid && _layout is { IsValid: true })
            {
                Cls(p, RootId, "st-hidden", true);
                _layout.SetInputCaptureEnabled(p, false);
            }
        });
        // A round restart may remove the entity: forget open panels so MenuManager does not think they are shown.
        AddTimer(0.5f, () =>
        {
            if (_layout is { IsValid: true }) return;
            if (_layout != null) _layout = null;
            foreach (var s in _slots)
                if (s.Open)
                {
                    s.Open = false;
                    s.Forget();
                }
        }, TimerFlags.REPEAT);
    }

    private void RegisterPainter()
    {
        if (_registry != null) return;
        try { _registry = PaintCapability.Get(); } catch { _registry = null; }
        if (_registry == null)
        {
            Logger.LogWarning("MenuManager has no pmm:paint: the store panel works, but another PMM menu does not close it");
            return;
        }
        _registry.Add(this);
        Logger.LogInformation("Store panel ready ({Layout})", Config.LayoutPath);
    }

    private void UnregisterPainter()
    {
        try { _registry?.Remove(this); } catch { }
        _registry = null;
    }

    // ---------------- IPmmMenuPainter ----------------

    // We draw no PMM menus: another panorama menu that opens closes the store panel.
    public bool TryPaint(CCSPlayerController player, IMenu menu)
    {
        if (player.IsValid && player.Slot is >= 0 and < 65 && _slots[player.Slot].Open) Hide(player);
        return false;
    }

    public void Close(CCSPlayerController player)
    {
        if (player.IsValid && player.Slot is >= 0 and < 65 && _slots[player.Slot].Open) Hide(player);
    }

    public bool IsOpen(CCSPlayerController player)
        => player.IsValid && player.Slot is >= 0 and < 65 && _slots[player.Slot].Open;

    // ---------------- open / hide ----------------

    private bool OpenPanel(CCSPlayerController player, string tab)
    {
        if (player.Slot is < 0 or >= 65) return false;
        if (!EnsureLayout())
        {
            if (!_layoutWarned) Logger.LogWarning("custom_hud_layout {Path} was not created, cs2-store keeps its own menu", Config.LayoutPath);
            _layoutWarned = true;
            return false;
        }

        var s = _slots[player.Slot];
        bool wasOpen = s.Open;
        if (!wasOpen)
        {
            // Close whatever PMM menu is open (that also calls our Close, so before Open = true).
            try { _menu?.CloseMenu(player); } catch { }
        }

        s.Tab = tab;
        s.Path.Clear();
        s.ItemId = null;
        s.Page = 0;
        s.CatPage = 0;
        s.Open = true;
        int token = ++s.Token;

        // Never draw in the frame of the chat command (!store): CS2 then kicks with NETWORK_DISCONNECT.
        Later(wasOpen ? 0f : Config.OpenDelay, () =>
        {
            if (s.Token != token || !s.Open || !player.IsValid) return;
            Draw(player);
            if (s.Captured) return;
            Later(Config.InputDelay, () =>
            {
                if (s.Token == token && s.Open && player.IsValid) Capture(player, true);
            });
        });
        return true;
    }

    private void Hide(CCSPlayerController player)
    {
        if (player.Slot is < 0 or >= 65) return;
        var s = _slots[player.Slot];
        s.Open = false;
        s.Token++;
        if (!player.IsValid || _layout is not { IsValid: true }) return;
        Capture(player, false);
        Cls(player, RootId, "toast-on", false);
        Cls(player, RootId, "st-hidden", true);
    }

    private bool EnsureLayout()
    {
        if (_layout is { IsValid: true }) return true;
        _layout = null;
        var created = Utilities.CreateEntityByName<CCSCustomHudLayout>("custom_hud_layout");
        if (created == null || created.Handle == IntPtr.Zero) return false;
        created.StrLayout = Config.LayoutPath;
        created.DispatchSpawn();
        if (!created.IsValid) return false;
        _layout = created;
        _layoutWarned = false;
        foreach (var s in _slots) s.Forget();
        return true;
    }

    private void Capture(CCSPlayerController player, bool on)
    {
        var s = _slots[player.Slot];
        if (_layout is not { IsValid: true } || !player.IsValid) return;
        if (s.Captured == on) return;
        s.Captured = on;
        _layout.SetInputCaptureEnabled(player, on);
    }

    // ---------------- clicks ----------------

    private void OnClick(CCSPlayerController player, CCSCustomHudLayout layout, string buttonId)
    {
        if (_layout == null || layout.Handle != _layout.Handle || !player.IsValid || player.Slot is < 0 or >= 65) return;
        var s = _slots[player.Slot];
        if (!s.Open || !Active) return;

        try
        {
            switch (buttonId)
            {
                case "st-close":
                    Hide(player);
                    return;
                case "st-tab-shop":
                case "st-tab-inv":
                case "st-tab-set":
                    s.Pages[s.PageKey] = s.Page;
                    s.Tab = buttonId[7..];
                    s.Path.Clear();
                    s.ItemId = null;
                    s.Page = 0;
                    s.CatPage = 0;
                    break;
                case "st-back":
                    if (s.ItemId != null) s.ItemId = null;
                    else if (s.Path.Count > 1)
                    {
                        s.Path.RemoveAt(s.Path.Count - 1);
                        s.Page = s.Pages.GetValueOrDefault(s.PageKey);
                    }
                    break;
                case "st-prev":
                case "st-next":
                    s.Page += buttonId == "st-next" ? 1 : -1;   // clamped in Draw
                    s.Page = Math.Max(0, s.Page);
                    s.Pages[s.PageKey] = s.Page;
                    break;
                case "st-cprev":
                case "st-cnext":
                    s.CatPage = Math.Max(0, s.CatPage + (buttonId == "st-cnext" ? 1 : -1));
                    break;
                case "st-gprev":
                case "st-gnext":
                    s.GiftPage = Math.Max(0, s.GiftPage + (buttonId == "st-gnext" ? 1 : -1));
                    break;
                case "st-it-main":
                    MainAction(player, s);
                    break;
                case "st-it-sell":
                    SellAction(player, s);
                    break;
                case "st-it-insp":
                    InspectAction(player, s);
                    break;
                case "st-s-trails":
                    ToggleTrails(player);
                    return;
                case "st-gsend":
                    SendGift(player, s);
                    return;
                default:
                    if (TryIndex(buttonId, "st-c-", out int c)) ClickCategory(s, s.CatPage * CatsPerPage + c);
                    else if (TryIndex(buttonId, "st-i-", out int i)) ClickCard(s, s.Page * CardsPerPage + i);
                    else if (TryIndex(buttonId, "st-g-", out int g))
                    {
                        int idx = s.GiftPage * GiftPerPage + g;
                        if (idx < s.GiftPlayers.Count && s.GiftPlayers[idx].IsValid) s.GiftTarget = s.GiftPlayers[idx].UserId ?? -1;
                    }
                    else if (TryIndex(buttonId, "st-a-", out int a))
                    {
                        if (a < Config.GiftAmounts.Count) s.GiftAmount = a;
                    }
                    else return;
                    break;
            }
            if (s.Open) Draw(player);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Store panel click {Button} failed", buttonId);
        }
    }

    private static void ClickCategory(Slot s, int index)
    {
        if (index < 0 || index >= s.Cats.Count) return;
        s.Pages[s.PageKey] = s.Page;
        s.Path.Clear();
        s.Path.Add(s.Cats[index].Key);
        s.ItemId = null;
        s.Page = s.Pages.GetValueOrDefault(s.PageKey);
    }

    private static void ClickCard(Slot s, int index)
    {
        if (index < 0 || index >= s.Cards.Count) return;
        var node = s.Cards[index];
        if (node.IsItem)
        {
            s.ItemId = node.Uid;
            return;
        }
        s.Pages[s.PageKey] = s.Page;
        s.Path.Add(node.Key);
        s.Page = s.Pages.GetValueOrDefault(s.PageKey);
    }

    // ---------------- actions ----------------

    private int Credits(CCSPlayerController player)
    {
        try { return _store?.GetPlayerCredits(player) ?? 0; }
        catch { return 0; }
    }

    private ItemState State(CCSPlayerController player, Dictionary<string, string> item)
    {
        string type = item["type"], uid = item["uniqueid"];
        bool has = _store!.Item_PlayerHas(player, type, uid, false);
        bool use = has && _store.Item_PlayerUsing(player, type, uid);
        int price = int.TryParse(item.GetValueOrDefault("price", "0"), out int p) ? p : 0;
        return new ItemState(has, use, _bridge!.Equipable(type), price, _bridge.PlayerItem(player, type, uid));
    }

    private bool SellAllowed(CCSPlayerController player, Dictionary<string, string> item, ItemState st, out int price)
    {
        price = 0;
        if (st.Owned == null || !_bridge!.SellingEnabled) return false;
        if (_store!.IsPlayerVip(player) || _bridge.ItemFlag(player, item, true)) return false;
        price = _bridge.SellingPrice(item, st.Owned);
        return price > 1;
    }

    private void MainAction(CCSPlayerController player, Slot s)
    {
        var item = s.ItemId == null ? null : _bridge!.FindItem(s.ItemId);
        if (item == null) return;
        var st = State(player, item);
        string name = _bridge!.ItemName(player, item, _tr);

        if (!st.Has)
        {
            int before = Credits(player);
            if (st.Price > before)
            {
                Toast(player, Fmt(T(player, "need"), st.Price - before), true);
                return;
            }
            bool ok = _store!.Item_Purchase(player, item);
            int after = Credits(player);
            // Item.Purchase returns false for single-use items (health, respawn ...) even when it worked.
            bool done = ok || after < before || (!st.Equipable && st.Price <= 0);
            if (!done)
            {
                Toast(player, Fmt(T(player, "t_fail"), name), true);
                return;
            }
            if (st.Equipable)
            {
                if (Config.EquipAfterPurchase && !_store.Item_PlayerUsing(player, item["type"], item["uniqueid"]))
                    _store.Item_Equip(player, item);
                Toast(player, Fmt(T(player, "t_bought"), name), false);
            }
            else Toast(player, Fmt(T(player, "t_used"), name), false);
            return;
        }

        if (!st.Equipable) return;
        if (st.Using)
        {
            bool ok = _store!.Item_Unequip(player, item, true);
            Toast(player, Fmt(T(player, ok ? "t_unequipped" : "t_fail"), name), !ok);
        }
        else
        {
            bool ok = _store!.Item_Equip(player, item);
            Toast(player, Fmt(T(player, ok ? "t_equipped" : "t_fail"), name), !ok);
        }
    }

    private void SellAction(CCSPlayerController player, Slot s)
    {
        var item = s.ItemId == null ? null : _bridge!.FindItem(s.ItemId);
        if (item == null) return;
        var st = State(player, item);
        if (!SellAllowed(player, item, st, out int price)) return;
        string name = _bridge!.ItemName(player, item, _tr);
        bool ok = _store!.Item_Sell(player, item);
        Toast(player, ok ? Fmt(T(player, "t_sold"), name, price) : Fmt(T(player, "t_fail"), name), !ok);
    }

    private void InspectAction(CCSPlayerController player, Slot s)
    {
        var item = s.ItemId == null ? null : _bridge!.FindItem(s.ItemId);
        if (item == null || !StoreBridge.CanInspect(item["type"])) return;
        if (_bridge!.Inspect(player, item))
            Toast(player, Fmt(T(player, "t_inspect"), _bridge.ItemName(player, item, _tr)), false);
    }

    private void ToggleTrails(CCSPlayerController player)
    {
        string? alias = _bridge!.CommandAlias("HideTrails");
        if (alias == null) return;
        player.ExecuteClientCommandFromServer(alias);
        var s = _slots[player.Slot];
        int token = s.Token;
        Later(0.1f, () =>
        {
            if (!player.IsValid || !s.Open || s.Token != token) return;
            Toast(player, T(player, _bridge.TrailsHidden(player) ? "t_trails_on" : "t_trails_off"), false);
            Draw(player);
        });
    }

    private void SendGift(CCSPlayerController player, Slot s)
    {
        string? alias = _bridge!.CommandAlias("Gift");
        var target = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.UserId == s.GiftTarget);
        if (alias == null || target == null || s.GiftAmount < 0 || s.GiftAmount >= Config.GiftAmounts.Count)
        {
            Toast(player, T(player, "gift_pick"), true);
            return;
        }
        int amount = Config.GiftAmounts[s.GiftAmount];
        int before = Credits(player);
        if (before < amount)
        {
            Toast(player, Fmt(T(player, "need"), amount - before), true);
            return;
        }
        string targetName = target.PlayerName ?? "";
        player.ExecuteClientCommandFromServer($"css_{alias} #{target.UserId} {amount}");
        int token = s.Token;
        Later(0.2f, () =>
        {
            if (!player.IsValid || !s.Open || s.Token != token) return;
            bool ok = Credits(player) < before;
            Toast(player, ok ? Fmt(T(player, "t_gift"), amount, targetName) : T(player, "t_gift_fail"), !ok);
            Draw(player);
        });
    }

    // ---------------- drawing ----------------

    private void Draw(CCSPlayerController player)
    {
        var s = _slots[player.Slot];
        if (!s.Open || _layout is not { IsValid: true }) return;
        if (_bridge == null || _store == null)
        {
            Hide(player);
            return;
        }

        try
        {
            Var(player, RootId, "st_title", T(player, "title"));
            Var(player, RootId, "st_nick", player.PlayerName ?? "");
            Var(player, RootId, "st_credits", Fmt(T(player, "credits"), Credits(player)));
            Var(player, RootId, "st_tab_shop", T(player, "tab_shop"));
            Var(player, RootId, "st_tab_inv", T(player, "tab_inv"));
            Var(player, RootId, "st_tab_set", T(player, "tab_set"));
            Var(player, RootId, "st_back", T(player, "back"));
            OneHot(player, RootId, "tab", "tab-" + s.Tab);
            string pos = Config.Position.ToLowerInvariant() switch { "left" => "pos-left", "right" => "pos-right", _ => "" };
            OneHot(player, RootId, "pos", pos);

            if (s.Tab == "set") DrawSettings(player, s);
            else DrawStore(player, s, s.Tab == "inv");

            Cls(player, RootId, "st-hidden", false);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Store panel draw failed");
            Hide(player);
        }
    }

    private void DrawStore(CCSPlayerController player, Slot s, bool inventory)
    {
        var bridge = _bridge!;
        var store = _store!;
        var root = bridge.Root;
        var top = bridge.Children(player, root, inventory, store, _tr);
        var cats = top.Where(n => !n.IsItem).ToList();
        if (top.Any(n => n.IsItem))
            cats.Add(new Node { Key = StoreBridge.RootKey, Name = T(player, "other"), Element = root, RootItems = true });
        s.Cats = cats;

        if (s.Path.Count == 0 || !cats.Any(c => c.Key == s.Path[0]))
        {
            s.Path.Clear();
            s.ItemId = null;
            if (cats.Count > 0) s.Path.Add(cats[0].Key);
            s.Page = s.Pages.GetValueOrDefault(s.PageKey);
        }

        // sidebar
        int cpages = Pages(cats.Count, CatsPerPage);
        s.CatPage = Math.Clamp(s.CatPage, 0, cpages - 1);
        for (int i = 0; i < CatsPerPage; i++)
        {
            string id = "st-c-" + i;
            int idx = s.CatPage * CatsPerPage + i;
            if (idx >= cats.Count)
            {
                Cls(player, id, "slot-off", true);
                continue;
            }
            var cat = cats[idx];
            Cls(player, id, "slot-off", false);
            Cls(player, id, "a", s.Path.Count > 0 && cat.Key == s.Path[0]);
            OneHot(player, "st-c-ic-" + i, "icon", "ic-" + CategoryIcon(cat));
            Var(player, RootId, "scn" + i, cat.Name);
            Var(player, RootId, "scc" + i, bridge.CountItems(player, cat, inventory, store).ToString(CultureInfo.InvariantCulture));
        }
        Cls(player, RootId, "has-cpages", cpages > 1);
        Var(player, RootId, "st_cpage", Fmt(T(player, "page"), s.CatPage + 1, cpages));

        // current category along the path
        var names = new List<string>();
        Node? cur = null;
        if (s.Path.Count > 0)
        {
            cur = cats.First(c => c.Key == s.Path[0]);
            names.Add(cur.Name);
            for (int k = 1; k < s.Path.Count; k++)
            {
                var next = bridge.Children(player, cur.Element, inventory, store, _tr, cur.RootItems)
                    .FirstOrDefault(n => !n.IsItem && n.Key == s.Path[k]);
                if (next == null)
                {
                    s.Path.RemoveRange(k, s.Path.Count - k);
                    s.ItemId = null;
                    break;
                }
                cur = next;
                names.Add(cur.Name);
            }
        }

        var children = cur == null ? new List<Node>() : bridge.Children(player, cur.Element, inventory, store, _tr, cur.RootItems);
        s.Cards = children;

        Dictionary<string, string>? item = null;
        if (s.ItemId != null)
        {
            item = children.FirstOrDefault(n => n.IsItem && n.Uid == s.ItemId)?.Item;
            if (item == null) s.ItemId = null;   // sold from the inventory, flag removed ...
        }

        Cls(player, RootId, "has-back", s.Path.Count > 1 || item != null);
        Var(player, RootId, "st_crumb", string.Join(" / ", names));

        if (item != null)
        {
            OneHot(player, RootId, "view", "view-item");
            Cls(player, RootId, "has-pages", false);
            Cls(player, RootId, "is-empty", false);
            Var(player, RootId, "st_count", "");
            DrawItem(player, item, names);
            return;
        }

        OneHot(player, RootId, "view", "view-grid");
        Var(player, RootId, "st_count", Fmt(T(player, "count"), children.Count(n => n.IsItem) +
                                              children.Where(n => !n.IsItem).Sum(n => bridge.CountItems(player, n, inventory, store))));
        Cls(player, RootId, "is-empty", children.Count == 0);
        Var(player, RootId, "st_empty", T(player, inventory ? "empty_inv" : "empty_shop"));

        int pages = Pages(children.Count, CardsPerPage);
        s.Page = Math.Clamp(s.Page, 0, pages - 1);
        int first = s.Page * CardsPerPage;
        int onPage = Math.Max(0, Math.Min(CardsPerPage, children.Count - first));
        for (int r = 0; r < CardsPerPage / Cols; r++)
            Cls(player, "st-gr-" + r, "slot-off", r * Cols >= onPage);

        int credits = Credits(player);
        for (int i = 0; i < CardsPerPage; i++)
        {
            string id = "st-i-" + i;
            if (i >= onPage)
            {
                Cls(player, id, "slot-off", true);
                continue;
            }
            var node = children[first + i];
            Cls(player, id, "slot-off", false);
            Var(player, RootId, "sin" + i, node.Name);
            if (!node.IsItem)
            {
                int n = bridge.CountItems(player, node, inventory, store);
                OneHot(player, "st-i-ic-" + i, "icon", "ic-" + CategoryIcon(node));
                OneHot(player, id, "pill", "p-folder");
                Var(player, RootId, "sis" + i, "");
                Var(player, RootId, "sip" + i, Fmt(T(player, "folder"), n));
                Var(player, RootId, "sih" + i, T(player, "hint_open"));
                continue;
            }

            var st = State(player, node.Item!);
            OneHot(player, "st-i-ic-" + i, "icon", "ic-" + ItemIcon(node.Item!));
            Var(player, RootId, "sis" + i, TypeName(player, node.Type));
            var (pill, text, hint) = Pill(player, st, credits);
            OneHot(player, id, "pill", pill);
            Var(player, RootId, "sip" + i, text);
            Var(player, RootId, "sih" + i, hint);
        }

        Cls(player, RootId, "has-pages", pages > 1);
        Var(player, RootId, "st_page", Fmt(T(player, "page"), s.Page + 1, pages));
    }

    private (string pill, string text, string hint) Pill(CCSPlayerController player, ItemState st, int credits)
    {
        if (st.Using) return ("p-equipped", T(player, "state_equipped"), T(player, "hint_open"));
        if (st.Has) return ("p-owned", T(player, "state_owned"), st.Equipable ? T(player, "hint_equip") : T(player, "hint_open"));
        if (st.Price <= 0) return ("p-free", T(player, "state_free"), T(player, "hint_buy"));
        string price = Fmt(T(player, "price"), st.Price);
        return credits >= st.Price
            ? ("p-price", price, T(player, "hint_buy"))
            : ("p-poor", price, Fmt(T(player, "hint_need"), st.Price - credits));
    }

    private void DrawItem(CCSPlayerController player, Dictionary<string, string> item, List<string> path)
    {
        var bridge = _bridge!;
        var st = State(player, item);
        int credits = Credits(player);
        string type = item["type"];

        Var(player, RootId, "st_it_name", bridge.ItemName(player, item, _tr));
        Var(player, RootId, "st_it_sub", TypeName(player, type) + (path.Count > 0 ? " · " + string.Join(" / ", path) : ""));
        OneHot(player, "st-it-ic", "icon", "ic-" + ItemIcon(item));
        var (pill, text, _) = Pill(player, st, credits);
        OneHot(player, "st-it-pill", "pill", pill);
        Var(player, RootId, "st_it_state", text);

        bool sell = SellAllowed(player, item, st, out int sellPrice);
        Var(player, RootId, "st_it_l0", T(player, "l_price"));
        Var(player, RootId, "st_it_v0", st.Price <= 0 ? T(player, "state_free") : Fmt(T(player, "price"), st.Price));
        if (sell)
        {
            Var(player, RootId, "st_it_l1", T(player, "l_sell"));
            Var(player, RootId, "st_it_v1", Fmt(T(player, "price"), sellPrice));
        }
        else
        {
            Var(player, RootId, "st_it_l1", T(player, "l_type"));
            Var(player, RootId, "st_it_v1", TypeName(player, type));
        }
        Var(player, RootId, "st_it_l2", T(player, "l_time"));
        Var(player, RootId, "st_it_v2", Duration(player, item, st));

        string main;
        bool dis = false, off = false;
        if (!st.Has)
        {
            main = st.Price <= 0 ? T(player, "buy_free") : Fmt(T(player, "buy"), st.Price);
            dis = st.Price > credits;
        }
        else if (st.Equipable) main = T(player, st.Using ? "unequip" : "equip");
        else
        {
            main = "";
            off = true;
        }
        Var(player, RootId, "st_it_main", main);
        Cls(player, RootId, "main-dis", dis);
        Cls(player, RootId, "main-off", off);
        Var(player, RootId, "st_it_note", !st.Has && dis ? Fmt(T(player, "need"), st.Price - credits) : "");

        Cls(player, RootId, "has-sell", sell);
        Var(player, RootId, "st_it_sell", sell ? Fmt(T(player, "sell"), sellPrice) : "");
        bool insp = StoreBridge.CanInspect(type);
        Cls(player, RootId, "has-insp", insp);
        Var(player, RootId, "st_it_insp", T(player, "inspect"));
    }

    private string Duration(CCSPlayerController player, Dictionary<string, string> item, ItemState st)
    {
        if (st.Owned != null && st.Owned.DateOfExpiration > DateTime.MinValue)
        {
            string date;
            try { date = st.Owned.DateOfExpiration.ToString(Config.DateFormat); }
            catch (FormatException) { date = st.Owned.DateOfExpiration.ToString("dd.MM.yyyy"); }
            return Fmt(T(player, "until"), date);
        }
        if (!st.Equipable) return T(player, "once");
        if (item.TryGetValue("expiration", out var exp) && long.TryParse(exp, out long sec) && sec > 0)
        {
            if (sec >= 86400) return Fmt(T(player, "duration_d"), Math.Round(sec / 86400.0, 1));
            if (sec >= 3600) return Fmt(T(player, "duration_h"), Math.Round(sec / 3600.0, 1));
            return Fmt(T(player, "duration_m"), Math.Max(1, sec / 60));
        }
        return T(player, "forever");
    }

    private void DrawSettings(CCSPlayerController player, Slot s)
    {
        var bridge = _bridge!;
        OneHot(player, RootId, "view", "view-grid");
        Cls(player, RootId, "has-pages", false);
        Cls(player, RootId, "has-back", false);

        Var(player, RootId, "st_set_player", T(player, "set_player"));
        Var(player, RootId, "st_set_none", T(player, "set_none"));
        Var(player, RootId, "st_set_foot", T(player, "set_foot"));

        string? trailsAlias = bridge.CommandAlias("HideTrails");
        bool trails = Config.ShowHideTrails && trailsAlias != null && bridge.CanReadTrails && bridge.HasType("trail");
        Cls(player, RootId, "has-trails", trails);
        Cls(player, RootId, "no-trails", !trails);
        if (trails)
        {
            Var(player, RootId, "st_trails_name", T(player, "trails_name"));
            Var(player, RootId, "st_trails_desc", T(player, "trails_desc"));
            Var(player, RootId, "st_trails_alias", Fmt(T(player, "alias_console"), trailsAlias!));
            Cls(player, "st-s-trails", "on", bridge.TrailsHidden(player));
        }

        string? giftAlias = bridge.CommandAlias("Gift");
        bool gift = Config.ShowGift && giftAlias != null && Config.GiftAmounts.Count > 0;
        Cls(player, RootId, "no-gift", !gift);
        if (!gift) return;

        Var(player, RootId, "st_gift_name", T(player, "gift_name"));
        Var(player, RootId, "st_gift_alias", Fmt(T(player, "alias_chat"), "!" + giftAlias));
        Var(player, RootId, "st_gift_who", T(player, "gift_who"));
        Var(player, RootId, "st_gift_how", T(player, "gift_how"));
        Var(player, RootId, "st_gift_empty", T(player, "gift_empty"));

        var others = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && !p.IsHLTV && p.Slot != player.Slot &&
                        p.Connected == PlayerConnectedState.Connected && p.UserId != null)
            .OrderBy(p => p.PlayerName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        s.GiftPlayers = others;
        if (others.All(p => p.UserId != s.GiftTarget)) s.GiftTarget = -1;

        int pages = Pages(others.Count, GiftPerPage);
        s.GiftPage = Math.Clamp(s.GiftPage, 0, pages - 1);
        int first = s.GiftPage * GiftPerPage;
        int onPage = Math.Max(0, Math.Min(GiftPerPage, others.Count - first));
        for (int r = 0; r < GiftPerPage / 2; r++)
            Cls(player, "st-ggr-" + r, "slot-off", r * 2 >= onPage);
        for (int i = 0; i < GiftPerPage; i++)
        {
            string id = "st-g-" + i;
            if (i >= onPage)
            {
                Cls(player, id, "slot-off", true);
                continue;
            }
            var p = others[first + i];
            Cls(player, id, "slot-off", false);
            Cls(player, id, "sel", p.UserId == s.GiftTarget);
            Var(player, RootId, "sgn" + i, p.PlayerName ?? "");
        }
        Cls(player, RootId, "gift-none", others.Count == 0);
        Cls(player, RootId, "has-gpages", pages > 1);
        Var(player, RootId, "st_gpage", Fmt(T(player, "page"), s.GiftPage + 1, pages));

        for (int i = 0; i < Amounts; i++)
        {
            string id = "st-a-" + i;
            bool on = i < Config.GiftAmounts.Count;
            Cls(player, id, "slot-off", !on);
            if (!on) continue;
            Cls(player, id, "sel", i == s.GiftAmount);
            Var(player, RootId, "sga" + i, Config.GiftAmounts[i].ToString(CultureInfo.InvariantCulture));
        }

        var target = others.FirstOrDefault(p => p.UserId == s.GiftTarget);
        bool ready = target != null && s.GiftAmount >= 0 && s.GiftAmount < Config.GiftAmounts.Count;
        int amount = ready ? Config.GiftAmounts[s.GiftAmount] : 0;
        Var(player, RootId, "st_gsend", ready ? Fmt(T(player, "gift_send"), amount, target!.PlayerName ?? "") : T(player, "gift_pick"));
        Cls(player, RootId, "send-dis", !ready || Credits(player) < amount);
    }

    // ---------------- icons / names ----------------

    private string ItemIcon(Dictionary<string, string> item)
    {
        string type = item.GetValueOrDefault("type", "");
        if (type is "weapon" or "customweapon" && item.TryGetValue("weapon", out var w))
        {
            string name = w.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase) ? w[7..] : w;
            if (IconNames.Contains(name)) return name.ToLowerInvariant();
        }
        if (Config.TypeIcons.TryGetValue(type, out var icon) && IconNames.Contains(icon)) return icon.ToLowerInvariant();
        return DefaultIcon;
    }

    private string CategoryIcon(Node cat)
    {
        if (!cat.RootItems && Config.CategoryIcons.TryGetValue(cat.Key, out var icon) && IconNames.Contains(icon))
            return icon.ToLowerInvariant();
        var first = _bridge?.FirstItem(cat.Element);
        return first != null ? ItemIcon(first) : DefaultIcon;
    }

    private string DefaultIcon => IconNames.Contains(Config.DefaultIcon) ? Config.DefaultIcon.ToLowerInvariant() : "star";

    private string TypeName(CCSPlayerController player, string type)
    {
        string key = "pmm_store.type." + type;
        var c = StoreTranslations.Culture(player);
        return _tr?.Get(c, key) ?? (StoreDefaults.Texts["en"].ContainsKey(key) ? StoreDefaults.Text(c, key) : type);
    }

    // Panel text: translation file (pmm_store.<key>, player's language) -> built-in defaults.
    private string T(CCSPlayerController player, string key)
    {
        var c = StoreTranslations.Culture(player);
        return _tr?.Get(c, "pmm_store." + key) ?? StoreDefaults.Text(c, "pmm_store." + key);
    }

    // ---------------- toast ----------------

    private void Toast(CCSPlayerController player, string text, bool warn)
    {
        if (!player.IsValid || player.Slot is < 0 or >= 65) return;
        var s = _slots[player.Slot];
        if (!s.Open || _layout is not { IsValid: true }) return;
        int token = ++s.ToastToken;
        Var(player, RootId, "st_toast", text);
        Cls(player, RootId, "toast-warn", warn);
        Cls(player, RootId, "toast-on", true);
        Later(Math.Max(0.5f, Config.ToastSeconds), () =>
        {
            if (s.ToastToken == token && player.IsValid && _layout is { IsValid: true })
                Cls(player, RootId, "toast-on", false);
        });
    }

    // ---------------- sends (cached per player) ----------------

    private void Cls(CCSPlayerController player, string panel, string cls, bool on)
    {
        var layout = _layout;
        if (layout is not { IsValid: true } || !player.IsValid) return;
        var s = _slots[player.Slot];
        string key = panel + "\u0001" + cls;
        if (s.Cls.TryGetValue(key, out bool cur) && cur == on) return;
        s.Cls[key] = on;
        layout.SetHasClassForPlayer(player, panel, cls, on);
    }

    private void Var(CCSPlayerController player, string panel, string name, string value)
    {
        var layout = _layout;
        if (layout is not { IsValid: true } || !player.IsValid) return;
        var s = _slots[player.Slot];
        string key = panel + "\u0001" + name;
        if (s.Vars.TryGetValue(key, out var cur) && cur == value) return;
        s.Vars[key] = value;
        layout.SetDialogVariableStringForPlayer(player, panel, name, value);
    }

    // One class out of a group (tab-*, view-*, p-*, ic-*): the previous one is removed.
    private void OneHot(CCSPlayerController player, string panel, string group, string cls)
    {
        var s = _slots[player.Slot];
        string key = panel + "\u0001" + group;
        if (!s.OneHot.TryGetValue(key, out var prev))
        {
            // First send for this panel: clear the whole group (a reconnect into the slot keeps old classes).
            prev = Authored(panel, group);
            if (GroupClasses(group) is { } all)
                foreach (var other in all)
                    if (other != cls) Cls(player, panel, other, false);
        }
        else if (prev == cls) return;
        if (!string.IsNullOrEmpty(prev) && prev != cls) Cls(player, panel, prev, false);
        if (!string.IsNullOrEmpty(cls)) Cls(player, panel, cls, true);
        s.OneHot[key] = cls;
    }

    private static string[]? GroupClasses(string group) => group switch
    {
        "tab" => new[] { "tab-shop", "tab-inv", "tab-set" },
        "view" => new[] { "view-grid", "view-item" },
        "pill" => new[] { "p-equipped", "p-owned", "p-price", "p-poor", "p-free", "p-folder" },
        "pos" => new[] { "pos-left", "pos-right" },
        _ => null,
    };

    private static string Authored(string panel, string group) => group switch
    {
        "tab" => "tab-shop",
        "view" => "view-grid",
        "pill" => "p-price",
        "icon" => "ic-star",
        _ => "",
    };

    // ---------------- helpers ----------------

    private static int Pages(int count, int per) => Math.Max(1, (count + per - 1) / per);

    private static bool TryIndex(string id, string prefix, out int index)
    {
        index = -1;
        return id.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(id.AsSpan(prefix.Length), out index) && index >= 0;
    }

    private static string Fmt(string format, params object[] args)
    {
        try { return string.Format(format, args); }
        catch (FormatException) { return format; }
    }

    private void Later(float delay, Action action)
    {
        if (delay <= 0f) Server.NextFrame(action);
        else AddTimer(delay, action);
    }
}
