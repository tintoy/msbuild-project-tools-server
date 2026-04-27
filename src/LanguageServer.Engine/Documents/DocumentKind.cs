namespace MSBuildProjectTools.LanguageServer.Documents
{
    /// <summary>
    ///     Well-known document kinds.
    /// </summary>
    public enum DocumentKind
    {
        /// <summary>
        ///     An unknown document kind.
        /// </summary>
        Unknown = 0,

        /// <summary>
        ///     A project document.
        /// </summary>
        Project = 1,

        /// <summary>
        ///     A solution document.
        /// </summary>
        Solution = 2
    }
}
