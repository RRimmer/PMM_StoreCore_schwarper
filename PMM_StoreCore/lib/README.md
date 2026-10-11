# lib

`0Harmony.dll` — [Lib.Harmony](https://github.com/pardeike/Harmony) 2.4.2, `lib/net10.0` (MIT license).
It is kept in the repository so the plugin builds without the NuGet package. The build copies it to
`bin/.../harmony/0Harmony.dll`. On the server it must stay in `plugins/PMM_StoreCore/harmony/`:
the plugin loads it into the default (non-collectible) load context itself.
If PMM_VIPCore or PMM_GG1MapChooser is installed too, the copy it already loaded is reused.

`StoreApi.dll` — `shared/StoreApi` of [cs2-store](https://github.com/schwarper/cs2-store) v33 (schwarper, GPL-3.0).
Compile-time only (`Private=false`): the server already has it in `shared/StoreApi`. Never ship it next to this plugin.
