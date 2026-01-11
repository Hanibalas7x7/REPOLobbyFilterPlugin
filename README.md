# R.E.P.O. Lobby Filter

A BepInEx plugin for R.E.P.O. that automatically filters Russian and Belarusian lobbies from the server browser.

## Features

- **Automatic Cyrillic Detection** - Blocks lobbies with Russian/Belarusian characters
- **Keyword Filtering** - Blocks lobbies containing RU, BY, RUS, RUSSIA, BELARUS, (RUS), (RU), (BY)
- **Region Filtering** - Blocks lobbies from Russia/Belarus based on Photon region data
- **Persistent Blocklists** - Separate auto-blocked and manual blocklists with readable format
- **Hotkey Blocking** - Press **B** or **F9** to instantly block the lobby you're hovering over
- **GUID-Based Blocking** - Prevents rename evasion by blocking lobby GUIDs permanently
- **Readable Format** - Blocklist files show both GUID and lobby name for easy management
- **Minimal Logging** - Clean console output showing blocked count and separate list sizes

## Installation

1. **Install BepInEx** (if not already installed):
   - Download BepInEx 5.4.23 from https://github.com/BepInEx/BepInEx/releases
   - Extract to your R.E.P.O. game folder
   - Run the game once to generate BepInEx folders

2. **Install the plugin**:
   - Copy `REPOLobbyFilter.dll` to `R.E.P.O\BepInEx\plugins\`
   - Launch the game

3. **Done!** - The plugin starts filtering automatically

## Usage

### Automatic Filtering
The plugin automatically filters lobbies based on Cyrillic characters, keywords, and region codes.

### Manual Blocking via Hotkey
1. In the server browser, **hover your mouse** over any lobby you want to block
2. Press **B** or **F9** while hovering
3. The lobby will be added to your manual blocklist

**Note:** You must be hovering over a lobby when pressing the hotkey. The hovered lobby is highlighted in the UI.

## How It Works

The plugin intercepts the server list before it's displayed and removes lobbies that match any of these criteria:

1. **Auto-Blocklist** - Lobby GUID is in the auto-blocked list (detected by criteria 3-5)
2. **Manual Blocklist** - Lobby GUID is in the manual list (added via B/F9 hotkey or file edit)
3. **Cyrillic Characters** - Lobby name contains Russian/Belarusian alphabet (U+0400 to U+04FF)
4. **Keywords** - Lobby name contains: RU, BY, RUS, RUSSIA, BELARUS, (RUS), (RU), (BY)
5. **Region Code** - Photon region is RU, BY, Russia, or Belarus

When a lobby matches criteria 3-5, its GUID is automatically saved to `REPOLobbyFilter_AutoBlocked.txt`.
When you press B/F9, the hovered lobby's GUID is saved to `REPOLobbyFilter_ManualBlocked.txt`.

## Configuration

The plugin creates a config file at:
`R.E.P.O\BepInEx\config\com.repolobbyfilter.repolobbyfilter.cfg`

```ini
[Filter]
# Enable or disable filtering (default: true)
EnableFilter = true

[Hotkey]
# Key to manually block currently hovered lobby (default: B)
BlockKey = B
```

You can change `BlockKey` to any Unity KeyCode (e.g., `F9`, `Delete`, `X`, etc.)

## Blocklist Files

The plugin creates two separate blocklist files:

### Auto-Blocked Lobbies
`R.E.P.O\BepInEx\config\REPOLobbyFilter_AutoBlocked.txt`

Contains lobbies automatically detected by Cyrillic/keyword/region filters.

### Manually Blocked Lobbies
`R.E.P.O\BepInEx\config\REPOLobbyFilter_ManualBlocked.txt`

Contains lobbies you blocked via the B/F9 hotkey or manual file editing.

### File Format
Both files use the same readable format:
```
550e8400-e29b-41d4-a716-446655440000 | My Server Name
8f6a0abb-8169-4398-a341-945038ff9b40 | GERMAN +18
```

Each line contains:
- **GUID** (used for blocking) - Permanent, prevents rename evasion
- **Name** (for reference) - Helps you identify what was blocked

You can manually edit these files to add/remove entries. Only the GUID portion is used for blocking.

## Console Output

Typical log messages:
```
[Info   :REPOLobbyFilter] Loaded 422 auto-blocked lobbies
[Info   :REPOLobbyFilter] Loaded 12 manually blocked lobbies
[Info   :REPOLobbyFilter] Lobby filter ready! Blocked lobbies will be auto-saved.
[Info   :REPOLobbyFilter] Press [B] or [F9] to manually block currently displayed lobby
[Info   :REPOLobbyFilter] Auto-blocked lobbies: F:\..\REPOLobbyFilter_AutoBlocked.txt
[Info   :REPOLobbyFilter] Manual blocklist: F:\..\REPOLobbyFilter_ManualBlocked.txt
[Info   :REPOLobbyFilter] Blocked 8/24 lobbies (Auto: 422, Manual: 12, Total: 434)
[Info   :REPOLobbyFilter] ✅ Auto-blocked: "Русский Сервер" (GUID: 550e8400-...)
[Warning:REPOLobbyFilter] ⛔ MANUALLY BLOCKED: "GERMAN +18"
```

## Building from Source

1. Install .NET SDK 6.0 or later
2. Clone/download this repository
3. Edit `REPOLobbyFilter.csproj` and update the `REPOPath` property to your game folder
4. Run: `dotnet build`
5. Copy `bin\Debug\net46\REPOLobbyFilter.dll` to `BepInEx\plugins\`

## Troubleshooting

**Plugin not loading:**
- Check `BepInEx\LogOutput.log` for errors
- Ensure BepInEx is properly installed
- Verify .dll is in the `plugins` folder

**Still seeing unwanted lobbies:**
- Hover over the lobby and press **B** or **F9** to block it manually
- Check if lobby name uses special characters or formats
- Report the lobby name so keyword detection can be improved
- Edit the manual blocklist file to add the lobby GUID directly

**Hotkey not working:**
- Make sure you're **hovering** over a lobby when pressing the key
- Check `BepInEx\LogOutput.log` for "KEY PRESSED IN UPDATE PATCH" message
- Try changing the hotkey in the config file to a different key

**Too many lobbies blocked:**
- Set `EnableFilter = false` in the config file
- Or edit the blocklist file to remove entries

## Technical Details

- **Framework:** BepInEx 5.4.23.4, .NET Framework 4.6
- **Patching:** Harmony 2.x patches:
  - Prefix on `MenuPageServerList.OnRoomListUpdate` (lobby filtering)
  - Postfix on `MenuPageServerList.Update` (hotkey detection)
- **Hover Detection:** Monitors `MenuElementHover.isHovering` to identify selected lobby
- **GUID Resolution:** Extracts room GUID from `MenuElementServer.roomName` field
- **Detection:** Unicode range check, keyword matching, Photon region detection
- **Storage:** Two plain text files (auto/manual) with HashSet for O(1) lookup
- **Format:** `GUID | Name` format for human readability while maintaining GUID-based blocking

## License

MIT License - Free to use and modify
