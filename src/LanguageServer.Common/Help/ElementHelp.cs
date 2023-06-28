using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace MSBuildProjectTools.LanguageServer.Help
{
    /// <summary>
    ///     Help information for an MSBuild element.
    /// </summary>
    public class ElementHelp
    {
        /// <summary>
        ///     The property description.
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        ///     Help link for the element (if any).
        /// </summary>
        [JsonProperty("help")]
        public string HelpLink { get; set; }

        /// <summary>
        ///     Load help property help from JSON.
        /// </summary>
        /// <param name="json">
        ///     A <see cref="JsonReader"/> representing the JSON ("PropertyName": { "description": "PropertyDescription" }).
        /// </param>
        /// <returns>
        ///     A sorted dictionary of help items, keyed by property name.
        /// </returns>
        public static SortedDictionary<string, ElementHelp> FromJson(JsonReader json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));

            return new JsonSerializer().Deserialize<SortedDictionary<string, ElementHelp>>(json);
        }
    }
}
