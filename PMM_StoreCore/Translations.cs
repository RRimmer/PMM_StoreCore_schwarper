using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using Microsoft.Extensions.Logging;

namespace PMM_StoreCore;

// Texts from MenuManager's file configs/plugins/MenuManagerCore/MenuManager_Modules_Translation.json
// (plugin -> language -> key -> text). The plugin adds its own "PMM_StoreCore" section once and later only
// the keys it is missing; texts the admin changed stay. Keys:
//   pmm_store.*            panel texts
//   pmm_store.type.<type>  item type names (playerskin, trail ...)
//   <category or item key> names from cs2-store.json ("Features", "Buy Weapon", "5 Sec Godmode" ...)
internal sealed class StoreTranslations
{
    public const string Section = "PMM_StoreCore";
    private const string FileName = "MenuManager_Modules_Translation.json";

    private readonly ILogger _log;
    private readonly string _path;
    private DateTime _writtenAt = DateTime.MinValue;
    private Dictionary<string, Dictionary<string, string>> _byLanguage = new(StringComparer.OrdinalIgnoreCase);

    public StoreTranslations(string moduleDirectory, ILogger log)
    {
        _log = log;
        _path = Path.GetFullPath(Path.Combine(moduleDirectory, "..", "..", "configs", "plugins", "MenuManagerCore", FileName));
    }

    public string FilePath => _path;

    public int Languages
    {
        get
        {
            ReloadIfChanged();
            return _byLanguage.Count;
        }
    }

    public void EnsureSection()
    {
        try
        {
            JsonObject root;
            if (File.Exists(_path))
            {
                var parsed = JsonNode.Parse(File.ReadAllText(_path), documentOptions: new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });
                if (parsed is not JsonObject obj)
                {
                    _log.LogWarning("Translations: {Path} is not a JSON object, section not added", _path);
                    return;
                }
                root = obj;
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                root = new JsonObject();
            }

            bool created = root[Section] is not JsonObject;
            if (created) root[Section] = new JsonObject();
            var section = (JsonObject)root[Section]!;
            int added = 0;
            foreach (var (lang, map) in StoreDefaults.Texts)
            {
                if (section[lang] is not JsonObject o)
                {
                    o = new JsonObject();
                    section[lang] = o;
                }
                foreach (var (k, v) in map)
                {
                    if (o.ContainsKey(k)) continue;
                    o[k] = v;
                    added++;
                }
            }
            if (added == 0) return;

            File.WriteAllText(_path, root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
            _log.LogInformation("Translations: {Count} keys {What} section {Section} of {Path}",
                added, created ? "written to the new" : "added to the", Section, _path);
        }
        catch (Exception e)
        {
            _log.LogWarning(e, "Translations: could not add the {Section} section to {Path}", Section, _path);
        }
    }

    private void ReloadIfChanged()
    {
        if (!File.Exists(_path)) return;
        DateTime written = File.GetLastWriteTimeUtc(_path);
        if (written == _writtenAt) return;
        _writtenAt = written;

        var next = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_path), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;

            // Only our own section: category names like "Speed" must not be taken from another plugin.
            foreach (var module in doc.RootElement.EnumerateObject())
            {
                if (!module.Name.Equals(Section, StringComparison.OrdinalIgnoreCase)) continue;
                if (module.Value.ValueKind != JsonValueKind.Object) continue;
                foreach (var language in module.Value.EnumerateObject())
                {
                    if (language.Value.ValueKind != JsonValueKind.Object) continue;
                    if (!next.TryGetValue(language.Name, out var map))
                        next[language.Name] = map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var phrase in language.Value.EnumerateObject())
                    {
                        if (phrase.Value.ValueKind != JsonValueKind.String) continue;
                        string? value = phrase.Value.GetString();
                        if (!string.IsNullOrEmpty(value)) map.TryAdd(phrase.Name, value);
                    }
                }
            }
            _byLanguage = next;
        }
        catch (Exception e)
        {
            _log.LogWarning(e, "Translations: could not read {Path}", _path);
        }
    }

    public static CultureInfo Culture(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return CultureInfo.InvariantCulture;
        try { return player.GetLanguage(); }
        catch { return CultureInfo.InvariantCulture; }
    }

    // Exact key in the player's language (or its parent), without the English fallback.
    public string? GetExact(CultureInfo culture, string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        ReloadIfChanged();
        foreach (string name in Candidates(culture, false))
            if (_byLanguage.TryGetValue(name, out var map) && map.TryGetValue(key, out var value))
                return value;
        return null;
    }

    // Exact key, English as the last resort, or null.
    public string? Get(CultureInfo culture, string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        ReloadIfChanged();
        foreach (string name in Candidates(culture, true))
            if (_byLanguage.TryGetValue(name, out var map) && map.TryGetValue(key, out var value))
                return value;
        return null;
    }

    private static IEnumerable<string> Candidates(CultureInfo culture, bool english)
    {
        if (!string.IsNullOrEmpty(culture.Name)) yield return culture.Name;
        if (!string.IsNullOrEmpty(culture.Parent.Name) && culture.Parent.Name != culture.Name) yield return culture.Parent.Name;
        if (!string.IsNullOrEmpty(culture.TwoLetterISOLanguageName) && culture.TwoLetterISOLanguageName != "iv")
            yield return culture.TwoLetterISOLanguageName;
        if (english) yield return "en";
    }
}

// What the PMM_StoreCore section starts with. Also the fallback when the file has no key.
internal static class StoreDefaults
{
    public static string Language(CultureInfo culture)
        => culture.TwoLetterISOLanguageName is "ru" or "uk" or "be" ? "ru" : "en";

    public static string Text(CultureInfo culture, string key)
    {
        if (Texts[Language(culture)].TryGetValue(key, out var v)) return v;
        return Texts["en"].TryGetValue(key, out v) ? v : key;
    }

    public static readonly Dictionary<string, Dictionary<string, string>> Texts = new()
    {
        ["ru"] = new()
        {
            ["pmm_store.title"] = "Магазин",
            ["pmm_store.tab_shop"] = "Магазин",
            ["pmm_store.tab_inv"] = "Инвентарь",
            ["pmm_store.tab_set"] = "Настройки",
            ["pmm_store.credits"] = "{0} кр.",
            ["pmm_store.back"] = "Назад",
            ["pmm_store.count"] = "{0} шт.",
            ["pmm_store.page"] = "{0} / {1}",
            ["pmm_store.other"] = "Разное",
            ["pmm_store.empty_shop"] = "Здесь пока ничего нет",
            ["pmm_store.empty_inv"] = "Инвентарь пуст. Купи что-нибудь во вкладке «Магазин»",
            ["pmm_store.state_equipped"] = "Надето",
            ["pmm_store.state_owned"] = "Куплено",
            ["pmm_store.state_free"] = "Бесплатно",
            ["pmm_store.price"] = "{0} кр.",
            ["pmm_store.folder"] = "{0} шт.",
            ["pmm_store.hint_open"] = "открыть",
            ["pmm_store.hint_buy"] = "купить",
            ["pmm_store.hint_equip"] = "надеть",
            ["pmm_store.hint_need"] = "не хватает {0}",
            ["pmm_store.buy"] = "Купить · {0} кр.",
            ["pmm_store.buy_free"] = "Получить",
            ["pmm_store.equip"] = "Надеть",
            ["pmm_store.unequip"] = "Снять",
            ["pmm_store.sell"] = "Продать · {0} кр.",
            ["pmm_store.inspect"] = "Осмотреть",
            ["pmm_store.l_price"] = "Цена",
            ["pmm_store.l_sell"] = "Продажа",
            ["pmm_store.l_type"] = "Тип",
            ["pmm_store.l_time"] = "Срок",
            ["pmm_store.forever"] = "навсегда",
            ["pmm_store.until"] = "до {0}",
            ["pmm_store.duration_d"] = "{0} дн.",
            ["pmm_store.duration_h"] = "{0} ч.",
            ["pmm_store.duration_m"] = "{0} мин.",
            ["pmm_store.once"] = "одно использование",
            ["pmm_store.need"] = "Не хватает {0} кр.",
            ["pmm_store.t_bought"] = "Куплено: {0}",
            ["pmm_store.t_used"] = "Готово: {0}",
            ["pmm_store.t_equipped"] = "Надето: {0}",
            ["pmm_store.t_unequipped"] = "Снято: {0}",
            ["pmm_store.t_sold"] = "Продано: {0} (+{1} кр.)",
            ["pmm_store.t_fail"] = "Не получилось: {0}. Причина в чате",
            ["pmm_store.t_inspect"] = "Осмотр: {0}",
            ["pmm_store.t_trails_on"] = "Чужие следы скрыты",
            ["pmm_store.t_trails_off"] = "Чужие следы видны",
            ["pmm_store.t_gift"] = "Подарок: {0} кр. → {1}",
            ["pmm_store.t_gift_fail"] = "Подарок не отправлен. Причина в чате",
            ["pmm_store.set_player"] = "Игрок",
            ["pmm_store.trails_name"] = "Скрывать чужие следы",
            ["pmm_store.trails_desc"] = "Следы других игроков у тебя не рисуются",
            ["pmm_store.alias_console"] = "или в консоли: {0}",
            ["pmm_store.alias_chat"] = "или в чате: {0}",
            ["pmm_store.set_none"] = "Других настроек у магазина нет",
            ["pmm_store.set_foot"] = "Переключатель делает то же, что команда. Писать её не нужно.",
            ["pmm_store.gift_name"] = "Подарить кредиты",
            ["pmm_store.gift_who"] = "Кому",
            ["pmm_store.gift_how"] = "Сколько",
            ["pmm_store.gift_empty"] = "На сервере нет других игроков",
            ["pmm_store.gift_send"] = "Подарить {0} кр. — {1}",
            ["pmm_store.gift_pick"] = "Выбери игрока и сумму",
            ["pmm_store.type.playerskin"] = "Скин игрока",
            ["pmm_store.type.weapon"] = "Оружие",
            ["pmm_store.type.health"] = "Здоровье",
            ["pmm_store.type.armor"] = "Броня",
            ["pmm_store.type.gravity"] = "Гравитация",
            ["pmm_store.type.speed"] = "Скорость",
            ["pmm_store.type.godmode"] = "Бессмертие",
            ["pmm_store.type.respawn"] = "Возрождение",
            ["pmm_store.type.bunnyhop"] = "Распрыжка",
            ["pmm_store.type.open"] = "Открыть двери",
            ["pmm_store.type.sound"] = "Звук",
            ["pmm_store.type.link"] = "Команда",
            ["pmm_store.type.smoke"] = "Цветной дым",
            ["pmm_store.type.trail"] = "След",
            ["pmm_store.type.tracer"] = "Трассер",
            ["pmm_store.type.grenadetrail"] = "След гранаты",
            ["pmm_store.type.coloredskin"] = "Цветной скин",
            ["pmm_store.type.customweapon"] = "Своё оружие",
            ["pmm_store.type.wings"] = "Крылья",
            ["pmm_store.type.equipment"] = "Аксессуар",
            ["pmm_store.type.chattag"] = "Тег в чате",
            ["pmm_store.type.scoretag"] = "Тег в таблице",
            ["pmm_store.type.namecolor"] = "Цвет ника",
            ["pmm_store.type.chatcolor"] = "Цвет сообщений",
            // categories of cs2-store-example.json
            ["Admin Player Skins"] = "Админ-скины",
            ["Buy Weapon"] = "Оружие",
            ["Features"] = "Способности",
            ["Gravity"] = "Гравитация",
            ["Speed"] = "Скорость",
            ["Godmode"] = "Бессмертие",
            ["Smoke"] = "Дым",
            ["Trail"] = "Следы",
            ["Player Trail"] = "След игрока",
            ["Grenade Trail"] = "След гранаты",
            ["Tracer"] = "Трассеры",
            ["Colored Skin"] = "Цветной скин",
            ["Custom Weapon"] = "Своё оружие",
            ["Pet"] = "Питомцы",
            ["Wings"] = "Крылья",
            ["Tags"] = "Теги",
            ["Chat Color"] = "Цвет сообщений",
            ["Name Color"] = "Цвет ника",
            ["Score Tag"] = "Тег в таблице",
            ["Chat Tag"] = "Тег в чате",
        },
        ["en"] = new()
        {
            ["pmm_store.title"] = "Store",
            ["pmm_store.tab_shop"] = "Store",
            ["pmm_store.tab_inv"] = "Inventory",
            ["pmm_store.tab_set"] = "Settings",
            ["pmm_store.credits"] = "{0} cr.",
            ["pmm_store.back"] = "Back",
            ["pmm_store.count"] = "{0} items",
            ["pmm_store.page"] = "{0} / {1}",
            ["pmm_store.other"] = "Other",
            ["pmm_store.empty_shop"] = "Nothing here yet",
            ["pmm_store.empty_inv"] = "Your inventory is empty. Buy something on the Store tab",
            ["pmm_store.state_equipped"] = "Equipped",
            ["pmm_store.state_owned"] = "Owned",
            ["pmm_store.state_free"] = "Free",
            ["pmm_store.price"] = "{0} cr.",
            ["pmm_store.folder"] = "{0} items",
            ["pmm_store.hint_open"] = "open",
            ["pmm_store.hint_buy"] = "buy",
            ["pmm_store.hint_equip"] = "equip",
            ["pmm_store.hint_need"] = "{0} short",
            ["pmm_store.buy"] = "Buy · {0} cr.",
            ["pmm_store.buy_free"] = "Get",
            ["pmm_store.equip"] = "Equip",
            ["pmm_store.unequip"] = "Unequip",
            ["pmm_store.sell"] = "Sell · {0} cr.",
            ["pmm_store.inspect"] = "Inspect",
            ["pmm_store.l_price"] = "Price",
            ["pmm_store.l_sell"] = "Sells for",
            ["pmm_store.l_type"] = "Type",
            ["pmm_store.l_time"] = "Duration",
            ["pmm_store.forever"] = "forever",
            ["pmm_store.until"] = "until {0}",
            ["pmm_store.duration_d"] = "{0} d",
            ["pmm_store.duration_h"] = "{0} h",
            ["pmm_store.duration_m"] = "{0} min",
            ["pmm_store.once"] = "single use",
            ["pmm_store.need"] = "{0} cr. short",
            ["pmm_store.t_bought"] = "Bought: {0}",
            ["pmm_store.t_used"] = "Done: {0}",
            ["pmm_store.t_equipped"] = "Equipped: {0}",
            ["pmm_store.t_unequipped"] = "Unequipped: {0}",
            ["pmm_store.t_sold"] = "Sold: {0} (+{1} cr.)",
            ["pmm_store.t_fail"] = "Failed: {0}. See chat",
            ["pmm_store.t_inspect"] = "Inspecting: {0}",
            ["pmm_store.t_trails_on"] = "Other players' trails are hidden",
            ["pmm_store.t_trails_off"] = "Other players' trails are shown",
            ["pmm_store.t_gift"] = "Gift: {0} cr. → {1}",
            ["pmm_store.t_gift_fail"] = "Gift not sent. See chat",
            ["pmm_store.set_player"] = "Player",
            ["pmm_store.trails_name"] = "Hide other players' trails",
            ["pmm_store.trails_desc"] = "Trails of other players are not drawn for you",
            ["pmm_store.alias_console"] = "or in console: {0}",
            ["pmm_store.alias_chat"] = "or in chat: {0}",
            ["pmm_store.set_none"] = "The store has no other settings",
            ["pmm_store.set_foot"] = "A switch does the same as the command. No need to type it.",
            ["pmm_store.gift_name"] = "Gift credits",
            ["pmm_store.gift_who"] = "To",
            ["pmm_store.gift_how"] = "Amount",
            ["pmm_store.gift_empty"] = "No other players on the server",
            ["pmm_store.gift_send"] = "Gift {0} cr. to {1}",
            ["pmm_store.gift_pick"] = "Pick a player and an amount",
            ["pmm_store.type.playerskin"] = "Player skin",
            ["pmm_store.type.weapon"] = "Weapon",
            ["pmm_store.type.health"] = "Health",
            ["pmm_store.type.armor"] = "Armor",
            ["pmm_store.type.gravity"] = "Gravity",
            ["pmm_store.type.speed"] = "Speed",
            ["pmm_store.type.godmode"] = "Godmode",
            ["pmm_store.type.respawn"] = "Respawn",
            ["pmm_store.type.bunnyhop"] = "Bunnyhop",
            ["pmm_store.type.open"] = "Open doors",
            ["pmm_store.type.sound"] = "Sound",
            ["pmm_store.type.link"] = "Command",
            ["pmm_store.type.smoke"] = "Smoke color",
            ["pmm_store.type.trail"] = "Trail",
            ["pmm_store.type.tracer"] = "Tracer",
            ["pmm_store.type.grenadetrail"] = "Grenade trail",
            ["pmm_store.type.coloredskin"] = "Colored skin",
            ["pmm_store.type.customweapon"] = "Custom weapon",
            ["pmm_store.type.wings"] = "Wings",
            ["pmm_store.type.equipment"] = "Accessory",
            ["pmm_store.type.chattag"] = "Chat tag",
            ["pmm_store.type.scoretag"] = "Scoreboard tag",
            ["pmm_store.type.namecolor"] = "Name color",
            ["pmm_store.type.chatcolor"] = "Chat color",
        },
    };
}
