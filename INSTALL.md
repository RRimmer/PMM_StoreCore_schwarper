# PMM_StoreCore 0.1.0

## RU

Магазин [cs2-store](https://github.com/schwarper/cs2-store) (schwarper, v33) на своей панели PanoramaMenuManager:
категории слева, карточки предметов, карточка предмета с покупкой, надеванием, продажей и осмотром,
вкладки «Инвентарь» и «Настройки» (скрыть чужие следы, подарить кредиты).
cs2-store не пересобирается: модуль перехватывает `!store` / `!inv` через Harmony и работает через `IStoreApi`.

### Что нужно

- CounterStrikeSharp 1.0.376+
- PanoramaMenuManager (MenuManagerCore) **1.2.04+** и `shared/MenuManagerApi` той же версии
- cs2-store v33 (`plugins/cs2-store`, `shared/StoreApi`). CS2MenuManager от schwarper **не нужен**: ядро PMM 1.2.04 само кладёт свой `shared/CS2MenuManager`.

### Сервер

Скопируй `Server-plugins/counterstrikesharp` в `game/csgo/addons/`. Получится:

- `addons/counterstrikesharp/plugins/PMM_StoreCore/PMM_StoreCore.dll`
- `addons/counterstrikesharp/plugins/PMM_StoreCore/harmony/0Harmony.dll` — в папке `harmony`, не рядом с dll
- `addons/counterstrikesharp/configs/plugins/PMM_StoreCore/PMM_StoreCore.json`

`StoreApi.dll` и `MenuManagerApi.dll` в папку плагина не кладутся — только в `shared/`.
После замены dll полностью перезапусти сервер.

### Клиентский аддон

Скопируй `Content-addonmanager/panorama` в корень аддона MultiAddonManager (туда же, где лежит `menu_ui.xml` ядра),
собери аддон в Workshop Tools и залей. Файлы:

- `panorama/layout/custom_game/pmm_store.xml`
- `panorama/styles/custom_game/pmm_store.css`

Пока у игрока нет нового аддона, `!store` у него открывает пустую панель — игроку нужно перезайти после обновления аддона.

### Кому показывается панель

`MenuTypes` в конфиге — типы меню PMM, при которых `!store` открывает панель. По умолчанию `PanoramaMenu` (мышь).
У игрока с другим типом в `!menu` (WASD, CSGO Menu, чат) остаётся обычное меню cs2-store через PMM.

### Переводы

Тексты панели, названия типов и категорий — в секции `PMM_StoreCore` файла
`configs/plugins/MenuManagerCore/MenuManager_Modules_Translation.json` (ru / en). Секция добавляется при первом запуске,
потом файл твой: модуль только дописывает недостающие ключи. Название категории или предмета ищется так:
секция `PMM_StoreCore` на языке игрока → `lang/*.json` cs2-store → секция `PMM_StoreCore` (en) → ключ как есть.
Свою категорию переводи ключом как в `cs2-store.json`, например `"Buy Weapon": "Оружие"`.

### Команды

`css_pmm_store` (консоль сервера) — состояние; `css_pmm_store reattach` — заново найти cs2-store и PMM.

## EN

cs2-store (schwarper, v33) on its own PanoramaMenuManager panel: categories, item cards, an item page with buy / equip /
sell / inspect, Inventory and Settings tabs (hide other players' trails, gift credits). cs2-store is not rebuilt: `!store`
and `!inv` are intercepted with Harmony, every action goes through `IStoreApi`.

Needs PanoramaMenuManager 1.2.04+ (it ships its own `shared/CS2MenuManager`, schwarper's CS2MenuManager is not needed)
and cs2-store v33. Server: copy `Server-plugins/counterstrikesharp` into `game/csgo/addons/`, keep `harmony/0Harmony.dll`
in its folder, restart the server. Client: add `Content-addonmanager/panorama` to the MultiAddonManager addon and rebuild it.
`MenuTypes` (default `PanoramaMenu`) chooses who gets the panel; everyone else keeps the normal cs2-store menu.
Texts: section `PMM_StoreCore` of `configs/plugins/MenuManagerCore/MenuManager_Modules_Translation.json`.
