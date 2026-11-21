# Windows 11 Taskbar Internet Speed Meter Widget

A lightweight, modern Windows 11 taskbar speed meter widget designed to sit beside the Windows 11 Widgets icon.

## UI Design

```text
  [↓ ↑]    ↓ 12.4 MB/s
 (larger)  ↑  1.2 MB/s
```

* **Clean Visuals (Zero Drop Shadow, Selective Ambient Glow)**:
  * Removed all box background, borders, and dark drop shadows.
  * Only the active arrows emit a vibrant ambient neon glow (`#0066CC` / `#0F8210` in Light Mode, `#4CC2FF` / `#6CCB5F` in Dark Mode).
  * Inactive arrows stay subtly dimmed ($25-30\%$ opacity) with zero glow.
* **Auto-Positioning Based on Taskbar Alignment**:
  * **Center-Aligned Taskbar (`TaskbarAl = 1`)**:
    * Widget board is pinned to the far-left edge of the screen.
    * Places the speed meter immediately to the **right** of the widget board (`X = 175px`).
  * **Left-Aligned Taskbar (`TaskbarAl = 0`)**:
    * In Windows 11, the Widget board shifts to the right next to the system tray.
    * Places the speed meter to the **left** of the widget board.
  * Automatically detects alignment changes live when you toggle alignment in Windows Settings!
* **Fixed Position (No Dragging Needed)**:
  * Removed drag-to-move so the widget stays perfectly and consistently locked to your taskbar layout.
* **Z-Order Taskbar Pinning (Never Goes Behind Taskbar)**:
  * Attached as an owned window of Windows 11 Taskbar (`Shell_TrayWnd`).
  * Real-time `SetWinEventHook` listens for foreground events: when the taskbar or desktop is clicked/tapped, the widget stays permanently on top of the taskbar.

## How to Run

* **Live Dev Mode**: Double-click [dev.bat](file:///d:/Personal/HG/Internet%20Speed%20Meter/dev.bat) (reloads on `Ctrl + S`).
* **Production**: Double-click [Start-Meter.bat](file:///d:/Personal/HG/Internet%20Speed%20Meter/Start-Meter.bat) or [app/InternetSpeedMeter.exe](file:///d:/Personal/HG/Internet%20Speed%20Meter/app/InternetSpeedMeter.exe).
