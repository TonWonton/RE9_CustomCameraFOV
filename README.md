# RE9_CustomCameraFOV

## Description
Custom camera FOV mod / plugin for Resident Evil Requiem (RE9). Set custom FOV for 3rd person, 1st person, ADS, and more.

## Prerequisites
- REFramework and the REFramework C# API (csharp-api) https://github.com/praydog/REFramework-nightly/releases
- .NET 10.0 Desktop Runtime https://dotnet.microsoft.com/en-us/download/dotnet/10.0

## Installation
1. Install prerequisites (download BOTH the `RE9.zip` AND `csharp-api` and extract to game folder) and install the .NET 10.0 Desktop Runtime if you don't have it installed
2. Download the plugin and extract to game folder
3. The first startup after installing the `csharp-api` might take a while. Wait until it is complete. When the game isn't frozen anymore and it says "setting up script watcher" it is done
4. Open the REFramework UI -> `REFramework.NET script generated UI` -> RE9_CustomCameraFOV -> change FOV and settings

## Features
- Change FOV for 1st and 3rd person separately
- Change ADS FOV for 1st and 3rd person separately
  - Two different ADS FOV modes
    - Fixed ADS FOV
    - Zoom in same percentage as the game based on the configured (not ADS) FOV
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