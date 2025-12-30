using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace MSBuildProjectTools.LanguageServer.Documents
{
    using Diagnostics;
    using Microsoft.VisualStudio.SolutionPersistence.Model;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     The workspace that holds project documents.
    /// </summary>
    public class Workspace
        : IDisposable
    {
        /// <summary>
        ///     Documents, keyed by document URI.
        /// </summary>
        readonly ConcurrentDictionary<DocumentUri, Document> _documents = new ConcurrentDictionary<DocumentUri, Document>();

        /// <summary>
        ///     Create a new <see cref="Workspace"/>.
        /// </summary>
        /// <param name="server">
        ///     The language server.
        /// </param>
        /// <param name="configuration">
        ///     The language server configuration.
        /// </param>
        /// <param name="diagnosticsPublisher">
        ///     The diagnostic publishing facility.
        /// </param>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public Workspace(ILanguageServer server, Configuration configuration, IPublishDiagnostics diagnosticsPublisher, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(diagnosticsPublisher);
            ArgumentNullException.ThrowIfNull(logger);

            Server = server;
            Configuration = configuration;
            DiagnosticsPublisher = diagnosticsPublisher;
            Log = logger.ForContext<Workspace>();

            DataDirectory = new DirectoryInfo(
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "data")
            );
            TaskMetadataCache = new MSBuildTaskMetadataCache(
                logger: logger.ForContext<MSBuildTaskMetadataCache>()
            );
            TaskMetadataCacheFile = new FileInfo(
                Path.Combine(DataDirectory.FullName, "task-metadata-cache.json")
            );
        }

        /// <summary>
        ///     Finalizer for <see cref="Workspace"/>.
        /// </summary>
        ~Workspace()
        {
            Dispose(false);
        }

        /// <summary>
        ///     Dispose of resources being used by the <see cref="Workspace"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Dispose of resources being used by the <see cref="Workspace"/>.
        /// </summary>
        /// <param name="disposing">
        ///     Explicit disposal?
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            Document[] documents = _documents.Values.ToArray();
            _documents.Clear();

            foreach (Document document in documents)
                document.Dispose();
        }

        /// <summary>
        ///     The language server configuration.
        /// </summary>
        public Configuration Configuration { get; }

        /// <summary>
        ///     The directory where extension data is stored.
        /// </summary>
        public DirectoryInfo DataDirectory { get; }

        /// <summary>
        ///     The file that stores the persisted task metadata cache.
        /// </summary>
        public FileInfo TaskMetadataCacheFile { get; }

        /// <summary>
        ///     The cache for MSBuild task metadata.
        /// </summary>
        public MSBuildTaskMetadataCache TaskMetadataCache { get; }

        /// <summary>
        ///     The master project (if any).
        /// </summary>
        /// <remarks>
        ///     TODO: Make this selectable from the editor (get the extension to show a pick-list of open projects).
        /// </remarks>
        MasterProjectDocument MasterProject { get; set; }

        /// <summary>
        ///     The language server.
        /// </summary>
        ILanguageServer Server { get; }

        /// <summary>
        ///     The diagnostic publishing facility.
        /// </summary>
        IPublishDiagnostics DiagnosticsPublisher { get; }

        /// <summary>
        ///     The workspace logger.
        /// </summary>
        ILogger Log { get; }

        /// <summary>
        ///     Has the version of MSBuild in use been logged?
        /// </summary>
        bool _msbuildVersionLogged;

        /// <summary>
        ///     Try to retrieve the current state for the specified document.
        /// </summary>
        /// <param name="documentUri">
        ///     The document URI.
        /// </param>
        /// <param name="reload">
        ///     Reload the document if it is already loaded?
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the async operation.
        /// </param>
        /// <returns>
        ///     The document.
        /// </returns>
        public async Task<Document> GetDocument(DocumentUri documentUri, bool reload = false, CancellationToken cancellationToken = default)
        {
            string documentFilePath = DocumentUri.GetFileSystemPath(documentUri);
            DocumentKind documentKind = GetDocumentKind(documentFilePath);

            switch (documentKind)
            {
                case DocumentKind.Project:
                {
                    bool isNewProject = false;
                    var projectDocument = (ProjectDocument)_documents.GetOrAdd(documentUri, _ =>
                    {
                        isNewProject = true;

                        if (MasterProject == null)
                        {
                            MasterProjectDocument masterProjectDocument = new(this, documentUri, Log);
                            MasterProject = masterProjectDocument;

                            return masterProjectDocument;
                        }

                        SubProjectDocument subProject = MasterProject.GetOrAddSubProject(documentUri,
                            () => new SubProjectDocument(this, documentUri, Log, MasterProject)
                        );

                        return subProject;
                    });

                    if (!_msbuildVersionLogged)
                    {
                        if (MSBuildHelper.HaveMSBuild)
                        {
                            Log.Information("Using MSBuild engine v{MSBuildVersion:l} from {MSBuildPath}.",
                                MSBuildHelper.MSBuildVersion,
                                MSBuildHelper.MSBuildPath
                            );
                        }
                        else
                            Log.Warning("Failed to find any version of MSBuild compatible with the current .NET SDK (respecting global.json).");

                        _msbuildVersionLogged = true;
                    }

                    try
                    {
                        if (isNewProject || reload)
                        {
                            using (await projectDocument.Lock.WriterLockAsync(cancellationToken))
                            {
                                await projectDocument.Load(cancellationToken);
                            }
                        }
                    }
                    catch (XmlException invalidXml)
                    {
                        Log.Error("Error parsing project file {ProjectFilePath}: {ErrorMessage:l}",
                            documentFilePath,
                            invalidXml.Message
                        );
                    }
                    catch (Exception loadError)
                    {
                        Log.Error(loadError, "Unexpected error loading file {ProjectFilePath}.", documentFilePath);
                    }

                    return projectDocument;
                }
                case DocumentKind.Solution:
                {
                    bool isNewSolution = false;

                    var solutionDocument = (SolutionDocument)_documents.GetOrAdd(documentUri, _ =>
                    {
                        isNewSolution = true;

                        return new SolutionDocument(this, documentUri, Log);
                    });

                    try
                    {
                        if (isNewSolution || reload)
                        {
                            using (await solutionDocument.Lock.WriterLockAsync(cancellationToken))
                            {
                                await solutionDocument.Load(cancellationToken);
                            }
                        }
                    }
                    catch (SolutionException invalidSolution)
                    {
                        Log.Error("Error parsing solution file {ProjectFilePath} ({ErrorType}): {ErrorMessage:l}",
                            documentFilePath,
                            invalidSolution.ErrorType,
                            invalidSolution.Message
                        );
                    }
                    catch (Exception loadError)
                    {
                        Log.Error(loadError, "Unexpected error loading file {ProjectFilePath}.", documentFilePath);
                    }

                    return solutionDocument;
                }
                default:
                {
                    throw new InvalidOperationException($"Unsupported document kind '{documentKind}' for document '{documentFilePath}'.");
                }
            }
        }

        /// <summary>
        ///     Try to retrieve the current state for the specified project document.
        /// </summary>
        /// <param name="documentUri">
        ///     The project document URI.
        /// </param>
        /// <param name="reload">
        ///     Reload the project if it is already loaded?
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the async operation.
        /// </param>
        /// <returns>
        ///     The project document.
        /// </returns>
        public async Task<ProjectDocument> GetProjectDocument(DocumentUri documentUri, bool reload = false, CancellationToken cancellationToken = default)
        {
            string projectFilePath = DocumentUri.GetFileSystemPath(documentUri);

            bool isNewProject = false;
            var projectDocument = (ProjectDocument)_documents.GetOrAdd(documentUri, _ =>
            {
                isNewProject = true;

                if (MasterProject == null)
                {
                    MasterProjectDocument masterProjectDocument = new(this, documentUri, Log);
                    MasterProject = masterProjectDocument;

                    return masterProjectDocument;
                }

                SubProjectDocument subProject = MasterProject.GetOrAddSubProject(documentUri,
                    () => new SubProjectDocument(this, documentUri, Log, MasterProject)
                );

                return subProject;
            });

            if (!_msbuildVersionLogged)
            {
                if (MSBuildHelper.HaveMSBuild)
                {
                    Log.Information("Using MSBuild engine v{MSBuildVersion:l} from {MSBuildPath}.",
                        MSBuildHelper.MSBuildVersion,
                        MSBuildHelper.MSBuildPath
                    );
                }
                else
                    Log.Warning("Failed to find any version of MSBuild compatible with the current .NET SDK (respecting global.json).");

                _msbuildVersionLogged = true;
            }

            try
            {
                if (isNewProject || reload)
                {
                    using (await projectDocument.Lock.WriterLockAsync(cancellationToken))
                    {
                        await projectDocument.Load(cancellationToken);
                    }
                }
            }
            catch (XmlException invalidXml)
            {
                Log.Error("Error parsing project file {ProjectFilePath}: {ErrorMessage:l}",
                    projectFilePath,
                    invalidXml.Message
                );
            }
            catch (Exception loadError)
            {
                Log.Error(loadError, "Unexpected error loading file {ProjectFilePath}.", projectFilePath);
            }

            return projectDocument;
        }

        /// <summary>
        ///     Try to retrieve a loaded project document.
        /// </summary>
        /// <param name="documentUri">
        ///     The project document URI.
        /// </param>
        /// <returns>
        ///     The project document.
        /// </returns>
        /// <exception cref="InvalidOperationException"></exception>
        public ProjectDocument GetLoadedProjectDocument(DocumentUri documentUri)
        {
            if (_documents.TryGetValue(documentUri, out Document document))
            {
                if (document is ProjectDocument projectDocument)
                    return projectDocument;

                Log.Error("Tried to use non-project document with document URI {DocumentUri}.", documentUri);

                throw new InvalidOperationException($"Document with URI '{documentUri}' is not a project.");
            }

            Log.Error("Tried to use non-existent project with document URI {DocumentUri}.", documentUri);

            throw new InvalidOperationException($"Project with document URI '{documentUri}' is not loaded.");
        }

        /// <summary>
        ///     Try to update the current state for the specified document.
        /// </summary>
        /// <param name="documentUri">
        ///     The document URI.
        /// </param>
        /// <param name="documentText">
        ///     The new document text.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the async operation.
        /// </param>
        /// <returns>
        ///     The document.
        /// </returns>
        public async Task<Document> TryUpdateDocument(DocumentUri documentUri, string documentText, CancellationToken cancellationToken = default)
        {
            if (!_documents.TryGetValue(documentUri, out Document document))
            {
                Log.Error("Tried to update non-existent document with URI {DocumentUri}.", documentUri);

                throw new InvalidOperationException($"Document with URI '{documentUri}' is not loaded.");
            }

            try
            {
                using (await document.Lock.WriterLockAsync(cancellationToken))
                {
                    await document.Update(documentText, cancellationToken);
                }
            }
            catch (Exception updateError)
            {
                Log.Error(updateError, "Failed to update project {ProjectFile}.", document.DocumentFile.FullName);
            }

            return document;
        }

        /// <summary>
        ///     Try to retrieve the current state for the specified project document.
        /// </summary>
        /// <param name="documentUri">
        ///     The project document URI.
        /// </param>
        /// <param name="documentText">
        ///     The new document text.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the async operation.
        /// </param>
        /// <returns>
        ///     The project document.
        /// </returns>
        public async Task<ProjectDocument> TryUpdateProjectDocument(DocumentUri documentUri, string documentText, CancellationToken cancellationToken = default)
        {
            if (!_documents.TryGetValue(documentUri, out Document document))
            {
                Log.Error("Tried to update non-existent project with document URI {DocumentUri}.", documentUri);

                throw new InvalidOperationException($"Project with document URI '{documentUri}' is not loaded.");
            }

            if (document is not ProjectDocument projectDocument)
            {
                Log.Error("Tried to update non-project document with URI {DocumentUri}.", documentUri);

                throw new InvalidOperationException($"Document with URI '{documentUri}' is not a project.");
            }

            try
            {
                using (await projectDocument.Lock.WriterLockAsync(cancellationToken))
                {
                    await projectDocument.Update(documentText, cancellationToken);
                }
            }
            catch (Exception updateError)
            {
                Log.Error(updateError, "Failed to update project {ProjectFile}.", projectDocument.ProjectFile.FullName);
            }

            return projectDocument;
        }

        /// <summary>
        ///     Publish current diagnostics (if any) for the specified document.
        /// </summary>
        /// <param name="document">
        ///     The document.
        /// </param>
        public void PublishDiagnostics(Document document)
        {
            ArgumentNullException.ThrowIfNull(document);

            DiagnosticsPublisher.Publish(
                documentUri: document.DocumentUri,
                diagnostics: document.Diagnostics.ToArray()
            );
        }

        /// <summary>
        ///     Clear current diagnostics (if any) for the specified project document.
        /// </summary>
        /// <param name="document">
        ///     The document.
        /// </param>
        public void ClearDiagnostics(Document document)
        {
            ArgumentNullException.ThrowIfNull(document);

            if (!document.HasDiagnostics)
                return;

            DiagnosticsPublisher.Publish(
                documentUri: document.DocumentUri,
                diagnostics: null // Overwrites existing diagnostics for this document with an empty list
            );
        }

        /// <summary>
        ///     Remove a document from the workspace.
        /// </summary>
        /// <param name="documentUri">
        ///     The document URI.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the async operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> that resolves to <c>true</c> if the document was removed to the workspace; otherwise, <c>false</c>.
        /// </returns>
        public async Task<bool> RemoveDocument(DocumentUri documentUri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(documentUri);

            if (!_documents.TryRemove(documentUri, out Document document))
                return false;

            if (MasterProject == document)
                MasterProject = null;

            using (await document.Lock.WriterLockAsync(cancellationToken))
            {
                ClearDiagnostics(document);

                await document.Unload(cancellationToken);
            }

            return true;
        }

        /// <summary>
        ///     Remove a project document from the workspace.
        /// </summary>
        /// <param name="documentUri">
        ///     The document URI.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the async operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> that resolves to <c>true</c> if the document was removed to the workspace; otherwise, <c>false</c>.
        /// </returns>
        public async Task<bool> RemoveProjectDocument(DocumentUri documentUri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(documentUri);

            if (!_documents.TryRemove(documentUri, out Document document))
                return false;

            if (document is not ProjectDocument projectDocument)
            {
                Log.Error("Tried to remove non-project document with URI {DocumentUri}.", documentUri);

                throw new InvalidOperationException($"Document with URI '{documentUri}' is not a project.");
            }

            if (MasterProject == document)
                MasterProject = null;

            using (await projectDocument.Lock.WriterLockAsync(cancellationToken))
            {
                ClearDiagnostics(projectDocument);

                await projectDocument.Unload(cancellationToken);
            }

            return true;
        }

        /// <summary>
        ///     Attempt to restore the task metadata cache from persisted state.
        /// </summary>
        /// <returns>
        ///     <c>true</c>, if the task metadata cache was restored from persisted state; otherwise, <c>false</c>.
        /// </returns>
        public bool RestoreTaskMetadataCache()
        {
            if (!TaskMetadataCacheFile.Exists)
                return false;

            try
            {
                TaskMetadataCache.Load(TaskMetadataCacheFile.FullName);

                return true;
            }
            catch (Exception cacheLoadError)
            {
                Log.Error(cacheLoadError, "An unexpected error occurred while restoring the task metadata cache.");

                return false;
            }
        }

        /// <summary>
        ///     Persist the task metadata cache to disk.
        /// </summary>
        public void PersistTaskMetadataCache()
        {
            if (!TaskMetadataCache.IsDirty)
                return; // Nothing new to persist.

            if (!TaskMetadataCacheFile.Directory.Exists)
                DataDirectory.Create();

            TaskMetadataCache.Save(TaskMetadataCacheFile.FullName);
        }

        static DocumentKind GetDocumentKind(string documentPath)
        {
            if (string.IsNullOrWhiteSpace(documentPath))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(documentPath)}.", nameof(documentPath));

            string fileExension = Path.GetExtension(documentPath);
            if (String.IsNullOrWhiteSpace(fileExension))
                return DocumentKind.Unknown;

            if (fileExension == ".slnx")
                return DocumentKind.Solution;

            if (fileExension.EndsWith("proj"))
                return DocumentKind.Project;

            return DocumentKind.Unknown;
        }
    }
}
