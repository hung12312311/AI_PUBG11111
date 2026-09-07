#NoEnv
SendMode Input
SetWorkingDir %A_ScriptDir%
#SingleInstance force
SetTitleMatchMode, 2
#ifwinactive, PLAYERUNKNOWN'S BATTLEGROUNDS

; --- Biến toàn cục để lưu trữ tọa độ ---
up_item_posX := 0
up_item_posY := 0
up2_item_posX := 0
up2_item_posY := 0
up3_item_posX := 0
up3_item_posY := 0
down_item_posX := 0
down_item_posY := 0
down2_item_posX := 0
down2_item_posY := 0
down3_item_posX := 0
down3_item_posY := 0
inv_posX := 0
inv_posY := 0

; --- Hàm để tải tọa độ từ config.ini ---
LoadCoordinates()
{
    global
    IniRead, up_item_posX, config.ini, Coordinates, up_item_posX, 0
    IniRead, up_item_posY, config.ini, Coordinates, up_item_posY, 0
    IniRead, up2_item_posX, config.ini, Coordinates, up2_item_posX, 0
    IniRead, up2_item_posY, config.ini, Coordinates, up2_item_posY, 0
    IniRead, up3_item_posX, config.ini, Coordinates, up3_item_posX, 0
    IniRead, up3_item_posY, config.ini, Coordinates, up3_item_posY, 0
    IniRead, down_item_posX, config.ini, Coordinates, down_item_posX, 0
    IniRead, down_item_posY, config.ini, Coordinates, down_item_posY, 0
    IniRead, down2_item_posX, config.ini, Coordinates, down2_item_posX, 0
    IniRead, down2_item_posY, config.ini, Coordinates, down2_item_posY, 0
    IniRead, down3_item_posX, config.ini, Coordinates, down3_item_posX, 0
    IniRead, down3_item_posY, config.ini, Coordinates, down3_item_posY, 0
    IniRead, inv_posX, config.ini, Coordinates, inv_posX, 0
    IniRead, inv_posY, config.ini, Coordinates, inv_posY, 0
}

; --- Tải tọa độ ban đầu khi script khởi động ---
LoadCoordinates()

; --- Tạo GUI cải tiến ---
Gui, +AlwaysOnTop -SysMenu +Border ; Thêm AlwaysOnTop, bỏ menu hệ thống, giữ viền
Gui, Color, 2E2E2E, 404040 ; Màu nền tối (đen xám) và màu điều khiển
Gui, Font, s11 cFFFFFF, Segoe UI ; Font hiện đại, kích thước 11, màu trắng
Gui, Add, Text, x20 y20 w300 h30 Center, Cấu hình Tọa độ MacroLoot ; Tiêu đề căn giữa

; --- Nhóm các mục với GroupBox ---
Gui, Font, s10 cD0D0D0 ; Font cho nhãn, màu xám nhạt
Gui, Add, GroupBox, x10 y50 w280 h290, Tọa độ các Item
Gui, Add, Text, x20 y80 w100 h25, Item 1 (Up):
Gui, Add, Text, x130 y80 w120 h25 vItem1Coords, X: %up_item_posX% Y: %up_item_posY%
Gui, Add, Button, x250 y80 w30 h25 gSetItem1, Set
Gui, Add, Text, x20 y110 w100 h25, Item 2 (Up2):
Gui, Add, Text, x130 y110 w120 h25 vItem2Coords, X: %up2_item_posX% Y: %up2_item_posY%
Gui, Add, Button, x250 y110 w30 h25 gSetItem2, Set
Gui, Add, Text, x20 y140 w100 h25, Item 3 (Up3):
Gui, Add, Text, x130 y140 w120 h25 vItem3Coords, X: %up3_item_posX% Y: %up3_item_posY%
Gui, Add, Button, x250 y140 w30 h25 gSetItem3, Set
Gui, Add, Text, x20 y170 w100 h25, Item 4 (Down):
Gui, Add, Text, x130 y170 w120 h25 vItem4Coords, X: %down_item_posX% Y: %down_item_posY%
Gui, Add, Button, x250 y170 w30 h25 gSetItem4, Set
Gui, Add, Text, x20 y200 w100 h25, Item 5 (Down2):
Gui, Add, Text, x130 y200 w120 h25 vItem5Coords, X: %down2_item_posX% Y: %down2_item_posY%
Gui, Add, Button, x250 y200 w30 h25 gSetItem5, Set
Gui, Add, Text, x20 y230 w100 h25, Item 6 (Down3):
Gui, Add, Text, x130 y230 w120 h25 vItem6Coords, X: %down3_item_posX% Y: %down3_item_posY%
Gui, Add, Button, x250 y230 w30 h25 gSetItem6, Set
Gui, Add, Text, x20 y260 w100 h25, Inventory:
Gui, Add, Text, x130 y260 w120 h25 vInvCoords, X: %inv_posX% Y: %inv_posY%
Gui, Add, Button, x250 y260 w30 h25 gSetInventory, Set

; --- Nút Đóng và Lưu ---
Gui, Font, s10 cFFFFFF, Segoe UI
Gui, Add, Button, x80 y350 w70 h30 gGuiClose, Đóng
Gui, Add, Button, x160 y350 w70 h30 gSaveConfig, Lưu
Gui, Show, w300 h390, Cấu hình MacroLoot ; Kích thước GUI cố định
return

; --- Xử lý sự kiện GUI ---
GuiClose:
ButtonĐóng:
ExitApp

SaveConfig:
    IniWrite, %up_item_posX%, config.ini, Coordinates, up_item_posX
    IniWrite, %up_item_posY%, config.ini, Coordinates, up_item_posY
    IniWrite, %up2_item_posX%, config.ini, Coordinates, up2_item_posX
    IniWrite, %up2_item_posY%, config.ini, Coordinates, up2_item_posY
    IniWrite, %up3_item_posX%, config.ini, Coordinates, up3_item_posX
    IniWrite, %up3_item_posY%, config.ini, Coordinates, up3_item_posY
    IniWrite, %down_item_posX%, config.ini, Coordinates, down_item_posX
    IniWrite, %down_item_posY%, config.ini, Coordinates, down_item_posY
    IniWrite, %down2_item_posX%, config.ini, Coordinates, down2_item_posX
    IniWrite, %down2_item_posY%, config.ini, Coordinates, down2_item_posY
    IniWrite, %down3_item_posX%, config.ini, Coordinates, down3_item_posX
    IniWrite, %down3_item_posY%, config.ini, Coordinates, down3_item_posY
    IniWrite, %inv_posX%, config.ini, Coordinates, inv_posX
    IniWrite, %inv_posY%, config.ini, Coordinates, inv_posY
    MsgBox, 64, Thông báo, Cấu hình đã được lưu vào config.ini!
return

; --- Thiết lập tọa độ Item 1 ---
SetItem1:
    WinSet, Disable,, Cấu hình MacroLoot
    ToolTip, Nhấp chuột trái để chọn vị trí cho Item 1.
    KeyWait, LButton, D
    KeyWait, LButton
    MouseGetPos, xpos, ypos
    ToolTip
    IniWrite, %xpos%, config.ini, Coordinates, up_item_posX
    IniWrite, %ypos%, config.ini, Coordinates, up_item_posY
    up_item_posX := xpos
    up_item_posY := ypos
    GuiControl,, Item1Coords, X: %xpos% Y: %ypos%
    WinSet, Enable,, Cấu hình MacroLoot
return

; --- Thiết lập tọa độ Item 2 ---
SetItem2:
    WinSet, Disable,, Cấu hình MacroLoot
    ToolTip, Nhấp chuột trái để chọn vị trí cho Item 2.
    KeyWait, LButton, D
    KeyWait, LButton
    MouseGetPos, xpos, ypos
    ToolTip
    IniWrite, %xpos%, config.ini, Coordinates, up2_item_posX
    IniWrite, %ypos%, config.ini, Coordinates, up2_item_posY
    up2_item_posX := xpos
    up2_item_posY := ypos
    GuiControl,, Item2Coords, X: %xpos% Y: %ypos%
    WinSet, Enable,, Cấu hình MacroLoot
return

; --- Thiết lập tọa độ Item 3 ---
SetItem3:
	WinSet, Disable,, Cấu hình MacroLoot
	ToolTip, Nhấp chuột trái để chọn vị trí cho Item 3.
	KeyWait, LButton, D
	KeyWait, LButton
	MouseGetPos, xpos, ypos
	ToolTip
	IniWrite, %xpos%, config.ini, Coordinates, up3_item_posX
	IniWrite, %ypos%, config.ini, Coordinates, up3_item_posY
	up3_item_posX := xpos
	up3_item_posY := ypos
	GuiControl,, Item3Coords, X: %xpos% Y: %ypos%
	WinSet, Enable,, Cấu hình MacroLoot
return

; --- Thiết lập tọa độ Item 4 ---
SetItem4:
    WinSet, Disable,, Cấu hình MacroLoot
    ToolTip, Nhấp chuột trái để chọn vị trí cho Item 4.
    KeyWait, LButton, D
    KeyWait, LButton
    MouseGetPos, xpos, ypos
    ToolTip
    IniWrite, %xpos%, config.ini, Coordinates, down_item_posX
    IniWrite, %ypos%, config.ini, Coordinates, down_item_posY
    down_item_posX := xpos
    down_item_posY := ypos
    GuiControl,, Item4Coords, X: %xpos% Y: %ypos%
    WinSet, Enable,, Cấu hình MacroLoot
return

; --- Thiết lập tọa độ Item 5 ---
SetItem5:
    WinSet, Disable,, Cấu hình MacroLoot
    ToolTip, Nhấp chuột trái để chọn vị trí cho Item 5.
    KeyWait, LButton, D
    KeyWait, LButton
    MouseGetPos, xpos, ypos
    ToolTip
    IniWrite, %xpos%, config.ini, Coordinates, down2_item_posX
    IniWrite, %ypos%, config.ini, Coordinates, down2_item_posY
    down2_item_posX := xpos
    down2_item_posY := ypos
    GuiControl,, Item5Coords, X: %xpos% Y: %ypos%
    WinSet, Enable,, Cấu hình MacroLoot
return

; --- Thiết lập tọa độ Item 6 ---
SetItem6:
    WinSet, Disable,, Cấu hình MacroLoot
    ToolTip, Nhấp chuột trái để chọn vị trí cho Item 6.
    KeyWait, LButton, D
    KeyWait, LButton
    MouseGetPos, xpos, ypos
    ToolTip
    IniWrite, %xpos%, config.ini, Coordinates, down3_item_posX
    IniWrite, %ypos%, config.ini, Coordinates, down3_item_posY
    down3_item_posX := xpos
    down3_item_posY := ypos
    GuiControl,, Item6Coords, X: %xpos% Y: %ypos%
    WinSet, Enable,, Cấu hình MacroLoot
return

; --- Thiết lập tọa độ Inventory ---
SetInventory:
    WinSet, Disable,, Cấu hình MacroLoot
    ToolTip, Nhấp chuột trái để chọn vị trí cho Inventory.
    KeyWait, LButton, D
    KeyWait, LButton
    MouseGetPos, xpos, ypos
    ToolTip
    IniWrite, %xpos%, config.ini, Coordinates, inv_posX
    IniWrite, %ypos%, config.ini, Coordinates, inv_posY
    inv_posX := xpos
    inv_posY := ypos
    GuiControl,, InvCoords, X: %xpos% Y: %ypos%
    WinSet, Enable,, Cấu hình MacroLoot
return