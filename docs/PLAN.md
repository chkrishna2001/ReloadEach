# ReloadEach Visual Studio Extension — Detailed Plan

## Overview
Create a Visual Studio extension (VSIX) named "ReloadEach" that iterates all projects in the open solution and for each project performs: unload the project, reload the project, then wait a configurable delay before moving to the next project. The extension should support Visual Studio 2019, 2022 and 2026 (target ranges will be specified in the VSIX manifest).

## Goals
- Support Visual Studio 2019, 2022, 2026.
- Provide a command (Tools menu / context / toolbar) that: 1) walks every project in the solution sequentially, 2) unloads the project, 3) reloads the project, 4) waits a configurable delay before proceeding to the next project.
- Default delay: 2 seconds (configurable via Tools → Options page in Visual Studio).
- Safe: only operate on loaded projects (skip F#/.special projects if API indicates unsupported), perform operations on UI thread when required, and report progress and errors to the Output window.

## High-level Design
1. VSIX project (C#) built with the Visual Studio SDK targeting supported versions.
2. An AsyncPackage-derived package registered in the VSIX manifest that exposes a menu command `Reload Each Project` under the `Tools` menu.
3. An Options page implemented via `DialogPage` to persist the `DelaySeconds` setting (default 2).
4. Implementation uses Visual Studio services to enumerate projects and perform unload/reload operations. Two possible approaches:
   - Strong approach (recommended): Use low-level IVsSolution/IVsSolution4 APIs to unload and reload projects programmatically using project GUIDs/IVsHierarchy. Example APIs: `IVsSolution.GetProjectEnum` + `IVsSolution4.UnloadProjectAsync` / `IVsSolution4.ReloadProjectAsync` (or synchronous equivalents depending on SDK availability). This approach is robust and does not rely on UI selection.
   - Simpler fallback: Use DTE automation and the built-in commands `Project.UnloadProject` and `Project.ReloadProject` by selecting each project and invoking `DTE.ExecuteCommand(...)`. This is less robust (depends on selection and command names) but easier to prototype.
5. Use `JoinableTaskFactory` / `ThreadHelper` to run asynchronous operations and ensure UI-thread calls are marshaled properly.
6. Add progress and logging: write messages to an `OutputWindowPane` and show a simplified status via `IVsStatusbar` if desired.

## Implementation Steps (detailed)
1. Scaffold VSIX solution:
   - Create a solution `ReloadEach.sln` containing a single VSIX project `ReloadEach`.
   - Reference `Microsoft.VisualStudio.Shell.15.0+` packages (SDK) required for AsyncPackage. Use SDK/nuget appropriate for multi-targeting (VSIX manifest will declare installation targets).
2. Implement package and command:
   - Add `AsyncPackage` class `ReloadEachPackage` with registration attributes and provide `ProvideMenuResource` + `ProvideAutoLoad` only if necessary (prefer `ProvideMenuResource` and command registration via .vsct file).
   - Add a menu command handler `ReloadEachCommand` that triggers the main workflow.
3. Enumerate projects:
   - Acquire `IVsSolution` service (`SVsSolution`) and iterate projects via `IVsSolution.GetProjectEnum` or use `DTE.Solution.Projects` for a simpler route.
   - Build a list of project nodes with identifying info (display name, project GUID/IVsHierarchy, loaded state).
4. Unload/reload each project:
    - For each candidate project:
       - If already unloaded, skip or attempt reload.
       - Call `IVsSolution4.UnloadProjectAsync(projectGuid, UnloadOptions)` (or call the appropriate API for the installed SDK) and await completion.
       - Call reload API (e.g., `IVsSolution4.ReloadProjectAsync` or equivalent) and await completion.
       - Wait `delayMs` using `Task.Delay` (respecting cancellation token if provided) before moving to the next project.
       - Log success/failure to the output pane.
5. Options page:
   - Add a `DialogPage`-derived class `ReloadEachOptionsPage` with a property `public int DelaySeconds { get; set; } = 2;`
   - Register via `ProvideOptionPage(typeof(ReloadEachOptionsPage), "ReloadEach", "General", 0, 0, true)` attribute on the package.
6. Threading & cancellation:
   - Expose cancellation by letting the user cancel the operation (optional). Use a `CancellationTokenSource` from the command to stop iteration.
   - Ensure calls that require UI thread are invoked via `JoinableTaskFactory.SwitchToMainThreadAsync()`.
7. VSIX manifest & Compatibility
   - In `source.extension.vsixmanifest` list `InstallationTarget` entries for Visual Studio versions:
     - Visual Studio 2019 (Version range 16.0 - 16.* / use `16.0` family)
     - Visual Studio 2022 (17.0 family)
     - Visual Studio 2026 (18.0 family) — if the host supports 18.x; otherwise use broad range.
   - Use manifests and `SupportedProducts` so the extension is discoverable in those VS instances.
8. Build and test
   - Build the VSIX in Debug/Release and test by launching Experimental Instance of Visual Studio (or install into local Visual Studio by right-clicking the VSIX and selecting Install).
   - Validate on VS2019, VS2022, and VS2026 if the user has them available; otherwise validate on available versions.

## Error handling & logging
- Log each unload/reload result including exception messages to the Output window under a pane named `ReloadEach`.
- Skip projects we cannot handle and continue with others.
- If a critical failure occurs (service unavailable), show a message box with a short error and write details to the output pane.

## UI & UX
- Single command `Tools → Reload Each Project` that runs the workflow.
- Options page under `Tools → Options → ReloadEach → General` with field `Delay seconds` (integer).
- Progress messages in Output pane.

## Files to create
- `ReloadEach.sln` (scaffold)
- `src/ReloadEach/ReloadEachPackage.cs` (AsyncPackage)
- `src/ReloadEach/Commands/ReloadEachCommand.cs` (command handler)
- `src/ReloadEach/Options/ReloadEachOptionsPage.cs` (DialogPage)
- `src/ReloadEach/Resources/` (vsct, icons)
- `src/ReloadEach/source.extension.vsixmanifest`
- `README.md` with build and install instructions
- `docs/PLAN.md` (this file)

## Timeline & next actions
1. Create this `docs/PLAN.md` and add it to the repository for your review. (DONE)
2. After you review and confirm, I will scaffold the VSIX project and implement the package + command.
3. Implement unload/reload using `IVsSolution4` APIs and add the Options page.
4. Build the VSIX and provide instructions to test.

## Notes / Open questions
- Do you want the command to run automatically on solution open or only via explicit Tools command? (current plan: explicit command)
- Do you require a UI confirmation dialog before performing the operations?
- Do you have Visual Studio 2019/2022/2026 available for testing, or should I rely on experimental instance testing only?

---

If this plan looks good, reply "Approve" and I'll start scaffolding the project files and implementing the package. If you'd like changes, tell me what to adjust and I'll update the plan.
