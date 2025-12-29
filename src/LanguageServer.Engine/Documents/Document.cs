using Microsoft.Language.Xml;
using Nito.AsyncEx;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using LspModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace MSBuildProjectTools.LanguageServer.Documents
{
    using Utilities;

    /// <summary>
    ///     The base class for documents in a workspace.
    /// </summary>
    public abstract class Document
        : IDisposable
    {
        /// <summary>
        ///     Diagnostics (if any) for the document.
        /// </summary>
        readonly List<LspModels.Diagnostic> _diagnostics = new List<LspModels.Diagnostic>();

        /// <summary>
        ///     Create a new <see cref="Document"/>.
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
        protected Document(Workspace workspace, DocumentUri documentUri, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ArgumentNullException.ThrowIfNull(documentUri);

            Workspace = workspace;
            DocumentUri = documentUri;
            DocumentFile = new FileInfo(DocumentUri.GetFileSystemPath(documentUri));

            Log = logger.ForContext(GetType()).ForContext("Document", DocumentFile.FullName);
        }

        /// <summary>
        ///     Finalizer for <see cref="Document"/>.
        /// </summary>
        ~Document()
        {
            Dispose(false);
        }

        /// <summary>
        ///     Dispose of resources being used by the <see cref="Document"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Dispose of resources being used by the <see cref="Document"/>.
        /// </summary>
        /// <param name="disposing">
        ///     Explicit disposal?
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        ///     The document workspace.
        /// </summary>
        public Workspace Workspace { get; }

        /// <summary>
        ///     The document document URI.
        /// </summary>
        public DocumentUri DocumentUri { get; }

        /// <summary>
        ///     The document file.
        /// </summary>
        public FileInfo DocumentFile { get; }

        /// <summary>
        ///     A lock used to control access to document state.
        /// </summary>
        public AsyncReaderWriterLock Lock { get; } = new AsyncReaderWriterLock();

        /// <summary>
        ///     Are there currently any diagnostics to be published for the document?
        /// </summary>
        public bool HasDiagnostics => _diagnostics.Count > 0;

        /// <summary>
        ///     Diagnostics (if any) for the document.
        /// </summary>
        public IReadOnlyList<LspModels.Diagnostic> Diagnostics => _diagnostics;

        /// <summary>
        ///     Does the document have in-memory changes?
        /// </summary>
        public bool IsDirty { get; protected set; }

        /// <summary>
        ///     The document's logger.
        /// </summary>
        protected ILogger Log { get; set; }

        /// <summary>
        ///     Load and parse the document.
        /// </summary>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A task representing the load operation.
        /// </returns>
        public abstract ValueTask Load(CancellationToken cancellationToken = default);

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
        public abstract ValueTask Update(string xml, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Unload the document.
        /// </summary>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        public abstract ValueTask Unload(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Remove all diagnostics for the document file.
        /// </summary>
        protected void ClearDiagnostics()
        {
            _diagnostics.Clear();
        }

        /// <summary>
        ///     Add a diagnostic to be published for the document file.
        /// </summary>
        /// <param name="severity">
        ///     The diagnostic severity.
        /// </param>
        /// <param name="message">
        ///     The diagnostic message.
        /// </param>
        /// <param name="range">
        ///     The range of text within the document XML that the diagnostic relates to.
        /// </param>
        /// <param name="diagnosticCode">
        ///     A code to identify the diagnostic type.
        /// </param>
        protected void AddDiagnostic(LspModels.DiagnosticSeverity severity, string message, Range range, string diagnosticCode)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'message'.", nameof(message));

            _diagnostics.Add(new LspModels.Diagnostic
            {
                Severity = severity,
                Code = new LspModels.DiagnosticCode(diagnosticCode),
                Message = message,
                Range = range.ToLsp(),
                Source = DocumentFile.FullName
            });
        }

        /// <summary>
        ///     Add an error diagnostic to be published for the document file.
        /// </summary>
        /// <param name="message">
        ///     The diagnostic message.
        /// </param>
        /// <param name="range">
        ///     The range of text within the document XML that the diagnostic relates to.
        /// </param>
        /// <param name="diagnosticCode">
        ///     A code to identify the diagnostic type.
        /// </param>
        protected void AddErrorDiagnostic(string message, Range range, string diagnosticCode) => AddDiagnostic(LspModels.DiagnosticSeverity.Error, message, range, diagnosticCode);

        /// <summary>
        ///     Add a warning diagnostic to be published for the document file.
        /// </summary>
        /// <param name="message">
        ///     The diagnostic message.
        /// </param>
        /// <param name="range">
        ///     The range of text within the document XML that the diagnostic relates to.
        /// </param>
        /// <param name="diagnosticCode">
        ///     A code to identify the diagnostic type.
        /// </param>
        protected void AddWarningDiagnostic(string message, Range range, string diagnosticCode) => AddDiagnostic(LspModels.DiagnosticSeverity.Warning, message, range, diagnosticCode);

        /// <summary>
        ///     Add an informational diagnostic to be published for the document file.
        /// </summary>
        /// <param name="message">
        ///     The diagnostic message.
        /// </param>
        /// <param name="range">
        ///     The range of text within the document XML that the diagnostic relates to.
        /// </param>
        /// <param name="diagnosticCode">
        ///     A code to identify the diagnostic type.
        /// </param>
        protected void AddInformationDiagnostic(string message, Range range, string diagnosticCode) => AddDiagnostic(LspModels.DiagnosticSeverity.Information, message, range, diagnosticCode);

        /// <summary>
        ///     Add a hint diagnostic to be published for the document file.
        /// </summary>
        /// <param name="message">
        ///     The diagnostic message.
        /// </param>
        /// <param name="range">
        ///     The range of text within the document XML that the diagnostic relates to.
        /// </param>
        /// <param name="diagnosticCode">
        ///     A code to identify the diagnostic type.
        /// </param>
        protected void AddHintDiagnostic(string message, Range range, string diagnosticCode) => AddDiagnostic(LspModels.DiagnosticSeverity.Hint, message, range, diagnosticCode);

        /// <summary>
        ///     Create a <see cref="Serilog.Context.LogContext"/> representing an operation.
        /// </summary>
        /// <param name="operationDescription">
        ///     The operation description.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the log context.
        /// </returns>
        protected static IDisposable OperationContext(string operationDescription)
        {
            if (string.IsNullOrWhiteSpace(operationDescription))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'operationDescription'.", nameof(operationDescription));

            return Serilog.Context.LogContext.PushProperty("Operation", operationDescription);
        }
    }
}
