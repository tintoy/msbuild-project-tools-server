using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    using Utilities;

    /// <summary>
    ///     A facility for looking up MSBuild project members by textual location.
    /// </summary>
    public class MSBuildObjectLocator
    {
        /// <summary>
        ///     The ranges for all XML objects in the document with positional annotations.
        /// </summary>
        /// <remarks>
        ///     Sorted by range comparison (effectively, this means document order).
        /// </remarks>
        private readonly List<Range> _objectRanges = new List<Range>();

        /// <summary>
        ///     All objects in the project, keyed by starting position.
        /// </summary>
        /// <remarks>
        ///     Sorted by range comparison.
        /// </remarks>
        private readonly SortedDictionary<Position, MSBuildObject> _objectsByStartPosition = new SortedDictionary<Position, MSBuildObject>();

        /// <summary>
        ///     The MSBuild project.
        /// </summary>
        private readonly Project _project;

        /// <summary>
        ///     The full path to the MSBuild project file.
        /// </summary>
        private readonly string _projectFile;

        /// <summary>
        ///     The project XML.
        /// </summary>
        private readonly XmlLocator _projectXmlLocator;

        /// <summary>
        ///     The position-lookup for the project XML.
        /// </summary>
        private readonly TextPositions _xmlPositions;

        /// <summary>
        ///     Create a new <see cref="MSBuildObjectLocator"/>.
        /// </summary>
        /// <param name="project">
        ///     The MSBuild project.
        /// </param>
        /// <param name="projectXmlLocator">
        ///     The <see cref="XmlLocator"/> for the project XML.
        /// </param>
        /// <param name="xmlPositions">
        ///     The position-lookup for the project XML.
        /// </param>
        public MSBuildObjectLocator(Project project, XmlLocator projectXmlLocator, TextPositions xmlPositions)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            if (projectXmlLocator == null)
                throw new ArgumentNullException(nameof(projectXmlLocator));

            if (xmlPositions == null)
                throw new ArgumentNullException(nameof(xmlPositions));

            _project = project;
            _projectFile = _project.FullPath ?? string.Empty;
            _projectXmlLocator = projectXmlLocator;
            _xmlPositions = xmlPositions;

            AddTargets();
            AddProperties();
            AddItems();
            AddImports();

            _objectRanges.Sort();
        }

        /// <summary>
        ///     All known MSBuild objects.
        /// </summary>
        public IEnumerable<MSBuildObject> AllObjects => _objectsByStartPosition.Values;

        /// <summary>
        ///     Find the project object (if any) at the specified position.
        /// </summary>
        /// <param name="position">
        ///     The target position .
        /// </param>
        /// <returns>
        ///     The project object, or <c>null</c> if no object was found at the specified position.
        /// </returns>
        public MSBuildObject Find(Position position)
        {
            // Internally, we always use 1-based indexing because this is what the System.Xml APIs use (and I'd rather keep things simple).
            position = position.ToOneBased();

            // Short-circuit.
            if (_objectsByStartPosition.TryGetValue(position, out MSBuildObject exactMatch))
                return exactMatch;

            // TODO: Use binary search.

            Range lastMatchingRange = Range.Zero;
            foreach (Range objectRange in _objectRanges)
            {
                if (lastMatchingRange != Range.Zero && objectRange.End > lastMatchingRange.End)
                    break; // We've moved past the end of the last matching range.

                if (objectRange.Contains(position))
                    lastMatchingRange = objectRange;
            }
            if (lastMatchingRange == Range.Zero)
                return null;

            return _objectsByStartPosition[lastMatchingRange.Start];
        }

        /// <summary>
        ///     Determine whether an element is located in the current project.
        /// </summary>
        /// <param name="element">
        ///     The project element.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the element is from the current project; otherwise, <c>false</c>.
        /// </returns>
        bool IsFromCurrentProject(ProjectElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            if (element.Location == null)
                return false;

            return string.Equals(element.Location.File, _projectFile, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Add all targets defined in the project.
        /// </summary>
        void AddTargets()
        {
            foreach (ProjectTargetElement target in _project.Xml.Targets)
            {
                if (IsFromCurrentProject(target))
                    AddTarget(target);
            }
        }

        /// <summary>
        ///     Add a target.
        /// </summary>
        /// <param name="target">
        ///     The target's declaring <see cref="ProjectTargetElement"/>.
        /// </param>
        void AddTarget(ProjectTargetElement target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            XmlLocation targetLocation = _projectXmlLocator.Inspect(
                target.Location.ToNative()
            );
            if (targetLocation == null)
                return;

            if (!targetLocation.IsElement(out XSElement targetElement))
                return;

            Add(
                new MSBuildTarget(target, targetElement)
            );
        }

        /// <summary>
        ///     Add all properties defined in the project.
        /// </summary>
        void AddProperties()
        {
            foreach (ProjectPropertyElement property in _project.Xml.Properties)
            {
                if (IsFromCurrentProject(property))
                    AddProperty(property);
            }
        }

        /// <summary>
        ///     Add a property.
        /// </summary>
        /// <param name="property">
        ///     The property's declaring <see cref="ProjectPropertyElement"/>.
        /// </param>
        void AddProperty(ProjectPropertyElement property)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            XmlLocation propertyLocation = _projectXmlLocator.Inspect(
                property.Location.ToNative()
            );
            if (propertyLocation == null)
                return;

            if (!propertyLocation.IsElement(out XSElement propertyElement))
                return;

            ProjectProperty evaluatedProperty = _project.GetProperty(property.Name);
            if (evaluatedProperty != null)
            {
                Add(
                    new MSBuildProperty(evaluatedProperty, property, propertyElement)
                );
            }
            else
            {
                Add(
                    new MSBuildUnusedProperty(property, propertyElement)
                );
            }
        }

        /// <summary>
        ///     Add all items defined in the project.
        /// </summary>
        void AddItems()
        {
            // First, map each item to the element in the XML from where it originates.
            var itemsByXml = new Dictionary<ProjectItemElement, List<ProjectItem>>();
            foreach (ProjectItem item in _project.ItemsIgnoringCondition)
            {
                // Must be declared in main project file.
                if (!IsFromCurrentProject(item.Xml))
                    continue;

                if (!itemsByXml.TryGetValue(item.Xml, out List<ProjectItem> itemsFromXml))
                {
                    itemsFromXml = new List<ProjectItem>();
                    itemsByXml.Add(item.Xml, itemsFromXml);
                }

                itemsFromXml.Add(item);
            }

            // Now process item elements and their associated items.
            HashSet<ProjectItem> usedItems = new HashSet<ProjectItem>(_project.Items);
            foreach (ProjectItemElement itemXml in itemsByXml.Keys)
            {
                Position itemStart = itemXml.Location.ToNative();

                XmlLocation itemLocation = _projectXmlLocator.Inspect(itemStart);
                if (itemLocation == null)
                    continue;

                if (!itemLocation.IsElement(out XSElement itemElement))
                    continue;

                if (!itemsByXml.TryGetValue(itemXml, out List<ProjectItem> itemsFromXml)) // AF: Should not happen.
                    throw new InvalidOperationException($"Found item XML at {itemLocation.Node.Range} with no corresponding items in the MSBuild project (irrespective of condition).");

                if (usedItems.Contains(itemsFromXml[0]))
                {
                    Add(
                        new MSBuildItemGroup(itemsByXml[itemXml], itemXml, itemElement)
                    );
                }
                else
                {
                    Add(
                        new MSBuildUnusedItemGroup(itemsByXml[itemXml], itemXml, itemElement)
                    );
                }
            }
        }

        /// <summary>
        ///     Add all imports defined in the project.
        /// </summary>
        /// <remarks>
        ///     Currently, this doesn't capture imports whose condition evaluates to false.
        /// </remarks>
        void AddImports()
        {
            HashSet<ProjectImportElement> resolvedImportElements = new HashSet<ProjectImportElement>();
            var importsBySdk =
                _project.Imports.Where(import =>
                    IsFromCurrentProject(import.ImportingElement)
                    &&
                    import.ImportedProject.Location != null
                )
                .GroupBy(
                    import => import.ImportingElement.Sdk
                );
            foreach (var importGroup in importsBySdk)
            {
                string importingSdk = importGroup.Key;
                if (importingSdk != string.Empty)
                    AddSdkImport(importGroup);
                else
                    AddImport(importGroup);

                resolvedImportElements.UnionWith(importGroup.Select(
                    resolvedImport => resolvedImport.ImportingElement
                ));
            }

            HashSet<ProjectImportElement> unresolvedImportElements = new HashSet<ProjectImportElement>(_project.Xml.Imports);
            unresolvedImportElements.ExceptWith(resolvedImportElements);

            foreach (ProjectImportElement importElement in unresolvedImportElements)
            {
                if (!string.IsNullOrWhiteSpace(importElement.Sdk))
                    AddUnresolvedSdkImport(importElement);
                else
                    AddUnresolvedImport(importElement);
            }
        }

        /// <summary>
        ///     Add an SDK-style import.
        /// </summary>
        /// <param name="resolvedImports">
        ///     The resolved imports resulting from the import declaration.
        /// </param>
        void AddSdkImport(IEnumerable<ResolvedImport> resolvedImports)
        {
            if (resolvedImports == null)
                throw new ArgumentNullException(nameof(resolvedImports));

            ResolvedImport firstImport = resolvedImports.First();
            Position importStart = firstImport.ImportingElement.Location.ToNative();

            // If the Sdk attribute is on the Project element rather than an import element, then the location reported by MSBuild will be invalid (go figure).
            if (importStart == Position.Invalid)
                importStart = Position.Origin;

            XmlLocation importLocation = _projectXmlLocator.Inspect(importStart);
            if (importLocation == null)
                return;

            if (!importLocation.IsElement(out XSElement importElement))
                return;

            XSAttribute sdkAttribute = importElement["Sdk"];
            if (sdkAttribute == null)
                return;

            Add(
                new MSBuildSdkImport(resolvedImports.ToArray(), sdkAttribute)
            );
        }

        /// <summary>
        ///     Add a regular-style import.
        /// </summary>
        /// <param name="resolvedImports">
        ///     The resolved imports resulting from the import declaration.
        /// </param>
        void AddImport(IEnumerable<ResolvedImport> resolvedImports)
        {
            if (resolvedImports == null)
                throw new ArgumentNullException(nameof(resolvedImports));

            var importsByImportingElement = resolvedImports.GroupBy(import => import.ImportingElement);
            foreach (var importsForImportingElement in importsByImportingElement)
            {
                Position importStart = importsForImportingElement.Key.Location.ToNative();

                XmlLocation importLocation = _projectXmlLocator.Inspect(importStart);
                if (importLocation == null)
                    continue;

                if (!importLocation.IsElement(out XSElement importElement))
                    continue;

                Add(
                    new MSBuildImport(importsForImportingElement.ToArray(), importElement)
                );
            }
        }

        /// <summary>
        ///     Add an unresolved SDK-style import (i.e. condition is false).
        /// </summary>
        /// <param name="import">
        ///     The declaring import element.
        /// </param>
        void AddUnresolvedSdkImport(ProjectImportElement import)
        {
            if (import == null)
                throw new ArgumentNullException(nameof(import));

            Position importStart = import.Location.ToNative();

            XmlLocation importLocation = _projectXmlLocator.Inspect(importStart);
            if (importLocation == null)
                return;

            if (!importLocation.IsElement(out XSElement importElement))
                return;

            XSAttribute sdkAttribute = importElement["Sdk"];

            Add(
                new MSBuildUnresolvedSdkImport(import, sdkAttribute)
            );
        }

        /// <summary>
        ///     Add an unresolved regular-style import (i.e. condition is false).
        /// </summary>
        /// <param name="import">
        ///     The declaring import element.
        /// </param>
        void AddUnresolvedImport(ProjectImportElement import)
        {
            if (import == null)
                throw new ArgumentNullException(nameof(import));

            Position importStart = import.Location.ToNative();

            XmlLocation importLocation = _projectXmlLocator.Inspect(importStart);
            if (importLocation == null)
                return;

            if (!importLocation.IsElement(out XSElement importElement))
                return;

            Add(
                new MSBuildUnresolvedImport(import, importElement)
            );
        }

        /// <summary>
        ///     Add the MSBuild object to the locator.
        /// </summary>
        /// <param name="msbuildObject">
        ///     The <see cref="MSBuildObject"/>.
        /// </param>
        void Add(MSBuildObject msbuildObject)
        {
            if (msbuildObject == null)
                throw new ArgumentNullException(nameof(msbuildObject));

            // Rarely, we get a duplicate item-group if the item group element is empty.
            // TODO: Figure out why.

            if (_objectsByStartPosition.TryGetValue(msbuildObject.XmlRange.Start, out MSBuildObject dupe))
            {
                Serilog.Log.Information("Found duplicate {0} (vs {1}) at {2} (vs {3}). Same underlying object: {IdentityMatch}",
                    msbuildObject.Kind,
                    dupe.Kind,
                    msbuildObject.XmlRange,
                    dupe.XmlRange,
                    msbuildObject.IsSameUnderlyingObject(dupe)
                );

                if (msbuildObject is MSBuildItemGroup itemGroup1 && dupe is MSBuildItemGroup itemGroup2)
                {
                    Serilog.Log.Information("Duplicate items are {Spec1} ({XmlSpec1}) vs {Spec2} ({XmlSpec2}) => {@Spec1Location} vs {@Spec2Location}",
                        itemGroup1.FirstInclude,
                        itemGroup1.FirstItem.Xml.Include,
                        itemGroup2.FirstInclude,
                        itemGroup2.FirstItem.Xml.Include,
                        itemGroup1.FirstItem.Xml.Location,
                        itemGroup2.FirstItem.Xml.Location
                    );
                }
            }

            _objectRanges.Add(msbuildObject.XmlRange);
            _objectsByStartPosition.Add(msbuildObject.XmlRange.Start, msbuildObject);
        }
    }
}
