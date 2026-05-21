using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace ReloadEach
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("ReloadEach", "Unload and reload each project sequentially", "0.1")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(Options.ReloadEachOptionsPage), "ReloadEach", "General", 0, 0, true)]
    [Guid(PackageGuidString)]
    public sealed class ReloadEachPackage : AsyncPackage
    {
        public const string PackageGuidString = "d3b8a7f4-9b1a-4c2f-8d1e-000000000002";

        protected override System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            return Commands.ReloadEachCommand.InitializeAsync(this);
        }
    }
}
