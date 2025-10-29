using NuGet.Versioning;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MSBuildProjectTools.LanguageServer.ContentProviders
{
    using Documents;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     Content for tooltips when hovering over nodes in the MSBuild XML.
    /// </summary>
    public class HoverContentProvider
    {
        /// <summary>
        ///     The project document for which hover content is provided.
        /// </summary>
        /// <remarks>
        ///     Because async-friendly locks are not re-entrant, the provider assumes that the project document's state lock is already held by any callers of its public methods.
        /// </remarks>
        readonly ProjectDocument _projectDocument;

        /// <summary>
        ///     Create a new <see cref="HoverContentProvider"/>.
        /// </summary>
        /// <param name="projectDocument">
        ///     The project document for which hover content is provided.
        /// </param>
        /// <remarks>
        ///     Because async-friendly locks are not re-entrant, the provider assumes that the project document's state lock is already held by any callers of its public methods.
        /// </remarks>
        public HoverContentProvider(ProjectDocument projectDocument)
        {
            ArgumentNullException.ThrowIfNull(projectDocument);

            _projectDocument = projectDocument;
        }

        /// <summary>
        ///     Get hover content for an <see cref="MSBuildProperty"/>.
        /// </summary>
        /// <param name="property">
        ///     The <see cref="MSBuildProperty"/>.
        /// </param>
        /// <returns>
        ///     The content, or <c>null</c> if no content is provided.
        /// </returns>
        public static Container<MarkedString> Property(MSBuildProperty property)
        {
            ArgumentNullException.ThrowIfNull(property);

            var content = new List<MarkedString>
            {
                $"Property: `{property.Name}`"
            };

            string propertyHelp = MSBuildSchemaHelp.ForProperty(property.Name);
            if (propertyHelp != null)
                content.Add(propertyHelp);

            if (property.IsOverridden)
            {
                // BUG: This is the location of the *overridden* property, not the *overriding* property.
                //      We'll need to build a lookup by recursively following ProjectProperty.Predecessor.
                Position overridingDeclarationPosition = property.DeclaringXml.Location.ToNative();

                var overrideDescription = new StringBuilder();
                string declarationFile = property.DeclaringXml.Location.File;
                if (declarationFile != property.Property.Xml.Location.File)
                {
                    Uri declarationDocumentUri = VSCodeDocumentUri.FromFileSystemPath(declarationFile);
                    overrideDescription.AppendLine(
                        $"Value overridden at {overridingDeclarationPosition} in [{Path.GetFileName(declarationFile)}]({declarationDocumentUri})."
                    );
                }
                else
                    overrideDescription.AppendLine($"Value overridden at {overridingDeclarationPosition} in this file.");

                overrideDescription.AppendLine();
                overrideDescription.AppendLine();
                overrideDescription.AppendLine(
                    $"Unused value: `{property.DeclaringXml.Value}`"
                );
                overrideDescription.AppendLine();
                overrideDescription.AppendLine(
                    $"Actual value: `{property.Value}`"
                );

                content.Add(overrideDescription.ToString());
            }
            else
                content.Add($"Value: `{property.Value}`");

            string helpLink = MSBuildSchemaHelp.HelpLinkForProperty(property.Name);
            if (!string.IsNullOrWhiteSpace(helpLink))
            {
                content.Add(
                    $"[Help]({helpLink})"
                );
            }

            return new Container<MarkedString>(content);
        }

        /// <summary>
        ///     Get hover content for an <see cref="MSBuildUnusedProperty"/>.
        /// </summary>
        /// <param name="unusedProperty">
        ///     The <see cref="MSBuildUnusedProperty"/>.
        /// </param>
        /// <returns>
        ///     The content, or <c>null</c> if no content is provided.
        /// </returns>
        public static Container<MarkedString> UnusedProperty(MSBuildUnusedProperty unusedProperty)
        {
            ArgumentNullException.ThrowIfNull(unusedProperty);

            var content = new List<MarkedString>();
            if (unusedProperty.Element.HasParentPath(WellKnownElementPaths.DynamicPropertyGroup))
            {
                content.Add(
                    $"Dynamic Property: `{unusedProperty.Name}`"
                );
                content.Add(
                    "(properties declared in targets are only evaluated when building the project)"
                );
            }
            else
            {
                content.Add(
                    $"Unused Property: `{unusedProperty.Name}` (condition is false)"
                );
            }

            string propertyHelp = MSBuildSchemaHelp.ForProperty(unusedProperty.Name);
            if (propertyHelp != null)
                content.Add(propertyHelp);

            content.Add(
                $"Value would have been: `{unusedProperty.Value}`"
            );

            string helpLink = MSBuildSchemaHelp.HelpLinkForProperty(unusedProperty.Name);
            if (!string.IsNullOrWhiteSpace(helpLink))
            {
                content.Add(
                    $"[Help]({helpLink})"
                );
            }

            return new Container<MarkedString>(content);
        }

        /// <summary>
        ///     Get hover content for an <see cref="MSBuildItemGroup"/>.
        /// </summary>
        /// <param name="itemGroup">
        ///     The <see cref="MSBuildItemGroup"/>.
        /// </param>
        /// <returns>
        ///     The content, or <c>null</c> if no content is provided.
        /// </returns>
        public Container<MarkedString> ItemGroup(MSBuildItemGroup itemGroup)
        {
            ArgumentNullException.ThrowIfNull(itemGroup);

            if (itemGroup.Name is "PackageReference" || itemGroup.Name is "PackageVersion")
            {
                string packageId = itemGroup.FirstInclude;
                string packageRequestedVersion = itemGroup.GetFirstMetadataValue("Version");
                if (!_projectDocument.ReferencedPackageVersions.TryGetValue(packageId, out SemanticVersion packageActualVersion))
                {
                    return new Container<MarkedString>(
                        $"NuGet Package: {itemGroup.FirstInclude}",
                        $"Requested Version: {packageRequestedVersion}`",
                        "State: Not restored"
                    );
                }

                // TODO: Verify package is from NuGet (later, we can also recognize MyGet)

                return new Container<MarkedString>(
                    $"NuGet Package: [{itemGroup.FirstInclude}](https://nuget.org/packages/{itemGroup.FirstInclude}/{packageActualVersion})",
                    $"Requested Version: `{packageRequestedVersion}`\nActual Version: `{packageActualVersion}`",
                    "State: Restored"
                );
            }

            var content = new List<MarkedString>
            {
                $"Item Group: `{itemGroup.OriginatingElement.ItemType}`"
            };

            string itemTypeHelp = MSBuildSchemaHelp.ForItemType(itemGroup.Name);
            if (itemTypeHelp != null)
                content.Add(itemTypeHelp);

            string[] includes = itemGroup.Includes.ToArray();
            var itemIncludeContent = new StringBuilder();
            itemIncludeContent.AppendLine(
                $"Include: `{itemGroup.OriginatingElement.Include}`  "
            );
            itemIncludeContent.AppendLine();
            itemIncludeContent.Append(
                $"Evaluates to {itemGroup.Items.Count} item"
            );
            if (!itemGroup.HasSingleItem)
                itemIncludeContent.Append('s');
            itemIncludeContent.AppendLine(".");

            foreach (string include in includes.Take(5))
            {
                // TODO: Consider making hyperlinks for includes that map to files which exist.
                itemIncludeContent.AppendLine(
                    $"* `{include}`"
                );
            }
            if (includes.Length > 5)
                itemIncludeContent.AppendLine("* ...");

            content.Add(
                itemIncludeContent.ToString()
            );

            string helpLink = MSBuildSchemaHelp.HelpLinkForItem(itemGroup.Name);
            if (!string.IsNullOrWhiteSpace(helpLink))
            {
                content.Add(
                    $"[Help]({helpLink})"
                );
            }

            return new Container<MarkedString>(content);
        }

        /// <summary>
        ///     Get hover content for an <see cref="MSBuildUnusedItemGroup"/>.
        /// </summary>
        /// <param name="unusedItemGroup">
        ///     The <see cref="MSBuildUnusedItemGroup"/>.
        /// </param>
        /// <returns>
        ///     The content, or <c>null</c> if no content is provided.
        /// </returns>
        public static Container<MarkedString> UnusedItemGroup(MSBuildUnusedItemGroup unusedItemGroup)
        {
            ArgumentNullException.ThrowIfNull(unusedItemGroup);

            var content = new List<MarkedString>
            {
                $"Unused Item Group: `{unusedItemGroup.OriginatingElement.ItemType}` (condition is false)"
            };

            string itemTypeHelp = MSBuildSchemaHelp.ForItemType(unusedItemGroup.Name);
            if (itemTypeHelp != null)
                content.Add(itemTypeHelp);

            var descriptionContent = new StringBuilder();

            string[] includes = unusedItemGroup.Includes.ToArray();
            descriptionContent.AppendLine(
                $"Include: `{unusedItemGroup.OriginatingElement.Include}`  "
            );
            descriptionContent.AppendLine();
            descriptionContent.Append(
                $"Would have evaluated to {unusedItemGroup.Items.Count} item"
            );
            if (!unusedItemGroup.HasSingleItem)
                descriptionContent.Append('s');
            descriptionContent.AppendLine(":");

            foreach (string include in includes.Take(5))
            {
                // TODO: Consider making hyperlinks for includes that map to files which exist.
                descriptionContent.AppendLine(
                    $"* `{include}`"
                );
            }
            if (includes.Length > 5)
                descriptionContent.AppendLine("* ...");

            content.Add(
                descriptionContent.ToString()
            );

            string helpLink = MSBuildSchemaHelp.HelpLinkForItem(unusedItemGroup.Name);
            if (!string.IsNullOrWhiteSpace(helpLink))
            {
                content.Add(
                    $"[Help]({helpLink})"
                );
            }

            return new Container<MarkedString>(content);
        }

        /// <summary>
        ///     Get hover content for an MSBuild condition.
        /// </summary>
        /// <param name="elementName">
        ///     The name of the element that contains the Condition attribute.
        /// </param>
        /// <param name="condition">
        ///     The raw (unevaluated) condition.
        /// </param>
        /// <returns>
        ///     The content, or <c>null</c> if no content is provided.
        /// </returns>
        public Container<MarkedString> Condition(string elementName, string condition)
        {
            if (string.IsNullOrWhiteSpace(elementName))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'elementName'.", nameof(elementName));

            if (string.IsNullOrWhiteSpace(condition))
                return null;

            string evaluatedCondition = _projectDocument.MSBuildProject.ExpandString(condition);

            var content = new List<MarkedString>
            {
                "Condition",
                $"Evaluated: `{evaluatedCondition}`"
            };

            string helpLink = MSBuildSchemaHelp.HelpLinkForElement("*.Condition");
            if (!string.IsNullOrWhiteSpace(helpLink))
            {
                content.Add(
                    $"[Help]({helpLink})"
                );
            }

            return new Container<MarkedString>(content);
        }

        /// <summary>
        ///     Get hover content for metadata of an <see cref="MSBuildItemGroup"/>.
        /// </summary>
        /// <param name="itemGroup">
        ///     The <see cref="MSBuildItemGroup"/>.
        /// </param>
        /// <param name="metadataName">
        ///     The metadata name.
        /// </param>
        /// <returns>
        ///     The content, or <c>null</c> if no content is provided.
        /// </returns>
        public Container<MarkedString> ItemGroupMetadata(MSBuildItemGroup itemGroup, string metadataName)
        {
            ArgumentNullException.ThrowIfNull(itemGroup);

            if (string.IsNullOrWhiteSpace(metadataName))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'metadataName'.", nameof(metadataName));

            if (itemGroup.Name is "PackageReference" or "PackageVersion")
                return ItemGroup(itemGroup);

            if (metadataName == "Condition")
                return Condition(itemGroup.Name, itemGroup.FirstItem.Xml.Condition);

            if (metadataName == "Include")
                metadataName = "Identity";

            var content = new List<MarkedString>
            {
                $"Item Metadata: `{itemGroup.Name}.{metadataName}`"
            };

            string metadataHelp = MSBuildSchemaHelp.ForItemMetadata(itemGroup.Name, metadataName);
            if (metadataHelp != null)
                content.Add(metadataHelp);

            string[] metadataValues =
                itemGroup.GetMetadataValues(metadataName).Where(
                    value => !string.IsNullOrWhiteSpace(value)
                )
                .Distinct()
                .ToArray();

            var metadataContent = new StringBuilder();
            if (metadataValues.Length > 0)
            {
                metadataContent.AppendLine("Values:");
                foreach (string metadataValue in metadataValues)
                {
                    metadataContent.AppendLine(
                        $"* `{metadataValue}`"
                    );
                }
            }
            else
                metadataContent.AppendLine("No values are present for this metadata.");

            content.Add(
                metadataContent.ToString()
            );

            string helpLink = MSBuildSchemaHelp.HelpLinkForItem(itemGroup.Name);
            if (!string.IsNullOrWhiteSpace(helpLink))
            {
                content.Add(
                    $"[Help]({helpLink})"
                );
            }

            return new Container<MarkedString>(content);
        }

        /// <summary>
        ///     Get hover content for a metadata attribute of an <see cref="MSBuildUnusedItemGroup"/>.
        /// </summary>
        /// <param name="itemGroup">
        ///     The <see cref="MSBuildUnusedItemGroup"/>.
        /// </param>
        /// <param name="metadataName">
        ///     The name of the metadata attribute.
        /// </param>
        /// <returns>
        ///     The content, or <c>null</c> if no content is provided.
        /// </returns>
        public Container<MarkedString> UnusedItemGroupMetadata(MSBuildUnusedItemGroup itemGroup, string metadataName)
        {
            ArgumentNullException.ThrowIfNull(itemGroup);

            if (string.IsNullOrWhiteSpace(metadataName))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'metadataName'.", nameof(metadataName));

            if (itemGroup.Name is "PackageReference" or "PackageVersion")
                return UnusedItemGroup(itemGroup);

            if (metadataName == "Condition")
                return Condition(itemGroup.Name, itemGroup.FirstItem.Xml.Condition);

            if (metadataName == "Include")
                metadataName = "Identity";

            var content = new List<MarkedString>
            {
                $"Unused Item Metadata: `{itemGroup.Name}.{metadataName}` (item condition is false)"
            };

            string metadataHelp = MSBuildSchemaHelp.ForItemMetadata(itemGroup.Name, metadataName);
            if (metadataHelp != null)
                content.Add(metadataHelp);

            string[] metadataValues =
                itemGroup.GetMetadataValues(metadataName).Where(
                    value => !string.IsNullOrWhiteSpace(value)
                )
                .Distinct()
                .ToArray();

            var metadataContent = new StringBuilder();
            if (metadataValues.Length > 0)
            {
                metadataContent.AppendLine("Values:");
                foreach (string metadataValue in metadataValues)
                {
                    metadataContent.AppendLine(
                        $"* `{metadataValue}`"
                    );
                }
            }
            else
                metadataContent.AppendLine("No values are present for this metadata.");

            content.Add(
                metadataContent.ToString()
            );

            return new Container<MarkedString>(content);
        }

        /// <summary>
        ///     Get hover content for an <see cref="MSBuildTarget"/>.
        /// </summary>
        /// <param name="target">
        ///     The <see cref="MSBuildTarget"/>.
        /// </param>
        /// <returns>
        ///     The content, or <c>null</c> if no content is provided.
        /// </returns>
        public static Container<MarkedString> Target(MSBuildTarget target)
        {
            ArgumentNullException.ThrowIfNull(target);

            var content = new List<MarkedString>
            {
                $"Target: `{target.Name}`"
            };

            string helpLink = MSBuildSchemaHelp.HelpLinkForElement(target.Element.Name);
            if (!string.IsNullOrWhiteSpace(helpLink))
            {
                content.Add(
                    $"[Help]({helpLink})"
                );
            }

            return new Container<MarkedString>(content);
        }

        /// <summary>
        ///     Get hover content for an <see cref="MSBuildImport"/>.
        /// </summary>
        /// <param name="import">
        ///     The <see cref="MSBuildImport"/>.
        /// </param>
        /// <returns>
        ///     The content, or <c>null</c> if no content is provided.
        /// </returns>
        public static Container<MarkedString> Import(MSBuildImport import)
        {
            ArgumentNullException.ThrowIfNull(import);

            var content = new List<MarkedString>
            {
                $"Import: `{import.Name}`"
            };

            var imports = new StringBuilder("Imports:");
            imports.AppendLine();
            foreach (string projectFile in import.ImportedProjectFiles)
                imports.AppendLine($"* [{Path.GetFileName(projectFile)}]({VSCodeDocumentUri.FromFileSystemPath(projectFile)})");

            content.Add(
                imports.ToString()
            );

            string helpLink = MSBuildSchemaHelp.HelpLinkForElement(import.Element.Name);
            if (!string.IsNullOrWhiteSpace(helpLink))
            {
                content.Add(
                    $"[Help]({helpLink})"
                );
            }

            return new Container<MarkedString>(content);
        }

        /// <summary>
        ///     Get hover content for an <see cref="MSBuildImport"/>.
        /// </summary>
        /// <param name="unresolvedImport">
        ///     The <see cref="MSBuildImport"/>.
        /// </param>
        /// <returns>
        ///     The content, or <c>null</c> if no content is provided.
        /// </returns>
        public Container<MarkedString> UnresolvedImport(MSBuildUnresolvedImport unresolvedImport)
        {
            ArgumentNullException.ThrowIfNull(unresolvedImport);

            string condition = unresolvedImport.Condition;
            string evaluatedCondition = _projectDocument.MSBuildProject.ExpandString(condition);

            string project = unresolvedImport.Project;
            string evaluatedProject = _projectDocument.MSBuildProject.ExpandString(project);

            var descriptionContent = new StringBuilder();
            descriptionContent.AppendLine(
                $"Project: `{project}`"
            );
            descriptionContent.AppendLine();
            descriptionContent.AppendLine(
                $"Evaluated Project: `{evaluatedProject}`"
            );
            descriptionContent.AppendLine();
            descriptionContent.AppendLine(
                $"Condition: `{condition}`"
            );
            descriptionContent.AppendLine();
            descriptionContent.AppendLine(
                $"Evaluated Condition: `{evaluatedCondition}`"
            );

            var content = new List<MarkedString>
            {
                "Unresolved Import (condition is false)",
                descriptionContent.ToString()
            };

            string helpLink = MSBuildSchemaHelp.HelpLinkForElement(unresolvedImport.Element.Name);
            if (!string.IsNullOrWhiteSpace(helpLink))
            {
                content.Add(
                    $"[Help]({helpLink})"
                );
            }

            return new Container<MarkedString>(content);
        }

        /// <summary>
        ///     Get hover content for an <see cref="MSBuildSdkImport"/>.
        /// </summary>
        /// <param name="sdkImport">
        ///     The <see cref="MSBuildSdkImport"/>.
        /// </param>
        /// <returns>
        ///     The content, or <c>null</c> if no content is provided.
        /// </returns>
        public static Container<MarkedString> SdkImport(MSBuildSdkImport sdkImport)
        {
            ArgumentNullException.ThrowIfNull(sdkImport);

            var imports = new StringBuilder("Imports:");
            imports.AppendLine();
            foreach (string projectFile in sdkImport.ImportedProjectFiles)
                imports.AppendLine($"* `{projectFile}`");

            return new Container<MarkedString>(
                $"SDK Import: {sdkImport.Name}",
                imports.ToString()
            );
        }

        /// <summary>
        ///     Get hover content for an <see cref="MSBuildUnresolvedSdkImport"/>.
        /// </summary>
        /// <param name="unresolvedSdkImport">
        ///     The <see cref="MSBuildUnresolvedSdkImport"/>.
        /// </param>
        /// <returns>
        ///     The content, or <c>null</c> if no content is provided.
        /// </returns>
        public Container<MarkedString> UnresolvedSdkImport(MSBuildUnresolvedSdkImport unresolvedSdkImport)
        {
            ArgumentNullException.ThrowIfNull(unresolvedSdkImport);

            string condition = unresolvedSdkImport.Condition;
            string evaluatedCondition = _projectDocument.MSBuildProject.ExpandString(condition);

            var descriptionContent = new StringBuilder();
            descriptionContent.AppendLine(
                $"Condition: `{condition}`"
            );
            descriptionContent.AppendLine();
            descriptionContent.AppendLine(
                $"Evaluated Condition: `{evaluatedCondition}`"
            );

            return new Container<MarkedString>(
                $"Unresolved Import `{unresolvedSdkImport.Sdk}` (condition is false)",
                descriptionContent.ToString()
            );
        }

        /// <summary>
        ///     Get hover content for an XML element that does not directly correspond to an <see cref="MSBuildObject"/>.
        /// </summary>
        /// <param name="element">
        ///     The <see cref="XSElement"/>.
        /// </param>
        /// <returns>
        ///     The content, or <c>null</c> if no content is provided.
        /// </returns>
        public static Container<MarkedString> Element(XSElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            string elementDescription = MSBuildSchemaHelp.ForElement(element.Name);
            if (string.IsNullOrWhiteSpace(elementDescription))
                return null;

            var content = new List<MarkedString>
            {
                elementDescription
            };

            string helpLink = MSBuildSchemaHelp.HelpLinkForElement(element.Name);
            if (!string.IsNullOrWhiteSpace(helpLink))
            {
                content.Add(
                    $"[Help]({helpLink})"
                );
            }

            return new Container<MarkedString>(content);
        }
    }
}
