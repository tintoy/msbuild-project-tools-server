using Microsoft.Build.Exceptions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Serilog;
using System;
using System.IO;
using System.Xml;

namespace MSBuildProjectTools.LanguageServer.Documents
{
    using SemanticModel;

    /// <summary>
    ///     Represents the document state for an MSBuild sub-project.
    /// </summary>
    public class SubProjectDocument
        : ProjectDocument
    {
        /// <summary>
        ///     Create a new <see cref="SubProjectDocument"/>.
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
        /// <param name="masterProjectDocument">
        ///     The master project document that owns the sub-project.
        /// </param>
        public SubProjectDocument(Workspace workspace, DocumentUri documentUri, ILogger logger, MasterProjectDocument masterProjectDocument)
            : base(workspace, documentUri, logger)
        {
            ArgumentNullException.ThrowIfNull(masterProjectDocument);

            MasterProjectDocument = masterProjectDocument;
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
                MSBuildProjectCollection = null;
        }

        /// <summary>
        ///     A <see cref="ProjectDocument"/> representing the project's parent (i.e. main) project.
        /// </summary>
        public ProjectDocument MasterProjectDocument { get; }

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

                if (!MasterProjectDocument.HasMSBuildProject)
                    throw new InvalidOperationException("Parent project does not have an MSBuild project.");

                MSBuildProjectCollection ??= MasterProjectDocument.MSBuildProjectCollection;

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
