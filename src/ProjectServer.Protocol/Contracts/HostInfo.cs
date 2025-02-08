using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MSBuildProjectTools.ProjectServer.Protocol.Contracts
{
    public record class HostInfo(ProtocolVersion ProtocolVersion, string RuntimeVersion, string SdkVersion, string MSBuildVersion);

    public enum ProtocolVersion
    {
        Unknown = 0,
        V1 = 1,
    }
}
