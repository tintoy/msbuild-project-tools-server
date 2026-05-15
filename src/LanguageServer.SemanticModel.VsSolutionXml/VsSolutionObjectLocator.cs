using System;
using System.Collections.Generic;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    using Microsoft.Language.Xml;
    using Microsoft.VisualStudio.SolutionPersistence.Model;
    using Serilog;
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
        /// <param name="logger">
        ///     An optional <see cref="ILogger"/> for diagnostic scenarios (e.g. during the initial solution-scan).
        /// </param>
        public VsSolutionObjectLocator(VsSolution solution, XmlLocator solutionXmlLocator, TextPositions xmlPositions, ILogger? logger = null)
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

            logger = logger?.ForContext<VsSolutionObjectLocator>() ?? Serilog.Core.Logger.None;

            ScanSolution(logger);

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
        /// <param name="logger">
        ///     A logger for diagnostic scenarios.
        /// </param>
        void ScanSolution(ILogger logger)
        {
            logger.Information("VsSolutionObjectLocator.ScanSolution: Starting...");

            XmlLocation rootLocation = _solutionXmlLocator.Inspect(_solutionXmlLocator.Xml.RootSyntax.AsNode.SpanStart);
            if (rootLocation == null)
            {
                logger.Warning("VsSolutionObjectLocator.ScanSolution: Bailed out (no root location).");

                return;
            }

            XSElement solutionElement;
            if (!rootLocation.IsElement(out solutionElement))
            {
                logger.Warning("VsSolutionObjectLocator.ScanSolution: Bailed out (root location is not an element).");

                return;
            }

            if (solutionElement.Name != XmlSolutionSchema.ElementNames.Solution)
            {
                logger.Warning("VsSolutionObjectLocator.ScanSolution: Bailed out (root location is not a Solution element).");

                return;
            }

            var solutionRoot = new VsSolutionRoot(_solution, _solution.Model, solutionElement);
            Add(solutionRoot, logger);

            // TODO: Add logging for each success/failure when matching SLNX elements to their associated VsSolution model.
            // TODO: Consider organising VsSolutionFolder objects into a hierarchy.

            foreach (IXmlElementSyntax folderElementSyntax in solutionElement.ElementNode.Elements.Where(element => element.Name == XmlSolutionSchema.ElementNames.Folder))
            {
                logger.Information("VsSolutionObjectLocator.ScanSolution: Scanning folder element syntax @ {ElementSyntaxSpan}...", folderElementSyntax.AsNode.Span);

                XmlLocation folderLocation = _solutionXmlLocator.Inspect(folderElementSyntax.AsNode.SpanStart);
                if (folderLocation == null)
                {
                    logger.Warning("VsSolutionObjectLocator.ScanSolution: Skipped folder element syntax @ {ElementSyntaxSpan} (no corresponding XmlLocation for this syntax span).", folderElementSyntax.AsNode.Span);

                    continue;
                }

                XSElement? folderElement;
                if (!folderLocation.IsElement(out folderElement))
                {
                    logger.Warning("VsSolutionObjectLocator.ScanSolution: Skipped folder element @ {ElementRange} (corresponding XmlLocation does not represent an XML element).", folderElement.Range);

                    continue;
                }

                if (folderElement.Name != XmlSolutionSchema.ElementNames.Folder)
                {
                    logger.Warning("VsSolutionObjectLocator.ScanSolution: Skipped folder element @ {ElementRange} (corresponding XmlLocation does not represent a Folder element).", folderElement.Range);

                    continue;
                }

                string? folderPath = folderElementSyntax.AsElement.Attributes.Where(attribute => attribute.Key == XmlSolutionSchema.AttributeNames.Name).Select(attribute => attribute.Value).FirstOrDefault();
                if (folderPath == null)
                {
                    logger.Warning("VsSolutionObjectLocator.ScanSolution: Skipped folder element @ {ElementRange} (element does not have a Name attribute).", folderElement.Range);

                    continue;
                }

                SolutionFolderModel? folderModel = _solution.Model.FindFolder(folderPath);
                if (folderModel == null)
                {
                    logger.Warning("VsSolutionObjectLocator.ScanSolution: Skipped folder element @ {ElementRange} (no corresponding SolutionFolderModel for path {FolderPath}).", folderElement.Range, folderPath);
                    foreach (SolutionFolderModel existingFolder in _solution.Model.SolutionFolders)
                        logger.Debug("VsSolutionObjectLocator.ScanSolution: Existing solution folder with path {FolderPath}...", existingFolder.Path);

                    continue;
                }

                var folder = new VsSolutionFolder(_solution, folderModel, declaringXml: folderElement);
                Add(folder, logger);

                ScanItems(parentFolder: folder, parentFolderElement: folderElement, logger);
                ScanProjects(parentElement: folderElement, logger);
            }

            ScanProjects(parentElement: solutionElement, logger);
        }

        /// <summary>
        ///     Scan the solution XML and match up each element to its corresponding object in the VS solution model.
        /// </summary>
        /// <param name="parentElement">
        ///     An <see cref="XSElement"/> representing the parent Solution or Folder element.
        /// </param>
        /// <param name="logger">
        ///     A logger for diagnostic scenarios.
        /// </param>
        void ScanProjects(XSElement parentElement, ILogger logger)
        {
            if (parentElement == null)
                throw new ArgumentNullException(nameof(parentElement));

            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

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
                Add(project, logger);
            }
        }

        /// <summary>
        ///     Scan the solution XML and match up each element to its corresponding object in the VS solution model.
        /// </summary>
        /// <param name="parentFolder">
        ///     A <see cref="VsSolutionFolder"/> representing the parent folder.
        /// </param>
        /// <param name="parentFolderElement">
        ///     An <see cref="XSElement"/> representing the parent Folder element.
        /// </param>
        /// <param name="logger">
        ///     A logger for diagnostic scenarios.
        /// </param>
        void ScanItems(VsSolutionFolder parentFolder, XSElement parentFolderElement, ILogger logger)
        {
            if (parentFolder == null)
                throw new ArgumentNullException(nameof(parentFolder));

            if (parentFolderElement == null)
                throw new ArgumentNullException(nameof(parentFolderElement));

            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            foreach (IXmlElementSyntax fileElementSyntax in parentFolderElement.ElementNode.Elements.Where(element => element.Name == XmlSolutionSchema.ElementNames.File))
            {
                XmlLocation fileLocation = _solutionXmlLocator.Inspect(fileElementSyntax.AsNode.SpanStart);
                if (fileLocation == null)
                    continue;

                XSElement? fileElement;
                if (!fileLocation.IsElement(out fileElement))
                    continue;

                if (fileElement.Name != XmlSolutionSchema.ElementNames.File)
                    continue;

                string? filePath = fileElementSyntax.AsElement.Attributes.Where(attribute => attribute.Key == XmlSolutionSchema.AttributeNames.Path).Select(attribute => attribute.Value).FirstOrDefault();
                if (filePath == null)
                    continue;

                // UGLY: Buggy path-matching behaviour (platform-specific directory separators) in the current version of the VS solution-model library.
                filePath = filePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

                if (parentFolder.Folder.Files == null || !parentFolder.Folder.Files.Contains(filePath, StringComparer.Ordinal))
                    continue;

                var file = new VsSolutionFile(_solution, parentFolder.Folder, relativePath: filePath, declaringXml: fileElement);
                Add(file, logger);
            }
        }

        /// <summary>
        ///     Add the solution object to the locator.
        /// </summary>
        /// <param name="solutionObject">
        ///     The <see cref="VsSolutionObject"/>.
        /// </param>
        /// <param name="logger">
        ///     A logger for diagnostic scenarios.
        /// </param>
        void Add(VsSolutionObject solutionObject, ILogger logger)
        {
            if (solutionObject == null)
                throw new ArgumentNullException(nameof(solutionObject));

            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            logger.Information("Add {SolutionObjectKind} {SolutionObjectName} @ {SolutionObjectRange}",
                solutionObject.Kind,
                solutionObject.Name,
                solutionObject.XmlRange
            );

            if (_objectsByStartPosition.TryGetValue(solutionObject.XmlRange.Start, out VsSolutionObject? dupe))
            {
                logger.Warning("Found duplicate {0} (vs {1}) at {2} (vs {3}). Same underlying object: {IdentityMatch}",
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
            public static readonly string File = "File";
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
