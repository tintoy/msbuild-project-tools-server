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
        public string Description { get; init; }

        /// <summary>
        ///     Help link for the element (if any).
        /// </summary>
        public string HelpLink { get; init; }
    }
}
