# RE9_CustomCameraFOV

## Description
Custom camera FOV mod / plugin for RE9. Set different FOV for third person, first person, and ADS. Can also disable ADS zoom and choose different FOV scaling types (set exact values, or scale FOV with game)

## Dependencies
- REFrameworkNETPluginConfig https://github.com/TonWonton/REFrameworkNETPluginConfig

## Installation
### Lua
1. Install REFramework
  - NexusMods: https://www.nexusmods.com/residentevilrequiem/mods/13
  - GitHub: https://github.com/praydog/REFramework-nightly/releases
2. Download the lua script and extract to game folder
  - `RE9_CustomCameraFOV.lua` should be in `\GAME_FOLDER\reframework\autorun\RE9_CustomCameraFOV.lua`

### C#
1. Install prerequisites
  - REFramework + REFramework csharp-api (download and extract both `RE9.zip` AND `csharp-api.zip` to the game folder): https://github.com/praydog/REFramework-nightly/releases
    - Only extract `dinput8.dll` from the `RE9.zip`
  - .NET 10.0 Desktop Runtime x64: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
2. Download the plugin and extract to game folder
  - `RE9_CustomCameraFOV.dll` should be in `\GAME_FOLDER\reframework\plugins\managed\RE9_CustomCameraFOV.dll`

- If the `csharp-api` is installed correctly a CMD window will pop up when launching the game
- The first startup after installing the `csharp-api` might take a while. Wait until it is complete. When the game isn't frozen anymore and it says "setting up script watcher" it is done
- The mod settings are under `REFramework.NET script generated UI` instead of the normal `Script generated UI`

## Features
- Change FOV for 1st and 3rd person separately
- Change ADS FOV for 1st and 3rd person separately
- Disable ADS zoom / FOV change
- Two different ADS FOV modes
  - Exact ADS FOV
  - Zoom in same percentage as the game based on the configured (not ADS) FOV
- Can change scope base FOV multiplier
- Scope is rendered at full quality/resolution
  - Improves performance compared to the base game when aiming with the scope
- FOV zoom speed is unchanged (exact same speed as the game for all settings)
- Cutscenes are not affected
- Interactions and in game events are not affected
- Settings are saved to config file and automatically loaded

## Changelog
### v1.0.0
- Initial release

### v1.1.0
- Add enable toggle
- Add option to use original FOV for specific actions
- Changes to fixed ADS FOV calculation
- Potential fix for various fixed ADS FOV issues

### v1.2.0
- Fix UI being distorted
- Fix interaction and inspection being too far zoomed in or out
  - It will now always be the original FOV
    - Remove previous two original FOV toggles

### v1.2.1
- Fix FOV not applying after cutscenes
- Fix FOV value being inaccurate during some parts of the game
- Fix fixed ADS FOV mode affecting the normal FOV

### v1.3.0
- Add option to use exact FOV instead of scaling together with game
- Potential fix for crash and FOV sometimes not applying

### v1.4.0
- Add option to disable ADS zoom / ADS FOV change
- Add option to set exact FOV for normal look FOV and ADS FOV separately

### v1.4.1
- Fix red dot sights not being affected by FOV settings

### v1.5.0
- Add option to change scope base zoom multiplier
- Fix the scope being low resolution
- Fix the FOV being wrong for red dot sights sometimes when not using disabled FPS ADS FOV or FPS force exact ADS FOV

### v1.6.0
- Fix FOV being higher or lower depending on character or situation if exact FOV wasn't enabled
  - Normal look FOV is now always exact
- Removed normal look exact FOV option
- Removed fixed ADS FOV
- Fix red dot sight FOV scaling

### v1.6.1
- Add lua version
- Potential fix for sometimes crashing when changing config
