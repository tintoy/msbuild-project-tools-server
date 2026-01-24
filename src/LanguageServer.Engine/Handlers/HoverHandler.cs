using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MSBuildProjectTools.LanguageServer.Handlers
{
    using ContentProviders;
    using Documents;
    using MediatR;
    using MSBuildProjectTools.LanguageServer.ToolTipProviders;
    using SemanticModel;
    using System.Collections.Generic;
    using System.Linq;
    using Utilities;

    /// <summary>
    ///     Handler for document tooltip requests.
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
        /// <param name="toolTipProviders">
        ///     Registered tooltip providers.
        /// </param>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public HoverHandler(ILanguageServer server, Workspace workspace, IEnumerable<IToolTipProvider> toolTipProviders, ILogger logger)
            : base(server, logger)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ArgumentNullException.ThrowIfNull(toolTipProviders);

            Workspace = workspace;
            ToolTipProviders = toolTipProviders;
        }

        /// <summary>
        ///     The document workspace.
        /// </summary>
        Workspace Workspace { get; }

        /// <summary>
        ///     Registered tooltip providers.
        /// </summary>
        IEnumerable<IToolTipProvider> ToolTipProviders { get; }

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
        ///     Called when the mouse pointer tooltips over text.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> whose result is the tooltip details, or <c>null</c> if no tooltip details are provided by the handler.
        /// </returns>
        async Task<Hover?> OnHover(TextDocumentPositionParams parameters, CancellationToken cancellationToken)
        {
            if (Workspace.Configuration.Language.DisableFeature.Hover)
                return null;

            Document document = await Workspace.GetDocument(parameters.TextDocument.Uri, cancellationToken: cancellationToken);

            using (await document.Lock.ReaderLockAsync(cancellationToken))
            {
                Position position = parameters.Position.ToNative();

                var xmlDocument = document as XmlDocument;
                var projectDocument = document as ProjectDocument;
                var solutionDocument = document as SolutionDocument;

                if (projectDocument != null)
                {
                    // This won't work if we can't inspect the MSBuild project state and match it up to the target position.
                    if (!projectDocument.HasMSBuildProject || projectDocument.IsMSBuildProjectCached)
                    {
                        Log.Debug("Not providing tooltip information for project {ProjectFile} (the underlying MSBuild project is not currently valid; see the list of diagnostics applicable to this file for more information).",
                            projectDocument.ProjectFile.FullName
                        );

                        return null;
                    }
                }
                else if (solutionDocument != null)
                {
                    // This won't work if we can't inspect the MSBuild project state and match it up to the target position.
                    if (!solutionDocument.HasSolution || solutionDocument.IsSolutionCached)
                    {
                        Log.Debug("Not providing tooltip information for solution {SolutionFile} (the underlying solution is not currently valid; see the list of diagnostics applicable to this file for more information).",
                            solutionDocument.SolutionFile.FullName
                        );

                        return null;
                    }
                }

                XmlLocation? location;
                if (xmlDocument != null)
                {
                    location = xmlDocument.XmlLocator.Inspect(position);
                    if (location == null)
                    {
                        Log.Debug("Not providing tooltip information for {Position} in {DocumentFile} (nothing interesting at this position).",
                            position,
                            xmlDocument.DocumentFile.FullName
                        );

                        return null;
                    }
                }
                else
                {
                    Log.Debug("Not providing tooltip information for {Position} in {DocumentFile} (tooltips are not supported for this file type).",
                        position,
                        document.DocumentFile.FullName
                    );

                    return null;
                }

                Log.Debug("Examining location {Location:l}...", location);

                if (!location.IsElementOrAttribute())
                {
                    Log.Debug("Not providing tooltip information for {Position} in {ProjectFile} (position does not represent an element or attribute).",
                        position,
                        document.DocumentFile.FullName
                    );

                    return null;
                }

                MarkedStringsOrMarkupContent? toolTipContent = null;

                foreach (IToolTipProvider toolTipProvider in ToolTipProviders.OrderBy(provider => provider.Priority))
                {
                    // The following logic implements document-kind fallback behaviour for tooltip providers (e.g. a tooltip provider for XmlDocuments will also be called for ProjectDocuments and SolutionDocuments).

                    if (projectDocument != null && toolTipProvider is IToolTipProvider<ProjectDocument> projectDocumentToolTipProvider)
                    {
                        toolTipContent = await projectDocumentToolTipProvider.ProvideToolTipContentAsync(location, projectDocument, cancellationToken);
                        if (toolTipContent != null)
                            break;
                    }

                    if (solutionDocument != null && toolTipProvider is IToolTipProvider<SolutionDocument> solutionDocumentToolTipProvider)
                    {
                        toolTipContent = await solutionDocumentToolTipProvider.ProvideToolTipContentAsync(location, solutionDocument, cancellationToken);
                        if (toolTipContent != null)
                            break;
                    }

                    if (toolTipProvider is IToolTipProvider<XmlDocument> xmlDocumentToolTipProvider)
                    {
                        toolTipContent = await xmlDocumentToolTipProvider.ProvideToolTipContentAsync(location, xmlDocument, cancellationToken);
                        if (toolTipContent != null)
                            break;
                    }
                }

                if (toolTipContent == null)
                {
                    Log.Debug("No tooltip content available for {Position} in {DocumentFile}.",
                        position,
                        document.DocumentFile.FullName
                    );

                    return null;
                }

                return new Hover
                {
                    Contents = toolTipContent,
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
        ///     Handle a request for tooltip information.
        /// </summary>
        /// <param name="parameters">
        ///     The request parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation whose result is the tooltip details or <c>null</c> if no tooltip details are provided.
        /// </returns>
        async Task<Hover> IRequestHandler<HoverParams, Hover>.Handle(HoverParams parameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            
            try
            {
                return await OnHover(parameters, cancellationToken) ?? new Hover();
            }
            catch (Exception unexpectedError)
            {
                Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnHover");

                return new Hover();
            }
        }

        /// <summary>
        ///     Called to inform the handler of the language server's tooltip capabilities.
        /// </summary>
        /// <param name="capabilities">
        ///     A <see cref="HoverCapability"/> data structure representing the capabilities.
        /// </param>
        void ICapability<HoverCapability>.SetCapability(HoverCapability capabilities)
        {
        }
    }
}
