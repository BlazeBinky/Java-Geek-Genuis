# MoonDrop Meadows

**A pixel-art life sandbox where one little town can become an entire lifetime.**

MoonDrop Meadows is a 2D top-down life simulator. Farming is one possible path, not the whole game. Start as a child, teenager, or adult; meet the town; go to school; find work; build a company; date, marry, raise children, and watch the people and places around you change.

The first playable slice is an asset-light MonoGame project. Its pixel scenery, characters, and 5x7 UI font are drawn in code, so it can run before a large art pipeline exists.

## Current prototype

- Character setup with name, starting age, and optional sibling.
- A small town map with home, school, shops, cafe, mayor's office, newspaper stand, business center, and Bob's tower.
- Keyboard movement and nearby interactions.
- Daily clock, seasons, yearly birthdays, energy, money, and local mail.
- Crop loop: plant, water, wait, harvest.
- General store and clothing shop.
- School attendance and career selection.
- Town computer with mail, news, jobs, business tools, land listings, and savings.
- Friendship, gifts, dates, partnership, marriage, and family save-data foundations.
- First mystery quest involving Bob the Wizard, three Moon Petals, and MAYORLARRY.
- Optional WAV voice-line support is planned. The prototype is silent by default.

This is an early foundation, not a finished Stardew-sized game. The design documents describe the larger world and its expansion plan.

## Build

Requirements: .NET 10 SDK.

From this folder run:

    dotnet restore
    dotnet run

Or build a release:

    dotnet build -c Release

The project currently targets MonoGame DesktopGL for Windows, macOS, and Linux. Mobile and console builds need their own platform projects and packaging. Xbox builds require approved developer access. Windows 10 Mobile is an unsupported legacy platform, so it is not promised as a native target.

## Controls

| Key | Action |
|---|---|
| WASD / Arrow keys | Walk |
| E | Talk, collect, or use a nearby place |
| F | Use a farm plot |
| G | Gather a Moon Petal during the quest |
| C | Open the home computer |
| Up / Down + Enter | Choose a dialogue or menu option |
| 1-9 | Quick-select a menu option |
| Escape | Close a panel |
| F5 | Save |

On the character screen, use Up/Down to choose an age, F to toggle a sibling, N to edit your name, and Enter to begin.

## Save data

Saves are stored in the user's local application-data folder under MoonDropMeadows. The game is designed to work offline. No account, server, API key, or network connection is required.

## Project map

- Core/Models.cs: save models for characters, relationships, crops, children, and businesses.
- UI/PixelFont.cs: built-in pixel font.
- Game1.cs: current playable prototype.
- DESIGN_BIBLE.md: life-sandbox systems and town design.
- STORY_ARCS.md: long-form story plan for Bob, MAYORLARRY, and the Moonwell.
- VOICE_ACTING.md: optional recorded-voice workflow.
- ROADMAP.md: staged expansion plan.

MoonDrop Meadows is an original setting with its own town, characters, story, and systems.
