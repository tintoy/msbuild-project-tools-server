using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
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
        LanguageClient _client;

        /// <summary>
        /// The language client.
        /// </summary>
        public LanguageClient Client => _client;

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
                WorkingDirectory = Path.GetDirectoryName(serverExecutable)
            };

            try
            {
                // Create and initialize the language client
                _client = new LanguageClient(loggerFactory, serverInfo);

                logger?.LogInformation("Initializing language client...");

                await _client.Initialize(workspaceRoot);
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
            var dlls = Directory.GetFiles(Path.Combine(serverProjectDir, "bin"), ServerDllName, SearchOption.AllDirectories);

            return Array.Find(dlls, File.Exists) ?? throw new FileNotFoundException("Language server executable not found.");
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

            while (currentDir != null)
            {
                if (Directory.Exists(Path.Combine(currentDir, ".git")))
                {
                    return currentDir;
                }

                currentDir = Path.GetDirectoryName(currentDir);
            }

            return null;
        }
    }
}
