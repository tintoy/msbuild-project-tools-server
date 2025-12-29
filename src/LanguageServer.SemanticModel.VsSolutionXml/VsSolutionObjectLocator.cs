using System;
using System.Collections.Generic;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    using Microsoft.Language.Xml;
    using Microsoft.VisualStudio.SolutionPersistence.Model;
    using System.IO;
    using System.Linq;
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

        void ScanSolution()
        {
            IXmlElementSyntax solutionRootElementSyntax = _solutionXmlLocator.Xml.RootSyntax;
            
            XmlLocation rootLocation = _solutionXmlLocator.Inspect(solutionRootElementSyntax.AsNode.SpanStart);
            if (rootLocation == null)
                return;

            if (rootLocation.IsElement(out XSElement element))
            {
                if (element.Name != XmlSolutionSchema.ElementNames.Solution)
                    return;

                var solutionRoot = new VsSolutionRoot(_solution, _solution.Model, element);
                Add(solutionRoot);

                ScanFolders(solutionRoot, solutionRootElementSyntax);
            }
        }

        void ScanFolders(VsSolutionRoot solutionRoot, IXmlElementSyntax solutionElementSyntax)
        {
            if (solutionRoot == null)
                throw new ArgumentNullException(nameof(solutionRoot));

            if (solutionElementSyntax == null)
                throw new ArgumentNullException(nameof(solutionElementSyntax));

            foreach (IXmlElementSyntax folderElementSyntax in solutionElementSyntax.Elements.Where(element => element.Name == XmlSolutionSchema.ElementNames.Folder))
            {
                XmlLocation folderLocation = _solutionXmlLocator.Inspect(folderElementSyntax.AsNode.SpanStart);
                if (folderLocation == null)
                    continue;

                XSElement? folderElement;
                if (!folderLocation.IsElement(out folderElement))
                    continue;

                if (folderElement.Name != XmlSolutionSchema.ElementNames.Folder)
                    continue;

                string? folderName = folderElementSyntax.AsElement.Attributes.Where(attribute => attribute.Key == XmlSolutionSchema.AttributeNames.Name).Select(attribute => attribute.Value).FirstOrDefault();
                if (folderName == null)
                    continue;

                SolutionFolderModel? folderModel = _solution.Model.FindFolder(folderName);
                if (folderModel == null)
                    continue;

                var folder = new VsSolutionFolder(_solution, folderModel, folderElement);
                Add(folder);

                // TODO: Handle nested folders.
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
            public static readonly string Solution = "Solution";
            public static readonly string Folder = "Folder";
        }

        public static class AttributeNames
        {
            public static readonly string Name = "Name";
        }
    }
}
