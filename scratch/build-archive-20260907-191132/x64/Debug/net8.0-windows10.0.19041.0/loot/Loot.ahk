#NoEnv
#SingleInstance force
#Persistent
SetBatchLines -1
SendMode Input
#NoTrayIcon
SetTitleMatchMode, 2

; ==================== CONFIGURATION ====================
Global IniFile := "config.ini"
Global GameWindowTitle := "PUBG: BATTLEGROUNDS"
Global FAST_LOOT_DELAY := 1

; Coordinates Variables
Global up_item_posX, up_item_posY, up2_item_posX, up2_item_posY, up3_item_posX, up3_item_posY
Global down_item_posX, down_item_posY, down2_item_posX, down2_item_posY, down3_item_posX, down3_item_posY
Global inv_posX, inv_posY

Global FastLootActive := 0

; ==================== INITIALIZATION ====================
; Check for Admin privileges
if not A_IsAdmin
{
    Run *RunAs "%A_ScriptFullPath%"
    ExitApp
}

ReadConfig()

; Initialize Hotkey
Hotkey, MButton, FastLoot, On
return

; ==================== FUNCTIONS ====================
ReadConfig() {
    Global
    IniPath := A_ScriptDir . "\" . IniFile
    IfNotExist, %IniPath%
    {
        MsgBox, 16, Lỗi, Không tìm thấy file %IniPath%!
        ExitApp
    }
    
    IniRead, up_item_posX, %IniPath%, Coordinates, up_item_posX, 0
    IniRead, up_item_posY, %IniPath%, Coordinates, up_item_posY, 0
    IniRead, up2_item_posX, %IniPath%, Coordinates, up2_item_posX, 0
    IniRead, up2_item_posY, %IniPath%, Coordinates, up2_item_posY, 0
    IniRead, up3_item_posX, %IniPath%, Coordinates, up3_item_posX, 0
    IniRead, up3_item_posY, %IniPath%, Coordinates, up3_item_posY, 0
    
    IniRead, down_item_posX, %IniPath%, Coordinates, down_item_posX, 0
    IniRead, down_item_posY, %IniPath%, Coordinates, down_item_posY, 0
    IniRead, down2_item_posX, %IniPath%, Coordinates, down2_item_posX, 0
    IniRead, down2_item_posY, %IniPath%, Coordinates, down2_item_posY, 0
    IniRead, down3_item_posX, %IniPath%, Coordinates, down3_item_posX, 0
    IniRead, down3_item_posY, %IniPath%, Coordinates, down3_item_posY, 0
    
    IniRead, inv_posX, %IniPath%, Coordinates, inv_posX, 0
    IniRead, inv_posY, %IniPath%, Coordinates, inv_posY, 0
    
    ; Debug: Uncomment below to verify coordinates if needed
    ; MsgBox, Coord Check: %up_item_posX%, %up_item_posY% - %inv_posX%, %inv_posY%
}

; ==================== FAST LOOT LOGIC ====================
FastLoot:
    if (FastLootActive)
        return

    ; Safety guard: abort if other mouse buttons are held
    if (GetKeyState("LButton", "P") or GetKeyState("RButton", "P") or GetKeyState("XButton1", "P") or GetKeyState("XButton2", "P"))
        return
    
    ; Check if game is active
    IfWinNotActive, %GameWindowTitle%
    {
        ; Pass through if not in game
        Send, {MButton Down}
        KeyWait, MButton
        Send, {MButton Up}
        return
    }
    
    FastLootActive := 1
    
    ; Disable hotkey
    Hotkey, MButton, Off
    
    ; Open Inventory
    SendInput {Tab}
    Sleep, %FAST_LOOT_DELAY%
    
    ; Drag Items
    MouseClickDrag, Left, up_item_posX, up_item_posY, inv_posX, inv_posY, 0
    Sleep, 5
    MouseClickDrag, Left, up2_item_posX, up2_item_posY, inv_posX, inv_posY, 0
    Sleep, 5
    MouseClickDrag, Left, up3_item_posX, up3_item_posY, inv_posX, inv_posY, 0
    Sleep, 5
    MouseClickDrag, Left, down_item_posX, down_item_posY, inv_posX, inv_posY, 0
    Sleep, 5
    MouseClickDrag, Left, down2_item_posX, down2_item_posY, inv_posX, inv_posY, 0
    Sleep, 5
    MouseClickDrag, Left, down3_item_posX, down3_item_posY, inv_posX, inv_posY, 0
    Sleep, 5
    
    ; Close Inventory
    SendInput {Tab}
    
    Sleep, 10
    
    ; Re-enable hotkey
    Hotkey, MButton, On
    FastLootActive := 0
return

; Emergency Exit
F10::ExitApp
