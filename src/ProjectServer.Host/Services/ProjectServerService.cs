using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using PSP = MSBuildProjectTools.ProjectServer.Protocol;
using PSC = MSBuildProjectTools.ProjectServer.Protocol.Contracts;

namespace ProjectServer.Host.Services
{
    /// <summary>
    ///     gRPC service endpoint for the project server protocol.
    /// </summary>
    public class ProjectServerService
        : PSP.ProjectServer.ProjectServerBase

    {
        /// <summary>
        ///     The logger for <see cref="ProjectServerService"/>.
        /// </summary>
        readonly ILogger<ProjectServerService> _logger;

        /// <summary>
        ///     Create a new <see cref="ProjectServerService"/>.
        /// </summary>
        /// <param name="logger">
        ///     The logger for <see cref="ProjectServerService"/>.
        /// </param>
        public ProjectServerService(ILogger<ProjectServerService> logger)
        {
            if (logger == null)
                throw new System.ArgumentNullException(nameof(logger));

            _logger = logger;
        }

        /// <summary>
        ///     Get information about the project server and its target runtime/SDK/MSBuild version.
        /// </summary>
        /// <param name="request">
        ///     The current request.
        /// </param>
        /// <param name="context">
        ///     Contextual information about the current request.
        /// </param>
        /// <returns>
        ///     The response.
        /// </returns>
        public override async Task<PSC.HostInfoResponse> GetHostInfo(PSC.HostInfoRequest request, ServerCallContext context)
        {
            await Task.Yield();

            context.CancellationToken.ThrowIfCancellationRequested();

            return new PSC.HostInfoResponse
            {
                ProtocolVersion = 1,
                RuntimeVersion = RuntimeEnvironment.GetSystemVersion(),
                SdkVersion = "0.0.0.0", // TODO: Get from runtime configuration.
                MsbuildVersion = "0.0.0.0" // TODO: Get from runtime configuration.
            };
        }

        /// <summary>
        ///     Get a list all of projects loaded by the project server.
        /// </summary>
        /// <param name="request">
        ///     The current request.
        /// </param>
        /// <param name="context">
        ///     Contextual information about the current request.
        /// </param>
        /// <returns>
        ///     The response.
        /// </returns>
        public override async Task<PSC.ListProjectsResponse> ListProjects(PSC.ListProjectsRequest request, ServerCallContext context)
        {
            await Task.Yield();
            
            context.CancellationToken.ThrowIfCancellationRequested();

            return new PSC.ListProjectsResponse
            {
                // TODO: Get from runtime state.
                Projects =
                {
                    new PSC.ProjectMetadata
                    {
                        Name = "Bar",
                        Location = "C:\\Foo\\Bar\\Bar.csproj",
                        Status = PSC.ProjectStatus.Valid,
                    },
                    new PSC.ProjectMetadata
                    {
                        Name = "Baz",
                        Location = "C:\\Foo\\Baz\\Baz.csproj",
                        Status = PSC.ProjectStatus.Invalid,
                    },
                }
            };
        }
    }
}
