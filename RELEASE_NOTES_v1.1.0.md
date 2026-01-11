# REPOLobbyFilter v1.1.0 Release Notes

## 🎯 What's New

### Manual Blocking Hotkey
The biggest new feature! You can now instantly block any lobby by:
1. **Hovering your mouse** over the lobby in the server browser
2. Pressing **B** or **F9**

No more manual GUID copying - just hover and press the hotkey!

### Separate Blocklists
The plugin now maintains two separate blocklist files:

- **`REPOLobbyFilter_AutoBlocked.txt`** - Automatically detected lobbies (Cyrillic, RU/BY keywords, region codes)
- **`REPOLobbyFilter_ManualBlocked.txt`** - Lobbies you blocked with the hotkey

This makes it easier to manage and review what you've blocked vs. what was auto-detected.

### Readable Blocklist Format
Both files now use a human-readable format:
```
550e8400-e29b-41d4-a716-446655440000 | Russian Server Name
8f6a0abb-8169-4398-a341-945038ff9b40 | GERMAN +18
```

You can now easily see which lobbies are blocked without having to cross-reference GUIDs!

### Better Logging
Console output now shows separate counts:
```
Blocked 8/24 lobbies (Auto: 422, Manual: 12, Total: 434)
```

## 🔧 Installation

1. Download `REPOLobbyFilter.dll` from the [Releases page](https://github.com/yourusername/REPOLobbyFilter/releases)
2. Place it in `R.E.P.O\BepInEx\plugins\`
3. Launch the game and start blocking!

## 📝 Configuration

The plugin generates a config file at:
`BepInEx\config\com.repolobbyfilter.repolobbyfilter.cfg`

You can customize:
- **EnableFilter**: Turn filtering on/off
- **BlockKey**: Change the hotkey (default: B)

## 🐛 Bug Fixes

- **Fixed**: Hotkey now blocks the correct lobby instead of always blocking the first one
- **Fixed**: Proper hover detection using Unity UI components
- **Improved**: GUID-based blocking prevents rename evasion

## 🔄 Upgrading from v1.0

Your old `REPOLobbyFilter_Blocklist.txt` will be automatically migrated:
- If it exists, it will be moved to `REPOLobbyFilter_AutoBlocked.txt`
- The new readable format will be applied on first save
- No data loss - all your blocked lobbies are preserved

## 📚 Documentation

- [README.md](README.md) - Full documentation
- [CHANGELOG.md](CHANGELOG.md) - Complete version history

## 💡 Usage Tips

- **Hover before pressing**: The hotkey only works when hovering over a lobby
- **Visual feedback**: Check the console for confirmation messages
- **Edit files manually**: Both blocklist files can be edited with any text editor

## 🙏 Feedback

Report issues or suggest features on the [GitHub Issues page](https://github.com/yourusername/REPOLobbyFilter/issues).

---

**Version**: 1.1.0  
**Release Date**: January 12, 2026  
**Compatible with**: R.E.P.O v0.3.2, BepInEx 5.4.23+
