using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.Handlers
{
    using ContentProviders;
    using Documents;
    using MediatR;
    using OmniSharp.Extensions.LanguageServer.Protocol.Document;
    using OmniSharp.Extensions.LanguageServer.Protocol.Server;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     Handler for document hover requests.
    /// </summary>
    public sealed class HoverHandler
        : Handler, IHoverHandler
    {
        /// <summary>
        ///     Create a new <see cref="HoverHandler"/>.
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
        public HoverHandler(ILanguageServer server, Workspace workspace, ILogger logger)
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
        HoverRegistrationOptions HoverRegistrationOptions
        {
            get => new HoverRegistrationOptions
            {
                DocumentSelector = DocumentSelector
            };
        }

        /// <summary>
        ///     Called when the mouse pointer hovers over text.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> whose result is the hover details, or <c>null</c> if no hover details are provided by the handler.
        /// </returns>
        async Task<Hover> OnHover(TextDocumentPositionParams parameters, CancellationToken cancellationToken)
        {
            if (Workspace.Configuration.Language.DisableFeature.Hover)
                return null;

            ProjectDocument projectDocument = await Workspace.GetProjectDocument(parameters.TextDocument.Uri, cancellationToken: cancellationToken);

            using (await projectDocument.Lock.ReaderLockAsync(cancellationToken))
            {
                // This won't work if we can't inspect the MSBuild project state and match it up to the target position.
                if (!projectDocument.HasMSBuildProject || projectDocument.IsMSBuildProjectCached)
                {
                    Log.Debug("Not providing hover information for project {ProjectFile} (the underlying MSBuild project is not currently valid; see the list of diagnostics applicable to this file for more information).",
                        projectDocument.ProjectFile.FullName
                    );

                    return null;
                }

                Position position = parameters.Position.ToNative();

                XmlLocation location = projectDocument.XmlLocator.Inspect(position);
                if (location == null)
                {
                    Log.Debug("Not providing hover information for {Position} in {ProjectFile} (nothing interesting at this position).",
                        position,
                        projectDocument.ProjectFile.FullName
                    );

                    return null;
                }

                Log.Debug("Examining location {Location:l}...", location);

                if (!location.IsElementOrAttribute())
                {
                    Log.Debug("Not providing hover information for {Position} in {ProjectFile} (position does not represent an element or attribute).",
                        position,
                        projectDocument.ProjectFile.FullName
                    );

                    return null;
                }

                // Match up the MSBuild item / property with its corresponding XML element / attribute.
                MSBuildObject msbuildObject;

                Container<MarkedString> hoverContent = null;
                var contentProvider = new HoverContentProvider(projectDocument);
                if (location.IsElement(out XSElement element))
                {
                    msbuildObject = projectDocument.GetMSBuildObjectAtPosition(element.Start);
                    switch (msbuildObject)
                    {
                        case MSBuildProperty property:
                        {
                            hoverContent = HoverContentProvider.Property(property);

                            break;
                        }
                        case MSBuildUnusedProperty unusedProperty:
                        {
                            hoverContent = HoverContentProvider.UnusedProperty(unusedProperty);

                            break;
                        }
                        case MSBuildItemGroup itemGroup:
                        {
                            hoverContent = contentProvider.ItemGroup(itemGroup);

                            break;
                        }
                        case MSBuildUnusedItemGroup unusedItemGroup:
                        {
                            hoverContent = HoverContentProvider.UnusedItemGroup(unusedItemGroup);

                            break;
                        }
                        case MSBuildTarget target:
                        {
                            // Currently (and this is a bug), an MSBuildTarget is returned by MSBuildLocator when the location being inspected
                            // is actually on one of its child (task) elements.
                            if (element.Path == WellKnownElementPaths.Target)
                                hoverContent = HoverContentProvider.Target(target);

                            break;
                        }
                        case MSBuildImport import:
                        {
                            hoverContent = HoverContentProvider.Import(import);

                            break;
                        }
                        case MSBuildUnresolvedImport unresolvedImport:
                        {
                            hoverContent = contentProvider.UnresolvedImport(unresolvedImport);

                            break;
                        }
                        default:
                        {
                            hoverContent = HoverContentProvider.Element(element);

                            break;
                        }
                    }
                }
                else if (location.IsElementText(out XSElementText text))
                {
                    msbuildObject = projectDocument.GetMSBuildObjectAtPosition(text.Element.Start);
                    switch (msbuildObject)
                    {
                        case MSBuildProperty property:
                        {
                            hoverContent = HoverContentProvider.Property(property);

                            break;
                        }
                        case MSBuildUnusedProperty unusedProperty:
                        {
                            hoverContent = HoverContentProvider.UnusedProperty(unusedProperty);

                            break;
                        }
                    }
                }
                else if (location.IsAttribute(out XSAttribute attribute))
                {
                    msbuildObject = projectDocument.GetMSBuildObjectAtPosition(attribute.Start);
                    switch (msbuildObject)
                    {
                        case MSBuildItemGroup itemGroup:
                        {
                            hoverContent = contentProvider.ItemGroupMetadata(itemGroup, attribute.Name);

                            break;
                        }
                        case MSBuildUnusedItemGroup unusedItemGroup:
                        {
                            hoverContent = contentProvider.UnusedItemGroupMetadata(unusedItemGroup, attribute.Name);

                            break;
                        }
                        case MSBuildSdkImport sdkImport:
                        {
                            hoverContent = HoverContentProvider.SdkImport(sdkImport);

                            break;
                        }
                        case MSBuildUnresolvedSdkImport unresolvedSdkImport:
                        {
                            hoverContent = contentProvider.UnresolvedSdkImport(unresolvedSdkImport);

                            break;
                        }
                        case MSBuildImport import:
                        {
                            hoverContent = HoverContentProvider.Import(import);

                            break;
                        }
                        default:
                        {
                            if (attribute.Name == "Condition")
                                hoverContent = contentProvider.Condition(attribute.Element.Name, attribute.Value);

                            break;
                        }
                    }
                }

                if (hoverContent == null)
                {
                    Log.Debug("No hover content available for {Position} in {ProjectFile}.",
                        position,
                        projectDocument.ProjectFile.FullName
                    );

                    return null;
                }

                return new Hover
                {
                    Contents = new MarkedStringsOrMarkupContent(hoverContent),
                    Range = location.Node.Range.ToLsp()
                };
            }
        }

        /// <summary>
        ///     Get registration options for handling document events.
        /// </summary>
        /// <returns>
        ///     The registration options.
        /// </returns>
        HoverRegistrationOptions IRegistration<HoverRegistrationOptions>.GetRegistrationOptions() => HoverRegistrationOptions;

        /// <summary>
        ///     Handle a request for hover information.
        /// </summary>
        /// <param name="parameters">
        ///     The request parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation whose result is the hover details or <c>null</c> if no hover details are provided.
        /// </returns>
        async Task<Hover> IRequestHandler<HoverParams, Hover>.Handle(HoverParams parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            try
            {
                return await OnHover(parameters, cancellationToken);
            }
            catch (Exception unexpectedError)
            {
                Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnHover");

                return null;
            }
        }

        /// <summary>
        ///     Called to inform the handler of the language server's hover capabilities.
        /// </summary>
        /// <param name="capabilities">
        ///     A <see cref="HoverCapability"/> data structure representing the capabilities.
        /// </param>
        void ICapability<HoverCapability>.SetCapability(HoverCapability capabilities)
        {
        }
    }
}
