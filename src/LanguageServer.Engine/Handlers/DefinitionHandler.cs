using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.Handlers
{
    using Documents;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     Handler for symbol definition requests.
    /// </summary>
    public sealed class DefinitionHandler
        : Handler, IDefinitionHandler
    {
        private readonly Workspace _workspace;

        /// <summary>
        ///     Create a new <see cref="DefinitionHandler"/>.
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
        public DefinitionHandler(ILanguageServer server, Workspace workspace, ILogger logger)
            : base(server, logger)
        {
            if (workspace == null)
                throw new ArgumentNullException(nameof(workspace));

            _workspace = workspace;
        }

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
        ///     Called when a definition is requested.
        /// </summary>
        /// <param name="parameters">
        ///     The request parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation whose result is the definition location or <c>null</c> if no definition is provided.
        /// </returns>
        async Task<LocationOrLocations> OnDefinition(TextDocumentPositionParams parameters, CancellationToken cancellationToken)
        {
            ProjectDocument projectDocument = await _workspace.GetProjectDocument(parameters.TextDocument.Uri);

            using (await projectDocument.Lock.ReaderLockAsync(cancellationToken))
            {
                if (!projectDocument.HasMSBuildProject || projectDocument.IsMSBuildProjectCached)
                    return null;

                Position position = parameters.Position.ToNative();
                MSBuildObject msbuildObjectAtPosition = projectDocument.GetMSBuildObjectAtPosition(position);
                if (msbuildObjectAtPosition == null)
                    return null;

                if (msbuildObjectAtPosition is MSBuildSdkImport sdkImportAtPosition)
                {
                    // TODO: Parse imported project and determine location of root element (use that range instead).
                    Location[] locations =
                        sdkImportAtPosition.ImportedProjectRoots.Select(
                            importedProjectRoot => new Location
                            {
                                Range = Range.Empty.ToLsp(),
                                Uri = VSCodeDocumentUri.FromFileSystemPath(importedProjectRoot.Location.File)
                            }
                        )
                        .ToArray();

                    return new LocationOrLocations(locations);
                }
                else if (msbuildObjectAtPosition is MSBuildImport importAtPosition)
                {
                    // TODO: Parse imported project and determine location of root element (use that range instead).
                    return new LocationOrLocations(
                        importAtPosition.ImportedProjectRoots.Select(
                            importedProjectRoot => new Location
                            {
                                Range = Range.Empty.ToLsp(),
                                Uri = VSCodeDocumentUri.FromFileSystemPath(importedProjectRoot.Location.File)
                            }
                    ));
                }
            }

            return null;
        }

        /// <summary>
        ///     Handle a request for a definition.
        /// </summary>
        /// <param name="parameters">
        ///     The request parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation whose result is definition location or <c>null</c> if no definition is provided.
        /// </returns>
        async Task<LocationOrLocations> IRequestHandler<TextDocumentPositionParams, LocationOrLocations>.Handle(TextDocumentPositionParams parameters, CancellationToken cancellationToken)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            using (BeginOperation("OnDefinition"))
            {
                try
                {
                    return await OnDefinition(parameters, cancellationToken);
                }
                catch (Exception unexpectedError)
                {
                    Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnDefinition");

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
        ///     Called to inform the handler of the language server's symbol definition capabilities.
        /// </summary>
        /// <param name="capabilities">
        ///     A <see cref="DefinitionCapability"/> data structure representing the capabilities.
        /// </param>
        void ICapability<DefinitionCapability>.SetCapability(DefinitionCapability capabilities)
        {
        }
    }
}
