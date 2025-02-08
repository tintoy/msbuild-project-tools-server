namespace MSBuildProjectTools.ProjectServer.Engine.Models
{
    public record class HostInfo(ProjectServerProtocolVersion ProtocolVersion, string? RuntimeVersion, string? SdkVersion, string? MSBuildVersion)
    {
        public static readonly HostInfo Empty = new HostInfo(ProtocolVersion: ProjectServerProtocolVersion.Unknown, RuntimeVersion: null, SdkVersion: null, MSBuildVersion: null);
    };
}
