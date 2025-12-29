using Microsoft.Build.Locator;
using Microsoft.Language.Xml;
using MSBuildProjectTools.LanguageServer.SemanticModel;
using MSBuildProjectTools.LanguageServer.Utilities;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.Documents
{
    /// <summary>
    ///     The base class for <see cref="Document"/>s, in a workspace, that are have an XML-based format.
    /// </summary>
    public abstract class XmlDocument
        : Document
    {
        /// <summary>
        ///     Create a new <see cref="XmlDocument"/>.
        /// </summary>
        /// <param name="workspace">
        ///     The document workspace.
        /// </param>
        /// <param name="documentUri">
        ///     The document URI.
        /// </param>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        protected XmlDocument(Workspace workspace, DocumentUri documentUri, ILogger logger)
            : base(workspace, documentUri, logger)
        {
        }

        /// <summary>
        ///     The parsed document XML.
        /// </summary>
        public XmlDocumentSyntax Xml { get; protected set; }

        /// <summary>
        ///     The textual position translator for the document XML .
        /// </summary>
        public TextPositions XmlPositions { get; protected set; }

        /// <summary>
        ///     The document XML node lookup facility.
        /// </summary>
        public XmlLocator XmlLocator { get; protected set; }

        /// <summary>
        ///     Is the project XML currently loaded?
        /// </summary>
        public bool HasXml => Xml != null && XmlPositions != null;

        /// <summary>
        ///     Load and parse the document.
        /// </summary>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A task representing the load operation.
        /// </returns>
        public override async ValueTask Load(CancellationToken cancellationToken = default)
        {
            ClearDiagnostics();

            Xml = null;
            XmlPositions = null;
            XmlLocator = null;

            string xml;
            using (StreamReader reader = DocumentFile.OpenText())
            {
                xml = await reader.ReadToEndAsync();
            }
            Xml = Parser.ParseText(xml);
            XmlPositions = new TextPositions(xml);
            XmlLocator = new XmlLocator(Xml, XmlPositions);

            IsDirty = false;
        }

        /// <summary>
        ///     Update the document in-memory state.
        /// </summary>
        /// <param name="xml">
        ///     The document XML.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A task representing the update operation.
        /// </returns>
        public override ValueTask Update(string xml, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(xml);

            ClearDiagnostics();

            Xml = Parser.ParseText(xml);
            XmlPositions = new TextPositions(xml);
            XmlLocator = new XmlLocator(Xml, XmlPositions);
            IsDirty = true;

            return ValueTask.CompletedTask;
        }

        /// <summary>
        ///     Unload the document.
        /// </summary>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        public override ValueTask Unload(CancellationToken cancellationToken = default)
        {
            Xml = null;
            XmlPositions = null;
            IsDirty = false;

            return ValueTask.CompletedTask;
        }
    }
}
