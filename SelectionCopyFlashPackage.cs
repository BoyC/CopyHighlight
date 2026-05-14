using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace SelectionCopyFlash
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Selection Copy Flash", "Flashes copied selections and exposes Tools > Options settings.", "1.1.4")]
    [ProvideOptionPage(typeof(SelectionCopyFlashOptionsPage), "Selection Copy Flash", "General", 0, 0, true)]
    [Guid(PackageGuidString)]
    public sealed class SelectionCopyFlashPackage : AsyncPackage
    {
        public const string PackageGuidString = "CC95643C-297F-4B82-ADEE-D4B3F8E52807";
    }
}
