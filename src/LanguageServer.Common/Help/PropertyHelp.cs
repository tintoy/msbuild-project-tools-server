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
        public string Description { get; init; }

        /// <summary>
        ///     A link to the property's documentation (if available).
        /// </summary>
        public string HelpLink { get; init; }

        /// <summary>
        ///     The property's default value.
        /// </summary>
        public string DefaultValue { get; init; }

        /// <summary>
        ///     The property's default values (if specified, the completion's snippet will present a drop-down list of values for the user to choose from as the property value.').
        /// </summary>
        public List<string> DefaultValues { get; init; }
    }
}
