using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using Serilog;
using Serilog.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MSBuildProjectTools.LanguageServer.IntegrationTests
{
    /// <summary>
    /// Fixture for managing the language server process and client connection.
    /// </summary>
    public class LanguageServerFixture : IAsyncDisposable
    {
        const string ServerDllName = "MSBuildProjectTools.LanguageServer.Host.dll";

        /// <summary>
        /// The logger.
        /// </summary>
        Microsoft.Extensions.Logging.ILogger _logger;

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

            // Find the language server executable
            string serverExecutable = FindServerExecutable();
            if (string.IsNullOrEmpty(serverExecutable))
                throw new FileNotFoundException("Cannot find the language server executable.");

            _logger = loggerProvider?.CreateLogger("LanguageServerFixture");

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
                     { "MSBUILD_PROJECT_TOOLS_VERBOSE_LOGGING", "1" }
                }
            };

            _serverExitCompletion = new TaskCompletionSource<object>();
            _ctsServerInitialize = new CancellationTokenSource();
            _ctsServerInitialize.Token.Register((state, token) =>
            {
                var _this = (LanguageServerFixture)state;
                _this._serverExitCompletion.TrySetCanceled(token);
            }, this);
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
                            _logger?.LogError("[SRV] {Msg}", message.Message); break;
                        case MessageType.Warning:
                            _logger?.LogWarning("[SRV] {Msg}", message.Message); break;
                        case MessageType.Info:
                            _logger?.LogInformation("[SRV] {Msg}", message.Message); break;
                        case MessageType.Log:
                            _logger?.LogDebug("[SRV] {Msg}", message.Message); break;
                    }
                });
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
                _ctsServerInitialize.Dispose();
                _ctsServerInitialize = null;
                _serverProcess.Dispose();
                _serverProcess = null;
                throw;
            }
        }

        private void ServerProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data is not null)
                _logger.LogError("[SRV] STDERR: {Line}", e.Data);
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

                    await _serverExitCompletion.Task;
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
        static string FindServerExecutable()
        {
            // Find the git root directory by looking for .git folder
            string gitRoot = FindGitRoot();
            if (gitRoot == null)
            {
                // Fallback: try relative to test assembly
                string testAssemblyDir = Path.GetDirectoryName(typeof(LanguageServerFixture).Assembly.Location);
                gitRoot = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", ".."));
            }

            string serverProjectDir = Path.Combine(gitRoot, "src", "LanguageServer");
            string binDir = Path.Combine(serverProjectDir, "bin");
            
            // Check if bin directory exists to avoid hangs or exceptions on Linux
            if (!Directory.Exists(binDir))
            {
                throw new FileNotFoundException($"Language server bin directory not found: {binDir}");
            }

            var dlls = Directory.GetFiles(binDir, ServerDllName, SearchOption.AllDirectories);

            if (dlls.Length == 0)
            {
                throw new FileNotFoundException($"Language server executable '{ServerDllName}' not found in: {binDir}");
            }

            return dlls[0];
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
                if (Directory.Exists(Path.Combine(currentDir, ".git")))
                {
                    return currentDir;
                }

                currentDir = Path.GetDirectoryName(currentDir);
                counter++;

                if (counter > 10) // Prevent infinite loop
                    break;
            }

            return null;
        }
    }
}
