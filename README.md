# ReloadEach

ReloadEach is a Visual Studio extension for unloading and reloading projects in a solution with a configurable delay between each project.

It is useful when Visual Studio, project systems, generated files, or dependencies need a clean project reload without manually unloading and reloading every project one by one.

## Features

- Reload all projects in the active solution.
- Reload only the selected project or selected projects from Solution Explorer.
- Configurable delay between each unload/reload cycle.
- Cancel command for an in-progress reload run.
- Progress updates in the Visual Studio status bar.
- Output logging in the `ReloadEach` output pane.

## Commands

Reload all projects:

- `Tools > Reload All Projects`
- Right-click the solution in Solution Explorer, then choose `Reload All Projects`
- Use the ReloadEach toolbar button when visible

Reload selected projects:

- Select one or more projects in Solution Explorer.
- Right-click the selection.
- Choose `Reload Selected Projects`.

Cancel a running reload:

- `Tools > Cancel ReloadEach`

## Settings

Open:

```text
Tools > Options > ReloadEach > General
```

Available setting:

- `Delay seconds`: number of seconds to wait after reloading one project before moving to the next. The default is `2`.

## Supported Visual Studio Versions

ReloadEach targets Visual Studio 2022 and newer, including Visual Studio 2026 previews, on `amd64` and `arm64`.

## Build

Command-line build:

```powershell
dotnet restore .\ReloadEach.slnx
dotnet build .\ReloadEach.slnx -c Release
```

The VSIX is created at:

```text
ReloadEach\bin\Release\ReloadEach.vsix
```

## Release

The GitHub Actions workflow builds the VSIX on pushes and pull requests to `main` or `master`.

To create a release build, push a version tag:

```powershell
git tag v0.1.6
git push origin v0.1.6
```

On `v*` tags, the workflow patches the VSIX manifest version from the tag, builds the VSIX, and uploads it as an artifact.

Marketplace publishing runs only when:

- The push is a `v*` tag.
- The repository has a `VS_MARKETPLACE_PAT` secret.
- `publishManifest.json` has the correct Marketplace publisher value.

## First Push And GitHub Actions

GitHub Actions cannot run until the workflow file exists on GitHub. Commit `.github/workflows/build.yml` and push it to the repository first.

If the GitHub repository already exists, a normal push is enough:

```powershell
git push -u origin main
```

If the GitHub repository does not exist yet, create it first, then push. You can create it on github.com or with the GitHub CLI:

```powershell
gh repo create chkrishna2001/ReloadEach --public --source . --remote origin --push
```

## Project Layout

- `ReloadEach/ReloadEach.csproj` - VSIX project and packaging configuration.
- `ReloadEach/Commands/Commands.vsct` - menu, context menu, toolbar, and icon registration.
- `ReloadEach/Commands/ReloadEachCommand.cs` - command handlers and project reload logic.
- `ReloadEach/Options/ReloadEachOptionsPage.cs` - delay setting.
- `ReloadEach/source.extension.vsixmanifest` - VSIX manifest.
- `ReleaseNotes.md` - release notes included in the VSIX.
