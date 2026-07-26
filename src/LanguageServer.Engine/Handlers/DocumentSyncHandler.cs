using MediatR;
using NuGet.Configuration;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.Handlers
{
    using CustomProtocol;
    using Documents;
    using SemanticModel;

    /// <summary>
    ///     The handler for language server document synchronization.
    /// </summary>
    public sealed class DocumentSyncHandler
        : Handler, ITextDocumentSyncHandler, IStaticDocumentSyncHandler
    {
        /// <summary>
        ///     Create a new <see cref="DocumentSyncHandler"/>.
        /// </summary>
        /// <param name="server">
        ///     The language server.
        /// </param>
        /// <param name="workspace">
        ///     The document workspace.
        /// </param>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public DocumentSyncHandler(ILanguageServer server, Workspace workspace, ILogger logger)
            : base(server, logger)
        {
            ArgumentNullException.ThrowIfNull(workspace);

            Workspace = workspace;
        }

        /// <summary>
        ///     Options that control synchronization.
        /// </summary>
        public TextDocumentSyncOptions Options { get; } = new TextDocumentSyncOptions
        {
            WillSaveWaitUntil = false,
            WillSave = true,
            Change = TextDocumentSyncKind.Full,
            Save = new SaveOptions
            {
                IncludeText = true
            },
            OpenClose = true
        };

        /// <summary>
        ///     The document selector that describes documents to synchronize.
        /// </summary>
        DocumentSelector DocumentSelector { get; } = DocumentSelectors.All;

        /// <summary>
        ///     The document workspace.
        /// </summary>
        Workspace Workspace { get; }

        /// <summary>
        ///     Get registration options for handling document events.
        /// </summary>
        TextDocumentRegistrationOptions DocumentRegistrationOptions
        {
            get => DocumentSaveRegistrationOptions;
        }

        /// <summary>
        ///     Get registration options for handling document-change events.
        /// </summary>
        TextDocumentChangeRegistrationOptions DocumentChangeRegistrationOptions
        {
            get => new TextDocumentChangeRegistrationOptions
            {
                DocumentSelector = DocumentSelector,
                SyncKind = Options.Change
            };
        }

        /// <summary>
        ///     Get registration options for handling document save events.
        /// </summary>
        TextDocumentSaveRegistrationOptions DocumentSaveRegistrationOptions
        {
            get => new TextDocumentSaveRegistrationOptions
            {
                DocumentSelector = DocumentSelector,
                IncludeText = Options.Save.Value.IncludeText
            };
        }

        /// <summary>
        ///    The kind of document synchronization supported.
        /// </summary>
        public TextDocumentSyncKind Change => TextDocumentSyncKind.Full;

        /// <summary>
        ///     Called when a text document is opened.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task OnDidOpenTextDocument(DidOpenTextDocumentParams parameters, CancellationToken cancellationToken)
        {
            Server.NotifyBusy("Loading...");

            Document document = await Workspace.GetDocument(parameters.TextDocument.Uri, cancellationToken: cancellationToken);
            Workspace.PublishDiagnostics(document);

            switch (document)
            {
                case ProjectDocument projectDocument:
                {
                    // Only enable expression-related language service facilities if they're using our custom "MSBuild" language type (rather than "XML").
                    projectDocument.EnableExpressions = parameters.TextDocument.LanguageId == "msbuild";

                    if (!projectDocument.HasXml)
                    {
                        Log.Warning("Failed to load project file {ProjectFilePath}.", projectDocument.ProjectFile.FullName);
                        
                        Server.ClearBusy($"Failed to load project file {projectDocument.ProjectFile.FullName}.");

                        return;
                    }

                    Server.ClearBusy("Project loaded.");

                    switch (projectDocument)
                    {
                        case MasterProjectDocument masterProjectDocument:
                        {
                            if (masterProjectDocument.HasMSBuildProject)
                                Log.Information("Successfully loaded project {ProjectFilePath}.", projectDocument.ProjectFile.FullName);

                            break;
                        }
                        case SubProjectDocument subProjectDocument:
                        {
                            if (subProjectDocument.HasMSBuildProject)
                            {
                                Log.Information("Successfully loaded project {ProjectFilePath} as a sub-project of {MasterProjectFileName}.",
                                    projectDocument.ProjectFile.FullName,
                                    subProjectDocument.MasterProjectDocument.ProjectFile.Name
                                );
                            }

                            break;
                        }
                    }

                    if (Log.IsEnabled(LogEventLevel.Verbose))
                    {
                        Log.Verbose("===========================");

                        foreach (PackageSource packageSource in projectDocument.ConfiguredPackageSources)
                        {
                            Log.Verbose(" - Project uses package source {PackageSourceName} ({PackageSourceUrl})",
                                packageSource.Name,
                                packageSource.Source
                            );
                        }

                        if (projectDocument.HasMSBuildProject)
                        {
                            Log.Verbose("===========================");

                            Log.Verbose("Scanning task definitions for project {ProjectName}...", projectDocument.ProjectFile.Name);
                            List<MSBuildTaskAssemblyMetadata> taskAssemblies = projectDocument.GetMSBuildProjectTaskAssemblies();
                            Log.Verbose("Scan complete for task definitions of project {ProjectName} ({AssemblyCount} assemblies scanned).", projectDocument.ProjectFile.Name, taskAssemblies.Count);
                        }

                        LogLoadedDocumentState(projectDocument);
                    }

                    break;
                }
                case SolutionDocument solutionDocument:
                {
                    if (!solutionDocument.HasXml)
                    {
                        Log.Warning("Failed to load solution file {ProjectFilePath}.", solutionDocument.SolutionFile.FullName);
                        
                        Server.ClearBusy($"Failed to load solution file {solutionDocument.SolutionFile.FullName}.");

                        return;
                    }
                    
                    Server.ClearBusy("Solution loaded.");

                    LogLoadedDocumentState(solutionDocument);

                    break;
                }
            }
        }

        /// <summary>
        ///     Called when a text document is changed.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task OnDidChangeTextDocument(DidChangeTextDocumentParams parameters, CancellationToken cancellationToken)
        {
            Log.Verbose("Reloading project {ProjectFile}...",
                DocumentUri.GetFileSystemPath(parameters.TextDocument.Uri)
            );

            TextDocumentContentChangeEvent mostRecentChange = parameters.ContentChanges.LastOrDefault();
            if (mostRecentChange == null)
                return;

            string updatedDocumentText = mostRecentChange.Text;
            Document document = await Workspace.TryUpdateDocument(parameters.TextDocument.Uri, updatedDocumentText, cancellationToken);
            Workspace.PublishDiagnostics(document);

            LogLoadedDocumentState(document);
        }

        /// <summary>
        ///     Called when a text document is saved.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task OnDidSaveTextDocument(DidSaveTextDocumentParams parameters, CancellationToken cancellationToken)
        {
            Log.Information("Reloading project {ProjectFile}...",
                DocumentUri.GetFileSystemPath(parameters.TextDocument.Uri)
            );

            Document document = await Workspace.GetDocument(parameters.TextDocument.Uri, reload: true, cancellationToken: cancellationToken);
            Workspace.PublishDiagnostics(document);

            switch (document)
            {
                case ProjectDocument projectDocument:
                {
                    if (!projectDocument.HasXml)
                    {
                        Log.Warning("Failed to reload project file {ProjectFilePath} (XML is invalid).", projectDocument.ProjectFile.FullName);

                        return;
                    }

                    if (!projectDocument.HasMSBuildProject)
                    {
                        Log.Warning("Reloaded project file {ProjectFilePath} (XML is valid, but MSBuild project is not).", projectDocument.ProjectFile.FullName);

                        return;
                    }

                    Log.Information("Successfully reloaded project {ProjectFilePath}.", projectDocument.ProjectFile.FullName);

                    break;
                }
                case SolutionDocument solutionDocument:
                {
                    if (!solutionDocument.HasXml)
                    {
                        Log.Warning("Failed to reload project file {ProjectFilePath} (XML is invalid).", solutionDocument.SolutionFile.FullName);

                        return;
                    }

                    if (!solutionDocument.HasSolution)
                    {
                        Log.Warning("Reloaded project file {ProjectFilePath} (XML is valid, but VS Solution model is not).", solutionDocument.SolutionFile.FullName);

                        return;
                    }

                    Log.Information("Successfully reloaded project {ProjectFilePath}.", solutionDocument.SolutionFile.FullName);

                    break;
                }
            }
        }

        /// <summary>
        ///     Called when a text document is closed.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task OnDidCloseTextDocument(DidCloseTextDocumentParams parameters, CancellationToken cancellationToken)
        {
            await Workspace.RemoveProjectDocument(parameters.TextDocument.Uri, cancellationToken);

            Log.Information("Unloaded project {ProjectFile}.",
                DocumentUri.GetFileSystemPath(parameters.TextDocument.Uri)
            );
        }

        /// <summary>
        ///     Called when a text document is about to be saved.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        Task OnWillSaveTextDocument(WillSaveTextDocumentParams parameters, CancellationToken cancellationToken)
        {
            ProjectDocument projectDocument = Workspace.GetLoadedProjectDocument(parameters.TextDocument.Uri);
            Workspace.PublishDiagnostics(projectDocument);

            Log.Information("Project {ProjectFile} will be saved, because it was triggered by {Reason}.",
                DocumentUri.GetFileSystemPath(parameters.TextDocument.Uri),
                parameters.Reason
            );

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Called when the client requests for a text document to be saved.
        /// </summary>
        /// <param name="parameters">
        ///     The request parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation whose result is the list of text edits or <c>null</c> if no text edits are provided.
        /// </returns>
        Task<TextEditContainer> OnWillSaveWaitUntilTextDocument(WillSaveWaitUntilTextDocumentParams parameters, CancellationToken cancellationToken)
        {
            ProjectDocument projectDocument = Workspace.GetLoadedProjectDocument(parameters.TextDocument.Uri);
            Workspace.PublishDiagnostics(projectDocument);

            Log.Information("Project {ProjectFile} will be saved, because it was triggered by {Reason}.",
                DocumentUri.GetFileSystemPath(parameters.TextDocument.Uri),
                parameters.Reason
            );
            //TODO: retrieve text edits async.
            //var textEdits = new List<TextEdit>();
            //return new TextEditContainer(textEdits);
            return Task.FromResult<TextEditContainer>(null);
        }

        void LogLoadedDocumentState(Document document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (!Log.IsEnabled(LogEventLevel.Verbose))
                return;

            Log.Verbose("===========================");

            switch (document)
            {
                case ProjectDocument projectDocument:
                {
                    Log.Verbose("===========================");

                    if (projectDocument.HasMSBuildProject)
                    {
                        if (!projectDocument.IsMSBuildProjectCached)
                        {
                            MSBuildObject[] msbuildObjects = projectDocument.MSBuildObjects.ToArray();
                            Log.Verbose("MSBuild project loaded ({MSBuildObjectCount} MSBuild objects).", msbuildObjects.Length);

                            foreach (MSBuildObject msbuildObject in msbuildObjects)
                            {
                                Log.Verbose("{Type:l}: {Kind} {Name} spanning {XmlRange}",
                                    msbuildObject.GetType().Name,
                                    msbuildObject.Kind,
                                    msbuildObject.Name,
                                    msbuildObject.XmlRange
                                );
                            }
                        }
                        else
                            Log.Verbose("MSBuild project not loaded; will used cached project state (as long as positional lookups are not required).");
                    }
                    else
                        Log.Verbose("MSBuild project not loaded.");

                    break;
                }
                case SolutionDocument solutionDocument:
                {
                    if (solutionDocument.HasSolution)
                    {
                        if (!solutionDocument.IsSolutionCached)
                        {
                            VsSolutionObject[] solutionObjects = solutionDocument.SolutionObjects.ToArray();
                            Log.Verbose("Solution loaded ({SolutionObjectCount} solution objects).", solutionObjects.Length);

                            foreach (VsSolutionObject solutionObject in solutionObjects)
                            {
                                Log.Verbose("{Type:l}: {Kind} {Name} spanning {XmlRange}",
                                    solutionObject.GetType().Name,
                                    solutionObject.Kind,
                                    solutionObject.Name,
                                    solutionObject.XmlRange
                                );
                            }
                        }
                        else
                            Log.Verbose("Solution not loaded; will used cached project state (as long as positional lookups are not required).");
                    }
                    else
                        Log.Verbose("Solution not loaded.");

                    break;
                }
            }
        }

        /// <summary>
        ///     Get attributes for the specified text document.
        /// </summary>
        /// <param name="documentUri">
        ///     The document URI.
        /// </param>
        /// <returns>
        ///     The document attributes.
        /// </returns>
        static TextDocumentAttributes GetTextDocumentAttributes(DocumentUri documentUri)
        {
            string documentFilePath = DocumentUri.GetFileSystemPath(documentUri);
            if (documentFilePath == null)
                return new TextDocumentAttributes(documentUri, LanguageIdentifiers.PlainText);

            string extension = Path.GetExtension(documentFilePath).ToLowerInvariant();
            switch (extension)
            {
                case ".slnx":
                {
                    return new TextDocumentAttributes(documentUri, LanguageIdentifiers.VsSolutionXml);
                }
                case ".props":
                case ".targets":
                {
                    break;
                }
                default:
                {
                    if (extension.EndsWith("proj"))
                        break;

                    return new TextDocumentAttributes(documentUri, LanguageIdentifiers.PlainText);
                }
            }

            return new TextDocumentAttributes(documentUri, LanguageIdentifiers.MSBuild);
        }

        /// <summary>
        ///     Handle a document being opened.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task<Unit> IRequestHandler<DidOpenTextDocumentParams, Unit>.Handle(DidOpenTextDocumentParams parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            using (BeginOperation("OnDidOpenTextDocument"))
            {
                try
                {
                    await OnDidOpenTextDocument(parameters, cancellationToken);
                }
                catch (Exception unexpectedError)
                {
                    Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnDidOpenTextDocument");
                }
            }

            return Unit.Value;
        }

        /// <summary>
        ///     Handle a document being closed.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task<Unit> IRequestHandler<DidCloseTextDocumentParams, Unit>.Handle(DidCloseTextDocumentParams parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            using (BeginOperation("OnDidCloseTextDocument"))
            {
                try
                {
                    await OnDidCloseTextDocument(parameters, cancellationToken);
                }
                catch (Exception unexpectedError)
                {
                    Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnDidCloseTextDocument");
                }
            }

            return Unit.Value;
        }

        /// <summary>
        ///     Handle a change in document text.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task<Unit> IRequestHandler<DidChangeTextDocumentParams, Unit>.Handle(DidChangeTextDocumentParams parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            using (BeginOperation("OnDidChangeTextDocument"))
            {
                try
                {
                    await OnDidChangeTextDocument(parameters, cancellationToken);
                }
                catch (Exception unexpectedError)
                {
                    Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnDidChangeTextDocument");
                }
            }

            return Unit.Value;
        }

        /// <summary>
        ///     Handle a document being saved.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task<Unit> IRequestHandler<DidSaveTextDocumentParams, Unit>.Handle(DidSaveTextDocumentParams parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            using (BeginOperation("OnDidSaveTextDocument"))
            {
                try
                {
                    await OnDidSaveTextDocument(parameters, cancellationToken);
                }
                catch (Exception unexpectedError)
                {
                    Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnDidSaveTextDocument");
                }
            }

            return Unit.Value;
        }

        /// <summary>
        /// Handle a notification for a document to be saved.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task<Unit> IRequestHandler<WillSaveTextDocumentParams, Unit>.Handle(WillSaveTextDocumentParams parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            using (BeginOperation("OnWillSaveTextDocument"))
            {
                try
                {
                    await OnWillSaveTextDocument(parameters, cancellationToken);
                }
                catch (Exception unexpectedError)
                {
                    Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnWillSaveTextDocument");
                }
            }

            return Unit.Value;
        }

        /// <summary>
        ///     Handle a request for a document that is about to be saved.
        /// </summary>
        /// <param name="parameters">
        ///     The request parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation whose result is the list of text edits or <c>null</c> if no text edits are provided.
        /// </returns>
        async Task<TextEditContainer> IRequestHandler<WillSaveWaitUntilTextDocumentParams, TextEditContainer>.Handle(WillSaveWaitUntilTextDocumentParams parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            using (BeginOperation("OnWillSaveWaitUntilTextDocument"))
            {
                try
                {
                    return await OnWillSaveWaitUntilTextDocument(parameters, cancellationToken);
                }
                catch (Exception unexpectedError)
                {
                    Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnWillSaveWaitUntilTextDocument");

                    return null;
                }
            }
        }

        /// <summary>
        ///     Get registration options for handling document events.
        /// </summary>
        /// <returns>
        ///     The registration options.
        /// </returns>
        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions() => DocumentRegistrationOptions;

        /// <summary>
        ///     Get registration options for handling document-change events.
        /// </summary>
        /// <returns>
        ///     The registration options.
        /// </returns>
        TextDocumentChangeRegistrationOptions IRegistration<TextDocumentChangeRegistrationOptions>.GetRegistrationOptions() => DocumentChangeRegistrationOptions;

        /// <summary>
        ///     Get registration options for handling document save events.
        /// </summary>
        /// <returns>
        ///     The registration options.
        /// </returns>
        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions>.GetRegistrationOptions() => DocumentSaveRegistrationOptions;

        /// <summary>
        ///     Get registration options that control synchronization.
        /// </summary>
        /// <returns>
        ///     The registration options.
        /// </returns>
        TextDocumentSyncOptions IRegistration<TextDocumentSyncOptions>.GetRegistrationOptions() => Options;

        /// <summary>
        ///     Called to inform the handler of the language server's document-synchronization capabilities.
        /// </summary>
        /// <param name="capabilities">
        ///     A <see cref="SynchronizationCapability"/> data structure representing the capabilities.
        /// </param>
        void ICapability<SynchronizationCapability>.SetCapability(SynchronizationCapability capabilities)
        {
        }

        /// <summary>
        ///     Get attributes for the specified text document.
        /// </summary>
        /// <param name="documentUri">
        ///     The document URI.
        /// </param>
        /// <returns>
        ///     The document attributes.
        /// </returns>
        TextDocumentAttributes ITextDocumentIdentifier.GetTextDocumentAttributes(DocumentUri documentUri)
        {
            ArgumentNullException.ThrowIfNull(documentUri);

            return GetTextDocumentAttributes(documentUri);
        }
    }
}
