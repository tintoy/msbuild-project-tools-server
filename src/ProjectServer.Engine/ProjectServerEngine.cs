using Microsoft.Extensions.Logging;
using MSBuildProjectTools.ProjectServer.Protocol.Contracts;
using MSBuildProjectTools.ProjectServer.Utilities;
using Nito.AsyncEx;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.ProjectServer.Engine
{
    /// <summary>
    ///     Primary API for the MSBuild project-server engine.
    /// </summary>
    public interface IProjectServerEngine
    {

    }

    /// <summary>
    ///     The MSBuild project-server engine.
    /// </summary>
    class ProjectServerEngine
        : DisposableObject, IProjectServerEngine
    {
        readonly AsyncReaderWriterLock _stateLock = new AsyncReaderWriterLock();
        readonly HostInfo _hostInfo;
        readonly ILogger _logger;

        ProjectServerConfiguration _configuration = ProjectServerConfiguration.Default;

        public ProjectServerEngine(HostInfo hostInfo, ILogger<ProjectServerEngine> logger)
        {
            if (hostInfo == null)
                throw new ArgumentNullException(nameof(hostInfo));

            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            _hostInfo = hostInfo;
            _logger = logger;
        }

        public async Task<ProjectServerConfiguration> GetConfiguration(CancellationToken cancellationToken)
        {
            using (await _stateLock.ReaderLockAsync(cancellationToken))
            {
                return _configuration;
            }
        }

        public async Task<ProjectServerConfiguration> UpdateConfiguration(ProjectServerConfiguration configuration, CancellationToken cancellationToken)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            using (await _stateLock.WriterLockAsync(cancellationToken))
            {
                _configuration = _configuration.Merge(configuration);

                return _configuration;
            }
        }
    }
}
