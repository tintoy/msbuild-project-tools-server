using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace MSBuildProjectTools.LanguageServer.Documents
{
    using Diagnostics;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     The workspace that holds project documents.
    /// </summary>
    public class Workspace
        : IDisposable
    {
        /// <summary>
        ///     Documents for loaded project, keyed by document URI.
        /// </summary>
        private readonly ConcurrentDictionary<Uri, ProjectDocument> _projectDocuments = new ConcurrentDictionary<Uri, ProjectDocument>();

        /// <summary>
        ///     The master project (if any).
        /// </summary>
        /// <remarks>
        ///     TODO: Make this selectable from the editor (get the extension to show a pick-list of open projects).
        /// </remarks>
        private MasterProjectDocument _masterProject;

        /// <summary>
        ///     The language server.
        /// </summary>
        private readonly ILanguageServer _server;

        /// <summary>
        ///     The diagnostic publishing facility.
        /// </summary>
        private readonly IPublishDiagnostics _diagnosticsPublisher;

        /// <summary>
        ///     The workspace logger.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        ///     Has the version of MSBuild in use been logged?
        /// </summary>
        private bool _msbuildVersionLogged;

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
            if (server == null)
                throw new ArgumentNullException(nameof(server));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (diagnosticsPublisher == null)
                throw new ArgumentNullException(nameof(diagnosticsPublisher));

            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            _server = server;
            Configuration = configuration;
            _diagnosticsPublisher = diagnosticsPublisher;
            _logger = logger.ForContext<Workspace>();

            string extensionDirectory = Environment.GetEnvironmentVariable("MSBUILD_PROJECT_TOOLS_DIR");
            if (string.IsNullOrWhiteSpace(extensionDirectory))
                throw new InvalidOperationException("Cannot determine current extension directory ('MSBUILD_PROJECT_TOOLS_DIR' environment variable is not present).");

            ExtensionDirectory = new DirectoryInfo(extensionDirectory);
            ExtensionDataDirectory = new DirectoryInfo(
                Path.Combine(ExtensionDirectory.FullName, "data")
            );
            TaskMetadataCache = new MSBuildTaskMetadataCache(
                logger: logger.ForContext<MSBuildTaskMetadataCache>()
            );
            TaskMetadataCacheFile = new FileInfo(
                Path.Combine(ExtensionDataDirectory.FullName, "task-metadata-cache.json")
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
            ProjectDocument[] projectDocuments = _projectDocuments.Values.ToArray();
            _projectDocuments.Clear();

            foreach (ProjectDocument projectDocument in projectDocuments)
                projectDocument.Dispose();
        }

        /// <summary>
        ///     The root directory for the workspace.
        /// </summary>
        public DirectoryInfo RootDirectory
        {
            get
            {
                return new DirectoryInfo(_server.Client.RootPath);
            }
        }

        /// <summary>
        ///     The language server configuration.
        /// </summary>
        public Configuration Configuration { get; }

        /// <summary>
        ///     The directory where the MSBuild Project Tools extension is located.
        /// </summary>
        public DirectoryInfo ExtensionDirectory { get; }

        /// <summary>
        ///     The directory where extension data is stored.
        /// </summary>
        public DirectoryInfo ExtensionDataDirectory { get; }

        /// <summary>
        ///     The file that stores the persisted task metadata cache.
        /// </summary>
        public FileInfo TaskMetadataCacheFile { get; }

        /// <summary>
        ///     The cache for MSBuild task metadata.
        /// </summary>
        public MSBuildTaskMetadataCache TaskMetadataCache { get; }

        /// <summary>
        ///     Try to retrieve the current state for the specified project document.
        /// </summary>
        /// <param name="documentUri">
        ///     The project document URI.
        /// </param>
        /// <param name="reload">
        ///     Reload the project if it is already loaded?
        /// </param>
        /// <returns>
        ///     The project document.
        /// </returns>
        public async Task<ProjectDocument> GetProjectDocument(Uri documentUri, bool reload = false)
        {
            string projectFilePath = VSCodeDocumentUri.GetFileSystemPath(documentUri);

            bool isNewProject = false;
            ProjectDocument projectDocument = _projectDocuments.GetOrAdd(documentUri, _ =>
            {
                isNewProject = true;

                if (_masterProject == null)
                {
                    _masterProject = new MasterProjectDocument(this, documentUri, _logger);
                    return _masterProject;
                }

                SubProjectDocument subProject = new SubProjectDocument(this, documentUri, _logger, _masterProject);
                _masterProject.AddSubProject(subProject);

                return subProject;
            });

            if (!_msbuildVersionLogged)
            {
                if (MSBuildHelper.HaveMSBuild)
                {
                    _logger.Information("Using MSBuild engine v{MSBuildVersion:l} from {MSBuildPath}.",
                        MSBuildHelper.MSBuildVersion,
                        MSBuildHelper.MSBuildPath
                    );
                }
                else
                    _logger.Warning("Failed to find any version of MSBuild compatible with the current .NET SDK (respecting global.json).");

                _msbuildVersionLogged = true;
            }

            try
            {
                if (isNewProject || reload)
                {
                    using (await projectDocument.Lock.WriterLockAsync())
                    {
                        await projectDocument.Load();
                    }
                }
            }
            catch (XmlException invalidXml)
            {
                _logger.Error("Error parsing project file {ProjectFilePath}: {ErrorMessage:l}",
                    projectFilePath,
                    invalidXml.Message
                );
            }
            catch (Exception loadError)
            {
                _logger.Error(loadError, "Unexpected error loading file {ProjectFilePath}.", projectFilePath);
            }

            return projectDocument;
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
        /// <returns>
        ///     The project document.
        /// </returns>
        public async Task<ProjectDocument> TryUpdateProjectDocument(Uri documentUri, string documentText)
        {
            if (!_projectDocuments.TryGetValue(documentUri, out ProjectDocument projectDocument))
            {
                _logger.Error("Tried to update non-existent project with document URI {DocumentUri}.", documentUri);

                throw new InvalidOperationException($"Project with document URI '{documentUri}' is not loaded.");
            }

            try
            {
                using (await projectDocument.Lock.WriterLockAsync())
                {
                    projectDocument.Update(documentText);
                }
            }
            catch (Exception updateError)
            {
                _logger.Error(updateError, "Failed to update project {ProjectFile}.", projectDocument.ProjectFile.FullName);
            }

            return projectDocument;
        }

        /// <summary>
        ///     Publish current diagnostics (if any) for the specified project document.
        /// </summary>
        /// <param name="projectDocument">
        ///     The project document.
        /// </param>
        public void PublishDiagnostics(ProjectDocument projectDocument)
        {
            if (projectDocument == null)
                throw new ArgumentNullException(nameof(projectDocument));

            _diagnosticsPublisher.Publish(
                documentUri: projectDocument.DocumentUri,
                diagnostics: projectDocument.Diagnostics.ToArray()
            );
        }

        /// <summary>
        ///     Clear current diagnostics (if any) for the specified project document.
        /// </summary>
        /// <param name="projectDocument">
        ///     The project document.
        /// </param>
        public void ClearDiagnostics(ProjectDocument projectDocument)
        {
            if (projectDocument == null)
                throw new ArgumentNullException(nameof(projectDocument));

            if (!projectDocument.HasDiagnostics)
                return;

            _diagnosticsPublisher.Publish(
                documentUri: projectDocument.DocumentUri,
                diagnostics: null // Overwrites existing diagnostics for this document with an empty list
            );
        }

        /// <summary>
        ///     Remove a project document from the workspace.
        /// </summary>
        /// <param name="documentUri">
        ///     The document URI.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> that resolves to <c>true</c> if the document was removed to the workspace; otherwise, <c>false</c>.
        /// </returns>
        public async Task<bool> RemoveProjectDocument(Uri documentUri)
        {
            if (documentUri == null)
                throw new ArgumentNullException(nameof(documentUri));

            if (!_projectDocuments.TryRemove(documentUri, out ProjectDocument projectDocument))
                return false;

            if (_masterProject == projectDocument)
                _masterProject = null;

            using (await projectDocument.Lock.WriterLockAsync())
            {
                ClearDiagnostics(projectDocument);

                projectDocument.Unload();
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
                _logger.Error(cacheLoadError, "An unexpected error occurred while restoring the task metadata cache.");

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
                ExtensionDataDirectory.Create();

            TaskMetadataCache.Save(TaskMetadataCacheFile.FullName);
        }
    }
}
