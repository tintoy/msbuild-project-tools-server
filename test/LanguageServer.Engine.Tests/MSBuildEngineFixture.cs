using Microsoft.Build.Locator;
using System;
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
        ///     Create a new <see cref="MSBuildEngineFixture"/>.
        /// </summary>
        public MSBuildEngineFixture()
        {
            var queryOptions = new VisualStudioInstanceQueryOptions
            {
                // We can only load the .NET Core MSBuild engine
                DiscoveryTypes = DiscoveryType.DotNetSdk
            };

            VisualStudioInstance latestInstance = MSBuildLocator
                .QueryVisualStudioInstances(queryOptions)
                .OrderBy(instance => instance.Version)
                .FirstOrDefault();

            if (latestInstance == null)
                throw new Exception("Cannot locate MSBuild engine.");

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
    sealed class MSBuildEngineFixtureCollection
        : ICollectionFixture<MSBuildEngineFixture>
    {
    }
}
