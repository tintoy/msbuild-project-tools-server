using System.Collections.Generic;

namespace MSBuildProjectTools.LanguageServer.Help
{
    /// <summary>
    ///     Help information for an MSBuild item.
    /// </summary>
    public class ItemHelp
    {
        /// <summary>
        ///     A description of the item.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        ///     A link to the item type's documentation (if available).
        /// </summary>
        public string HelpLink { get; init; }

        /// <summary>
        ///     Descriptions for the item's metadata.
        /// </summary>
        public SortedDictionary<string, string> Metadata { get; init; }
    }
}
