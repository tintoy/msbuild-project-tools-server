using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace MSBuildProjectTools.LanguageServer
{
    /// <summary>
    ///     Well-known document selectors.
    /// </summary>
    public static class DocumentSelectors
    {
        /// <summary>
        ///     A selector for all document types.
        /// </summary>
        public static DocumentSelector All => new DocumentSelector(DocumentFilters.All);

        /// <summary>
        ///     A selector for all MSBuild document types.
        /// </summary>
        public static DocumentSelector MSBuild => new DocumentSelector(DocumentFilters.MSBuild.All);

        /// <summary>
        ///     A selector for all VS Solution XML (SLNX) document types.
        /// </summary>
        public static DocumentSelector VsSolutionXml => new DocumentSelector(DocumentFilters.VsSolutionXml.All);
    }
}
