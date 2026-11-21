# Windows 11 Taskbar Internet Speed Meter

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2011%20%7C%2010-0078D4?logo=windows&logoColor=white" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 8.0" />
  <img src="https://img.shields.io/github/v/release/ShahidRaaz/Internet-Speed-Meter?color=success&label=Release" alt="Release" />
  <img src="https://img.shields.io/badge/License-MIT-blue.svg" alt="License" />
</p>

A lightweight, modern Internet Speed Meter widget natively pinned to the Windows 11 taskbar. Designed with Windows 11 Fluent Design principles, live taskbar tracking, instant auto-hide, and full Light/Dark mode integration.

---

## 📥 Downloads

Grab the latest release from the [GitHub Releases Page](https://github.com/ShahidRaaz/Internet-Speed-Meter/releases/latest):

| Package | Format | Description |
| :--- | :---: | :--- |
| **Windows Installer** | [`.msi`](https://github.com/ShahidRaaz/Internet-Speed-Meter/releases/latest) | Full installer with Start Menu shortcut and clean uninstaller. No admin privileges required. |
| **Standalone Portable** | [`.exe`](https://github.com/ShahidRaaz/Internet-Speed-Meter/releases/latest) | Single-file executable. Runs immediately without installation or prerequisites. |
| **Portable Archive** | [`.zip`](https://github.com/ShahidRaaz/Internet-Speed-Meter/releases/latest) | Zipped portable release ready to extract and run anywhere. |

---

## ✨ Features

- **Native Windows 11 Taskbar Integration**:
  - Pinned directly to the taskbar (`Shell_TrayWnd`) so it never falls behind other windows or taskbar elements when clicked.
  - Native Windows 11 taskbar hover highlight effects matching system styling.

- **Intelligent Dynamic Positioning**:
  - **Center-Aligned Taskbar (`TaskbarAl = 1`)**:
    - If Windows 11 Widgets Board is enabled: positioned immediately to the right of the Widgets icon (`X = 165px`).
    - If Widgets Board is disabled: positions cleanly at the far left edge (`X = 14px`).
  - **Left-Aligned Taskbar (`TaskbarAl = 0`)**:
    - Automatically measures the width of the System Tray on the right.
    - If Widgets Board is enabled: places the widget immediately to the left of the Widgets container (`trayLeft - 150px - widgetWidth - 10px`).
    - If Widgets Board is disabled: places the widget immediately to the left of the System Tray.
  - Automatically recalculates on screen resolution, DPI scaling, or taskbar setting changes.

- **Instant Auto-Hide Support**:
  - Automatically and instantly toggles visibility in place when the taskbar auto-hides without awkward sliding animations.
  - Instantly reappears when the taskbar returns.

- **Fluent Design & System Theming**:
  - Automatically switches between Windows 11 Light and Dark themes live via system registry events.
  - Uses official Windows 11 Fluent icons (`Segoe Fluent Icons`) and colors.
  - Active upload/download indicators illuminate dynamically with full opacity when bandwidth is in use.

- **Settings Flyout & Startup Integration**:
  - Click on the widget to open the sleek Fluent settings dialog.
  - Live session statistics (Session Uploaded & Downloaded traffic).
  - One-click **Start with Windows** registry configuration with dirty-state change detection.

- **Custom Drag & Repositioning**:
  - Drag the widget anywhere along the taskbar to customize its position.
  - Right-click and choose **Auto-Align Taskbar** to reset to automatic alignment at any time.

---

## 🛠️ Building from Source

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher.
- Windows 10 / 11.

### Build and Run Locally
```powershell
# Clone the repository
git clone https://github.com/ShahidRaaz/Internet-Speed-Meter.git
cd "Internet-Speed-Meter"

# Build Release
dotnet build -c Release -o ./app

# Run Application
./app/InternetSpeedMeter.exe
```

### Build Standalone Single-File Executable
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish
```

---

## 📄 License
This project is open-source under the [MIT License](LICENSE).
