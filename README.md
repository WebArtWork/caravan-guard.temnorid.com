# Temnorid Caravan Guard

**Place the guards. Bring the wagons home.**

Defending trade caravans on dangerous roads. A [Temnorid](https://temnorid.com) game for **Windows**, built on the [IT Kamianets 3D Engine](https://3d.itkamianets.com) with Unity 6000.6.

**Status:** first playable (0.1.0) built and tested; the installer is not published yet. Website: https://caravan-guard.temnorid.com · Download: [Temnorid-CaravanGuard-Setup.exe](https://github.com/WebArtWork/caravan-guard.temnorid.com/releases/latest/download/Temnorid-CaravanGuard-Setup.exe)

## What it is

A trade caravan sets out along a Temnorid road, and raiders, steppe riders and hungry wolves lie in wait. Place guards along the route, upgrade the ones that matter, and bring the wagons home.

The more goods arrive, the busier trade becomes in your city in Lords. A lost caravan means empty stalls.

## The first playable

The first playable is one road, ten guard posts and three waves of attackers. You are a commander, not a fighter: you play from above and only buy and upgrade guards.

### How a session goes

1. You begin with 180 coins and a caravan of three wagons waiting at the start of the road.
2. Click a free guard post beside the road and buy an archer for it (50 coins).
3. Start a wave. The caravan moves on toward the next stopping point and attackers come for it from the sides; your archers shoot anyone in range.
4. Every defeated attacker pays coins, and every cleared wave pays a bonus of 40. Spend them on new guards ahead of the caravan and on upgrading guards (40 coins: more damage, longer reach, faster shots).
5. The caravan waits at each stopping point until the wave is dead. Clear all three waves and it reaches the market gate at the end of the road.

The caravan only moves while a wave is on, so guards left behind no longer help: each wave needs guards along the next stretch of road.

| Attackers | Wave 1 | Wave 2 | Wave 3 |
| --- | --- | --- | --- |
| Raiders (slow, tough) | 8 | 8 | 10 |
| Wolves (fast, frail) | | 6 | 6 |
| Steppe riders (fast, strong) | | | 4 |

### Controls

| Key | Action |
| --- | --- |
| W A S D or the mouse at the screen edge | Move the camera |
| Mouse wheel | Zoom |
| Left mouse | Select a guard post |
| E or the Buy / Upgrade button | Buy a guard on the selected post, or upgrade it |
| Space or the Start wave button | Start the next wave |
| Esc | Pause menu |

### Win and lose

- **Win:** Bring the caravan to the end of the road after the final wave.
- **Lose:** The caravan (500 health) is destroyed. Retry restarts the journey.
- **Saved on your PC:** journeys played, journeys won, the most waves cleared and the most caravan health left after a win.

### The scene

One winding road about 164 m long across simple terrain, a caravan of three wagons, ten fixed guard posts beside the road and ten red-flagged spawn points where attackers appear. Attackers walk straight to the nearest wagon; guards never move. Seen from above.

## How it connects to Lords

Goods that arrive will raise market income and bring more merchants to your city in Lords.

This is for a later phase: [Lords](https://lords.temnorid.com) is the central strategy game and every Temnorid game will be connected to it. **This version is fully offline and does not talk to Lords**; progress is saved locally on the player's PC. Keep identifiers and save data simple and stable so the connection is cheap to add later.

## Scope of this version

- One road, ten fixed guard posts, one guard type (the archer) with one upgrade, three waves, win or lose.
- No hero, no second map, no free placement and no spearmen yet.

Everything is drawn with Unity primitives (blocks, capsules, spheres), simple colours and text. No models, no paid assets. Every visible object has a stable asset id (for example `resources/wood-node`) so real models can replace the placeholders without touching gameplay.

## For developers and agents

- **Unity:** 6000.6, Universal 3D (URP). Open the folder in Unity Hub. `Library/` is rebuilt locally and not committed.
- **Platform:** Windows only. Keyboard and mouse. One map, offline, local saves.
- **Starts from:** the IT Kamianets 3D Engine [God / Free Camera starter](AGENTS.md). Read [AGENTS.md](AGENTS.md) first for the engine packages and workflow.
- **Engine pieces this game uses:** `3d-unity-topdown-camera` (the overhead pan and zoom camera, new for this game); `3d-unity-combat` (health and damage); `3d-unity-feedback` (arrow tracers, health bars); `3d-unity-graybox` (scene building, asset ids); `3d-unity-ui` (screens); `3d-unity-save`; `3d-unity-input`.
- **Game code:** `Assets/Scripts`: `CaravanRules` (coins, guard and attacker stats, waves, records, with no Unity objects), `RoadPath`, `CaravanGame` (the journey), `Caravan`, `GuardPost`, `Enemy` and `EnemyFactory`, `CaravanHud`. The map is built by `Assets/Scripts/Editor/CaravanSceneBuilder.cs` (menu Temnorid > CaravanGuard > Build Scene); the Windows player by `CaravanReleaseBuilder` and `installer/CaravanGuard.iss`.
- **Balance:** `BalanceTests.Bots_play_whole_journeys` (explicit play-mode test) plays whole journeys with a bot that guards the next stopping point; it wins 6 of 6 with 23 to 82% caravan health left. A journey with no guards is lost.
- **Where code lives:**
  - `3d-*` packages: generic, engine-level behaviour with no Temnorid content (combat, AI, inventory, save, ...).
  - `temnorid-*` packages: Temnorid content shared by **several games** (for example `temnorid-character`). Never `temnorid-<game>-*`.
  - `Assets/Scripts` in this project: this game's own rules, scene builders, HUD and menus.
- **Packages while developing:** new and changed packages sit in the shared workspace folder next to the games and are referenced from `Packages/manifest.json` by local path. They move to their own repositories later, and only the paths change.
- **Level building:** the graybox level is made by an idempotent Editor builder, not by hand, so it can be rebuilt at any time.
- **Tests:** rules that can be tested without the Editor (scores, damage, inventory, laps, win conditions, saves) get edit-mode tests.
- **Do not add:** networking, Firebase, accounts, or any link to Lords in this version.

## Website and downloads

The `docs/` folder is this game's public website (`docs/index.html`, served at https://caravan-guard.temnorid.com through GitHub Pages; `docs/CNAME` holds the domain). It repeats the information in this README: what the game is, how to play, controls and the download button.

The download button points to the latest GitHub Release asset:

`https://github.com/WebArtWork/caravan-guard.temnorid.com/releases/latest/download/Temnorid-CaravanGuard-Setup.exe`

Release steps (when the first playable is ready):

1. Build the Windows player from Unity (File > Build Profiles > Windows).
2. Wrap the build in a Windows installer named `Temnorid-CaravanGuard-Setup.exe`.
3. Create a GitHub Release (tag `v0.1.0`) and attach that file.
4. Open the website, click the button, install, play, save, restart, uninstall.

When you change the game's description, controls or rules, update this README and `docs/index.html` together.

## License

See [LICENSE](LICENSE).
