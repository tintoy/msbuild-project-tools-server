using Microsoft.Build.Exceptions;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace MSBuildProjectTools.LanguageServer.Documents
{
    using SemanticModel;
    using System.Collections.Concurrent;
    using Utilities;

    /// <summary>
    ///     Represents the document state for an MSBuild project.
    /// </summary>
    public class MasterProjectDocument
        : ProjectDocument
    {
        /// <summary>
        ///     Sub-projects (if any).
        /// </summary>
        readonly ConcurrentDictionary<Uri, SubProjectDocument> _subProjects = new ConcurrentDictionary<Uri, SubProjectDocument>();

        /// <summary>
        ///     Create a new <see cref="MasterProjectDocument"/>.
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
        public MasterProjectDocument(Workspace workspace, Uri documentUri, ILogger logger)
            : base(workspace, documentUri, logger)
        {
        }

        /// <summary>
        ///     Dispose of resources being used by the <see cref="ProjectDocument"/>.
        /// </summary>
        /// <param name="disposing">
        ///     Explicit disposal?
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (MSBuildProjectCollection != null)
                {
                    MSBuildProjectCollection.Dispose();
                    MSBuildProjectCollection = null;
                }
            }
        }

        /// <summary>
        ///     Sub-projects (if any).
        /// </summary>
        public IReadOnlyDictionary<Uri, SubProjectDocument> SubProjects => _subProjects;

        /// <summary>
        ///     Add a sub-project.
        /// </summary>
        /// <param name="documentUri">
        ///     The sub-project.
        /// </param>
        /// <param name="createSubProjectDocument">
        ///     A factory delegate to create the <see cref="SubProjectDocument"/> if it does not already exist.
        /// </param>
        public SubProjectDocument GetOrAddSubProject(Uri documentUri, Func<SubProjectDocument> createSubProjectDocument)
        {
            if (documentUri == null)
                throw new ArgumentNullException(nameof(documentUri));

            if (createSubProjectDocument == null)
                throw new ArgumentNullException(nameof(createSubProjectDocument));

            return _subProjects.GetOrAdd(documentUri, _ => createSubProjectDocument());
        }

        /// <summary>
        ///     Remove a sub-project.
        /// </summary>
        /// <param name="documentUri">
        ///     The sub-project document URI.
        /// </param>
        public void RemoveSubProject(Uri documentUri)
        {
            if (documentUri == null)
                throw new ArgumentNullException(nameof(documentUri));

            if (_subProjects.TryRemove(documentUri, out SubProjectDocument subProjectDocument))
                subProjectDocument.Unload();
        }

        /// <summary>
        ///     Unload the project.
        /// </summary>
        public override void Unload()
        {
            // Unload sub-projects, if necessary.
            Uri[] subProjectDocumentUris = SubProjects.Keys.ToArray();
            foreach (Uri subProjectDocumentUri in subProjectDocumentUris)
                RemoveSubProject(subProjectDocumentUri);

            base.Unload();
        }

        /// <summary>
        ///     Load the project document.
        /// </summary>
        /// <param name="cancellationToken">
        ///     A cancellation token that can be used to cancel the load.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        public override async Task Load(CancellationToken cancellationToken)
        {
            await base.Load(cancellationToken);

            if (!Workspace.Configuration.NuGet.DisablePreFetch)
                WarmUpNuGetClient();
        }

        /// <summary>
        ///     Attempt to load the underlying MSBuild project.
        /// </summary>
        /// <returns>
        ///     <c>true</c>, if the project was successfully loaded; otherwise, <c>false</c>.
        /// </returns>
        protected override bool TryLoadMSBuildProject()
        {
            try
            {
                if (HasMSBuildProject && !IsDirty)
                    return true;

                MSBuildProjectCollection ??= MSBuildHelper.CreateProjectCollection(ProjectFile.Directory.FullName,
                    globalPropertyOverrides: GetMSBuildGlobalPropertyOverrides()
                );

                if (HasMSBuildProject && IsDirty)
                {
                    using (var reader = new StringReader(Xml.ToFullString()))
                    using (var xmlReader = new XmlTextReader(reader))
                    {
                        MSBuildProject.Xml.ReloadFrom(xmlReader,
                            throwIfUnsavedChanges: false,
                            preserveFormatting: true
                        );
                    }

                    MSBuildProject.ReevaluateIfNecessary();

                    Log.Verbose("Successfully updated MSBuild project '{ProjectFileName}' from in-memory changes.");
                }
                else
                    MSBuildProject = MSBuildProjectCollection.LoadProject(ProjectFile.FullName);

                return true;
            }
            catch (InvalidProjectFileException invalidProjectFile)
            {
                if (Workspace.Configuration.Logging.IsDebugLoggingEnabled)
                {
                    Log.Error(invalidProjectFile, "Failed to load MSBuild project '{ProjectFileName}'.",
                        ProjectFile.FullName
                    );
                }

                AddErrorDiagnostic(invalidProjectFile.BaseMessage,
                    range: invalidProjectFile.GetRange(XmlLocator),
                    diagnosticCode: invalidProjectFile.ErrorCode
                );
            }
            catch (XmlException invalidProjectXml)
            {
                if (Workspace.Configuration.Logging.IsDebugLoggingEnabled)
                {
                    Log.Error(invalidProjectXml, "Failed to parse XML for project '{ProjectFileName}'.",
                        ProjectFile.FullName
                    );
                }

                // TODO: Match SourceUri (need overloads of AddXXXDiagnostic for reporting diagnostics for other files).
                AddErrorDiagnostic(invalidProjectXml.Message,
                    range: invalidProjectXml.GetRange(XmlLocator),
                    diagnosticCode: "MSBuild.InvalidXML"
                );
            }
            catch (Exception loadError)
            {
                Log.Error(loadError, "Error loading MSBuild project '{ProjectFileName}'.", ProjectFile.FullName);
            }

            return false;
        }

        /// <summary>
        ///     Attempt to unload the underlying MSBuild project.
        /// </summary>
        /// <returns>
        ///     <c>true</c>, if the project was successfully unloaded; otherwise, <c>false</c>.
        /// </returns>
        protected override bool TryUnloadMSBuildProject()
        {
            try
            {
                if (!HasMSBuildProject)
                    return true;

                if (MSBuildProjectCollection == null)
                    return true;

                MSBuildProjectCollection.UnloadProject(MSBuildProject);
                MSBuildProject = null;

                return true;
            }
            catch (Exception unloadError)
            {
                Log.Error(unloadError, "Error unloading MSBuild project '{ProjectFileName}'.", ProjectFile.FullName);

                return false;
            }
        }
    }
}
