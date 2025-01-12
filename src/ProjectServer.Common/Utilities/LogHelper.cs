using Microsoft.Extensions.Logging;
using System;

namespace MSBuildProjectTools.ProjectServer.Utilities
{
    /// <summary>
    ///     Helper functions for working with Microsoft.Extensions.Logging components.
    /// </summary>
    public static class LogHelper
    {
        /// <summary>
        ///     Create a dummy logger factory to be used when no logger is supplied.
        /// </summary>
        public static ILoggerFactory CreateDummyLoggerFactory() => LoggerFactory.Create(logging => logging.ClearProviders());

        /// <summary>
        ///     Create a dummy logger used when no logger is supplied.
        /// </summary>
        /// <typeparam name="TComponent">
        ///     The type of component used as a category for the logger.
        /// </typeparam>
        public static ILogger CreateDummyLogger<TComponent>()
            where TComponent : class
        {
            return CreateDummyLoggerFactory().CreateLogger<TComponent>();
        }

        /// <summary>
        ///     Create a dummy logger used when no logger is supplied.
        /// </summary>
        /// <param name="componentType">
        ///     The type of component used as a category for the logger.
        /// </param>
        public static ILogger CreateDummyLogger(Type componentType)
        {
            if (componentType == null)
                throw new ArgumentNullException(nameof(componentType));


            return CreateDummyLoggerFactory().CreateLogger(componentType);
        }
    }
}
