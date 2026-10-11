# PMM_StoreCore — changelog

[English](#english) · [Русский](#русский)

## English

### 0.1.0 — 2026-10-11
- First version. cs2-store v33 `!store` / `!inv` open an own PMM panel ("Showcase" design).
- Categories on the left, nested categories as folder cards, item cards three per row with their state: equipped, owned, price, free, "N short". No pages: categories, items and the gift player list scroll with the mouse wheel.
- Item page: price, sell price, duration, Buy / Equip / Unequip, Sell, Inspect (player skins, custom weapons). It replaces the purchase confirmation menu.
- A bought item is equipped at once (`EquipAfterPurchase`).
- Inventory tab: the same, owned items only.
- Settings tab: "Hide other players' trails" switch (HideTrails command) and "Gift credits" (player and amount, Gift command). No admin items.
- Panel, type and category texts: section `PMM_StoreCore` in `MenuManager_Modules_Translation.json`.
- Players on another menu type in `!menu` get the normal cs2-store menu through PMM.

## Русский

### 0.1.0 — 2026-10-11
- Первая версия. `!store` / `!inv` cs2-store v33 открывают свою панель PMM (вариант «Витрина»).
- Категории слева, вложенные категории как карточки-папки, карточки предметов по 3 в ряд со статусом: надето, куплено, цена, бесплатно, «не хватает N». Страниц нет: категории, предметы и список игроков для подарка прокручиваются колесом мыши.
- Карточка предмета: цена, цена продажи, срок, кнопки «Купить» / «Надеть» / «Снять», «Продать», «Осмотреть» (скины игрока и своё оружие). Карточка заменяет меню подтверждения покупки.
- После покупки предмет сразу надевается (`EquipAfterPurchase`).
- Вкладка «Инвентарь» — то же, только купленное.
- Вкладка «Настройки»: переключатель «Скрывать чужие следы» (команда HideTrails) и «Подарить кредиты» (выбор игрока и суммы, команда Gift). Админских пунктов нет.
- Переводы панели, типов и категорий — секция `PMM_StoreCore` в `MenuManager_Modules_Translation.json`.
- Игроки с другим типом меню в `!menu` получают обычное меню cs2-store через PMM.
