using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

using ToolTip = OmniSharp.Extensions.LanguageServer.Protocol.Models.Hover;

namespace MSBuildProjectTools.LanguageServer.ToolTipProviders
{
    using ContentProviders;
    using Documents;
    using SemanticModel;
    using Utilities;
    
    /// <summary>
    ///     Tooltip provider for MSBuild <see cref="ProjectDocument"/>s.
    /// </summary>
    public class MSBuildProjectToolTipProvider
        : ToolTipProvider<ProjectDocument>
    {
        /// <summary>
        ///     Create a new <see cref="MSBuildProjectToolTipProvider"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public MSBuildProjectToolTipProvider(ILogger logger)
            : base(logger)
        {
        }

        /// <summary>
        ///     Provide tooltip content for the specified location.
        /// </summary>
        /// <param name="location">
        ///     The <see cref="XmlLocation"/> where hovers are requested.
        /// </param>
        /// <param name="projectDocument">
        ///     The <see cref="ProjectDocument"/> that contains the <paramref name="location"/>.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> that resolves either a <see cref="MarkedStringsOrMarkupContent"/>, or <c>null</c> if no tooltips are provided.
        /// </returns>
        public override async Task<MarkedStringsOrMarkupContent?> ProvideToolTipContentAsync(XmlLocation location, ProjectDocument projectDocument, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(location);
            ArgumentNullException.ThrowIfNull(projectDocument);

            Log.Verbose("Evaluate tooltip for {XmlLocation:l}", location);

            using (await projectDocument.Lock.ReaderLockAsync(cancellationToken))
            {
                // Match up the MSBuild item / property with its corresponding XML element / attribute.
                MSBuildObject msbuildObject;

                Container<MarkedString>? tooltipContent = null;
                var contentProvider = new HoverContentProvider(projectDocument);
                if (location.IsElement(out XSElement element))
                {
                    msbuildObject = projectDocument.GetMSBuildObjectAtPosition(element.Start);

                    switch (msbuildObject)
                    {
                        case MSBuildProperty property:
                        {
                            tooltipContent = HoverContentProvider.Property(property);

                            break;
                        }
                        case MSBuildUnusedProperty unusedProperty:
                        {
                            tooltipContent = HoverContentProvider.UnusedProperty(unusedProperty);

                            break;
                        }
                        case MSBuildItemGroup itemGroup:
                        {
                            tooltipContent = contentProvider.ItemGroup(itemGroup);

                            break;
                        }
                        case MSBuildUnusedItemGroup unusedItemGroup:
                        {
                            tooltipContent = HoverContentProvider.UnusedItemGroup(unusedItemGroup);

                            break;
                        }
                        case MSBuildTarget target:
                        {
                            // Currently (and this is a bug), an MSBuildTarget is returned by MSBuildLocator when the location being inspected
                            // is actually on one of its child (task) elements.
                            if (element.Path == WellKnownElementPaths.Target)
                                tooltipContent = HoverContentProvider.Target(target);

                            break;
                        }
                        case MSBuildImport import:
                        {
                            tooltipContent = HoverContentProvider.Import(import);

                            break;
                        }
                        case MSBuildUnresolvedImport unresolvedImport:
                        {
                            tooltipContent = contentProvider.UnresolvedImport(unresolvedImport);

                            break;
                        }
                        default:
                        {
                            tooltipContent = HoverContentProvider.Element(element);

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
                            tooltipContent = HoverContentProvider.Property(property);

                            break;
                        }
                        case MSBuildUnusedProperty unusedProperty:
                        {
                            tooltipContent = HoverContentProvider.UnusedProperty(unusedProperty);

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
                            tooltipContent = contentProvider.ItemGroupMetadata(itemGroup, attribute.Name);

                            break;
                        }
                        case MSBuildUnusedItemGroup unusedItemGroup:
                        {
                            tooltipContent = contentProvider.UnusedItemGroupMetadata(unusedItemGroup, attribute.Name);

                            break;
                        }
                        case MSBuildSdkImport sdkImport:
                        {
                            tooltipContent = HoverContentProvider.SdkImport(sdkImport);

                            break;
                        }
                        case MSBuildUnresolvedSdkImport unresolvedSdkImport:
                        {
                            tooltipContent = contentProvider.UnresolvedSdkImport(unresolvedSdkImport);

                            break;
                        }
                        case MSBuildImport import:
                        {
                            tooltipContent = HoverContentProvider.Import(import);

                            break;
                        }
                        default:
                        {
                            if (attribute.Name == "Condition")
                                tooltipContent = contentProvider.Condition(attribute.Element.Name, attribute.Value);

                            break;
                        }
                    }
                }

                if (tooltipContent == null)
                {
                    Log.Debug("No hover content available for {Position} in {ProjectFile}.",
                        location.Position,
                        projectDocument.ProjectFile.FullName
                    );

                    return null;
                }

                return new MarkedStringsOrMarkupContent(tooltipContent);
            }
        }
    }
}
