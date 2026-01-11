using System;
using System.Collections.Generic;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    using Microsoft.Language.Xml;
    using Microsoft.VisualStudio.SolutionPersistence.Model;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using Utilities;

    /// <summary>
    ///     A facility for looking up solution members by textual location.
    /// </summary>
    public class VsSolutionObjectLocator
    {
        /// <summary>
        ///     The ranges for all XML objects in the document with positional annotations.
        /// </summary>
        /// <remarks>
        ///     Sorted by range comparison (effectively, this means document order).
        /// </remarks>
        readonly List<Range> _objectRanges = new List<Range>();

        /// <summary>
        ///     All objects in the solution, keyed by starting position.
        /// </summary>
        /// <remarks>
        ///     Sorted by range comparison.
        /// </remarks>
        readonly SortedDictionary<Position, VsSolutionObject> _objectsByStartPosition = new SortedDictionary<Position, VsSolutionObject>();

        /// <summary>
        ///     The solution.
        /// </summary>
        readonly VsSolution _solution;

        /// <summary>
        ///     The solution XML.
        /// </summary>
        readonly XmlLocator _solutionXmlLocator;

        /// <summary>
        ///     The position-lookup for the solution XML.
        /// </summary>
        readonly TextPositions _xmlPositions;

        /// <summary>
        ///     Create a new <see cref="VsSolutionObjectLocator"/>.
        /// </summary>
        /// <param name="solution">
        ///     The solution.
        /// </param>
        /// <param name="solutionXmlLocator">
        ///     The <see cref="XmlLocator"/> for the solution XML.
        /// </param>
        /// <param name="xmlPositions">
        ///     The position-lookup for the solution XML.
        /// </param>
        public VsSolutionObjectLocator(VsSolution solution, XmlLocator solutionXmlLocator, TextPositions xmlPositions)
        {
            if (solution == null)
                throw new ArgumentNullException(nameof(solution));

            if (solutionXmlLocator == null)
                throw new ArgumentNullException(nameof(solutionXmlLocator));

            if (xmlPositions == null)
                throw new ArgumentNullException(nameof(xmlPositions));

            _solution = solution;

            _solutionXmlLocator = solutionXmlLocator;
            _xmlPositions = xmlPositions;

            ScanSolution();

            _objectRanges.Sort();
        }

        /// <summary>
        ///     All known VsSolution objects.
        /// </summary>
        public IEnumerable<VsSolutionObject> AllObjects => _objectsByStartPosition.Values;

        /// <summary>
        ///     Find the solution object (if any) at the specified position.
        /// </summary>
        /// <param name="position">
        ///     The target position .
        /// </param>
        /// <returns>
        ///     The solution object, or <c>null</c> if no object was found at the specified position.
        /// </returns>
        public VsSolutionObject? Find(Position position)
        {
            // Internally, we always use 1-based indexing because this is what the System.Xml APIs use (and I'd rather keep things simple).
            position = position.ToOneBased();

            // Short-circuit.
            if (_objectsByStartPosition.TryGetValue(position, out VsSolutionObject? exactMatch))
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
        ///     Scan the solution XML and match up each element to its corresponding object in the VS solution model.
        /// </summary>
        void ScanSolution()
        {
            XmlLocation rootLocation = _solutionXmlLocator.Inspect(_solutionXmlLocator.Xml.RootSyntax.AsNode.SpanStart);
            if (rootLocation == null)
                return;

            XSElement solutionElement;
            if (!rootLocation.IsElement(out solutionElement))
                return;

            if (solutionElement.Name != XmlSolutionSchema.ElementNames.Solution)
                return;

            var solutionRoot = new VsSolutionRoot(_solution, _solution.Model, solutionElement);
            Add(solutionRoot);

            // TODO: Add logging for each success/failure when matching SLNX elements to their associated VsSolution model.
            // TODO: Consider organising VsSolutionFolder objects into a hierarchy.

            foreach (IXmlElementSyntax folderElementSyntax in solutionElement.ElementNode.Elements.Where(element => element.Name == XmlSolutionSchema.ElementNames.Folder))
            {
                XmlLocation folderLocation = _solutionXmlLocator.Inspect(folderElementSyntax.AsNode.SpanStart);
                if (folderLocation == null)
                    continue;

                XSElement? folderElement;
                if (!folderLocation.IsElement(out folderElement))
                    continue;

                if (folderElement.Name != XmlSolutionSchema.ElementNames.Folder)
                    continue;

                string? folderPath = folderElementSyntax.AsElement.Attributes.Where(attribute => attribute.Key == XmlSolutionSchema.AttributeNames.Name).Select(attribute => attribute.Value).FirstOrDefault();
                if (folderPath == null)
                    continue;

                SolutionFolderModel? folderModel = _solution.Model.FindFolder(folderPath);
                if (folderModel == null)
                    continue;

                var folder = new VsSolutionFolder(_solution, folderModel, declaringXml: folderElement);
                Add(folder);

                ScanProjects(parentElement: folderElement);
            }

            ScanProjects(parentElement: solutionElement);
        }

        /// <summary>
        ///     Scan the solution XML and match up each element to its corresponding object in the VS solution model.
        /// </summary>
        void ScanProjects(XSElement parentElement)
        {
            if (parentElement == null)
                throw new ArgumentNullException(nameof(parentElement));

            // TODO: Add logging for each success/failure when matching SLNX elements to their associated VsSolution model.

            foreach (IXmlElementSyntax projectElementSyntax in parentElement.ElementNode.Elements.Where(element => element.Name == XmlSolutionSchema.ElementNames.Project))
            {
                XmlLocation projectLocation = _solutionXmlLocator.Inspect(projectElementSyntax.AsNode.SpanStart);
                if (projectLocation == null)
                    continue;

                XSElement? projectElement;
                if (!projectLocation.IsElement(out projectElement))
                    continue;

                if (projectElement.Name != XmlSolutionSchema.ElementNames.Project)
                    continue;

                string? projectPath = projectElementSyntax.AsElement.Attributes.Where(attribute => attribute.Key == XmlSolutionSchema.AttributeNames.Path).Select(attribute => attribute.Value).FirstOrDefault();
                if (projectPath == null)
                    continue;

                SolutionProjectModel? projectModel;

                // UGLY: Buggy path-matching behaviour (platform-specific directory separators) in the current version of the VS solution-model library.
                projectPath = projectPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                projectModel = _solution.Model.SolutionProjects.FirstOrDefault(
                    projectModel => projectModel.FilePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar) == projectPath
                );
                if (projectModel == null)
                    continue;

                var project = new VsSolutionProject(_solution, projectModel, declaringXml: projectElement);
                Add(project);
            }
        }


        /// <summary>
        ///     Add the solution object to the locator.
        /// </summary>
        /// <param name="solutionObject">
        ///     The <see cref="VsSolutionObject"/>.
        /// </param>
        void Add(VsSolutionObject solutionObject)
        {
            if (solutionObject == null)
                throw new ArgumentNullException(nameof(solutionObject));

            if (_objectsByStartPosition.TryGetValue(solutionObject.XmlRange.Start, out VsSolutionObject? dupe))
            {
                Serilog.Log.Information("Found duplicate {0} (vs {1}) at {2} (vs {3}). Same underlying object: {IdentityMatch}",
                    solutionObject.Kind,
                    dupe.Kind,
                    solutionObject.XmlRange,
                    dupe.XmlRange,
                    solutionObject.IsSameUnderlyingObject(dupe)
                );
            }

            _objectRanges.Add(solutionObject.XmlRange);
            _objectsByStartPosition.Add(solutionObject.XmlRange.Start, solutionObject);
        }
    }

    static class XmlSolutionSchema
    {
        public static class ElementNames
        {
            public static readonly string Folder = "Folder";
            public static readonly string Project = "Project";
            public static readonly string Solution = "Solution";
        }

        public static class AttributeNames
        {
            public static readonly string Name = "Name";
            public static readonly string Path = "Path";
        }
    }
}
