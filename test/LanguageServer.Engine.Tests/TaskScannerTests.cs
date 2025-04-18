using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    using SemanticModel;
    using System;
    using System.Runtime.InteropServices;
    using Utilities;

    /// <summary>
    ///     Tests for <see cref="MSBuildTaskScanner"/>.
    /// </summary>
    public class TaskScannerTests
        : TestBase
    {
        /// <summary>
        ///     Enable dotnet host diagnostics while running the tests?
        /// </summary>
        public static readonly bool EnableDotNetHostDiagnostics = false;

        /// <summary>
        ///     Create a new <see cref="MSBuildTaskScanner"/> test suite.
        /// </summary>
        /// <param name="testOutput">
        ///     Output for the current test.
        /// </param>
        public TaskScannerTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
            LogLevelSwitch.MinimumLevel = EnableDotNetHostDiagnostics ? Serilog.Events.LogEventLevel.Verbose : Serilog.Events.LogEventLevel.Information;

            if (EnableDotNetHostDiagnostics)
            {
                Log.Information("Runtime Directory = {RuntimeDirectory}", RuntimeEnvironment.GetRuntimeDirectory());

                Environment.SetEnvironmentVariable("MSBUILD_PROJECT_TOOLS_DOTNET_HOST_DIAGNOSTICS", "1");
                CurrentDotnetInfo = DotnetInfo.GetCurrent(logger: Log);
                Environment.SetEnvironmentVariable("MSBUILD_PROJECT_TOOLS_DOTNET_HOST_DIAGNOSTICS", null);
            }
            else
                CurrentDotnetInfo = DotnetInfo.GetCurrent();

            Assert.NotNull(CurrentDotnetInfo.BaseDirectory);
        }

        /// <summary>
        ///     Information about the current .NET runtime.
        /// </summary>
        DotnetInfo CurrentDotnetInfo { get; }

        /// <summary>
        ///     Verify that the task scanner can retrieve task metadata from an assembly.
        /// </summary>
        /// <param name="fileName">
        ///     The relative path of the assembly containing the tasks.
        /// </param>
        [InlineData("NuGet.Build.Tasks.dll")]
        [InlineData("Microsoft.Build.Tasks.Core.dll")]
        [InlineData("Sdks/Microsoft.NET.Sdk/tools/net8.0/Microsoft.NET.Build.Tasks.dll")]
        [Theory(DisplayName = "TaskScanner can get tasks from framework task assembly ")]
        public void Scan_FrameworkTaskAssembly_Success(string fileName)
        {
            string taskAssemblyFile = GetFrameworkTaskAssemblyFile(fileName);
            Assert.True(File.Exists(taskAssemblyFile),
                $"Task assembly '{taskAssemblyFile}' exists"
            );

            MSBuildTaskAssemblyMetadata metadata = MSBuildTaskScanner.GetAssemblyTaskMetadata(taskAssemblyFile, CurrentDotnetInfo.Sdk, Log);
            Assert.NotNull(metadata);

            Assert.NotEmpty(metadata.Tasks);

            foreach (MSBuildTaskMetadata taskMetadata in metadata.Tasks.OrderBy(task => task.TypeName))
                TestOutput.WriteLine("Found task '{0}'.", taskMetadata.TypeName);
        }

        /// <summary>
        ///     Retrieve the full path of a task assembly supplied as part of the current framework.
        /// </summary>
        /// <param name="assemblyFileName">
        ///     The relative filename of the task assembly.
        /// </param>
        /// <returns>
        ///     The full path of the task assembly.
        /// </returns>
        string GetFrameworkTaskAssemblyFile(string assemblyFileName)
        {
            if (string.IsNullOrWhiteSpace(assemblyFileName))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(assemblyFileName)}.", nameof(assemblyFileName));

            return Path.Combine(CurrentDotnetInfo.BaseDirectory,
                assemblyFileName.Replace('/', Path.DirectorySeparatorChar)
            );
        }
    }
}
