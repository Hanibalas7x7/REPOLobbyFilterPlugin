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
        private ConfigEntry<bool> enableFilter;
        private static HashSet<string> manualBlocklist = new HashSet<string>(); // Stores both names and GUIDs
        private static string blocklistPath;

        void Awake()
        {
            Instance = this;
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} v{PluginInfo.PLUGIN_VERSION} loaded!");
            
            // Load configuration
            enableFilter = Config.Bind("Filter", "EnableFilter", true, "Enable Russian/Belarusian lobby filtering");
            
            // Setup manual blocklist
            blocklistPath = System.IO.Path.Combine(Paths.ConfigPath, "REPOLobbyFilter_Blocklist.txt");
            LoadBlocklist();
            
            // Apply Harmony patches
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            PatchMenuPageServerList();
            Logger.LogInfo("Lobby filter ready! Blocked lobbies will be auto-saved.");
            Logger.LogInfo($"To manually block: Edit {blocklistPath}");
        }

        void LoadBlocklist()
        {
            try
            {
                if (System.IO.File.Exists(blocklistPath))
                {
                    var lines = System.IO.File.ReadAllLines(blocklistPath);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            manualBlocklist.Add(line.Trim());
                        }
                    }
                    Logger.LogInfo($"Loaded {manualBlocklist.Count} blocked lobbies from file");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading blocklist: {ex.Message}");
            }
        }

        void SaveBlocklist()
        {
            try
            {
                System.IO.File.WriteAllLines(blocklistPath, manualBlocklist);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving blocklist: {ex.Message}");
            }
        }

        public static void AddToBlocklist(string identifier, string displayName = null)
        {
            if (manualBlocklist.Add(identifier))
            {
                Instance.SaveBlocklist();
                if (!string.IsNullOrEmpty(displayName))
                {
                    Instance.Logger.LogInfo($"✅ Auto-saved to blocklist: \"{displayName}\" (GUID: {identifier})");
                }
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
                Instance.Logger.LogInfo($"Blocked {removedCount}/{originalCount} lobbies (Total in blocklist: {manualBlocklist.Count})");
            }
        }

        static bool ShouldBlockLobby(object room)
        {
            var roomType = room.GetType();
            
            // Get room GUID (Name property)
            var nameProperty = roomType.GetProperty("Name");
            string roomGuid = nameProperty?.GetValue(room)?.ToString();
            
            // Check if GUID is already blocked
            if (!string.IsNullOrEmpty(roomGuid) && manualBlocklist.Contains(roomGuid))
            {
                return true;
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
                        
                        // Check if server name is already blocked
                        if (!string.IsNullOrEmpty(serverName) && manualBlocklist.Contains(serverName))
                        {
                            return true;
                        }
                        
                        // Check for Cyrillic - if found, auto-add to blocklist
                        if (!string.IsNullOrEmpty(serverName) && ContainsCyrillic(serverName))
                        {
                            // Save GUID to blocklist (prevents rename evasion)
                            if (!string.IsNullOrEmpty(roomGuid))
                            {
                                AddToBlocklist(roomGuid, serverName);
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
                                    AddToBlocklist(roomGuid, serverName);
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
                            // Save to blocklist for future
                            if (!string.IsNullOrEmpty(roomGuid))
                            {
                                string name = customProps.Contains("server_name") ? customProps["server_name"]?.ToString() : null;
                                AddToBlocklist(roomGuid, name);
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
