using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MSBuildProjectTools.LanguageServer.ToolTipProviders
{
    using ContentProviders;
    using Documents;
    using SemanticModel;

    /// <summary>
    ///     Tooltip provider for MSBuild <see cref="SolutionDocument"/>s.
    /// </summary>
    public class SolutionDocumentToolTipProvider
        : ToolTipProvider<SolutionDocument>
    {
        /// <summary>
        ///     Create a new <see cref="SolutionDocumentToolTipProvider"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public SolutionDocumentToolTipProvider(ILogger logger)
            : base(logger)
        {
        }

        /// <summary>
        ///     Provide tooltip content for the specified location.
        /// </summary>
        /// <param name="location">
        ///     The <see cref="XmlLocation"/> where hovers are requested.
        /// </param>
        /// <param name="solutionDocument">
        ///     The <see cref="SolutionDocument"/> that contains the <paramref name="location"/>.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> that resolves either a <see cref="MarkedStringsOrMarkupContent"/>, or <c>null</c> if no tooltips are provided.
        /// </returns>
        public override async Task<MarkedStringsOrMarkupContent?> ProvideToolTipContentAsync(XmlLocation location, SolutionDocument solutionDocument, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(location);
            ArgumentNullException.ThrowIfNull(solutionDocument);

            Log.Verbose("Evaluate tooltip for {XmlLocation:l}", location);

            using (await solutionDocument.Lock.ReaderLockAsync(cancellationToken))
            {
                // Match up the MSBuild item / property with its corresponding XML element / attribute.
                VsSolutionObject? vsSolutionObject;

                Container<MarkedString>? tooltipContent = null;

                if (location.IsElement(out XSElement element))
                {
                    vsSolutionObject = solutionDocument.GetVsSolutionObjectAtPosition(element.Start);

                    switch (vsSolutionObject)
                    {
                        case VsSolutionRoot solutionRoot:
                        {
                            tooltipContent = null; // TODO: Provide content for the solution root.

                            break;
                        }
                        case VsSolutionFolder folder:
                        {
                            tooltipContent = null; // TODO: Provide content for the solution root.

                            break;
                        }
                        case VsSolutionProject project:
                        {
                            tooltipContent = null; // TODO: Provide content for the project.

                            break;
                        }
                        default:
                        {
                            tooltipContent = null; // TODO: Provide content for the element.

                            break;
                        }
                    }
                }
                else if (location.IsElementText(out XSElementText text))
                {
                    vsSolutionObject = solutionDocument.GetVsSolutionObjectAtPosition(element.Start);

                    switch (vsSolutionObject)
                    {
                        case VsSolutionRoot solutionRoot:
                        {
                            tooltipContent = null; // TODO: Provide content for the solution root.

                            break;
                        }
                        case VsSolutionFolder folder:
                        {
                            tooltipContent = null; // TODO: Provide content for the solution root.

                            break;
                        }
                        case VsSolutionProject project:
                        {
                            tooltipContent = null; // TODO: Provide content for the project.

                            break;
                        }
                        default:
                        {
                            tooltipContent = null; // TODO: Provide content for the containing element.

                            break;
                        }
                    }
                }
                else if (location.IsAttribute(out XSAttribute attribute))
                {
                    vsSolutionObject = solutionDocument.GetVsSolutionObjectAtPosition(element.Start);

                    switch (vsSolutionObject)
                    {
                        case VsSolutionRoot solutionRoot:
                        {
                            tooltipContent = null; // TODO: Provide content for the solution root.

                            break;
                        }
                        case VsSolutionFolder folder:
                        {
                            tooltipContent = null; // TODO: Provide content for the solution root.

                            break;
                        }
                        case VsSolutionProject project:
                        {
                            tooltipContent = null; // TODO: Provide content for the project.

                            break;
                        }
                        default:
                        {
                            tooltipContent = null; // TODO: Provide content for the attribute.

                            break;
                        }
                    }
                }

                if (tooltipContent == null)
                {
                    Log.Debug("No hover content available for {Position} in {ProjectFile}.",
                        location.Position,
                        solutionDocument.SolutionFile.FullName
                    );

                    return null;
                }

                return new MarkedStringsOrMarkupContent(tooltipContent);
            }
        }
    }
}
