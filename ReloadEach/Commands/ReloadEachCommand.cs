using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ReloadEach.Commands
{
    internal sealed class ReloadEachCommand
    {
        public const int StartCommandId = 0x0100;
        public const int CancelCommandId = 0x0101;

        public static readonly Guid CommandSet = new Guid("9f3c8f3a-7c1d-4b2a-9c9f-000000000003");

        private static Guid OutputPaneGuid = new Guid("d4e4c9d1-7f99-4c33-9f9a-000000000004");
        private static readonly object RunGate = new object();

        private static CancellationTokenSource currentCancellationSource;
        private static int isRunning;

        private readonly AsyncPackage package;
        private readonly OleMenuCommand startCommand;
        private readonly OleMenuCommand cancelCommand;
        private IVsOutputWindowPane outputPane;

        private ReloadEachCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));

            this.startCommand = new OleMenuCommand(this.ExecuteStart, new CommandID(CommandSet, StartCommandId));
            this.cancelCommand = new OleMenuCommand(this.ExecuteCancel, new CommandID(CommandSet, CancelCommandId))
            {
                Enabled = false
            };

            commandService.AddCommand(this.startCommand);
            commandService.AddCommand(this.cancelCommand);
        }

        public static async System.Threading.Tasks.Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService == null)
            {
                return;
            }

            new ReloadEachCommand(package, commandService);
        }

        private void ExecuteStart(object sender, EventArgs e)
        {
            if (Interlocked.CompareExchange(ref isRunning, 1, 0) != 0)
            {
                _ = WriteToOutputAsync("ReloadEach is already running.");
                return;
            }

            var cancellationSource = new CancellationTokenSource();
            lock (RunGate)
            {
                currentCancellationSource = cancellationSource;
            }

            ThreadHelper.ThrowIfNotOnUIThread();
            this.startCommand.Enabled = false;
            this.cancelCommand.Enabled = true;

            var runningTask = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await RunReloadEachAsync(cancellationSource.Token);
                }
                catch (OperationCanceledException)
                {
                    await WriteToOutputAsync("ReloadEach canceled.");
                }
                catch (Exception ex)
                {
                    await WriteToOutputAsync("ReloadEach failed: " + ex.Message);
                }
                finally
                {
                    lock (RunGate)
                    {
                        currentCancellationSource?.Dispose();
                        currentCancellationSource = null;
                    }

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    this.startCommand.Enabled = true;
                    this.cancelCommand.Enabled = false;
                    Interlocked.Exchange(ref isRunning, 0);
                }
            });

            GC.KeepAlive(runningTask);
        }

        private void ExecuteCancel(object sender, EventArgs e)
        {
            CancellationTokenSource cancellationSource;
            lock (RunGate)
            {
                cancellationSource = currentCancellationSource;
            }

            if (cancellationSource == null)
            {
                _ = WriteToOutputAsync("No active ReloadEach run to cancel.");
                return;
            }

            cancellationSource.Cancel();
            _ = WriteToOutputAsync("Cancellation requested.");
        }

        private async System.Threading.Tasks.Task RunReloadEachAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var solution = await this.package.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            var solution4 = solution as IVsSolution4;
            var statusbar = await this.package.GetServiceAsync(typeof(SVsStatusbar)) as IVsStatusbar;

            if (solution == null || solution4 == null)
            {
                await WriteToOutputAsync("SVsSolution or IVsSolution4 is unavailable.");
                return;
            }

            var options = (Options.ReloadEachOptionsPage)this.package.GetDialogPage(typeof(Options.ReloadEachOptionsPage));
            int delayMs = Math.Max(0, options?.DelaySeconds ?? 2) * 1000;

            var projects = await GetProjectsAsync(solution, cancellationToken);
            if (projects.Count == 0)
            {
                await WriteToOutputAsync("No projects were found in the solution.");
                return;
            }

            await WriteToOutputAsync($"Starting ReloadEach for {projects.Count} projects with {delayMs / 1000.0:0.##} second delay.");

            uint progressCookie = 0;
            BeginProgress(statusbar, ref progressCookie, projects.Count, "ReloadEach in progress");

            try
            {
                for (int index = 0; index < projects.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ProjectInfo project = projects[index];
                    string label = $"ReloadEach: {project.Name}";
                    UpdateProgress(statusbar, ref progressCookie, index, projects.Count, label);
                    await WriteToOutputAsync($"[{index + 1}/{projects.Count}] {project.Name}");

                    int unloadHr = solution4.UnloadProject(project.ProjectGuid, (uint)_VSProjectUnloadStatus.UNLOADSTATUS_UnloadedByUser);
                    if (ErrorHandler.Failed(unloadHr))
                    {
                        await WriteToOutputAsync($"  Unload failed: 0x{unloadHr:X8}");
                    }
                    else
                    {
                        await WriteToOutputAsync("  Unload completed.");
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    int reloadHr = solution4.ReloadProject(project.ProjectGuid);
                    if (ErrorHandler.Failed(reloadHr))
                    {
                        await WriteToOutputAsync($"  Reload failed: 0x{reloadHr:X8}");
                    }
                    else if (reloadHr == VSConstants.S_FALSE)
                    {
                        await WriteToOutputAsync("  Reload skipped (project was not unloaded).");
                    }
                    else
                    {
                        await WriteToOutputAsync("  Reload completed.");
                    }

                    if (index < projects.Count - 1)
                    {
                        await WriteToOutputAsync($"  Waiting {delayMs / 1000.0:0.##} seconds before next project.");
                        await System.Threading.Tasks.Task.Delay(delayMs, cancellationToken);
                    }
                }

                await WriteToOutputAsync("ReloadEach completed.");
            }
            finally
            {
                EndProgress(statusbar, ref progressCookie, projects.Count);
            }
        }

        private static async System.Threading.Tasks.Task<List<ProjectInfo>> GetProjectsAsync(IVsSolution solution, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var projects = new List<ProjectInfo>();
            ErrorHandler.ThrowOnFailure(solution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_ALLPROJECTS, Guid.Empty, out IEnumHierarchies projectEnumerator));

            if (projectEnumerator == null)
            {
                return projects;
            }

            var hierarchies = new IVsHierarchy[1];

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                uint fetched = 0;
                int hr = projectEnumerator.Next(1, hierarchies, out fetched);
                if (hr != VSConstants.S_OK || fetched == 0)
                {
                    break;
                }

                IVsHierarchy hierarchy = hierarchies[0];
                if (hierarchy == null)
                {
                    continue;
                }

                if (!TryGetProjectGuid(hierarchy, out Guid projectGuid))
                {
                    continue;
                }

                string name = GetProjectName(hierarchy) ?? projectGuid.ToString();
                projects.Add(new ProjectInfo(projectGuid, name));
            }

            return projects;
        }

        private static bool TryGetProjectGuid(IVsHierarchy hierarchy, out Guid projectGuid)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            projectGuid = Guid.Empty;
            int hr = hierarchy.GetGuidProperty((uint)VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ProjectIDGuid, out projectGuid);
            return ErrorHandler.Succeeded(hr) && projectGuid != Guid.Empty;
        }

        private static string GetProjectName(IVsHierarchy hierarchy)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            int hr = hierarchy.GetProperty((uint)VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_Name, out object value);
            if (ErrorHandler.Succeeded(hr) && value is string name && !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return null;
        }

        private void BeginProgress(IVsStatusbar statusbar, ref uint progressCookie, int totalProjects, string label)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (statusbar == null)
            {
                return;
            }

            progressCookie = 0;
            statusbar.Progress(ref progressCookie, 1, label, 0, (uint)totalProjects);
        }

        private void UpdateProgress(IVsStatusbar statusbar, ref uint progressCookie, int completed, int totalProjects, string label)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (statusbar == null)
            {
                return;
            }

            statusbar.Progress(ref progressCookie, 1, label, (uint)completed, (uint)totalProjects);
        }

        private void EndProgress(IVsStatusbar statusbar, ref uint progressCookie, int totalProjects)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (statusbar == null)
            {
                return;
            }

            statusbar.Progress(ref progressCookie, 0, "ReloadEach complete", (uint)totalProjects, (uint)totalProjects);
        }

        private async System.Threading.Tasks.Task WriteToOutputAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IVsOutputWindowPane pane = await EnsureOutputPaneAsync();
            pane?.OutputStringThreadSafe(message + Environment.NewLine);
        }

        private async System.Threading.Tasks.Task<IVsOutputWindowPane> EnsureOutputPaneAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (this.outputPane != null)
            {
                return this.outputPane;
            }

            var outputWindow = await this.package.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow == null)
            {
                return null;
            }

            int hr = outputWindow.CreatePane(ref OutputPaneGuid, "ReloadEach", 1, 1);
            if (ErrorHandler.Failed(hr))
            {
                hr = outputWindow.GetPane(ref OutputPaneGuid, out this.outputPane);
                if (ErrorHandler.Failed(hr))
                {
                    this.outputPane = null;
                }

                return this.outputPane;
            }

            ErrorHandler.ThrowOnFailure(outputWindow.GetPane(ref OutputPaneGuid, out this.outputPane));
            return this.outputPane;
        }

        private sealed class ProjectInfo
        {
            public ProjectInfo(Guid projectGuid, string name)
            {
                ProjectGuid = projectGuid;
                Name = name;
            }

            public Guid ProjectGuid { get; }
            public string Name { get; }
        }
    }
}
