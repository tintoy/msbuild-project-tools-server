using Microsoft.Build.Locator;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    /// <summary>
    ///     An xUnit collection fixture that ensures the MSBuild Locator API is called to discover and use the latest version of the MSBuild engine before any tests are run that depend on it.
    /// </summary>
    public sealed class MSBuildEngineFixture
        : IDisposable
    {
        /// <summary>
        /// The minimum version of the .NET Core SDK supported by tests that depend on this fixture.
        /// </summary>
        static readonly Version TargetSdkMinVersion = new Version("3.1.201");

        /// <summary>
        /// The maximum version of the .NET Core SDK supported by tests that depend on this fixture.
        /// </summary>
        static readonly Version TargetSdkMaxVersion = new Version("3.1.999");

        /// <summary>
        ///     Create a new <see cref="MSBuildEngineFixture"/>.
        /// </summary>
        public MSBuildEngineFixture()
        {
            var queryOptions = new VisualStudioInstanceQueryOptions
            {
                // We can only load the .NET Core MSBuild engine
                DiscoveryTypes = DiscoveryType.DotNetSdk
            };

            VisualStudioInstance[] allInstances = MSBuildLocator
                .QueryVisualStudioInstances(queryOptions)
                .ToArray();

            VisualStudioInstance latestInstance = allInstances
                .OrderByDescending(instance => instance.Version)
                .FirstOrDefault(instance =>
                    // The tests that depend on this fixture only work with the .NET Core 3.1 SDK
                    instance.Version >= TargetSdkMinVersion
                    &&
                    instance.Version <= TargetSdkMaxVersion
                );

            if (latestInstance == null)
            {
                string foundVersions = String.Join(", ", allInstances.Select(instance => instance.Version));

                throw new Exception($"Cannot locate MSBuild engine for .NET Core {TargetSdkMinVersion.Major}.{TargetSdkMinVersion.Minor} SDK ({TargetSdkMinVersion} <= SDK version <= {TargetSdkMaxVersion}). Found versions: [{foundVersions}].");   
            }

            MSBuildLocator.RegisterInstance(latestInstance);
        }

        /// <summary>
        ///     Dispose of resources being used by the <see cref="MSBuildEngineFixture"/>.
        /// </summary>
        public void Dispose()
        {
            if (MSBuildLocator.IsRegistered)
                MSBuildLocator.Unregister();
        }
    }

    /// <summary>
    ///     The collection-fixture binding for <see cref="MSBuildEngineFixture"/>.
    /// </summary>
    [CollectionDefinition("MSBuild Engine")]
    public sealed class MSBuildEngineFixtureCollection
        : ICollectionFixture<MSBuildEngineFixture>
    {
    }
}
