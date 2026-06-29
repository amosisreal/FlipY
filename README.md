# FlipY

A small native Windows app that toggles Y-axis inversion for mouse movement.

## What it does
FlipY flips the Y-axis of mouse movement so that moving the mouse up or down feels inverted. This can be useful for certain input setups and workflows.

## Current behavior
The mouse is only flipped while the cursor is not visible. This is intentional and makes it easier to navigate menus and on-screen interfaces when you are using the cursor to select items.

## Missing feature
A feature for Meccha Chameleon is not implemented yet. If you want that behavior, it will need to be added in a future update.

## Download and run
You can download the published executable, FlipY.exe, and run it directly on Windows.

## Build
This project uses .NET 6 WinForms. Install the .NET 6 SDK, then run:

```powershell
dotnet build

dotnet run
```
