using Xunit;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    using Utilities;

    /// <summary>
    ///     An xUnit collection fixture that ensures the MSBuild Locator API is called to discover and use the latest version of the MSBuild engine before any tests are run that depend on it.
    /// </summary>
    public sealed class MSBuildEngineFixture
    {
        /// <summary>
        ///     Create a new <see cref="MSBuildEngineFixture"/>.
        /// </summary>
        public MSBuildEngineFixture()
        {
            MSBuildHelper.DiscoverMSBuildEngine();
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
