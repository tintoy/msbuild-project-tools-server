namespace MSBuildProjectTools.LanguageServer.Documents
{
    /// <summary>
    ///     A kind of <see cref="ProjectDocument"/>.
    /// </summary>
    public enum ProjectKind
    {
        /// <summary>
        ///     An unknown project kind.
        /// </summary>
        Unknown = 0,

        /// <summary>
        ///     A project (*.*proj).
        /// </summary>
        Project = 1,

        /// <summary>
        ///     A properties file (*.props).
        /// </summary>
        Properties = 2,

        /// <summary>
        ///     A targets file (*.targets).
        /// </summary>
        Targets = 3,

        /// <summary>
        ///     Some other file type (*.*).
        /// </summary>
        Other = 4
    }
}
