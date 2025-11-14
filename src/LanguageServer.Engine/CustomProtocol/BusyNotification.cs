using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OmniSharp.Extensions.JsonRpc;

namespace MSBuildProjectTools.LanguageServer.CustomProtocol
{
    /// <summary>
    ///     Parameters for notifying the LSP language client that the language service is (or is not) busy.
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    [Serial, Method("msbuild/busy", Direction.ServerToClient)]
    public class BusyNotificationParams : IRequest, INotification
    {
        /// <summary>
        ///     Create new <see cref="BusyNotificationParams"/>.
        /// </summary>
        public BusyNotificationParams()
        {
        }

        /// <summary>
        ///     Is the language service busy?
        /// </summary>
        public bool IsBusy { get; set; }

        /// <summary>
        ///     If the language service is busy, a message describing why.
        /// </summary>
        public string Message { get; set; }
    }
}
