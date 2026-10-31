# Temnorid Caravan Guard

**Place the guards. Bring the wagons home.**

Defending trade caravans on dangerous roads. A [Temnorid](https://temnorid.com) game for **Windows**, built on the [IT Kamianets 3D Engine](https://3d.itkamianets.com) with Unity 6000.6.

**Status:** 0.2.0: the first playable with online co-op, a shared menu, phone controls and self-updating; the installer is not published yet. Website: https://caravan-guard.temnorid.com · Download: [Temnorid-CaravanGuard-Setup.exe](https://github.com/WebArtWork/caravan-guard.temnorid.com/releases/latest/download/Temnorid-CaravanGuard-Setup.exe)

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
| Esc or the Pause button | Pause menu (alone only) |

On a phone or tablet: **tap** a post to select it, **drag** one finger to move the map, **pinch** to zoom, and use the big Buy / Upgrade and Start wave buttons.

### Playing with others

Up to four people defend the same caravan together. On the first screen choose **Play with others**, then either **Host a game here** (friends on the same network type the address it shows) or **Host online** (friends type a short code; needs the game linked to Unity Cloud). The host starts the journey. Everybody shares one purse of coins and one caravan: anyone can place or upgrade a guard on any free post or start the next wave, and the host checks the cost and the post before it happens. Each person's camera is their own, and the others' pointers show on the map as coloured rings with their names. The **Archers** place lists the posts and a wave log. The host decides the win or the loss and tells everyone. A journey with others cannot be paused or retried; if the host leaves, the game ends for everybody and you return to the main menu.

### Updates

When a newer release is published on GitHub, the Windows game downloads and installs it by itself the next time it starts, and starts again. Turn it off in **Profile** if you prefer. On Android and iOS the update comes from the store or the release page.

### Phones

Android and iOS builds are made with Unity Build Profiles (File > Build Profiles, Android or iOS; the scene builder sets landscape, the application id and the version). The whole game plays by touch (tap, drag, pinch and on-screen buttons); the tabs of the Defence and Archers places and the Profile sit at the bottom.

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
- No hero, no second map, no free placement and no spearmen yet. Online play has no spectators, no joining after the start and no pause.

Everything is drawn with Unity primitives (blocks, capsules, spheres), simple colours and text. No models, no paid assets. Every visible object has a stable asset id (for example `resources/wood-node`) so real models can replace the placeholders without touching gameplay.

## For developers and agents

- **Unity:** 6000.6, Universal 3D (URP). Open the folder in Unity Hub. `Library/` is rebuilt locally and not committed.
- **Platform:** Windows (self-updating installer), Android and iOS builds from Unity Build Profiles. Mouse and keyboard, or touch. One map, local saves; up to four players online.
- **Starts from:** the IT Kamianets 3D Engine [God / Free Camera starter](AGENTS.md). Read [AGENTS.md](AGENTS.md) first for the engine packages and workflow.
- **Engine pieces this game uses:** `3d-unity-topdown-camera` (the overhead pan and zoom camera, new for this game); `3d-unity-combat` (health and damage); `3d-unity-feedback` (arrow tracers, health bars); `3d-unity-graybox` (scene building, asset ids); `3d-unity-ui` (screens); `3d-unity-save`; `3d-unity-input`.
- **Game code:** `Assets/Scripts`: `CaravanRules` (coins, guard and attacker stats, waves, records, with no Unity objects), `RoadPath`, `CaravanGame` (the journey), `Caravan`, `GuardPost`, `Enemy` and `EnemyFactory`, `CaravanHud`, `CaravanShell` (the shared shell's configuration), `CaravanNet` (online play), `CaravanTouchView` and `TouchGesture` (touch camera). The map is built by `Assets/Scripts/Editor/CaravanSceneBuilder.cs` (menu Temnorid > CaravanGuard > Build Scene); the Windows player by `CaravanReleaseBuilder` and `installer/CaravanGuard.iss`.
- **Balance:** `BalanceTests.Bots_play_whole_journeys` (explicit play-mode test) plays whole journeys with a bot that guards the next stopping point; it wins 6 of 6 with 23 to 82% caravan health left. A journey with no guards is lost.
- **Where code lives:**
  - `3d-*` packages: generic, engine-level behaviour with no Temnorid content (combat, AI, inventory, save, ...).
  - `temnorid-*` packages: Temnorid content shared by **several games** (for example `temnorid-character`). Never `temnorid-<game>-*`.
  - `Assets/Scripts` in this project: this game's own rules, scene builders, HUD and menus.
- **Packages while developing:** new and changed packages sit in the shared workspace folder next to the games and are referenced from `Packages/manifest.json` by local path. They move to their own repositories later, and only the paths change.
- **Level building:** the graybox level is made by an idempotent Editor builder, not by hand, so it can be rebuilt at any time.
- **Tests:** rules that can be tested without the Editor (scores, damage, inventory, laps, win conditions, saves) get edit-mode tests.
- **Online rules:** the host is the authority (`NetSession.IsAuthority` is true alone and on the host): waves, attackers, coins, the caravan's health and the win or loss all run only there, in `CaravanGame` and `CaravanRules.cs`, unchanged. `CaravanNet` publishes them about ten times a second as entities (id 0 state: coins, phase, waves cleared; ids 1 to 10 the posts' levels; id 500 the caravan; ids from 1000 the attackers) and shows each player's pointer as a pose. A client sends `cg.buy` / `cg.up` (post number) and `cg.wave`; the host validates and answers with `cg.msg`. A client's guards only draw tracers and do no damage; its attackers are pictures (`Enemy.Remote`). Single player is exactly as before. Test port: 7802 (`OnlineDefenceTests`).
- **Touch:** the overhead camera package has no touch route, so `CaravanTouchView` adds it in the game (drag, pinch, tap); `TouchGesture` is plain code with edit-mode tests.
- **Shell:** scenes `Boot`, `Menu`, `Main` (`ShellSceneKit` from `3d-unity-shell`); updates come from `3d-unity-update` in the Boot scene, with `Repo` set in `CaravanShell`.
- **Do not add:** Firebase, accounts, or any link to Lords in this version.

## Website and downloads

The `docs/` folder is this game's public website (`docs/index.html`, served at https://caravan-guard.temnorid.com through GitHub Pages; `docs/CNAME` holds the domain). It repeats the information in this README: what the game is, how to play, controls and the download button.

The download button points to the latest GitHub Release asset:

`https://github.com/WebArtWork/caravan-guard.temnorid.com/releases/latest/download/Temnorid-CaravanGuard-Setup.exe`

Release steps (when the first playable is ready):

1. Build the Windows player from Unity (File > Build Profiles > Windows).
2. Wrap the build in a Windows installer named `Temnorid-CaravanGuard-Setup.exe`.
3. Create a GitHub Release (tag `v0.2.0`) and attach that file.
4. Open the website, click the button, install, play, save, restart, uninstall.

When you change the game's description, controls or rules, update this README and `docs/index.html` together.

## License

See [LICENSE](LICENSE).
