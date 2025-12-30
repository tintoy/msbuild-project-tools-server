using Serilog;
using System;

namespace MSBuildProjectTools.LanguageServer.CompletionProviders.Project
{
    using Documents;
    using Xml;

    /// <summary>
    ///     The base class for completion providers that target a <see cref="ProjectDocument"/>.
    /// </summary>
    public abstract class ProjectCompletionProvider
        : XmlDocumentCompletionProvider<ProjectDocument>
    {
        /// <summary>
        ///     Create a new <see cref="CompletionProvider{TDocument}"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        protected ProjectCompletionProvider(ILogger logger)
            : base(logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
        }
    }
}
