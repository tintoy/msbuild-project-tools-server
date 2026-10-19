using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using Serilog.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace MSBuildProjectTools.LanguageServer.IntegrationTests
{
    using Utilities;

    /// <summary>
    /// Fixture for managing the language server process and client connection.
    /// </summary>
    public class LanguageServerFixture : IAsyncDisposable
    {
        const string ServerDllName = "MSBuildProjectTools.LanguageServer.Host.dll";

        static readonly Assembly ThisAssembly = typeof(LanguageServerFixture).Assembly;

        /// <summary>
        ///     JSON serialisation settings to use when generating global.json.
        /// </summary>
        static JsonSerializerSettings GlobalJsonSerializerSettings => new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include,
            Converters =
            {
                new StringEnumConverter(),
            }
        };

        /// <summary>
        ///     The default roll-forward policy to use when selecting a .NET SDK version for use by the language server (affects the behaviour of <see cref="MSBuildLocator"/>.
        /// </summary>
        public const string DefaultSdkRollForwardPolicy = "feature";

        /// <summary>
        ///  Controls dynamic registration support of the language client.
        /// </summary>
        readonly bool _dynamicRegistration;

        /// <summary>
        ///     The collector for log messages from the language server (via "window/logMessage" notifications).
        /// </summary>
        readonly LanguageServerLogCollector _logCollector;

        /// <summary>
        /// The logger.
        /// </summary>
        ILogger _logger = NullLogger.Instance;

        /// <summary>
        /// The cancellation token source for server initialization.
        /// </summary>
        CancellationTokenSource? _ctsServerInitialize;

        /// <summary>
        /// The task completion source to wait for the server process to exit.
        /// </summary>
        TaskCompletionSource<object?>? _serverExitCompletion;

        /// <summary>
        /// The language server process.
        /// </summary>
        Process? _serverProcess;

        /// <summary>
        /// The language client.
        /// </summary>
        ILanguageClient? _client;

        public LanguageServerFixture(bool dynamicRegistration = true)
        {
            _logCollector = new LanguageServerLogCollector(this);

            _dynamicRegistration = dynamicRegistration;
        }

        /// <summary>
        /// The language client.
        /// </summary>
        public ILanguageClient? Client => _client;

        /// <summary>
        ///     Is the language server running?
        /// </summary>
        public bool IsRunning => _client != null;

        /// <summary>
        ///     The collector for log messages from the language server (via "window/logMessage" notifications).
        /// </summary>
        public LanguageServerLogCollector LogCollector => _logCollector;

        /// <summary>
        /// Start the language server and connect the client.
        /// </summary>
        /// <param name="workspaceRoot">
        /// The root directory of the workspace to open.
        /// </param>
        /// <param name="loggerProvider">
        /// An optional logger provider for the client.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        public async Task StartAsync(string workspaceRoot, ILoggerProvider? loggerProvider = null)
        {
            if (_client != null)
                throw new InvalidOperationException("Language server is already started.");

            loggerProvider ??= NullLoggerProvider.Instance;
            _logger = loggerProvider.CreateLogger("LanguageServerFixture");
            _logCollector.Clear();

            // Find the language server executable
            string serverExecutable = FindServerExecutable();
            if (string.IsNullOrEmpty(serverExecutable))
                throw new FileNotFoundException("Cannot find the language server executable.");

            _logger.LogInformation("Starting language server from: {ServerExecutable}", serverExecutable);

            // Create the server process info, 
            // Important: run from the directory containing the DLL
            var serverInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = serverExecutable,
                WorkingDirectory = workspaceRoot,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment = {
                    ["MSBUILD_PROJECT_TOOLS_LOGGING_TO_STDERR"] = "2", // Using "2" instead of 1 will cause the language server to produce logs in a format that can be displayed, inline, with other (local) logs.
                    ["MSBUILD_PROJECT_TOOLS_VERBOSE_LOGGING"] = "1",
                }
            };

            _serverExitCompletion = new TaskCompletionSource<object?>();
            _ctsServerInitialize = new CancellationTokenSource();
            _serverExitCompletion.CanceledBy(_ctsServerInitialize);
            _serverProcess = new Process
            {
                StartInfo = serverInfo,
                EnableRaisingEvents = true
            };
            _serverProcess.Exited += ServerProcess_Exit;
            _serverProcess.ErrorDataReceived += ServerProcess_ErrorDataReceived;
            try
            {
                if (!_serverProcess.Start())
                    throw new InvalidOperationException("Failed to launch language server.");

                _logger.LogInformation("Language server process started. PID: {ServerProcessId}", _serverProcess.Id);
                _serverProcess.BeginErrorReadLine();

                // Create and initialize the language client
                var options = new LanguageClientOptions()
                    .ConfigureLogging(logging =>
                    {
                        logging.AddProvider(loggerProvider);
                        logging.AddFilter<SerilogLoggerProvider>(null, LogLevel.Trace);
                    })
                    .WithInput(_serverProcess.StandardOutput.BaseStream)
                    .WithOutput(_serverProcess.StandardInput.BaseStream)
                    .WithRootUri(DocumentUri.FromFileSystemPath(workspaceRoot));
                options.OnLogMessage(message =>
                {
                    switch (message.Type)
                    {
                        case MessageType.Error:
                        {
                            _logger.LogError("[SRV/LSP/{Lvl}] {Msg}", nameof(LogLevel.Error), message.Message);
                            
                            break;
                        }
                        case MessageType.Warning:
                        {
                            _logger.LogWarning("[SRV/LSP/{Lvl:l}] {Msg}", nameof(LogLevel.Warning), message.Message);
                            
                            break;
                        }
                        case MessageType.Info:
                        {
                            _logger.LogInformation("[SRV/LSP/{Lvl:l}] {Msg}", nameof(LogLevel.Information), message.Message);

                            break;
                        }
                        case MessageType.Log:
                        {
                            _logger.LogDebug("[SRV/LSP/{Lvl:l}] {Msg}", nameof(LogLevel.Debug), message.Message);

                            break;
                        }
                    }

                    try
                    {
                        _logCollector.OnMessageLogged(message);
                    }
                    catch (Exception logHandlerError)
                    {
                        _logger.LogError(logHandlerError, "Unexpected error while sending log message to the log collector.");
                    }
                });
                if (!_dynamicRegistration)
                    options.DisableDynamicRegistration();
                else
                    options.EnableDynamicRegistration();
                var initializeTask = LanguageClient.From(options, _ctsServerInitialize.Token);

                _logger.LogInformation("Initializing language client...");

                var initCancelRegistration = _ctsServerInitialize.Token.Register((state, token) =>
                {
                    if (state == null)
                        throw new ArgumentNullException(nameof(state));

                    (LanguageServerFixture? _this, LanguageClientOptions? _options) = ((LanguageServerFixture, LanguageClientOptions))state;
                    // As long as the bug in OmniSharp libs exists, 
                    // we need to cancel request from ResponseRouter manually
                    var client = _this._client;
                    if (client == null)
                    {
                        client = (from x in _options.Services
                                  where x.ServiceType == typeof(ILanguageClient)
                                      && x.ImplementationInstance is ILanguageClient
                                  select (ILanguageClient?)x.ImplementationInstance)
                                .FirstOrDefault();
                        if (client != null)
                        {
                            // The first request should always be the initialize request
                            var (method, tcsRequest) = client.GetRequest(1);
                            if (method == GeneralNames.Initialize)
                                tcsRequest.TrySetCanceled(token);

                            client.Dispose();
                        }
                    }
                }, (this, options));
                try
                {
                    _client = await initializeTask;
                }
                finally
                {
                    initCancelRegistration.Dispose();
                }
                _logger.LogInformation("Language client initialized.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize language client");
                // xUnit changed its cleanup logic for IAsyncLifetime,
                // so call StopAsync manually here when an exception is caught.
                //see: https://github.com/xunit/xunit/issues/3124
                await StopAsync();
                throw;
            }
        }

        private void ServerProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data is not null)
                _logger.LogInformation("[SRV] {StdErrorLine}", e.Data);
        }

        private void ServerProcess_Exit(object? sender, EventArgs args)
        {
            _logger.LogDebug("Server process has exited.");
            _serverExitCompletion?.TrySetResult(null);
            _ctsServerInitialize?.Cancel();
            var client = Interlocked.Exchange(ref _client, null);
            client?.Dispose();
        }

        /// <summary>
        /// Stop the language server and disconnect the client.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        public async Task StopAsync()
        {
            var client = Interlocked.Exchange(ref _client, null);
            if (client != null)
            {
                try
                {
                    await client.Shutdown();
                }
                catch (Exception)
                {
                    // Ignore errors during shutdown
                }

                client.Dispose();
            }

            if (_serverProcess != null)
            {
                _ctsServerInitialize?.CancelAfter(5000);
                try
                {
                    if (!_serverProcess.HasExited)
                    {
                        try
                        {
                            _serverProcess.Kill();
                        }
                        catch (Exception)
                        {
                            // Ignore errors during process termination
                        }
                    }

                    try
                    {
                        TaskCompletionSource<object?>? serverExitCompletion = _serverExitCompletion;
                        if (serverExitCompletion != null)
                            await serverExitCompletion.Task;
                    }
                    catch (TaskCanceledException tce)
                    when (tce.CancellationToken == _ctsServerInitialize?.Token)
                    {
                        _logger?.LogInformation("The server process did not shutdown in a timely manner.");
                        // swallow cancellation exception
                    }
                }
                finally
                {
                    _ctsServerInitialize?.Dispose();
                    _ctsServerInitialize = null;
                    _serverProcess?.Dispose();
                    _serverProcess = null;
                }
            }
        }

        public async ValueTask DisposeAsync() => await StopAsync();

        /// <summary>
        ///     Create or update the global.json file that controls the version of the .NET SDK used in the specified workspace root.
        /// </summary>
        /// <param name="workspaceRoot">
        ///     A <see cref="DirectoryInfo"/> representing the root directory of the target workspace.
        /// </param>
        /// <param name="targetSdkVersion">
        ///     The version of the .NET SDK to target.
        /// </param>
        /// <param name="sdkRollForwardPolicy">
        ///     The policy for rolling forward to newer versions of the .NET SDK (if present).
        /// </param>
        /// <returns>
        ///     The full path to global.json.
        /// </returns>
        public string ConfigureTargetSdkForWorkspace(string workspaceRoot, string targetSdkVersion, string sdkRollForwardPolicy = DefaultSdkRollForwardPolicy)
        {
            if (String.IsNullOrWhiteSpace(workspaceRoot))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(workspaceRoot)}.", nameof(workspaceRoot));

            if (String.IsNullOrWhiteSpace(targetSdkVersion))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(targetSdkVersion)}.", nameof(targetSdkVersion));

            if (String.IsNullOrWhiteSpace(sdkRollForwardPolicy))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(sdkRollForwardPolicy)}.", nameof(sdkRollForwardPolicy));

            FileInfo globalJsonFile = ConfigureTargetSdkForWorkspace(
                new DirectoryInfo(workspaceRoot),
                targetSdkVersion,
                sdkRollForwardPolicy
            );

            return globalJsonFile.FullName;
        }

        /// <summary>
        ///     Create or update the global.json file that controls the version of the .NET SDK used in the specified workspace root.
        /// </summary>
        /// <param name="workspaceRoot">
        ///     A <see cref="DirectoryInfo"/> representing the root directory of the target workspace.
        /// </param>
        /// <param name="targetSdkVersion">
        ///     The version of the .NET SDK to target.
        /// </param>
        /// <param name="sdkRollForwardPolicy">
        ///     The policy for rolling forward to newer versions of the .NET SDK (if present).
        /// </param>
        /// <returns>
        ///     A <see cref="FileInfo"/> representing global.json in the workspace root.
        /// </returns>
        public FileInfo ConfigureTargetSdkForWorkspace(DirectoryInfo workspaceRoot, string targetSdkVersion, string sdkRollForwardPolicy = DefaultSdkRollForwardPolicy)
        {
            if (workspaceRoot == null)
                throw new ArgumentNullException(nameof(workspaceRoot));

            if (String.IsNullOrWhiteSpace(targetSdkVersion))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(targetSdkVersion)}.", nameof(targetSdkVersion));

            if (String.IsNullOrWhiteSpace(sdkRollForwardPolicy))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(sdkRollForwardPolicy)}.", nameof(sdkRollForwardPolicy));

            if (!workspaceRoot.Exists)
                workspaceRoot.Create();

            GlobalJsonRoot globalJson;

            var jsonSerializer = JsonSerializer.Create(GlobalJsonSerializerSettings);

            var globalJsonFile = new FileInfo(
                Path.Combine(workspaceRoot.FullName, "global.json")
            );
            if (globalJsonFile.Exists)
            {
                using (TextReader globalJsonReader = globalJsonFile.OpenText())
                {
                    globalJson =
                        jsonSerializer.Deserialize<GlobalJsonRoot>(
                            new JsonTextReader(globalJsonReader)
                        )
                        ??
                        GlobalJsonRoot.Empty;
                }

                globalJson = globalJson with
                {
                    Sdk = globalJson.Sdk with
                    {
                        Version = targetSdkVersion,
                        RollForward = sdkRollForwardPolicy
                    }
                };
            }
            else
            {
                globalJson = new GlobalJsonRoot(
                    Sdk: new GlobalJsonSdk(
                        Version: targetSdkVersion,
                        RollForward: sdkRollForwardPolicy
                    )
                );
            }

            using (TextWriter globalJsonWriter = globalJsonFile.CreateText())
            {
                jsonSerializer.Serialize(globalJsonWriter, globalJson);
            }

            return globalJsonFile;
        }

        /// <summary>
        /// Find the language server executable.
        /// </summary>
        /// <returns>
        /// The path to the language server executable, or <c>null</c> if not found.
        /// </returns>
        string FindServerExecutable()
        {
            Assembly thisAssembly = GetType().Assembly;

            // Find the git root directory by looking for .git folder
            string? gitRoot = FindGitRoot();
            if (gitRoot == null)
            {
                // Fallback: try relative to test assembly
                string testAssemblyDir = Path.GetDirectoryName(thisAssembly.Location) ?? throw new InvalidOperationException("Cannot determine the location of the test assembly.");

                gitRoot = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", ".."));
            }

            string serverProjectDir = Path.Combine(gitRoot, "src", "LanguageServer");
            string binDir = Path.Combine(serverProjectDir, "bin");

            // Check if bin directory exists to avoid hangs or exceptions on Linux
            if (!Directory.Exists(binDir))
            {
                throw new FileNotFoundException($"Language server bin directory not found: {binDir}");
            }

            var serverAssemblyPaths = Directory.GetFiles(binDir, ServerDllName, SearchOption.AllDirectories);
            if (serverAssemblyPaths.Length == 0)
            {
                throw new FileNotFoundException($"Language server executable '{ServerDllName}' not found under: '{binDir}'");
            }

            // Find first matching assembly by target framework.
            string? thisTargetFramework = thisAssembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
            if (thisTargetFramework != null)
                _logger.LogInformation("Searching for language server assembly matching target framework {FrameworkName}...", thisTargetFramework);
            else
                Assert.Fail("The current assembly is missing a [TargetFramework] attribute.");

            using (MetadataLoadContext loader = CreateServerAssemblyLoader())
            {
                foreach (string serverAssemblyPath in serverAssemblyPaths)
                {
                    try
                    {
                        Assembly serverAssembly = loader.LoadFromAssemblyPath(serverAssemblyPath);

                        string? assemblyTargetFramework = GetTargetFramework(serverAssembly);
                        if (assemblyTargetFramework != thisTargetFramework)
                        {
                            _logger.LogInformation("Ignoring assembly {AssemblyPath} (its target framework {TargetFrameworkName} does not match {FrameworkName}).",
                                serverAssemblyPath,
                                assemblyTargetFramework,
                                thisTargetFramework
                            );

                            continue;
                        }
                    }
                    catch (BadImageFormatException invalidAssembly)
                    {
                        _logger.LogError(invalidAssembly, "Ignoring assembly {AssemblyPath} (this file does not appear to be a valid assembly).", serverAssemblyPath);

                        continue;
                    }
                    catch (FileLoadException assemblyLoadFailure)
                    {
                        _logger.LogError(assemblyLoadFailure, "Ignoring assembly {AssemblyPath} (an unexpected error occurred while loading this assembly).", serverAssemblyPath);

                        continue;
                    }

                    return serverAssemblyPath;
                }

                throw new FileNotFoundException($"No matching language server executable '{ServerDllName}' was found under: '{binDir}'.");
            }
        }

        /// <summary>
        ///     Attempt to determine the name of the target framework for the specified assembly (via <see cref="TargetFrameworkAttribute"/>).
        /// </summary>
        /// <param name="assembly">
        ///     The assembly to examine.
        /// </param>
        /// <returns>
        ///     The assembly's target framework, or <c>null</c> if the assembly is not decorated with <see cref="TargetFrameworkAttribute"/>.
        /// </returns>
        string? GetTargetFramework(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            // Some jiggery-pokery is required to access custom-attribute data, since we aren't really loading the assembly.
            CustomAttributeData? targetFrameworkAttributeData = assembly.GetCustomAttributesData().FirstOrDefault(
                attributeData => attributeData.AttributeType.FullName == typeof(TargetFrameworkAttribute).FullName
            );
            if (targetFrameworkAttributeData == null)
            {
                _logger.LogWarning("Ignoring assembly {AssemblyPath} (missing [TargetFramework] attribute).", assembly.Location);

                return null;
            }

            return targetFrameworkAttributeData.ConstructorArguments[0].Value as string;
        }

        /// <summary>
        ///     Create a <see cref="MetadataLoadContext"/> to examine candidate language-server assemblies.
        /// </summary>
        /// <returns>
        ///     The configured <see cref="MetadataLoadContext"/> (including assemblies from the current runtime).
        /// </returns>
        /// <remarks>
        ///     We use <see cref="MetadataLoadContext"/>, rather than <see cref="AssemblyLoadContext"/>, because we want to examine candidate assemblies without have to load them (and all their dependencies).
        /// </remarks>
        static MetadataLoadContext CreateServerAssemblyLoader()
        {
            string[] runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
            var assemblyResolver = new PathAssemblyResolver(runtimeAssemblies);

            return new MetadataLoadContext(assemblyResolver);
        }

        /// <summary>
        /// Find the git root directory by looking for .git folder.
        /// </summary>
        /// <returns>
        /// The path to the git root directory, or <c>null</c> if not found.
        /// </returns>
        static string? FindGitRoot()
        {
            string? currentDir = Path.GetDirectoryName(typeof(LanguageServerFixture).Assembly.Location);

            var counter = 0;
            while (currentDir != null)
            {
                string dotGitPath = Path.Combine(currentDir, ".git");

                bool isMainRepository = Directory.Exists(dotGitPath);
                if (isMainRepository)
                    return currentDir;

                bool isSubmodule = File.Exists(dotGitPath);
                if (isSubmodule)
                    return currentDir;

                currentDir = Path.GetDirectoryName(currentDir);
                counter++;

                if (counter > 10) // Prevent infinite loop
                    break;
            }

            return null;
        }
    }

    /// <summary>
    ///     Collector for logged messages from a language server (via "window/logMessage" notifications).
    /// </summary>
    public class LanguageServerLogCollector
    {
        /// <summary>
        ///     Messages logged by the language server (if any).
        /// </summary>
        readonly ConcurrentQueue<LogMessage> _loggedMessages = new ConcurrentQueue<LogMessage>();

        /// <summary>
        ///     The <see cref="IntegrationTests.LanguageServerFixture"/> that owns the language server for which logs are collected.
        /// </summary>
        readonly LanguageServerFixture _languageServerFixture;

        /// <summary>
        ///     Create a new <see cref="LogCollector"/>.
        /// </summary>
        /// <param name="languageServerFixture">
        ///     The <see cref="IntegrationTests.LanguageServerFixture"/> that owns the language server for which logs are collected.
        /// </param>
        public LanguageServerLogCollector(LanguageServerFixture languageServerFixture)
        {
            if (languageServerFixture == null)
                throw new ArgumentNullException(nameof(languageServerFixture));

            _languageServerFixture = languageServerFixture;
        }

        /// <summary>
        ///     Have any messages been logged?
        /// </summary>
        public bool HasLoggedMessages => !_loggedMessages.IsEmpty;

        /// <summary>
        ///     The <see cref="IntegrationTests.LanguageServerFixture"/> that owns the language server for which logs are collected.
        /// </summary>
        public LanguageServerFixture LanguageServerFixture => _languageServerFixture;

        /// <summary>
        ///     Messages logged by the language server (if any).
        /// </summary>
        public IEnumerable<LogMessage> LoggedMessages => _loggedMessages;

        /// <summary>
        ///     Error messages logged by the language server (if any).
        /// </summary>
        public IEnumerable<LogMessage> LoggedErrors => _loggedMessages.Where(loggedMessage => loggedMessage.Type == MessageType.Error);

        /// <summary>
        ///     Warning messages logged by the language server (if any).
        /// </summary>
        public IEnumerable<LogMessage> LoggedWarnings => _loggedMessages.Where(loggedMessage => loggedMessage.Type == MessageType.Warning);

        /// <summary>
        ///     Warning messages logged by the language server (if any).
        /// </summary>
        public IEnumerable<LogMessage> LoggedWarningsAndErrors => _loggedMessages.Where(loggedMessage => loggedMessage.Type == MessageType.Warning || loggedMessage.Type == MessageType.Error);

        /// <summary>
        ///     Event raised when a message is logged by the language server (via a "window/logMessage" notifications).
        /// </summary>
        public event Action<LanguageServerFixture, LogMessage>? MessageLogged;

        /// <summary>
        ///     Reset the <see cref="LogCollector"/>, clearing all logged messages.
        /// </summary>
        public void Clear()
        {
            _loggedMessages.Clear();
        }

        /// <summary>
        ///     Notify the log collector that a message has been logged by the language server.
        /// </summary>
        /// <param name="logMessageParams">
        ///     LSP <see cref="LogMessageParams"/> representing the logged message.
        /// </param>
        public void OnMessageLogged(LogMessageParams logMessageParams)
        {
            if (logMessageParams == null)
                throw new ArgumentNullException(nameof(logMessageParams));

            LogMessage logMessage = logMessageParams;

            OnMessageLogged(logMessage);
        }

        /// <summary>
        ///     Notify the log collector that a message has been logged by the language server.
        /// </summary>
        /// <param name="logMessageParams">
        ///     A <see cref="LogMessage"/> record representing the logged message.
        /// </param>
        public void OnMessageLogged(LogMessage logMessage)
        {
            if (logMessage == null)
                throw new ArgumentNullException(nameof(logMessage));

            _loggedMessages.Enqueue(logMessage);

            if (MessageLogged != null)
                MessageLogged(LanguageServerFixture, logMessage);
        }

        /// <summary>
        ///     Ensure that the language server has not logged any errors during the test run.
        /// </summary>
        /// <param name="logger">
        ///     An optional serilog <see cref="Serilog.ILogger"/> that will be used to replay any errors that have been logged.
        /// </param>
        public void ExpectNoErrors(Serilog.ILogger? logger = null)
        {
            logger ??= Serilog.Core.Logger.None;

            var loggedErrors = LoggedErrors.ToList();
            if (loggedErrors.Count > 0)
            {
                foreach (LogMessageParams loggedError in loggedErrors)
                    logger.Error("Unexpected error logged by language server (via window/logMessage): {ErrorMessage:l}", loggedError.Message);

                Assert.Fail(
                    message: $"The language server logged an unexpected error (via window/logMessage): {loggedErrors[0].Message}"
                );
            }
            Assert.Empty(loggedErrors);
        }

        /// <summary>
        ///     Ensure that the language server has not logged any errors during the test run.
        /// </summary>
        /// <param name="logger">
        ///     An optional serilog <see cref="Serilog.ILogger"/> that will be used to replay any errors that have been logged.
        /// </param>
        public void ExpectError(Serilog.ILogger? logger = null)
        {
            logger ??= Serilog.Core.Logger.None;

            var loggedErrors = LoggedErrors.ToList();
            if (loggedErrors.Count > 0)
            {
                foreach (LogMessageParams loggedError in loggedErrors)
                    logger?.Error("Expected error logged by language server (via window/logMessage): {ErrorMessage:l}", loggedError.Message);
            }
            else
                Assert.Fail("The language server was expected to log an error (via window/logMessage), but did not.");
        }
    }

    /// <summary>
    ///     A message logged from a language server (to an LSP client, via a "window/logMessage" notification).
    /// </summary>
    /// <param name="Type">
    ///     The type of message logged.
    /// </param>
    /// <param name="Message">
    ///     The logged message.
    /// </param>
    public record class LogMessage(MessageType Type, string Message)
    {
        /// <summary>
        ///     Convert the <see cref="LogMessage"/> to its LSP equivalent (<see cref="LogMessageParams"/>).
        /// </summary>
        /// <returns>
        ///     The converted <see cref="LogMessageParams"/>.
        /// </returns>
        public LogMessageParams ToLsp()
        {
            return new LogMessageParams
            {
                Type = Type,
                Message = Message,
            };
        }

        /// <summary>
        ///     Convert an LSP <see cref="LogMessageParams"/> to a <see cref="LogMessage"/>.
        /// </summary>
        /// <param name="logMessageParams">
        ///     The LSP <see cref="LogMessageParams"/>.
        /// </param>
        /// <returns>
        ///     The converted <see cref="LogMessage"/>.
        /// </returns>
        public static LogMessage FromLsp(LogMessageParams logMessageParams)
        {
            if (logMessageParams == null)
                throw new ArgumentNullException(nameof(logMessageParams));

            return new LogMessage(Type: logMessageParams.Type, Message: logMessageParams.Message);
        }

        /// <summary>
        ///     Explicit cast operator from <see cref="LogMessage"/> to <see cref="LogMessageParams"/>.
        /// </summary>
        /// <param name="logMessage">
        ///     The <see cref="LogMessage"/> to convert.
        /// </param>
        [return: NotNullIfNotNull(nameof(logMessage))]
        public static explicit operator LogMessageParams?(LogMessage? logMessage)
        {
            if (logMessage is null)
                return null;

            return logMessage.ToLsp();
        }

        /// <summary>
        ///     Explicit cast operator from <see cref="LogMessageParams"/> to <see cref="LogMessage"/>.
        /// </summary>
        /// <param name="logMessageParams">
        ///     The <see cref="LogMessageParams"/> to convert.
        /// </param>
        [return: NotNullIfNotNull(nameof(logMessageParams))]
        public static implicit operator LogMessage?(LogMessageParams? logMessageParams)
        {
            if (logMessageParams is null)
                return null;

            return FromLsp(logMessageParams);
        }
    };

    /// <summary>
    ///     The root JSON object in global.json.
    /// </summary>
    /// <param name="Sdk">
    ///     .NET SDK configuration.
    /// </param>
    public record class GlobalJsonRoot(GlobalJsonSdk Sdk)
    {
        /// <summary>
        ///     An empty <see cref="GlobalJsonRoot"/>.
        /// </summary>
        public static readonly GlobalJsonRoot Empty = new GlobalJsonRoot(Sdk: GlobalJsonSdk.Empty);

        /// <summary>
        ///     JSON extension data (used to support round-tripping of unrecognised JSON members).
        /// </summary>
        [JsonExtensionData]
        readonly IDictionary<string, JToken> _extensionData  = new Dictionary<string, JToken>(StringComparer.Ordinal);
    };

    /// <summary>
    ///     .NET SDK configuration in global.json.
    /// </summary>
    /// <param name="Version">
    ///     The required version of the .NET SDK.
    /// </param>
    /// <param name="RollForward">
    ///     The policy for rolling forward to newer .NET SDKs, if the required version is not available.
    /// </param>
    public record class GlobalJsonSdk(string Version, string RollForward = LanguageServerFixture.DefaultSdkRollForwardPolicy)
    {
        /// <summary>
        ///     An empty <see cref="GlobalJsonSdk"/>.
        /// </summary>
        public static readonly GlobalJsonSdk Empty = new GlobalJsonSdk(Version: String.Empty, RollForward: String.Empty);

        /// <summary>
        ///     JSON extension data (used to support round-tripping of unrecognised JSON members).
        /// </summary>
        [JsonExtensionData]
        readonly IDictionary<string, JToken> _extensionData = new Dictionary<string, JToken>(StringComparer.Ordinal);
    };
}
