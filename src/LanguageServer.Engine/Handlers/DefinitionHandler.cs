using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Serilog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.Handlers
{
    using Documents;
    using MediatR;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     Handler for symbol definition requests.
    /// </summary>
    public sealed class DefinitionHandler
        : Handler, IDefinitionHandler
    {
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
            ArgumentNullException.ThrowIfNull(workspace);

            Workspace = workspace;
        }

        /// <summary>
        ///     The document workspace.
        /// </summary>
        Workspace Workspace { get; }

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
        DefinitionRegistrationOptions DocumentRegistrationOptions
        {
            get => new DefinitionRegistrationOptions
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
        async Task<LocationOrLocationLinks> OnDefinition(TextDocumentPositionParams parameters, CancellationToken cancellationToken)
        {
            ProjectDocument projectDocument = await Workspace.GetProjectDocument(parameters.TextDocument.Uri, cancellationToken: cancellationToken);

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
                    LocationOrLocationLink[] locations =
                        sdkImportAtPosition.ImportedProjectRoots.Select(
                            importedProjectRoot => new LocationOrLocationLink(
                                new Location
                                {
                                    Range = Range.Empty.ToLsp(),
                                    Uri = VSCodeDocumentUri.FromFileSystemPath(importedProjectRoot.Location.File)
                                })
                        )
                        .ToArray();

                    return new LocationOrLocationLinks(locations);
                }
                else if (msbuildObjectAtPosition is MSBuildImport importAtPosition)
                {
                    // TODO: Parse imported project and determine location of root element (use that range instead).
                    return new LocationOrLocationLinks(
                        importAtPosition.ImportedProjectRoots.Select(
                            importedProjectRoot => new LocationOrLocationLink(
                                new Location
                                {
                                    Range = Range.Empty.ToLsp(),
                                    Uri = VSCodeDocumentUri.FromFileSystemPath(importedProjectRoot.Location.File)
                                })
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
        async Task<LocationOrLocationLinks> IRequestHandler<DefinitionParams, LocationOrLocationLinks>.Handle(DefinitionParams parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(parameters);

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
        DefinitionRegistrationOptions IRegistration<DefinitionRegistrationOptions>.GetRegistrationOptions() => DocumentRegistrationOptions;

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
