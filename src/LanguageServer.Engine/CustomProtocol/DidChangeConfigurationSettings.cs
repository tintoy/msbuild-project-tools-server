using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.Embedded.MediatR;

namespace MSBuildProjectTools.LanguageServer.CustomProtocol
{
    /// <summary>
    ///     Custom handler for "workspace/didChangeConfiguration" with the configuration as a <see cref="JObject"/>.
    /// </summary>
    [Serial, Method("workspace/didChangeConfiguration")]
    public interface IDidChangeConfigurationSettingsHandler
        : IJsonRpcNotificationHandler<DidChangeConfigurationObjectParams>, INotificationHandler<DidChangeConfigurationObjectParams>, IJsonRpcHandler, IRegistration<object>, ICapability<DidChangeConfigurationCapability>
    {
    }

    /// <summary>
    ///     Notification parameters for "workspace/didChangeConfiguration".
    /// </summary>
    public class DidChangeConfigurationObjectParams : IRequest, INotification
    {
        /// <summary>
        ///     The current settings.
        /// </summary>
        [JsonProperty("settings")]
        public JToken Settings;
    }
}
