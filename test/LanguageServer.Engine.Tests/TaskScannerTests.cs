using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable xUnit2013 // Do not use equality check to check for collection size.

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
            LogLevelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Verbose;


            Log.Information("Runtime Directory = {RuntimeDirectory}", RuntimeEnvironment.GetRuntimeDirectory());

            if (EnableDotNetHostDiagnostics)
            {
                Environment.SetEnvironmentVariable("MSBUILD_PROJECT_TOOLS_DOTNET_HOST_DIAGNOSTICS", "1");
                RuntimeInfo = DotNetRuntimeInfo.GetCurrent();
                Environment.SetEnvironmentVariable("MSBUILD_PROJECT_TOOLS_DOTNET_HOST_DIAGNOSTICS", null);
            }
            else
                RuntimeInfo = DotNetRuntimeInfo.GetCurrent();

            Assert.NotNull(RuntimeInfo.BaseDirectory);
        }

        /// <summary>
        ///     Information about the current .NET runtime.
        /// </summary>
        DotNetRuntimeInfo RuntimeInfo { get; }

        /// <summary>
        ///     Verify that the task scanner can retrieve task metadata from an assembly.
        /// </summary>
        /// <param name="fileName">
        ///     The relative path of the assembly containing the tasks.
        /// </param>
        [InlineData("NuGet.Build.Tasks.dll")]
        [InlineData("Microsoft.Build.Tasks.Core.dll")]
        [InlineData("Sdks/Microsoft.NET.Sdk/tools/net6.0/Microsoft.NET.Build.Tasks.dll")]
        [Theory(DisplayName = "TaskScanner can get tasks from framework task assembly ")]
        public void Scan_FrameworkTaskAssembly_Success(string fileName)
        {
            string taskAssemblyFile = GetFrameworkTaskAssemblyFile(fileName);
            Assert.True(File.Exists(taskAssemblyFile),
                $"Task assembly '{taskAssemblyFile}' exists"
            );

            MSBuildTaskAssemblyMetadata metadata = MSBuildTaskScanner.GetAssemblyTaskMetadata(taskAssemblyFile, RuntimeInfo.Sdk, Log);
            Assert.NotNull(metadata);

            Assert.NotEqual(0, metadata.Tasks.Count);

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
                throw new System.ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(assemblyFileName)}.", nameof(assemblyFileName));

            return Path.Combine(RuntimeInfo.BaseDirectory,
                assemblyFileName.Replace('/', Path.DirectorySeparatorChar)
            );
        }
    }
}
