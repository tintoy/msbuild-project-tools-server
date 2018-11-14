using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    using Utilities;

    /// <summary>
    ///     Tests for <see cref="DotNetRuntimeInfo"/>.
    /// </summary>
    public class DotNetRuntimeInfoTests
    {
        /// <summary>
        ///     Verify that <see cref="DotNetRuntimeInfo"/> can parse the output of "dotnet --info".
        /// </summary>
        [Theory(DisplayName = "Parse 'dotnet --info' output ")]
        [InlineData("English", "2.1.401", @"C:\Program Files\dotnet\sdk\2.1.401\", "win10-x64", Examples.English_2_1_401)]
        [InlineData("German", "2.1.403", @"C:\Program Files\dotnet\sdk\2.1.403\", "win10-x64", Examples.German_2_1_403)]
        [InlineData("Chinese", "2.1.403", @"C:\Program Files\dotnet\sdk\2.1.403\", "win10-x64", Examples.Chinese_2_1_403)]
        public void Parse(string language, string expectedVersion, string expectedBaseDirectory, string expectedRID, string dotnetInfoOutput)
        {
            DotNetRuntimeInfo parsedOutput;

            using (StringReader outputReader = new StringReader(dotnetInfoOutput))
            {
                parsedOutput = DotNetRuntimeInfo.ParseDotNetInfoOutput(outputReader);
            }

            Assert.NotNull(parsedOutput);
            Assert.Equal(expectedVersion, parsedOutput.Version);
            Assert.Equal(expectedBaseDirectory, parsedOutput.BaseDirectory);
            Assert.Equal(expectedRID, parsedOutput.RID);
        }

        /// <summary>
        ///     Output examples from "dotnet --info".
        /// </summary>
        static class Examples
        {
            /// <summary>
            ///     .NET Core SDK v2.1.401 (English)
            /// </summary>
            public const string English_2_1_401 = @"
.NET Core SDK (reflecting any global.json):
 Version:   2.1.401
 Commit:    91b1c13032

Runtime Environment:
 OS Name:     Windows
 OS Version:  10.0.17134
 OS Platform: Windows
 RID:         win10-x64
 Base Path:   C:\Program Files\dotnet\sdk\2.1.401\

Host (useful for support):
  Version: 2.1.3
  Commit:  124038c13e

.NET Core SDKs installed:
  1.1.7 [C:\Program Files\dotnet\sdk]
  1.1.8 [C:\Program Files\dotnet\sdk]
  1.1.9 [C:\Program Files\dotnet\sdk]
  1.1.10 [C:\Program Files\dotnet\sdk]
  2.1.4 [C:\Program Files\dotnet\sdk]
  2.1.100 [C:\Program Files\dotnet\sdk]
  2.1.103 [C:\Program Files\dotnet\sdk]
  2.1.104 [C:\Program Files\dotnet\sdk]
  2.1.200-preview-007474 [C:\Program Files\dotnet\sdk]
  2.1.200-preview-007576 [C:\Program Files\dotnet\sdk]
  2.1.200-preview-007597 [C:\Program Files\dotnet\sdk]
  2.1.200 [C:\Program Files\dotnet\sdk]
  2.1.201 [C:\Program Files\dotnet\sdk]
  2.1.202 [C:\Program Files\dotnet\sdk]
  2.1.300-preview1-008174 [C:\Program Files\dotnet\sdk]
  2.1.300-preview2-008530 [C:\Program Files\dotnet\sdk]
  2.1.300-preview2-008533 [C:\Program Files\dotnet\sdk]
  2.1.300-rc1-008673 [C:\Program Files\dotnet\sdk]
  2.1.300 [C:\Program Files\dotnet\sdk]
  2.1.401 [C:\Program Files\dotnet\sdk]

.NET Core runtimes installed:
  Microsoft.AspNetCore.All 2.1.0-preview1-final [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.All]
  Microsoft.AspNetCore.All 2.1.0-preview2-final [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.All]
  Microsoft.AspNetCore.All 2.1.0-rc1-final [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.All]
  Microsoft.AspNetCore.All 2.1.0 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.All]
  Microsoft.AspNetCore.All 2.1.3 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.All]
  Microsoft.AspNetCore.App 2.1.0-preview1-final [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 2.1.0-preview2-final [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 2.1.0-rc1-final [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 2.1.0 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 2.1.3 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.NETCore.App 1.0.9 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 1.0.10 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 1.0.11 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 1.0.12 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 1.1.6 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 1.1.7 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 1.1.8 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 1.1.9 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.0.5 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.0.6 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.0.7 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.0.9 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.1.0-preview1-26216-03 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.1.0-preview2-26406-04 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.1.0-rc1 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.1.0 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.1.3 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]

To install additional .NET Core runtimes or SDKs:
  https://aka.ms/dotnet-download
            ";

            /// <summary>
            ///     .NET Core SDK v2.1.401 (Chinese)
            /// </summary>
            public const string Chinese_2_1_403 = @"
.NET Core SDK（反映任何 global.json）:
 Version:   2.1.403
 Commit:    04e15494b6

运行时环境:
 OS Name:     Windows
 OS Version:  10.0.17134
 OS Platform: Windows
 RID:         win10-x64
 Base Path:   C:\Program Files\dotnet\sdk\2.1.403\

Host (useful for support):
  Version: 2.1.5
  Commit:  290303f510

.NET Core SDKs installed:
  2.1.202 [C:\Program Files\dotnet\sdk]
  2.1.401 [C:\Program Files\dotnet\sdk]
  2.1.402 [C:\Program Files\dotnet\sdk]
  2.1.403 [C:\Program Files\dotnet\sdk]

.NET Core runtimes installed:
  Microsoft.AspNetCore.All 2.1.0 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.All]
  Microsoft.AspNetCore.All 2.1.1 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.All]
  Microsoft.AspNetCore.All 2.1.2 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.All]
  Microsoft.AspNetCore.All 2.1.4 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.All]
  Microsoft.AspNetCore.All 2.1.5 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.All]
  Microsoft.AspNetCore.App 2.1.0 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 2.1.1 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 2.1.2 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 2.1.4 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 2.1.5 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.NETCore.App 2.0.9 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.1.4 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.1.5 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]

To install additional .NET Core runtimes or SDKs:
  https://aka.ms/dotnet-download
            ";

            /// <summary>
            ///     .NET Core SDK v2.1.403 (German)
            /// </summary>
            public const string German_2_1_403 = @"
.NET Core SDK (gemäß ""global.json""):
 Version:   2.1.403
 Commit:    04e15494b6

Laufzeitumgebung:
 OS Name:     Windows
 OS Version:  10.0.17763
 OS Platform: Windows
 RID:         win10-x64
 Base Path:   C:\Program Files\dotnet\sdk\2.1.403\

Host (useful for support):
  Version: 2.1.5
  Commit:  290303f510
            ";
        }
    }
}
