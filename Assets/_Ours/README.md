# Assets/_Ours - this project's own assets

Everything else under `Assets/` (except the code folders) is the game's data: the setup exports it from each player's own dump,
and git ignores it. This folder is the exception: it is tracked, so assets made for this project - a new prefab, UI, icon,
material or ScriptableObject for a new feature - reach every player with `git pull`.

- Only original work goes here, never game data or edited copies of it. A change to one of the game's own assets ships as code
  or as a setup stage instead (see CONTRIBUTING.md, "Changing the setup").
- Commit each asset together with its `.meta` file: the GUID in it is how scenes, prefabs and code find the asset.
- Assets the game loads by name at runtime belong in `Assets/_Ours/Resources/` - Unity includes Resources folders in the standalone
  build automatically. Assets referenced from a scene or prefab need nothing extra.
