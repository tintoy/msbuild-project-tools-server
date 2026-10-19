using Autofac;
using NuGet.Credentials;
using Serilog;
using System;
using System.IO;
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

            EnsureMSBuildEnvironment();

            try
            {
                // Ensure the initial MSBuild discovery process has a logger to work with.
                ILogger msbuildDiscoveryLogger = LoggingModule.CreateDefaultLoggerConfiguration()
                    .CreateLogger()
                    .ForContext("Operation", "MSBuildDiscovery");

                using (msbuildDiscoveryLogger as IDisposable)
                {
                    string workingDirectory = Directory.GetCurrentDirectory();

                    try
                    {


                        MSBuildHelper.DiscoverMSBuildEngine(baseDirectory: workingDirectory, logger: msbuildDiscoveryLogger);
                    }
                    catch (DotnetDiscoveryException dotnetDiscoveryFailure) when (dotnetDiscoveryFailure.DotNetHostExitCode == DotNetHostExitCode.SdkResolverResolveFailure || dotnetDiscoveryFailure.DotNetHostExitCode == DotNetHostExitCode.LibHostSdkFindFailure)
                    {
                        msbuildDiscoveryLogger.Error(dotnetDiscoveryFailure, "Cannot find a compatible version of the .NET SDK for use with working directory {WorkingDirectory}.", workingDirectory);

                        return ExitCode.NoCompatibleSdk;
                    }
                }

                ConfigureNuGetCredentialProviders();

                using (ActivityCorrelationManager.BeginActivityScope())
                using (var terminator = new Terminator())
                using (IContainer container = BuildContainer())
                {
                    // Force initialization of logging.
                    ILogger log = container.Resolve<ILogger>().ForContext(typeof(Program));
                    terminator.Log = log.ForContext<Terminator>();

                    log.Debug("Creating language server...");

                    var serverInitTask = container.Resolve<Task<LSP.Server.LanguageServer>>();

                    log.Debug("Waiting for client to initialize language server...");

                    var server = await serverInitTask;

                    log.Debug("Language server initialized by client.");

                    if (server.ClientSettings.ProcessId != null)
                    {
                        terminator.Initialize(
                            (int)server.ClientSettings.ProcessId.Value
                        );
                    }

                    await server.WasShutDown;

                    log.Debug("Language server is shutting down...");

                    await server.WaitForExit;

                    log.Debug("Server has shut down. Preparing to terminate server process...");

                    log.Debug("Server process is ready to terminate.");

                    return ExitCode.Success;
                }
            }
            catch (Exception unexpectedError)
            {
                Console.Error.WriteLine(unexpectedError);

                return ExitCode.UnexpectedError;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        ///     Well-known process exit codes for the language-server host.
        /// </summary>
        public static class ExitCode
        {
            /// <summary>
            ///     The language-server host terminated after graceful shutdown.
            /// </summary>
            public static readonly int Success = 0;

            /// <summary>
            ///     The language-server host terminated due to an unexpected error.
            /// </summary>
            public static readonly int UnexpectedError = 1;

            /// <summary>
            ///     The language-server host terminated because it was unable to find a compatible version of the .NET SDK for the current working directory.
            /// </summary>
            public static readonly int NoCompatibleSdk = 2;
        }

        /// <summary>
        ///     Ensures that either both DOTNET_HOST_PATH and DOTNET_ROOT are initialized or none of them
        /// </summary>
        static void EnsureMSBuildEnvironment()
        {
            var dotnetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");

            // If DOTNET_HOST_PATH is present, but DOTNET_ROOT isn't set DOTNET_ROOT to be the directory of DOTNET_HOST_PATH
            if (dotnetHostPath is not null && dotnetRoot is null)
            {
                var dotnetRootFromHostPath = Path.GetDirectoryName(dotnetHostPath);
                Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetRootFromHostPath);
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
            var builder = new ContainerBuilder();

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
