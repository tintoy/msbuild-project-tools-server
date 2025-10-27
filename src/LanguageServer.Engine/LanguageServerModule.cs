using Autofac;
using Autofac.Core;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using MSLogging = Microsoft.Extensions.Logging;

namespace MSBuildProjectTools.LanguageServer
{
    using CompletionProviders;
    using CustomProtocol;
    using Diagnostics;
    using Handlers;
    using Utilities;
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
                    // Can't use current context to resolve services from another lambda, when
                    // this lambda is invoked at a later time. So re-resolve this context again
                    // and use that in the other lambda.
                    // see: https://stackoverflow.com/questions/52084803/system-objectdisposedexception-this-resolve-operation-has-already-ended
                    // Using this new context also breaks circular dependency detection, but by
                    // passing ServiceProviderParameter around, dependencies on ILanguageServer
                    // or LanguageServer will be resolved directly from the service provider
                    // instead of Autofac, so it can also break out of the endless loop.
                    var currentScope = componentContext.Resolve<IComponentContext>();
                    return new LanguageServerOptions()
                        .WithInput(Console.OpenStandardInput())
                        .WithOutput(Console.OpenStandardOutput())
                        .WithLoggerFactory(componentContext.Resolve<MSLogging.ILoggerFactory>())
                        .AddDefaultLoggingProvider()
                        // New LSP C# implementation uses its own DI container, so configure it here with
                        // the same registrations from Autofac.
                        .WithServices(services =>
                        {
                            // Can't directly resolve some component instances here when registering
                            // services for the other DI container, as it would result in an endless dependency loop, e.g.:
                            // ILanguageServer -> Task<ILanguageServer> -> LanguageServerOptions -> CompletionHandler -> ILanguageServer -> ...
                            // ILogger -> ILanguageServer -> Task<ILanguageServer> -> LanguageServerOptions -> ILogger -> ...
                            // So do the registrations with factory delegates using the current lifetime scope as
                            // component context and ServiceProviderParameter to shortcut some resolve operations.
                            // At the time when this factory delegate is executed, the current ILanguageServer
                            // instance already exists in the service provider as singleton instance and doesn't
                            // need to be resolved from Autofac.
                            services.AddSingleton(_ => currentScope.Resolve<Configuration>());
                            services.AddSingleton(sp => currentScope.Resolve<ILogger>(sp.ToAutofacParameter()));
                            services.AddTransient(sp => currentScope.Resolve<IPublishDiagnostics>(sp.ToAutofacParameter()));
                            services.AddSingleton(sp => currentScope.Resolve<Documents.Workspace>(sp.ToAutofacParameter()));

                            static void addRegistrations(IServiceCollection services, IComponentContext componentContext, bool addOnlyConcreteType, Type baseType, params Type[] additionalServiceTypes)
                            {
                                var registrations = componentContext.ComponentRegistry
                                    .ServiceRegistrationsFor(new TypedService(baseType));
                                var filterTypes = baseType.Yield().Concat(additionalServiceTypes);
                                foreach (var reg in registrations)
                                {
                                    var serviceTypes = reg.Registration.Services.OfType<IServiceWithType>()
                                                                                .Select(s => s.ServiceType);
                                    var concreteType = serviceTypes.Except(filterTypes)
                                                                .FirstOrDefault() ?? baseType;

                                    services.AddSingleton(concreteType, sp =>
                                        componentContext.ResolveComponent(
                                            new ResolveRequest(new TypedService(concreteType), reg,
                                                new[] { sp.ToAutofacParameter() })
                                            ));
                                    if (!addOnlyConcreteType)
                                    {
                                        foreach (var serviceType in serviceTypes.Except(concreteType.Yield()))
                                            services.AddSingleton(serviceType,
                                                sp => sp.GetService(concreteType));
                                    }
                                }
                            }

                            // Register all handlers. Not needed any more since v0.11.1 of OmniSharp LSP libs.
                            //addRegistrations(services, currentScope, true, typeof(Handler));

                            // Register all completion providers.
                            addRegistrations(services, currentScope, false, typeof(ICompletionProvider), typeof(CompletionProvider));
                        })
                        .OnInitialize((languageServer, initializationParameters) =>
                        {
                            var configurationHandler = currentScope.Resolve<ConfigurationHandler>();

                            void configureServerLogLevel()
                            {
                                if (configurationHandler.Configuration.Logging.Level < LogEventLevel.Verbose)
                                    ((LanguageServer)languageServer).MinimumLogLevel = MSLogging.LogLevel.Warning;
                            }

                            configurationHandler.Configuration.UpdateFrom(initializationParameters);
                            configureServerLogLevel();

                            // Handle subsequent logging configuration changes.
                            configurationHandler.ConfigurationChanged += (sender, args) => configureServerLogLevel();

                            // Register all handlers. Now possible inside OnInitialize since v0.11.1 of OmniSharp LSP libs.
                            languageServer.AddHandlers(currentScope.Resolve<IEnumerable<Handler>>().ToArray());

                            return Task.CompletedTask;
                        });
                })
                .AsSelf()
                .SingleInstance();

            builder
                .Register(componentContext => LanguageServer.From(
                    componentContext.Resolve<LanguageServerOptions>()
                ))
                .AsSelf()
                .SingleInstance();

            builder
                .RegisterAdapter<Task<ILanguageServer>, Task<LanguageServer>>(
                    async task => (LanguageServer)await task)
                .SingleInstance();

            // Can't use adapter here, because the "parameters" parameter of this lambda
            // will always be empty and the component in the "from" parameter is already
            // unconditionally resolved, which can lead to an endless loop in this specific
            // case.
            /*builder
                .RegisterAdapter<Task<ILanguageServer>, LanguageServer>(
                (componentContext, parameters, from) =>
                {
                    var options = componentContext.Resolve<LanguageServerOptions>();
                    using var sp = options.Services.BuildServiceProvider();
                    return (LanguageServer)sp.GetRequiredService<ILanguageServer>();
                })
                .As<LanguageServer>()   // AsSelf() not available on adapter registration,
                                        // but it needs to register itself as service type,
                                        // so do it manually.
                .As<ILanguageServer>()
                .SingleInstance();*/

            var rb = builder
                .Register((componentContext, parameters) =>
                {
                    var sp = parameters.OfType<IServiceProvider>().FirstOrDefault();
                    if (sp == null)
                    {
                        // At this point, there is no service provider in parameters,
                        // which LanguageServer had created, so this may be the first
                        // resolution and a new LanguageServer instance should be created
                        // by resolving Task<ILanguageServer>.
                        // If it wasn't the first resolution, then this should return the
                        // cached singleton instance of Task<ILanguageServer> and the task
                        // could already be completed, in this case the Result of the Task can
                        // directly be returned without blocking.
                        var from = componentContext.Resolve<Task<ILanguageServer>>(parameters);
                        if (from.IsCompletedSuccessfully)
                            return (LanguageServer)from.Result;
                        // If the task isn't completed yet, return LanguageServer instance from
                        // services collection of LanguageServerOptions which was created by
                        // resolving Task<ILanguageServer>. Use a new temporary service provider
                        // for retrieving the instance.
                        var options = componentContext.Resolve<LanguageServerOptions>(parameters);
                        sp = options.Services.BuildServiceProvider();
                    }
                    try
                    {
                        return (LanguageServer)sp.GetRequiredService<ILanguageServer>();
                    }
                    finally
                    {
                        (sp as IDisposable)?.Dispose();
                    }
                })
                // Mimic the behavior of an adapter registration with the following,
                // so keep similar semantics.
                .AsAdapter(builder).For<Task<ILanguageServer>>()
                .AsSelf()
                .As<ILanguageServer>()
                // This component is actually a singleton, but it could be resolved recursively,
                // which this IoC implementation sees as an error. So every dependency should
                // create a new scope before resolving this component and the registration
                // needs to be configured as per LifetimeScope.
                .InstancePerLifetimeScope()
                // Every instance returned of this registration is the same singleton instance and it is disposable,
                // so register disposing of the instance only in the root LifetimeScope.
                .OwnedByRootLifetimeScope();

            builder.RegisterType<LspDiagnosticsPublisher>()
                .As<IPublishDiagnostics>()
                .InstancePerDependency();

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
                    typeof(HoverHandler),
                    typeof(CompletionHandler)
                )
                .AsSelf()
                .As<Handler>()
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
