using NuGet.Versioning;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     Information about the .NET Core runtime.
    /// </summary>
    public partial class DotNetRuntimeInfo
    {
        [GeneratedRegex(@"(?<SdkVersion>.*) \[(?<SdkBaseDirectory>.*)\]")]
        private static partial Regex SdkInfoParser();

        [GeneratedRegex(@"(?<RuntimeName>.*) (?<RuntimeVersion>.*) \[(?<RuntimeBaseDirectory>.*)\]")]
        private static partial Regex RuntimeInfoParser();

        /// <summary>
        ///     Information, if known, about the current .NET runtime (i.e. host).
        /// </summary>
        public DotnetRuntimeInfo Runtime { get; set; } = DotnetRuntimeInfo.Empty;

        /// <summary>
        ///     The .NET runtime (host) version.
        /// </summary>
        public string RuntimeVersion => Runtime?.Version?.ToString();

        /// <summary>
        ///     Information, if known, about the current .NET SDK.
        /// </summary>
        public DotnetSdkInfo Sdk { get; set; } = DotnetSdkInfo.Empty;

        /// <summary>
        ///     The .NET SDK version.
        /// </summary>
        public string SdkVersion => Sdk?.Version?.ToString();

        /// <summary>
        ///     The .NET SDK base directory.
        /// </summary>
        public string BaseDirectory => Sdk?.BaseDirectory;

        /// <summary>
        ///     Create a new <see cref="DotNetRuntimeInfo"/>.
        /// </summary>
        public DotNetRuntimeInfo()
        {
        }

        /// <summary>
        ///     Get information about the current .NET Core runtime.
        /// </summary>
        /// <param name="baseDirectory">
        ///     An optional base directory where dotnet.exe should be run (this may affect the version it reports due to global.json).
        /// </param>
        /// <param name="logger">
        ///     An optional <see cref="ILogger"/> to use for diagnostic purposes (if not specified, the static <see cref="Log.Logger"/> will be used).
        /// </param>
        /// <returns>
        ///     A <see cref="DotNetRuntimeInfo"/> containing the runtime information.
        /// </returns>
        public static DotNetRuntimeInfo GetCurrent(string baseDirectory = null, ILogger logger = null)
        {
            logger ??= Log.Logger;

            SemanticVersion sdkVersion;

            bool enableDotnetHostDiagnostics = false;
            if (Environment.GetEnvironmentVariable("MSBUILD_PROJECT_TOOLS_DOTNET_HOST_DIAGNOSTICS") == "1")
                enableDotnetHostDiagnostics = true;

            int dotNetExitCode;
            TextReader dotnetOutput;

            (dotNetExitCode, dotnetOutput) = InvokeDotNetHost("--version", baseDirectory, logger, enableHostTracing: enableDotnetHostDiagnostics);
            using (dotnetOutput)
            {
                if (dotNetExitCode == DotNetExitCodes.CannotResolveTargetSdkOrRuntime)
                {
                    logger.Error("Cannot resolve the target .NET SDK tooling or runtime. Please verify that the SDK version referenced in global.json (or a compatible runtime that matches the configured roll-forward policy) is correctly installed.");

                    throw new Exception("Cannot resolve the target .NET SDK tooling or runtime. Please verify that the SDK version referenced in global.json (or a compatible runtime that matches the configured roll-forward policy) is correctly installed.");
                }
                else if (dotNetExitCode != DotNetExitCodes.Success)
                    throw new Exception("Failed to determine current .NET version.");

                sdkVersion = ParseDotNetVersionOutput(dotnetOutput);

                logger.Verbose("Discovered .NET SDK v{SdkVersion:l}.", sdkVersion);
            }

            DotnetSdkInfo targetSdk;

            (dotNetExitCode, dotnetOutput) = InvokeDotNetHost("--list-sdks", baseDirectory, logger);
            using (dotnetOutput)
            {
                if (dotNetExitCode != DotNetExitCodes.Success)
                    throw new Exception("Failed to discover available .NET SDKs.");

                List<DotnetSdkInfo> discoveredSdks = ParseDotNetListSdksOutput(dotnetOutput);

                targetSdk = discoveredSdks.Find(sdk => sdk.Version == sdkVersion);
                if (targetSdk != null)
                    logger.Verbose("Target .NET SDK is v{SdkVersion:l} in {SdkBaseDirectory}.", targetSdk.Version, targetSdk.BaseDirectory);
                else
                    logger.Error("Cannot find SDK v{SdkVersion} via 'dotnet --list-sdks'.", sdkVersion);
            }

            DotnetRuntimeInfo hostRuntime = null;

            (dotNetExitCode, dotnetOutput) = InvokeDotNetHost("--list-runtimes", baseDirectory, logger);
            using (dotnetOutput)
            {
                if (dotNetExitCode != DotNetExitCodes.Success)
                    throw new Exception("Failed to discover available .NET runtimes.");

                List<DotnetRuntimeInfo> discoveredRuntimes = ParseDotNetListRuntimesOutput(dotnetOutput);

                // AF: As far as I can tell, the host runtime version always corresponds to the latest version of the "Microsoft.NETCore.App" runtime (i.e. is not affected by global.json).

                hostRuntime = discoveredRuntimes
                    .Where(runtime => runtime.Name == WellKnownDotnetRuntimes.NetCore)
                    .OrderByDescending(runtime => runtime.Version)
                    .FirstOrDefault();

                if (hostRuntime != null)
                    logger.Verbose(".NET host runtime is v{RuntimeVersion:l} ({RuntimeName}).", hostRuntime.Version, hostRuntime.Name);
                else
                    logger.Error("Failed to discover any runtimes via 'dotnet --list-runtimes'.");
            }

            return new DotNetRuntimeInfo
            {
                Runtime = hostRuntime,
                Sdk = targetSdk,
            };
        }

        /// <summary>
        ///     Parse the output of "dotnet --version".
        /// </summary>
        /// <param name="dotnetVersionOutput">
        ///     A <see cref="TextReader"/> containing the output of "dotnet --version".
        /// </param>
        /// <returns>
        ///     A <see cref="SemanticVersion"/> representing the .NET SDK version.
        /// </returns>
        public static SemanticVersion ParseDotNetVersionOutput(TextReader dotnetVersionOutput)
        {
            if (dotnetVersionOutput == null)
                throw new ArgumentNullException(nameof(dotnetVersionOutput));

            // Output of "dotnet --version" is expected to be a single line containing a valid semantic version (SemVer).
            string rawVersion = dotnetVersionOutput.ReadToEnd().Trim();
            if (rawVersion.Length == 0)
                throw new InvalidOperationException("The 'dotnet --version' command did not return any output.");

            if (!SemanticVersion.TryParse(rawVersion, out SemanticVersion parsedVersion))
                throw new FormatException($"The 'dotnet --version' command did not return valid version information ('{rawVersion}' is not a valid semantic version).");

            return parsedVersion;
        }

        /// <summary>
        ///     Parse the output of "dotnet --list-sdks".
        /// </summary>
        /// <param name="dotNetListSdksOutput">
        ///     A <see cref="TextReader"/> containing the output of "dotnet --info".
        /// </param>
        /// <returns>
        ///     A list of <see cref="DotnetSdkInfo"/> representing the discovered SDKs.
        /// </returns>
        public static List<DotnetSdkInfo> ParseDotNetListSdksOutput(TextReader dotNetListSdksOutput)
        {
            if (dotNetListSdksOutput == null)
                throw new ArgumentNullException(nameof(dotNetListSdksOutput));

            var dotnetSdks = new List<DotnetSdkInfo>();

            string currentLine;
            while ((currentLine = dotNetListSdksOutput.ReadLine()) != null)
            {
                Match parseResult = SdkInfoParser().Match(currentLine);
                if (!parseResult.Success)
                    continue;

                if (!SemanticVersion.TryParse(parseResult.Groups["SdkVersion"].Value, out SemanticVersion sdkVersion))
                    continue;

                string sdkBaseDirectory = Path.Combine(parseResult.Groups["SdkBaseDirectory"].Value, sdkVersion.ToString());

                dotnetSdks.Add(
                    new DotnetSdkInfo(sdkVersion, sdkBaseDirectory)
                );
            }

            return dotnetSdks;
        }

        /// <summary>
        ///     Parse the output of "dotnet --list-runtimes".
        /// </summary>
        /// <param name="dotNetListRuntimesOutput">
        ///     A <see cref="TextReader"/> containing the output of "dotnet --runtimes".
        /// </param>
        /// <returns>
        ///     A list of <see cref="DotnetRuntimeInfo"/> representing the discovered runtimes.
        /// </returns>
        public static List<DotnetRuntimeInfo> ParseDotNetListRuntimesOutput(TextReader dotNetListRuntimesOutput)
        {
            if (dotNetListRuntimesOutput == null)
                throw new ArgumentNullException(nameof(dotNetListRuntimesOutput));

            var dotnetRuntimes = new List<DotnetRuntimeInfo>();

            string currentLine;
            while ((currentLine = dotNetListRuntimesOutput.ReadLine()) != null)
            {
                Match parseResult = RuntimeInfoParser().Match(currentLine);
                if (!parseResult.Success)
                    continue;

                if (!SemanticVersion.TryParse(parseResult.Groups["RuntimeVersion"].Value, out SemanticVersion runtimeVersion))
                    continue;

                string runtimeName = parseResult.Groups["RuntimeName"].Value;

                dotnetRuntimes.Add(
                    new DotnetRuntimeInfo(runtimeName, runtimeVersion)
                );
            }

            return dotnetRuntimes;
        }

        /// <summary>
        ///     Invoke the .NET host ("dotnet").
        /// </summary>
        /// <param name="commandLineArguments">
        ///     Command-line arguments to be passed to the host.
        /// </param>
        /// <param name="baseDirectory">
        ///     The directory where the host will be invoked.
        /// </param>
        /// <param name="logger">
        ///     The logger for diagnostic messages.
        /// </param>
        /// <param name="enableHostTracing">
        ///     Enable host-level tracing?
        /// </param>
        /// <returns>
        ///     The process exit code, and a <see cref="TextReader"/> containing the program output (STDOUT and STDERR).
        /// </returns>
        static (int ExitCode, TextReader StdOut) InvokeDotNetHost(string commandLineArguments, string baseDirectory, ILogger logger, bool enableHostTracing = false)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            var dotnetHostProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                    WorkingDirectory = baseDirectory,
                    Arguments = commandLineArguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Environment =
                    {
                        ["COREHOST_TRACE"] = enableHostTracing ? "1" : "0",
                    }
                },
                EnableRaisingEvents = true
            };

            using (dotnetHostProcess)
            {
                // For logging purposes.
                string command = $"{dotnetHostProcess.StartInfo.FileName} {dotnetHostProcess.StartInfo.Arguments}";

                // Buffer the output locally (otherwise, the process may hang if it fills up its STDOUT / STDERR buffer).
                var stdOutBuffer = new StringBuilder();
                dotnetHostProcess.OutputDataReceived += (sender, args) =>
                {
                    lock (stdOutBuffer)
                    {
                        stdOutBuffer.AppendLine(args.Data);
                    }
                };
                var stdErrBuffer = new StringBuilder();
                dotnetHostProcess.ErrorDataReceived += (sender, args) =>
                {
                    lock (stdErrBuffer)
                    {
                        stdErrBuffer.AppendLine(args.Data);
                    }
                };

                logger.Debug("Launching {Command}...", command);

                dotnetHostProcess.Start();

                logger.Debug("Launched {Command}. Waiting for process {TargetProcessId} to terminate...", command, dotnetHostProcess.Id);

                // Asynchronously start reading from STDOUT / STDERR.
                dotnetHostProcess.BeginOutputReadLine();
                dotnetHostProcess.BeginErrorReadLine();

                bool exited = dotnetHostProcess.WaitForExit(milliseconds: 5000);
                if (!exited)
                {
                    logger.Error("Timed out after waiting 5 seconds for {Command} to exit.", command);

                    throw new TimeoutException($"Timed out after waiting 5 seconds for '{command}' to exit.");
                }

                // Ensure redirected STDOUT and STDERROR have been flushed (tintoy/msbuild-project-tools-vscode#105).
                logger.Debug("Waiting for redirected STDOUT/STDERR streams to complete for process {TargetProcessId}...", dotnetHostProcess.Id);
                dotnetHostProcess.WaitForExit();

                logger.Debug("{Command} terminated with exit code {ExitCode}.", command, dotnetHostProcess.ExitCode);

                string stdOut;
                lock (stdOutBuffer)
                {
                    stdOut = stdOutBuffer.ToString();
                }
                string stdErr;
                lock (stdErrBuffer)
                {
                    stdErr = stdErrBuffer.ToString();
                }

                if (dotnetHostProcess.ExitCode != 0 || logger.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
                {
                    if (!string.IsNullOrWhiteSpace(stdOut))
                        logger.Debug("{Command} returned the following text on STDOUT:\n\n{DotNetOutput:l}", command, stdOut);
                    else
                        logger.Debug("{Command} returned no output on STDOUT.", command);

                    if (!string.IsNullOrWhiteSpace(stdErr))
                        logger.Debug("{Command} returned the following text on STDERR:\n\n{DotNetOutput:l}", command, stdErr);
                    else
                        logger.Debug("{Command} returned no output on STDERR.", command);
                }

                return (
                    ExitCode: dotnetHostProcess.ExitCode,
                    StdOut: new StringReader(stdOut)
                );
            }
        }
    }

    /// <summary>
    ///     Information about a discovered version of the .NET SDK.
    /// </summary>
    /// <param name="Version">
    ///     The SDK version.
    /// </param>
    /// <param name="BaseDirectory">
    ///     The SDK base directory.
    /// </param>
    public record DotnetSdkInfo(SemanticVersion Version, string BaseDirectory)
    {
        /// <summary>
        ///     Empty <see cref="DotnetRuntimeInfo"/>.
        /// </summary>
        public static readonly DotnetSdkInfo Empty = new DotnetSdkInfo(null, null);
    };

    /// <summary>
    ///     Information about a discovered .NET runtime.
    /// </summary>
    /// <param name="Name">
    ///     The runtime name (e.g. "Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App", "Microsoft.AspNetCore.App", etc).
    /// </param>
    /// <param name="Version">
    ///     The SDK version.
    /// </param>
    public record DotnetRuntimeInfo(string Name, SemanticVersion Version)
    {
        /// <summary>
        ///     Empty <see cref="DotnetRuntimeInfo"/>.
        /// </summary>
        public static readonly DotnetRuntimeInfo Empty = new DotnetRuntimeInfo(null, null);
    };

    /// <summary>
    ///     Well-known names for various .NET runtimes.
    /// </summary>
    public static class WellKnownDotnetRuntimes
    {
        /// <summary>
        ///     The name of the .NET runtime for ASP.NET.
        /// </summary>
        public static readonly string AspNetCore = "Microsoft.AspNetCore.App";

        /// <summary>
        ///     The name of the (default) .NET / .NET Core runtime.
        /// </summary>
        public static readonly string NetCore = "Microsoft.NETCore.App";

        /// <summary>
        ///     The name of the .NET runtime for Windows Desktop.
        /// </summary>
        public static readonly string WindowsDesktop = "Microsoft.WindowsDesktop.App";
    }

    /// <summary>
    ///     Well-known process exit codes for the "dotnet" executable.
    /// </summary>
    public static class DotNetExitCodes
    {
        /// <summary>
        ///     The dotnet host exit code indicating that the requested operation was successful.
        /// </summary>
        public static readonly int Success = 0;

        /// <summary>
        ///     The dotnet host exit code indicating that the target SDK or runtime version cannot be resolved.
        /// </summary>
        public static readonly int CannotResolveTargetSdkOrRuntime = -2147450735;
    }
}
