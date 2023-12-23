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
    public class DotNetRuntimeInfo
    {
        /// <summary>
        ///     The minimum SDK version to be considered .NET 6.x.
        /// </summary>
        static readonly SemanticVersion Sdk60Version = new SemanticVersion(6, 0, 101);

        /// <summary>
        ///     Regular expression to parse SDK information from "dotnet --list-sdks".
        /// </summary>
        static readonly Regex SdkInfoParser = new Regex(@"(?<SdkVersion>.*) \[(?<SdkBaseDirectory>.*)\]");

        /// <summary>
        ///     Regular expression to parse SDK information from "dotnet --list-runtimes".
        /// </summary>
        static readonly Regex RuntimeInfoParser = new Regex(@"(?<RuntimeName>.*) (?<RuntimeVersion>.*) \[(?<RuntimeBaseDirectory>.*)\]");

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

            using (TextReader dotnetVersionOutput = InvokeDotNetHost("--version", baseDirectory, logger, enableHostTracing: enableDotnetHostDiagnostics))
            {
                sdkVersion = ParseDotNetVersionOutput(dotnetVersionOutput);

                logger.Verbose("Discovered .NET SDK v{SdkVersion:l}.", sdkVersion);
            }

            if (sdkVersion >= Sdk60Version)
            {
                // From .NET 6.x onwards, we can rely on "dotnet --list-sdks" and "dotnet --list-runtimes" to give us the information we need.
                logger.Verbose("Using new SDK discovery logic because .NET SDK v{SdkVersion:l} is greater than or equal to the minimum required v6 SDK version (v{MinSdkVersion:l}).", sdkVersion, Sdk60Version);

                DotnetSdkInfo targetSdk;

                using (TextReader dotnetListSdksOutput = InvokeDotNetHost("--list-sdks", baseDirectory, logger))
                {
                    List<DotnetSdkInfo> discoveredSdks = ParseDotNetListSdksOutput(dotnetListSdksOutput);

                    targetSdk = discoveredSdks.Find(sdk => sdk.Version == sdkVersion);
                    if (targetSdk != null)
                        logger.Verbose("Target .NET SDK is v{SdkVersion:l} in {SdkBaseDirectory}.", targetSdk.Version, targetSdk.BaseDirectory);
                    else
                        logger.Error("Cannot find SDK v{SdkVersion} via 'dotnet --list-sdks'.", sdkVersion);
                }

                DotnetRuntimeInfo hostRuntime = null;

                using (TextReader dotnetListRuntimesOutput = InvokeDotNetHost("--list-runtimes", baseDirectory, logger))
                {
                    List<DotnetRuntimeInfo> discoveredRuntimes = ParseDotNetListRuntimesOutput(dotnetListRuntimesOutput);

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
            else
            {
                // Fall back to legacy parser.
                logger.Verbose("Using legacy (pre-v6) SDK discovery logic because .NET SDK v{SdkVersion:l} is less than the minimum required v6 SDK version (v{MinSdkVersion:l}).", sdkVersion, Sdk60Version);

                using TextReader dotnetInfoOutput = InvokeDotNetHost("--info", baseDirectory, logger);
                
                return ParseDotNetInfoOutput(dotnetInfoOutput);
            }
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
                Match parseResult = SdkInfoParser.Match(currentLine);
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
                Match parseResult = RuntimeInfoParser.Match(currentLine);
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
        ///     A <see cref="TextReader"/> containing the program output (STDOUT and STDERR).
        /// </returns>
        static TextReader InvokeDotNetHost(string commandLineArguments, string baseDirectory, ILogger logger, bool enableHostTracing = false)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            Process dotnetHostProcess = new Process
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
                StringBuilder stdOutBuffer = new StringBuilder();
                dotnetHostProcess.OutputDataReceived += (sender, args) =>
                {
                    lock (stdOutBuffer)
                    {
                        stdOutBuffer.AppendLine(args.Data);
                    }
                };
                StringBuilder stdErrBuffer = new StringBuilder();
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

                return new StringReader(stdOut);
            }
        }

        /// <summary>
        ///     Parse the output of "dotnet --info" into a <see cref="DotNetRuntimeInfo"/>.
        /// </summary>
        /// <param name="dotnetInfoOutput">
        ///     A <see cref="TextReader"/> containing the output of "dotnet --info".
        /// </param>
        /// <returns>
        ///     The <see cref="DotNetRuntimeInfo"/>.
        /// </returns>
        public static DotNetRuntimeInfo ParseDotNetInfoOutput(TextReader dotnetInfoOutput)
        {
            if (dotnetInfoOutput == null)
                throw new ArgumentNullException(nameof(dotnetInfoOutput));

            DotNetRuntimeInfo runtimeInfo = new DotNetRuntimeInfo();

            DotnetInfoSection currentSection = DotnetInfoSection.Start;

            string currentLine;
            while ((currentLine = dotnetInfoOutput.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(currentLine))
                    continue;

                if (!currentLine.StartsWith(" ") && currentLine.EndsWith(":"))
                {
                    currentSection++;

                    if (currentSection > DotnetInfoSection.RuntimeEnvironment)
                        break;

                    continue;
                }

                string[] property = currentLine.Split(new char[] { ':' }, count: 2);
                if (property.Length != 2)
                    continue;

                property[0] = property[0].Trim();
                property[1] = property[1].Trim();

                switch (currentSection)
                {
                    case DotnetInfoSection.ProductInformation:
                    {
                        switch (property[0])
                        {
                            case "Version":
                            {
                                runtimeInfo.Sdk = runtimeInfo.Sdk with
                                {
                                    Version = SemanticVersion.Parse(property[1]),
                                };

                                break;
                            }
                        }

                        break;
                    }
                    case DotnetInfoSection.RuntimeEnvironment:
                    {
                        switch (property[0])
                        {
                            case "Base Path":
                            {
                                runtimeInfo.Sdk = runtimeInfo.Sdk with
                                {
                                    BaseDirectory = property[1],
                                };

                                break;
                            }
                        }

                        break;
                    }
                    case DotnetInfoSection.Host:
                    {
                        switch (property[0])
                        {
                            case "Version":
                            {
                                runtimeInfo.Runtime = runtimeInfo.Runtime with
                                {
                                    Version = SemanticVersion.Parse(property[1]),
                                };

                                break;
                            }
                        }

                        break;
                    }
                }
            }

            return runtimeInfo;
        }

        /// <summary>
        ///     Well-known sections returned by "dotnet --info".
        /// </summary>
        /// <remarks>
        ///     Since the section titles returned by "dotnet --info" are now localized, we have to resort to this (more-fragile) method of parsing the output.
        /// </remarks>
        enum DotnetInfoSection
        {
            /// <summary>
            ///     Start of output.
            /// </summary>
            Start = 0,

            /// <summary>
            ///     The product information section (e.g. ".NET Core SDK (reflecting any global.json)").
            /// </summary>
            ProductInformation = 1,

            /// <summary>
            ///     The runtime environment section (e.g. "Runtime Environment").
            /// </summary>
            RuntimeEnvironment = 2,

            /// <summary>
            ///     The host section (e.g. "Host (useful for support)").
            /// </summary>
            Host = 3,

            /// <summary>
            ///     The SDK list section (e.g. ".NET Core SDKs installed").
            /// </summary>
            SdkList = 4,

            /// <summary>
            ///     The runtime list section (e.g. ".NET Core runtimes installed").
            /// </summary>
            RuntimeList = 5
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
}
