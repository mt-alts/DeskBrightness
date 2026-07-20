<div align="center">
  <img src="https://raw.githubusercontent.com/mt-alts/DeskBrightness/refs/heads/master/icon.png" width="64" height="64" alt="DeskBrightness">
  <h1>DeskBrightness</h1>
</div>

![Version](https://img.shields.io/badge/version-1.0.0-brightgreen) [![License MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://raw.githubusercontent.com/mt-alts/DeskBrightness/refs/heads/master/LICENSE)

Turn your Android phone into an ambient light sensor and automatically adjust your Windows display brightness.

## Overview

DeskBrightness consists of two components:

- **Desktop app** (C#, WPF, .NET 10) — runs on Windows, communicates with the phone via ADB, and controls monitor brightness through WMI and DDC/CI.
- **Android app** — runs on your phone as a lightweight process, reads the ambient light sensor, and streams lux data to the desktop over ADB.

## Features

- **Ambient light sensing** — uses your phone's existing light sensor, no extra hardware needed
- **Automatic brightness adjustment** — logarithmic mapping curve calibrated for human eye perception
- **DDC/CI support** — directly controls monitor brightness when supported
- **WMI fallback** — uses Windows WMI for laptops and monitors without DDC/CI
- **Smooth transitions** — gradual brightness changes via smooth stepping controller
- **Low-pass filtering** — EMA filter reduces sensor noise
- **USB and wireless ADB** — connect via USB cable or wireless debugging
- **Configurable curve** — adjustable lux threshold, brightness step threshold, smoothing window, and apply interval
- **Multi-language** — English and Turkish interface

## Requirements

### Desktop
- Windows 10 or later
- [ADB (Android Debug Bridge)](https://developer.android.com/studio/releases/platform-tools) — bundled with the installer
- Monitor with DDC/CI support (recommended) or laptop with WMI brightness control

### Phone
- Android 8.0 (API 26) or later
- USB debugging enabled, or wireless debugging enabled for wireless connection
- Ambient light sensor (present on virtually all Android devices)

## Quick Start

### 1. Enable USB debugging on your phone
1. Open **Settings → About phone** → tap **Build number** 7 times to unlock Developer options
2. Go to **Settings → System → Developer options** → enable **USB debugging**
3. For wireless: also enable **Wireless debugging** and tap **Pair with pairing code**

### 2. Connect the device
- **USB**: Connect your phone via USB cable. The device appears automatically.
- **Wireless**: Click the **+** button next to the device list → select **Pair** → enter the IP, port, and pairing code shown on your phone.

### 3. Start
1. Select your device from the list
2. Press **Connect**
3. DeskBrightness automatically senses ambient light and adjusts your screen brightness

## How It Works

```
Phone light sensor → Low-pass filter → Logarithmic mapper
→ Hysteresis filter → Smooth stepping → DDC/CI or WMI → Monitor brightness
```

1. **Phone** reads ambient light (lux) via the Android sensor API
2. **Low-pass filter** (EMA) smooths sensor noise
3. **Logarithmic mapper** converts lux to brightness using a human-perception-matched curve
4. **Hysteresis filter** prevents unnecessary small changes
5. **Smooth stepping controller** applies gradual brightness transitions
6. **DDC/CI or WMI** sets the actual monitor brightness

## Project Structure

```
DeskBrightness/
├── src/
│   ├── DeskBrightness/          # WPF desktop application
│   ├── DeskBrightness.Adb/      # ADB communication library
│   ├── DeskBrightness.Core/     # Shared core logic
│   ├── DeskBrightness.Win/      # Windows brightness controllers
│   └── DeskBrightness.Test/     # Unit tests
├── DeskBrightness.Mobile/       # Android application
├── setup/                       # Inno Setup installer
└── README.md
```

## Building

```bash
dotnet build DeskBrightness.slnx
```

## License

MIT License. See [LICENSE](https://github.com/mt-alts/DeskBrightness/blob/master/LICENSE) for details.
