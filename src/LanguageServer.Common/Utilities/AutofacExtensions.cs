using Autofac;
using Autofac.Builder;
using Autofac.Core;
using Autofac.Core.Registration;
using Autofac.Core.Resolving.Pipeline;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    /// Extension methods for working with Autofac.
    /// </summary>
    public static class AutofacExtensions
    {
        private sealed class ServiceProviderParameter : Parameter, IServiceProvider
        {
            private readonly IServiceProvider _sp;

            public ServiceProviderParameter(IServiceProvider sp)
            {
                _sp = sp;
            }

            /// <inheritdoc/>
            public object GetService(Type serviceType)
            {
                return _sp.GetService(serviceType);
            }

            /// <summary>
            ///     Returns true if the parameter is able to provide a value to a particular site.
            /// </summary>
            /// <param name="pi">
            ///     Constructor, method, or property-mutator parameter.
            /// </param>
            /// <param name="context">
            ///     The component context in which the value is being provided.
            /// </param>
            /// <param name="valueProvider">
            ///     If the result is true, the valueProvider parameter will be set to a function
            ///     that will lazily retrieve the parameter value. If the result is false, will be
            ///     set to null.
            /// </param>
            /// <returns>
            ///     True if a value can be supplied; otherwise, false.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            ///     Thrown if pi or context is null.
            /// </exception>
            public override bool CanSupplyValue(System.Reflection.ParameterInfo pi, IComponentContext context, out Func<object> valueProvider)
            {
                if (pi == null)
                    throw new ArgumentNullException(nameof(pi));

                if (context == null)
                    throw new ArgumentNullException(nameof(context));

                var paramType = pi.ParameterType;
                bool isEnumerableParam = paramType.IsConstructedGenericType &&
                    paramType.GetGenericTypeDefinition() == typeof(IEnumerable<>);
                object value;
                if ((value = _sp.GetService(paramType)) != null &&
                    (!isEnumerableParam || (value is IEnumerable enumerable &&
                    enumerable.Any())))
                {
                    valueProvider = () => value;
                    return true;
                }

                valueProvider = null;
                return false;
            }
        }

        private sealed class NewScopeLifetime : IComponentLifetime, IResolveMiddleware
        {
            private readonly Dictionary<ILifetimeScope, bool> _scopes = new Dictionary<ILifetimeScope, bool>();
            private readonly Guid _registrationId;
            private readonly object _newScopeTag;

            public NewScopeLifetime(Guid id, object tag)
            {
                _registrationId = id;
                _newScopeTag = tag;
            }

            //Can only be used with Autofac until version 4.9.4
            /*internal void OnResolveOperationBeginning(object sender, ResolveOperationBeginningEventArgs args)
            {
                EventHandler<ResolveOperationEndingEventArgs> handler = null;
                handler = (s, e) =>
                {
                    e.ResolveOperation.InstanceLookupBeginning -= OnInstanceLookupBeginning;
                    e.ResolveOperation.CurrentOperationEnding -= handler;
                };
                args.ResolveOperation.InstanceLookupBeginning += OnInstanceLookupBeginning;
                args.ResolveOperation.CurrentOperationEnding += handler;
            }

            private void OnInstanceLookupBeginning(object sender, InstanceLookupBeginningEventArgs args)
            {
                if (args.InstanceLookup.ComponentRegistration.Id != _registrationId ||
                    !_scopes.TryGetValue(args.InstanceLookup.ActivationScope, out bool instanceActivated) ||
                    instanceActivated)
                    return;

                args.InstanceLookup.InstanceLookupEnding += OnInstanceLookupEnding;
            }

            private void OnInstanceLookupEnding(object sender, InstanceLookupEndingEventArgs args)
            {
                if (args.NewInstanceActivated)
                    _scopes[args.InstanceLookup.ActivationScope] = true;
                args.InstanceLookup.InstanceLookupEnding -= OnInstanceLookupEnding;
            }*/

            /// <summary>
            ///     Given the most nested scope visible within the resolve operation,
            ///     find the scope for the component.
            /// </summary>
            /// <param name="mostNestedVisibleScope">
            ///     The most nested visible scope.
            /// </param>
            /// <returns>
            ///     The scope for the component.
            /// </returns>
            /// <exception cref="ArgumentNullException"></exception>
            public ISharingLifetimeScope FindScope(ISharingLifetimeScope mostNestedVisibleScope)
            {
                if (mostNestedVisibleScope == null)
                    throw new ArgumentNullException(nameof(mostNestedVisibleScope));

                if (!_scopes.TryGetValue(mostNestedVisibleScope, out bool instanceActivated) || instanceActivated)
                {
                    if (!instanceActivated)
                        _scopes.Add(mostNestedVisibleScope, false);
                    return mostNestedVisibleScope;
                }

                static bool isChildOfScope(ISharingLifetimeScope childScope, ISharingLifetimeScope parentScope)
                {
                    while (childScope != null)
                    {
                        if ((childScope = childScope.ParentLifetimeScope) == parentScope)
                            return true;
                    }
                    return false;
                }

                foreach (var (scope, activated) in _scopes)
                {
                    if (!activated)
                        continue;
                    if (scope is ISharingLifetimeScope sharingScope && isChildOfScope(sharingScope, mostNestedVisibleScope))
                        return sharingScope;
                }

                var childScope = _newScopeTag != null ?
                    mostNestedVisibleScope.BeginLifetimeScope(_newScopeTag) : mostNestedVisibleScope.BeginLifetimeScope();
                mostNestedVisibleScope.Disposer.AddInstanceForDisposal(childScope);
                //childScope.ResolveOperationBeginning += OnResolveOperationBeginning;
                _scopes.Add(childScope, false);
                return (ISharingLifetimeScope)childScope;
            }

            public PipelinePhase Phase => PipelinePhase.Sharing;

            public void Execute(ResolveRequestContext context, Action<ResolveRequestContext> next)
            {
                next(context);

                if (context.Registration.Id == _registrationId &&
                    _scopes.TryGetValue(context.ActivationScope, out bool instanceActivated) &&
                    !instanceActivated && context.NewInstanceActivated)
                    _scopes[context.ActivationScope] = true;
            }
        }

        const string RegistrationOrderMetadataKey = "__RegistrationOrder";

        /// <summary>
        ///     Configure the component so that every dependent component or call to Resolve()
        ///     within a single ILifetimeScope gets the same, shared instance, when this
        ///     instance was already activated in its ILifetimeScope. When a new instance
        ///     in this same ILifetimeScope is about to be activated for the first time,
        ///     this ILifetimeScope is used for it, otherwise a new (temporary) ILifetimeScope
        ///     will be created for it.
        /// </summary>
        /// <remarks>
        ///     This can be used to resolve recursive dependencies on the same component
        ///     before the first activation ends. This is also called circular dependencies,
        ///     but it can't be detected for dependencies that are resolved within a
        ///     IResolveOperation other to that of the component which started the first
        ///     resolve.
        /// </remarks>
        /// <typeparam name="TLimit">
        ///     Registration limit type.
        /// </typeparam>
        /// <typeparam name="TConcreteActivatorData">
        ///     Activator data type.
        /// </typeparam>
        /// <param name="rb">
        ///     The registration builder.
        /// </param>
        /// <param name="builder">
        ///     The container builder to configure.
        /// </param>
        /// <returns>
        ///     A registration builder allowing further configuration of the component.
        /// </returns>
        public static IRegistrationBuilder<TLimit, TConcreteActivatorData, SingleRegistrationStyle>
            LifetimeScopePerInstance<TLimit, TConcreteActivatorData>(
                this IRegistrationBuilder<TLimit, TConcreteActivatorData, SingleRegistrationStyle> rb, ContainerBuilder builder)
            where TConcreteActivatorData : IConcreteActivatorData
        {
            rb.RegistrationData.Sharing = InstanceSharing.Shared;
            var lifetime = new NewScopeLifetime(rb.RegistrationStyle.Id, new TypedService(rb.ActivatorData.Activator.LimitType));
            rb.RegistrationData.Lifetime = lifetime;
            // A bug ignores MiddlewareInsertionMode of this insertion, because Autofac
            // is always adding default middlewares to the end of the pipeline builder.
            // See: https://github.com/autofac/Autofac/blob/0f4913ab3be8aa7d0654d6f35ca553fb97f5953e/src/Autofac/Core/Registration/ServiceRegistrationInfo.cs#L338
            // So to execute this middleware after default ScopeSelection middleware but
            // before Sharing phase, it needs to be inserted before the default Sharing middleware
            builder.RegisterServiceMiddleware<TLimit>(lifetime, MiddlewareInsertionMode.StartOfPhase);
            //builder.RegisterBuildCallback(container => container.ResolveOperationBeginning += lifetime.OnResolveOperationBeginning);
            return rb;
        }

        /// <summary>
        ///     Configure the component so that instances that support IDisposable are disposed
        ///     by the root container only.
        /// </summary>
        /// <typeparam name="TLimit">
        ///     The most specific type to which instances of the registration can be cast.
        /// </typeparam>
        /// <typeparam name="TActivatorData">
        ///     Activator builder type.
        /// </typeparam>
        /// <typeparam name="TRegistrationStyle">
        ///     Registration style type.
        /// </typeparam>
        /// <param name="rb">
        ///     The registration builder.
        /// </param>
        /// <returns>
        ///     A registration builder allowing further configuration of the component.
        /// </returns>
        public static IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle>
            OwnedByRootLifetimeScope<TLimit, TActivatorData, TRegistrationStyle>(
                this IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> rb)
        {
            return rb.ExternallyOwned()
                .OnActivating(activating =>
                {
                    if (activating.Instance is IDisposable instanceAsDisposable && activating.Context is ResolveRequestContext requestContext &&
                        requestContext.ActivationScope is ISharingLifetimeScope sharingScope && sharingScope == sharingScope.RootLifetimeScope)
                        sharingScope.Disposer.AddInstanceForDisposal(instanceAsDisposable);
                });
        }

        /// <summary>
        ///     Wrapper type to help in partial type inference for generic extension methods.
        /// </summary>
        /// <remarks>
        ///     Partial type inference on generic methods is still not possible with C#, see:
        ///     https://stackoverflow.com/questions/2893698/partial-generic-type-inference-possible-in-c
        ///     https://github.com/dotnet/csharplang/issues/1349
        /// </remarks>
        /// <typeparam name="TLimit">
        ///     The most specific type to which instances of the registration can be cast.
        /// </typeparam>
        /// <typeparam name="TActivatorData">
        ///     Activator data type.
        /// </typeparam>
        /// <typeparam name="TSingleRegistrationStyle">
        ///     Registration style.
        /// </typeparam>
        public struct AdapterExtensionWrapper<TLimit, TActivatorData, TSingleRegistrationStyle> :
            IRegistrationSource
            where TActivatorData : IConcreteActivatorData
            where TSingleRegistrationStyle : SingleRegistrationStyle
        {
            private readonly IRegistrationBuilder<TLimit, TActivatorData, TSingleRegistrationStyle> _rb;
            private readonly ContainerBuilder _builder;
            private Service _fromService;
            internal AdapterExtensionWrapper(IRegistrationBuilder<TLimit, TActivatorData, TSingleRegistrationStyle> rb, ContainerBuilder builder)
            {
                _rb = rb;
                _builder = builder;
            }

            /// <summary>
            ///     Specify the source service type for the adapter configuration.
            /// </summary>
            /// <typeparam name="TFrom">
            ///     Service type to adapt from.
            /// </typeparam>
            /// <returns>
            ///     A registration builder allowing further configuration of the component.
            /// </returns>
            public IRegistrationBuilder<TLimit, TActivatorData, TSingleRegistrationStyle>
                For<TFrom>()
            {
                _fromService = new TypedService(typeof(TFrom));
                IRegistrationSource adapterSource = this;
                _rb.RegistrationData.DeferredCallback = _builder.RegisterCallback(
                    cr => cr.AddRegistrationSource(adapterSource));
                return _rb;
                // The following is not possible since Autofac 6.0.0, because neither
                // ComponentRegistry nor IComponentRegistryServices is available in
                // the callback.
                /*var rb = _rb;
                return _rb.OnRegistered(registered =>
                {
                    //IComponentRegistryServices.TryGetRegistration;
                    if (registered.ComponentRegistryBuilder.TryGetRegistration(new TypedService(typeof(TFrom)), out var sourceReg))
                    {
                        var targetReg = rb.Targeting(sourceReg)
                            .InheritRegistrationOrderFrom(sourceReg)
                            .CreateRegistration();
                        registered.ComponentRegistryBuilder.Register(targetReg);
                    }
                });*/
            }

            /// <inheritdoc/>
            bool IRegistrationSource.IsAdapterForIndividualComponents => true;

            /// <inheritdoc/>
            IEnumerable<IComponentRegistration> IRegistrationSource.RegistrationsFor(
                Service service, Func<Service, IEnumerable<ServiceRegistration>> registrationAccessor)
            {
                if (service == null)
                    throw new ArgumentNullException(nameof(service));

                if (registrationAccessor == null)
                    throw new ArgumentNullException(nameof(registrationAccessor));

                if (_rb.RegistrationData.Services.Contains(service))
                {
                    var rb = _rb;
                    return registrationAccessor(_fromService)
                        .Select(sr =>
                        {
                            rb.RegistrationData.Options |= RegistrationOptions.Fixed;
                            return rb.Targeting(sr.Registration)
                                .InheritRegistrationOrderFrom(sr.Registration)
                                .CreateRegistration();
                        });
                }

                return Enumerable.Empty<IComponentRegistration>();
            }
        }

        /// <summary>
        ///     Configure the component so that it is registered like an adapter.
        /// </summary>
        /// <typeparam name="TTo">
        ///     Service type to adapt to. Must not be the same as TFrom.
        /// </typeparam>
        /// <typeparam name="TActivatorData">
        ///     Activator data type.
        /// </typeparam>
        /// <typeparam name="TSingleRegistrationStyle">
        ///     Registration style.
        /// </typeparam>
        /// <param name="rb">
        ///     The registration builder.
        /// </param>
        /// <param name="builder">
        ///     The container builder to configure.
        /// </param>
        /// <returns>
        ///     A wrapper type allowing further configuration of the adapter registration.
        /// </returns>
        public static AdapterExtensionWrapper<TTo, TActivatorData, TSingleRegistrationStyle>
            AsAdapter<TTo, TActivatorData, TSingleRegistrationStyle>(
                this IRegistrationBuilder<TTo, TActivatorData, TSingleRegistrationStyle> rb, ContainerBuilder builder)
                where TActivatorData : IConcreteActivatorData
                where TSingleRegistrationStyle : SingleRegistrationStyle
        {
            return new AdapterExtensionWrapper<TTo, TActivatorData, TSingleRegistrationStyle>(rb, builder);
        }

        /// <summary>
        ///     Configure the component so that it has the same registration order like another
        ///     component registration.
        /// </summary>
        /// <typeparam name="TLimit">
        ///     The most specific type to which instances of the registration can be cast.
        /// </typeparam>
        /// <typeparam name="TActivatorData">
        ///     Activator builder type.
        /// </typeparam>
        /// <typeparam name="TRegistrationStyle">
        ///     Registration style type.
        /// </typeparam>
        /// <param name="rb">
        ///     The registration builder.
        /// </param>
        /// <param name="registration">
        ///     The component registration from which the registration order is copied.
        /// </param>
        /// <returns>
        ///     A registration builder allowing further configuration of the component.
        /// </returns>
        public static IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle>
            InheritRegistrationOrderFrom<TLimit, TActivatorData, TRegistrationStyle>(
                this IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> rb,
                IComponentRegistration registration)
        {
            var sourceRegistrationOrder = registration.Metadata.TryGetValue(RegistrationOrderMetadataKey, out var value) ? (long)value : long.MaxValue;
            rb.RegistrationData.Metadata[RegistrationOrderMetadataKey] = sourceRegistrationOrder;
            return rb;
        }

        /// <summary>
        ///     Encapsulates the service provider in an Autofac parameter which
        ///     can be used to resolve dependencies from this service provider.
        /// </summary>
        /// <param name="sp">The service provider.</param>
        /// <returns>
        ///     An Autofac parameter which can be used to resolve dependencies from a
        ///     service provider.
        /// </returns>
        public static Parameter ToAutofacParameter(this IServiceProvider sp)
        {
            return new ServiceProviderParameter(sp);
        }
    }
}
