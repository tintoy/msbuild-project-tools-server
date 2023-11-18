using Autofac;
using NuGet.Credentials;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

using LSP = OmniSharp.Extensions.LanguageServer;
using NuGetNullLogger = NuGet.Common.NullLogger;

namespace MSBuildProjectTools.LanguageServer
{
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
        private static async Task<int> Main()
        {
            SynchronizationContext.SetSynchronizationContext(
                new SynchronizationContext()
            );

            try
            {
                // Ensure the initial MSBuild discovery process has a logger to work with.
                ILogger msbuildDiscoveryLogger = LoggingModule.CreateDefaultLoggerConfiguration()
                    .CreateLogger()
                    .ForContext("Operation", "MSBuildDiscovery");

                using (msbuildDiscoveryLogger as IDisposable)
                {
                    MSBuildHelper.DiscoverMSBuildEngine(logger: msbuildDiscoveryLogger);
                }

                ConfigureNuGetCredentialProviders();

                using (ActivityCorrelationManager.BeginActivityScope())
                using (Terminator terminator = new Terminator())
                using (IContainer container = BuildContainer())
                {
                    // Force initialization of logging.
                    ILogger log = container.Resolve<ILogger>().ForContext(typeof(Program));

                    log.Debug("Creating language server...");

                    var server = container.Resolve<LSP.Server.LanguageServer>();

                    log.Debug("Waiting for client to initialize language server...");

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

                    log.Debug("Language server initialized by client.");

                    if (server.Client.ProcessId != null)
                    {
                        terminator.Initialize(
                            (int)server.Client.ProcessId.Value
                        );
                    }

                    await server.WasShutDown;

                    log.Debug("Language server is shutting down...");

                    await server.WaitForExit;

                    log.Debug("Server has shut down. Preparing to terminate server process...");

                    log.Debug("Server process is ready to terminate.");

                    return 0;
                }
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
        ///     Configure NuGet's credential providers (i.e. support for authenticated package feeds).
        /// </summary>
        static void ConfigureNuGetCredentialProviders()
        {
            DefaultCredentialServiceUtility.SetupDefaultCredentialService(
                logger: NuGetNullLogger.Instance,
                nonInteractive: true
            );
        }
    }
}
