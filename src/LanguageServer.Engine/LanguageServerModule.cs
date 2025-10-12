using Autofac;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using MSLogging = Microsoft.Extensions.Logging;

namespace MSBuildProjectTools.LanguageServer
{
    using CompletionProviders;
    using CustomProtocol;
    using Diagnostics;
    using Handlers;
    using OmniSharp.Extensions.JsonRpc;
    using LanguageServer = OmniSharp.Extensions.LanguageServer.Server.LanguageServer;

    /// <summary>
    ///     Registration logic for language server components.
    /// </summary>
    public class LanguageServerModule
        : Module
    {
        /// <summary>
        ///     Create a new <see cref="LanguageServerModule"/>.
        /// </summary>
        public LanguageServerModule()
        {
        }

        /// <summary>
        ///     Configure language server components.
        /// </summary>
        /// <param name="builder">
        ///     The container builder to configure.
        /// </param>
        protected override void Load(ContainerBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.RegisterInstance(Configuration).AsSelf();

            builder
                .Register(componentContext =>
                {
                    ILanguageServer languageServer = LanguageServer.From(options =>
                    {
                        options.Input = Console.OpenStandardInput();
                        options.Output = Console.OpenStandardOutput();
                        options.LoggerFactory = componentContext.Resolve<MSLogging.ILoggerFactory>();

                        var configurationHandler = componentContext.Resolve<ConfigurationHandler>();

                        options.OnInitialize(initializationParameters =>
                        {
                            configurationHandler.Configuration.UpdateFrom(initializationParameters);
                            if (configurationHandler.Configuration.Logging.Level < LogEventLevel.Verbose)
                                options.MinimumLogLevel = MSLogging.LogLevel.Warning;

                            // Handle subsequent logging configuration changes.
                            configurationHandler.ConfigurationChanged += (sender, args) =>
                            {
                                if (configurationHandler.Configuration.Logging.Level < LogEventLevel.Verbose)
                                    componentContext.Resolve<LanguageServer>().MinimumLogLevel = MSLogging.LogLevel.Warning;
                            };

                            return Task.CompletedTask;
                        });
                    }).GetAwaiter().GetResult();

                    return (LanguageServer)languageServer;
                })
                .AsSelf()
                .As<ILanguageServer>()
                .SingleInstance()
                .OnActivated(activated =>
                {
                    ILanguageServer languageServer = activated.Instance;

                    // Register configuration handler (which is not a Handler).
                    var configurationHandler = activated.Context.Resolve<ConfigurationHandler>();
                    languageServer.AddHandlers(configurationHandler);

                    // Register all other handlers.
                    var handlers = activated.Context.Resolve<IEnumerable<Handler>>();
                    foreach (Handler handler in handlers)
                        languageServer.AddHandlers(handler);
                });

            builder.RegisterType<LspDiagnosticsPublisher>()
                .As<IPublishDiagnostics>()
                .InstancePerDependency();

            builder.RegisterType<ConfigurationHandler>()
                .AsSelf()
                .SingleInstance();

            builder.RegisterType<Documents.Workspace>()
                .AsSelf()
                .SingleInstance()
                .OnActivated(activated =>
                {
                    Documents.Workspace workspace = activated.Instance;
                    workspace.RestoreTaskMetadataCache();
                });

            builder
                .RegisterTypes(
                    typeof(ConfigurationHandler),
                    typeof(DocumentSyncHandler),
                    typeof(DocumentSymbolHandler),
                    typeof(DefinitionHandler),
                    typeof(HoverHandler)
                )
                .AsSelf()
                .As<Handler>()
                .SingleInstance();

            builder.RegisterType<CompletionHandler>()
                .AsSelf().As<Handler>()
                .SingleInstance();

            Type completionProviderType = typeof(CompletionProvider);
            builder.RegisterAssemblyTypes(ThisAssembly)
                .Where(
                    type => type.IsSubclassOf(completionProviderType) && !type.IsAbstract
                )
                .AsSelf()
                .As<CompletionProvider>()
                .As<ICompletionProvider>()
                .SingleInstance();
        }

        /// <summary>
        ///     The language server configuration.
        /// </summary>
        public Configuration Configuration { get; } = new Configuration();
    }
}
