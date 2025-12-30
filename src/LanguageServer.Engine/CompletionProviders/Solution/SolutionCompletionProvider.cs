using Serilog;
using System;

namespace MSBuildProjectTools.LanguageServer.CompletionProviders.Solution
{
    using Documents;
    using Xml;

    /// <summary>
    ///     The base class for completion providers that target a <see cref="SolutionDocument"/>.
    /// </summary>
    public abstract class SolutionCompletionProvider
        : XmlDocumentCompletionProvider<SolutionDocument>
    {
        /// <summary>
        ///     Create a new <see cref="CompletionProvider{TDocument}"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        protected SolutionCompletionProvider(ILogger logger)
            : base(logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
        }
    }
}
