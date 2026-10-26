# Contributing to uDPR

Thank you for helping. Bug reports are as valuable as code: most of the game has not been played through on PC yet.

## Reporting a problem

Open an issue and say what you did, what you expected and what happened instead.

- **Setup problems:** attach the files in `SetupWork\logs\` (at least `setup.log` and the log of the stage that failed) and say
  which line of `setup.cmd`'s output you stopped at.
- **Game problems:** attach `Player.log` from `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Dpr\` (or the editor's Console output),
  the place in the game, and - if you can - a save from just before it (`SaveData.bin`).
- Your PC: Windows version, CPU, RAM, GPU and driver version.

Never attach game files, exported assets or a build: they are the game's copyrighted data.

## Pull requests

Keep each pull request to one fix or feature, and describe how you tested it.

**Never commit game data.** `.gitignore` keeps everything the setup exports out of the repository; do not force-add exported assets,
textures, sound banks, shipped bundles or built players. Do not add the Audiokinetic Wwise integration files either - only their
`.meta` files are tracked.

**Game code (`Assets/Scripts/{Game,AssetAssistant,DebugLog,nn,DOTweenPro}`)**

- It mirrors the shipped game: one file per type at `<Assembly>/<namespace>/<Type>.cs`, the shipped names, signatures and
  attributes. A fix should make a method behave like the shipped one; work from the game's own executable (your dump), not from
  other reconstruction projects.
- Keep the shipped code paths. Switch-only code stays under `#if SWITCH`; where PC behaviour must differ from a shipped statement,
  put the PC version in the `#if` arm of a PC define and keep the shipped statement in the `#else` arm (see `PC_UNLOCKED_FPS`).
- Call `UnityEngine.Debug` explicitly (the game has its own `Debug` types).
- `.cs` files use LF line endings (`.gitattributes` enforces it).

**PC layer (`Assets/Scripts/_Ours/PcPlatform`)** - PC input, display, saves and options live here, not inside game types, wherever
possible.

**Your own assets (`Assets/_Ours/`)** - the one tracked asset folder: original prefabs, UI, icons, materials or ScriptableObjects a
feature needs. Commit each asset with its `.meta`; put assets loaded by name at runtime in `Assets/_Ours/Resources/`. See
`Assets/_Ours/README.md`.

**Setup (`setup/`)** - every stage must stay resumable (running `setup.cmd` again continues where it stopped) and must give the same
result on every machine. Test a setup change on a fresh clone with a full `setup.cmd` run.

**Changing the setup for people who already ran it** (`setup/setup.py`):

- A change to the game's own exported assets (a scene, prefab, material or data asset) cannot ship as the edited file - each player
  has their own export. Ship it as code, or as a setup stage that applies the change to every player's export.
- A **new stage** runs on every existing setup at the player's next `setup.cmd` (it is not in their `SetupWork\state.json` yet), so
  it must also work on a finished project - see how `fix_arrays` finds its folder.
- A **changed stage** reaches existing setups only when you bump its number in `VERSIONS`. That stage then runs again on the finished
  project with `BDSP_STAGE_FRESH=1` (its own sub-steps start over; it may replace what it installed earlier), followed by `verify`.
  Only stages in `RERUNNABLE` may be bumped - add a stage there only after testing it on a finished project. Bumping any other stage
  makes `setup.cmd` stop and ask for a fresh setup.
- `setup.py --plan` prints what a run would do on a given `SetupWork\` without doing it.

**Before you open the pull request**

- The project compiles in Unity 2019.4.27f1 with no errors (Console clean after the scripts reload).
- The game still boots to the title screen and your change works in play mode.
- For anything that touches the build: `setup.cmd -Build` still produces a working `Dpr.exe`.
- Project settings you did not mean to change are not in the diff (in particular, the Sprite Packer mode stays **Disabled**).
