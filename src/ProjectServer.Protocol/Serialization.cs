using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSBuildProjectTools.ProjectServer.Protocol
{
    public static class Serialization
    {
        public static JsonSerializerOptions DefaultOptions => new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            },
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }
}
