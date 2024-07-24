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

            DotNetHostExitCode dotNetExitCode;
            TextReader dotnetOutput;

            (dotNetExitCode, dotnetOutput) = InvokeDotNetHost("--version", baseDirectory, logger, enableHostTracing: enableDotnetHostDiagnostics);
            using (dotnetOutput)
            {
                if (dotNetExitCode == DotNetHostExitCode.LibHostSdkFindFailure)
                {
                    logger.Error("Cannot resolve the target .NET SDK tooling or runtime. Please verify that the SDK version referenced in global.json (or a compatible runtime that matches the configured roll-forward policy) is correctly installed.");

                    throw new Exception("Cannot resolve the target .NET SDK tooling or runtime. Please verify that the SDK version referenced in global.json (or a compatible runtime that matches the configured roll-forward policy) is correctly installed.");
                }
                else if (dotNetExitCode != DotNetHostExitCode.Success)
                    throw new Exception("Failed to determine current .NET version.");

                sdkVersion = ParseDotNetVersionOutput(dotnetOutput);

                logger.Verbose("Discovered .NET SDK v{SdkVersion:l}.", sdkVersion);
            }

            DotnetSdkInfo targetSdk;

            (dotNetExitCode, dotnetOutput) = InvokeDotNetHost("--list-sdks", baseDirectory, logger);
            using (dotnetOutput)
            {
                if (dotNetExitCode != DotNetHostExitCode.Success)
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
                if (dotNetExitCode != DotNetHostExitCode.Success)
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
        static (DotNetHostExitCode ExitCode, TextReader StdOut) InvokeDotNetHost(string commandLineArguments, string baseDirectory, ILogger logger, bool enableHostTracing = false)
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

                var hostExitCode = (DotNetHostExitCode)dotnetHostProcess.ExitCode;

                logger.Debug("{Command} terminated with exit code {ExitCode:X} ({ExitCodeName}).", command, (int)hostExitCode, hostExitCode);

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
                    ExitCode: hostExitCode,
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
    /// <remarks>
    ///     <seealso href="https://github.com/dotnet/runtime/blob/d123560a23078989f9563b83fa49a24802e88378/docs/design/features/host-error-codes.md"/>
    /// </remarks>
    public enum DotNetHostExitCode
        : int
    {
        /// <summary>
        ///     Operation was successful.
        /// </summary>
        Success = 0,

        /// <summary>
        ///     Initialization was successful, but another host context is already initialized, so the returned context is "secondary" (the requested context was otherwise fully compatible with the already initialized context).
        /// </summary>
        /// <remarks>
        ///     Probably not used as an actual exit code; returned by <c>hostfxr_initialize_for_runtime_config</c> if it's called when the host is already initialized in the process.
        /// </remarks>
        Success_HostAlreadyInitialized = 0x00000001,

        /// <summary>
        ///     Initialization was successful, but another host context is already initialized and the requested context specified some runtime properties which are not the same (either in value or in presence) to the already initialized context.
        /// </summary>
        /// <remarks>
        ///     Probably not used as an actual exit code; returned by <c>hostfxr_initialize_for_runtime_config</c> if it's called when the host is already initialized in the process.
        /// </remarks>
        Success_DifferentRuntimeProperties = 0x00000002,

        /// <summary>
        ///     One of the specified arguments for the operation is invalid.
        /// </summary>
        InvalidArgFailure = unchecked((int)0x80008081),

        /// <summary>
        ///     There was a failure loading a dependent library.
        /// </summary>
        /// <remarks>
        ///     If any of the hosting components calls <c>LoadLibrary</c>/<c>dlopen</c> on a dependent library and the call fails, this error code is returned.
        ///     The most common case for this failure is if the dependent library is missing some of its dependencies (for example the necessary CRT is missing on the machine).
        ///     
        ///      Probably means corrupted or incomplete installation.
        /// </remarks>
        CoreHostLibLoadFailure = unchecked((int)0x80008082),

        /// <summary>
        ///     One of the dependent libraries is missing.
        /// </summary>
        /// <remarks>
        ///     Typically when the <c>hostfxr</c>, <c>hostpolicy</c>, or <c>coreclr</c> dynamic libraries are not present in the expected locations.
        ///     
        ///     Probably means corrupted or incomplete installation.
        /// </remarks>
        CoreHostLibMissingFailure = unchecked((int)0x80008083),

        /// <summary>
        ///     One of the dependent libraries is missing a required entry point.
        /// </summary>
        CoreHostEntryPointFailure = unchecked((int)0x80008084),

        /// <summary>
        ///     The hosting component is trying to use the path to the current module (the hosting component itself) and, from it, deduce the location of the installation, but either the location of the current module could not be determined (some weird OS call failure) or the location is not in the right place relative to other expected components.
        /// </summary>
        /// <remarks>
        ///     For example the <c>hostfxr</c> may look at its location and try to deduce the location of the <c>shared</c> folder with the framework from it; it assumes the typical install layout on disk but if that doesn't work, then this error will be returned.
        /// </remarks>
        CoreHostCurHostFindFailure = unchecked((int)0x80008085),

        /// <summary>
        ///     The <c>coreclr</c> library could not be found.
        /// </summary>
        /// <remarks>
        ///     The hosting layer (<c>hostpolicy</c>) looks for the <c>coreclr</c> library either next to the app itself (for self-contained) or in the root framework (for framework-dependent).
        ///     This search can be done purely by looking at disk or more commonly by looking into the respective <c>.deps.json</c>. If the <c>coreclr</c> library is missing in <c>.deps.json</c> or it's there but doesn't exist on disk, then this error is returned.
        /// </remarks>
        CoreClrResolveFailure = unchecked((int)0x80008087),

        /// <summary>
        ///     The loaded <c>coreclr</c> library doesn't have one of the required entry points.
        /// </summary>
        CoreClrBindFailure = unchecked((int)0x80008088),

        /// <summary>
        ///     The call to <c>coreclr_initialize</c> failed.
        /// </summary>
        /// <remarks>
        ///     The actual error returned by coreclr is reported in the error message.
        /// </remarks>
        CoreClrInitFailure = unchecked((int)0x80008089),

        /// <summary>
        ///     The call to <c>coreclr_execute_assembly</c> failed.
        /// </summary>
        /// <remarks>
        ///     Note that this failure does not relate to the app's exit code; it occurs if <c>coreclr</c> failed to run the app itself.
        /// </remarks>
        CoreClrExeFailure = unchecked((int)0x8000808a),

        /// <summary>
        ///     Initialization of the <c>hostpolicy</c> dependency resolver failed.
        /// </summary>
        /// <remarks>
        ///     Possible causes:
        ///     <list type="bullet">
        ///         <item>One of the <c>.deps.json</c> files is invalid (invalid JSON, or missing required properties and so on).</item>
        ///         <item>One of the frameworks or the app is missing a required <c>.deps.json</c> file.</item>
        ///     </list>
        /// </remarks>
        ResolverInitFailure = unchecked((int)0x8000808b),

        /// <summary>
        ///     Resolution of dependencies in <c>hostpolicy</c> failed.
        /// </summary>
        /// <remarks>
        ///     This can have several different causes but, in general, it means that one of the processed <c>.deps.json</c> contains an entry for a file which could not found, or its resolution failed for some other reason (conflict for example).
        /// </remarks>
        ResolverResolveFailure = unchecked((int)0x8000808c),

        /// <summary>
        ///     Failure to determine the location of the current executable.
        /// </summary>
        /// <remarks>
        ///     The hosting layer uses the current executable path to deduce the install location (in some cases). If that path can't be obtained (OS call fails, or the returned path doesn't exist), then this error is returned.
        /// </remarks>
        LibHostCurExeFindFailure = unchecked((int)0x8000808d),

        /// <summary>
        ///     Initialization of the <c>hostpolicy</c> library failed.
        /// </summary>
        /// <remarks>
        ///     The <c>corehost_load</c> method takes a structure with lot of initialization parameters.
        ///     If the version of that structure doesn't match the expected value, this error code is returned.
        ///     
        ///     This would in general mean incompatibility between the <c>hostfxr</c> and <c>hostpolicy</c>, which should really only happen if somehow a newer <c>hostpolicy</c> is used by older <c>hostfxr</c>.
        ///     Typically, that indicates a corrupted installation.
        /// </remarks>
        LibHostInitFailure = unchecked((int)0x8000808e),

        /// <summary>
        ///     Failure to find the requested SDK.
        /// </summary>
        /// <remarks>
        ///     This happens in the <c>hostfxr</c> when an SDK (also called CLI) command is used with dotnet.
        ///     In this case, the hosting layer tries to find an installed .NET SDK to run the command on.
        ///     The search is based on deduced install location and on the requested version (potentially from a <c>global.json</c> file).
        ///     If either no matching SDK version can be found, or that version exists, but it's missing the dotnet.dll file, this error code is returned.
        /// </remarks>
        LibHostSdkFindFailure = unchecked((int)0x80008091),

        /// <summary>
        ///     Arguments to <c>hostpolicy</c> are invalid.
        /// </summary>
        /// <remarks>
        ///     This is used in three unrelated places in the <c>hostpolicy</c> but, in all cases, it means that the component calling <c>hostpolicy</c> did something wrong
        /// </remarks>
        LibHostInvalidArgs = unchecked((int)0x80008092),

        /// <summary>
        ///     The .runtimeconfig.json file is invalid.
        /// </summary>
        /// <remarks>
        ///     The reasons for this failure can include:
        ///     
        ///     <list type="bullet">
        ///         <item>Failure to read from the file</item>
        ///         <item>Invalid JSON</item>
        ///         <item>Invalid value for a property (for example number for property which requires a string)</item>
        ///         <item>Missing required property</item>
        ///         <item>Other inconsistencies (for example <c>rollForward</c> and <c>applyPatches</c> are not allowed to be specified in the same config file)</item>
        ///         <item>Any of the above failures reading the <c>.runtimecofig.dev.json</c> file</item>
        ///         <item>Self-contained <c>.runtimeconfig.json</c> used in <c>hostfxr_initialize_for_runtime_config</c>. Note that missing <c>.runtimconfig.json</c> is not an error (means self-contained app).</item>
        ///     </list>
        ///     
        ///     It is also used when there is a problem reading the CLSID map file in comhost.
        /// </remarks>
        InvalidConfigFile = unchecked((int)0x80008093),

        /// <summary>
        ///     Used internally when the command line for <c>dotnet.exe</c> doesn't contain path to the application to run.
        /// </summary>
        /// <remarks>
        ///     In this scenario, the command line is considered to be a CLI/SDK command. This error code should never be returned to an external caller.
        /// </remarks>
        AppArgNotRunnable = unchecked((int)0x80008094),

        /// <summary>
        ///     <c>apphost</c> failed to determine which application to run.
        /// </summary>
        /// <remarks>
        ///     The reasons for this failure can include:
        ///     
        ///     <list type="bullet">
        ///         <item>The <c>apphost</c> binary has not been imprinted with the path to the app to run (so freshly built <c>apphost.exe</c> from the branch will fail to run like this)</item>
        ///         <item>The <c>apphost</c> is a bundle (single-file exe) and it failed to extract correctly</item>
        ///         <item>The <c>apphost</c> binary has been imprinted with invalid .NET search options</item>
        ///     </list>
        /// </remarks>
        AppHostExeNotBoundFailure = unchecked((int)0x80008095),

        /// <summary>
        ///     It was not possible to find a compatible framework version.
        /// </summary>
        /// <remarks>
        ///     This exit code originates in <c>hostfxr</c> (<c>resolve_framework_reference</c>), and means that the app specified a reference to a framework in its <c>.runtimeconfig.json</c> which could not be resolved.
        ///     The failure to resolve can mean that no such framework is available on the disk, or that the available frameworks don't match the minimum version specified or that the roll forward options specified excluded all available frameworks.
        ///     
        ///     Typically, it would be returned if, for example, a 3.0 app is trying to run on a machine which has no 3.0 installed or a 32bit 3.0 app is running on a machine which has 3.0 installed but only for 64bit.
        /// </remarks>
        FrameworkMissingFailure = unchecked((int)0x80008096),

        /// <summary>
        ///     Returned by <c>hostfxr_get_native_search_directories</c> if the <c>hostpolicy</c> could not calculate the <c>NATIVE_DLL_SEARCH_DIRECTORIES</c>.
        /// </summary>
        HostApiFailed = unchecked((int)0x80008097),

        /// <summary>
        ///     Returned when the buffer specified to an API is not big enough to fit the requested value. 
        /// </summary>
        /// <remarks>
        ///     Can be returned from:
        ///     
        ///     <list type="bullet">
        ///         <item><c>hostfxr_get_runtime_properties</c></item>
        ///         <item><c>hostfxr_get_native_search_directories</c></item>
        ///         <item><c>get_hostfxr_path</c></item>
        ///     </list>
        /// </remarks>
        HostApiBufferTooSmall = unchecked((int)0x80008098),

        /// <summary>
        ///     Returned by <c>hostpolicy</c> if the corehost_main_with_output_buffer is called with unsupported host command.
        /// </summary>
        /// <remarks>
        ///     This error code means there is incompatibility between the <c>hostfxr</c> and <c>hostpolicy</c>. In reality, this should pretty much never happen.
        /// </remarks>
        LibHostUnknownCommand = unchecked((int)0x80008099),

        /// <summary>
        ///     Returned by <c>apphost</c> if the imprinted application path doesn't exist.
        /// </summary>
        /// <remarks>
        ///     This would happen if the app is built with an executable (the apphost) and the main app.dll is missing.
        /// </remarks>
        LibHostAppRootFindFailure = unchecked((int)0x8000809a),

        /// <summary>
        ///     Returned from <c>hostfxr_resolve_sdk2</c> when it fails to find a matching SDK.
        /// </summary>
        /// <remarks>
        ///     Similar to <c>LibHostSdkFindFailure</c>, but only used in the <c>hostfxr_resolve_sdk2</c>.
        /// </remarks>
        SdkResolverResolveFailure = unchecked((int)0x8000809b),

        /// <summary>
        ///     During processing of .runtimeconfig.json there were two framework references to the same framework which were not compatible.
        /// </summary>
        /// <remarks>
        ///     This can happen if the app specified a framework reference to a lower-level framework which is also specified by a higher-level framework which is also used by the app.
        ///     For example, this would happen if the app referenced Microsoft.AspNet.App version 2.0 and Microsoft.NETCore.App version 3.0.
        ///     In such case the Microsoft.AspNet.App has .runtimeconfig.json which also references Microsoft.NETCore.App but it only allows versions 2.0 up to 2.9 (via roll forward options).
        ///     So the version 3.0 requested by the app is incompatible.
        /// </remarks>
        FrameworkCompatFailure = unchecked((int)0x8000809c),

        /// <summary>
        ///     Error used internally if the processing of framework references from .runtimeconfig.json reached a point where it needs to reprocess another already processed framework reference.
        /// </summary>
        /// <remarks>
        ///     If this error is returned to the external caller, it would mean there's a bug in the framework resolution algorithm.
        /// </remarks>
        FrameworkCompatRetry = unchecked((int)0x8000809d),

        /// <summary>
        ///     Error reading the bundle footer metadata from a single-file <c>apphost</c>.
        /// </summary>
        /// <remarks>
        ///     This indicates a corrupted <c>apphost</c>.
        /// </remarks>
        AppHostExeNotBundle = unchecked((int)0x8000809e),

        /// <summary>
        ///     Error extracting single-file apphost bundle.
        /// </summary>
        /// <remarks>
        ///     This is used in case of any error related to the bundle itself. Typically would mean a corrupted bundle.
        /// </remarks>
        BundleExtractionFailure = unchecked((int)0x8000809f),

        /// <summary>
        ///     Error reading or writing files during single-file apphost bundle extraction.
        /// </summary>
        BundleExtractionIOError = unchecked((int)0x800080a0),

        /// <summary>
        ///     The .runtimeconfig.json specified by the app contains a runtime property which is also produced by the hosting layer.
        /// </summary>
        /// <remarks>
        ///     For example if the .runtimeconfig.json would specify a property TRUSTED_PLATFORM_ROOTS, this error code would be returned.
        ///     It is not allowed to specify properties which are otherwise populated by the hosting layer (<c>hostpolicy</c>) as there is not good way to resolve such conflicts.
        /// </remarks>
        LibHostDuplicateProperty = unchecked((int)0x800080a1),

        /// <summary>
        ///     Feature which requires certain version of the hosting layer binaries was used on a version which doesn't support it.
        /// </summary>
        /// <remarks>
        ///     For example if a COM component specified to run on 2.0 Microsoft.NETCore.App - as that contains older version of <c>hostpolicy</c> which doesn't support the necessary features to provide COM services.
        /// </remarks>
        HostApiUnsupportedVersion = unchecked((int)0x800080a2),
        
        /// <summary>
        ///     Error code returned by the hosting APIs in <c>hostfxr</c> if the current state is incompatible with the requested operation. 
        /// </summary>
        HostInvalidState = unchecked((int)0x800080a3),

        /// <summary>
        ///     A property requested by <c>hostfxr_get_runtime_property_value</c> doesn't exist.
        /// </summary>
        HostPropertyNotFound = unchecked((int)0x800080a4),

        /// <summary>
        ///     Error returned by <c>hostfxr_initialize_for_runtime_config</c> if the component being initialized requires framework which is not available or incompatible with the frameworks loaded by the runtime already in the process.
        /// </summary>
        /// <remarks>
        ///     For example trying to load a component which requires 3.0 into a process which is already running a 2.0 runtime.
        /// </remarks>
        CoreHostIncompatibleConfig = unchecked((int)0x800080a5),

        /// <summary>
        ///     Error returned by <c>hostfxr_get_runtime_delegate</c> when <c>hostfxr</c> doesn't currently support requesting the given delegate type using the given context.
        /// </summary>
        HostApiUnsupportedScenario = unchecked((int)0x800080a6),

        /// <summary>
        ///     Error returned by <c>hostfxr_get_runtime_delegate</c> when managed feature support for native host is disabled.
        /// </summary>
        HostFeatureDisabled = unchecked((int)0x800080a7),
    }
}
