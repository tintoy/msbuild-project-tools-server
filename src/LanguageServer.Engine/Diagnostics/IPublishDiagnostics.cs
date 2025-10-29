using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;

namespace MSBuildProjectTools.LanguageServer.Diagnostics
{
    /// <summary>
    ///     Represents a facility for publishing diagnostics (i.e. warnings, errors, etc).
    /// </summary>
    public interface IPublishDiagnostics
    {
        /// <summary>
        ///     Publish the specified diagnostics.
        /// </summary>
        /// <param name="documentUri">
        ///     The URI of the document that the diagnostics apply to.
        /// </param>
        /// <param name="diagnostics">
        ///     A sequence of <see cref="Diagnostic"/>s to publish.
        /// </param>
        void Publish(DocumentUri documentUri, IEnumerable<Diagnostic> diagnostics);
    }
}
