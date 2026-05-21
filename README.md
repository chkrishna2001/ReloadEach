# ReloadEach

ReloadEach is a Visual Studio extension that walks every project in the active solution and performs a repeatable unload/reload cycle with a configurable delay between projects.

## Features

- Sequential project processing using `IVsSolution4`.
- Per-project unload → reload → delay workflow.
- Configurable delay with a default of 2 seconds.
- Cancel command to stop an in-progress run.
- Progress reporting in the Visual Studio status bar.
- Logging in the Output window under the `ReloadEach` pane.

## Commands

- `Tools → Reload Each Project`
- `Tools → Cancel ReloadEach`

## Configuration

Open `Tools → Options → ReloadEach → General` and change `Delay seconds`.

## Installation

Build the solution and install the resulting VSIX into Visual Studio 2019, 2022, or 2026.

## Build

Open `ReloadEach.slnx` in Visual Studio with the VSIX development workload installed, then build the project.

Command-line build:

```powershell
dotnet build .\ReloadEach\ReloadEach.csproj -c Debug
```

## Project layout

- `ReloadEach/ReloadEach.csproj` - VSIX project.
- `ReloadEach/Commands/ReloadEachCommand.cs` - command and project processing logic.
- `ReloadEach/Commands/Commands.vsct` - menu and command icon registration.
- `ReloadEach/Options/ReloadEachOptionsPage.cs` - options page.
- `ReloadEach/source.extension.vsixmanifest` - VSIX manifest.

## Notes

- The command icon uses `ReloadEach/Images/Icon-16x16.png`.
- The extension icon uses `ReloadEach/Images/Icon-64x64.png`.
- Additional generated icon sizes are stored under `ReloadEach/Images`.
