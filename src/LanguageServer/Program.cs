using Autofac;
using OmniSharp.Extensions.LanguageServer;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using LSP = OmniSharp.Extensions.LanguageServer;

namespace MSBuildProjectTools.LanguageServer
{
    using Documents;
    using Handlers;
    using Logging;
    using Utilities;

    /// <summary>
    ///     The MSBuild language server.
    /// </summary>
    static class Program
    {
        /// <summary>
        ///     The main program entry-point.
        /// </summary>
        /// <returns>
        ///     The process exit code.
        /// </returns>
        static int Main()
        {
            SynchronizationContext.SetSynchronizationContext(
                new SynchronizationContext()
            );

            try
            {
                AutoDetectExtensionDirectory();

                return AsyncMain().GetAwaiter().GetResult();
            }
            catch (AggregateException aggregateError)
            {
                foreach (Exception unexpectedError in aggregateError.Flatten().InnerExceptions)
                    Console.Error.WriteLine(unexpectedError);

                return 1;
            }
            catch (Exception unexpectedError)
            {
                Console.Error.WriteLine(unexpectedError);

                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        ///     The asynchronous program entry-point.
        /// </summary>
        /// <returns>
        ///     The process exit code.
        /// </returns>
        static async Task<int> AsyncMain()
        {
            using (ActivityCorrelationManager.BeginActivityScope())
            using (Terminator terminator = new Terminator())
            using (IContainer container = BuildContainer())
            {
                // Force initialisation of logging.
                ILogger log = container.Resolve<ILogger>().ForContext(typeof(Program));

                log.Debug("Creating language server...");

                var server = container.Resolve<LSP.Server.LanguageServer>();

                log.Debug("Waiting for client to initialise language server...");

                Task initializeTask = server.Initialize();

                // Special case for probing whether language server is startable given current runtime environment.
                string[] commandLineArguments = Environment.GetCommandLineArgs();
                if (commandLineArguments.Length == 2 && commandLineArguments[1] == "--probe")
                {
                    // Give the language server a chance to start.
                    await Task.Yield();

                    // Show any exception encountered while starting the language server.
                    if (initializeTask.IsFaulted || initializeTask.IsCanceled)
                        await initializeTask;

                    Console.Error.WriteLine("PROBE: Yes, the language server can start.");

                    return 0;
                }

                await initializeTask;

                log.Debug("Language server initialised by client.");

                await server.WasShutDown;

                log.Debug("Language server is shutting down...");

                await server.WaitForExit;

                log.Debug("Server has shut down. Preparing to terminate server process...");

                // AF: Temporary fix for tintoy/msbuild-project-tools-vscode#36
                //
                //     The server hangs while waiting for LSP's ProcessScheduler thread to terminate so, after a timeout has elapsed, we forcibly terminate this process.
                terminator.TerminateAfter(
                    TimeSpan.FromSeconds(3)
                );

                log.Debug("Server process is ready to terminate.");

                return 0;
            }
        }

        /// <summary>
        ///     Build a container for language server components.
        /// </summary>
        /// <returns>
        ///     The container.
        /// </returns>
        static IContainer BuildContainer()
        {
            ContainerBuilder builder = new ContainerBuilder();
            
            builder.RegisterModule<LoggingModule>();
            builder.RegisterModule<LanguageServerModule>();

            return builder.Build();
        }

        /// <summary>
        ///     Auto-detect the directory containing the extension's files.
        /// </summary>
        static void AutoDetectExtensionDirectory()
        {
            string extensionDir = Environment.GetEnvironmentVariable("MSBUILD_PROJECT_TOOLS_DIR");
            if (String.IsNullOrWhiteSpace(extensionDir))
            {
                extensionDir = Path.Combine(
                    AppContext.BaseDirectory, "..", ".."
                );
            }
            extensionDir = Path.GetFullPath(extensionDir);
            Environment.SetEnvironmentVariable("MSBUILD_PROJECT_TOOLS_DIR", extensionDir);
        }
    }
}
