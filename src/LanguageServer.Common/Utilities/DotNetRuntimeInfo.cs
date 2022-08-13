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
        ///     The .NET runtime (host) version.
        /// </summary>
        public string RuntimeVersion { get; set; }

        /// <summary>
        ///     The .NET SDK version.
        /// </summary>
        public string SdkVersion { get; set; }

        /// <summary>
        ///     The .NET Core base directory.
        /// </summary>
        public string BaseDirectory { get; set; }

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
            if (logger == null)
                logger = Log.Logger;

            SemanticVersion sdkVersion;

            using (TextReader dotnetVersionOutput = InvokeDotNetHost("--version", baseDirectory, logger))
            {
                sdkVersion = ParseDotNetVersionOutput(dotnetVersionOutput);
            }

            if (sdkVersion >= Sdk60Version)
            {
                // From .NET 6.x onwards, we can rely on "dotnet --list-sdks" and "dotnet --list-runtimes" to give us the information we need.

                DotnetSdkInfo targetSdk;

                using (TextReader dotnetListSdksOutput = InvokeDotNetHost("--list-sdks", baseDirectory, logger))
                {
                    List<DotnetSdkInfo> discoveredSdks = ParseDotNetListSdksOutput(dotnetListSdksOutput);
                    
                    targetSdk = discoveredSdks.Find(sdk => sdk.Version == sdkVersion);
                    if (targetSdk == null)
                    {
                        logger.Error("Cannot find SDK v{SdkVersion} via 'dotnet --list-sdks'.", sdkVersion);
                    }
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
                    
                    if (hostRuntime == null)
                        logger.Error("Failed to discover any runtimes via 'dotnet --list-runtimes'.");
                }

                return new DotNetRuntimeInfo
                {
                    RuntimeVersion = hostRuntime?.Version?.ToString(),
                    SdkVersion = sdkVersion.ToString(),
                    BaseDirectory = targetSdk?.BaseDirectory,
                };
            }
            else
            {
                // Fall back to legacy parser.

                using (TextReader dotnetInfoOutput = InvokeDotNetHost("--info", baseDirectory, logger))
                {
                    return ParseDotNetInfoOutput(dotnetInfoOutput);
                }
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

            string rawVersion = dotnetVersionOutput.ReadToEnd().Trim();

            return SemanticVersion.Parse(rawVersion);
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

                SemanticVersion sdkVersion;
                if (!SemanticVersion.TryParse(parseResult.Groups["SdkVersion"].Value, out sdkVersion))
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

                SemanticVersion runtimeVersion;
                if (!SemanticVersion.TryParse(parseResult.Groups["RuntimeVersion"].Value, out runtimeVersion))
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
        /// <returns>
        ///     A <see cref="TextReader"/> containing the program output (STDOUT and STDERR).
        /// </returns>
        static TextReader InvokeDotNetHost(string commandLineArguments, string baseDirectory, ILogger logger)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            Process dotnetInfoProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    WorkingDirectory = baseDirectory,
                    Arguments = commandLineArguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };
            using (dotnetInfoProcess)
            {
                // For logging purposes.
                string command = $"{dotnetInfoProcess.StartInfo.FileName} {dotnetInfoProcess.StartInfo.Arguments}";

                // Buffer the output locally (otherwise, the process may hang if it fills up its STDOUT / STDERR buffer).
                StringBuilder localOutputBuffer = new StringBuilder();
                dotnetInfoProcess.OutputDataReceived += (sender, args) =>
                {
                    lock (localOutputBuffer)
                    {
                        localOutputBuffer.AppendLine(args.Data);
                    }
                };
                dotnetInfoProcess.ErrorDataReceived += (sender, args) =>
                {
                    lock (localOutputBuffer)
                    {
                        localOutputBuffer.AppendLine(args.Data);
                    }
                };

                logger.Debug("Launching {Command}...", command);

                dotnetInfoProcess.Start();

                logger.Debug("Launched {Command}. Waiting for process {TargetProcessId} to terminate...", command, dotnetInfoProcess.Id);

                // Asynchronously start reading from STDOUT / STDERR.
                dotnetInfoProcess.BeginOutputReadLine();
                dotnetInfoProcess.BeginErrorReadLine();

                try
                {
                    dotnetInfoProcess.WaitForExit(milliseconds: 5000);
                }
                catch (TimeoutException exitTimedOut)
                {
                    logger.Error(exitTimedOut, "Timed out after waiting 5 seconds for {Command} to exit.", command);

                    throw new TimeoutException($"Timed out after waiting 5 seconds for '{command}' to exit.", exitTimedOut);
                }

                logger.Debug("{Command} terminated with exit code {ExitCode}.", command, dotnetInfoProcess.ExitCode);

                string processOutput;
                lock (localOutputBuffer)
                {
                    processOutput = localOutputBuffer.ToString();
                }

                if (dotnetInfoProcess.ExitCode != 0 || logger.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
                {
                    if (!String.IsNullOrWhiteSpace(processOutput))
                        logger.Debug("{Command} returned the following text on STDOUT / STDERR.\n\n{DotNetInfoOutput:l}", command, processOutput);
                    else
                        logger.Debug("{Command} returned no output on STDOUT / STDERR.");
                }

                return new StringReader(processOutput);
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
                if (String.IsNullOrWhiteSpace(currentLine))
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
                                runtimeInfo.SdkVersion = property[1];

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
                                runtimeInfo.BaseDirectory = property[1];

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
                                runtimeInfo.RuntimeVersion = property[1];

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
        ///     Since the section titles returned by "dotnet --info" are now localised, we have to resort to this (more-fragile) method of parsing the output.
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
    public record DotnetSdkInfo(SemanticVersion Version, string BaseDirectory);

    /// <summary>
    ///     Information about a discovered .NET runtime.
    /// </summary>
    /// <param name="Name">
    ///     The runtime name (e.g. "Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App", "Microsoft.AspNetCore.App", etc).
    /// </param>
    /// <param name="Version">
    ///     The SDK version.
    /// </param>
    public record DotnetRuntimeInfo(string Name, SemanticVersion Version);

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
