using Aimmy2.Class;
using Other;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace InputLogic
{
    /// <summary>
    /// Manages Fast Loot functionality - automatically drags items from 
    /// configured screen positions into the inventory area.
    /// Replaces external Loot.exe (AutoHotkey) with native C# implementation.
    /// </summary>
    public static class LootManager
    {
        #region Win32 API Imports

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint MOUSEEVENTF_MOVE = 0x0001;

        private const byte VK_TAB = 0x09;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        #endregion

        #region State

        private static volatile bool _isLooting = false;
        private const int ACTION_DRAG = 1;
        private const int AHK_FAST_LOOT_OPEN_DELAY_MS = 1;
        private const int AHK_FAST_LOOT_ITEM_DELAY_MS = 5;

        /// <summary>
        /// Display names for each coordinate pair (used in UI for backward compatibility)
        /// </summary>
        public static readonly string[] PositionNames = new[]
        {
            "Item 1 (Up)",
            "Item 2 (Up2)",
            "Item 3 (Up3)",
            "Item 4 (Down)",
            "Item 5 (Down2)",
            "Item 6 (Down3)"
        };

        #endregion

        #region Public Methods

        public static void StartExternalLootProcess()
        {
            try
            {
                StopExternalLootProcesses();

                string lootDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "loot");
                string lootPath = Path.Combine(lootDirectory, "Loot.exe");

                if (!File.Exists(lootPath))
                {
                    LogManager.Log(LogManager.LogLevel.Warning, $"Loot.exe not found: {lootPath}", true, 3000);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = lootPath,
                    WorkingDirectory = lootDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"Failed to start Loot.exe: {ex.Message}", true, 3000);
            }
        }

        public static void StopExternalLootProcesses()
        {
            try
            {
                RunTaskKill("Loot.exe");
                RunTaskKill("_cache_Loot*.exe");
                RunTaskKill("._cache_Loot*.exe");

                foreach (var process in Process.GetProcesses().Where(IsLootProcess))
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(1000);
                    }
                    catch
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"Failed to stop Loot.exe: {ex.Message}");
            }
        }

        private static void RunTaskKill(string imageName)
        {
            try
            {
                using var taskKill = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = $"/F /T /IM \"{imageName}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                taskKill?.WaitForExit(1500);
            }
            catch
            {
            }
        }

        private static bool IsLootProcess(Process process)
        {
            try
            {
                string processName = process.ProcessName;
                return string.Equals(processName, "Loot", StringComparison.OrdinalIgnoreCase) ||
                       processName.StartsWith("_cache_Loot", StringComparison.OrdinalIgnoreCase) ||
                       processName.StartsWith("._cache_Loot", StringComparison.OrdinalIgnoreCase) ||
                       processName.Contains("cache_Loot", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Clears all dynamic loot item keys from the settings dictionary before loading a new configuration.
        /// </summary>
        public static void PrepareForConfigLoad()
        {
            var keysToRemove = Dictionary.sliderSettings.Keys
                .Where(k => k.StartsWith("Loot Item ") || k == "Fast Loot Item Count")
                .ToList();
            foreach (var key in keysToRemove)
            {
                Dictionary.sliderSettings.Remove(key);
            }
        }

        /// <summary>
        /// Ensures all dynamic loot item settings are initialized after a configuration is loaded.
        /// </summary>
        public static void PostConfigLoad()
        {
            if (!Dictionary.sliderSettings.ContainsKey("Fast Loot Item Count"))
            {
                Dictionary.sliderSettings["Fast Loot Item Count"] = 6.0;
            }

            int itemCount = (int)Convert.ToDouble(Dictionary.sliderSettings["Fast Loot Item Count"]);
            for (int i = 1; i <= itemCount; i++)
            {
                if (!Dictionary.sliderSettings.ContainsKey($"Loot Item {i} X"))
                    Dictionary.sliderSettings[$"Loot Item {i} X"] = 0.0;
                
                if (!Dictionary.sliderSettings.ContainsKey($"Loot Item {i} Y"))
                    Dictionary.sliderSettings[$"Loot Item {i} Y"] = 0.0;
                
                if (!Dictionary.sliderSettings.ContainsKey($"Loot Item {i} Action"))
                    Dictionary.sliderSettings[$"Loot Item {i} Action"] = (double)ACTION_DRAG;
            }

            ConvertLegacyAlternatingActionsToDrag(itemCount);

            // Ensure inventory is set
            if (!Dictionary.sliderSettings.ContainsKey("Loot Inventory X"))
                Dictionary.sliderSettings["Loot Inventory X"] = 0.0;
            if (!Dictionary.sliderSettings.ContainsKey("Loot Inventory Y"))
                Dictionary.sliderSettings["Loot Inventory Y"] = 0.0;
        }

        /// <summary>
        /// Executes the Fast Loot sequence:
        /// 1. Save current cursor position
        /// 2. Press Tab to open inventory
        /// 3. Loot dynamic items by either right-clicking or dragging to inventory target position
        /// 4. Press Tab to close inventory
        /// 5. Restore cursor position
        /// </summary>
        public static void ExecuteFastLoot()
        {
            if (_isLooting) return;
            if (!Dictionary.toggleState.ContainsKey("Fast Loot") || !Dictionary.toggleState["Fast Loot"]) return;

            _isLooting = true;

            try
            {
                // Get inventory target position
                int invX = GetCoord("Loot Inventory X");
                int invY = GetCoord("Loot Inventory Y");

                // Press Tab to open inventory
                PressKey(VK_TAB);
                Thread.Sleep(AHK_FAST_LOOT_OPEN_DELAY_MS);

                // Get item count
                int itemCount = 6;
                if (Dictionary.sliderSettings.ContainsKey("Fast Loot Item Count"))
                {
                    itemCount = (int)Convert.ToDouble(Dictionary.sliderSettings["Fast Loot Item Count"]);
                }

                ConvertLegacyAlternatingActionsToDrag(itemCount);

                // Match loot/Loot.ahk: always drag each configured item to the inventory slot.
                for (int i = 0; i < itemCount; i++)
                {
                    int itemNum = i + 1;
                    int itemX = GetCoord($"Loot Item {itemNum} X");
                    int itemY = GetCoord($"Loot Item {itemNum} Y");

                    // Skip if coordinates are 0 (not configured)
                    if (itemX == 0 && itemY == 0) continue;

                    MouseClickDrag(itemX, itemY, invX, invY);
                    Thread.Sleep(AHK_FAST_LOOT_ITEM_DELAY_MS);
                }

                // Press Tab to close inventory
                PressKey(VK_TAB);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"Fast Loot error: {ex.Message}");
            }
            finally
            {
                _isLooting = false;
            }
        }

        /// <summary>
        /// Loads default coordinates from existing config.ini if available.
        /// </summary>
        public static void LoadDefaultsFromIni()
        {
            try
            {
                string iniPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "loot", "config.ini");
                if (!System.IO.File.Exists(iniPath)) return;

                var lines = System.IO.File.ReadAllLines(iniPath);
                var iniValues = new System.Collections.Generic.Dictionary<string, int>();

                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int val))
                    {
                        iniValues[parts[0].Trim()] = val;
                    }
                }

                // Map INI keys to our coordinate keys
                var mapping = new (string iniKey, string lootKey)[]
                {
                    ("up_item_posX", "Loot Item 1 X"), ("up_item_posY", "Loot Item 1 Y"),
                    ("up2_item_posX", "Loot Item 2 X"), ("up2_item_posY", "Loot Item 2 Y"),
                    ("up3_item_posX", "Loot Item 3 X"), ("up3_item_posY", "Loot Item 3 Y"),
                    ("down_item_posX", "Loot Item 4 X"), ("down_item_posY", "Loot Item 4 Y"),
                    ("down2_item_posX", "Loot Item 5 X"), ("down2_item_posY", "Loot Item 5 Y"),
                    ("down3_item_posX", "Loot Item 6 X"), ("down3_item_posY", "Loot Item 6 Y"),
                    ("inv_posX", "Loot Inventory X"), ("inv_posY", "Loot Inventory Y")
                };

                foreach (var (iniKey, lootKey) in mapping)
                {
                    if (iniValues.TryGetValue(iniKey, out int val))
                    {
                        // Only set if not already configured (don't override saved config)
                        if (!Dictionary.sliderSettings.ContainsKey(lootKey) ||
                            Convert.ToDouble(Dictionary.sliderSettings[lootKey]) == 0)
                        {
                            Dictionary.sliderSettings[lootKey] = (double)val;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"Failed to load loot defaults from INI: {ex.Message}");
            }
        }

        #endregion

        #region Private Helpers

        private static int GetCoord(string key)
        {
            if (Dictionary.sliderSettings.ContainsKey(key))
            {
                return (int)Convert.ToDouble(Dictionary.sliderSettings[key]);
            }
            return 0;
        }

        private static void ConvertLegacyAlternatingActionsToDrag(int itemCount)
        {
            if (itemCount <= 0)
                return;

            bool matchesOddRightClick = true;
            bool matchesOddDrag = true;

            for (int i = 1; i <= itemCount; i++)
            {
                string actionKey = $"Loot Item {i} Action";
                if (!Dictionary.sliderSettings.ContainsKey(actionKey))
                    return;

                int action = (int)Convert.ToDouble(Dictionary.sliderSettings[actionKey]);
                int oddRightClickPattern = i % 2 == 1 ? 0 : ACTION_DRAG;
                int oddDragPattern = i % 2 == 1 ? ACTION_DRAG : 0;

                matchesOddRightClick &= action == oddRightClickPattern;
                matchesOddDrag &= action == oddDragPattern;
            }

            if (!matchesOddRightClick && !matchesOddDrag)
                return;

            for (int i = 1; i <= itemCount; i++)
            {
                Dictionary.sliderSettings[$"Loot Item {i} Action"] = (double)ACTION_DRAG;
            }
        }

        /// <summary>
        /// Simulates a mouse click-drag from (startX, startY) to (endX, endY).
        /// Uses absolute screen coordinates with SendInput.
        /// </summary>
        private static void MouseClickDrag(int startX, int startY, int endX, int endY)
        {
            // Move cursor to start position
            SetCursorPos(startX, startY);

            // Press left button down
            var downInput = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dwFlags = MOUSEEVENTF_LEFTDOWN
                }
            };
            SendInput(1, new[] { downInput }, Marshal.SizeOf(typeof(INPUT)));

            // Move to end position
            SetCursorPos(endX, endY);

            // Release left button
            var upInput = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dwFlags = MOUSEEVENTF_LEFTUP
                }
            };
            SendInput(1, new[] { upInput }, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Simulates a mouse right click at (x, y).
        /// Uses absolute screen coordinates with SendInput.
        /// </summary>
        private static void MouseRightClick(int x, int y)
        {
            // Move cursor to position
            SetCursorPos(x, y);
            Thread.Sleep(20);

            // Press right button down
            var downInput = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dwFlags = MOUSEEVENTF_RIGHTDOWN
                }
            };
            SendInput(1, new[] { downInput }, Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(30);

            // Release right button
            var upInput = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dwFlags = MOUSEEVENTF_RIGHTUP
                }
            };
            SendInput(1, new[] { upInput }, Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(20);
        }

        /// <summary>
        /// Presses and releases a keyboard key using keybd_event.
        /// </summary>
        private static void PressKey(byte vkCode)
        {
            keybd_event(vkCode, 0, 0, UIntPtr.Zero);
            Thread.Sleep(1);
            keybd_event(vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        #endregion
    }
}
