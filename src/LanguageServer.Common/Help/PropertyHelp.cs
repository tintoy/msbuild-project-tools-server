using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace MSBuildProjectTools.LanguageServer.Help
{
    /// <summary>
    ///     Help information for an MSBuild property.
    /// </summary>
    public class PropertyHelp
    {
        /// <summary>
        ///     The property description.
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        ///     The property's default value.
        /// </summary>
        [JsonProperty("defaultValue")]
        public string DefaultValue { get; set; }

        /// <summary>
        ///     The property's default values (if specified, the completion's snippet will present a drop-down list of values for the user to choose from as the property value.').
        /// </summary>
        [JsonProperty("defaultValues", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public List<string> DefaultValues { get; set; }

        /// <summary>
        ///     Load help property help from JSON.
        /// </summary>
        /// <param name="json">
        ///     A <see cref="JsonReader"/> representing the JSON ("PropertyName": { "description": "PropertyDescription" }).
        /// </param>
        /// <returns>
        ///     A sorted dictionary of help items, keyed by property name.
        /// </returns>
        public static SortedDictionary<string, PropertyHelp> FromJson(JsonReader json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));
            
            return new JsonSerializer().Deserialize<SortedDictionary<string, PropertyHelp>>(json);
        }
    }
}
