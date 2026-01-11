using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace REPOLobbyFilter
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private static Plugin Instance;
        private Harmony _harmony;
        public ConfigEntry<bool> enableFilter;
        public ConfigEntry<KeyCode> blockHotkey;
        private static HashSet<string> autoBlocklist = new HashSet<string>(); // Auto-blocked lobbies (Cyrillic, RU/BY region)
        private static HashSet<string> manualBlocklist = new HashSet<string>(); // Manually blocked lobbies (B/F9 key)
        private static Dictionary<string, string> autoBlocklistNames = new Dictionary<string, string>(); // GUID -> Name mapping
        private static Dictionary<string, string> manualBlocklistNames = new Dictionary<string, string>(); // GUID -> Name mapping
        private static string autoBlocklistPath;
        private static string manualBlocklistPath;
        private static Dictionary<string, object> currentRooms = new Dictionary<string, object>(); // Store rooms by name

        void Awake()
        {
            Instance = this;
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} v{PluginInfo.PLUGIN_VERSION} loaded!");
            
            // Load configuration
            enableFilter = Config.Bind("Filter", "EnableFilter", true, "Enable Russian/Belarusian lobby filtering");
            blockHotkey = Config.Bind("Hotkey", "BlockKey", KeyCode.B, "Press this key to manually block the currently displayed lobby");
            
            // Setup blocklists
            autoBlocklistPath = System.IO.Path.Combine(Paths.ConfigPath, "REPOLobbyFilter_AutoBlocked.txt");
            manualBlocklistPath = System.IO.Path.Combine(Paths.ConfigPath, "REPOLobbyFilter_ManualBlocked.txt");
            LoadBlocklists();
            
            // Apply Harmony patches
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            PatchMenuPageServerList();
            PatchMenuPageServerListUpdate();
            
            Logger.LogInfo("Lobby filter ready! Blocked lobbies will be auto-saved.");
            Logger.LogInfo($"Press [B] or [F9] to manually block currently displayed lobby");
            Logger.LogInfo($"Auto-blocked lobbies: {autoBlocklistPath}");
            Logger.LogInfo($"Manual blocklist: {manualBlocklistPath}");
        }

        void PatchMenuPageServerListUpdate()
        {
            var type = AccessTools.TypeByName("MenuPageServerList");
            if (type == null) return;

            // Try to patch Update method
            var updateMethod = AccessTools.Method(type, "Update");
            if (updateMethod != null)
            {
                var postfix = new HarmonyMethod(typeof(Plugin).GetMethod(nameof(MenuPageServerList_Update_Postfix), 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
                _harmony.Patch(updateMethod, postfix: postfix);
                Logger.LogInfo("Patched MenuPageServerList.Update for hotkey detection");
            }
            else
            {
                Logger.LogWarning("MenuPageServerList has no Update method");
            }
        }

        static void MenuPageServerList_Update_Postfix(object __instance)
        {
            // Check for hotkey
            if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.F9))
            {
                Instance.Logger.LogInfo("KEY PRESSED IN UPDATE PATCH!");
                Instance.TryBlockCurrentLobby(__instance);
            }
        }

        public void TryBlockCurrentLobby(object menuPageInstance)
        {
            try
            {
                Logger.LogInfo("TryBlockCurrentLobby started");
                
                var type = menuPageInstance.GetType();
                var roomListField = AccessTools.Field(type, "roomList");
                var serverElementParentField = AccessTools.Field(type, "serverElementParent");
                
                if (roomListField == null || serverElementParentField == null)
                {
                    Logger.LogError("Could not find required fields");
                    return;
                }

                var roomList = roomListField.GetValue(menuPageInstance) as IList;
                var serverElementParent = serverElementParentField.GetValue(menuPageInstance) as Transform;
                
                Logger.LogInfo($"roomList.Count = {roomList?.Count ?? 0}");
                
                if (serverElementParent == null)
                {
                    Logger.LogError("serverElementParent is null");
                    return;
                }
                
                // Look through child UI elements to find which one is selected/hovered
                string hoveredRoomGuid = null;
                Logger.LogInfo($"Checking {serverElementParent.childCount} server UI elements...");
                
                for (int i = 0; i < serverElementParent.childCount; i++)
                {
                    var child = serverElementParent.GetChild(i);
                    if (child == null || !child.gameObject.activeInHierarchy) continue;
                    
                    // Get MenuElementHover component to check if this element is hovered
                    var hoverComp = child.GetComponent(AccessTools.TypeByName("MenuElementHover"));
                    if (hoverComp != null)
                    {
                        var isHoveringField = AccessTools.Field(hoverComp.GetType(), "isHovering");
                        if (isHoveringField != null)
                        {
                            bool isHovering = (bool)isHoveringField.GetValue(hoverComp);
                            
                            if (isHovering)
                            {
                                // This is the hovered element! Get the room GUID from MenuElementServer
                                var serverComp = child.GetComponent(AccessTools.TypeByName("MenuElementServer"));
                                if (serverComp != null)
                                {
                                    var roomNameField = AccessTools.Field(serverComp.GetType(), "roomName");
                                    if (roomNameField != null)
                                    {
                                        hoveredRoomGuid = roomNameField.GetValue(serverComp)?.ToString();
                                        Logger.LogInfo($"Found hovered element at UI index {i}, roomGUID = {hoveredRoomGuid}");
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(hoveredRoomGuid))
                {
                    Logger.LogWarning("No hovered lobby found!");
                    return;
                }
                
                // Now find this room in the roomList to get its display name
                if (roomList == null)
                {
                    Logger.LogError("roomList is null");
                    return;
                }
                
                object targetRoom = null;
                foreach (var room in roomList)
                {
                    if (room == null) continue;
                    var roomNameField = AccessTools.Field(room.GetType(), "roomName");
                    string roomGuid = roomNameField?.GetValue(room)?.ToString();
                    if (roomGuid == hoveredRoomGuid)
                    {
                        targetRoom = room;
                        break;
                    }
                }
                
                if (targetRoom == null)
                {
                    Logger.LogError($"Could not find room with GUID {hoveredRoomGuid} in roomList");
                    return;
                }
                
                // Get display name from ServerListRoom
                string displayName = "Unknown";
                var displayNameField = AccessTools.Field(targetRoom.GetType(), "displayName");
                if (displayNameField != null)
                {
                    displayName = displayNameField.GetValue(targetRoom)?.ToString() ?? "Unknown";
                }
                
                Logger.LogInfo($"Blocking hovered lobby: \"{displayName}\" (GUID: {hoveredRoomGuid})");
                Logger.LogInfo($"Blocking hovered lobby: \"{displayName}\" (GUID: {hoveredRoomGuid})");
                
                AddToManualBlocklist(hoveredRoomGuid, displayName);
                Logger.LogWarning($"⛔ MANUALLY BLOCKED: \"{displayName}\"");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to block lobby: {ex.Message}");
            }
        }

        void LoadBlocklists()
        {
            try
            {
                // Load auto-blocked lobbies
                if (System.IO.File.Exists(autoBlocklistPath))
                {
                    var lines = System.IO.File.ReadAllLines(autoBlocklistPath);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            // Parse "GUID | Name" format, but only use GUID for blocking
                            var parts = line.Split(new[] { " | " }, StringSplitOptions.None);
                            var guid = parts[0].Trim();
                            if (!string.IsNullOrEmpty(guid))
                            {
                                autoBlocklist.Add(guid);
                                if (parts.Length > 1)
                                {
                                    autoBlocklistNames[guid] = parts[1].Trim();
                                }
                            }
                        }
                    }
                    Logger.LogInfo($"Loaded {autoBlocklist.Count} auto-blocked lobbies");
                }
                
                // Load manually blocked lobbies
                if (System.IO.File.Exists(manualBlocklistPath))
                {
                    var lines = System.IO.File.ReadAllLines(manualBlocklistPath);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            // Parse "GUID | Name" format, but only use GUID for blocking
                            var parts = line.Split(new[] { " | " }, StringSplitOptions.None);
                            var guid = parts[0].Trim();
                            if (!string.IsNullOrEmpty(guid))
                            {
                                manualBlocklist.Add(guid);
                                if (parts.Length > 1)
                                {
                                    manualBlocklistNames[guid] = parts[1].Trim();
                                }
                            }
                        }
                    }
                    Logger.LogInfo($"Loaded {manualBlocklist.Count} manually blocked lobbies");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading blocklists: {ex.Message}");
            }
        }

        void SaveAutoBlocklist()
        {
            try
            {
                var lines = new List<string>();
                foreach (var guid in autoBlocklist)
                {
                    if (autoBlocklistNames.ContainsKey(guid))
                    {
                        lines.Add($"{guid} | {autoBlocklistNames[guid]}");
                    }
                    else
                    {
                        lines.Add(guid);
                    }
                }
                System.IO.File.WriteAllLines(autoBlocklistPath, lines);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving auto-blocklist: {ex.Message}");
            }
        }
        
        void SaveManualBlocklist()
        {
            try
            {
                var lines = new List<string>();
                foreach (var guid in manualBlocklist)
                {
                    if (manualBlocklistNames.ContainsKey(guid))
                    {
                        lines.Add($"{guid} | {manualBlocklistNames[guid]}");
                    }
                    else
                    {
                        lines.Add(guid);
                    }
                }
                System.IO.File.WriteAllLines(manualBlocklistPath, lines);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving manual blocklist: {ex.Message}");
            }
        }

        public static void AddToAutoBlocklist(string identifier, string displayName = null)
        {
            if (autoBlocklist.Add(identifier))
            {
                if (!string.IsNullOrEmpty(displayName))
                {
                    autoBlocklistNames[identifier] = displayName;
                    Instance.Logger.LogInfo($"✅ Auto-blocked: \"{displayName}\" (GUID: {identifier})");
                }
                Instance.SaveAutoBlocklist();
            }
        }
        
        public static void AddToManualBlocklist(string identifier, string displayName = null)
        {
            if (manualBlocklist.Add(identifier))
            {
                if (!string.IsNullOrEmpty(displayName))
                {
                    manualBlocklistNames[identifier] = displayName;
                    Instance.Logger.LogInfo($"✅ Manually blocked: \"{displayName}\" (GUID: {identifier})");
                }
                Instance.SaveManualBlocklist();
            }
        }

        void PatchMenuPageServerList()
        {
            var type = AccessTools.TypeByName("MenuPageServerList");
            if (type == null)
            {
                Logger.LogError("MenuPageServerList type not found!");
                return;
            }

            var method = AccessTools.Method(type, "OnRoomListUpdate");
            if (method == null)
            {
                Logger.LogError("OnRoomListUpdate method not found!");
                return;
            }

            var prefix = new HarmonyMethod(typeof(Plugin).GetMethod(nameof(OnRoomListUpdate_Prefix), 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));

            _harmony.Patch(method, prefix: prefix);
            
            Logger.LogInfo("Patched OnRoomListUpdate");
        }

        static void OnRoomListUpdate_Prefix(object _roomList)
        {
            if (!Instance.enableFilter.Value) return;
            
            var list = _roomList as IList;
            if (list == null || list.Count == 0) return;

            int originalCount = list.Count;
            int removedCount = 0;
            
            // Store all rooms by name for hotkey lookup
            currentRooms.Clear();
            foreach (var room in list)
            {
                if (room == null) continue;
                var nameField = AccessTools.Property(room.GetType(), "Name");
                string lobbyName = nameField?.GetValue(room)?.ToString();
                if (!string.IsNullOrEmpty(lobbyName))
                {
                    currentRooms[lobbyName] = room;
                }
            }

            // Filter lobbies in reverse order
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var room = list[i];
                if (room == null) continue;

                if (ShouldBlockLobby(room))
                {
                    list.RemoveAt(i);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                int totalBlocked = autoBlocklist.Count + manualBlocklist.Count;
                Instance.Logger.LogInfo($"Blocked {removedCount}/{originalCount} lobbies (Auto: {autoBlocklist.Count}, Manual: {manualBlocklist.Count}, Total: {totalBlocked})");
            }
        }

        static bool ShouldBlockLobby(object room)
        {
            var roomType = room.GetType();
            
            // Get room GUID (Name property)
            var nameProperty = roomType.GetProperty("Name");
            string roomGuid = nameProperty?.GetValue(room)?.ToString();
            
            // Check if GUID is already blocked (either auto or manual)
            if (!string.IsNullOrEmpty(roomGuid))
            {
                if (autoBlocklist.Contains(roomGuid) || manualBlocklist.Contains(roomGuid))
                {
                    return true;
                }
            }
            
            var customPropsProperty = roomType.GetProperty("CustomProperties");
            if (customPropsProperty != null)
            {
                var customProps = customPropsProperty.GetValue(room) as IDictionary;
                if (customProps != null)
                {
                    string serverName = null;
                    if (customProps.Contains("server_name"))
                    {
                        serverName = customProps["server_name"]?.ToString();
                        
                        // Check if server name is already blocked (either auto or manual)
                        if (!string.IsNullOrEmpty(serverName))
                        {
                            if (autoBlocklist.Contains(serverName) || manualBlocklist.Contains(serverName))
                            {
                                return true;
                            }
                        }
                        
                        // Check for Cyrillic - if found, auto-add to auto-blocklist
                        if (!string.IsNullOrEmpty(serverName) && ContainsCyrillic(serverName))
                        {
                            // Save GUID to auto-blocklist (prevents rename evasion)
                            if (!string.IsNullOrEmpty(roomGuid))
                            {
                                AddToAutoBlocklist(roomGuid, serverName);
                            }
                            return true;
                        }
                        
                        // Check if lobby name contains RU, BY, Russia, Belarus keywords
                        if (!string.IsNullOrEmpty(serverName))
                        {
                            var upperName = serverName.ToUpper();
                            if (upperName.Contains(" RU ") || upperName.StartsWith("RU ") || upperName.EndsWith(" RU") ||
                                upperName.Contains(" BY ") || upperName.StartsWith("BY ") || upperName.EndsWith(" BY") ||
                                upperName.Contains("(RU)") || upperName.Contains("(RUS)") || upperName.Contains("(BY)") ||
                                upperName.Contains(" RUS ") || upperName.Contains(" RUS)") || upperName.Contains("(RUS ") ||
                                upperName.Contains("RUSSIA") || upperName.Contains("BELARUS") ||
                                upperName.Contains("РУСС") || upperName.Contains("РУС"))
                            {
                                if (!string.IsNullOrEmpty(roomGuid))
                                {
                                    AddToAutoBlocklist(roomGuid, serverName);
                                }
                                return true;
                            }
                        }
                    }
                    
                    // Check region code - if RU/BY, auto-add to blocklist
                    if (customProps.Contains("Region"))
                    {
                        string region = customProps["Region"]?.ToString() ?? "";
                        
                        if (region.Equals("RU", StringComparison.OrdinalIgnoreCase) || 
                            region.Equals("BY", StringComparison.OrdinalIgnoreCase) ||
                            region.IndexOf("Russia", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            region.IndexOf("Belarus", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // Save to auto-blocklist for future
                            if (!string.IsNullOrEmpty(roomGuid))
                            {
                                string name = customProps.Contains("server_name") ? customProps["server_name"]?.ToString() : null;
                                AddToAutoBlocklist(roomGuid, name);
                            }
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        static bool ContainsCyrillic(string text)
        {
            foreach (char c in text)
            {
                if (c >= '\u0400' && c <= '\u04FF')
                {
                    return true;
                }
            }
            return false;
        }
    }
}
