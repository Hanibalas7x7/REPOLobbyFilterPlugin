# Changelog

All notable changes to REPOLobbyFilter will be documented in this file.

## [1.1.0] - 2026-01-12

### Added
- **Manual Blocking Hotkey**: Press **B** or **F9** while hovering over a lobby to instantly block it
- **Hover Detection**: Plugin now correctly identifies which lobby you're hovering over in the UI
- **Separate Blocklists**: Auto-blocked and manually blocked lobbies are now stored in separate files
  - `REPOLobbyFilter_AutoBlocked.txt` - Lobbies detected by Cyrillic/keyword/region filters
  - `REPOLobbyFilter_ManualBlocked.txt` - Lobbies blocked via hotkey or manual file editing
- **Readable Blocklist Format**: Files now show both GUID and lobby name for easy management
  - Format: `GUID | Lobby Name`
  - Example: `550e8400-e29b-41d4-a716-446655440000 | Russian Server`
- **Configurable Hotkey**: Added config option to change the blocking hotkey (default: B)
- **Enhanced Logging**: Better log messages showing separate counts for auto/manual blocks
  - Example: `Blocked 8/24 lobbies (Auto: 422, Manual: 12, Total: 434)`

### Changed
- **Improved Blocking Logic**: Now uses `MenuElementHover.isHovering` to detect selected lobby
- **Better GUID Resolution**: Extracts room GUID directly from UI components instead of guessing by index
- **Backward Compatible**: Old blocklist format is automatically converted to new format
- **Save Logic**: Only saves blocklist when actually adding new entries (reduces disk I/O)

### Fixed
- **Hotkey Always Blocked First Lobby**: Now correctly blocks the lobby you're hovering over
- **Page Number Confusion**: Previously used page index instead of actual lobby selection
- **Rename Evasion**: GUID-based blocking prevents lobby owners from evading blocks by renaming

### Technical
- Added Harmony postfix patch to `MenuPageServerList.Update` for hotkey detection
- Implemented hover state monitoring through `MenuElementHover` component
- Added `autoBlocklistNames` and `manualBlocklistNames` dictionaries for name storage
- Optimized blocklist loading/saving with GUID | Name parsing

## [1.0.0] - Initial Release

### Added
- Automatic filtering of Russian/Belarusian lobbies
- Cyrillic character detection (Unicode U+0400 to U+04FF)
- Keyword filtering (RU, BY, RUS, RUSSIA, BELARUS)
- Region code filtering (Photon region detection)
- Persistent GUID-based blocklist
- Auto-save functionality for detected lobbies
- Configuration file with enable/disable toggle
- Harmony patching of `MenuPageServerList.OnRoomListUpdate`
