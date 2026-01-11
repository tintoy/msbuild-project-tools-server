using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using Serilog.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MSBuildProjectTools.LanguageServer.IntegrationTests
{
    using Utilities;

    /// <summary>
    /// Fixture for managing the language server process and client connection.
    /// </summary>
    public class LanguageServerFixture(bool dynamicRegistration = true) : IAsyncDisposable
    {
        const string ServerDllName = "MSBuildProjectTools.LanguageServer.Host.dll";

        /// <summary>
        ///  Controls dynamic registration support of the language client.
        /// </summary>
        readonly bool _dynamicRegistration = dynamicRegistration;

        /// <summary>
        /// The logger.
        /// </summary>
        ILogger _logger;

        /// <summary>
        /// The cancellation token source for server initialization.
        /// </summary>
        CancellationTokenSource _ctsServerInitialize;

        /// <summary>
        /// The task completion source to wait for the server process to exit.
        /// </summary>
        TaskCompletionSource<object> _serverExitCompletion;

        /// <summary>
        /// The language server process.
        /// </summary>
        Process _serverProcess;

        /// <summary>
        /// The language client.
        /// </summary>
        ILanguageClient _client;

        /// <summary>
        /// The language client.
        /// </summary>
        public ILanguageClient Client => _client;

        /// <summary>
        /// Start the language server and connect the client.
        /// </summary>
        /// <param name="workspaceRoot">
        /// The root directory of the workspace to open.
        /// </param>
        /// <param name="loggerProvider">
        /// An optional logger provider for the client.
        /// </param>
        /// <param name="cancellationToken">
        /// An optional cancellation token.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        public async Task StartAsync(string workspaceRoot, ILoggerProvider loggerProvider)
        {
            if (_client != null)
                throw new InvalidOperationException("Language server is already started.");

            _logger = loggerProvider?.CreateLogger("LanguageServerFixture");
            
            // Find the language server executable
            string serverExecutable = FindServerExecutable();
            if (string.IsNullOrEmpty(serverExecutable))
                throw new FileNotFoundException("Cannot find the language server executable.");

            _logger?.LogInformation("Starting language server from: {ServerExecutable}", serverExecutable);

            // Create the server process info, 
            // Important: run from the directory containing the DLL
            var serverInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = ServerDllName,
                WorkingDirectory = Path.GetDirectoryName(serverExecutable),
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

            _serverExitCompletion = new TaskCompletionSource<object>();
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
                    .ConfigureLogging(builder =>
                    {
                        builder.AddProvider(loggerProvider);
                        builder.AddFilter<SerilogLoggerProvider>(null, LogLevel.Trace);
                    })
                    .WithInput(_serverProcess.StandardOutput.BaseStream)
                    .WithOutput(_serverProcess.StandardInput.BaseStream)
                    .WithRootUri(DocumentUri.FromFileSystemPath(workspaceRoot));
                options.OnLogMessage(message =>
                {
                    switch (message.Type)
                    {
                        case MessageType.Error:
                            _logger?.LogError("[SRV/LSP/{Lvl}] {Msg}", nameof(LogLevel.Error), message.Message); break;
                        case MessageType.Warning:
                            _logger?.LogWarning("[SRV/LSP/{Lvl:l}] {Msg}", nameof(LogLevel.Warning), message.Message); break;
                        case MessageType.Info:
                            _logger?.LogInformation("[SRV/LSP/{Lvl:l}] {Msg}", nameof(LogLevel.Information), message.Message); break;
                        case MessageType.Log:
                            _logger?.LogDebug("[SRV/LSP/{Lvl:l}] {Msg}", nameof(LogLevel.Debug), message.Message); break;
                    }
                });
                if (!_dynamicRegistration)
                    options.DisableDynamicRegistration();
                else
                    options.EnableDynamicRegistration();
                var initializeTask = LanguageClient.From(options, _ctsServerInitialize.Token);

                _logger?.LogInformation("Initializing language client...");

                var initCancelRegistration = _ctsServerInitialize.Token.Register((state, token) =>
                {
                    var (_this, _options) = ((LanguageServerFixture, LanguageClientOptions))state;
                    // As long as the bug in OmniSharp libs exists, 
                    // we need to cancel request from ResponseRouter manually
                    var client = _this._client;
                    if (client == null)
                    {
                        client = (from x in _options.Services
                                  where x.ServiceType == typeof(ILanguageClient)
                                      && x.ImplementationInstance != null
                                  select (ILanguageClient)x.ImplementationInstance)
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
                _logger?.LogInformation("Language client initialized.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize language client");
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

        private void ServerProcess_Exit(object sender, EventArgs e)
        {
            _logger.LogDebug("Server process has exited.");
            _serverExitCompletion.TrySetResult(null);
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
                        await _serverExitCompletion.Task;
                    }
                    catch (TaskCanceledException tce)
                    when (tce.CancellationToken == _ctsServerInitialize.Token)
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
        /// Find the language server executable.
        /// </summary>
        /// <returns>
        /// The path to the language server executable, or <c>null</c> if not found.
        /// </returns>
        string FindServerExecutable()
        {
            Assembly thisAssembly = GetType().Assembly;

            // Find the git root directory by looking for .git folder
            string gitRoot = FindGitRoot();
            if (gitRoot == null)
            {
                // Fallback: try relative to test assembly
                string testAssemblyDir = Path.GetDirectoryName(thisAssembly.Location);
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
            string thisTargetFramework = thisAssembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
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

                        string assemblyTargetFramework = GetTargetFramework(serverAssembly);
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
        string GetTargetFramework(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            // Some jiggery-pokery is required to access custom-attribute data, since we aren't really loading the assembly.
            CustomAttributeData targetFrameworkAttributeData = assembly.GetCustomAttributesData().FirstOrDefault(
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
            PathAssemblyResolver assemblyResolver = new PathAssemblyResolver(runtimeAssemblies);

            return new MetadataLoadContext(assemblyResolver);
        }

        /// <summary>
        /// Find the git root directory by looking for .git folder.
        /// </summary>
        /// <returns>
        /// The path to the git root directory, or <c>null</c> if not found.
        /// </returns>
        static string FindGitRoot()
        {
            string currentDir = Path.GetDirectoryName(typeof(LanguageServerFixture).Assembly.Location);

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
}
