using Autofac;
using Microsoft.Build.Locator;
using NuGet.Credentials;
using Serilog;
using System;
using System.Linq;
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
        /// The minimum version of the .NET Core SDK supported by the language server.
        /// </summary>
        static readonly Version TargetSdkMinVersion = new Version("5.0.102");

        /// <summary>
        /// The maximum version of the .NET Core SDK supported by the language server.
        /// </summary>
        static readonly Version TargetSdkMaxVersion = new Version("5.0.999");
        
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

                DiscoverMSBuildEngine();
                ConfigureNuGetCredentialProviders();

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

        /// <summary>
        ///     Find and use the latest version of the MSBuild engine.
        /// </summary>
        static void DiscoverMSBuildEngine()
        {
            // Assume working directory is VS code's current working directory (i.e. the workspace root).
            //
            // Really, until we figure out a way to change the version of MSBuild we're using after the server has started,
            // we're still going to have problems here.
            //
            // In the end we will probably wind up having to move all the MSBuild stuff out to a separate process, and use something like GRPC (or even Akka.NET's remoting) to communicate with it.
            // It can be stopped and restarted by the language server (even having different instances for different SDK / MSBuild versions).
            //
            // This will also ensure that the language server's model doesn't expose any MSBuild objects anywhere.
            //
            // For now, though, let's choose the dumb option.
            DotNetRuntimeInfo runtimeInfo = DotNetRuntimeInfo.GetCurrent();
            Version targetSdkVersion = new Version(runtimeInfo.SdkVersion);

            var queryOptions = new VisualStudioInstanceQueryOptions
            {
                // We can only load the .NET Core MSBuild engine
                DiscoveryTypes = DiscoveryType.DotNetSdk
            };

            VisualStudioInstance[] allInstances = MSBuildLocator
                .QueryVisualStudioInstances(queryOptions)
                .ToArray();

            VisualStudioInstance latestInstance = allInstances
                .OrderByDescending(instance => instance.Version)
                .FirstOrDefault(instance =>
                    // We need a version of MSBuild for the currently-supported SDK
                    instance.Version == targetSdkVersion
                );

            if (latestInstance == null)
            {
                string foundVersions = String.Join(", ", allInstances.Select(instance => instance.Version));

                throw new Exception($"Cannot locate MSBuild engine for .NET SDK v{targetSdkVersion}. This probably means that MSBuild Project Tools cannot find the MSBuild for the current project instance. It did find the following version(s), though: [{foundVersions}].");
            }

            MSBuildLocator.RegisterInstance(latestInstance);
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
