# RE9_CustomCameraFOV

## Description
Custom camera FOV mod / plugin for Resident Evil Requiem (RE9). Set custom FOV for 3rd person, 1st person, ADS, and more.

## Prerequisites
- REFramework and the REFramework C# API (csharp-api) [GitHub REFramework-nightly releases](https://github.com/praydog/REFramework-nightly/releases)

## Installation
1. Install prerequisites (download BOTH the `RE9.zip` AND `csharp-api` and extract to game folder)
2. Download the plugin and extract to game folder
3. The first startup after installing the `csharp-api` might take a while. Wait until it is complete
4. Open the REFramework UI -> `REFramework.NET script generated UI` -> RE9_CustomCameraFOV -> change FOV and settings

## Features
- Change FOV for 1st and 3rd person separately
- Change ADS FOV for 1st and 3rd person separately
  - Two different ADS FOV modes
    - Fixed ADS FOV
	- Zoom in same percentage as the game based on the configured (not ADS) FOV
- Cutscenes are not affected
- Interactions and in game events are not affected (e.g. the FOV will zoom in when inspecting things)
- Settings are saved to config file and automatically loaded

## Known issues
- The UI is distorted/warped
  - More noticeable the higher you set the FOV. Changing the Graphics -> Ultrawide fixes and changing some things related to UI and FOV might help (enabling the FOV options there might override this mods changes)

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