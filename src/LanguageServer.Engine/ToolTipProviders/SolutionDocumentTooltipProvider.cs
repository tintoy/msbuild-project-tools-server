using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MSBuildProjectTools.LanguageServer.ToolTipProviders
{
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
                            tooltipContent = new List<MarkedString>
                            {
                                $"Solution: `{solutionDocument.DocumentFile.Name}`"
                            };

                            break;
                        }
                        case VsSolutionFolder folder:
                        {
                            tooltipContent = new List<MarkedString>
                            {
                                $"Folder: `{folder.Name}`"
                            };

                            break;
                        }
                        case VsSolutionFile item:
                        {
                            tooltipContent = new List<MarkedString>
                            {
                                $"File: `{item.Name}`"
                            };

                            break;
                        }
                        case VsSolutionProject project:
                        {
                            tooltipContent = new List<MarkedString>
                            {
                                $"Project: `{project.Name}`"
                            };

                            break;
                        }
                        default:
                        {
                            if (vsSolutionObject != null)
                            {
                                tooltipContent = new List<MarkedString>
                                {
                                    $"Unknown {vsSolutionObject.GetType().Name.Replace("VsSolution", String.Empty)}: `{vsSolutionObject.Name}`"
                                };
                            }
                            else
                                tooltipContent = null;

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
                            tooltipContent = new List<MarkedString>
                            {
                                $"Solution: `{solutionDocument.DocumentFile.Name}`"
                            };

                            break;
                        }
                        case VsSolutionFolder folder:
                        {
                            tooltipContent = new List<MarkedString>
                            {
                                $"Folder: `{folder.Name}`"
                            };

                            break;
                        }
                        case VsSolutionFile item:
                        {
                            tooltipContent = new List<MarkedString>
                            {
                                $"File: `{item.Name}`"
                            };

                            break;
                        }
                        case VsSolutionProject project:
                        {
                            tooltipContent = new List<MarkedString>
                            {
                                $"Project: `{project.Name}`"
                            };

                            break;
                        }
                        default:
                        {
                            if (vsSolutionObject != null)
                            {
                                tooltipContent = new List<MarkedString>
                                {
                                    $"Unknown {vsSolutionObject.GetType().Name.Replace("VsSolution", String.Empty)}: `{vsSolutionObject.Name}`"
                                };
                            }
                            else
                                tooltipContent = null;

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
                            // We don't currently support tooltips for any specific attributes of the solution root element.
                            tooltipContent = new List<MarkedString>
                            {
                                $"Solution: `{solutionDocument.DocumentFile.Name}`"
                            };

                            break;
                        }
                        case VsSolutionFolder folder:
                        {
                            switch (attribute.Name)
                            {
                                case "Name":
                                {
                                    tooltipContent = new List<MarkedString>
                                    {
                                        $"Folder.Name: `{folder.Name}`"
                                    };

                                    break;
                                }
                                default:
                                {
                                    tooltipContent = new List<MarkedString>
                                    {
                                        $"Folder: `{folder.Name}`"
                                    };

                                    break;
                                }
                            }

                            break;
                        }
                        case VsSolutionFile file:
                        {
                            switch (attribute.Name)
                            {
                                case "Path":
                                {
                                    tooltipContent = new List<MarkedString>
                                    {
                                        $"File.Path: `{file.Name}`"
                                    };

                                    break;
                                }
                                default:
                                {
                                    tooltipContent = new List<MarkedString>
                                    {
                                        $"File: `{file.Name}`"
                                    };

                                    break;
                                }
                            }

                            break;
                        }
                        case VsSolutionProject project:
                        {
                            switch (attribute.Name)
                            {
                                case "Path":
                                {
                                    tooltipContent = new List<MarkedString>
                                    {
                                        $"Project.Path: `{project.Name}`"
                                    };

                                    break;
                                }
                                default:
                                {
                                    tooltipContent = new List<MarkedString>
                                    {
                                        $"Project: `{project.Name}`"
                                    };

                                    break;
                                }
                            }

                            break;
                        }
                        default:
                        {
                            if (vsSolutionObject != null)
                            {
                                tooltipContent = new List<MarkedString>
                                {
                                    $"Unknown {vsSolutionObject.GetType().Name.Replace("VsSolution", String.Empty)}: `{vsSolutionObject.Name}`"
                                };
                            }
                            else
                                tooltipContent = null;

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
