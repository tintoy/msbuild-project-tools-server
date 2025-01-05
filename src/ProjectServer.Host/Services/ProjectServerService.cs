using Grpc.Core;
using Microsoft.Extensions.Logging;
using MSBuildProjectTools.ProjectServer.Protocol;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using PSP = MSBuildProjectTools.ProjectServer.Protocol;

namespace ProjectServer.Host.Services
{
    public class ProjectServerService
        : PSP.ProjectServer.ProjectServerBase

    {
        readonly ILogger<ProjectServerService> _logger;

        public ProjectServerService(ILogger<ProjectServerService> logger)
        {
            if (logger == null)
                throw new System.ArgumentNullException(nameof(logger));

            _logger = logger;
        }

        public override async Task<HostInfoResponse> GetHostInfo(HostInfoRequest request, ServerCallContext context)
        {
            await Task.Yield();

            context.CancellationToken.ThrowIfCancellationRequested();

            return new HostInfoResponse
            {
                ProtocolVersion = 1,
                RuntimeVersion = RuntimeEnvironment.GetSystemVersion(),
                SdkVersion = "0.0.0.0", // TODO: Get from runtime configuration.
                MsbuildVersion = "0.0.0.0" // TODO: Get from runtime configuration.
            };
        }

        public override async Task<ListProjectsResponse> ListProjects(ListProjectsRequest request, ServerCallContext context)
        {
            await Task.Yield();
            
            context.CancellationToken.ThrowIfCancellationRequested();

            return new ListProjectsResponse
            {
                // TODO: Get from runtime state.
                Projects =
                {
                    new ProjectMetadata
                    {
                        Name = "Bar",
                        Location = "C:\\Foo\\Bar\\Bar.csproj",
                        Status = ProjectStatus.Valid,
                    },
                    new ProjectMetadata
                    {
                        Name = "Baz",
                        Location = "C:\\Foo\\Baz\\Baz.csproj",
                        Status = ProjectStatus.Invalid,
                    },
                }
            };
        }
    }
}
