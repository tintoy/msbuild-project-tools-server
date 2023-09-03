using NuGet.Configuration;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.Handlers
{
    using CustomProtocol;
    using Documents;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     The handler for language server document synchronization.
    /// </summary>
    public sealed class DocumentSyncHandler
        : Handler, ITextDocumentSyncHandler
    {
        private readonly Workspace _workspace;

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
            if (workspace == null)
                throw new ArgumentNullException(nameof(workspace));

            _workspace = workspace;
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
        DocumentSelector DocumentSelector { get; } = new DocumentSelector(
            new DocumentFilter
            {
                Pattern = "**/*.*",
                Language = "msbuild",
                Scheme = "file"
            },
            new DocumentFilter
            {
                Pattern = "**/*.*proj",
                Language = "xml",
                Scheme = "file"
            },
            new DocumentFilter
            {
                Pattern = "**/*.props",
                Language = "xml",
                Scheme = "file"
            },
            new DocumentFilter
            {
                Pattern = "**/*.targets",
                Language = "xml",
                Scheme = "file"
            }
        );

        /// <summary>
        ///     Get registration options for handling document events.
        /// </summary>
        TextDocumentRegistrationOptions DocumentRegistrationOptions
        {
            get => new TextDocumentRegistrationOptions
            {
                DocumentSelector = DocumentSelector
            };
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
                IncludeText = Options.Save.IncludeText
            };
        }

        /// <summary>
        ///     Called when a text document is opened.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task OnDidOpenTextDocument(DidOpenTextDocumentParams parameters)
        {
            Server.NotifyBusy("Loading project...");

            ProjectDocument projectDocument = await _workspace.GetProjectDocument(parameters.TextDocument.Uri);
            _workspace.PublishDiagnostics(projectDocument);

            // Only enable expression-related language service facilities if they're using our custom "MSBuild" language type (rather than "XML").
            projectDocument.EnableExpressions = parameters.TextDocument.LanguageId == "msbuild";

            Server.ClearBusy("Project loaded.");

            if (!projectDocument.HasXml)
            {
                Log.Warning("Failed to load project file {ProjectFilePath}.", projectDocument.ProjectFile.FullName);

                return;
            }

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

                Log.Verbose("===========================");
                if (projectDocument.HasMSBuildProject)
                {
                    if (_workspace.Configuration.Language.CompletionsFromProject.Contains(CompletionSource.Task))
                    {
                        Log.Verbose("Scanning task definitions for project {ProjectName}...", projectDocument.ProjectFile.Name);
                        List<MSBuildTaskAssemblyMetadata> taskAssemblies = projectDocument.GetMSBuildProjectTaskAssemblies();
                        Log.Verbose("Scan complete for task definitions of project {ProjectName} ({AssemblyCount} assemblies scanned).", projectDocument.ProjectFile.Name, taskAssemblies.Count);

                        Log.Verbose("===========================");
                    }

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
            }
        }

        /// <summary>
        ///     Called when a text document is changed.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task OnDidChangeTextDocument(DidChangeTextDocumentParams parameters)
        {
            Log.Verbose("Reloading project {ProjectFile}...",
                VSCodeDocumentUri.GetFileSystemPath(parameters.TextDocument.Uri)
            );

            TextDocumentContentChangeEvent mostRecentChange = parameters.ContentChanges.LastOrDefault();
            if (mostRecentChange == null)
                return;

            string updatedDocumentText = mostRecentChange.Text;
            ProjectDocument projectDocument = await _workspace.TryUpdateProjectDocument(parameters.TextDocument.Uri, updatedDocumentText);
            _workspace.PublishDiagnostics(projectDocument);

            if (Log.IsEnabled(LogEventLevel.Verbose))
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
            }
        }

        /// <summary>
        ///     Called when a text document is saved.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task OnDidSaveTextDocument(DidSaveTextDocumentParams parameters)
        {
            Log.Information("Reloading project {ProjectFile}...",
                VSCodeDocumentUri.GetFileSystemPath(parameters.TextDocument.Uri)
            );

            ProjectDocument projectDocument = await _workspace.GetProjectDocument(parameters.TextDocument.Uri, reload: true);
            _workspace.PublishDiagnostics(projectDocument);

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
        }

        /// <summary>
        ///     Called when a text document is closed.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task OnDidCloseTextDocument(DidCloseTextDocumentParams parameters)
        {
            await _workspace.RemoveProjectDocument(parameters.TextDocument.Uri);

            Log.Information("Unloaded project {ProjectFile}.",
                VSCodeDocumentUri.GetFileSystemPath(parameters.TextDocument.Uri)
            );
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
        static TextDocumentAttributes GetTextDocumentAttributes(Uri documentUri)
        {
            string documentFilePath = VSCodeDocumentUri.GetFileSystemPath(documentUri);
            if (documentFilePath == null)
                return new TextDocumentAttributes(documentUri, "plaintext");

            string extension = Path.GetExtension(documentFilePath).ToLower();
            switch (extension)
            {
                case "props":
                case "targets":
                {
                    break;
                }
                default:
                {
                    if (extension.EndsWith("proj"))
                        break;

                    return new TextDocumentAttributes(documentUri, "plaintext");
                }
            }

            return new TextDocumentAttributes(documentUri, "msbuild");
        }

        /// <summary>
        ///     Handle a document being opened.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task INotificationHandler<DidOpenTextDocumentParams>.Handle(DidOpenTextDocumentParams parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            using (BeginOperation("OnDidOpenTextDocument"))
            {
                try
                {
                    await OnDidOpenTextDocument(parameters);
                }
                catch (Exception unexpectedError)
                {
                    Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnDidOpenTextDocument");
                }
            }
        }

        /// <summary>
        ///     Handle a document being closed.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task INotificationHandler<DidCloseTextDocumentParams>.Handle(DidCloseTextDocumentParams parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            using (BeginOperation("OnDidCloseTextDocument"))
            {
                try
                {
                    await OnDidCloseTextDocument(parameters);
                }
                catch (Exception unexpectedError)
                {
                    Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnDidCloseTextDocument");
                }
            }
        }

        /// <summary>
        ///     Handle a change in document text.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task INotificationHandler<DidChangeTextDocumentParams>.Handle(DidChangeTextDocumentParams parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            using (BeginOperation("OnDidChangeTextDocument"))
            {
                try
                {
                    await OnDidChangeTextDocument(parameters);
                }
                catch (Exception unexpectedError)
                {
                    Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnDidChangeTextDocument");
                }
            }
        }

        /// <summary>
        ///     Handle a document being saved.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task INotificationHandler<DidSaveTextDocumentParams>.Handle(DidSaveTextDocumentParams parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            using (BeginOperation("OnDidSaveTextDocument"))
            {
                try
                {
                    await OnDidSaveTextDocument(parameters);
                }
                catch (Exception unexpectedError)
                {
                    Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnDidSaveTextDocument");
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
        TextDocumentAttributes ITextDocumentSyncHandler.GetTextDocumentAttributes(Uri documentUri)
        {
            if (documentUri == null)
                throw new ArgumentNullException(nameof(documentUri));

            return GetTextDocumentAttributes(documentUri);
        }
    }
}
