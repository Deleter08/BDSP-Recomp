# Third-party software

## Included in this repository

| What | Where | Licence |
|---|---|---|
| crunch, Unity Technologies' fork (commit 8708900e) - the decoder header + our small `crn_levels` source; clean snapshots rebuild helpers locally | `setup/bin/crunch_src/` | zlib licence (see the end of `crn_decomp.h`). Copyright (c) 2010-2016 Richard Geldreich, Jr. and Binomial LLC |

## Public setup dependencies (not redistributed here)

| What | Licence |
|---|---|
| Python 3.13.12 (python.org's NuGet package) | Python Software Foundation License |
| UnityPy, numpy, Pillow, texture2ddecoder and their dependencies (`setup/requirements.lock`, from PyPI) | each package's own licence (MIT / BSD / HPND / ...) |
| AssetRipper 1.3.14 (free edition, official GitHub release; user-supplied in the macOS launcher) | GPL-3.0 |

## Installed by you

| What | Licence |
|---|---|
| DOTween 1.2.335 (Demigiant); DLLs and artwork installed locally by `setup/stages/dotween.py`, not published | Original licence/readme retained in `Assets/Plugins/DOTween/readme.txt`; http://dotween.demigiant.com/license.php |
| Unity 6000.6.6f1 (this branch; legacy setup used Unity 2019.4) | Unity's terms |
| Audiokinetic Wwise Unity Integration **2019.2.15.2215** (SDK 7667) - not included; the setup extracts it from your own Launcher **offline package** (`--wwise-package`). The repository keeps only integration metadata: the `.meta` files under `Assets/Wwise` (the Unity GUIDs the game's assets reference), `Assets/Wwise/LauncherInfo.json` (an upstream file naming an older 2019.2.5 install), and three project settings ScriptableObjects (`Assets/Wwise/ScriptableObjects/AkWwiseInitializationSettings.asset`, `Windows.asset`, `Mac.asset`). No Audiokinetic code or engine binaries | Audiokinetic's end-user licence |

## Used while making this project (not included)

| What | Licence |
|---|---|
| Il2CppDumper | MIT |
| Ghidra | Apache-2.0 |

## Recovered and game-derived material (included; not covered by the MIT License)

The game's asset files (textures, meshes, audio, movies, asset bundles) are not in this repository: the setup exports them from
your dump. The repository **does** include material recovered or derived from Pokémon Brilliant Diamond. Its owners' rights are
unaffected, no licence to it is granted by `LICENSE`, and no rights clearance is claimed:

| What | Where |
|---|---|
| C# game code restored from the game's executable | `Assets/Scripts/` (`Game`, `AssetAssistant`, `DebugLog`, `nn`, `DOTweenPro`); this project's own platform code is `Assets/Scripts/_Ours/` |
| Shader source translated from the game's compiled shader programs | `setup/data/shaders/` |
| Index tables derived from the game's data (GUID map, bundle → asset-path map, atlas GUIDs, script references, Wwise event IDs) | `setup/data/guid_table.tsv.gz`, `Assets/Editor/EditorAssetAssistant/bundle_map.tsv`, `setup/data/atlas_guids.tsv`, `setup/data/script_refs.tsv`, `Assets/Scripts/Game/AK/EVENTS.cs` |

The restored C#, the translated shaders and the `setup/data` tables are also present in the public upstream
[uDPR](https://github.com/Manurocker95/uDPR) repository; that is context, not permission.

## Licence of this project's own work

This project's own work is under the MIT License (`LICENSE`, copyright pkdec, the upstream uDPR author's notice, kept as MIT
requires). Third-party components keep their own licences as listed above.
