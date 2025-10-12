using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MSBuildProjectTools.LanguageServer.Diagnostics
{
    /// <summary>
    ///     An implementation of <see cref="IPublishDiagnostics"/> that publishes diagnostics via LSP.
    /// </summary>
    class LspDiagnosticsPublisher
        : IPublishDiagnostics
    {
        /// <summary>
        ///     The LSP <see cref="OmniSharp.Extensions.LanguageServer.Server.ILanguageServer"/>.
        /// </summary>
        readonly OmniSharp.Extensions.LanguageServer.Server.ILanguageServer _languageServer;

        /// <summary>
        ///     Create a new <see cref="LspDiagnosticsPublisher"/>.
        /// </summary>
        /// <param name="languageServer">
        ///     The LSP <see cref="OmniSharp.Extensions.LanguageServer.Server.ILanguageServer"/>.
        /// </param>
        public LspDiagnosticsPublisher(OmniSharp.Extensions.LanguageServer.Server.ILanguageServer languageServer)
        {
            if (languageServer == null)
                throw new ArgumentNullException(nameof(languageServer));

            _languageServer = languageServer;
        }

        /// <summary>
        ///     Publish the specified diagnostics.
        /// </summary>
        /// <param name="documentUri">
        ///     The URI of the document that the diagnostics apply to.
        /// </param>
        /// <param name="diagnostics">
        ///     A sequence of <see cref="Diagnostic"/>s to publish.
        /// </param>
        public void Publish(Uri documentUri, IEnumerable<Diagnostic> diagnostics)
        {
            if (documentUri == null)
                throw new ArgumentNullException(nameof(documentUri));

            diagnostics ??= Enumerable.Empty<Diagnostic>();

            _languageServer.Document.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = documentUri,
                Diagnostics = diagnostics.ToArray()
            });
        }
    }
}
