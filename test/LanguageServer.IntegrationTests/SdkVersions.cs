namespace MSBuildProjectTools.LanguageServer.IntegrationTests
{
    /// <summary>
    ///     Well-known versions of the .NET SDK.
    /// </summary>
    public static class SdkVersions
    {
        /// <summary>
        ///     The default SDK version for .NET 8.0.
        /// </summary>
        public const string Net8_0_100 = "8.0.100";

        /// <summary>
        ///     The default SDK version for .NET 9.0.
        /// </summary>
        public const string Net9_0_100 = "9.0.100";

        /// <summary>
        ///     The default SDK version for .NET 10.0.
        /// </summary>
        public const string Net10_0_100 = "10.0.100";

        /// <summary>
        ///     The default SDK version for .NET 20.0 (non-existent; used to simulate failure conditions).
        /// </summary>
        public const string Net20_0_100 = "20.0.100";

        /// <summary>
        ///     The default version of the .NET SDK used for integration tests.
        /// </summary>
        public const string Default = Net8_0_100;
    }
}
