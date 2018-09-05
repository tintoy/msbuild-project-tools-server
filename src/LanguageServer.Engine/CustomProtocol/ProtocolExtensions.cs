using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace MSBuildProjectTools.LanguageServer.CustomProtocol
{
    /// <summary>
    ///     Custom Language Server Protocol extensions.
    /// </summary>
    public static class ProtocolExtensions
    {
        /// <summary>
        ///     Notify the language client that the language service is busy.
        /// </summary>
        /// <param name="router">
        ///     The language server used to route messages to the client.
        /// </param>
        /// <param name="message">
        ///     A message describing why the language service is busy.
        /// </param>
        public static void NotifyBusy(this ILanguageServer router, string message)
        {
            if (router == null)
                throw new ArgumentNullException(nameof(router));

            if (String.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'message'.", nameof(message));

            router.SendNotification("msbuild/busy", new BusyNotificationParams
            {
                IsBusy = true,
                Message = message
            });
        }

        /// <summary>
        ///     Notify the language client that the language service is no longer busy.
        /// </summary>
        /// <param name="router">
        ///     The language server used to route messages to the client.
        /// </param>
        /// <param name="message">
        ///     An optional message indicating the operation that was completed.
        /// </param>
        public static void ClearBusy(this ILanguageServer router, string message = null)
        {
            if (router == null)
                throw new ArgumentNullException(nameof(router));

            router.SendNotification("msbuild/busy", new BusyNotificationParams
            {
                IsBusy = false,
                Message = message
            });
        }

        /// <summary>
        ///     Update the configuration from the specified configuration-change notification.
        /// </summary>
        /// <param name="configuration">
        ///     The <see cref="Configuration"/> to update.
        /// </param>
        /// <param name="request">
        ///     The configuration-change notification.
        /// </param>
        public static void UpdateFrom(this Configuration configuration, DidChangeConfigurationObjectParams request)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            
            JObject json = request.Settings?.SelectToken(Configuration.SectionName) as JObject;
            if (json == null)
                return;

            configuration.UpdateFrom(json);
        }

        /// <summary>
        ///     Update the configuration from the specified initialisation request.
        /// </summary>
        /// <param name="configuration">
        ///     The <see cref="Configuration"/> to update.
        /// </param>
        /// <param name="request">
        ///     The initialisation request.
        /// </param>
        public static void UpdateFrom(this Configuration configuration, InitializeParams request)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            JToken initializationParameters = request.InitializationOptions as JToken;
            if (initializationParameters == null)
                return;
            
            JObject json = initializationParameters.SelectToken(Configuration.SectionName) as JObject;
            if (json == null)
                return;

            configuration.UpdateFrom(json);
        }

        /// <summary>
        ///     Update the configuration from the specified JSON.
        /// </summary>
        /// <param name="configuration">
        ///     The <see cref="Configuration"/> to update.
        /// </param>
        /// <param name="flattenedJson">
        ///     A <see cref="JObject"/> representing the flattened settings JSON from VS Code.
        /// </param>
        public static void UpdateFrom(this Configuration configuration, JObject flattenedJson)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            
            if (flattenedJson == null)
                throw new ArgumentNullException(nameof(flattenedJson));
            
            // Temporary workaround - JsonSerializer.Populate reuses existing HashSet.
            configuration.Language.CompletionsFromProject.Clear();
            configuration.EnableExperimentalFeatures.Clear();

            using (JsonReader reader = flattenedJson.ToNested().CreateReader())
            {
                new JsonSerializer().Populate(reader, configuration);
            }
        }

        /// <summary>
        ///     Convert a flattened settings <see cref="JObject"/> from VS Code to the native (nested) settings format.
        /// </summary>
        /// <param name="flattenedJson">
        ///     The flattened <see cref="JObject"/> to convert.
        /// </param>
        /// <returns>
        ///     The nested settings <see cref="JObject"/>.
        /// </returns>
        static JObject ToNested(this JObject flattenedJson)
        {
            if (flattenedJson == null)
                throw new ArgumentNullException(nameof(flattenedJson));
            
            JObject root = new JObject();

            foreach (JProperty property in flattenedJson.Properties())
            {
                string[] pathSegments = property.Name.Split('.');

                JObject target = root;
                foreach (string intermediatePropertyName in pathSegments.Take(pathSegments.Length - 1))
                {
                    // Get or create intermediate object.
                    JObject child = target.Value<JToken>(intermediatePropertyName) as JObject;
                    if (child == null)
                    {
                        child = new JObject();
                        target[intermediatePropertyName] = child;
                    }

                    target = child;
                }

                string targetPropertyName = pathSegments.Last();
                target[targetPropertyName] = property.Value;
            }

            return root;
        }
    }
}
