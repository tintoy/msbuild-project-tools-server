using Microsoft.Extensions.Logging;
using MSBuildProjectTools.ProjectServer.Engine.Models;
using MSBuildProjectTools.ProjectServer.Utilities;
using DotNext.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.ProjectServer.Engine
{
    /// <summary>
    ///     The MSBuild project-server engine.
    /// </summary>
    class ProjectServerEngine
        : DisposableObject, IProjectServerEngine
    {
        public static readonly ProjectServerProtocolVersion CurrentProtocolVersion = ProjectServerProtocolVersion.V1;

        public static readonly HostInfo HostInfoTemplate = HostInfo.Empty with
        {
            ProtocolVersion = CurrentProtocolVersion,
        };

        readonly AsyncReaderWriterLock _stateLock = new AsyncReaderWriterLock();
        
        readonly ILogger _logger;

        HostInfo _cachedHostInfo;
        ProjectServerConfiguration _configuration = ProjectServerConfiguration.Default;

        public ProjectServerEngine(ILogger<ProjectServerEngine> logger)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            _logger = logger;
        }

        public async Task<HostInfo> GetHostInfo(CancellationToken cancellationToken = default)
        {
            using (AsyncReadLockScope readLock = await _stateLock.EnterReadScopeAsync(cancellationToken))
            {
                if (_cachedHostInfo != null)
                    return _cachedHostInfo;

                using (await readLock.UpgradeToWriteLockAsync(cancellationToken))
                {
                    // May have already been initialised by another thread
                    if (_cachedHostInfo == null)
                        _cachedHostInfo = DiscoverHostInfo();
                }

                return _cachedHostInfo;
            }
        }

        public async Task<ProjectServerConfiguration> GetConfiguration(CancellationToken cancellationToken = default)
        {
            using (await _stateLock.EnterReadScopeAsync(cancellationToken))
            {
                return _configuration;
            }
        }

        public async Task<ProjectServerConfiguration> UpdateConfiguration(ProjectServerConfiguration configuration, CancellationToken cancellationToken = default)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            using (await _stateLock.EnterWriteScopeAsync(cancellationToken))
            {
                _configuration = _configuration.Merge(configuration);

                return _configuration;
            }
        }

        HostInfo DiscoverHostInfo()
        {
            HostInfo hostInfo = HostInfoTemplate with
            {
                RuntimeVersion = "0.0.0.0",
                SdkVersion = "0.0.0.0",
                MSBuildVersion = "0.0.0.0"
            };

            _logger.LogInformation("Discovered project-server host configuration: ProtocolVersion={ProtocolVersion}, RuntimeVersion={RuntimeVersion}, SdkVersion={SdkVersion}, MSBuildVersion={MSBuildVersion}",
                hostInfo.ProtocolVersion,
                hostInfo.RuntimeVersion,
                hostInfo.SdkVersion,
                hostInfo.MSBuildVersion
            );

            return hostInfo;
        }
    }
}

