using System.Reflection;
using System.Runtime.Loader;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using MenuManager;
using Microsoft.Extensions.Logging;
using StoreApi;

namespace PMM_StoreCore;

public partial class Plugin : BasePlugin, IPluginConfig<StoreCoreConfig>
{
    public override string ModuleName => "PMM_StoreCore";
    public override string ModuleVersion => "0.1.0";
    public override string ModuleAuthor => "Rimmer";
    public override string ModuleDescription => "cs2-store (schwarper) on a PanoramaMenuManager panel";

    private const string HarmonyId = "rimmer.pmm.storecore";
    internal const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public StoreCoreConfig Config { get; set; } = new();

    public void OnConfigParsed(StoreCoreConfig config)
    {
        config.MenuTypes ??= new();
        config.GiftAmounts = (config.GiftAmounts ?? new()).Where(a => a > 0).Take(4).ToList();
        config.TypeIcons = new Dictionary<string, string>(config.TypeIcons ?? new(), StringComparer.OrdinalIgnoreCase);
        config.CategoryIcons = new Dictionary<string, string>(config.CategoryIcons ?? new(), StringComparer.OrdinalIgnoreCase);
        if (config.OpenDelay < 0.05f) config.OpenDelay = 0.05f;
        if (config.InputDelay < 0f) config.InputDelay = 0f;
        Config = config;
    }

    private static readonly PluginCapability<IMenuApi?> MenuCapability = new("menu:nfcore");
    internal static Plugin? Instance;

    private IMenuApi? _menu;
    private IStoreApi? _store;
    private StoreBridge? _bridge;
    private StoreTranslations? _tr;
    private readonly List<string> _patched = new();
    private bool _harmonyReady;
    private string _why = "not started";

    internal bool Active => Config.Enabled && _menu != null && _store != null && _bridge != null && _patched.Count > 0;

    public override void Load(bool hotReload)
    {
        Instance = this;
        _harmonyReady = HarmonyLoader.Ensure(ModuleDirectory, Logger);
        _tr = new StoreTranslations(ModuleDirectory, Logger);
        _tr.EnsureSection();
        PanelLoad();
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        Attach();
    }

    public override void Unload(bool hotReload)
    {
        UnregisterPainter();
        foreach (var p in Utilities.GetPlayers())
            if (p.IsValid && IsOpen(p)) Hide(p);
        if (_harmonyReady) HarmonyHost.UnpatchAll(HarmonyId);
        _patched.Clear();
        Instance = null;
    }

    private void Attach()
    {
        try { _menu = MenuCapability.Get(); } catch { _menu = null; }
        try { _store = IStoreApi.Capability.Get(); } catch { _store = null; }
        _bridge = StoreBridge.Create(Logger, out string why);

        if (_menu == null) _why = "MenuManager (menu:nfcore) not found";
        else if (_store == null) _why = "cs2-store API (cs2-store:api) not found";
        else if (_bridge == null) _why = why;
        else _why = "";

        if (_why != "")
        {
            Logger.LogWarning("Store panel is off: {Why}. cs2-store keeps its own menu", _why);
            return;
        }

        Patch();
        if (Active) RegisterPainter();
    }

    // ---------------- Harmony ----------------

    private void Patch()
    {
        if (!_harmonyReady)
        {
            _why = "0Harmony is not loaded";
            Logger.LogError("0Harmony is not loaded, store panel disabled");
            return;
        }
        HarmonyHost.UnpatchAll(HarmonyId);
        _patched.Clear();

        var asm = _bridge!.Asm;
        var p = typeof(Patches);
        const BindingFlags st = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var cmd = typeof(CommandInfo);

        // !store / !inv end in Menu.DisplayStore(player, inventory). Every step is hooked: the small ones may be inlined.
        TryPatch(asm.GetType("Store.Command")?.GetMethod("Command_Store", st, null, new[] { typeof(CCSPlayerController), cmd }, null),
            p.GetMethod(nameof(Patches.StoreCommandPrefix)));
        TryPatch(asm.GetType("Store.Command")?.GetMethod("Command_Inv", st, null, new[] { typeof(CCSPlayerController), cmd }, null),
            p.GetMethod(nameof(Patches.InventoryCommandPrefix)));
        TryPatch(asm.GetType("Store.MenuBase")?.GetMethod("DisplayStoreMenu", st, null, new[] { typeof(CCSPlayerController), typeof(bool) }, null),
            p.GetMethod(nameof(Patches.DisplayPrefix)));
        TryPatch(asm.GetType("Store.Menu")?.GetMethod("DisplayStore", st, null, new[] { typeof(CCSPlayerController), typeof(bool) }, null),
            p.GetMethod(nameof(Patches.DisplayPrefix)));

        if (_patched.Count == 0)
        {
            _why = "no cs2-store method could be patched";
            Logger.LogError("cs2-store menu entry points not found, store panel disabled");
            return;
        }
        Logger.LogInformation("Patched cs2-store {Ver}: {List}", asm.GetName().Version, string.Join(", ", _patched));
    }

    private void TryPatch(MethodInfo? m, MethodInfo? prefix)
    {
        if (m == null || prefix == null) return;
        try
        {
            HarmonyHost.Patch(HarmonyId, m, prefix, null, null);
            _patched.Add($"{m.DeclaringType?.Name}.{m.Name}");
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Harmony failed on {Type}.{Method}", m.DeclaringType?.Name, m.Name);
        }
    }

    // true = our panel was opened instead of the cs2-store menu.
    internal bool TryOpenStore(CCSPlayerController? player, bool inventory)
    {
        if (player == null || !player.IsValid || player.IsBot || !Active) return false;
        if (!WantsPanel(player)) return false;
        return OpenPanel(player, inventory ? "inv" : "shop");
    }

    private bool WantsPanel(CCSPlayerController player)
    {
        if (_menu == null) return false;
        MenuType type;
        try { type = _menu.GetSelectedMenu(player); }
        catch { return false; }
        return Config.MenuTypes.Any(t => t.Equals(type.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    // ---------------- commands ----------------

    [ConsoleCommand("css_pmm_store", "PMM_StoreCore status | reattach")]
    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    public void OnStatus(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount > 1 && info.GetArg(1) == "reattach")
        {
            UnregisterPainter();
            Attach();
        }
        info.ReplyToCommand($"[PMM_Store] active={Active} enabled={Config.Enabled} menuApi={_menu != null} storeApi={_store != null} store={_bridge?.Asm.GetName().Version?.ToString() ?? "none"}");
        if (_why != "") info.ReplyToCommand($"[PMM_Store] off: {_why}");
        info.ReplyToCommand($"[PMM_Store] patched: {(_patched.Count == 0 ? "-" : string.Join(", ", _patched))}");
        info.ReplyToCommand($"[PMM_Store] types=[{string.Join(", ", Config.MenuTypes)}] painter={_registry != null} layout={(_layout is { IsValid: true } ? "ok" : "none")} open={_slots.Count(s => s.Open)}");
        info.ReplyToCommand($"[PMM_Store] translations: {_tr?.FilePath ?? "-"} ({_tr?.Languages ?? 0} languages)");
    }

    // ---------------- reflection ----------------

    internal static object? Get(object? o, string name)
    {
        if (o == null) return null;
        var t = o.GetType();
        PropertyInfo? p = null;
        try { p = t.GetProperty(name, Any); }
        catch (AmbiguousMatchException) { p = t.GetProperties(Any).FirstOrDefault(x => x.Name == name && x.DeclaringType == t); }
        if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(o);
        return t.GetField(name, Any)?.GetValue(o);
    }
}

internal static class Patches
{
    // Command_Store(player, info) / Command_Inv(player, info). false = the panel is shown instead.
    public static bool StoreCommandPrefix(CCSPlayerController? __0) => !Safe(() => Plugin.Instance?.TryOpenStore(__0, false) ?? false);

    public static bool InventoryCommandPrefix(CCSPlayerController? __0) => !Safe(() => Plugin.Instance?.TryOpenStore(__0, true) ?? false);

    // MenuBase.DisplayStoreMenu(player, inventory) / Menu.DisplayStore(player, inventory)
    public static bool DisplayPrefix(CCSPlayerController? __0, bool __1) => !Safe(() => Plugin.Instance?.TryOpenStore(__0, __1) ?? false);

    private static bool Safe(Func<bool> f)
    {
        try { return f(); }
        catch (Exception e)
        {
            Plugin.Instance?.Logger.LogError(e, "store panel failed, cs2-store menu is shown");
            return false;
        }
    }
}

// 0Harmony must live in the default (non-collectible) load context: MonoMod builds a proxy
// in a non-collectible dynamic assembly that references 0Harmony by name, and CSS loads plugins
// into collectible contexts. So 0Harmony.dll ships in plugins/PMM_StoreCore/harmony/ and is loaded here.
// If PMM_VIPCore or PMM_GG1MapChooser already loaded it, the same copy is reused.
internal static class HarmonyLoader
{
    public static bool Ensure(string pluginDir, ILogger log)
    {
        try
        {
            var asm = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == "0Harmony");
            if (asm == null)
            {
                string path = Path.Combine(pluginDir, "harmony", "0Harmony.dll");
                if (!File.Exists(path))
                {
                    log.LogError("Not found: {Path}", path);
                    return false;
                }
                asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            }

            var own = AssemblyLoadContext.GetLoadContext(typeof(HarmonyLoader).Assembly);
            if (own != null && own != AssemblyLoadContext.Default)
            {
                var found = asm;
                own.Resolving += (_, name) => name.Name == "0Harmony" ? found : null;
            }

            log.LogInformation("0Harmony {Ver} in default context: {Loc}", asm.GetName().Version, asm.Location);
            return true;
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to load 0Harmony into the default context");
            return false;
        }
    }
}

// The only place that touches HarmonyLib types; it is JIT-compiled after HarmonyLoader.Ensure.
internal static class HarmonyHost
{
    private static readonly Dictionary<string, HarmonyLib.Harmony> Instances = new();

    private static HarmonyLib.Harmony Get(string id)
    {
        if (!Instances.TryGetValue(id, out var h)) Instances[id] = h = new HarmonyLib.Harmony(id);
        return h;
    }

    public static void Patch(string id, MethodBase original, MethodInfo? prefix, MethodInfo? postfix, MethodInfo? finalizer)
    {
        Get(id).Patch(original,
            prefix: prefix == null ? null : new HarmonyLib.HarmonyMethod(prefix),
            postfix: postfix == null ? null : new HarmonyLib.HarmonyMethod(postfix),
            finalizer: finalizer == null ? null : new HarmonyLib.HarmonyMethod(finalizer));
    }

    public static void UnpatchAll(string id)
    {
        if (Instances.TryGetValue(id, out var h)) h.UnpatchAll(id);
    }
}
