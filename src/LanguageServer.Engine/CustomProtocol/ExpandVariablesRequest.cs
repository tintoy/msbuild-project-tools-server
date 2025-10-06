using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MSBuildProjectTools.LanguageServer.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OmniSharp.Extensions.JsonRpc;

namespace MSBuildProjectTools.LanguageServer.CustomProtocol
{
    [Method("variables/expand")]
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ExpandVariablesRequest
    {
        public ExpandVariablesRequest()
        {
        }

        public ExpandVariablesRequest(Dictionary<string, string> variables)
        {
            if (variables == null)
                throw new ArgumentNullException(nameof(variables));

            Variables.AddRange(variables);
        }

        [JsonProperty("variables", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public Dictionary<string, string> Variables { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ExpandVariablesResponse
    {
        [JsonProperty("variables", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public Dictionary<string, string> Variables { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
