#!/usr/bin/env python3
"""
Generates the PMM_StoreCore panorama: pmm_store.xml + pmm_store.css.

    python3 gen_layout.py <addon root>      (writes <root>/panorama/...)

The plugin (Panel.cs) drives ids and class names from the constants below, keep them in sync:
  categories  st-c-0..23       (sidebar, scrolls with the mouse wheel)   icons st-c-ic-N, vars scnN / sccN
  cards       st-i-0..59       (3 per row, scrolls)             icons st-i-ic-N, vars sinN sisN sipN sihN
  gift        st-g-0..15       (players, 2 per row, scrolls)         vars sgnN
  amounts     st-a-0..3                                  vars sgaN
  buttons     st-close st-tab-shop st-tab-inv st-tab-set st-back
              st-it-main st-it-sell st-it-insp st-s-trails st-gsend
Every dialog variable is set on #st-root.
"""
import pathlib, sys

CATS = 24
CARDS, COLS = 60, 3
GIFT = 16
AMOUNTS = 4

# icon class name -> game image (CS2 client VPK, panorama/images/icons/<path>.vsvg)
ICONS = {
    # ui
    "health": "ui/health", "shield": "ui/shield", "fast": "ui/fast", "exojump": "ui/exojump",
    "parachute": "ui/parachute", "dollar_sign": "ui/dollar_sign", "coin_stack": "ui/coin_stack",
    "star": "ui/star", "gift": "ui/gift", "random": "ui/random", "clantag": "ui/clantag",
    "bullet": "ui/bullet", "bullet_burst": "ui/bullet_burst", "kill": "ui/kill", "refresh": "ui/refresh",
    "timer": "ui/timer", "key": "ui/key", "teamplayer": "ui/teamplayer", "colorwheel": "ui/colorwheel",
    "loadout": "ui/loadout", "inventory": "ui/inventory", "sound_3": "ui/sound_3", "crosshair": "ui/crosshair",
    "shoppingcart": "ui/shoppingcart", "leader": "ui/leader", "trophy": "ui/trophy", "settings": "ui/settings",
    "lock": "ui/locked", "check": "ui/check", "warning": "ui/warning", "clock": "ui/clock",
    # equipment
    "armor_helmet": "equipment/armor_helmet", "armor": "equipment/armor", "kevlar": "equipment/kevlar",
    "healthshot": "equipment/healthshot", "defuser": "equipment/defuser", "flashbang": "equipment/flashbang",
    "smokegrenade": "equipment/smokegrenade", "hegrenade": "equipment/hegrenade", "molotov": "equipment/molotov",
    "incgrenade": "equipment/incgrenade", "decoy": "equipment/decoy", "grenadepack": "equipment/grenadepack",
    "taser": "equipment/taser", "knife": "equipment/knife", "c4": "equipment/c4",
    "assaultsuit": "equipment/assaultsuit", "customplayer": "equipment/customplayer",
    # weapons (weapon_<name> -> ic-<name>)
    "ak47": "equipment/ak47", "aug": "equipment/aug", "awp": "equipment/awp", "bizon": "equipment/bizon",
    "cz75a": "equipment/cz75a", "deagle": "equipment/deagle", "elite": "equipment/elite",
    "famas": "equipment/famas", "fiveseven": "equipment/fiveseven", "g3sg1": "equipment/g3sg1",
    "galilar": "equipment/galilar", "glock": "equipment/glock", "hkp2000": "equipment/hkp2000",
    "m249": "equipment/m249", "m4a1": "equipment/m4a1", "m4a1_silencer": "equipment/m4a1_silencer",
    "mac10": "equipment/mac10", "mag7": "equipment/mag7", "mp5sd": "equipment/mp5sd", "mp7": "equipment/mp7",
    "mp9": "equipment/mp9", "negev": "equipment/negev", "nova": "equipment/nova", "p250": "equipment/p250",
    "p90": "equipment/p90", "revolver": "equipment/revolver", "sawedoff": "equipment/sawedoff",
    "scar20": "equipment/scar20", "sg556": "equipment/sg556", "ssg08": "equipment/ssg08", "tec9": "equipment/tec9",
    "ump45": "equipment/ump45", "usp_silencer": "equipment/usp_silencer", "xm1014": "equipment/xm1014",
}

# Accent of the panel. Change here and regenerate, or edit the colour block at the top of pmm_store.css.
ACCENT = "#ffa9de"
ACCENT_DARK = "#2a1020"   # text on accent

T = "\t"


def xml() -> str:
    o = []
    a = o.append
    a('<root>')
    a(T + '<styles>')
    a(T * 2 + '<include src="s2r://panorama/styles/csgostyles.vcss_c" />')
    a(T * 2 + '<include src="s2r://panorama/styles/custom_game/pmm_store.vcss_c" />')
    a(T + '</styles>')
    a(T + '<Panel class="st-screen" hittest="false">')
    a(T * 2 + '<Panel id="st-root" class="st-root st-hidden tab-shop view-grid">')
    a(T * 3 + '<Panel class="st-glow" hittest="false" />')

    # ---- header
    a(T * 3 + '<Panel class="st-head">')
    a(T * 4 + '<Panel class="st-badge" hittest="false"><Panel class="st-badge-ic" hittest="false" /></Panel>')
    a(T * 4 + '<Panel class="st-ht" hittest="false">')
    a(T * 5 + '<Label class="st-title stratum-bold-tf" text="{s:st_title}" />')
    a(T * 5 + '<Label class="st-nick stratum-medium-tf" text="{s:st_nick}" />')
    a(T * 4 + '</Panel>')
    a(T * 4 + '<Panel class="st-credits" hittest="false"><Panel class="st-coin" hittest="false" /><Label class="st-credits-t stratum-bold-tf" text="{s:st_credits}" /></Panel>')
    a(T * 4 + '<Panel class="st-tabs">')
    for tab in ("shop", "inv", "set"):
        a(T * 5 + f'<Button id="st-tab-{tab}" class="st-tab t-{tab}"><Label class="st-tab-t stratum-bold-tf" text="{{s:st_tab_{tab}}}" /></Button>')
    a(T * 4 + '</Panel>')
    a(T * 4 + '<Button id="st-close" class="st-x"><Panel class="st-x-ic" hittest="false" /></Button>')
    a(T * 3 + '</Panel>')

    # ---- body
    a(T * 3 + '<Panel class="st-body">')

    # sidebar
    a(T * 4 + '<Panel class="st-side only-store">')
    a(T * 5 + '<Panel class="st-side-list">')
    for i in range(CATS):
        a(T * 5 + f'<Button id="st-c-{i}" class="st-cat">')
        a(T * 6 + f'<Panel id="st-c-ic-{i}" class="st-cat-ic ic-star" hittest="false" />')
        a(T * 6 + f'<Label class="st-cat-t stratum-bold-tf" text="{{s:scn{i}}}" />')
        a(T * 6 + f'<Label class="st-cat-n stratum-medium-tf" text="{{s:scc{i}}}" />')
        a(T * 5 + '</Button>')
    a(T * 5 + '</Panel>')
    a(T * 4 + '</Panel>')

    # main column
    a(T * 4 + '<Panel class="st-main only-store">')
    a(T * 5 + '<Panel class="st-crumb">')
    a(T * 6 + '<Button id="st-back" class="st-back"><Panel class="st-back-ic" hittest="false" /><Label class="st-back-t stratum-bold-tf" text="{s:st_back}" /></Button>')
    a(T * 6 + '<Label class="st-crumb-t stratum-bold-tf" text="{s:st_crumb}" />')
    a(T * 6 + '<Label class="st-crumb-n stratum-medium-tf" text="{s:st_count}" />')
    a(T * 5 + '</Panel>')

    # grid
    a(T * 5 + '<Panel class="st-grid only-grid">')
    for r in range(CARDS // COLS):
        a(T * 6 + f'<Panel id="st-gr-{r}" class="st-grow">')
        for c in range(COLS):
            i = r * COLS + c
            a(T * 7 + f'<Button id="st-i-{i}" class="st-card p-price">')
            a(T * 8 + '<Panel class="st-card-top" hittest="false">')
            a(T * 9 + f'<Panel class="st-card-icbox" hittest="false"><Panel id="st-i-ic-{i}" class="st-card-ic ic-star" hittest="false" /></Panel>')
            a(T * 9 + '<Panel class="st-card-text" hittest="false">')
            a(T * 10 + f'<Label class="st-card-name stratum-bold-tf" text="{{s:sin{i}}}" />')
            a(T * 10 + f'<Label class="st-card-sub stratum-medium-tf" text="{{s:sis{i}}}" />')
            a(T * 9 + '</Panel>')
            a(T * 8 + '</Panel>')
            a(T * 8 + '<Panel class="st-card-bot" hittest="false">')
            a(T * 9 + f'<Panel class="st-pill" hittest="false"><Label class="st-pill-t stratum-bold-tf" text="{{s:sip{i}}}" /></Panel>')
            a(T * 9 + f'<Label class="st-card-hint stratum-medium-tf" text="{{s:sih{i}}}" />')
            a(T * 8 + '</Panel>')
            a(T * 7 + '</Button>')
        a(T * 6 + '</Panel>')
    a(T * 6 + '<Label class="st-empty stratum-medium-tf" text="{s:st_empty}" />')
    a(T * 5 + '</Panel>')

    # item view
    a(T * 5 + '<Panel class="st-item only-item">')
    a(T * 6 + '<Panel class="st-it-top" hittest="false">')
    a(T * 7 + '<Panel class="st-it-icbox" hittest="false"><Panel id="st-it-ic" class="st-it-ic ic-star" hittest="false" /></Panel>')
    a(T * 7 + '<Panel class="st-it-text" hittest="false">')
    a(T * 8 + '<Label class="st-it-name stratum-bold-tf" text="{s:st_it_name}" />')
    a(T * 8 + '<Label class="st-it-sub stratum-medium-tf" text="{s:st_it_sub}" />')
    a(T * 8 + '<Panel id="st-it-pill" class="st-it-pillrow p-price" hittest="false"><Panel class="st-pill" hittest="false"><Label class="st-pill-t stratum-bold-tf" text="{s:st_it_state}" /></Panel></Panel>')
    a(T * 7 + '</Panel>')
    a(T * 6 + '</Panel>')
    a(T * 6 + '<Panel class="st-it-info" hittest="false">')
    for i in range(3):
        a(T * 7 + f'<Panel id="st-it-box-{i}" class="st-it-box" hittest="false"><Label class="st-it-l stratum-medium-tf" text="{{s:st_it_l{i}}}" /><Label class="st-it-v stratum-bold-tf" text="{{s:st_it_v{i}}}" /></Panel>')
    a(T * 6 + '</Panel>')
    a(T * 6 + '<Label class="st-it-note stratum-medium-tf" text="{s:st_it_note}" />')
    a(T * 6 + '<Panel class="st-it-acts">')
    a(T * 7 + '<Button id="st-it-main" class="st-btn st-btn-main"><Label class="st-btn-t stratum-bold-tf" text="{s:st_it_main}" /></Button>')
    a(T * 7 + '<Button id="st-it-sell" class="st-btn st-btn-sec"><Label class="st-btn-t stratum-bold-tf" text="{s:st_it_sell}" /></Button>')
    a(T * 7 + '<Button id="st-it-insp" class="st-btn st-btn-sec"><Panel class="st-btn-ic" hittest="false" /><Label class="st-btn-t stratum-bold-tf" text="{s:st_it_insp}" /></Button>')
    a(T * 6 + '</Panel>')
    a(T * 5 + '</Panel>')
    a(T * 4 + '</Panel>')

    # settings
    a(T * 4 + '<Panel class="st-set only-set">')
    a(T * 5 + '<Panel class="st-set-col">')
    a(T * 6 + '<Label class="st-set-h stratum-bold-tf" text="{s:st_set_player}" />')
    a(T * 6 + '<Button id="st-s-trails" class="st-row st-trails">')
    a(T * 7 + '<Panel class="st-row-text" hittest="false">')
    a(T * 8 + '<Label class="st-row-name stratum-bold-tf" text="{s:st_trails_name}" />')
    a(T * 8 + '<Label class="st-row-desc stratum-medium-tf" text="{s:st_trails_desc}" />')
    a(T * 8 + '<Label class="st-row-alias stratum-medium-tf" text="{s:st_trails_alias}" />')
    a(T * 7 + '</Panel>')
    a(T * 7 + '<Panel class="st-sw" hittest="false"><Panel class="st-knob" hittest="false" /></Panel>')
    a(T * 6 + '</Button>')
    a(T * 6 + '<Label class="st-set-none stratum-medium-tf" text="{s:st_set_none}" />')
    a(T * 6 + '<Label class="st-set-foot stratum-medium-tf" text="{s:st_set_foot}" />')
    a(T * 5 + '</Panel>')
    a(T * 5 + '<Panel class="st-gift">')
    a(T * 6 + '<Panel class="st-gift-head" hittest="false">')
    a(T * 7 + '<Panel class="st-gift-ic" hittest="false" />')
    a(T * 7 + '<Panel class="st-row-text" hittest="false">')
    a(T * 8 + '<Label class="st-row-name stratum-bold-tf" text="{s:st_gift_name}" />')
    a(T * 8 + '<Label class="st-row-alias stratum-medium-tf" text="{s:st_gift_alias}" />')
    a(T * 7 + '</Panel>')
    a(T * 6 + '</Panel>')
    a(T * 6 + '<Label class="st-set-h stratum-bold-tf" text="{s:st_gift_who}" />')
    a(T * 6 + '<Panel class="st-glist">')
    for r in range(GIFT // 2):
        a(T * 6 + f'<Panel id="st-ggr-{r}" class="st-ggrow">')
        for c in range(2):
            i = r * 2 + c
            a(T * 7 + f'<Button id="st-g-{i}" class="st-chip st-gp"><Label class="st-chip-t stratum-bold-tf" text="{{s:sgn{i}}}" /></Button>')
        a(T * 6 + '</Panel>')
    a(T * 6 + '</Panel>')
    a(T * 6 + '<Label class="st-gift-empty stratum-medium-tf" text="{s:st_gift_empty}" />')
    a(T * 6 + '<Label class="st-set-h stratum-bold-tf" text="{s:st_gift_how}" />')
    a(T * 6 + '<Panel class="st-amrow">')
    for i in range(AMOUNTS):
        a(T * 7 + f'<Button id="st-a-{i}" class="st-chip st-am"><Label class="st-chip-t stratum-bold-tf" text="{{s:sga{i}}}" /></Button>')
    a(T * 6 + '</Panel>')
    a(T * 6 + '<Button id="st-gsend" class="st-btn st-btn-main st-gsend"><Label class="st-btn-t stratum-bold-tf" text="{s:st_gsend}" /></Button>')
    a(T * 5 + '</Panel>')
    a(T * 4 + '</Panel>')
    a(T * 3 + '</Panel>')

    # ---- footer
    a(T * 3 + '<Panel class="st-foot">')
    a(T * 4 + '<Panel id="st-toast" class="st-toast" hittest="false"><Panel class="st-toast-ic" hittest="false" /><Label class="st-toast-t stratum-medium-tf" text="{s:st_toast}" /></Panel>')
    a(T * 3 + '</Panel>')
    a(T * 2 + '</Panel>')
    a(T + '</Panel>')
    a('</root>')
    return "\n".join(o) + "\n"


BASE = r"""/* ============================================================
   PMM_StoreCore - cs2-store panorama (generated by tools/gen_layout.py)
   Server drives: dialog variables on #st-root, classes per player.
   Root classes: st-hidden, tab-shop | tab-inv | tab-set, view-grid | view-item,
                 has-back, is-empty, has-trails, no-trails, no-gift, gift-none,
                 has-sell, has-insp, main-dis, main-off, send-dis, toast-on, toast-warn, pos-left | pos-right
   Card classes: p-equipped | p-owned | p-price | p-poor | p-free | p-folder, slot-off
   ============================================================ */

@define dur: 0.14s;

.st-screen
{
	width: 100%;
	height: 100%;
	z-index: 2000000000;
}

.st-root
{
	width: 1040px;
	height: fit-children;
	flow-children: down;
	horizontal-align: center;
	vertical-align: middle;
	background-color: gradient( linear, 0% 0%, 0% 100%, from( #141318f2 ), to( #0d0c10f5 ) );
	border: 1px solid #2b2930;
	border-radius: 12px;
	box-shadow: #000000c0 0px 18px 50px 0px;
	overflow: clip clip;
}

.st-root.st-hidden { visibility: collapse; }
.st-root.pos-left { horizontal-align: left; margin-left: 40px; }
.st-root.pos-right { horizontal-align: right; margin-right: 40px; }

.st-glow
{
	width: 100%;
	height: 2px;
	background-color: ACCENT;
}

.slot-off { visibility: collapse; }

/* ---------- views ---------- */

.only-store, .only-set, .only-grid, .only-item { visibility: collapse; }
.st-root.tab-shop .only-store, .st-root.tab-inv .only-store { visibility: visible; }
.st-root.tab-set .only-set { visibility: visible; }
.st-root.view-grid .only-grid { visibility: visible; }
.st-root.view-item .only-item { visibility: visible; }

/* ---------- header ---------- */

.st-head
{
	width: 100%;
	height: 86px;
	flow-children: right;
	padding: 17px 22px 17px 22px;
	border-bottom: 1px solid #25232a;
}

.st-badge
{
	width: 52px;
	height: 52px;
	border-radius: 12px;
	vertical-align: center;
	background-color: ACCENT;
}

.st-badge-ic
{
	width: 30px;
	height: 30px;
	horizontal-align: center;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/shoppingcart.vsvg");
	background-size: 100% 100%;
	wash-color: ACCDARK;
}

.st-ht
{
	width: 210px;
	height: fit-children;
	flow-children: down;
	vertical-align: center;
	margin-left: 16px;
}

.st-title
{
	width: 100%;
	font-size: 24px;
	color: #f2eef0;
	letter-spacing: 2.5px;
	text-transform: uppercase;
	text-overflow: shrink;
	white-space: nowrap;
}

.st-nick
{
	width: 100%;
	font-size: 13px;
	color: #a39ca2;
	margin-top: 2px;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.st-credits
{
	height: 32px;
	padding: 0px 14px 0px 10px;
	vertical-align: center;
	flow-children: right;
	border-radius: 16px;
	background-color: #f3c9691f;
	border: 1px solid #f3c96959;
}

.st-coin
{
	width: 16px;
	height: 16px;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/coin_stack.vsvg");
	background-size: 100% 100%;
	wash-color: #f3c969;
}

.st-credits-t
{
	vertical-align: center;
	margin-left: 7px;
	font-size: 15px;
	color: #f3c969;
	white-space: nowrap;
}

.st-tabs
{
	height: 44px;
	vertical-align: center;
	horizontal-align: right;
	flow-children: right;
	padding: 4px;
	margin-left: 20px;
	border-radius: 10px;
	background-color: #141318;
	border: 1px solid #25232a;
}

.st-tab
{
	height: 34px;
	padding: 0px 16px;
	margin-right: 4px;
	border-radius: 7px;
	background-color: #00000000;
	transition-property: background-color;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.st-tab:last-child { margin-right: 0px; }
.st-tab:hover { background-color: #ffffff12; }

.st-tab-t
{
	vertical-align: center;
	font-size: 12px;
	letter-spacing: 1.2px;
	text-transform: uppercase;
	color: #c9c3c8;
}

.st-root.tab-shop .t-shop, .st-root.tab-inv .t-inv, .st-root.tab-set .t-set { background-color: ACCENT; }
.st-root.tab-shop .t-shop .st-tab-t, .st-root.tab-inv .t-inv .st-tab-t, .st-root.tab-set .t-set .st-tab-t { color: ACCDARK; }

.st-x
{
	width: 40px;
	height: 40px;
	vertical-align: center;
	margin-left: 12px;
	border-radius: 9px;
	background-color: #1c1b21;
	transition-property: background-color;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.st-x:hover { background-color: #c93642; }

.st-x-ic
{
	width: 14px;
	height: 14px;
	horizontal-align: center;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/cancel.vsvg");
	background-size: 100% 100%;
	wash-color: #e4dfe3;
}

/* ---------- body ---------- */

.st-body
{
	width: 100%;
	height: 560px;
	flow-children: right;
}

/* sidebar */

.st-side
{
	width: 250px;
	height: 100%;
	flow-children: down;
	padding: 14px 12px 10px 12px;
	border-right: 1px solid #25232a;
}

.st-cat
{
	width: 214px;
	height: 40px;
	margin-bottom: 2px;
	flow-children: right;
	border-radius: 8px;
	background-color: #00000000;
	transition-property: background-color;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.st-cat:hover { background-color: #ffffff0d; }
.st-cat.a { background-color: ACCENT1f; }

.st-cat-ic
{
	width: 20px;
	height: 20px;
	margin-left: 12px;
	vertical-align: center;
	background-size: 100% 100%;
	background-repeat: no-repeat;
	wash-color: #c9c3c8;
}

.st-cat.a .st-cat-ic { wash-color: ACCENT; }

.st-cat-t
{
	width: fill-parent-flow( 1.0 );
	margin-left: 12px;
	vertical-align: center;
	font-size: 14px;
	color: #c9c3c8;
	text-overflow: shrink;
	white-space: nowrap;
}

.st-cat.a .st-cat-t { color: ACCENT; }

.st-cat-n
{
	margin-right: 12px;
	margin-left: 6px;
	vertical-align: center;
	font-size: 12px;
	color: #7e777d;
}

/* category list: scrolls with the mouse wheel */
.st-side-list
{
	width: 100%;
	height: 536px;
	flow-children: down;
	overflow: squish scroll;
}

/* main column */

.st-main
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	flow-children: down;
	padding: 16px 20px 10px 20px;
}

.st-crumb
{
	width: 100%;
	height: 36px;
	flow-children: right;
	margin-bottom: 12px;
}

.st-back
{
	height: 32px;
	padding: 0px 12px 0px 8px;
	margin-right: 12px;
	vertical-align: center;
	flow-children: right;
	border-radius: 8px;
	background-color: #1c1b21;
	visibility: collapse;
	transition-property: background-color;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.st-root.has-back .st-back { visibility: visible; }
.st-back:hover { background-color: #2a2830; }

.st-back-ic
{
	width: 12px;
	height: 12px;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/expand.vsvg");
	background-size: 100% 100%;
	transform: rotateZ( 90deg );
	wash-color: #e4dfe3;
}

.st-back-t
{
	vertical-align: center;
	margin-left: 6px;
	font-size: 12px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: #e4dfe3;
}

.st-crumb-t
{
	max-width: 480px;
	vertical-align: center;
	font-size: 17px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: #f2eef0;
	text-overflow: shrink;
	white-space: nowrap;
}

.st-crumb-n
{
	vertical-align: center;
	margin-left: 10px;
	font-size: 13px;
	color: #a39ca2;
}

/* grid */

/* item cards: one list, scrolls with the mouse wheel */
.st-grid
{
	width: 100%;
	height: 486px;
	flow-children: down;
	overflow: squish scroll;
}

.st-grow
{
	width: 100%;
	height: 124px;
	flow-children: right;
}

.st-card
{
	width: 236px;
	height: 112px;
	margin-right: 12px;
	flow-children: down;
	padding: 14px;
	border-radius: 10px;
	background-color: #1c1b21;
	border: 1px solid #2b2930;
	transition-property: background-color, border;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.st-card:last-child { margin-right: 0px; }
.st-card:hover { background-color: #24222a; border: 1px solid #3a3741; }

.st-card-top
{
	width: 100%;
	height: 40px;
	flow-children: right;
}

.st-card-icbox
{
	width: 36px;
	height: 36px;
	border-radius: 9px;
	background-color: #26242c;
}

.st-card-ic
{
	width: 22px;
	height: 22px;
	horizontal-align: center;
	vertical-align: center;
	background-size: 100% 100%;
	background-repeat: no-repeat;
	wash-color: ACCENT;
}

.st-card.p-poor .st-card-ic { wash-color: #6e676d; }

.st-card-text
{
	width: fill-parent-flow( 1.0 );
	height: fit-children;
	flow-children: down;
	margin-left: 10px;
}

.st-card-name
{
	width: 100%;
	font-size: 14px;
	color: #f2eef0;
	text-overflow: shrink;
	white-space: nowrap;
}

.st-card-sub
{
	width: 100%;
	margin-top: 3px;
	font-size: 12px;
	color: #a39ca2;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.st-card-bot
{
	width: 100%;
	height: 26px;
	margin-top: 16px;
	flow-children: right;
}

.st-pill
{
	height: 24px;
	padding: 0px 10px;
	vertical-align: center;
	border-radius: 12px;
	background-color: #f3c9691f;
	border: 1px solid #f3c96959;
}

.st-pill-t
{
	vertical-align: center;
	font-size: 12px;
	color: #f3c969;
	white-space: nowrap;
}

.p-equipped .st-pill { background-color: ACCENT24; border: 1px solid ACCENT66; }
.p-equipped .st-pill-t { color: ACCENT; }
.p-owned .st-pill, .p-free .st-pill { background-color: #7bd88f1f; border: 1px solid #7bd88f59; }
.p-owned .st-pill-t, .p-free .st-pill-t { color: #7bd88f; }
.p-poor .st-pill { background-color: #232128; border: 1px solid #2e2c33; }
.p-poor .st-pill-t { color: #8e878d; }
.p-folder .st-pill { background-color: #ffffff0d; border: 1px solid #ffffff1f; }
.p-folder .st-pill-t { color: #e4dfe3; }

.st-card-hint
{
	width: fill-parent-flow( 1.0 );
	margin-left: 8px;
	vertical-align: center;
	text-align: right;
	font-size: 12px;
	color: #8e878d;
	text-overflow: shrink;
	white-space: nowrap;
}

.st-empty
{
	width: 100%;
	margin-top: 120px;
	text-align: center;
	font-size: 15px;
	color: #8e878d;
	visibility: collapse;
}

.st-root.is-empty .st-empty { visibility: visible; }

/* item view */

.st-item
{
	width: 100%;
	height: fit-children;
	flow-children: down;
}

.st-it-top
{
	width: 100%;
	height: 150px;
	flow-children: right;
}

.st-it-icbox
{
	width: 150px;
	height: 150px;
	border-radius: 14px;
	background-color: gradient( radial, 50% 45%, 0% 0%, 70% 70%, from( ACCENT33 ), to( #1a1920 ) );
	border: 1px solid #2b2930;
}

.st-it-ic
{
	width: 76px;
	height: 76px;
	horizontal-align: center;
	vertical-align: center;
	background-size: 100% 100%;
	background-repeat: no-repeat;
	wash-color: ACCENT;
}

.st-it-text
{
	width: fill-parent-flow( 1.0 );
	height: fit-children;
	flow-children: down;
	vertical-align: center;
	margin-left: 22px;
}

.st-it-name
{
	width: 100%;
	font-size: 26px;
	letter-spacing: 1.5px;
	text-transform: uppercase;
	color: #f2eef0;
	text-overflow: shrink;
	white-space: nowrap;
}

.st-it-sub
{
	width: 100%;
	margin-top: 4px;
	font-size: 14px;
	color: #a39ca2;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.st-it-pillrow
{
	height: 26px;
	margin-top: 12px;
	flow-children: right;
}

.st-it-info
{
	width: 100%;
	height: 66px;
	margin-top: 22px;
	flow-children: right;
}

.st-it-box
{
	width: 240px;
	height: 62px;
	margin-right: 12px;
	flow-children: down;
	padding: 10px 14px;
	border-radius: 10px;
	background-color: #ffffff0a;
}

.st-it-box:last-child { margin-right: 0px; }

.st-it-l
{
	font-size: 12px;
	color: #8e878d;
}

.st-it-v
{
	width: 100%;
	margin-top: 4px;
	font-size: 16px;
	color: #f2eef0;
	text-overflow: shrink;
	white-space: nowrap;
}

.st-it-note
{
	width: 100%;
	margin-top: 14px;
	font-size: 13px;
	color: #ff8e8e;
}

.st-it-acts
{
	width: 100%;
	height: 56px;
	margin-top: 16px;
	flow-children: right;
}

.st-btn
{
	height: 50px;
	padding: 0px 22px;
	margin-right: 10px;
	flow-children: right;
	border-radius: 11px;
	transition-property: background-color, opacity;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.st-btn-main { min-width: 260px; background-color: ACCENT; }
.st-btn-main:hover { background-color: ACCLIGHT; }
.st-btn-sec { background-color: #26242c; border: 1px solid #34313a; }
.st-btn-sec:hover { background-color: #302d37; }

.st-btn-t
{
	horizontal-align: center;
	vertical-align: center;
	font-size: 14px;
	letter-spacing: 1.2px;
	text-transform: uppercase;
	color: #f2eef0;
	white-space: nowrap;
}

.st-btn-main .st-btn-t { color: ACCDARK; }

.st-btn-ic
{
	width: 18px;
	height: 18px;
	margin-right: 8px;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/zoom_in.vsvg");
	background-size: 100% 100%;
	wash-color: #f2eef0;
}

#st-it-sell, #st-it-insp { visibility: collapse; }
.st-root.has-sell #st-it-sell { visibility: visible; }
.st-root.has-insp #st-it-insp { visibility: visible; }
.st-root.main-off #st-it-main { visibility: collapse; }
.st-root.main-dis #st-it-main { background-color: #2a2830; }
.st-root.main-dis #st-it-main .st-btn-t { color: #8e878d; }

/* ---------- settings ---------- */

.st-set
{
	width: 100%;
	height: 100%;
	flow-children: right;
	padding: 20px 22px 10px 22px;
}

.st-set-col
{
	width: 470px;
	height: 100%;
	flow-children: down;
	margin-right: 22px;
}

.st-set-h
{
	width: 100%;
	margin: 6px 0px 8px 2px;
	font-size: 11px;
	letter-spacing: 2.2px;
	text-transform: uppercase;
	color: #8e878d;
}

.st-row
{
	width: 100%;
	height: fit-children;
	flow-children: right;
	padding: 14px 16px;
	margin-bottom: 10px;
	border-radius: 10px;
	background-color: #1c1b21;
	border: 1px solid #2b2930;
	visibility: collapse;
	transition-property: background-color;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.st-row:hover { background-color: #24222a; }
.st-root.has-trails .st-trails { visibility: visible; }

.st-row-text
{
	width: fill-parent-flow( 1.0 );
	height: fit-children;
	flow-children: down;
	vertical-align: center;
}

.st-row-name
{
	width: 100%;
	font-size: 15px;
	color: #f2eef0;
	text-overflow: shrink;
	white-space: nowrap;
}

.st-row-desc
{
	width: 100%;
	margin-top: 4px;
	font-size: 12px;
	color: #a39ca2;
}

.st-row-alias
{
	width: 100%;
	margin-top: 5px;
	font-size: 11px;
	color: #7e777d;
}

.st-sw
{
	width: 46px;
	height: 26px;
	margin-left: 14px;
	vertical-align: center;
	border-radius: 13px;
	background-color: #3a3740;
	transition-property: background-color;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.st-knob
{
	width: 20px;
	height: 20px;
	margin-left: 3px;
	horizontal-align: left;
	vertical-align: center;
	border-radius: 10px;
	background-color: #ffffff;
}

.st-trails.on .st-sw { background-color: ACCENT; }
.st-trails.on .st-knob { horizontal-align: right; margin-left: 0px; margin-right: 3px; }

.st-set-none
{
	width: 100%;
	font-size: 13px;
	color: #8e878d;
	visibility: collapse;
}

.st-root.no-trails .st-set-none { visibility: visible; }

.st-set-foot
{
	width: 100%;
	margin-top: 10px;
	font-size: 12px;
	color: #8e878d;
}

.st-gift
{
	width: fill-parent-flow( 1.0 );
	height: fit-children;
	flow-children: down;
	padding: 16px;
	border-radius: 10px;
	background-color: #1c1b21;
	border: 1px solid ACCENT;
}

.st-root.no-gift .st-gift { visibility: collapse; }

.st-gift-head
{
	width: 100%;
	height: fit-children;
	flow-children: right;
	margin-bottom: 8px;
}

.st-gift-ic
{
	width: 22px;
	height: 22px;
	margin-right: 12px;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/gift.vsvg");
	background-size: 100% 100%;
	wash-color: ACCENT;
}

.st-ggrow
{
	width: 100%;
	height: 42px;
	flow-children: right;
}

.st-chip
{
	height: 36px;
	border-radius: 8px;
	background-color: #26242c;
	border: 1px solid #34313a;
	transition-property: background-color, border;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.st-chip:hover { background-color: #302d37; }
.st-chip.sel { background-color: ACCENT24; border: 1px solid ACCENT; }

.st-gp { width: 214px; margin-right: 8px; }
.st-gp:last-child { margin-right: 0px; }

.st-chip-t
{
	width: 90%;
	horizontal-align: center;
	vertical-align: center;
	text-align: center;
	font-size: 13px;
	color: #e4dfe3;
	text-overflow: shrink;
	white-space: nowrap;
}

.st-chip.sel .st-chip-t { color: ACCENT; }

.st-gift-empty
{
	width: 100%;
	margin: 6px 0px 10px 2px;
	font-size: 13px;
	color: #8e878d;
	visibility: collapse;
}

.st-root.gift-none .st-gift-empty { visibility: visible; }

/* gift players: scrolls with the mouse wheel */
.st-glist
{
	width: 100%;
	height: 172px;
	flow-children: down;
	overflow: squish scroll;
}

.st-amrow
{
	width: 100%;
	height: 44px;
	flow-children: right;
}

.st-am { width: 107px; margin-right: 8px; }
.st-am:last-child { margin-right: 0px; }

.st-gsend
{
	width: 100%;
	margin-top: 8px;
	margin-right: 0px;
}

.st-root.send-dis .st-gsend { background-color: #2a2830; }
.st-root.send-dis .st-gsend .st-btn-t { color: #8e878d; }

/* ---------- footer ---------- */

.st-foot
{
	width: 100%;
	height: 56px;
	flow-children: right;
	padding: 12px 22px;
	border-top: 1px solid #25232a;
}

.st-toast
{
	height: 32px;
	max-width: 600px;
	vertical-align: center;
	padding: 0px 12px;
	flow-children: right;
	border-radius: 16px;
	background-color: #7bd88f1f;
	border: 1px solid #7bd88f59;
	opacity: 0;
	transition-property: opacity;
	transition-duration: 0.2s;
	transition-timing-function: ease-out;
}

.st-root.toast-on .st-toast { opacity: 1; }
.st-root.toast-warn .st-toast { background-color: #ff7a7a1f; border: 1px solid #ff7a7a59; }

.st-toast-ic
{
	width: 13px;
	height: 13px;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/check.vsvg");
	background-size: 100% 100%;
	wash-color: #7bd88f;
}

.st-root.toast-warn .st-toast-ic { background-image: url("s2r://panorama/images/icons/ui/cancel.vsvg"); wash-color: #ff8e8e; }

.st-toast-t
{
	max-width: 560px;
	margin-left: 8px;
	vertical-align: center;
	font-size: 13px;
	color: #7bd88f;
	text-overflow: shrink;
	white-space: nowrap;
}

.st-root.toast-warn .st-toast-t { color: #ff8e8e; }

.st-pb
{
	width: 32px;
	height: 32px;
	vertical-align: center;
	border-radius: 8px;
	background-color: #1c1b21;
	transition-property: background-color;
	transition-duration: dur;
	transition-timing-function: ease-out;
}

.st-pb:hover { background-color: #2a2830; }

.st-pb-ic
{
	width: 12px;
	height: 12px;
	horizontal-align: center;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/expand.vsvg");
	background-size: 100% 100%;
	transform: rotateZ( -90deg );
	wash-color: #f2eef0;
}

.st-pb-l { transform: rotateZ( 90deg ); }
"""


def lighten(hexc: str, f: float) -> str:
    r, g, b = (int(hexc[i:i + 2], 16) for i in (1, 3, 5))
    r, g, b = (round(c + (255 - c) * f) for c in (r, g, b))
    return f"#{r:02x}{g:02x}{b:02x}"


def css() -> str:
    # ACCENT1f / ACCENT24 ... = accent with alpha; longest names first.
    body = BASE
    for alpha in ("1f", "24", "33", "66"):
        body = body.replace("ACCENT" + alpha, ACCENT + alpha)
    body = body.replace("ACCLIGHT", lighten(ACCENT, 0.25)).replace("ACCDARK", ACCENT_DARK).replace("ACCENT", ACCENT)
    o = [body, "\n/* ---------- icons (ic-*) ---------- */\n"]
    for name, path in ICONS.items():
        o.append(f'.ic-{name} {{ background-image: url("s2r://panorama/images/icons/{path}.vsvg"); }}\n')
    return "".join(o)


def main():
    root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else ".")
    lay = root / "panorama" / "layout" / "custom_game"
    sty = root / "panorama" / "styles" / "custom_game"
    lay.mkdir(parents=True, exist_ok=True)
    sty.mkdir(parents=True, exist_ok=True)
    (lay / "pmm_store.xml").write_text(xml(), encoding="utf-8")
    (sty / "pmm_store.css").write_text(css(), encoding="utf-8")
    print("icons:", " ".join(ICONS))


if __name__ == "__main__":
    main()
