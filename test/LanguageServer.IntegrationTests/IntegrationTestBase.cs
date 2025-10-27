using Serilog;
using Serilog.Context;
using Serilog.Core;
using System;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.IntegrationTests
{
    /// <summary>
    ///     The base class for integration test suites.
    /// </summary>
    public abstract class IntegrationTestBase
    {
        protected IntegrationTestBase(ITestOutputHelper testOutput)
        {
            ArgumentNullException.ThrowIfNull(testOutput);

            TestOutput = testOutput;

            // Redirect component logging to Serilog.
            Log = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.FromLogContext()
                .WriteTo.TestOutput(TestOutput)
                .CreateLogger();
        }

        protected ITestOutputHelper TestOutput { get; }

        protected ILogger Log { get; }
    }
}
