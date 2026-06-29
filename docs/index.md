---
layout: default
---

<p>
  <a href="https://github.com/amosisreal/FlipY/raw/main/FlipY.exe" class="btn">Download FlipY.exe</a>
  <a href="https://github.com/amosisreal/FlipY" class="btn">View on GitHub</a>
</p>

## What it does

FlipY flips the Y-axis of mouse movement so moving the mouse up or down feels inverted. Useful for gaming setups or input workflows that expect inverted Y.

## Current behavior

Inversion is only active while the cursor is not visible. This means normal desktop navigation stays unchanged — inversion only kicks in inside games or apps that hide the cursor.

## Usage

Toggle Y-axis inversion with:

- The button in the app window
- The **Ctrl+Alt+Y** global hotkey
- A click on the system tray icon

## Download

Download [FlipY.exe](https://github.com/amosisreal/FlipY/raw/main/FlipY.exe) and run it directly — no installer needed. Requires Windows.

## Build from source

Requires the [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0).

```powershell
dotnet build
dotnet run
```
