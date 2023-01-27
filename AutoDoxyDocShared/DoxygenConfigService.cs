using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace AutoDoxyDoc
{
    /// <summary>
    /// Doxygen configuration service for accessing Doxygen configuration.
    /// </summary>
    public class DoxygenConfigService
    {
        /// <summary>
        /// Global doxygen configuration.
        /// </summary>
        public DoxygenConfig Config { get; private set; }

        /// <summary>
        /// Initializes the config service asynchronously.
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task InitializeAsync(IAsyncServiceProvider provider, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            Config = new DoxygenConfig();
        }
    }
}
