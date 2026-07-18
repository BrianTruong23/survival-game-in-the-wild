# 3D Final Project Game Course

This repository contains my Unity 3D game. The game is a low-poly nature survival and crafting level where the player explores a forest terrain, gathers proton and electron particles, crafts them into coins, defeats wandering enemies, avoids hazards, and clears the level before a countdown timer runs out.

## Game Concept

The player is dropped into a low-poly forest environment and must gather **protons** and **electrons** scattered near their position, then **craft** those particles into coins. At the same time, wandering enemies must be shot and defeated, poison gas zones chip away at health, and a hard countdown clock keeps the pressure on. Weapons can be collected and equipped, a drivable vehicle lets the player move faster than sprinting, and NPCs plus a full HUD guide the player toward the objective.

## Main Objective / Goal

Clear the level before the timer hits zero by completing **both** goals:

- Craft **2 coins** (each coin = **1 proton + 1 electron**).
- Defeat **2 enemies**.

- **Win:** both goals completed before time runs out -> "LEVEL CLEAR" message, then the restart scene.
- **Lose:** health reaches zero, **or** the countdown timer reaches zero -> loss message, then the restart scene.

## Controls

- Move: `WASD` or left stick.
- Look / Camera: mouse or right stick.
- Jump: `Space`.
- Sprint: `Left Shift`.
- Shoot equipped weapon: `Left Click` or `F`.
- Equip / holster a gun from the chest: `1`-`5`.
- Open / close chest inventory: `Y`.
- Craft a coin (from 1 proton + 1 electron): `B`.
- Cycle compass target (Enemy / Proton / Electron / Vehicle): `U`.
- Enter / exit the nearby vehicle: `I`.
- Talk to a nearby NPC: `N`.
- Open / close the instructions & progress screen: `E`.

## Scenes

- `Assets/Scenes/StartScene.unity` - start menu with a play button.
- `Assets/Scenes/MainScene.unity` - main playable level.
- `Assets/Scenes/RestartScene.unity` - restart flow shown after winning or losing.

---

## Checkpoint 2 Requirements and How the Game Fulfills Them

Below is each Checkpoint 2 requirement and the specific part of the game that satisfies it.

**1. Keep or improve your playable 3D world / terrain / environment.**

- Low-poly forest built on Unity terrain with trees, grass details, props, a sky, real-time lighting, and a global post-processing volume (from the Low Poly Environment Nature Free asset). Improved with a **day/night cycle** (`DayNightManager`) that changes lighting and ambience over time.
  Replace the player Third Person Asset to Survivalist Player Asset.

**2. Keep or improve your controllable player and working camera system.**
Survivalist Character Asset with `WASD` movement, mouse look, jump, and sprint, followed by a Cinemachine third-person camera. Improved with a **dedicated driving camera**: while in the vehicle (`VehicleController`), the game takes direct control of the main camera to follow the car at a tunable distance, with mouse-look and ground-slope handling.

**3. Add at least five interactive / collectible / usable objects.**

- The level has more than five: **protons** (`ProtonCollectible`), **electrons** (`ElectronCollectible`), **weapon pickups** (5 guns: 3 revolvers + 2 shotguns, `GunPickup`), **enemies** (`WanderAI` / `EnemyDamage`), **NPCs** (`NpcDialogue`), a **drivable vehicle** (`VehicleController`), and **poison gas hazard zones** (`PoisonGasZone`).

**4. Add at least three interaction systems.**

- Collect protons/electrons (walk-over pickup), **craft** particles into coins (`B`), pick up and **equip/holster** weapons (`1`-`5`, `Y`), **shoot** enemies (`Left Click`/`F`), **talk to NPCs** (`N`), **enter/drive** the vehicle (`I`), and **cycle the compass target** (`U`).

**5. Add item / resource / score / progress tracking.**

- `GameScoreManager` tracks protons collected, electrons collected, coins crafted, enemies defeated, and the remaining time. An inventory system tracks the unique guns acquired in the chest, and `PlayerHealth` tracks health.

**6. Add clear UI feedback.**

- A runtime HUD shows coin progress (`x/2`), enemy progress (`x/2`), current weapon, chest count, health icons, a countdown timer, a compass/nearest-target marker, interaction prompts, crafting/dialogue status messages, a chest inventory panel, and an instructions + live-progress screen (`E`).

**7. Add at least three challenge systems.**

- More than three: **wandering enemies** that deal damage on contact (`WanderAI`, `EnemyDamage`), **poison gas hazard zones** that chip health while standing in them (`PoisonGasZone`), **countdown timer pressure** (loss on reaching zero), a **resource requirement** (must gather enough protons + electrons before a coin can be crafted), and **enemy respawn** so the threat is continuous.

**8. Add a clear gameplay goal.**

- Craft 2 coins (1 proton + 1 electron each) **and** defeat 2 enemies **before the countdown timer runs out** (10 minutes). The goal and live progress are shown on the HUD and the instructions screen.

**9. Add a win condition, lose condition, and completion / restart flow.**

- Win when both goals are met (`CheckWinCondition`); lose when health hits zero (`PlayerHealth`) or the timer expires (`TriggerTimeUp`). Each outcome shows a status message and loads `RestartScene`, which links back into the play flow via `StartScene`.

**10. Add or improve audio.**

- `DayNightManager` plays looping **background music** plus **day and night forest ambience** that cross-fade with the cycle. `RuntimeSfx` synthesizes **sound effects** in code (proton/electron pickup chimes, enemy-defeat thud) and `PlayerGunController` plays a **gunshot** sound, so every core interaction has audio feedback.

**11. Add basic visual polish.**

- Real-time lighting, a sky, a post-processing volume, low-poly imported nature assets, terrain grass/detail, color-coded proton (orange) and electron (blue) particles, styled UI panels and inventory slots, a compass/minimap marker, and the day/night lighting transition.

**12. Testable from start to finish without major errors.**

- The game runs `StartScene` -> `MainScene` -> `RestartScene`. Core objects are spawned/wired at runtime by `GameScoreManager` so the level is playable end to end: gather particles, craft coins, defeat enemies, and reach a win or loss state.

---

## What Makes This Game Unique

Beyond the base requirements, this project adds several distinctive features:

- **Custom player asset** — the default Starter Assets third-person character was replaced with the **Survivalist** character asset, giving the player a unique look and rig.
- **NPC conversations** — the player can walk up to NPCs and talk to them for guidance (`N`).
- **Environmental hazard** — **poison gas zones** actively harm the player, chipping away health while they stand inside one.
- **Objective-driven win condition** — completing the goal (crafting the required coins and defeating the required enemies) triggers the win state.
- **Drivable vehicle** — the player can interact with a vehicle, enter it, and **drive it around** the world (`I`), moving faster than sprinting, with its own follow camera.
- **Combat** — the player can pick up guns and **shoot enemies**.

### Advanced Feature (Grad Student Requirement)

- **Object combination / crafting** — the player can **combine two different objects (protons + electrons) into a new object (a coin)**. This crafting mechanic is the core progression loop of the game and is what drives the win condition.

## What Changed Since Checkpoint 1

- Replaced flat coin collection with a **proton/electron -> coin crafting** system (gather particles, then press `B` to craft).
- Added a **countdown timer** as a hard loss condition (timer pressure).
- Added **poison gas hazard zones** that damage the player.
- Added a **drivable vehicle** with its own follow-camera system, ground-slope handling, and mouse-look.
- Added a **day/night cycle** with background music and cross-fading ambience.
- Added a **compass** that cycles between the nearest enemy, proton, electron, and vehicle (`U`).
- Expanded the HUD (timer, craft status, compass, richer instructions/progress screen).

## Known Issues / Notes

- The project is a prototype, so enemy AI and combat feedback are intentionally simple.
- Several gameplay objects (particles, guns, enemies, vehicle, hazards) are spawned by scripts at runtime when `MainScene` starts.
- UI uses Unity legacy `Text` components for fast implementation.
- The win/lose flow shows a status message and then loads the restart scene after a short delay, rather than a fully polished end screen.

## Included Audio

Located in `Assets/Audio` (ambience and music); sound effects for pickups, enemy defeats, and gunshots are generated procedurally at runtime by `RuntimeSfx` / `PlayerGunController`.

- `leberch-atmospheric-documentary-509386.mp3`
- `capaholiczsfx-forest-daytime-446356.mp3`
- `eryliaa-night-forest-with-frogs-and-crickets-for-sleep-451153.mp3`

## External Assets and Resources

- Low Poly Environment Nature Free:
  `https://assetstore.unity.com/packages/3d/environments/low-poly-environment-nature-free-lowpoly-medieval-fantasy-series-187052`
- Starter Assets Third Person URP:
  `https://assetstore.unity.com/packages/essentials/starter-assets-thirdperson-urp-196526`
- Imported placeholder weapon models in `Assets/Collectibles`.
- Imported placeholder enemy / NPC / animal models in `Assets/Enemies`, `Assets/NPC`, and `Assets/Animals`.

## Repository

GitHub repository:

`https://github.com/BrianTruong23/3d-final-project-game-course`
