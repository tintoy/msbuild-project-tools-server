using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
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
        /// <param name="loggerFactory">
        /// An optional logger factory for the client.
        /// </param>
        /// <param name="cancellationToken">
        /// An optional cancellation token.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        public async Task StartAsync(string workspaceRoot, ILoggerFactory loggerFactory)
        {
            if (_client != null)
                throw new InvalidOperationException("Language server is already started.");

            // Find the language server executable
            string serverExecutable = FindServerExecutable();
            if (string.IsNullOrEmpty(serverExecutable))
                throw new FileNotFoundException("Cannot find the language server executable.");

            var logger = loggerFactory?.CreateLogger("LanguageServerFixture");

            logger?.LogInformation("Starting language server from: {ServerExecutable}", serverExecutable);

            // Create the server process info, 
            // Important: run from the directory containing the DLL
            var serverInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = ServerDllName,
                WorkingDirectory = Path.GetDirectoryName(serverExecutable),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment = {
                     { "MSBUILD_PROJECT_TOOLS_VERBOSE_LOGGING", "1" }
                }
            };

            _serverProcess = Process.Start(serverInfo);
            logger.LogInformation("Language server process started. PID: {ServerProcessId}", _serverProcess.Id);

            try
            {
                // Create and initialize the language client
                var options = new LanguageClientOptions()
                    .WithLoggerFactory(loggerFactory)
                    .WithInput(_serverProcess.StandardOutput.BaseStream)
                    .WithOutput(_serverProcess.StandardInput.BaseStream)
                    .WithRootUri(DocumentUri.FromFileSystemPath(workspaceRoot));
                options.OnLogMessage(message =>
                {
                    switch (message.Type)
                    {
                        case MessageType.Error:
                            logger?.LogError("[CLT] {Msg}", message); break;
                        case MessageType.Warning:
                            logger?.LogWarning("[CLT] {Msg}", message); break;
                        case MessageType.Info:
                            logger?.LogInformation("[CLT] {Msg}", message); break;
                        case MessageType.Log:
                            logger?.LogDebug("[CLT] {Msg}", message); break;
                    }
                });
                _client = await LanguageClient.From(options);

                logger?.LogInformation("Initializing language client...");

                await _client.Initialize(default);
                logger?.LogInformation("Language client initialized.");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to initialize language client");
                throw;
            }
        }

        /// <summary>
        /// Stop the language server and disconnect the client.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        public async Task StopAsync()
        {
            if (_client != null)
            {
                try
                {
                    await _client.Shutdown();
                }
                catch (Exception)
                {
                    // Ignore errors during shutdown
                }

                _client = null;
            }

            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                try
                {
                    _serverProcess.Kill();
                    _serverProcess.WaitForExit(5000);
                }
                catch (Exception)
                {
                    // Ignore errors during process termination
                }

                _serverProcess?.Dispose();
                _serverProcess = null;
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
