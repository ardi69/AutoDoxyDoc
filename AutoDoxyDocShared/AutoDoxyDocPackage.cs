using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;

namespace AutoDoxyDoc
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(AutoDoxyDocPackage.PackageGuidString)]
    [ProvideService(typeof(DoxygenConfigService), IsAsyncQueryable = true)]
    //[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)] // TODO: Enable background load. It is currently disabled because for some reason the package doesn't always load.
    //[ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    //[ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    //[ProvideAutoLoad(UIContextGuids.SolutionExists)] // TODO: Should be changed to async load but for some reason it is not loaded properly in that case.
    [ProvideOptionPage(typeof(OptionsPage), "AutoDoxyDoc", "General", 0, 0, true)]
    [ProvideProfileAttribute(typeof(OptionsPage), "AutoDoxyDoc", "AutoDoxyDoc Settings", 106, 107, isToolsOptionPage: true, DescriptionResourceID = 108)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class AutoDoxyDocPackage : AsyncPackage
    {
        /// <summary>
        /// GenerateCommentPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "5a32bbf1-a606-4a9c-bbe1-851c173adc45";

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoDoxyDocPackage"/> class.
        /// </summary>
        public AutoDoxyDocPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            AddService(typeof(DoxygenConfigService), CreateDoxygenConfigServiceAsync, true);

            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Load Doxygen configuration.
            OptionsPage page = (OptionsPage)GetDialogPage(typeof(OptionsPage));

            var configService = await GetServiceAsync(typeof(DoxygenConfigService)) as DoxygenConfigService;

            if (configService != null)
            {
                configService.Config.LoadSettings(page);
            }

            await GenerateComment.InitializeAsync(this);
        }

        private async Task<object> CreateDoxygenConfigServiceAsync(IAsyncServiceContainer container, CancellationToken cancellationToken, Type serviceType)
        {
            var svc = new DoxygenConfigService();
            await svc.InitializeAsync(this, cancellationToken);
            return svc;
        }

        #endregion
    }
}
